using System.Diagnostics;
using System.Threading;
using RigToggle.Core;
using RigToggle.Core.Models;
using RigToggle.Core.Persistence;

namespace RigToggle.App;

/// <summary>
/// The real relaunch-helper entry point for the app's own internal self-update
/// relaunch (UPDATE-04/UPDATE-07). IN-01: reached from the first branch of
/// Program.Main() after its two position-sensitive bootstrap calls --
/// <c>Application.SetColorMode(...)</c> and <c>ApplicationConfiguration.Initialize()</c>
/// (the latter's documented <c>SystemEvents</c> background-thread spin-up) -- and
/// strictly before the single-instance guard is ever acquired and before every other
/// bootstrap step, so the app's own relaunch of itself can never be blocked by the
/// very guard mechanism the currently-running instance holds (D-03,
/// ARCHITECTURE.md Pattern 1).
///
/// Phase 25 shipped Run's body deliberately empty; this phase (26-01) replaces the
/// body with the real update-apply logic while leaving the flag token, this method's
/// signature, and its return contract untouched (D-04) -- both are locked, one-way
/// decisions from 25-02/25-CONTEXT.md.
///
/// <paramref name="applyUpdateArgs"/>'s positional contract (owned entirely by this
/// phase, set by RigToggle.Windows.WindowsUpdateApplier.ApplyAndRelaunch): staged
/// exe path, target exe path, the original process's id, and an optional trailing
/// "--tray" token. A payload that doesn't satisfy this contract -- too few tokens,
/// an empty path, an unparseable id, a missing staged file, or a target directory
/// that doesn't match the staged file's directory (T-26-04, since this command line
/// is locally forgeable by any process) -- is a total no-op: nothing on disk is
/// touched and this method still returns the bypass exit code. This no-op-on-garbage
/// behaviour is a contract, not defensive decoration -- it is what keeps the
/// existing <c>ApplyUpdateBypass_*</c> tests in
/// RigToggle.Windows.Tests/SingleInstanceProcessTests.cs (which pass literal
/// non-path tokens like "token-one") green.
///
/// The returned constant lets an automated test distinguish "the bypass ran" from
/// "a duplicate launch was blocked" -- both would otherwise look identical from
/// outside the process: one that started and then quickly exited.
/// </summary>
internal static class UpdateApplyEntryPoint
{
    private const int MinimumTokenCount = 3;
    private const string TrayToken = "--tray";

    private static readonly TimeSpan WaitForProcessExitTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan WaitForWritableTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan WaitForWritablePollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Runs the real relaunch-helper body and returns
    /// <see cref="StartupArgs.ApplyUpdateBypassExitCode"/>. See the class doc
    /// comment for <paramref name="applyUpdateArgs"/>'s positional contract and the
    /// no-op-on-garbage guarantee.
    /// </summary>
    internal static int Run(string[] applyUpdateArgs)
    {
        if (applyUpdateArgs is null || applyUpdateArgs.Length < MinimumTokenCount)
        {
            Log($"Run: payload has fewer than {MinimumTokenCount} tokens ({applyUpdateArgs?.Length ?? 0}) -- no-op.");
            return StartupArgs.ApplyUpdateBypassExitCode;
        }

        string stagedPath = applyUpdateArgs[0];
        string targetPath = applyUpdateArgs[1];
        string pidToken = applyUpdateArgs[2];
        bool preserveTray = applyUpdateArgs.Length > 3
            && string.Equals(applyUpdateArgs[3], TrayToken, StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(stagedPath) || string.IsNullOrWhiteSpace(targetPath))
        {
            Log("Run: staged path or target path is empty -- no-op.");
            return StartupArgs.ApplyUpdateBypassExitCode;
        }

        if (!int.TryParse(pidToken, out int originalProcessId))
        {
            Log($"Run: process id token '{pidToken}' did not parse as an int -- no-op.");
            return StartupArgs.ApplyUpdateBypassExitCode;
        }

        if (!File.Exists(stagedPath))
        {
            Log($"Run: staged file '{stagedPath}' does not exist -- no-op.");
            return StartupArgs.ApplyUpdateBypassExitCode;
        }

        // T-26-04 (threat register, medium severity, mitigate): the helper must only
        // ever act inside the app's own install directory -- reject a payload whose
        // target directory doesn't match the staged file's directory, since this
        // command line is readable and forgeable by any local process.
        string? stagedDirectory = Path.GetDirectoryName(Path.GetFullPath(stagedPath));
        string? targetDirectory = Path.GetDirectoryName(Path.GetFullPath(targetPath));
        if (!string.Equals(stagedDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase))
        {
            Log($"Run: staged directory '{stagedDirectory}' does not match target directory '{targetDirectory}' -- no-op.");
            return StartupArgs.ApplyUpdateBypassExitCode;
        }

        // UPDATE-05/D-09: version numbers are read HERE, before either path is
        // touched by a rename/move below -- stagedPath still holds the new exe and
        // targetPath still holds the currently-running one. A read failure (e.g. no
        // version resource embedded) degrades to "unknown" rather than aborting a
        // swap that is otherwise fully validated and ready to proceed.
        string newVersionForMarker = TryReadFileVersion(stagedPath);
        string previousVersionForMarker = TryReadFileVersion(targetPath);

        // Fast-path only: wait for the original process to exit if it's still alive.
        // A reused or already-exited id must not be fatal -- the writable-poll below
        // is the real gate this method relies on for correctness.
        try
        {
            using Process originalProcess = Process.GetProcessById(originalProcessId);
            originalProcess.WaitForExit((int)WaitForProcessExitTimeout.TotalMilliseconds);
        }
        catch
        {
            // Already exited, or the id was reused/invalid -- not fatal, fall through
            // to the writable-poll gate below.
        }

        if (!WaitUntilWritable(targetPath, WaitForWritableTimeout, WaitForWritablePollInterval))
        {
            Log($"Run: target '{targetPath}' never became writable within {WaitForWritableTimeout} -- no-op.");
            return StartupArgs.ApplyUpdateBypassExitCode;
        }

        string backupPath = targetPath + ".bak";
        try
        {
            File.Move(targetPath, backupPath, overwrite: true);
        }
        catch (Exception ex)
        {
            Log($"Run: renaming '{targetPath}' to '.bak' failed: {ex.GetType().Name}: {ex.Message} -- no-op, target left untouched.");
            return StartupArgs.ApplyUpdateBypassExitCode;
        }

        try
        {
            File.Move(stagedPath, targetPath);
        }
        catch (Exception ex)
        {
            Log($"Run: moving staged exe into place failed: {ex.GetType().Name}: {ex.Message} -- rolling back '.bak'.");
            try
            {
                File.Move(backupPath, targetPath, overwrite: true);
            }
            catch (Exception rollbackEx)
            {
                Log($"Run: rollback of '.bak' also failed: {rollbackEx.GetType().Name}: {rollbackEx.Message} -- target path may be left absent.");
            }

            return StartupArgs.ApplyUpdateBypassExitCode;
        }

        // Pitfall 5/9: '.bak' is deliberately left in place here, not deleted -- this
        // task's UpdateRollbackChecker owns its full retained-backup lifecycle
        // (auto-rollback on a new exe that fails to reach confirmed-healthy).
        // Deleting it eagerly here would remove the one thing that makes a
        // next-launch rollback possible.
        Log($"Run: swap complete -- '{targetPath}' now holds the staged exe; '.bak' left in place.");

        // UPDATE-05/D-09: record the applied-but-unconfirmed marker BEFORE the
        // relaunch below, so a crash between this write and the new exe reaching
        // confirmed-healthy is still detectable on the next launch. This helper
        // computes the base path itself (rather than borrowing Program.cs's
        // basePath local) because it runs above every Program.cs bootstrap step by
        // contract (see this class's own doc comment). A marker-write failure must
        // never abort a swap that already succeeded on disk -- log and continue.
        try
        {
            var markerStore = new JsonUpdateAppliedMarkerStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RigToggle",
                "update-applied.json"));
            markerStore.Save(new UpdateAppliedMarker(
                newVersionForMarker,
                previousVersionForMarker,
                DateTimeOffset.UtcNow,
                UpdateMarkerStage.Applied));
            Log($"Run: wrote update-applied marker (new={newVersionForMarker}, previous={previousVersionForMarker}, stage=Applied).");
        }
        catch (Exception ex)
        {
            Log($"Run: writing update-applied marker failed: {ex.GetType().Name}: {ex.Message} -- continuing (swap already succeeded).");
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = targetPath,
                Arguments = preserveTray ? TrayToken : string.Empty,
                UseShellExecute = true,
            });
            Log($"Run: relaunched '{targetPath}' (preserveTray={preserveTray}).");
        }
        catch (Exception ex)
        {
            Log($"Run: relaunch of '{targetPath}' failed: {ex.GetType().Name}: {ex.Message}.");
        }

        return StartupArgs.ApplyUpdateBypassExitCode;
    }

    // Pitfall 1: the running exe cannot be overwritten in place, but the file
    // handle's exclusive lock IS released the moment the original process fully
    // exits -- poll/retry opening the target for exclusive write access rather than
    // trusting a single attempt, since exiting can briefly race an AV scan or the
    // OS's own handle teardown.
    private static bool WaitUntilWritable(string path, TimeSpan timeout, TimeSpan pollInterval)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
                return true;
            }
            catch (FileNotFoundException)
            {
                // Nothing to wait on -- treat as writable; the subsequent File.Move
                // will surface a real error if this assumption is wrong.
                return true;
            }
            catch (IOException)
            {
                // Still locked by the exiting original process (or a transient AV
                // scan) -- retry until the deadline.
            }
            catch (UnauthorizedAccessException)
            {
                // Transient permission denial can also occur mid-exit -- retry.
            }

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            Thread.Sleep(pollInterval);
        }
    }

    // UPDATE-05/D-09: reads a file's FileVersionInfo.FileVersion, degrading to
    // "unknown" on any failure (missing version resource, file removed out from
    // under this read, etc.) -- never lets a version-string lookup abort an
    // otherwise-valid swap.
    private static string TryReadFileVersion(string path)
    {
        try
        {
            return FileVersionInfo.GetVersionInfo(path).FileVersion ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    // Best-effort diagnostic logging, matching every other new type in this phase's
    // exact try/Trace.WriteLine/swallow shape.
    private static void Log(string message)
    {
        try
        {
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] UpdateApplyEntryPoint: {message}");
        }
        catch
        {
            // Logging is diagnostic-only; never let it affect the apply sequence.
        }
    }
}
