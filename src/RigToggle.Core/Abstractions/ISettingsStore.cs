using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Persistence contract for AppSettings. Implemented by
/// RigToggle.Core.Persistence.JsonSettingsStore (plain net10.0, no Windows API refs).
/// </summary>
public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}
