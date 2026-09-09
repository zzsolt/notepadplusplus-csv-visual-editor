# Building and contributing

Build on Windows with the .NET 10 SDK and the Visual Studio C++ build tools and Windows SDK required by Native AOT.

```powershell
dotnet run --project tests/CsvVisualEditor.Core.Tests -c Release
dotnet publish src/CsvVisualEditor/CsvVisualEditor.csproj -c Release -r win-x64 -o artifacts/publish
```

The output plugin is `artifacts/publish/CsvVisualEditor.dll`. Install it in an isolated portable 64-bit Notepad++ instance for integration testing. The CI workflow also runs parser/edit tests, Native AOT UI checks and a production-DLL host test before packaging.

Keep parsing, validation and editing logic independent of WinForms and the host. Read and write the active editor through the existing document coordinator; do not bypass conflict/encoding checks or write directly to disk. Add regression tests for behavior changes, preserve raw CSV values and document user-visible changes concisely in the changelog.

Use concise product-focused pull request descriptions. Keep source builds reproducible and avoid unrelated dependencies or generated binaries in commits.
