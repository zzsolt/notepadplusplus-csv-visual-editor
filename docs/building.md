# Building and contributing

Build on Windows with Python 3.11 or later, the .NET 10 SDK and the Visual Studio C++ build tools and Windows SDK required by Native AOT.

```powershell
python -m unittest discover -s tests/localization -p 'test_*.py'
python tools/localization/generate.py --check
python tools/localization/verify.py --check-upstream
dotnet run --project tests/CsvVisualEditor.Core.Tests -c Release
dotnet publish src/CsvVisualEditor/CsvVisualEditor.csproj -c Release -r win-x64 -o artifacts/publish
```

The output plugin is `artifacts/publish/CsvVisualEditor.dll`. Install it in an isolated portable 64-bit Notepad++ instance for integration testing. CI runs parser/edit tests, Native AOT UI checks and production-DLL host tests before packaging.

Keep parsing, validation and editing independent of WinForms and the host. Use the existing document coordinator; preserve raw CSV values, conflict and encoding checks, and Notepad++ Save ownership. Add regression tests for behavior changes. Document user-visible changes concisely in the changelog and use concise product-focused pull request descriptions.

## Interface translations

English source messages and translated catalogs live in `src/CsvVisualEditor.Localization/Catalogs`. Use stable keys, translator context and exact source hashes. Update every language in `languages.json` whenever interface text changes. Preserve format arguments and invariant numeric examples; translations are embedded in the same DLL.

Validation rejects missing or stale catalogs, malformed placeholders and unreviewed new UI literals. CI checks the official Notepad++ language inventory and representative localized startup, menus and dialogs. Unknown host languages use English; document encoding and CSV numeric semantics do not select the display language.

All commands above must succeed before publishing a build. English-only bootstrap checks and staged translation drafts do not establish a complete localized release. Do not bypass a failed catalog check to generate a distributable package.

Use `tools/localization/import_catalogs.py` to validate external keyed catalogs before promotion. Its default mode rejects incomplete sets; `--stage` writes drafts only outside shipped catalogs and returns a nonzero status while coverage is incomplete. Legacy indexed input requires an exact source-order fingerprint; related languages and script variants are never silently substituted. Run the full validation above after reviewing and promoting imported catalogs.
