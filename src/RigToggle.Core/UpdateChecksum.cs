using System.Security.Cryptography;

namespace RigToggle.Core;

/// <summary>
/// Pure static SHA256 utility closing the D-10/D-11 corrupted/truncated-download gap
/// PITFALLS.md Pitfall 5 and the Security Mistakes table both flag: a partial download
/// applied as though it were a valid update. <see cref="System.Security.Cryptography"/>
/// is BCL and adds no package dependency; there is no prior hash-comparison code
/// anywhere in this repository to pattern-match against.
///
/// Follows ToggleResultFormatter's discipline: no I/O beyond reading the file it is
/// handed, defensive against degenerate input, never throws on a malformed/missing
/// published digest. <see cref="Matches"/> resolves "cannot establish the checksum" to
/// <c>false</c> in every case -- fail closed, never an implicit pass.
/// </summary>
public static class UpdateChecksum
{
    /// <summary>
    /// Computes the SHA256 digest of the file at <paramref name="filePath"/> and returns
    /// it as a canonical lowercase hex string. Throws on I/O failure (missing file,
    /// locked file, etc.) -- callers are expected to have already staged the file
    /// successfully before calling this.
    /// </summary>
    public static string ComputeSha256(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Compares a computed SHA256 digest against a published checksum. Accepts a bare
    /// 64-hex-character digest OR a full "sha256sum"-style line (digest, whitespace,
    /// filename) by extracting the first whitespace-delimited token. Case-insensitive.
    ///
    /// Returns <c>false</c> -- never throws, never returns <c>true</c> -- for null,
    /// empty, whitespace-only, or short-of-64-hex-characters published text: an
    /// unestablishable checksum must fail closed (D-11, the prohibition this method
    /// exists to enforce).
    /// </summary>
    public static bool Matches(string? computedHex, string? publishedText)
    {
        if (string.IsNullOrWhiteSpace(computedHex) || string.IsNullOrWhiteSpace(publishedText))
        {
            return false;
        }

        ReadOnlySpan<char> trimmed = publishedText.AsSpan().TrimStart();
        int whitespaceIndex = trimmed.IndexOfAny(' ', '\t');
        ReadOnlySpan<char> digestToken = whitespaceIndex >= 0 ? trimmed[..whitespaceIndex] : trimmed;

        if (digestToken.Length != 64)
        {
            return false;
        }

        foreach (char c in digestToken)
        {
            bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!isHex)
            {
                return false;
            }
        }

        return string.Equals(computedHex, digestToken.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
