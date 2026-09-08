# 0.12.6 visual regression correction

The owner rejected the 0.12.5 appearance. Its prior successful tests are not visual acceptance.

## Corrected behavior

- Value and header cells inherit normal UI typography, not a forced monospaced font. Vertical grid lines again separate columns throughout the body.
- Space markers are filled, 3-DIP dots with a darker orange on light cells and a light orange on dark/selected cells. DPI comes from the actual grid when painting in the host.
- A proportional-font space can be narrower than a readable marker. When marks are visible, the painter uses an en/em-width display slot where necessary rather than shrinking the marker to a speck. This is temporary layout text only: CSV values, source offsets, edit values and clipboard output are never replaced or padded. Turning marks off restores ordinary space advances. Native typing uses the normal editor control.
- Search children are inset from the field border, so the magnifier cannot erase its top edge. Query width is capped, scope/count/arrows form one group, empty queries hide the clear icon and redundant status. The whole docking form can shrink with its responsive search bar.
- Initial data focus excludes the # presentation lane. Existing complete-row gestures and editing/Apply policy remain.
- Reassigning a null search index no longer invalidates the grid on every populated cell.

## Automated verification

Existing Core and Native AOT tests remain. New assertions require visibly sized separate dots, inherited UI font, continuous search-field borders, bounded wide layouts, compact breakpoints and hidden idle affordances.

`tests/host-review/Invoke-HostReview.ps1` runs the actual published DLL in a temporary Notepad++ 8.9.8 x64 portable host. Its official archive is pinned by SHA-256. The test does not use the user's installation or files. It loads a synthetic CSV, invokes the native plugin menu, captures the full host and docking form, enters a query through the actual native text box, and checks the result counter. It also captures resized docks. Outputs retain for three days. No updater or installer is run and only the test-owned process/folder is cleaned up.

Host automation is a bounded load/render/search check, not manual acceptance or a proof of every native gesture, DPI/theme, clipboard or Apply/Undo case. Generated images must be reviewed; a successful bitmap call alone does not prove readable UI.

## Magyar ellenorzes

1. Frissitsd a DLL-t bezart Notepad++ mellett. Az About 0.12.6-alpha.<futas>.<proba> verziot mutasson.
2. A tablazat normalkarakteres legyen, fuggoleges es vizszintes cellaraccsal. Harom valodi szokoz harom jol lathato kulon pont legyen. A Show spaces csak a megjelenitest valtoztassa.
3. A keresomezo felso kerete legyen folytonos. A mezot, oszlopot, szamlalot es nyilakat ne huzzak szet nagy ures szakaszok. Szuk panelen ne legyen atfedes.
4. Kereses, torles, rendezes, Edit/Transform, masolas es egy Apply/Undo kor maradjon helyes. A jelek nem kerulhetnek az adatba. Vilagos/sotet modot es a hasznalt DPI-t kulon kell ellenorizni.

Record exact package and Windows/Notepad++ versions with each PASS/FAIL/NOT RUN result. Source F2-F4/G and prior unreported host cases remain pending. Both PR #12 remain draft/unmerged.
