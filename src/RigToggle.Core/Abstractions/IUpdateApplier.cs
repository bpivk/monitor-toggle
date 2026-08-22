using System.Threading;
using System.Threading.Tasks;
using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Download+apply contract for the platform-specific update installer
/// (RigToggle.Windows.WindowsUpdateApplier). Both members are documented as allowed
/// to throw -- Core sequences (UpdateOrchestrator), the App layer executes real
/// OS/UI work and catches every user-visible failure (D-08), mirroring
/// IAppController/IMonitorController's existing "the interface can throw, the App
/// layer decides how to degrade" contract.
/// </summary>
public interface IUpdateApplier
{
    /// <summary>
    /// Downloads <paramref name="release"/>'s asset to a local staging path and
    /// returns that path. May throw on any download/IO failure -- the caller (App
    /// layer, via UpdateOrchestrator) is responsible for catching and toasting it
    /// (D-08); this method itself never swallows a failure into a sentinel value.
    /// </summary>
    Task<string> DownloadAndStageAsync(ReleaseInfo release, CancellationToken cancellationToken);

    /// <summary>
    /// Swaps the staged exe into place at the running exe's own path and relaunches
    /// it, then returns -- it does NOT call Application.Exit() itself (Core/Windows
    /// never touch WinForms); the caller is responsible for exiting the current
    /// process once this returns (Pitfall 4's mutex-release-after-spawn ordering).
    /// May throw on any swap/relaunch failure -- same caller-catches contract as
    /// <see cref="DownloadAndStageAsync"/>.
    /// </summary>
    void ApplyAndRelaunch(string stagedExePath, bool wasStartedHidden);
}
