namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;

public enum CsvFilterCombination { All, Any }
public enum CsvFilterOperator
{
    Contains, DoesNotContain, Equals, DoesNotEqual, StartsWith, EndsWith,
    IsEmpty, IsNotEmpty, IsWhitespace, IsNotWhitespace,
    NumberEquals, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual
}
public enum CsvSortKind { Text, Number }

/// <summary>One literal, immutable column predicate. No expressions or regular expressions.</summary>
public sealed record CsvColumnFilter
{
    public const int MaximumValueLength = 4096;

    public CsvColumnFilter(int columnIndex, CsvFilterOperator operation,
        string value = "", bool matchCase = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (columnIndex < 0) throw new ArgumentOutOfRangeException(nameof(columnIndex));
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        if (value.Length > MaximumValueLength)
            throw new ArgumentException("A filter value may contain at most 4,096 characters.", nameof(value));
        ColumnIndex = columnIndex;
        Operation = operation;
        MatchCase = matchCase;
        Value = RequiresValue(operation) ? value : string.Empty;
        if (IsNumeric(operation))
        {
            if (!CsvNumericValue.TryParse(value, out var number))
                throw new ArgumentException("Use an exact decimal number with a dot, such as -12.5. Grouping, exponents and rounded values are not supported.", nameof(value));
            NumericOperand = number;
        }
    }

    public int ColumnIndex { get; }
    public CsvFilterOperator Operation { get; }
    public string Value { get; }
    public bool MatchCase { get; }
    internal decimal? NumericOperand { get; }
    public static bool IsNumeric(CsvFilterOperator operation) => operation is
        CsvFilterOperator.NumberEquals or CsvFilterOperator.GreaterThan or
        CsvFilterOperator.GreaterThanOrEqual or CsvFilterOperator.LessThan or CsvFilterOperator.LessThanOrEqual;
    public static bool RequiresValue(CsvFilterOperator operation) => operation is not
        (CsvFilterOperator.IsEmpty or CsvFilterOperator.IsNotEmpty or CsvFilterOperator.IsWhitespace or CsvFilterOperator.IsNotWhitespace);

    internal bool Matches(string value)
    {
        var comparison = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (IsNumeric(Operation))
        {
            if (!CsvNumericValue.TryParse(value, out var number)) return false;
            var result = number.CompareTo(NumericOperand!.Value);
            return Operation switch
            {
                CsvFilterOperator.NumberEquals => result == 0,
                CsvFilterOperator.GreaterThan => result > 0,
                CsvFilterOperator.GreaterThanOrEqual => result >= 0,
                CsvFilterOperator.LessThan => result < 0,
                CsvFilterOperator.LessThanOrEqual => result <= 0,
                _ => false
            };
        }
        return Operation switch
        {
            CsvFilterOperator.Contains => value.Contains(Value, comparison),
            CsvFilterOperator.DoesNotContain => !value.Contains(Value, comparison),
            CsvFilterOperator.Equals => string.Equals(value, Value, comparison),
            CsvFilterOperator.DoesNotEqual => !string.Equals(value, Value, comparison),
            CsvFilterOperator.StartsWith => value.StartsWith(Value, comparison),
            CsvFilterOperator.EndsWith => value.EndsWith(Value, comparison),
            CsvFilterOperator.IsEmpty => value.Length == 0,
            CsvFilterOperator.IsNotEmpty => value.Length != 0,
            CsvFilterOperator.IsWhitespace => value.Length > 0 && string.IsNullOrWhiteSpace(value),
            CsvFilterOperator.IsNotWhitespace => value.Length == 0 || !string.IsNullOrWhiteSpace(value),
            _ => throw new InvalidOperationException("Unsupported column predicate.")
        };
    }
}

public sealed record CsvSortKey
{
    public CsvSortKey(int columnIndex, CsvTableSortDirection direction, CsvSortKind kind = CsvSortKind.Text)
    {
        if (columnIndex < 0) throw new ArgumentOutOfRangeException(nameof(columnIndex));
        if (direction is not (CsvTableSortDirection.Ascending or CsvTableSortDirection.Descending))
            throw new ArgumentOutOfRangeException(nameof(direction));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        ColumnIndex = columnIndex;
        Direction = direction;
        Kind = kind;
    }
    public int ColumnIndex { get; }
    public CsvTableSortDirection Direction { get; }
    public CsvSortKind Kind { get; }
}

/// <summary>Bounded, defensively copied view state. Never holds or changes source rows.</summary>
public sealed class CsvDataViewDefinition
{
    public const int MaximumFilters = 8;
    public const int MaximumSortKeys = 3;
    public static CsvDataViewDefinition Empty { get; } = new();

    public CsvDataViewDefinition(IEnumerable<CsvColumnFilter>? filters = null,
        CsvFilterCombination combination = CsvFilterCombination.All,
        IEnumerable<CsvSortKey>? sortKeys = null)
    {
        if (!Enum.IsDefined(combination)) throw new ArgumentOutOfRangeException(nameof(combination));
        var filterArray = filters?.Take(MaximumFilters + 1).ToArray() ?? [];
        var sortArray = sortKeys?.Take(MaximumSortKeys + 1).ToArray() ?? [];
        if (filterArray.Length > MaximumFilters || filterArray.Any(static item => item is null))
            throw new ArgumentException("Use at most eight non-null filter conditions.", nameof(filters));
        if (sortArray.Length > MaximumSortKeys || sortArray.Any(static item => item is null))
            throw new ArgumentException("Use at most three non-null sort levels.", nameof(sortKeys));
        if (sortArray.Select(static item => item.ColumnIndex).Distinct().Count() != sortArray.Length)
            throw new ArgumentException("Each sort level must use a different column.", nameof(sortKeys));
        Filters = Array.AsReadOnly(filterArray);
        SortKeys = Array.AsReadOnly(sortArray);
        Combination = combination;
    }

    public ReadOnlyCollection<CsvColumnFilter> Filters { get; }
    public ReadOnlyCollection<CsvSortKey> SortKeys { get; }
    public CsvFilterCombination Combination { get; }
    public bool IsActive => Filters.Count > 0 || SortKeys.Count > 0;

    internal void ValidateColumns(int columnCount)
    {
        if (Filters.Any(filter => filter.ColumnIndex >= columnCount) ||
            SortKeys.Any(key => key.ColumnIndex >= columnCount))
            throw new ArgumentException("View rules refer to a column outside the current table.");
    }

    internal bool Matches(CsvTableRow row)
    {
        if (Filters.Count == 0) return true;
        foreach (var filter in Filters)
        {
            var matches = filter.Matches(row.Values[filter.ColumnIndex]);
            if (Combination == CsvFilterCombination.All && !matches) return false;
            if (Combination == CsvFilterCombination.Any && matches) return true;
        }
        return Combination == CsvFilterCombination.All;
    }
}
