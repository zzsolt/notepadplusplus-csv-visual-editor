# Transform — részletes Notepad++ tesztelés

Ezek az új funkció valódi Notepad++-tesztjei. Állapotuk a felhasználó eredményéig **NOT RUN**. A korábbi Source F1 sikerét külön rögzítettük; az nem helyettesíti ezeket.

## Telepítés és előkészítés

1. A PR #12-ben megjelölt, teljesen zöld **0.12.1 Transform tesztjelölt** csomagot töltsd le. A ZIP neve most már buildenként változik: `CsvVisualEditor-0.12.1-alpha.<futásszám>.<próbálkozás>-win-x64.zip`. A korábbi Source-csomag még nem tartalmazza a Transform gombot.
2. Zárd be a Notepad++-t, majd a csomag `CsvVisualEditor/CsvVisualEditor.dll` fájljával cseréld le a plugin DLL-jét a Notepad++ `plugins/CsvVisualEditor` mappájában. Ha a GitHub-letöltés egy újabb ZIP-et tartalmaz, azt is bontsd ki.
3. Indítsd el a Notepad++ x64-et. Írd fel a Windows és a Notepad++ verzióját. A telepített DLL SHA-256 értékét a PowerShell `Get-FileHash` parancsával kérheted le; a privát elérési utat nem kell elküldened.
4. Nyiss új, nem mentett UTF-8 dokumentumot, és másold bele ezt a kizárólag tesztelésre kitalált CSV-t. Az idézőjeleken belüli szóközök fontosak:

   ```csv
   Name,City,Note
   "  anna  ","  Budapest  ","alpha alpha"
   "  BÉLA  ","  Szeged  ","Alpha beta"
   "  csilla  ","  Pécs  ","keep  inner  spaces"
   ```

5. Nyisd meg a CSV Visual Editort. Válaszd a vessző elválasztót és az első rekordot fejlécnek értelmező beállítást, majd Refresh. Három adatsort és három valódi adatoszlopot kell látnod.

**A két elfogadó gomb szerepe:** a Transform ablak **Accept changes** gombja csak a függő táblaszerkesztéseket módosítja. A fő eszköztár **Apply** gombja írja ezeket a Notepad++ szövegébe. A **Revert All** az összes függő szerkesztést eldobja, az esetleges korábbi kézi cellaszerkesztéseket is.

## T1 — Egy cella, előnézet és megszakítás

1. Read-only módban a Transform legyen letiltva. Kattints az Edit gombra.
2. Kattints egyszer az első sor `anna` cellájába, majd Transform.
3. Operation: **Trim outer whitespace**. Scope: **Selected cells / rows**. Kattints Preview-ra.
4. Elvárt számlálók: **1 target cell, 1 change, 1 row**. A Before `⟦··anna··⟧ (8)`, az After `⟦anna⟧ (4)`. A `·` szóközt jelöl; a zárójelben a teljes UTF-16-hossz látható. A jelölések kizárólag az előnézethez tartoznak, nem kerülnek az adatba. Eddig sem a táblának, sem a Notepad++ szövegének nem szabad átalakulnia.
5. Cancel. Ne legyen új függő módosítás.
6. Nyisd meg újra, állítsd be ugyanezt, Preview, majd Accept changes. Csak az első név körüli szóközök tűnjenek el; a városok és a többi név maradjanak szóközösek. A Notepad++ szövege még maradjon az eredeti.
7. Revert All: a szóközök térjenek vissza, a függő módosítások száma legyen nulla.

## T2 — Szövegcsere egy oszlopban

1. Edit módban kattints az első sor Note cellájába. Transform.
2. Operation: **Replace text (literal)**; Scope: **Current column**; Find: `alpha`; Replace with: `X`; **Match case** bekapcsolva.
3. Preview: **3 célcella, 1 módosult cella**. Az első megjegyzés `X X` lesz az előnézetben; a nagy A-val kezdődő második megjegyzés még nem változik.
4. Kapcsold ki a Match case-t. A régi előnézet törlődjön, az Accept changes legyen letiltva.
5. Preview újra: **2 módosult cella**; a második megjegyzés `X beta`.
6. Accept changes: csak ez a két megjegyzés változzon a táblában. A nevek, városok és a harmadik megjegyzés maradjanak eredetiek; a Notepad++ szövege még ne változzon.
7. Revert All. Üres Find esetén ne lehessen végrehajtható előnézetet kapni. Az üres Replace with viszont megengedett: az a találatok törlését jelenti.

## T3 — Teljes sor és beszúrt sor

1. Edit módban válaszd ki a teljes második sort a sorfejléccel, a `#` mezővel vagy a Select jelölőnégyzettel.
2. Transform → Trim outer whitespace → Selected cells / rows → Preview.
3. Elvárt: **3 célcella, 2 módosult cella**. A név és a város tisztul; a `#` és Select nem jelenhet meg adatmódosításként.
4. Accept changes után a teljes sor kijelölése maradjon meg. A Delete Rows továbbra is ezt a kijelölést ismerje fel. Most ne töröld, hanem Revert All.
5. Add Row; az új sor egy cellájába írj például `  proba  ` értéket. Válaszd ki ezt a cellát, majd Trim → Preview → Accept changes. Az új sorban is `proba` legyen az eredmény.
6. Apply előtt az új sornak továbbra sincs eredeti Source-helye. Revert All távolítsa el a beszúrt sort, és állítsa vissza az eredeti adatokat.

## T4 — Egész tábla, kis- és nagybetűk

1. Az eredeti tesztadatokon: Edit → Transform → Trim outer whitespace → All data cells → Preview.
2. Elvárt: **9 célcella, 6 módosult cella, 3 módosult sor**. A fejléc nem változik.
3. Accept changes után a nevek és városok szélső szóközei tűnjenek el. A `keep  inner  spaces` belső dupla szóközei maradjanak meg.
4. Ugyanerre a táblára új Trim-előnézet: **0 módosult cella**, Accept changes letiltva. Cancel.
5. Próbáld ki az **UPPERCASE**, majd a **lowercase** előnézetet is. Az ékezetek maradjanak helyesek, például BÉLA → béla, Pécs → PÉCS. Beállításváltás után mindig új Preview szükséges.
6. Revert All az eredeti állapotot állítsa vissza.

## T5 — Apply, egyetlen Undo/Redo és ütközés

1. Az eredeti tesztadaton fogadd el az egész táblás Trim hat módosítását. A Notepad++ szövege az Apply előtt még legyen eredeti.
2. Kattints a fő eszköztár **Apply** gombjára. Mind a hat módosítás kerüljön át a szövegbe. Automatikus mentés ne történjen.
3. Kattints a Notepad++ szövegszerkesztőjébe, és nyomj **egyszer Ctrl+Z-t**. Mind a hat módosítás egyszerre tűnjön el.
4. **Egyszer Ctrl+Y**: mind a hat térjen vissza. Refresh a pluginban, mielőtt továbbtesztelsz.
5. Új példányban kezdd újra az eredeti CSV-vel. Fogadj el egy Transform-módosítást, majd közvetlenül a Notepad++ szövegében is módosíts valamit.
6. A fő Apply most blokkoljon. Ne írja felül a közvetlen szövegszerkesztést, és a függő táblamódosítás se vesszen el. Jegyezd fel a státusz tartalommentes lényegét, majd Revert All → Exit Edit → Refresh.

## T6 — Windows-1250

1. Külön, szintetikus Windows-1250 dokumentumban ismételj meg egy ékezetes kis-/nagybetűsítést és Trim-műveletet. A Preview és Accept changes után az Apply veszteségmentesen működjön.
2. Egy új Windows-1250 Edit sessionben egy tesztjelölőt cserélj emojira a Transform segítségével. A függő táblában ez megengedett, de a fő Apply-nak blokkolnia kell a Windows-1250-ben nem tárolható eredményt.
3. A blokkolás ne írjon a forrásba és ne hozzon létre szerkesztési undo-lépést. A függő módosítás maradjon javítható vagy Revert All-lal eldobható. Az elutasított karaktert nem kell az eredménybe vagy naplóba másolnod.

## Eredmény visszaküldése

Elég ezt kitölteni; nem szükséges képernyőkép vagy valódi adat:

```text
Windows:
Notepad++ verzió / x64:
DLL SHA-256 vagy igazolt csomagazonosság:
T1: PASS / FAIL / NOT RUN
T2: PASS / FAIL / NOT RUN
T3: PASS / FAIL / NOT RUN
T4: PASS / FAIL / NOT RUN
T5: PASS / FAIL / NOT RUN
T6: PASS / FAIL / NOT RUN; tényleges encoding:
Eltérés röviden, adatok nélkül:
```

Ha csak egy teszten belül néhány lépést végeztél el, írd oda a lépésszámokat. A Source F2–F4 és a pontos Windows-1250 forráspozíció külön, továbbra is nyitott teszteset; ezeket a [Source tesztleírás](host-validation-0.12-source-navigation.md) tartalmazza.
