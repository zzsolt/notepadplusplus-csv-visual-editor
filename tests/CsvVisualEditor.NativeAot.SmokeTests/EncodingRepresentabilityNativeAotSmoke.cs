namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor.Core;
using System.Runtime.CompilerServices;

internal static class EncodingRepresentabilityNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string hungarian =
            "\u00c1rv\u00edzt\u0171r\u0151 t\u00fck\u00f6rf\u00far\u00f3g\u00e9p";

        var utf8 = CsvEncodingRepresentability.Evaluate(
            65001,
            hungarian + " \u6771\u4eac \U0001F600");
        Require(utf8.IsExact, "Native AOT UTF-8 strict round trip failed.");

        var windows1250 = CsvEncodingRepresentability.Evaluate(1250, hungarian);
        Require(
            windows1250.IsExact,
            "Native AOT Windows-1250 Hungarian round trip failed.");

        var rejected = CsvEncodingRepresentability.Evaluate(1250, "\u6771\u4eac");
        Require(
            rejected.Status == CsvEncodingRepresentabilityStatus.TextNotRepresentable,
            "Native AOT Windows-1250 did not reject unrepresentable text.");

        var defaultPolicy = CsvEncodingApplyPolicy.Utf8Only.Evaluate(1250, hungarian);
        Require(
            defaultPolicy.Status == CsvEncodingApplyPreflightStatus.HostWriteNotEnabled,
            "Native AOT conservative default unexpectedly enabled a legacy host write.");

        var testPolicy = CsvEncodingApplyPolicy.Create([1250]).Evaluate(1250, hungarian);
        Require(
            testPolicy.IsReady,
            "Native AOT explicit Windows-1250 policy did not complete strict preflight.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
