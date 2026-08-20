using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using RigToggle.Core;
using RigToggle.Windows;
using Xunit;

namespace RigToggle.Windows.Tests;

/// <summary>
/// D-05/D-06 (25-CONTEXT.md): proves the single-instance guard end-to-end by launching
/// the real built <c>RigToggle.App.exe</c> as a genuine child process -- not a stub, not
/// a shim, not a purpose-built script. This is the plan that turns Plans 25-01/25-02
/// from "wired and compiling" into "observed working," and the only place ROADMAP
/// success criteria 1, 2, and 3 (INSTANCE-01, INSTANCE-02, UPDATE-07) are actually
/// satisfied rather than prepared for.
///
/// Flagged deviation from D-05, recorded here per 25-03-PLAN.md's objective: D-05's
/// literal text names <c>RigToggle.Tests</c> as the home for these tests. They live in
/// <c>RigToggle.Windows.Tests</c> instead -- <c>RigToggle.Tests</c> targets plain
/// <c>net10.0</c> specifically so it stays buildable/runnable on non-Windows machines
/// (a documented, pre-existing project invariant), and a test that launches a WinForms
/// exe and reads <c>MainWindowHandle</c> is unambiguously Windows-only. The substance of
/// D-05 (automated xUnit, real child process, not a manual script) is fully honoured;
/// only the project name differs.
///
/// All six tests -- the two survival tests here, plus the restore test and three bypass
/// tests added by Plan 25-03 Task 2 -- live in this ONE class. xUnit runs test cases
/// inside a single class sequentially but parallelises across classes; these tests all
/// contend for the same machine-wide named kernel object (the single-instance mutex),
/// so splitting them across classes would guarantee exactly the intermittent red this
/// phase exists to prevent.
///
/// These tests cannot execute in a Linux build environment: this project's testhost
/// requires the <c>Microsoft.WindowsDesktop.App</c> runtime, which is absent there -- a
/// pre-existing property of this repository (25-01/25-02-SUMMARY.md), not something
/// this plan introduces. Compilation is verifiable everywhere; execution is Plan
/// 25-03 Task 3's operator checkpoint on real Windows hardware.
/// </summary>
public sealed class SingleInstanceProcessTests : IDisposable
{
    /// <summary>Process name (no extension) every survival/concurrency test enumerates by, rather than trusting tracked handles -- this catches an orphan the test never started.</summary>
    private const string AppProcessName = "RigToggle.App";

    /// <summary>Iteration count for <see cref="RapidRelaunch_ExactlyOneProcessSurvives"/> -- ROADMAP success criterion 1 / INSTANCE-01.</summary>
    private const int RapidRelaunchIterations = 10;

    /// <summary>Round count for <see cref="TightRaceLaunch_ExactlyOneProcessSurvives"/> -- PITFALLS.md Pitfall 8.</summary>
    private const int TightRaceRounds = 3;

    /// <summary>Bound on <see cref="SingleInstanceGuard.WaitForInstanceReady(TimeSpan, string?)"/> calls in this file.</summary>
    private static readonly TimeSpan ReadinessTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Bound on every per-launch settle wait (duplicate exit, bypass exit) -- generous enough for a cold first start on a loaded CI runner.</summary>
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Bound on the hidden-window-restore poll in Task 2's restore test.</summary>
    private static readonly TimeSpan WindowRestoreTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Poll interval used by every bounded poll loop in this file (never a fixed sleep used as the synchronisation mechanism itself).</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private static readonly string ExePath = ResolveExePath();

    private readonly List<Process> _startedProcesses = new();

    /// <summary>
    /// Reads the <c>RigToggleAppExePath</c> assembly-metadata value published by
    /// RigToggle.Windows.Tests.csproj and asserts the file exists. A missing exe fails
    /// loudly and actionably -- naming the exact build command to run -- never
    /// silently passing (T-25-13).
    /// </summary>
    private static string ResolveExePath()
    {
        string? path = typeof(SingleInstanceProcessTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "RigToggleAppExePath")
            ?.Value;

        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidOperationException(
                "RigToggleAppExePath assembly metadata is missing from RigToggle.Windows.Tests.csproj.");
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"RigToggle.App.exe was not found at '{path}'. Build it first: " +
                "dotnet build RigToggle.sln -c Release -p:EnableWindowsTargeting=true");
        }

        return path;
    }

    /// <summary>
    /// Starts the real built exe as a child process, tracks it for teardown in
    /// <see cref="Dispose"/>, and returns the live <see cref="Process"/>.
    /// </summary>
    private Process StartApp(params string[] args)
    {
        var startInfo = new ProcessStartInfo(ExePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Process.Start returned null for '{ExePath}'.");

        _startedProcesses.Add(process);
        return process;
    }

    /// <summary>
    /// Kills the entire process tree of every process this instance started that is
    /// still running, and waits briefly for exit. Swallows only the exceptions that
    /// arise from a process that already exited -- a leaked instance would poison
    /// every later test in the run and, on CI, the job itself (T-25-10).
    /// </summary>
    public void Dispose()
    {
        foreach (Process process in _startedProcesses)
        {
            try
            {
                process.Refresh();
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit((int)SettleTimeout.TotalMilliseconds);
                }
            }
            catch (InvalidOperationException)
            {
                // Already exited between the HasExited check and Kill -- benign.
            }
            catch (Win32Exception)
            {
                // Process already exiting/exited at the OS level -- benign.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    /// <summary>
    /// INSTANCE-01, ROADMAP success criterion 1, D-05/D-06. Starts the primary
    /// tray-hidden, waits for its readiness handle, then rapid-relaunches
    /// <see cref="RapidRelaunchIterations"/> duplicates one at a time: each duplicate
    /// must exit inside the settle timeout with exit code 0. After the loop the
    /// primary must still be running, and exactly one process named
    /// <see cref="AppProcessName"/> must be alive with the primary's process id --
    /// enumerating by process name rather than trusting tracked handles catches an
    /// orphan this test never started.
    /// </summary>
    [Fact]
    public void RapidRelaunch_ExactlyOneProcessSurvives()
    {
        Process primary = StartApp("--tray");
        Assert.True(
            SingleInstanceGuard.WaitForInstanceReady(ReadinessTimeout),
            "The primary instance never published readiness, so the rest of this test would be meaningless.");

        for (int iteration = 0; iteration < RapidRelaunchIterations; iteration++)
        {
            Process duplicate = StartApp("--tray");
            bool exited = duplicate.WaitForExit((int)SettleTimeout.TotalMilliseconds);
            Assert.True(
                exited,
                $"Iteration {iteration}: duplicate launch did not exit within the settle timeout -- a duplicate that does not exit is a duplicate the guard failed to block.");
            Assert.Equal(0, duplicate.ExitCode);
        }

        primary.Refresh();
        Assert.False(primary.HasExited, "The primary instance exited unexpectedly during the rapid-relaunch loop.");

        Process[] survivors = Process.GetProcessesByName(AppProcessName);
        try
        {
            Process onlySurvivor = Assert.Single(survivors);
            Assert.Equal(primary.Id, onlySurvivor.Id);
        }
        finally
        {
            foreach (Process survivor in survivors)
            {
                survivor.Dispose();
            }
        }
    }

    /// <summary>
    /// PITFALLS.md Pitfall 8: iteration count alone (the test above) does not
    /// reproduce the tight-race case -- launching two processes back to back with NO
    /// readiness wait between them does, because the loser may signal before the
    /// winner is listening. Three independent rounds, each starting two fresh
    /// processes with no pre-existing primary, polling until at most one survives.
    /// Kills the survivor and waits for its exit before the next round so rounds do
    /// not contaminate each other.
    /// </summary>
    [Fact]
    public void TightRaceLaunch_ExactlyOneProcessSurvives()
    {
        for (int round = 0; round < TightRaceRounds; round++)
        {
            Process a = StartApp("--tray");
            Process b = StartApp("--tray");

            DateTime deadline = DateTime.UtcNow + SettleTimeout;
            int aliveCount;
            do
            {
                a.Refresh();
                b.Refresh();
                aliveCount = (a.HasExited ? 0 : 1) + (b.HasExited ? 0 : 1);
                if (aliveCount > 1 && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(PollInterval);
                }
            }
            while (aliveCount > 1 && DateTime.UtcNow < deadline);

            Process[] survivors = Process.GetProcessesByName(AppProcessName);
            try
            {
                Assert.True(
                    survivors.Length == 1,
                    survivors.Length > 1
                        ? $"Round {round}: {survivors.Length} processes alive -- the guard did not block the race."
                        : $"Round {round}: 0 processes alive -- both processes exited and the user got nothing.");

                Process survivor = survivors[0];
                survivor.Kill(entireProcessTree: true);
                Assert.True(
                    survivor.WaitForExit((int)SettleTimeout.TotalMilliseconds),
                    $"Round {round}: survivor did not exit after Kill within the settle timeout.");
            }
            finally
            {
                foreach (Process survivor in survivors)
                {
                    survivor.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// INSTANCE-02, ROADMAP success criterion 2, D-01. Starts the primary tray-hidden
    /// (so no window is ever shown) and asserts its <see cref="Process.MainWindowHandle"/>
    /// is <see cref="IntPtr.Zero"/> first -- this is the control, not decoration: a
    /// tray-hidden start that already shows a window invalidates the test's premise.
    /// Then a duplicate launch is started and must exit; the primary is polled,
    /// refreshed each round, until its handle becomes non-zero.
    ///
    /// A bounded poll (rather than a handle wait) is used here deliberately: there is
    /// no waitable kernel object for "another process's window became visible," so
    /// this is the one place in this file where polling is the only available
    /// mechanism -- it is still bounded and message-carrying, never a blind sleep.
    /// <see cref="Process.MainWindowHandle"/> returns a real handle only for a
    /// visible, top-level, owner-less window, so a transition from zero to non-zero is
    /// exactly the observable effect of the restore sequence's <c>Show()</c> call.
    ///
    /// KNOWN GAP (25-03 Task 3 operator checkpoint, real Windows hardware): this
    /// assertion proves <c>IsWindowVisible</c> flipped, which is necessary but NOT
    /// sufficient to prove the window actually became the foreground/active window --
    /// a never-shown (tray-hidden) window's first <c>Show()</c> is not gated by
    /// Windows' anti-focus-stealing heuristic the same way activating an
    /// ALREADY-VISIBLE minimized window is. This test passed on real Windows hardware
    /// while the real-world manual scenario below (an already-visible, minimized
    /// primary) still failed to restore -- see
    /// <see cref="DuplicateLaunch_RestoresMinimizedVisibleWindowToForeground"/> below
    /// for the stronger assertion that actually caught the regression.
    /// </summary>
    [Fact]
    public void DuplicateLaunch_RestoresHiddenWindowOfExistingInstance()
    {
        Process primary = StartApp("--tray");
        Assert.True(
            SingleInstanceGuard.WaitForInstanceReady(ReadinessTimeout),
            "The primary instance never published readiness, so the rest of this test would be meaningless.");

        primary.Refresh();
        Assert.True(
            primary.MainWindowHandle == IntPtr.Zero,
            "A tray-hidden start that already shows a window invalidates this test's premise.");

        Process duplicate = StartApp("--tray");
        bool duplicateExited = duplicate.WaitForExit((int)SettleTimeout.TotalMilliseconds);
        Assert.True(duplicateExited, "The duplicate launch did not exit within the settle timeout.");

        DateTime deadline = DateTime.UtcNow + WindowRestoreTimeout;
        IntPtr handle;
        do
        {
            primary.Refresh();
            handle = primary.MainWindowHandle;
            if (handle == IntPtr.Zero && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(PollInterval);
            }
        }
        while (handle == IntPtr.Zero && DateTime.UtcNow < deadline);

        Assert.True(
            handle != IntPtr.Zero,
            "MainWindowHandle never became non-zero within the window-restore timeout -- either the activation signal was lost in the startup race, or the window-procedure branch never fired.");
    }

    /// <summary>
    /// INSTANCE-02 regression test (25-03 Task 3 operator checkpoint, real Windows
    /// hardware): reproduces the actual manual scenario that caught a real bug the
    /// test above did not -- a primary started VISIBLE (not tray-hidden), minimized
    /// via the standard OS minimize (<c>ShowWindow(SW_MINIMIZE)</c>, matching a user
    /// clicking the taskbar minimize button, not <c>--tray</c>'s never-shown state),
    /// then a duplicate launch attempting to restore it. Unlike the test above, this
    /// asserts <see cref="NativeMethods.GetForegroundWindow"/> actually returns the
    /// primary's handle -- proving genuine foreground activation, not merely that
    /// <c>IsWindowVisible</c> is true. This is what actually caught the regression:
    /// Windows' anti-focus-stealing heuristic blocks a background process's own
    /// <c>Activate()</c>/<c>SetForegroundWindow</c> call on an already-visible window
    /// unless some process currently holds "may set foreground window" permission --
    /// see <c>ActivationSignal.BroadcastActivation</c>'s <c>AllowSetForegroundWindow</c>
    /// fix, which this test proves closes the gap.
    /// </summary>
    [Fact]
    public void DuplicateLaunch_RestoresMinimizedVisibleWindowToForeground()
    {
        Process primary = StartApp();
        Assert.True(
            SingleInstanceGuard.WaitForInstanceReady(ReadinessTimeout),
            "The primary instance never published readiness, so the rest of this test would be meaningless.");

        IntPtr primaryHandle = WaitForNonZeroMainWindowHandle(primary, WindowRestoreTimeout);
        Assert.True(
            primaryHandle != IntPtr.Zero,
            "The visibly-started primary never got a MainWindowHandle -- this test's premise (a real visible window to minimize) is broken.");

        Assert.True(
            NativeMethods.ShowWindow(primaryHandle, NativeMethods.SW_MINIMIZE),
            "ShowWindow(SW_MINIMIZE) returned false -- could not put the primary into the minimized-but-visible state this test needs to reproduce the manual repro.");

        DateTime minimizedDeadline = DateTime.UtcNow + WindowRestoreTimeout;
        while (!NativeMethods.IsIconic(primaryHandle) && DateTime.UtcNow < minimizedDeadline)
        {
            Thread.Sleep(PollInterval);
        }

        Assert.True(
            NativeMethods.IsIconic(primaryHandle),
            "The primary never actually became minimized (IsIconic) within the timeout.");

        Process duplicate = StartApp();
        bool duplicateExited = duplicate.WaitForExit((int)SettleTimeout.TotalMilliseconds);
        Assert.True(duplicateExited, "The duplicate launch did not exit within the settle timeout.");

        DateTime foregroundDeadline = DateTime.UtcNow + WindowRestoreTimeout;
        IntPtr foregroundWindow;
        do
        {
            foregroundWindow = NativeMethods.GetForegroundWindow();
            if (foregroundWindow != primaryHandle && DateTime.UtcNow < foregroundDeadline)
            {
                Thread.Sleep(PollInterval);
            }
        }
        while (foregroundWindow != primaryHandle && DateTime.UtcNow < foregroundDeadline);

        Assert.True(
            foregroundWindow == primaryHandle,
            "GetForegroundWindow() never returned the primary's handle within the window-restore timeout -- the window may have un-minimized without actually becoming the foreground/active window (the anti-focus-stealing gap this test exists to catch).");

        Assert.False(
            NativeMethods.IsIconic(primaryHandle),
            "The primary is still minimized (IsIconic) even though it became the foreground window -- WindowState never actually transitioned to Normal.");
    }

    /// <summary>
    /// Polls <see cref="Process.MainWindowHandle"/> until it is non-zero or the
    /// timeout elapses, refreshing the process handle each round. Used only by tests
    /// that start a VISIBLE (non-<c>--tray</c>) primary, where the window handle
    /// becomes available shortly after process start rather than immediately.
    /// </summary>
    private static IntPtr WaitForNonZeroMainWindowHandle(Process process, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        IntPtr handle;
        do
        {
            process.Refresh();
            handle = process.MainWindowHandle;
            if (handle == IntPtr.Zero && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(PollInterval);
            }
        }
        while (handle == IntPtr.Zero && DateTime.UtcNow < deadline);

        return handle;
    }

    /// <summary>
    /// UPDATE-07, ROADMAP success criterion 3, D-05. Acquires the guard inside this
    /// test process and asserts it is primary -- the test itself now holds the
    /// single-instance name, so any normal launch must lose. A bypass-flagged launch
    /// must still exit with <see cref="StartupArgs.ApplyUpdateBypassExitCode"/> while
    /// the name is held. The second half -- a normal launch under the identical held
    /// condition exiting with 0, specifically not the bypass code -- is what makes the
    /// first half mean something: it proves the two paths are distinguishable and
    /// that the bypass code was reached by the bypass branch, not by any process that
    /// merely exits. Finally asserts no process named <see cref="AppProcessName"/> is
    /// alive, proving the bypass launch did not leave a process behind.
    /// </summary>
    [Fact]
    public void ApplyUpdateBypass_RunsWhileGuardIsHeld()
    {
        using SingleInstanceGuard guard = SingleInstanceGuard.Acquire();
        Assert.True(
            guard.IsPrimaryInstance,
            "This test must hold the single-instance name as its control. If it is not primary, a real Rig Toggle instance is already running on this machine and this test's premise is broken.");

        Process bypassProcess = StartApp(StartupArgs.ApplyUpdateFlag, "token-one", "token-two", "token-three", "--tray");
        bool bypassExited = bypassProcess.WaitForExit((int)SettleTimeout.TotalMilliseconds);
        Assert.True(bypassExited, "The --apply-update process did not exit within the settle timeout while the guard was held.");
        Assert.Equal(StartupArgs.ApplyUpdateBypassExitCode, bypassProcess.ExitCode);

        Process normalProcess = StartApp("--tray");
        bool normalExited = normalProcess.WaitForExit((int)SettleTimeout.TotalMilliseconds);
        Assert.True(normalExited, "The normal launch did not exit within the settle timeout while the guard was held.");
        Assert.Equal(0, normalProcess.ExitCode);
        Assert.NotEqual(StartupArgs.ApplyUpdateBypassExitCode, normalProcess.ExitCode);

        Process[] survivors = Process.GetProcessesByName(AppProcessName);
        try
        {
            Assert.Empty(survivors);
        }
        finally
        {
            foreach (Process survivor in survivors)
            {
                survivor.Dispose();
            }
        }
    }

    /// <summary>
    /// The resolved UPDATE-07 idempotency edge. Phase 25's guarantee rests on
    /// <c>UpdateApplyEntryPoint</c> being side-effect-free by contract; plan 25-02
    /// enforces that with a source-level gate, and this test is the behavioural half
    /// of the same claim -- running the identical apply-update command line twice in
    /// sequence must both exit with the bypass code and leave no process behind.
    /// </summary>
    [Fact]
    public void ApplyUpdateBypass_IsIdempotentAndSideEffectFree()
    {
        Process first = StartApp(StartupArgs.ApplyUpdateFlag, "token-one", "--tray");
        bool firstExited = first.WaitForExit((int)SettleTimeout.TotalMilliseconds);
        Assert.True(firstExited, "The first --apply-update run did not exit within the settle timeout.");
        Assert.Equal(StartupArgs.ApplyUpdateBypassExitCode, first.ExitCode);

        Process second = StartApp(StartupArgs.ApplyUpdateFlag, "token-one", "--tray");
        bool secondExited = second.WaitForExit((int)SettleTimeout.TotalMilliseconds);
        Assert.True(secondExited, "The second, identical --apply-update run did not exit within the settle timeout.");
        Assert.Equal(StartupArgs.ApplyUpdateBypassExitCode, second.ExitCode);

        Process[] survivors = Process.GetProcessesByName(AppProcessName);
        try
        {
            Assert.Empty(survivors);
        }
        finally
        {
            foreach (Process survivor in survivors)
            {
                survivor.Dispose();
            }
        }
    }

    /// <summary>
    /// The resolved UPDATE-07 concurrency edge. Three concurrent apply-update launches
    /// alongside a live normal instance must all complete with the bypass exit code,
    /// while the primary remains undisturbed -- the unguarded concurrency is
    /// deliberate (it is the requirement, not an oversight); Phase 26 owns serialising
    /// the real apply logic once the body does real work.
    /// </summary>
    [Fact]
    public void ApplyUpdateBypass_ConcurrentInvocationsDoNotInterfere()
    {
        Process primary = StartApp("--tray");
        Assert.True(
            SingleInstanceGuard.WaitForInstanceReady(ReadinessTimeout),
            "The primary instance never published readiness, so the rest of this test would be meaningless.");

        Process bypass1 = StartApp(StartupArgs.ApplyUpdateFlag, "token-one", "--tray");
        Process bypass2 = StartApp(StartupArgs.ApplyUpdateFlag, "token-two", "--tray");
        Process bypass3 = StartApp(StartupArgs.ApplyUpdateFlag, "token-three", "--tray");

        foreach (Process bypass in new[] { bypass1, bypass2, bypass3 })
        {
            bool exited = bypass.WaitForExit((int)SettleTimeout.TotalMilliseconds);
            Assert.True(exited, "A concurrent --apply-update process did not exit within the settle timeout.");
            Assert.Equal(StartupArgs.ApplyUpdateBypassExitCode, bypass.ExitCode);
        }

        primary.Refresh();
        Assert.False(primary.HasExited, "The primary instance exited unexpectedly during the concurrent bypass launches.");

        Process[] survivors = Process.GetProcessesByName(AppProcessName);
        try
        {
            Process onlySurvivor = Assert.Single(survivors);
            Assert.Equal(primary.Id, onlySurvivor.Id);
        }
        finally
        {
            foreach (Process survivor in survivors)
            {
                survivor.Dispose();
            }
        }
    }
}
