namespace CsvVisualEditor.Core;

/// <summary>
/// Host abstraction for obtaining a complete immutable copy of the currently
/// active editor buffer.
/// </summary>
public interface IActiveDocumentReader
{
    ActiveDocumentSnapshot ReadActiveDocument();
}