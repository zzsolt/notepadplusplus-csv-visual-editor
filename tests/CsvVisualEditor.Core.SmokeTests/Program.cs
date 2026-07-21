using CsvVisualEditor.Core;

var failures = new List<string>();

AssertSequence(
    TableColumnNameGenerator.Create(3),
    ["Column 1", "Column 2", "Column 3"],
    "Three generated column names");
AssertSequence(
    TableColumnNameGenerator.Create(0),
    [],
    "Zero generated column names");
AssertThrows<ArgumentOutOfRangeException>(
    () => _ = TableColumnNameGenerator.Create(-1),
    "Negative column count");

var capturedAt = new DateTimeOffset(
    year: 2026,
    month: 7,
    day: 21,
    hour: 12,
    minute: 0,
    second: 0,
    offset: TimeSpan.FromHours(2));
var snapshot = ActiveDocumentSnapshot.Create(
    "C:/Data/sample.csv",
    "a,b\r\n1,2",
    editorByteLength: 8,
    codePage: 65001,
    caretPosition: 5,
    anchorPosition: 2,
    isModified: true,
    capturedAt);

AssertEqual(snapshot.DisplayName, "sample.csv", "Snapshot display name");
AssertEqual(snapshot.DocumentPath, "C:/Data/sample.csv", "Snapshot document path");
AssertEqual(snapshot.CharacterCount, 8, "Snapshot character count");
AssertEqual(snapshot.EditorByteLength, 8L, "Snapshot editor byte length");
AssertEqual(snapshot.CodePage, 65001, "Snapshot code page");
AssertEqual(snapshot.CaretPosition, 5L, "Snapshot caret position");
AssertEqual(snapshot.AnchorPosition, 2L, "Snapshot anchor position");
AssertEqual(snapshot.SelectionLength, 3L, "Snapshot selection length");
AssertEqual(snapshot.IsModified, true, "Snapshot modified state");
AssertEqual(
    snapshot.CapturedAtUtc,
    new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero),
    "Snapshot UTC timestamp");
AssertEqual(
    snapshot.ContentSha256,
    "a64a34aacbdacd17c0c52c867be14c6b9dab76b5e7348392eb829431fbba3a33",
    "Snapshot SHA-256");

var emptySnapshot = ActiveDocumentSnapshot.Create(
    documentPath: null,
    text: string.Empty,
    editorByteLength: 0,
    codePage: 65001,
    caretPosition: 0,
    anchorPosition: 0,
    isModified: false,
    DateTimeOffset.UnixEpoch);
AssertEqual(emptySnapshot.DisplayName, "Untitled", "Untitled snapshot display name");
AssertEqual(emptySnapshot.SelectionLength, 0L, "Empty snapshot selection length");
AssertEqual(
    emptySnapshot.ContentSha256,
    "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "Empty snapshot SHA-256");

AssertThrows<ArgumentNullException>(
    () => _ = ActiveDocumentSnapshot.Create(
        string.Empty,
        text: null!,
        editorByteLength: 0,
        codePage: 65001,
        caretPosition: 0,
        anchorPosition: 0,
        isModified: false,
        DateTimeOffset.UtcNow),
    "Null snapshot text");
AssertThrows<ArgumentOutOfRangeException>(
    () => _ = ActiveDocumentSnapshot.Create(
        string.Empty,
        string.Empty,
        editorByteLength: -1,
        codePage: 65001,
        caretPosition: 0,
        anchorPosition: 0,
        isModified: false,
        DateTimeOffset.UtcNow),
    "Negative editor byte length");
AssertThrows<ArgumentOutOfRangeException>(
    () => _ = ActiveDocumentSnapshot.Create(
        string.Empty,
        string.Empty,
        editorByteLength: 0,
        codePage: 65001,
        caretPosition: -1,
        anchorPosition: 0,
        isModified: false,
        DateTimeOffset.UtcNow),
    "Negative caret position");
AssertThrows<ArgumentOutOfRangeException>(
    () => _ = ActiveDocumentSnapshot.Create(
        string.Empty,
        string.Empty,
        editorByteLength: 0,
        codePage: 65001,
        caretPosition: 0,
        anchorPosition: -1,
        isModified: false,
        DateTimeOffset.UtcNow),
    "Negative anchor position");

if (failures.Count == 0)
{
    Console.WriteLine("All CsvVisualEditor.Core smoke tests passed.");
    return 0;
}

Console.Error.WriteLine("CsvVisualEditor.Core smoke tests failed:");
foreach (var failure in failures)
{
    Console.Error.WriteLine($"- {failure}");
}

return 1;

void AssertSequence(
    IReadOnlyList<string> actual,
    IReadOnlyList<string> expected,
    string testName)
{
    if (actual.SequenceEqual(expected, StringComparer.Ordinal))
    {
        return;
    }

    failures.Add(
        $"{testName}: expected [{string.Join(", ", expected)}], " +
        $"got [{string.Join(", ", actual)}].");
}

void AssertEqual<T>(T actual, T expected, string testName)
{
    if (EqualityComparer<T>.Default.Equals(actual, expected))
    {
        return;
    }

    failures.Add($"{testName}: expected '{expected}', got '{actual}'.");
}

void AssertThrows<TException>(Action action, string testName)
    where TException : Exception
{
    try
    {
        action();
        failures.Add($"{testName}: expected {typeof(TException).Name}.");
    }
    catch (TException)
    {
        // Expected.
    }
    catch (Exception exception)
    {
        failures.Add(
            $"{testName}: expected {typeof(TException).Name}, " +
            $"got {exception.GetType().Name}.");
    }
}