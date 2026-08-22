using System.Diagnostics;
using RigToggle.Core;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.App
{
    /// <summary>
    /// Above-the-guard startup state machine for UPDATE-05/D-09's auto-rollback path
    /// -- a sibling of StartupRecoveryChecker, but invoked at a DIFFERENT point in
    /// Program.cs: strictly BEFORE SingleInstanceGuard.Acquire(), never after,
    /// because a Stage == FirstLaunchAttempted restore spawns a replacement process,
    /// and doing that while holding the mutex is exactly the race PITFALLS.md
    /// Pitfall 4 describes (the same reason the --apply-update bypass branch
    /// precedes the guard).
    ///
    /// Run's three-stage walk (Applied -> FirstLaunchAttempted -> restore ->
    /// Reverted -> cleared) is strictly one-way -- every branch either advances the
    /// marker forward by exactly one stage or clears it outright, so a new exe that
    /// crashes on every single boot can restore-and-relaunch at most once, never
    /// loop forever.
    /// </summary>
    internal static class UpdateRollbackChecker
    {
        private const string BackupSuffix = ".bak";
        private const string FailedSuffix = ".failed";
        private const string TrayFlag = "--tray";

        /// <summary>
        /// Walks the marker's current stage forward exactly one step (or clears it)
        /// and returns true when Program.Main must return immediately -- this
        /// happens ONLY on the FirstLaunchAttempted -> restore-and-relaunch branch,
        /// since that branch has already spawned the restored previous version as a
        /// replacement process. revertNotice is non-null only on the Reverted
        /// branch, naming both versions for MainForm.ShowUpdateRevertNotice.
        /// </summary>
        internal static bool Run(IUpdateAppliedMarkerStore markerStore, string runningExePath, out string? revertNotice)
        {
            revertNotice = null;

            UpdateAppliedMarker? marker = markerStore.TryLoad();
            if (marker is null)
            {
                // ARCHITECTURE.md Robustness table: best-effort next-launch cleanup
                // of an orphaned backup/failed file left by an interrupted or
                // already-resolved sequence -- never fatal, never blocks startup.
                TryDelete(runningExePath + BackupSuffix);
                TryDelete(runningExePath + FailedSuffix);
                return false;
            }

            switch (marker.Stage)
            {
                case UpdateMarkerStage.Applied:
                    // This is the new version's very first launch. Consume the
                    // attempt immediately -- re-saving as FirstLaunchAttempted
                    // BEFORE this boot has any chance to crash means a crash later
                    // in this same boot can never be mistaken for a fresh,
                    // not-yet-attempted first launch.
                    try
                    {
                        markerStore.Save(marker with { Stage = UpdateMarkerStage.FirstLaunchAttempted });
                    }
                    catch (Exception ex)
                    {
                        Log($"Run: failed to advance marker to FirstLaunchAttempted: {ex.GetType().Name}: {ex.Message}.");
                    }

                    return false;

                case UpdateMarkerStage.FirstLaunchAttempted:
                    return RunRestoreBranch(markerStore, marker, runningExePath);

                case UpdateMarkerStage.Reverted:
                    // This process IS the restored previous version, on its first
                    // launch back. Clear the marker (this surfacing must never
                    // repeat) and hand the caller the exact D-09 wording naming
                    // both versions.
                    try
                    {
                        markerStore.Clear();
                    }
                    catch (Exception ex)
                    {
                        Log($"Run: failed to clear Reverted marker: {ex.GetType().Name}: {ex.Message}.");
                    }

                    revertNotice = $"Update to v{marker.NewVersion} failed to start — reverted to v{marker.PreviousVersion}.";
                    return false;

                default:
                    // Unreachable for a value this codebase itself ever wrote, but a
                    // degenerate/hand-edited on-disk value must never crash startup
                    // -- treat it identically to "nothing to do."
                    Log($"Run: marker has an unrecognised Stage '{marker.Stage}' -- clearing and continuing.");
                    try
                    {
                        markerStore.Clear();
                    }
                    catch (Exception ex)
                    {
                        Log($"Run: failed to clear marker with an unrecognised Stage: {ex.GetType().Name}: {ex.Message}.");
                    }

                    return false;
            }
        }

        /// <summary>
        /// The previous boot of the new version never reached confirmed-healthy. If
        /// a backup exists, restore it to the exact path autostart's Run-key entry
        /// already names (Pitfall 9) and relaunch it; if any step throws partway,
        /// undo as much as possible, clear the marker so the next launch can never
        /// loop, and continue this (failed) boot rather than crash startup
        /// entirely.
        /// </summary>
        private static bool RunRestoreBranch(IUpdateAppliedMarkerStore markerStore, UpdateAppliedMarker marker, string runningExePath)
        {
            string backupPath = runningExePath + BackupSuffix;
            string failedPath = runningExePath + FailedSuffix;

            if (!File.Exists(backupPath))
            {
                // Nothing to restore -- clear so this can never loop, and never
                // pretend a revert happened that didn't.
                Log("Run: FirstLaunchAttempted marker found but no backup exists -- nothing to restore, clearing marker.");
                try
                {
                    markerStore.Clear();
                }
                catch (Exception ex)
                {
                    Log($"Run: failed to clear marker after no-backup case: {ex.GetType().Name}: {ex.Message}.");
                }

                return false;
            }

            bool renamedRunningToFailed = false;
            bool movedBackupIntoPlace = false;

            try
            {
                // Renaming a running exe is permitted on Windows (only
                // deleting/writing to it while it's the executing image is not) --
                // this is what frees the target path for the backup to land on the
                // identical original path.
                File.Move(runningExePath, failedPath, overwrite: true);
                renamedRunningToFailed = true;

                File.Move(backupPath, runningExePath, overwrite: true);
                movedBackupIntoPlace = true;

                markerStore.Save(marker with { Stage = UpdateMarkerStage.Reverted });

                bool preserveTray = StartupArgs.ShouldStartHidden(Environment.GetCommandLineArgs());
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = runningExePath,
                    Arguments = preserveTray ? TrayFlag : string.Empty,
                    UseShellExecute = true,
                });

                Log($"Run: restored backup to '{runningExePath}' and relaunched it (preserveTray={preserveTray}).");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Run: restore-and-relaunch failed partway: {ex.GetType().Name}: {ex.Message} -- undoing what was done and clearing marker.");

                // Undo as far as possible so at least one launchable copy remains at
                // the original path (the backstop truth this whole method exists to
                // uphold).
                if (!movedBackupIntoPlace && renamedRunningToFailed)
                {
                    // The rename to .failed succeeded but the backup move did not --
                    // move the running exe back so the original path is never left
                    // empty.
                    try
                    {
                        File.Move(failedPath, runningExePath, overwrite: true);
                    }
                    catch (Exception undoEx)
                    {
                        Log($"Run: undo of the running-exe rename also failed: {undoEx.GetType().Name}: {undoEx.Message} -- '{runningExePath}' may be left absent.");
                    }
                }

                // If movedBackupIntoPlace is true, the backup already landed at
                // runningExePath -- runningExePath itself IS the restored backup
                // now, so there is nothing further to undo there (only the relaunch
                // or the marker-save that followed could have thrown).

                try
                {
                    markerStore.Clear();
                }
                catch (Exception clearEx)
                {
                    Log($"Run: failed to clear marker after a failed restore: {clearEx.GetType().Name}: {clearEx.Message}.");
                }

                return false;
            }
        }

        /// <summary>
        /// Called from MainForm.BeginUpdateHealthWatch's one-shot timer tick -- the
        /// deliberate confirmed-healthy signal (a tick can only fire once the
        /// message pump is actually running). This is the commit point
        /// PITFALLS.md Pitfall 5 requires: the backup is removed only once the new
        /// exe has demonstrably run, never merely because a file move completed.
        /// </summary>
        internal static void ConfirmHealthy(IUpdateAppliedMarkerStore markerStore, string runningExePath)
        {
            try
            {
                markerStore.Clear();
            }
            catch (Exception ex)
            {
                Log($"ConfirmHealthy: failed to clear marker: {ex.GetType().Name}: {ex.Message}.");
            }

            TryDelete(runningExePath + BackupSuffix);
            TryDelete(runningExePath + FailedSuffix);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Log($"TryDelete: failed to delete '{path}': {ex.GetType().Name}: {ex.Message}.");
            }
        }

        // Best-effort diagnostic logging, matching every other type in this
        // codebase's exact try/Trace.WriteLine/swallow shape.
        private static void Log(string message)
        {
            try
            {
                Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] UpdateRollbackChecker: {message}");
            }
            catch
            {
                // Logging is diagnostic-only; never let it affect the rollback sequence.
            }
        }
    }
}
