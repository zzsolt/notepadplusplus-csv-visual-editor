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

## Additional source-audit corrections

Value cells in the main grid and Transform use a monospaced font so adjacent spaces
have readable, equal advances. Header/menu text retains the normal UI font. Raw CSV
values, clipboard contents and source offsets do not change. Font ownership is local
to the control/dialog and is disposed with it.

The Notepad++ Plugins > About entry now opens the same version-aware dialog as the
panel's About menu; the stale hard-coded 0.12.4 message was removed.

The production Apply call had still used Core's conservative UTF-8-only default,
contrary to the previously accepted Windows-1250 host behavior. The explicit host
policy now authorizes UTF-8 and Windows-1250 only. It retains complete strict
encode/decode round-trip checks before native target construction and one undo action.
Windows-1252, ASCII and unknown code pages are not newly authorized. Native AOT tests
exercise the same host coordinator for successful writes, unrepresentable text,
unapproved code pages and a source conflict; rejected operations preserve pending edits.
This is automated target simulation, not a fresh Notepad++ acceptance result.

A failed snapshot read now retires the preceding asynchronous build and source
baseline before reading. Previously the old build could repopulate the error state
when a newer refresh failed. The generation/token guard remains active at presentation.
Actual host scheduling and failed-read recovery still require host regression testing.

## Magyar hostellenőrzés

Új 0.12.5 hosteredmény: **NOT RUN**. A korábbi képek hibajelentések, nem új PASS.

1. Bezárt Notepad++ mellett frissítsd a DLL-t az új, egyedi nevű ZIP-ből.
   A panel About és a Plugins > CSV Visual Editor > About ugyanazt a verziót,
   fejlesztői nevet, e-mail-címet és támogatási szöveget mutassa.
2. Három szóköz három külön, azonos méretű tömör pont legyen: csak szóközös
   cellában, szöveg előtt/után és belül, valamint a Transform előnézetében.
   Ellenőrizd kijelölésnél, világos/sötét módban és a használt DPI-n.
3. Gépelj a keresőbe. A jelölés és a számláló a találati cellákat kövesse.
   Enter/Shift+Enter és a nyílgombok lépjenek körbe. A gyors Enter mindig a legutóbb
   begépelt szöveget használja. Clear őrizze meg a rendezést; Reset view állítsa
   vissza a teljes nézetet. Oszlopváltás/rendezés után ne maradjon régi jelölés.
4. Szűkítsd, majd szélesítsd a panelt: szűk helyen két sor legyen.
   Search > Find közvetlenül a keresőbe vigyen, ne nyisson beviteli almenüt.
5. Ellenőrizd a másolást, Edit/Transform/Apply/Revert és egy Undo/Redo kört.
   A jelölők nem kerülhetnek a CSV-be vagy a vágólapra. Nyisd/csukd újra a panelt.
6. Ismert Windows-1250 Scintilla-kódlapon a magyar ékezetes módosítás legyen
   veszteségmentes és egy lépésben visszavonható. Nem ábrázolható karakter esetén
   Apply blokkoljon, és a függő módosítás maradjon meg. A menüben látható fájlkódolás
   önmagában nem bizonyítja a Scintilla kódlapját. Más kódlap nem kapott új engedélyt.

A pontos Windows/Notepad++ x64 verzió és csomagazonosító mellett az esetek
PASS/FAIL/NOT RUN állapotát kell rögzíteni. A fennmaradó Source F2–F4/G esetek
nem válnak elfogadottá a GUI vagy az automatizált tesztek sikerétől. PR #12 marad draft.
