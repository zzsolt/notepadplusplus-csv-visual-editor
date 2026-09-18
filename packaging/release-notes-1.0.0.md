# CSV Visual Editor 1.0.0

First stable Windows x64 release.

## Assets

- `CsvVisualEditor-1.0.0-win-x64.zip`
- ZIP SHA-256: `e513b9316806662dd358853c7f0a718ba78c50ca5b2992d3db23c4c0d77bd351`
- DLL SHA-256: `7ba84aab6b8e733e586e358248859ea6ccf9651f0fe3d5f53aaeacc6b2072385`

The ZIP contains `CsvVisualEditor.dll`, `LICENSE.txt` and
`THIRD_PARTY_NOTICES.txt` at its root. The root-level DLL layout is compatible
with Notepad++ Plugins Admin.

## Compatibility

- Windows x64 / 64-bit Notepad++
- Production host gate: Notepad++ 8.9.8 x64
- Native AOT: no separate end-user .NET runtime
- Interface: English, Hungarian, Simplified Chinese, Hindi, Spanish, Arabic and
  French; other Notepad++ languages fall back to English
- Apply writes are guarded for UTF-8 and Windows-1250; Save remains owned by
  Notepad++

## License

Project code: GPL-3.0-only. Third-party components retain their own licenses;
see the notices included with the package.

## Plugins Admin

The prepared x64 entry is in `packaging/nppPluginList/entry.x64.json`. Its
download URL becomes valid after this exact ZIP is attached to a public GitHub
Release tagged `v1.0.0`.
