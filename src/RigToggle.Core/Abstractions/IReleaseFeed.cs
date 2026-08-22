using System.Threading;
using System.Threading.Tasks;
using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// GitHub-releases read contract (UPDATE-02). Implemented by
/// RigToggle.Core.GitHubReleaseFeed -- lives in Core, not Windows, since it is
/// genuinely platform-neutral (no Win32/COM), matching JsonSettingsStore's own
/// precedent of "plain BCL I/O lives in Core."
///
/// <see cref="GetLatestReleaseAsync"/> returning null means "no usable latest
/// release" and covers a 404 (no releases at all), a release with no matching
/// asset, a rejected (non-allow-listed) asset URL, and any transport/deserialization
/// failure alike -- the interface never throws for any of those, mirroring how
/// IAppController documents its own contract. A caller (UpdateOrchestrator) that
/// receives null must treat it identically to "already up to date," never as an
/// error to surface.
/// </summary>
public interface IReleaseFeed
{
    Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken);
}
