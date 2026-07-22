namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using Xunit;

public sealed class CsvColumnFillWeightCalculatorTests
{
    [Fact]
    public void Calculate_LongContentReceivesMoreSpaceThanShortContent()
    {
        var projection = CreateProjection(
            ["EmailAddress", "Code", "Password"],
            [
                ["very.long.address@example.invalid", "A1", "TEMP-26"],
                ["short@example.invalid", "B2", "TEMP-27"]
            ]);

        var weights = CsvColumnFillWeightCalculator.Calculate(projection);

        Assert.Equal(3, weights.Count);
        Assert.True(weights[0] > weights[1]);
        Assert.True(weights[0] > weights[2]);
        Assert.All(
            weights,
            static weight => Assert.InRange(
                weight,
                CsvColumnFillWeightCalculator.MinimumFillWeight,
                CsvColumnFillWeightCalculator.MaximumFillWeight));
    }

    [Fact]
    public void Calculate_EmptyAndShortColumnsUseReadableMinimumWeight()
    {
        var projection = CreateProjection(
            ["A", "B"],
            [[string.Empty, "x"]]);

        var weights = CsvColumnFillWeightCalculator.Calculate(projection);

        Assert.Equal(
            [
                CsvColumnFillWeightCalculator.MinimumFillWeight,
                CsvColumnFillWeightCalculator.MinimumFillWeight
            ],
            weights);
    }

    [Fact]
    public void Calculate_MultilineValueUsesLongestDisplayedLine()
    {
        var projection = CreateProjection(
            ["Short", "Other"],
            [["ab\n123456789012345", "tiny"]]);

        var weights = CsvColumnFillWeightCalculator.Calculate(projection);

        Assert.Equal(15f, weights[0]);
        Assert.Equal(CsvColumnFillWeightCalculator.MinimumFillWeight, weights[1]);
    }

    [Fact]
    public void Calculate_ExcessivelyLongValueIsCapped()
    {
        var projection = CreateProjection(
            ["Value"],
            [[new string('x', 500)]]);

        var weights = CsvColumnFillWeightCalculator.Calculate(projection);

        Assert.Equal(CsvColumnFillWeightCalculator.MaximumFillWeight, weights[0]);
    }

    [Fact]
    public void Calculate_EvenSamplingIncludesLastDisplayedRow()
    {
        var rows = Enumerable.Range(0, 20)
            .Select(index => new[]
            {
                index == 19 ? "last-row-is-significantly-longer" : "x"
            })
            .ToArray();
        var projection = CreateProjection(["Value"], rows);

        var weights = CsvColumnFillWeightCalculator.Calculate(
            projection,
            maximumSampledRows: 3);

        Assert.True(weights[0] > CsvColumnFillWeightCalculator.MinimumFillWeight);
    }

    [Fact]
    public void Calculate_InvalidSampleLimitThrows()
    {
        var projection = CreateProjection(["Value"], [["x"]]);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => CsvColumnFillWeightCalculator.Calculate(
                projection,
                maximumSampledRows: 0));

        Assert.Equal("maximumSampledRows", exception.ParamName);
    }

    private static CsvTableProjection CreateProjection(
        IReadOnlyList<string> headers,
        IReadOnlyList<string[]> values)
    {
        var columns = headers
            .Select((header, index) => new CsvTableColumn(index, header))
            .ToArray();
        var rows = values
            .Select((row, index) => new CsvTableRow(
                index + 1,
                row,
                new CsvSourceSpan(index, row.Sum(static value => value.Length))))
            .ToArray();

        return new CsvTableProjection(
            columns,
            rows,
            rows.Length,
            headerSourceRecordIndex: 0,
            isRowLimited: false);
    }
}
