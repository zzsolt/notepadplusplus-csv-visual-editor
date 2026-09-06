namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;

public enum CsvCellTransformKind
{
    ReplaceText,
    Trim,
    Uppercase,
    Lowercase
}

/// <summary>Literal text operations. Casing is invariant; replacement is ordinal.</summary>
public sealed record CsvCellTransform(
    CsvCellTransformKind Kind,
    string Find = "",
    string Replacement = "",
    bool MatchCase = true);

public sealed record CsvCellTransformChange(CsvCellAddress Address, string Before, string After);

/// <summary>
/// Immutable, bounded preview tied to one pending edit model. No editor or file access.
/// Callers serialize model access, as with the other edit-model operations.
/// </summary>
public sealed class CsvCellTransformPlan
{
    public const int MaximumTargetCells = 250_000;
    public const int MaximumResultCharacters = 16 * 1024 * 1024;

    private readonly CsvRowEditModel _model;
    private readonly IReadOnlyList<CsvCellTransformChange> _targets;

    private CsvCellTransformPlan(
        CsvRowEditModel model,
        List<CsvCellTransformChange> targets)
    {
        _model = model;
        _targets = targets.AsReadOnly();
        Changes = new ReadOnlyCollection<CsvCellTransformChange>(targets
            .Where(static target => !string.Equals(target.Before, target.After, StringComparison.Ordinal))
            .ToArray());
        ChangedRowCount = Changes.Select(static change => change.Address.RowId).Distinct().Count();
    }

    public int TargetCellCount => _targets.Count;
    public int ChangedRowCount { get; }
    public IReadOnlyList<CsvCellTransformChange> Changes { get; }

    public static CsvCellTransformPlan Create(
        CsvRowEditModel model,
        IEnumerable<CsvCellAddress> addresses,
        CsvCellTransform transform)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(transform);
        if (!Enum.IsDefined(transform.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(transform));
        }

        ArgumentNullException.ThrowIfNull(transform.Find);
        ArgumentNullException.ThrowIfNull(transform.Replacement);
        if (transform.Kind == CsvCellTransformKind.ReplaceText && transform.Find.Length == 0)
        {
            throw new ArgumentException("Enter non-empty text to find.", nameof(transform));
        }

        var rows = new Dictionary<CsvEditRowId, CsvEditRowSnapshot>();
        var seen = new HashSet<CsvCellAddress>();
        var targets = new List<CsvCellTransformChange>();
        long resultCharacters = 0;
        foreach (var address in addresses)
        {
            if (!seen.Add(address))
            {
                continue;
            }

            if (seen.Count > MaximumTargetCells)
            {
                throw new InvalidOperationException("The transformation exceeds the cell limit. Select a smaller scope.");
            }

            if (!rows.TryGetValue(address.RowId, out var row))
            {
                row = model.GetRow(address.RowId);
                rows.Add(address.RowId, row);
            }

            if (row.IsDeleted || address.ColumnIndex < 0 || address.ColumnIndex >= model.ColumnCount)
            {
                throw new InvalidOperationException("The target is not an editable CSV cell.");
            }

            var before = row.Values[address.ColumnIndex];
            var after = Transform(before, transform, MaximumResultCharacters - resultCharacters);
            resultCharacters += after.Length;
            targets.Add(new CsvCellTransformChange(address, before, after));
        }

        return new CsvCellTransformPlan(model, targets);
    }

    /// <summary>Validate every previewed cell, including no-ops, before changing any cell.</summary>
    public int Apply(CsvRowEditModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!ReferenceEquals(model, _model))
        {
            throw new InvalidOperationException("The edit session changed. Create a new preview.");
        }

        var rows = new Dictionary<CsvEditRowId, CsvEditRowSnapshot>();
        foreach (var target in _targets)
        {
            if (!rows.TryGetValue(target.Address.RowId, out var row))
            {
                row = model.GetRow(target.Address.RowId);
                rows.Add(target.Address.RowId, row);
            }

            if (row.IsDeleted || !string.Equals(
                    row.Values[target.Address.ColumnIndex], target.Before, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Previewed cells changed. Create a new preview.");
            }
        }

        var applied = 0;
        try
        {
            foreach (var change in Changes)
            {
                model.SetCellValue(change.Address.RowId, change.Address.ColumnIndex, change.After);
                applied++;
            }
        }
        catch
        {
            for (var index = applied - 1; index >= 0; index--)
            {
                var change = Changes[index];
                model.SetCellValue(change.Address.RowId, change.Address.ColumnIndex, change.Before);
            }

            throw;
        }

        return applied;
    }

    private static string Transform(string value, CsvCellTransform transform, long remainingCharacters)
    {
        // Measure literal replacement growth before allocating the replacement string.
        long length = value.Length;
        var comparison = transform.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (transform.Kind == CsvCellTransformKind.ReplaceText)
        {
            var offset = 0;
            while (offset < value.Length)
            {
                var match = value.IndexOf(transform.Find, offset, comparison);
                if (match < 0)
                {
                    break;
                }

                length += (long)transform.Replacement.Length - transform.Find.Length;
                offset = match + transform.Find.Length;
            }
        }
        else if (transform.Kind == CsvCellTransformKind.Trim)
        {
            // Trim can reduce an otherwise oversized input before the output limit applies.
            value = value.Trim();
            length = value.Length;
        }

        if (length > remainingCharacters)
        {
            throw new InvalidOperationException("The transformation exceeds the text limit. Select a smaller scope.");
        }

        return transform.Kind switch
        {
            CsvCellTransformKind.ReplaceText => value.Replace(transform.Find, transform.Replacement, comparison),
            CsvCellTransformKind.Trim => value,
            CsvCellTransformKind.Uppercase => value.ToUpperInvariant(),
            CsvCellTransformKind.Lowercase => value.ToLowerInvariant(),
            _ => throw new ArgumentOutOfRangeException(nameof(transform))
        };
    }
}
