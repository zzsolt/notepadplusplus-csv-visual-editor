namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;

public enum CsvEncodingApplyPreflightStatus
{
    Ready,
    HostWriteNotEnabled,
    UnsupportedCodePage,
    TextNotRepresentable,
    RoundTripMismatch
}

/// <summary>
/// Sanitized preflight result used before an editor replacement target is
/// touched. It never contains replacement text or rejected character values.
/// </summary>
public sealed record CsvEncodingApplyPreflightResult
{
    private CsvEncodingApplyPreflightResult(
        CsvEncodingApplyPreflightStatus status,
        int codePage,
        string? encodingWebName,
        long? encodedByteCount,
        CsvEncodingRepresentabilityResult? representability)
    {
        Status = status;
        CodePage = codePage;
        EncodingWebName = encodingWebName;
        EncodedByteCount = encodedByteCount;
        Representability = representability;
    }

    public CsvEncodingApplyPreflightStatus Status { get; }

    public int CodePage { get; }

    public string? EncodingWebName { get; }

    public long? EncodedByteCount { get; }

    public CsvEncodingRepresentabilityResult? Representability { get; }

    public bool IsReady => Status == CsvEncodingApplyPreflightStatus.Ready;

    internal static CsvEncodingApplyPreflightResult HostWriteNotEnabled(
        CsvEncodingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new CsvEncodingApplyPreflightResult(
            CsvEncodingApplyPreflightStatus.HostWriteNotEnabled,
            profile.CodePage,
            profile.WebName,
            encodedByteCount: null,
            representability: null);
    }

    internal static CsvEncodingApplyPreflightResult FromRepresentability(
        CsvEncodingRepresentabilityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var status = result.Status switch
        {
            CsvEncodingRepresentabilityStatus.ExactRoundTrip =>
                CsvEncodingApplyPreflightStatus.Ready,
            CsvEncodingRepresentabilityStatus.UnsupportedCodePage =>
                CsvEncodingApplyPreflightStatus.UnsupportedCodePage,
            CsvEncodingRepresentabilityStatus.TextNotRepresentable =>
                CsvEncodingApplyPreflightStatus.TextNotRepresentable,
            CsvEncodingRepresentabilityStatus.RoundTripMismatch =>
                CsvEncodingApplyPreflightStatus.RoundTripMismatch,
            _ => throw new ArgumentOutOfRangeException(
                nameof(result),
                result.Status,
                "Unknown encoding representability status.")
        };

        return new CsvEncodingApplyPreflightResult(
            status,
            result.CodePage,
            result.EncodingWebName,
            result.EncodedByteCount,
            result);
    }

    internal static CsvEncodingApplyPreflightResult Unsupported(int codePage)
    {
        return new CsvEncodingApplyPreflightResult(
            CsvEncodingApplyPreflightStatus.UnsupportedCodePage,
            codePage,
            encodingWebName: null,
            encodedByteCount: null,
            representability: null);
    }
}

/// <summary>
/// Separates host-independent representability support from code pages actually
/// authorized for host replacement. Utf8Only is the conservative API default;
/// a host must explicitly supply its independently validated write policy.
/// </summary>
public sealed class CsvEncodingApplyPolicy
{
    private readonly HashSet<int> _hostWriteEnabledCodePages;
    private readonly ReadOnlyCollection<int> _orderedHostWriteEnabledCodePages;

    private CsvEncodingApplyPolicy(IEnumerable<int> hostWriteEnabledCodePages)
    {
        ArgumentNullException.ThrowIfNull(hostWriteEnabledCodePages);

        _hostWriteEnabledCodePages = hostWriteEnabledCodePages.ToHashSet();
        foreach (var codePage in _hostWriteEnabledCodePages)
        {
            if (!CsvEncodingProfiles.TryGet(codePage, out _))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(hostWriteEnabledCodePages),
                    codePage,
                    "Every host-write-enabled code page requires an explicit profile.");
            }
        }

        _orderedHostWriteEnabledCodePages = Array.AsReadOnly(
            _hostWriteEnabledCodePages.Order().ToArray());
    }

    public static CsvEncodingApplyPolicy Utf8Only { get; } =
        new([CsvEncodingProfiles.Utf8CodePage]);

    public IReadOnlyList<int> HostWriteEnabledCodePages =>
        _orderedHostWriteEnabledCodePages;

    public static CsvEncodingApplyPolicy Create(
        IEnumerable<int> hostWriteEnabledCodePages)
    {
        return new CsvEncodingApplyPolicy(hostWriteEnabledCodePages);
    }

    public CsvEncodingApplyPreflightResult Evaluate(
        int codePage,
        string replacementText)
    {
        ArgumentNullException.ThrowIfNull(replacementText);

        if (!CsvEncodingProfiles.TryGet(codePage, out var profile) || profile is null)
        {
            return CsvEncodingApplyPreflightResult.Unsupported(codePage);
        }

        if (!_hostWriteEnabledCodePages.Contains(codePage))
        {
            return CsvEncodingApplyPreflightResult.HostWriteNotEnabled(profile);
        }

        return CsvEncodingApplyPreflightResult.FromRepresentability(
            CsvEncodingRepresentability.Evaluate(codePage, replacementText));
    }
}
