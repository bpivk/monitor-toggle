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

    private readonly HttpClient _httpClient;

    public WindowsUpdateApplier(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

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

        Log($"DownloadAndStageAsync: staged '{stagedPath}'.");
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

        string helperPath = Path.Combine(Path.GetTempPath(), $"RigToggle-updater-{Environment.ProcessId}.exe");
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
