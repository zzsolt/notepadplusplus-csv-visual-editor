namespace CsvVisualEditor.Core;

using System.Text;

/// <summary>
/// Deterministic CSV serializer for records that must be regenerated after an edit.
/// </summary>
public static class CsvSerializer
{
    public static string SerializeRecords(
        IEnumerable<IReadOnlyList<string>> records,
        CsvSerializationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(policy);

        var copiedRecords = records.ToArray();
        if (copiedRecords.Any(static record => record is null))
        {
            throw new ArgumentException(
                "Records cannot contain null entries.",
                nameof(records));
        }

        var builder = new StringBuilder();
        if (policy.HasLeadingBom)
        {
            builder.Append('\uFEFF');
        }

        for (var recordIndex = 0; recordIndex < copiedRecords.Length; recordIndex++)
        {
            var record = copiedRecords[recordIndex];
            AppendRecord(builder, record, record.Count, policy);

            if (recordIndex + 1 < copiedRecords.Length || policy.HasTerminalNewLine)
            {
                builder.Append(policy.NewLine);
            }
        }

        return builder.ToString();
    }

    public static string SerializeRecord(
        IReadOnlyList<string> values,
        int fieldCount,
        CsvSerializationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(policy);

        var builder = new StringBuilder();
        AppendRecord(builder, values, fieldCount, policy);
        return builder.ToString();
    }

    private static void AppendRecord(
        StringBuilder builder,
        IReadOnlyList<string> values,
        int fieldCount,
        CsvSerializationPolicy policy)
    {
        if (fieldCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fieldCount),
                fieldCount,
                "A serialized CSV record must contain at least one field.");
        }

        if (fieldCount > values.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fieldCount),
                fieldCount,
                "The field count cannot exceed the supplied value count.");
        }

        for (var fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++)
        {
            if (fieldIndex > 0)
            {
                builder.Append(policy.Delimiter);
            }

            var value = values[fieldIndex] ??
                throw new ArgumentException(
                    "CSV field values cannot be null.",
                    nameof(values));
            AppendField(builder, value, policy);
        }
    }

    private static void AppendField(
        StringBuilder builder,
        string value,
        CsvSerializationPolicy policy)
    {
        if (!RequiresQuotes(value, policy))
        {
            builder.Append(value);
            return;
        }

        builder.Append(policy.Quote);
        foreach (var character in value)
        {
            if (character == policy.Quote)
            {
                builder.Append(policy.Quote);
            }

            builder.Append(character);
        }

        builder.Append(policy.Quote);
    }

    private static bool RequiresQuotes(
        string value,
        CsvSerializationPolicy policy)
    {
        if (value.IndexOf(policy.Delimiter) >= 0 ||
            value.IndexOf(policy.Quote) >= 0 ||
            value.IndexOf('\r') >= 0 ||
            value.IndexOf('\n') >= 0)
        {
            return true;
        }

        return value.Length > 0 &&
               (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]));
    }
}
