namespace CsvVisualEditor.Core;

using System.Text;

public enum CsvEncodingRepresentabilityStatus
{
    ExactRoundTrip,
    UnsupportedCodePage,
    TextNotRepresentable,
    RoundTripMismatch
}

/// <summary>
/// Sanitized result of encoding a complete proposed replacement and decoding it
/// again with strict fallbacks. The result deliberately contains no source text
/// or rejected character value.
/// </summary>
public sealed record CsvEncodingRepresentabilityResult
{
    private CsvEncodingRepresentabilityResult(
        CsvEncodingRepresentabilityStatus status,
        int codePage,
        string? encodingWebName,
        long? encodedByteCount)
    {
        if (status == CsvEncodingRepresentabilityStatus.ExactRoundTrip)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(encodingWebName);
            if (encodedByteCount is null or < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(encodedByteCount));
            }
        }
        else if (encodedByteCount is not null)
        {
            throw new ArgumentException(
                "Only an exact round trip may expose an encoded byte count.",
                nameof(encodedByteCount));
        }

        Status = status;
        CodePage = codePage;
        EncodingWebName = encodingWebName;
        EncodedByteCount = encodedByteCount;
    }

    public CsvEncodingRepresentabilityStatus Status { get; }

    public int CodePage { get; }

    public string? EncodingWebName { get; }

    public long? EncodedByteCount { get; }

    public bool IsExact => Status == CsvEncodingRepresentabilityStatus.ExactRoundTrip;

    internal static CsvEncodingRepresentabilityResult Exact(
        CsvEncodingProfile profile,
        long encodedByteCount)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new CsvEncodingRepresentabilityResult(
            CsvEncodingRepresentabilityStatus.ExactRoundTrip,
            profile.CodePage,
            profile.WebName,
            encodedByteCount);
    }

    internal static CsvEncodingRepresentabilityResult Blocked(
        CsvEncodingRepresentabilityStatus status,
        int codePage,
        string? encodingWebName)
    {
        if (status == CsvEncodingRepresentabilityStatus.ExactRoundTrip)
        {
            throw new ArgumentException(
                "An exact result must include the encoded byte count.",
                nameof(status));
        }

        return new CsvEncodingRepresentabilityResult(
            status,
            codePage,
            encodingWebName,
            encodedByteCount: null);
    }
}

/// <summary>
/// Proves whether a complete decoded replacement can be encoded and decoded
/// exactly in one explicitly supported code page. Replacement fallbacks are
/// never permitted.
/// </summary>
public static class CsvEncodingRepresentability
{
    public static CsvEncodingRepresentabilityResult Evaluate(
        int codePage,
        string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!CsvEncodingProfiles.TryGet(codePage, out var profile) || profile is null)
        {
            return CsvEncodingRepresentabilityResult.Blocked(
                CsvEncodingRepresentabilityStatus.UnsupportedCodePage,
                codePage,
                encodingWebName: null);
        }

        Encoding encoding;
        try
        {
            encoding = profile.CreateStrictEncoding();
        }
        catch (NotSupportedException)
        {
            return CsvEncodingRepresentabilityResult.Blocked(
                CsvEncodingRepresentabilityStatus.UnsupportedCodePage,
                codePage,
                profile.WebName);
        }

        byte[] encoded;
        try
        {
            encoded = encoding.GetBytes(text);
        }
        catch (EncoderFallbackException)
        {
            return CsvEncodingRepresentabilityResult.Blocked(
                CsvEncodingRepresentabilityStatus.TextNotRepresentable,
                codePage,
                profile.WebName);
        }

        string decoded;
        try
        {
            decoded = encoding.GetString(encoded);
        }
        catch (DecoderFallbackException)
        {
            return CsvEncodingRepresentabilityResult.Blocked(
                CsvEncodingRepresentabilityStatus.RoundTripMismatch,
                codePage,
                profile.WebName);
        }

        if (!string.Equals(text, decoded, StringComparison.Ordinal))
        {
            return CsvEncodingRepresentabilityResult.Blocked(
                CsvEncodingRepresentabilityStatus.RoundTripMismatch,
                codePage,
                profile.WebName);
        }

        return CsvEncodingRepresentabilityResult.Exact(
            profile,
            encoded.LongLength);
    }
}
