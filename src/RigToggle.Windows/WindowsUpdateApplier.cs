using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using RigToggle.Core;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Windows;

/// <summary>
/// Real download+self-replace implementation of IUpdateApplier (UPDATE-04),
/// following ARCHITECTURE.md Pattern 2 (detached-helper self-update via process
/// replication, not in-place overwrite) and citing PITFALLS.md Pitfalls 1 (the
/// running exe cannot be overwritten in place -- only renamed), 4 (relaunch races
/// the single-instance mutex), 6 (SmartScreen/Mark-of-the-Web), and 9 (the swap
/// must preserve the exact original exe path, since
/// WindowsAutostartConfigurator.Enable() already baked it into the HKCU Run key).
///
/// DownloadAndStageAsync stages the downloaded asset in the SAME directory as the
/// running exe (never %TEMP%) so the later move-into-place inside
/// UpdateApplyEntryPoint is a same-volume rename, never a cross-volume copy
/// fallback. Plain HttpClient is the deliberate download mechanism: unlike a
/// browser-initiated download, it does not stamp a Mark-of-the-Web
/// Zone.Identifier alternate-data stream on the downloaded file (Pitfall 6), so the
/// unattended relaunch UpdateApplyEntryPoint performs later is not gated behind a
/// SmartScreen interstitial -- a future change of download API would reintroduce
/// that risk and should re-verify this deliberately.
///
/// ApplyAndRelaunch copies the running exe to a per-process-id temp helper path and
/// launches it with the fixed positional --apply-update payload UpdateApplyEntryPoint
/// parses (staged path, target path, running process id, optional trailing --tray).
/// It does NOT call Application.Exit() -- Core/Windows never touch WinForms; the App
/// layer is responsible for exiting immediately after this method returns, releasing
/// the single-instance mutex right after the helper has been spawned (Pitfall 4's
/// required ordering).
/// </summary>
public sealed class WindowsUpdateApplier : IUpdateApplier
{
    private const string StagedFileName = "RigToggle.App.update.exe";
    private const string TrayToken = "--tray";

    // WR-01 (code review, 26-REVIEW.md): shared prefix for the per-process-id
    // %TEMP% helper exe ApplyAndRelaunch copies the running self-contained exe
    // to (~150 MB per CLAUDE.md). Exposed publicly so UpdateRollbackChecker's
    // best-effort orphan sweep (its own doc comment) can target exactly these
    // files without duplicating the literal string in two places.
    public const string HelperFileNamePrefix = "RigToggle-updater-";
    public const string HelperFileSearchPattern = HelperFileNamePrefix + "*.exe";

    private readonly HttpClient _httpClient;

    public WindowsUpdateApplier(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Downloads the release asset to a staged path in the same directory as the running
    /// exe, then verifies it (D-10/D-11) BEFORE returning -- no code path can reach
    /// ApplyAndRelaunch with an unverified staged file, since verification happens here,
    /// in the still-running old process with a working tray icon, rather than in the
    /// UpdateApplyEntryPoint helper process. A mismatch there would produce a
    /// half-swapped installation; a mismatch here throws and the old version keeps
    /// running, surfacing through the exact same D-08 Warning toast an ordinary apply
    /// failure already uses (deliberate -- do not "improve" this into a separate,
    /// bespoke checksum-failure path).
    /// </summary>
    /// <inheritdoc/>
    public async Task<string> DownloadAndStageAsync(ReleaseInfo release, CancellationToken cancellationToken)
    {
        if (release is null)
        {
            throw new ArgumentNullException(nameof(release));
        }

        string? exeDirectory = Path.GetDirectoryName(Environment.ProcessPath);
        if (string.IsNullOrEmpty(exeDirectory))
        {
            throw new InvalidOperationException("Could not resolve the running executable's directory.");
        }

        string stagedPath = Path.Combine(exeDirectory, StagedFileName);
        Log($"DownloadAndStageAsync: downloading '{release.AssetDownloadUrl}' to '{stagedPath}'.");

        using Stream source = await _httpClient.GetStreamAsync(release.AssetDownloadUrl, cancellationToken).ConfigureAwait(false);
        await using (FileStream destination = new(stagedPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        Log($"DownloadAndStageAsync: staged '{stagedPath}', verifying checksum before returning.");

        if (string.IsNullOrWhiteSpace(release.ChecksumDownloadUrl))
        {
            File.Delete(stagedPath);
            throw new InvalidOperationException(
                $"Release '{release.TagName}' published no checksum -- the update cannot be verified and will not be applied.");
        }

        string publishedChecksum = await _httpClient.GetStringAsync(release.ChecksumDownloadUrl, cancellationToken).ConfigureAwait(false);
        string computedChecksum = UpdateChecksum.ComputeSha256(stagedPath);

        if (!UpdateChecksum.Matches(computedChecksum, publishedChecksum))
        {
            File.Delete(stagedPath);
            string expected = publishedChecksum.Length >= 16 ? publishedChecksum[..16] : publishedChecksum;
            string actual = computedChecksum.Length >= 16 ? computedChecksum[..16] : computedChecksum;
            throw new InvalidOperationException(
                $"Checksum mismatch for release '{release.TagName}': expected {expected}..., got {actual}...");
        }

        Log($"DownloadAndStageAsync: checksum verified for '{stagedPath}'.");
        return stagedPath;
    }

    /// <inheritdoc/>
    public void ApplyAndRelaunch(string stagedExePath, bool wasStartedHidden)
    {
        if (string.IsNullOrEmpty(stagedExePath))
        {
            throw new ArgumentNullException(nameof(stagedExePath));
        }

        string? runningExePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(runningExePath))
        {
            throw new InvalidOperationException("Could not resolve the running executable's path.");
        }

        string helperPath = Path.Combine(Path.GetTempPath(), $"{HelperFileNamePrefix}{Environment.ProcessId}.exe");
        File.Copy(runningExePath, helperPath, overwrite: true);

        // Fixed positional payload UpdateApplyEntryPoint.Run parses: staged path,
        // target path, this process's id, and (only when wasStartedHidden) a
        // trailing --tray token so the pre-update session's hidden/visible state
        // survives the swap.
        var arguments = new StringBuilder(StartupArgs.ApplyUpdateFlag)
            .Append(' ').Append('"').Append(stagedExePath).Append('"')
            .Append(' ').Append('"').Append(runningExePath).Append('"')
            .Append(' ').Append(Environment.ProcessId);
        if (wasStartedHidden)
        {
            arguments.Append(' ').Append(TrayToken);
        }

        Log($"ApplyAndRelaunch: spawning helper '{helperPath}' with args '{arguments}'.");

        // Same ProcessStartInfo { UseShellExecute = true } shape as
        // WindowsAppController.LaunchOrFocus -- this codebase's only existing
        // rig-verified process-relaunch mechanism. Deliberately does NOT call
        // Application.Exit() here (Core/Windows never touch WinForms) -- the App
        // layer exits immediately after this method returns.
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = helperPath,
            Arguments = arguments.ToString(),
            UseShellExecute = true,
        }) ?? throw new InvalidOperationException($"Failed to start update helper '{helperPath}'.");
    }

    // Best-effort diagnostic logging, matching WindowsAppController's exact
    // try/Trace.WriteLine/swallow shape with this type's own name in the prefix.
    private static void Log(string message)
    {
        try
        {
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] WindowsUpdateApplier: {message}");
        }
        catch
        {
            // Logging is diagnostic-only; never let it affect update behavior.
        }
    }
}
