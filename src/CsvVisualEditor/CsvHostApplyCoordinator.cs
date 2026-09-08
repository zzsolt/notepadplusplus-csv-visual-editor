namespace CsvVisualEditor;

using CsvVisualEditor.Core;

/// <summary>
/// The host's explicit write authorization, separate from Core's conservative default.
/// Windows-1250 was owner-accepted in milestone 0.10. Other profiled encodings are NOT
/// implicitly authorized. Strict whole-replacement round-trip and conflict preflight
/// still execute before the native replacement target is constructed.
/// </summary>
internal static class CsvHostApplyCoordinator
{
    private static readonly CsvEncodingApplyPolicy Policy = CsvEncodingApplyPolicy.Create(
        [CsvEncodingProfiles.Utf8CodePage, CsvEncodingProfiles.Windows1250CodePage]);

    internal static CsvEditorApplyResult Execute(CsvRowEditModel model,
        ActiveDocumentSnapshot freshSnapshot, Func<IEditorReplacementTarget> createTarget) =>
        CsvEditorApplyCoordinator.Execute(model, freshSnapshot, createTarget, Policy);
}
