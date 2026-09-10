# Building and contributing

Build on Windows with Python 3.11 or later, the .NET 10 SDK and the Visual Studio C++ build tools and Windows SDK required by Native AOT.

```powershell
dotnet run --project tests/CsvVisualEditor.Core.Tests -c Release
dotnet publish src/CsvVisualEditor/CsvVisualEditor.csproj -c Release -r win-x64 -o artifacts/publish
```

The output plugin is `artifacts/publish/CsvVisualEditor.dll`. Install it in an isolated portable 64-bit Notepad++ instance for integration testing. The CI workflow also runs parser/edit tests, Native AOT UI checks and a production-DLL host test before packaging.

Keep parsing, validation and editing logic independent of WinForms and the host. Read and write the active editor through the existing document coordinator; do not bypass conflict/encoding checks or write directly to disk. Add regression tests for behavior changes, preserve raw CSV values and document user-visible changes concisely in the changelog.

Use concise product-focused pull request descriptions. Keep source builds reproducible and avoid unrelated dependencies or generated binaries in commits.

## Interface translations

English source messages and translated catalogs live in `src/CsvVisualEditor.Localization/Catalogs`. Use stable keys, translator context and exact source hashes. Update every language in `languages.json` when adding or changing interface text; keep format arguments and invariant numeric examples intact. All catalogs are embedded in the same DLL.

```powershell
python -m unittest discover -s tests/localization -p 'test_*.py'
python tools/localization/generate.py --check
python tools/localization/verify.py --check-upstream
```

Build validation rejects missing/stale catalogs, invalid placeholders and unreviewed new UI literals. CI also checks the current official Notepad++ language inventory and representative localized startup, menus and dialogs in the published DLL. Unknown host languages use English; document encoding and CSV numeric semantics do not select the display language.
