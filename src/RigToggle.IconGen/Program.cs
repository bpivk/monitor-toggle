namespace RigToggle.IconGen;

/// <summary>
/// Dev-time-only console entry point. Generates the three checked-in .ico assets
/// under src/RigToggle.App/Resources/ from the procedural geometry in
/// IconGeometry.cs, packed via IconWriter.cs. Never referenced by RigToggle.App --
/// this project is not part of the shipped self-contained publish (13-RESEARCH.md
/// "Architectural Responsibility Map").
/// </summary>
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
    }
}
