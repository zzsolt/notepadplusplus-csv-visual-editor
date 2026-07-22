namespace CsvVisualEditor.Core;

/// <summary>
/// Calculates a one-time, usable initial width for a newly created right-side dock panel.
/// Existing user-sized panels are deliberately preserved.
/// </summary>
public static class DockPanelSizingPolicy
{
    private const int DefaultDpi = 96;
    private const int DefaultHostWidthUpperBound = 280;
    private const int MinimumUsableDockWidth = 520;
    private const int MinimumEditorWidth = 420;
    private const int MaximumPreferredDockWidth = 900;
    private const double PreferredHostFraction = 0.44;

    public static int CalculateTargetWidth(
        int hostClientWidth,
        int currentDockWidth,
        int dpi = DefaultDpi)
    {
        if (hostClientWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hostClientWidth),
                hostClientWidth,
                "The host client width must be positive.");
        }

        if (currentDockWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentDockWidth),
                currentDockWidth,
                "The current dock width must be positive.");
        }

        if (dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dpi),
                dpi,
                "DPI must be positive.");
        }

        var defaultWidthUpperBound = Scale(DefaultHostWidthUpperBound, dpi);
        if (currentDockWidth > defaultWidthUpperBound)
        {
            return currentDockWidth;
        }

        var minimumUsableWidth = Scale(MinimumUsableDockWidth, dpi);
        var minimumEditorWidth = Scale(MinimumEditorWidth, dpi);
        var maximumPreferredWidth = Scale(MaximumPreferredDockWidth, dpi);
        var maximumAllowedWidth = hostClientWidth - minimumEditorWidth;

        if (maximumAllowedWidth <= currentDockWidth)
        {
            return currentDockWidth;
        }

        var proportionalWidth = (int)Math.Round(
            hostClientWidth * PreferredHostFraction,
            MidpointRounding.AwayFromZero);
        var preferredWidth = Math.Max(minimumUsableWidth, proportionalWidth);
        preferredWidth = Math.Min(preferredWidth, maximumPreferredWidth);
        preferredWidth = Math.Min(preferredWidth, maximumAllowedWidth);

        return Math.Max(currentDockWidth, preferredWidth);
    }

    private static int Scale(int logicalPixels, int dpi)
    {
        return checked((int)Math.Round(
            logicalPixels * (dpi / (double)DefaultDpi),
            MidpointRounding.AwayFromZero));
    }
}
