namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor.Core;
using System.Runtime.CompilerServices;
using System.Text;

internal static class SourceNavigationNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string text = "Név,City\r\nÁrvíz,Budapest\r\n";
        var byteLength = new UTF8Encoding(false, true).GetByteCount(text);
        var snapshot = ActiveDocumentSnapshot.Create(
            @"C:\Data\native-aot.csv",
            text,
            byteLength,
            CsvEncodingProfiles.Utf8CodePage,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 8, 19, 7, 45, 0, TimeSpan.Zero));
        var parse = CsvParser.Parse(text, CsvDialect.Create(','));
        var plan = CsvSourceNavigationPlanner.Create(
            snapshot,
            snapshot,
            parse,
            new CsvSourceNavigationAddress(1, 1));

        Require(plan.IsReady, "Native AOT source-navigation plan was not ready.");
        Require(
            plan.CaretBytePosition > plan.AnchorBytePosition,
            "Native AOT source-navigation cell selection is empty.");
        Require(
            plan.AnchorBytePosition > parse.Records[1].Cells[1].SourceSpan.Start,
            "Native AOT source-navigation did not account for preceding UTF-8 multibyte characters.");

        var mismatch = ActiveDocumentSnapshot.Create(
            @"C:\Data\native-aot.csv",
            text,
            byteLength + 1,
            CsvEncodingProfiles.Utf8CodePage,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 8, 19, 7, 45, 0, TimeSpan.Zero));
        var blocked = CsvSourceNavigationPlanner.Create(
            mismatch,
            mismatch,
            parse,
            new CsvSourceNavigationAddress(1, 1));
        Require(
            blocked.Status == CsvSourceNavigationStatus.EditorByteLengthMismatch,
            "Native AOT source navigation did not fail closed on byte-length mismatch.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
