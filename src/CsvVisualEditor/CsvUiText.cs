namespace CsvVisualEditor;

using CsvVisualEditor.Localization;

using CsvVisualEditor.Core;

/// <summary>Presentation of structured Core values; never translate document strings.</summary>
internal static class CsvUiText
{
    internal static string ColumnName(CsvTableColumn column) => column.IsGeneratedName
        ? L10n.Get(TextKey.Common_Column) + " " + (column.Index + 1).ToString(L10n.FormattingCulture)
        : column.Name;

    internal static string Exception(Exception exception)
    {
        var details = CsvErrorDetails.From(exception);
        return details?.Code switch
        {
            CsvUserError.FilterValueTooLong => L10n.Get(TextKey.Messages_AFilterValueMayContainAtMostCharacters),
            CsvUserError.InvalidDecimal => L10n.Get(TextKey.Messages_UseAnExactDecimalNumberWithADot),
            CsvUserError.TooManyFilters => L10n.Get(TextKey.Messages_UseAtMostEightNonNullFilterConditions),
            CsvUserError.TooManySortLevels => L10n.Get(TextKey.Messages_UseAtMostThreeNonNullSortLevels),
            CsvUserError.DuplicateSortColumn => L10n.Get(TextKey.Messages_EachSortLevelMustUseADifferentColumn),
            CsvUserError.InvalidViewColumn => L10n.Get(TextKey.Messages_ViewRulesReferToAColumnOutsideThe),
            CsvUserError.ClipboardTooLarge => L10n.Get(TextKey.Messages_TheClipboardTextExceedsTheSafeMiUTF),
            CsvUserError.ClipboardTooManyRows => L10n.Get(TextKey.Messages_TheClipboardContainsMoreRowsThanTheEditor),
            CsvUserError.ClipboardTooManyColumns => L10n.Get(TextKey.Messages_TheClipboardContainsMoreColumnsThanTheEditor),
            CsvUserError.ClipboardNotRectangular => L10n.Get(TextKey.Messages_ClipboardRowsMustFormOneRectangularCellMatrix),
            CsvUserError.ClipboardTooManyCells => L10n.Get(TextKey.Messages_TheClipboardCellMatrixExceedsTheSafePaste),
            CsvUserError.ClipboardControlCharacter => L10n.Get(TextKey.Messages_TheClipboardContainsAnUnsupportedControlCharacter),
            CsvUserError.ProjectionTooManyColumns => L10n.Format(TextKey.Messages_TheParsedDocumentContainsColumnsWhichExceedsThe, details!.Arguments[0], details!.Arguments[1]),
            CsvUserError.ProjectionTooManyCells => L10n.Format(TextKey.Messages_OneVisualRowWouldRequireCellsWhichExceeds, details!.Arguments[0], details!.Arguments[1]),
            _ => L10n.Get(TextKey.Messages_TheOperationCouldNotBeCompletedCheckThe)
        };
    }

    internal static string Severity(CsvDiagnosticSeverity severity) => severity switch
    {
        CsvDiagnosticSeverity.Error => L10n.Get(TextKey.Messages_Error),
        CsvDiagnosticSeverity.Warning => L10n.Get(TextKey.Messages_Warning),
        _ => L10n.Get(TextKey.Messages_Information)
    };
    internal static string Confidence(CsvDelimiterConfidence? confidence) => confidence switch
    {
        CsvDelimiterConfidence.High => L10n.Get(TextKey.Messages_High),
        CsvDelimiterConfidence.Medium => L10n.Get(TextKey.Messages_Medium),
        CsvDelimiterConfidence.Low => L10n.Get(TextKey.Messages_Low),
        CsvDelimiterConfidence.None => L10n.Get(TextKey.Messages_None),
        _ => L10n.Get(TextKey.Messages_Unknown)
    };
    internal static string SortDirection(CsvTableSortDirection direction) => direction switch
    {
        CsvTableSortDirection.Ascending => L10n.Get(TextKey.Common_Ascending),
        CsvTableSortDirection.Descending => L10n.Get(TextKey.Common_Descending),
        _ => L10n.Get(TextKey.Common_OriginalOrder)
    };
    internal static string SortKind(CsvSortKind kind) => kind == CsvSortKind.Number ? L10n.Get(TextKey.Common_Number) : L10n.Get(TextKey.Common_Text);
    internal static string Combination(CsvFilterCombination combination) => combination == CsvFilterCombination.All ? L10n.Get(TextKey.Messages_ALL) : L10n.Get(TextKey.Messages_ANY);
    internal static string Delimiter(char delimiter) => delimiter switch
    {
        ',' => L10n.Get(TextKey.Table_Comma2), ';' => L10n.Get(TextKey.Table_Semicolon2), '\t' => L10n.Get(TextKey.Table_Tab2), _ => delimiter.ToString()
    };
    internal static string Diagnostic(CsvDiagnostic diagnostic) => diagnostic.Code switch
    {
        CsvDiagnosticCodes.UnexpectedQuoteInUnquotedField => L10n.Get(TextKey.Messages_AQuoteAppearedInsideAnUnquotedField),
        CsvDiagnosticCodes.UnexpectedCharacterAfterClosingQuote => L10n.Get(TextKey.Messages_UnexpectedCharactersFollowedAClosingQuote),
        CsvDiagnosticCodes.UnterminatedQuotedField => L10n.Get(TextKey.Messages_TheFinalQuotedFieldWasNotTerminatedBefore),
        CsvDiagnosticCodes.InconsistentFieldCount when diagnostic.ActualFieldCount.HasValue && diagnostic.ExpectedFieldCount.HasValue =>
            L10n.Format(TextKey.Messages_RecordHasFieldsExpectedBasedOnTheSampled, diagnostic.ActualFieldCount.Value, diagnostic.ExpectedFieldCount.Value),
        CsvDiagnosticCodes.InconsistentFieldCount => L10n.Get(TextKey.Messages_TheNumberOfFieldsDiffersFromTheOther),
        CsvDiagnosticCodes.LeadingBomRemoved => L10n.Get(TextKey.Messages_ALeadingByteOrderMarkWasExcludedFrom),
        CsvDiagnosticCodes.LowDelimiterConfidence => L10n.Get(TextKey.Messages_TheSuggestedDelimiterHasLowConfidenceCheckThe),
        CsvDiagnosticCodes.NoReliableDelimiter => L10n.Get(TextKey.Messages_NoSupportedDelimiterProducedASufficientlyReliableMulti),
        CsvDiagnosticCodes.AmbiguousDelimiter => L10n.Get(TextKey.Messages_TwoDelimiterCandidatesHaveSimilarScoresCheckThe),
        _ => L10n.Format(TextKey.Messages_DiagnosticCode, diagnostic.Code)
    };
    internal static string DescribeSpaces(string value)
    {
        var count = value.Count(static c => c == ' ');
        var leading = 0; while (leading < value.Length && value[leading] == ' ') leading++;
        var trailing = 0; while (trailing < value.Length && value[value.Length - trailing - 1] == ' ') trailing++;
        return L10n.Format(TextKey.Messages_SpacesLeadingTrailingLengthUTFUnitsOrangeDots, count, leading, trailing, value.Length);
    }
}
