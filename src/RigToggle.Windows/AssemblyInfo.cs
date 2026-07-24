using System.Runtime.CompilerServices;

// Exposes internal members (CopyOutputTechnology, AssignSource) to
// RigToggle.Windows.Tests for direct unit testing without live display hardware
// (04-03 SUMMARY.md WR-05/WR-06).
[assembly: InternalsVisibleTo("RigToggle.Windows.Tests")]
