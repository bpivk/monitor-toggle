using System.Linq;
using RigToggle.Core;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves UpdateChecksum.ComputeSha256's round-trip correctness and Matches' case-
/// insensitivity, sha256sum-line tolerance, and mismatch detection (D-10/D-11). Each
/// test uses its own unique temp file, matching JsonStoreTests' temp-fixture
/// convention (create in the constructor, delete in Dispose).
///
/// The final [Theory] group is an explicit fail-closed test: a missing or malformed
/// published digest must yield false, never true -- the prohibition this class
/// protects is that an update whose integrity cannot be established must not pass.
/// </summary>
public class UpdateChecksumTests : IDisposable
{
    // Built programmatically (never hand-counted) so its length is provably exact:
    // "0123456789abcdef" is 16 chars, repeated 4 times is exactly 64.
    private static readonly string ValidHex64 = string.Concat(Enumerable.Repeat("0123456789abcdef", 4));

    private readonly string _tempFile;

    public UpdateChecksumTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), "RigToggleTests_" + Guid.NewGuid().ToString("N") + ".bin");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }

    [Fact]
    public void ComputeSha256_OverKnownBytes_MatchesIndependentComputation()
    {
        byte[] bytes = { 0x01, 0x02, 0x03, 0x04, 0x05, 0xFF, 0x00, 0xAB };
        File.WriteAllBytes(_tempFile, bytes);

        string computed = UpdateChecksum.ComputeSha256(_tempFile);

        // Independent second computation over the same bytes, using the BCL API
        // directly rather than calling ComputeSha256 again, so this test does not
        // just check the method against itself.
        string expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(expected, computed);
        Assert.Equal(64, computed.Length);
    }

    [Fact]
    public void Matches_SameDigestDifferentCase_ReturnsTrue()
    {
        byte[] bytes = { 0x10, 0x20, 0x30 };
        File.WriteAllBytes(_tempFile, bytes);
        string computed = UpdateChecksum.ComputeSha256(_tempFile);

        Assert.True(UpdateChecksum.Matches(computed, computed.ToUpperInvariant()));
    }

    [Fact]
    public void Matches_FullSha256SumStyleLine_ReturnsTrue()
    {
        byte[] bytes = { 0xAA, 0xBB, 0xCC };
        File.WriteAllBytes(_tempFile, bytes);
        string computed = UpdateChecksum.ComputeSha256(_tempFile);

        Assert.True(UpdateChecksum.Matches(computed, $"{computed}  RigToggle.App.exe"));
    }

    [Fact]
    public void Matches_FullSha256SumStyleLineWithAlteredDigest_ReturnsFalse()
    {
        byte[] bytes = { 0xAA, 0xBB, 0xCC };
        File.WriteAllBytes(_tempFile, bytes);
        string computed = UpdateChecksum.ComputeSha256(_tempFile);

        // Flip the first hex character so the digest portion genuinely differs.
        char flipped = computed[0] == 'a' ? 'b' : 'a';
        string alteredLine = flipped + computed[1..] + "  RigToggle.App.exe";

        Assert.False(UpdateChecksum.Matches(computed, alteredLine));
    }

    /// <summary>
    /// Fail-closed group (D-11 prohibition): a missing or malformed published digest
    /// must resolve to false in every case, never an implicit pass.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Matches_UnestablishableDigest_ReturnsFalse(string? published)
    {
        Assert.False(UpdateChecksum.Matches(ValidHex64, published));
    }

    [Fact]
    public void Matches_SixtyThreeCharacterDigest_ReturnsFalse()
    {
        string shortOfSixtyFour = ValidHex64[..^1];
        Assert.Equal(63, shortOfSixtyFour.Length);

        Assert.False(UpdateChecksum.Matches(ValidHex64, shortOfSixtyFour));
    }

    [Fact]
    public void Matches_NonHexCharacterInSixtyFourCharToken_ReturnsFalse()
    {
        // Exactly 64 characters (same length as ValidHex64) but the leading digit is
        // 'g', which is not valid hex.
        string invalidHex = "g" + ValidHex64[1..];
        Assert.Equal(64, invalidHex.Length);

        Assert.False(UpdateChecksum.Matches(ValidHex64, invalidHex));
    }

    [Fact]
    public void Matches_NullComputedHex_ReturnsFalse()
    {
        Assert.False(UpdateChecksum.Matches(null, ValidHex64));
    }
}
