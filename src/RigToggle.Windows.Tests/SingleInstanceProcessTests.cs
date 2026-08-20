using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using RigToggle.Core;
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

    /// <summary>Bound on <see cref="SingleInstanceGuard.WaitForInstanceReady(TimeSpan)"/> calls in this file.</summary>
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
}
