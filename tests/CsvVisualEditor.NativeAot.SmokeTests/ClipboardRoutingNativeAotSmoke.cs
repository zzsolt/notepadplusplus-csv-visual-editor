namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor.Core;
using System.Runtime.CompilerServices;

internal static class ClipboardRoutingNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        Require(
            CsvClipboardCommandRouting.ResolvePasteTarget(true, "single") ==
                CsvClipboardPasteRoutingTarget.InCellEditor,
            "Native AOT single-cell in-editor paste routing mismatch.");
        Require(
            CsvClipboardCommandRouting.ResolvePasteTarget(true, "A\tB\r\nC\tD\r\n") ==
                CsvClipboardPasteRoutingTarget.Grid,
            "Native AOT spreadsheet paste was not promoted from the cell editor to the grid.");
        Require(
            CsvClipboardCommandRouting.ResolvePasteTarget(false, "single") ==
                CsvClipboardPasteRoutingTarget.Grid,
            "Native AOT grid-focus paste routing mismatch.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
