using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using RigToggle.Core;
using Xunit;

// 26-03 Task 3: this test class contends for the SAME machine-wide named kernel
// object (the single-instance mutex) and the same "RigToggle.App" process name as
// SingleInstanceProcessTests -- xUnit's default behaviour parallelises across test
// CLASSES within one assembly (only same-class test cases are serialised), so
// without this, the two classes' processes would race the real single-instance
// guard against each other, producing the exact kind of intermittent red this
// disable exists to prevent. This is an assembly-level attribute, so it can be
// declared here without editing SingleInstanceProcessTests.cs.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RigToggle.Windows.Tests;

/// <summary>
/// Real child-process proof of UpdateApplyEntryPoint.Run's swap mechanism
/// (UPDATE-05, plan 26-03 Task 3): success (backup retained), idempotency (a
/// second identical run is a no-op), the garbage-payload no-op, and the T-26-04
/// out-of-directory containment refusal. Reuses SingleInstanceProcessTests'
/// RigToggleAppExePath assembly-metadata lookup mechanism rather than inventing a
/// second one.
///
/// These tests cannot execute on Linux -- this project's testhost requires
/// Microsoft.WindowsDesktop.App, absent there (a pre-existing property of this
/// repository, not something this plan introduces). Compilation is verifiable
/// everywhere; execution happens in CI on windows-latest, exactly like
/// SingleInstanceProcessTests.
///
/// Two hazards this class's constructor/Dispose exist specifically to handle --
/// removing either would leak state into every subsequent test run AND into this
/// developer's own real app state:
/// (1) a SUCCESSFUL swap relaunches the target exe as a real, separate running app
///     instance -- Dispose enumerates and kills every surviving RigToggle.App
///     process, mirroring SingleInstanceProcessTests' own survivor discipline.
/// (2) the helper under test writes its marker into the REAL
///     %LocalAppData%\RigToggle\update-applied.json -- a path this test cannot
///     redirect because the helper resolves it itself -- so the constructor moves
///     any existing marker aside to a backup path, and Dispose deletes whatever
///     this test run left there before restoring the original file (if any).
/// </summary>
public sealed class UpdateApplyProcessTests : IDisposable
{
    private const string AppProcessName = "RigToggle.App";
    private const string TargetExeName = "RigToggle.App.exe";
    private const string StagedExeName = "RigToggle.App.update.exe";

    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(30);

    private static readonly string ExePath = ResolveExePath();

    private static readonly string RealMarkerPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RigToggle",
        "update-applied.json");

    private static readonly string RealMarkerBackupPath = RealMarkerPath + ".UpdateApplyProcessTests.bak";

    private readonly string _scratchDir;

    public UpdateApplyProcessTests()
    {
        _scratchDir = Path.Combine(Path.GetTempPath(), "RigToggleUpdateApplyTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_scratchDir);

        // Hazard 2: move any real, developer-owned marker aside before this test
        // class's own runs can overwrite it. If a backup already exists (a prior
        // run of this class crashed before Dispose ran), leave it alone rather
        // than clobbering the ORIGINAL real marker with an already-test-polluted one.
        if (File.Exists(RealMarkerPath) && !File.Exists(RealMarkerBackupPath))
        {
            File.Move(RealMarkerPath, RealMarkerBackupPath);
        }
    }

    public void Dispose()
    {
        // Hazard 1: a successful swap relaunches the target exe as a real app
        // instance -- kill every survivor before this test class exits.
        foreach (Process survivor in Process.GetProcessesByName(AppProcessName))
        {
            try
            {
                survivor.Kill();
                survivor.WaitForExit((int)SettleTimeout.TotalMilliseconds);
            }
            catch (InvalidOperationException)
            {
                // Already exited between enumeration and Kill -- benign.
            }
            catch (Win32Exception)
            {
                // Process already exiting/exited at the OS level -- benign.
            }
            finally
            {
                survivor.Dispose();
            }
        }

        // Hazard 2: delete whatever this test run left in the real marker path,
        // then restore whatever was really there before this test class ran.
        try
        {
            if (File.Exists(RealMarkerPath))
            {
                File.Delete(RealMarkerPath);
            }
        }
        catch
        {
            // Best-effort cleanup only -- must never mask a test's real failure.
        }

        if (File.Exists(RealMarkerBackupPath))
        {
            File.Move(RealMarkerBackupPath, RealMarkerPath, overwrite: true);
        }

        try
        {
            if (Directory.Exists(_scratchDir))
            {
                Directory.Delete(_scratchDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup only -- must never mask a test's real failure.
        }
    }

    private static string ResolveExePath()
    {
        string? path = typeof(UpdateApplyProcessTests).Assembly
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
    /// Copies the real built exe into this test's scratch directory twice --
    /// once as the target ("RigToggle.App.exe") and once as the staged payload
    /// ("RigToggle.App.update.exe") -- so the swap under test operates on
    /// disposable copies, never the real build output.
    /// </summary>
    private (string stagedPath, string targetPath) SeedScratchDirectory()
    {
        string targetPath = Path.Combine(_scratchDir, TargetExeName);
        string stagedPath = Path.Combine(_scratchDir, StagedExeName);
        File.Copy(ExePath, targetPath, overwrite: true);
        File.Copy(ExePath, stagedPath, overwrite: true);
        return (stagedPath, targetPath);
    }

    private Process StartApplyUpdate(params string[] applyUpdateArgs)
    {
        var startInfo = new ProcessStartInfo(ExePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add(StartupArgs.ApplyUpdateFlag);
        foreach (string arg in applyUpdateArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Process.Start returned null for '{ExePath}'.");
    }

    [Fact]
    public void ApplyUpdate_ValidPayload_SwapsAndRetainsBackup()
    {
        (string stagedPath, string targetPath) = SeedScratchDirectory();
        string backupPath = targetPath + ".bak";

        using Process process = StartApplyUpdate(stagedPath, targetPath, Environment.ProcessId.ToString(), "--tray");
        bool exited = process.WaitForExit((int)SettleTimeout.TotalMilliseconds);
        Assert.True(exited, "The --apply-update process did not exit within the settle timeout.");
        Assert.Equal(StartupArgs.ApplyUpdateBypassExitCode, process.ExitCode);

        Assert.True(File.Exists(targetPath), "The target path should still hold a file after a successful swap.");
        Assert.True(File.Exists(backupPath), "A .bak backup should exist after a successful swap.");
        Assert.False(File.Exists(stagedPath), "The staged file should be gone after a successful swap (moved into place).");
    }

    [Fact]
    public void ApplyUpdate_MalformedPayload_ExitsUnchangedAndTouchesNothing()
    {
        (string stagedPath, string targetPath) = SeedScratchDirectory();
        DateTime targetWriteTimeBefore = File.GetLastWriteTimeUtc(targetPath);

        using Process process = StartApplyUpdate("token-one");
        bool exited = process.WaitForExit((int)SettleTimeout.TotalMilliseconds);
        Assert.True(exited, "The malformed-payload process did not exit within the settle timeout.");
        Assert.Equal(StartupArgs.ApplyUpdateBypassExitCode, process.ExitCode);

        Assert.True(File.Exists(targetPath), "The target file must be untouched by a malformed payload.");
        Assert.True(File.Exists(stagedPath), "The staged file must be untouched by a malformed payload.");
        Assert.Equal(targetWriteTimeBefore, File.GetLastWriteTimeUtc(targetPath));
        Assert.False(File.Exists(targetPath + ".bak"), "No backup should be created for a malformed payload.");
    }

    [Fact]
    public void ApplyUpdate_TargetOutsideStagedDirectory_RefusedAndUntouched()
    {
        (string stagedPath, string targetPath) = SeedScratchDirectory();

        string outsideDir = Path.Combine(_scratchDir, "outside");
        Directory.CreateDirectory(outsideDir);
        string outsideTargetPath = Path.Combine(outsideDir, TargetExeName);
        File.Copy(ExePath, outsideTargetPath, overwrite: true);

        using Process process = StartApplyUpdate(stagedPath, outsideTargetPath, Environment.ProcessId.ToString());
        bool exited = process.WaitForExit((int)SettleTimeout.TotalMilliseconds);
        Assert.True(exited, "The out-of-directory payload process did not exit within the settle timeout.");
        Assert.Equal(StartupArgs.ApplyUpdateBypassExitCode, process.ExitCode);

        Assert.True(File.Exists(stagedPath), "The staged file must be untouched by a refused out-of-directory payload.");
        Assert.True(File.Exists(outsideTargetPath), "The out-of-directory target must be untouched.");
        Assert.False(File.Exists(outsideTargetPath + ".bak"), "No backup should be created for a refused payload.");
        Assert.True(File.Exists(targetPath), "The in-directory target (never named in this payload) must also be untouched.");
    }

    [Fact]
    public void ApplyUpdate_SameCommandLineRunTwice_SecondRunIsNoOp()
    {
        (string stagedPath, string targetPath) = SeedScratchDirectory();
        string backupPath = targetPath + ".bak";
        string pidToken = Environment.ProcessId.ToString();

        using Process first = StartApplyUpdate(stagedPath, targetPath, pidToken, "--tray");
        bool firstExited = first.WaitForExit((int)SettleTimeout.TotalMilliseconds);
        Assert.True(firstExited, "The first --apply-update run did not exit within the settle timeout.");
        Assert.Equal(StartupArgs.ApplyUpdateBypassExitCode, first.ExitCode);
        Assert.True(File.Exists(targetPath));
        Assert.True(File.Exists(backupPath));
        Assert.False(File.Exists(stagedPath));

        int survivorCountAfterFirst = Process.GetProcessesByName(AppProcessName).Length;

        // Identical command line, second run: the staged file no longer exists
        // (consumed by the first run), so Run's own File.Exists(stagedPath) check
        // must no-op this second invocation -- no second relaunch, no further
        // mutation of the target/backup that already resulted from the first run.
        using Process second = StartApplyUpdate(stagedPath, targetPath, pidToken, "--tray");
        bool secondExited = second.WaitForExit((int)SettleTimeout.TotalMilliseconds);
        Assert.True(secondExited, "The second, identical --apply-update run did not exit within the settle timeout.");
        Assert.Equal(StartupArgs.ApplyUpdateBypassExitCode, second.ExitCode);

        Process[] survivorsAfterSecond = Process.GetProcessesByName(AppProcessName);
        try
        {
            Assert.True(
                survivorsAfterSecond.Length <= survivorCountAfterFirst,
                "The second, no-op run must not have produced a second relaunch.");
        }
        finally
        {
            foreach (Process survivor in survivorsAfterSecond)
            {
                survivor.Dispose();
            }
        }
    }
}
