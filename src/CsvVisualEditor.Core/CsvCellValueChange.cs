namespace CsvVisualEditor.Core;

/// <summary>
/// One pending cell replacement, bound to the exact model, stable row identity,
/// column and pre-edit value. No host I/O or model mutation occurs during creation.
/// </summary>
public sealed class CsvCellValueChange
{
    private readonly CsvRowEditModel _model;

    private CsvCellValueChange(CsvRowEditModel model, CsvCellAddress address, string before, string after)
    {
        _model = model;
        Address = address;
        Before = before;
        After = after;
    }

    public CsvCellAddress Address { get; }
    public string Before { get; }
    public string After { get; }

    public static CsvCellValueChange Create(CsvRowEditModel model, CsvCellAddress address, string after)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(after);
        if (after.Length > CsvCellTextCodec.MaximumValueLength)
            throw new ArgumentOutOfRangeException(nameof(after));
        if (address.ColumnIndex < 0 || address.ColumnIndex >= model.ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(address));
        var row = model.GetRow(address.RowId);
        if (row.IsDeleted) throw new InvalidOperationException("The target row is deleted.");
        return new(model, address, row.Values[address.ColumnIndex], after);
    }

    public bool Apply(CsvRowEditModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!ReferenceEquals(model, _model))
            throw new InvalidOperationException("The cell change belongs to another edit model.");
        var row = model.GetRow(Address.RowId);
        if (row.IsDeleted || !string.Equals(row.Values[Address.ColumnIndex], Before, StringComparison.Ordinal))
            throw new InvalidOperationException("The target cell changed after the preview was opened.");
        return model.SetCellValue(Address.RowId, Address.ColumnIndex, After);
    }
}
