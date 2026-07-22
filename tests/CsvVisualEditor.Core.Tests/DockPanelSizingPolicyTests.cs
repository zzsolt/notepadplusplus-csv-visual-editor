namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using Xunit;

public sealed class DockPanelSizingPolicyTests
{
    [Fact]
    public void CalculateTargetWidth_DefaultNotepadPanel_ExpandsToUsableProportion()
    {
        var target = DockPanelSizingPolicy.CalculateTargetWidth(
            hostClientWidth: 1_700,
            currentDockWidth: 200);

        Assert.Equal(748, target);
    }

    [Fact]
    public void CalculateTargetWidth_UserSizedPanel_RemainsUnchanged()
    {
        var target = DockPanelSizingPolicy.CalculateTargetWidth(
            hostClientWidth: 1_700,
            currentDockWidth: 420);

        Assert.Equal(420, target);
    }

    [Fact]
    public void CalculateTargetWidth_SmallHost_PreservesMinimumEditorSpace()
    {
        var target = DockPanelSizingPolicy.CalculateTargetWidth(
            hostClientWidth: 800,
            currentDockWidth: 200);

        Assert.Equal(380, target);
    }

    [Fact]
    public void CalculateTargetWidth_HighDpi_ScalesThresholds()
    {
        var target = DockPanelSizingPolicy.CalculateTargetWidth(
            hostClientWidth: 2_550,
            currentDockWidth: 300,
            dpi: 144);

        Assert.Equal(1_122, target);
    }

    [Theory]
    [InlineData(0, 200, 96, "hostClientWidth")]
    [InlineData(1_000, 0, 96, "currentDockWidth")]
    [InlineData(1_000, 200, 0, "dpi")]
    public void CalculateTargetWidth_InvalidInput_Throws(
        int hostClientWidth,
        int currentDockWidth,
        int dpi,
        string parameterName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => DockPanelSizingPolicy.CalculateTargetWidth(
                hostClientWidth,
                currentDockWidth,
                dpi));

        Assert.Equal(parameterName, exception.ParamName);
    }
}
