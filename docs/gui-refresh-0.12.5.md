# 0.12.5 GUI refresh

## Behavior

The search bar owns the only query editor. Search > Find in table focuses it directly.
Typing filters displayed rows with the existing trimmed, ordinal-ignore-case query.
Column scope and sorting remain view-only. The counter counts matching **cells**, not
occurrences. Next/previous wrap in visible row-major order. Enter while the debounce
is pending first applies the latest query; it never jumps using the previous query.

The search close button / Escape in the field clears only text. View > Reset view
also resets column scope and sorting. Search is disabled in Edit mode as before.
Native selection colors are preserved. A no-matches message offers a recovery hint.
Shortcuts apply in the plugin, not as a replacement for the Notepad++ editor's Find.

Solid orange space dots use the same layout as the original text. Their size is fixed
within each font/DPI paint pass, with a one-pixel gap reserved for adjacent spaces.
No spaces are inserted, replaced or widened in the underlying data. Full values are
searched even when the paint-only 1,024 UTF-16-unit limit truncates displayed text.

About shows package version, Zolnai Zsolt, zzsolt@gmail.com, the project link and a
support invitation. Email and project links open only when clicked. The dialog does
not read CSV content, transmit diagnostics, or assume a donation provider.

## Automated coverage

Core tests cover matching-cell indexing, column scope, trimmed query parity, sorted
row identity, full long values, empty results, binary-search lookup, navigation
wrapping, a 20,000-hit index and invalid sort-direction rejection.

Native AOT checks real grid frame painting and clearing, invalidation after mutation,
consecutive space components and equal marker areas at 96/120/144/168/192 DPI using
9/10/12-point fonts, 40-space measurement batches, 260/400/750-pixel light/dark search
layouts, About content/version and repeated menu install/disposal. CI publishes
synthetic review images for three days. These do not establish real-host acceptance.

## Magyar hostellenorzes

Uj 0.12.5 hosteredmeny: **NOT RUN**. A korabbi kepek hibajelentesek; nem uj PASS.

1. Bezart Notepad++ mellett frissitsd a CsvVisualEditor.dll-t az uj, egyedi nevu ZIP-bol.
   Az About menuben ellenorizd a verzioszamot, a fejleszto nevet, az e-mail-cimet es a tamogatasi szoveget.
2. Harom egymas melletti szokoz mindenutt harom kulon, azonos meretu tomor pont legyen:
   csak szokozos cellaban, szoveg elott/utan es belul, a Transform elonezeteben is.
   Vizsgald meg kijelolesnel, vilagos/sotet modban es a hasznalt DPI-n.
3. Gepelj a keresobe. A talalati cellak latszodjanak, a szamlalo cellakat szamoljon.
   Enter/Shift+Enter es a nyilgombok lepjenek korbe a talalatokon. A gyors Enter
   mindig a legutobb begepelt szoveget hasznalja. Clear utan a rendezes maradjon meg;
   Reset view allitsa vissza a teljes nezetet. Oszlopvaltas/rendezes utan ne maradjon regi jeloles.
4. Szukitsd, majd szelesitsd a panelt: a kereso ne tunjon el, szuk helyen ket sor legyen.
   Search > Find kozvetlenul a keresobe vigyen; ne nyisson ujabb beviteli almenut.
5. Ellenorizd a masolast, Edit/Transform/Apply/Revert es egy Undo/Redo kor mukodeset.
   A jelolok nem kerulhetnek a CSV-be vagy a vagolapra. Nyisd/csukd ujra a panelt.

A pontos Windows/Notepad++ x64 verzio es csomagazonosito mellett az egyes esetek
PASS/FAIL/NOT RUN allapotat kell rogzeni. A fennmarado Source F2-F4/G esetek nem
valnak elfogadotta a GUI vagy az automatizalt tesztek sikeretol. PR #12 marad draft.
