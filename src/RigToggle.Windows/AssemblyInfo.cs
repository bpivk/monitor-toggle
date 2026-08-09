using System.Runtime.CompilerServices;

// Exposes internal members (MergeAllMonitors, AnyRectanglesOverlap) to
// RigToggle.Windows.Tests for direct unit testing without live display hardware
// (06-RESEARCH.md dedup/geometry work; quick task 260728-qj1's duplicate-row/
// dual-primary regression fix).
[assembly: InternalsVisibleTo("RigToggle.Windows.Tests")]
