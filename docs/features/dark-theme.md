# Tamna tema

Runda 19, grana 5 od 5. Telefon se koristi u teretani, često u slabo osvetljenom prostoru, a aplikacija je imala samo
svetlu paletu. Tema je namerno bila poslednja u rundi: to je odluka o svakom ekranu, pa je morala da sačeka da ekrani
prestanu da se menjaju.

## Šta je urađeno

- **Sve boje iz tokena.** Nijedan stil komponente više ne piše boju kao vrednost (heksadecimalnu, `white`, `rgb(...)`):
  čita token. U `styles.scss` stoje svetle vrednosti (`:root`) i tamne (`@mixin dark-tokens`). 142 izmene u stilovima
  komponenata (139 skriptom, tri ručno).
- **Tema se bira atributom `data-theme`** na `<html>`; bez njega prati telefon (`prefers-color-scheme`). Tri izbora:
  „Kao sistem" (podrazumevano), „Svetla", „Tamna".
- **`ThemeService`** čita i čuva izbor (u pregledaču, po telefonu), drži atribut i boju trake pregledača
  (`<meta name="theme-color">`) usklađene, i izlaže temu koja se stvarno vidi.
- **Izbor na ekranu Podešavanja** (`app-theme-picker`), kartica „Izgled", prva na ekranu. Menja se odmah.
- **`public/theme-init.js`** postavlja izbor pre prvog iscrtavanja, da se na tamnom telefonu ne vidi bljesak svetle teme.
- **Grafikon** (Chart.js crta po platnu i ne zna za CSS) čita boje iz tokena pri crtanju i crta se ponovo kad se tema
  promeni; boja teksta u tooltip-u prati njegovu pozadinu.
- **`scripts/check-theme-contrast.py`** meri odnose kontrasta svih parova tekst/pozadina u obe teme (tintovi se mešaju
  kao u pregledaču), proverava da se `theme-init.js` i servis ne razilaze, i pada ako se u stilu komponente pojavi
  napisana boja.

## Odluke

- **Ime tokena kaže ulogu, ne boju.** Beli tekst je do sada bio `#fff` svuda, a u tamnoj temi je „ink" (boja teksta)
  svetao, pa tekst na njemu mora da bude taman. Zato su `--color-on-primary`, `--color-on-ink`, `--color-on-danger`,
  `--color-on-above` i `--color-on-accent`: svaki uz pozadinu na kojoj stoji. Beli tekst je mapiran po pozadini koju
  isto pravilo već ima, a ne po slovu: skripta za zamenu prekida ako beli tekst nema pozadinu u istom pravilu.
- **Svetla tema se ne menja.** Svaka zamena preslikava literal u token čija je svetla vrednost taj literal, a to je i
  izmereno (vidi niže: 0 razlika u 3356 elemenata).
- **Tintovi se mešaju sa karticom, ne sa belim.** `color-mix(boja 16%, white)` je postao `color-mix(boja 16%,
  var(--color-elevated))`: svetlo je isto, a u tamnoj temi tint ostaje taman, sa svetlom varijantom iste boje za tekst.
- **Prsten fokusa ostaje mešan sa belim**, i senke / zatamnjenje ostaju kao što su (tamno na svetlom i na tamnom): to
  su jedine napisane boje, i skripta ih izričito dozvoljava.
- **Bez čisto crne.** Podloga je `#0f1215`, kartica `#171b1f`: čisto crna na OLED-u pravi oštar prelaz sa karticama i
  zamara oči pri skrolovanju.
- **Dugmad u „ink" boji postaju svetla** u tamnoj temi (−/+ uz težinu, dugmad pored polja): to je uloga najjačeg
  naglaska, koja u svetloj temi znači crno. Jak je utisak; ako smeta, menja se jednom vrednošću (`--color-ink` ili
  zaseban token za te dugmiće).
- **Izbor je po telefonu, ne po nalogu.** Isti razlog kao cilj pauze: tema zavisi od uređaja i okruženja, a ne od osobe.
- **Skripta mora da bude spoljašnja, i da ne bude keširana godinu dana.** CSP dozvoljava samo `script-src 'self'`
  (umetnuta skripta bi bila blokirana), a nginx kešira svaki `.js` kao nepromenljiv. Zato `theme-init.js` ima tačno
  podudaranje sa `no-cache`, isto kao `index.html`.

## Merenja

- Testovi klijenta: 391 → 423: servis teme 23 (8 od toga za `parseThemePreference`), birač 4, grafikon 4, pokretanje
  aplikacije 1. Server nije menjan (1413).
- **Kontrast po tokenima:** tamna tema nema nijedan par ispod praga (4,5 za tekst, 3,0 za onemogućeno dugme i prsten
  fokusa); najmanji je onemogućeno dugme sa 3,28. Svetla tema ima **šest para ispod praga koji su postojali i pre**: prigušen
  tekst na čipu (4,24), beli tekst na `#e4572e` (3,68), isti crveni kao tekst na kartici (3,68) i na stranici (3,38),
  „optimalno" zelena kao tekst (3,05), onemogućeno dugme (2,64). Nisu menjani: promena svetle teme nije bila zadatak.
- **Kontrast u stranici, uživo** (svaki tekst prema stvarnoj pozadini, kroz roditelje, na 375 px): u tamnoj temi
  **0 neuspelih elemenata** na svih dvanaest ekrana i stanja: Trening (71 element), Plan (23), Profil (33), Podešavanja
  (32), Istorija (49), Analitika (388), Vežbe (140), Šabloni (15), Unos maksimuma (447), planiran trening (123), završen
  (111), trening u toku sa trakom pauze u stanju „istekla" (224). U svetloj temi, za poređenje, na istim ekranima ima
  ranijih nedostataka: Unos maksimuma 156, Analitika 40, završen trening 15, planiran 12, u toku 9, Podešavanja 2.
- **Vraćanje pravila na committed stablu:** servis teme, grafikon i birač 12 izmena, svaka ruši bar jedan test
  (nevažeća sačuvana vrednost 7; „kao sistem" ne skida atribut 8; traka pregledača 4; slušalac sistemske teme 2; skidanje
  slušaoca 1; čuvanje izbora 3; sistemska tema ignorisana 3; `App` ne pokreće servis 1; grafikon se ne crta ponovo 1;
  boja teksta tooltip-a 1; redosled izbora 3; birač ne primenjuje izbor 2). Skripta: pad kontrasta, napisana boja u
  komponenti, razilaženje boje trake i razilaženje ključa, sva četiri daju izlaz 1.
- **Svetla tema nepromenjena, izmereno:** isti ekrani uzeti dvaput, jednom sa stilovima grane, jednom sa `main`-ovim
  (TypeScript isti, stilovi privremeno vraćeni), svaki element sa 10 izračunatih svojstava boje (tekst, pozadina,
  četiri ivice, obris, senka, stil ivice, popuna), vrednosti normalizovane (ista boja se zapisuje kao `rgba(...)` i kao
  `color(srgb ...)`). Jedanaest ekrana i stanja, **3356 elemenata, 0 razlika**. Prvi pokušaj sa digestom bez
  normalizacije je dao razliku na svakom ekranu: bila je samo u zapisu jedne boje (traka na dnu, `rgba(255,255,255,0.92)`
  prema `color(srgb 1 1 1 / 0.92)`), što je pokazao pregled jednog ekrana element po element. Ekran Podešavanja je
  izostavljen jer na grani ima karticu „Izgled".
- **Uživo:** izbor „Tamna" kroz ekran postavlja atribut, čuva se, pozadina i `color-scheme` postaju tamni, a traka
  pregledača `#0f1215`; `theme-init.js` stoji ispred svih stilova i nije `defer` ni `async`; u praznoj stranici postavlja
  atribut i boju trake za `dark` i `light`, a za nepoznatu vrednost ili bez izbora ostavlja sistem. Tri režima u CSS-u:
  bez atributa na tamnom sistemu je tamno, „Svetla" je svetla i na tamnom sistemu, „Tamna" je tamna. Grafikon u tamnoj
  temi: 3818 piksela linije u svetlom tealu tamne teme i nijedan u svetlom; isto za oznake i mrežu.

## Šta je merenje našlo

- **Značka „Deload" i dva dugmeta su imali 1,49:1 u tamnoj temi.** Token `--color-accent-ink-strong` sam prvo
  pogrešno odredio kao tekst na providnom tintu akcenta, a sva tri mesta (dugme „Nastavi", značka „Deload", dugme
  „Završi trening") stoje na punoj akcent boji. Tabela kontrasta je merila pogrešan par i bila zelena; našlo ga je tek
  merenje stvarno iscrtanog ekrana. Preimenovan u `--color-on-accent` (tamno smeđe u obe teme), tabela sada meri puni
  akcent.
- **`--color-above` i `--color-optimal` nikad nisu postojali.** Komponente su pisale `var(--color-above, #e4572e)`, pa
  se uvek koristila rezervna vrednost: u tamnoj temi bi ostala jarka. Sada su definisani kao `--volume-*`.
- **Tooltip grafikona je imao beo tekst na „ink" pozadini**, a „ink" je u tamnoj temi svetao: svetlo na svetlom. Sada
  uzima `--color-on-ink`.
- **Prvo merenje je bilo pogrešno postavljeno** i ispravljeno pre nego što je išta zaključeno: serija za grafikon koja
  je „menjala temu" emulacijom sistema nije dobila `change` događaj ni na mom sopstvenom slušaocu (to je ograničenje
  emulacije u skrivenom panelu, ne servisa), pa je reakcija na događaj ostala za jedinični test, a boje grafikona su
  proverene drugim putem (izbor kroz ekran, pa prelaz u analitiku i uzorkovanje piksela).

## Šta nije proveravano

- **Pravi telefon.** Nije viđeno kako izgleda na stvarnom ekranu (OLED), ni na iPhone-u/Safari-ju. Bljesak pri otvaranju
  proveren je redosledom i ponašanjem skripte, ne gledanjem pri učitavanju.
- **Pravilo za nginx** (`location = /theme-init.js`) nije pokretano: Docker nije dostupan. Postoji, i čita se kao
  pravilo za `index.html`.
- **Izgled očima:** pogledan je jedan snimak ekrana treninga u tamnoj temi; ostalo je izmereno, ne gledano. Svetla
  tema je upoređena po izračunatim bojama, ne po slici (razmak, veličine i slično nisu u poređenju, ali ih ova grana ne
  dira).
- **Boje koje nisu tokeni:** ikone (`mat-icon`) nasleđuju boju teksta. Boja trake instalirane aplikacije
  (`theme_color` u `manifest.webmanifest`) ostaje svetla vrednost: manifest je jedan i ne zavisi od izabrane teme.
- **Veličina stilova.** Imena tokena su duža od heksadecimalnih vrednosti, pa su tri stila koji su već bili iznad
  upozorenja od 6 kB porasla: `workout-session.scss` 6,19 → 6,48 kB, `plan-home.scss` 6,29 → 6,39, `one-rep-max-setup.scss`
  6,01 → 6,26. Tvrda granica je 8 kB.
