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

## Local development and candidate builds

Normal pull-request changes run inexpensive Linux validation; documentation-only
changes do not start CI. Windows publishing and both production-DLL host reviews
run only for a source-changing `[test-package]` head commit or manual CI dispatch.
They remain mandatory before distributing a candidate. There are no per-language
hosted translation jobs.

The same complete candidate gate can run without GitHub Actions on a Windows
machine with PowerShell 7 and the build prerequisites above:

```powershell
pwsh -File tools/build-local.ps1
```

Each invocation uses a distinct version and output directory. Catalog validation
precedes compilation; no ZIP is produced after a failed validation or host test.

For offline translation preparation, `tools/localization/translate_local.py`
reuses one predownloaded CTranslate2 model/tokenizer across languages and processes
only missing, invalid or stale messages. Install the optional development-only
packages in `requirements-offline.txt` into a separate Python environment. The
existing draft model is `Nextcloud-AI/madlad400-3b-mt-ct2-int8`, revision
`aa32bbdeba7880eff2096ec044cb155a340a9400`; the tool itself never downloads it.

```powershell
python tools/localization/translate_local.py --plan --output work/drafts --report work/plan.json
python tools/localization/translate_local.py --model-dir C:/models/madlad400 --output work/drafts --report work/translation-report.json
```

Use `--resume` explicitly to continue an existing draft directory. No model is
loaded when all requested messages are current. Unsupported regional/script
variants require explicit catalogs; related languages are not substituted.
Invalid protected arguments reject the whole message instead of translating
sentence fragments. Exit code 2 means that full catalog coverage is incomplete.
Drafts, including structurally valid machine-assisted text, require contextual
review before import and the complete build gate before distribution.

Legacy curated inputs in `tools/localization/curated` are pinned by `manifest.json`
to both an English key-order fingerprint and exact input-file hashes. Updating
English invalidates that pin; recomputing a command-line fingerprint alone does
not authorize reusing index-based translations with a different source.
