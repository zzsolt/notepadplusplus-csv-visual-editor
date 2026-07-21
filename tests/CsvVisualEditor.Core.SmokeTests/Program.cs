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

try
{
    _ = TableColumnNameGenerator.Create(-1);
    failures.Add("Negative column count did not throw ArgumentOutOfRangeException.");
}
catch (ArgumentOutOfRangeException)
{
    // Expected.
}

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
