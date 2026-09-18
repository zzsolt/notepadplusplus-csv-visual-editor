# Notepad++ Plugins Admin submission

CSV Visual Editor 1.0.1 targets 64-bit Notepad++ and therefore belongs in
`src/pl.x64.json` of the official `notepad-plus-plus/nppPluginList` repository.

The stable release gate generates `nppPluginList-entry.x64.json` from the exact
validated ZIP. Do not hand-edit its SHA-256. The release ZIP must expose
`CsvVisualEditor.dll` at its root; Plugins Admin creates the
`plugins/CsvVisualEditor` installation directory.

Before submitting upstream:

1. Publish the exact `CsvVisualEditor-1.0.1-win-x64.zip` at the versioned URL in
   the generated entry and confirm it is publicly downloadable.
2. Confirm the generated `id` equals the public ZIP SHA-256 and DLL file version
   is `1.0.1.0` while the JSON version is `1.0.1`.
3. Test the entry with the current debug Plugins Admin workflow, including
   install and removal on 64-bit Notepad++. The declared compatibility floor is
   8.9.8 because that is the production host version validated by this project.
4. Fork `notepad-plus-plus/nppPluginList`, add only the generated object to
   `src/pl.x64.json`, run its validator/tests, and submit the upstream PR.

The checked-in `entry.template.json` intentionally has a SHA placeholder.
After the final release build, the exact generated entry and release manifest
are recorded with the release evidence.

Official references:

- https://npp-user-manual.org/docs/plugins/#plugins-admin
- https://github.com/notepad-plus-plus/nppPluginList
- https://github.com/notepad-plus-plus/nppPluginList/blob/master/validator.py


## Local Plugins Admin test for the first official submission

The public v1.0.1 release URL and SHA-256 are live. A full current x64 plugin-list
snapshot with the CSV Visual Editor entry already inserted is checked in as
`nppPluginList.test.x64.json` for local Plugins Admin validation.

For the first official-list submission, verify the published 1.0.1 entry locally:

1. Use a recent 64-bit portable Notepad++ in a disposable debug directory.
2. Use the matching debug GUP build required by the Notepad++ local test workflow.
3. Copy `nppPluginList.test.x64.json` to
   `plugins/Config/nppPluginList.json`.
4. Confirm CSV Visual Editor 1.0.1 is listed and can be installed, restarted,
   loaded, deactivated, re-activated and removed successfully.
5. Submit only the generated object from `entry.x64.json` to upstream
   `src/pl.x64.json`.

An update from a previous official Plugins Admin version is applicable to later
releases once such a prior official entry exists. The disposable test snapshot
is support material only; the upstream PR modifies only the official JSON list.

