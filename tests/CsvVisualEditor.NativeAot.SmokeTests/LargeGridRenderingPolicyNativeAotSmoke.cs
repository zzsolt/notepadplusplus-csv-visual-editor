namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor.Core;
using System.Runtime.CompilerServices;

internal static class LargeGridRenderingPolicyNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        Require(
            !CsvGridRenderingPolicy.ShouldUseVirtualReadOnlyRows(999, 10),
            "Small read-only grid unexpectedly selected virtual rows.");
        Require(
            CsvGridRenderingPolicy.ShouldUseVirtualReadOnlyRows(1_000, 1),
            "Large row count did not select virtual rows.");
        Require(
            CsvGridRenderingPolicy.ShouldUseVirtualReadOnlyRows(800, 50),
            "Large cell count did not select virtual rows.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
