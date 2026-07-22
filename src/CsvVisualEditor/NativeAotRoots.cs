namespace CsvVisualEditor;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

/// <summary>
/// Preserves narrowly scoped WinForms members that the framework activates through reflection.
/// </summary>
internal static class NativeAotRoots
{
    [ModuleInitializer]
    [DynamicDependency(
        DynamicallyAccessedMemberTypes.PublicParameterlessConstructor,
        typeof(DataGridViewRowHeaderCell))]
    internal static void Initialize()
    {
        // DataGridViewBand creates its default row-header cell through Activator.
        // Native AOT must retain that public constructor metadata explicitly.
    }
}
