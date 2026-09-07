# 0.12.4 — keresési találatok és egységes szóközkarikák

2026-09-07: a felhasználó a 0.12.3 GUI-nál két hibát jelentett: nem látszik, melyik cellában van keresési találat, és a szóközkarikák mérete eltérőnek látszik. Minden mást jónak jelentett. Ezt összesített felhasználói visszajelzésként rögzítjük, pontos hostverzió/DLL-hash és külön DPI-esetek nélkül. A javított 0.12.4 hosttesztje **NOT RUN**.

## Rövid újrateszt

Telepítsd a PR #12 teljesen zöld `CsvVisualEditor-0.12.4-alpha.<futás>.<próba>-win-x64.zip` csomagját, bezárt Notepad++ mellett DLL-cserével. Az alábbi korábbi szintetikus CSV használható.

1. **Keresés:** `test` keresésnél a találati cella kapjon aranyszínű belső keretet; az ugyanabban a sorban található másik cella ne. Nagybetűs keresés ugyanazt találja. Kijelölt cellán is látszódjon a keret.
2. **Oszlop és törlés:** a keresést korlátozd egy oszlopra. Csak annak találatai kapjanak keretet. Clear után minden találati keret tűnjön el. Rendezés után a keret a helyes értéknél maradjon. Edit módba lépve a keresésjelölés megszűnik a meglévő szűrés-visszaállítás részeként.
3. **Karikák:** vezető, belső, záró és csak szóközös cellákban azonos méretű jelek legyenek, a Transformban is. Világos/sötét módban, és ha elérhető, 100/150/200% méretezésnél ellenőrizd. A méret DPI-nként változhat, egy adott DPI-n belül egyezzen.
4. **Elrendezés:** a kereső külön sorban jelenjen meg. A képeken látott panelszélességnél a mező már legyen látható és használható. Nagyon keskeny panelnél a Search menü továbbra is teljes hozzáférést adjon. Keresés, görgetés és menünyitás maradjon folyamatos.

A keret az egész találati cellát jelöli, nem az egyes szövegrészleteket. A teljes értéket vizsgálja akkor is, ha hosszú cellánál a kirajzolt szöveg rövidített. Jelölések nem kerülhetnek Copy/Apply útján az adatba.

Küldd: csomagnév, Windows/Notepad++ x64 verzió, 1–4 PASS/FAIL/NOT RUN. Új Source F2–F4/G PASS ebből nem következik.

---

## Korábbi 0.12.3 protokoll és történeti állapot

# 0.12.3 — ikonos GUI hostteszt

Állapot: **NOT RUN** az új GUI-ra. A 2026-09-07-i felhasználói „minden teszt jó” jelentés az előző 0.12.2 szóközjelölési körre vonatkozik. Pontos Windows/Notepad++ verzió és DLL-hash nem érkezett. A screenshotokat és valódi adatokat nem rögzítjük. A 0.12 milestone nyitott, draft, unmerged marad.

## Telepítés és tesztadat

1. A PR #12 aktuális, teljesen zöld 0.12.3 csomagját töltsd le. Ne a régi 0.12.2-t használd. Név: `CsvVisualEditor-0.12.3-alpha.<futás>.<próba>-win-x64.zip`.
2. Zárd be a Notepad++-t, bontsd ki a letöltést (ha belső ZIP van, azt is), cseréld a `plugins/CsvVisualEditor/CsvVisualEditor.dll` fájlt, majd indítsd újra a Notepad++ x64-et.
3. Írd fel a Windows és Notepad++ verziót, valamint a csomag teljes nevét. Ha lehet, a telepített DLL SHA-256 hashét is küldd el; privát elérési út nem kell.
4. Új, nem mentett UTF-8 dokumentumba másold ezt a kitalált tesztet. Az idézőjelek között az első mezőben 2 vezető és 2 záró szóköz van; a második sor első mezője 3 szóközből áll.

```csv
Value,Note
"  test  ","a b"
"   ","x  y"
"","end"
```

5. Nyisd meg a plugint. CSV > Delimiter > Comma, CSV > Header > First row is header; Table > Refresh. Három adatsor legyen.

## U1 — szóközök

1. A `test` előtt és után két-két apró, üres narancsszínű kör látszódjon. A három szóközös cellában három jel legyen, az üres cellában egy sem. A jelek ne fedjék a betűket.
2. Egeret a `test` cella fölé: a súgó 4 szóközt, 2 vezetőt, 2 zárót és 8 UTF-16 egységnyi hosszt jelezzen. Csak számokat és magyarázatot mutat, a cellaadatot nem ismétli.
3. View > Show spaces kikapcsolva minden kör eltűnik; visszakapcsolva visszatér. A Notepad++ szövege és a függő változások száma közben ne módosuljon.
4. Edit > Edit; válaszd a `test` cellát; Edit > Transform > Trim outer whitespace > Selected cells / rows > Preview. Before: 4 kör és 8-as hossz; After: kör nélkül, 4-es hossz. Cancel ne változtasson adatot.
5. Rejtett szóközökkel újra nyitott Transformban se legyenek körök, de az értékhatárok és a 8/4 hosszak maradjanak. Kapcsold végül vissza a Show spaces nézetet.

## U2 — ikonok és szöveges menü

1. Felül mindig legyen Table / Edit / View / CSV / Search szöveges menü, alatta kompakt ikonok. Vidd az egeret sorban az ikonokra: mindegyiknél a parancs neve és súgó jelenjen meg.
2. Edit be- és kikapcsolásakor az ikon és az Edit menü állapota egyezzen. Read-only módban a Cut/Paste/Transform/Apply megfelelően legyen tiltott mindkét helyen.
3. Edit módban módosíts egy cellát. Egyszer az ikonról, másodszor új módosítás után a menüből használd a Revert All parancsot: az eredeti érték térjen vissza.
4. Válassz egy cellát; a Copy ikonról, majd a menüből másolt értéket egy külön új tesztdokumentumba illeszd. Valódi szóközök legyenek, ne körkarakterek.
5. Kattints Source ikonra, majd ugyanarra a cellára visszatérve Table > Source: ugyanazt az eredeti forrásmezőt jelöljék ki.
6. Új, szándékos függő módosítás után Edit > Apply egyszer írjon a Notepad++-ba. Ott egy Ctrl+Z állítsa vissza az előző szöveget; Ctrl+Y ismételje meg. Ezután a pluginban Refresh.

## U3 — minden nézetfunkció a menüből

1. Szűkítsd össze a dokkolt panelt. Az esetleg túlcsorduló ikonok parancsai a menüből továbbra is elérhetők legyenek.
2. Search > Find in table alatt írj `test` keresést: egy találati sor. Search > Clear visszaadja a teljes táblát. A Search column menüből változtatható legyen a céloszlop.
3. View > Sort by alatt rendezz egy oszlopot növekvően, csökkenően, majd Original order. Ez csak a nézetet rendezze.
4. View > Diagnostics és Table > Show table váltsa a füleket. A CSV > Delimiter/Header a meglévő választókkal egyezően működjön; Edit közben ugyanúgy legyen tiltott, mint azok.

## U4 — világos/sötét, méret és stabilitás

1. Világos és sötét Notepad++ témában is ellenőrizd az ikonokat, normál/letiltott/fölé vitt állapotot, lenyíló menüket és Transformot. Szöveg és háttér ne olvadjon össze.
2. Ha van rá mód, 100% és 150–200% Windows méretezésnél vagy eltérő DPI-jű monitorok között mozgatva ellenőrizd: olvasható menü, arányos ikon, nem levágott kattintási terület.
3. Nyisd/zárd újra a pluginpanelt néhányszor; ne duplázódjon a menü vagy egy kattintás művelete. Nagy szintetikus CSV-n görgetés és szóközkapcsolás ne akadjon tartósan.
4. Az aktív cella gépelés közben szabványos szerkesztő marad, a körök a cellaszerkesztés befejezése után térnek vissza. Ez szándékos.

Eredmény: csomagnév + Windows/Notepad++ x64 verzió + U1/U2/U3/U4 PASS/FAIL/NOT RUN, hibánál a lépés száma és rövid, szanitizált leírás. A nem próbált DPI/monitor részt jelöld NOT RUN-nak. A GUI-kör nem helyettesíti a még külön igazolandó Source F2–F4 és Windows-1250 G eseteket.
