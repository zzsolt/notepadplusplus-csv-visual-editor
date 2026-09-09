namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using Xunit;

public sealed class GeneratedColumnLabelTests
{
    [Fact]
    public void HeaderLookingLikeAFallbackRemainsUserData()
    {
        var parsed = CsvParser.Parse("Column 1,,Other\nalpha,beta,gamma", CsvDialect.Create(','));
        var projection = CsvTableProjector.Create(parsed, new CsvTableProjectionOptions { HeaderMode = CsvHeaderMode.FirstRecord });
        Assert.False(projection.Columns[0].IsGeneratedName);
        Assert.Equal("Column 1", projection.Columns[0].Name);
        Assert.True(projection.Columns[1].IsGeneratedName);
        Assert.False(projection.Columns[2].IsGeneratedName);
        Assert.Equal("alpha", projection.Rows[0].Values[0]);
    }

    [Fact]
    public void NoHeaderCreatesExplicitSyntheticLabelMetadata()
    {
        var parsed = CsvParser.Parse("alpha,beta", CsvDialect.Create(','));
        var projection = CsvTableProjector.Create(parsed, new CsvTableProjectionOptions { HeaderMode = CsvHeaderMode.NoHeader });
        Assert.All(projection.Columns, column => Assert.True(column.IsGeneratedName));
        Assert.Equal("alpha", projection.Rows[0].Values[0]);
    }
}
