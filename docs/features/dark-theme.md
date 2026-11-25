# Tamna tema

Runda 19, grana 5 od 5. Telefon se koristi u teretani, često u slabo osvetljenom prostoru, a aplikacija je imala samo
svetlu paletu. Tema je namerno bila poslednja u rundi: to je odluka o svakom ekranu, pa je morala da sačeka da ekrani
prestanu da se menjaju.

## Šta je urađeno

- **Sve boje iz tokena.** Nijedan stil komponente više ne piše boju kao vrednost (heksadecimalnu, `white`, `rgb(...)`):
  čita token. U `styles.scss` stoje svetle vrednosti (`:root`) i tamne (`@mixin dark-tokens`). 142 zamene u stilovima
  komponenata (139 skriptom, tri ručno; to je 138 izmenjenih redova, jer neki red nosi dve zamene).
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
  podudaranje sa `no-cache`, isto kao `index.html`. Svaki hladan početak plaća jedan uslovni zahtev (304) pre prvog
  iscrtavanja; skripta se gotovo nikad ne menja, pa bi i kratak `max-age` radio. Ostavljeno na `no-cache`: sigurnije
  je od zastarele skripte, a cena je jedan mali zahtev.
- **Glavni CSS u produkciji ne sme da bude asinhron.** Angular po podrazumevanoj optimizaciji umeće „kritični" CSS
  i ostatak učitava kroz `<link media="print" onload="...">`, a CSP isporučenog nginx-a taj umetnuti `onload` blokira
  (vidi „Šta je pregled našao"). Zato je u produkcionoj konfiguraciji `optimization.styles.inlineCritical: false`.

## Merenja

- Testovi klijenta: 391 → 425: servis teme 24 (8 od toga za `parseThemePreference`; jedan je za `matchMedia` bez
  `addEventListener`), birač 4, grafikon 5 (jedan za boje tooltip-a), pokretanje aplikacije 1. Server nije menjan
  (1413).
- **Kontrast po tokenima:** tamna tema nema nijedan par ispod praga (4,5 za tekst, 3,0 za onemogućeno dugme i prsten
  fokusa); najmanji je onemogućeno dugme sa 3,28. Svetla tema ima **osam para ispod praga koji su postojali i pre** (prvih šest, plus dve
  oznake granica na traci volumena koje je tek pregled uveo u merenje: MAV 1,69 i MEV 2,87 prema traci): prigušen
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

## Šta je pregled našao (ispravljeno)

Pregled je radio agent koji samo čita; nalaz 1 sam ponovio u pregledaču pre ispravke.

- **U produkcionom buildu iza CSP-a tamna tema ne radi** (greška, blokirajuća; najvažniji nalaz grane). Angular umeće
  kao „kritični CSS" samo `:root` (svetle tokene) i osnovna pravila; pravila `@media (prefers-color-scheme: dark)` i
  `:root[data-theme=dark]` stoje samo u glavnom CSS-u, koji se učitava kao `<link media="print"
  onload="this.media='all'">`. CSP u `security-headers.conf` ima `script-src 'self'` bez `'unsafe-inline'`, pa
  pregledač odbija umetnuti `onload`, i glavni CSS ostaje na `media="print"` do kraja sesije. Posledice: telefon u
  tamnom režimu dobija svetlu temu, prekidač „Tamna" ne radi, a `theme-init.js` ne može da spreči bljesak jer tamnih
  tokena nema u prvom iscrtavanju. **Isto važi za svaki globalni stil na `main`-u**; u razvojnom serveru nema
  optimizacije, a Docker slika nikad nije pokretana, pa se nije videlo. Ponovljeno u pregledaču sa istim CSP
  zaglavljem (Python server umesto nginx-a): sistem taman, `body` svetao, `color-scheme: light`, glavni CSS na
  `media="print"`, greška CSP-a u konzoli. Ispravljeno sa `optimization.styles.inlineCritical: false`; posle toga
  `<link rel="stylesheet">` je običan blokirajući, nema umetnutih rukovalaca ni skripti, i sve tri kombinacije
  (sačuvano „svetla" na tamnom sistemu → svetlo; „tamna" na svetlom → tamno; bez izbora na tamnom → tamno) rade iza
  CSP-a. Tvrdnje „nema bljeska" su sada istinite za produkcioni build, a pre ovoga nisu bile.
- **Provera da se to ne vrati:** `scripts/check-production-index.py` pada ako izgrađen `index.html` ima rukovaoca
  događaja, umetnutu skriptu, stil na `media="print"` ili `theme-init.js` ne stoji pre prvog stila (četiri podmetnute
  greške, svaka daje izlaz 1; prvi pokušaj je prolazio dva od četiri jer je HTML komentar pominjao „theme-init.js"). CI
  je pokreće posle produkcionog builda, uz skriptu za kontrast.
- **Oznaka MEV na traci volumena nestajala je u tamnoj temi (1,13:1).** Bila je tamna providna crta
  (`rgb(20 21 26 / 45%)`) koju je izuzetak skripte za senke propustio. Sada je `--color-ink` na 45% (u svetloj isto što
  i pre), a sve tri oznake dobijaju oreol u boji kartice, samo u tamnoj temi (u svetloj je providan). Izuzetak skripte je
  sužen na redove sa `box-shadow`, a merenje je dobilo parove oznaka prema oreolu (ili traci). Uživo (36 oznaka): u
  tamnoj temi MEV je svetao na 45% sa oreolom `rgb(23,27,31)`, u svetloj `rgb(20,21,26)` na 45% i providan oreol.
- **Skripta za kontrast je pucala pri preusmerenom izlazu na Windows-u** (`cp1252`): sada prebacuje izlaz na UTF-8.
  Poboljšana je i drugo: komentari (i oni koji sadrže `--x: y;`) ne zbunjuju čitanje tokena, prepoznaje više oblika
  boje (`hsl`, `oklch`, imenovane), ne greši na `//` u adresi, i proverava da je `--color-primary` šest cifara
  (grafikon na njega dodaje providnost). Svaka od četiri podmetnute greške daje izlaz 1.
- **Boja teksta tooltip-a grafikona nije bila pokrivena testom** (zamena sa belom je ostavljala svih 36 specifikacija
  zelenim). Izdvojena je funkcija sa testom; vraćanje na fiksnu belu ruši 1.
- **`matchMedia` bez `addEventListener` (Safari do 14)** bi bacio izuzetak u konstruktoru servisa; `?.` je na metodama,
  uz test; vraćanje ruši 1.
- **Manji nalazi:** `background_color` u `manifest.webmanifest` je svetao, pa instalirana aplikacija na tamnom telefonu
  ima svetao uvodni ekran pre nego što se učita (manifest je jedan i ne prati temu; kao `theme_color`, ostaje);
  `${primary}1f` u grafikonu pretpostavlja šest cifara (sada proverava skripta); jedan predugačak red komentara u
  `settings-page.ts`.
- **Nije prihvaćeno:** da skripta za kontrast čita i HTML i TS. U njima su pri pregledu (`grep`) ostale samo namerne
  boje: rezervne svetle vrednosti grafikona (`chartColors`) i boje trake pregledača (`THEME_COLORS`,
  `theme-init.js`, koje skripta već poredi). Provera nad kodom bi morala da razlikuje naziv boje od običnog teksta
  (`red`, `teal` u porukama), pa ostaje za stilove.

## Šta nije proveravano

- **Pravi telefon.** Nije viđeno kako izgleda na stvarnom ekranu (OLED), ni na iPhone-u/Safari-ju. Bljesak pri otvaranju
  proveren je redosledom i ponašanjem skripte, ne gledanjem pri učitavanju.
- **nginx sam.** Pravilo `location = /theme-init.js` nije pokretano: Docker nije dostupan. Produkcioni build je
  proveren u pregledaču iza zaglavlja CSP koje je poslao Python server sa tačnim tekstom iz `security-headers.conf`,
  što dokazuje ponašanje pregledača, ne nginx konfiguracije.
- **Izgled očima:** pogledan je jedan snimak ekrana treninga u tamnoj temi; ostalo je izmereno, ne gledano. Svetla
  tema je upoređena po izračunatim bojama, ne po slici (razmak, veličine i slično nisu u poređenju, ali ih ova grana ne
  dira).
- **Boje koje nisu tokeni:** ikone (`mat-icon`) nasleđuju boju teksta. Boja trake instalirane aplikacije
  (`theme_color`) i njen uvodni ekran (`background_color`) u `manifest.webmanifest` ostaju svetle vrednosti: manifest
  je jedan i ne zavisi od izabrane teme.
- **Veličina stilova.** Imena tokena su duža od heksadecimalnih vrednosti, pa su tri stila koji su već bili iznad
  upozorenja od 6 kB porasla: `workout-session.scss` 6,19 → 6,48 kB, `plan-home.scss` 6,29 → 6,39, `one-rep-max-setup.scss`
  6,01 → 6,26. Tvrda granica je 8 kB.
