# Tekst na ekranu koji nije govorio istinu ili nije bio srpski

**Grana:** `fix/ui-copy`

Dva nalaza sa E2E pregleda granice serija po treningu (#87). Nijedan nije vezan za pravila
treninga, pa su izdvojeni u svoju granu.

## Upozorenje na 0 kg je pominjalo šipku uz svaku spravu

Uz svaku vežbu koja se opterećuje spolja, a stoji na 0 kg, ekran je pisao „prazna šipka je već
oko 20 kg" — i uz Cable Fly, Triceps Pushdown i bučice, koje šipku nemaju. Beleška runde 13 u
`CLAUDE.md` tvrdi da je ta rečenica bila greška prve verzije i da je ispravljena, ali ispravka
nije stigla u kod: `git log -S "prazna šipka"` vraća jedan jedini commit, onaj koji ju je uveo.

- `loadInputNote` sada vraća `zero-on-barbell` za šipku i `zero-on-loaded` za ostalo.
- Ni kod šipke se 20 kg više ne tvrdi kao činjenica o vežbi. Skull Crusher je u katalogu
  `Barbell`, a radi se najčešće sa EZ šipkom od oko pola toga, pa tekst kaže „i prazna šipka ima
  težinu (olimpijska oko 20 kg)".
- Test traži da rečenica o šipci stoji samo uz šipku.

## Šest tekstova u čarobnjaku plana bez dijakritika

Opisi rasporeda nedelja i načina brojanja serija u `plan-home.ts` bili su otkucani bez
dijakritika: „Cetvrta je deload", „Krece sa vise ponavljanja nego sto si uneo", „Prati moj
sablon", „podesava… po misicu", „tacno… moze… nece". Ostatak aplikacije ih ima.

Pretraga je bila šira od ta tri ekrana. Skripta je prošla sve string literale u klijentu (TS) i
sve tekstualne čvorove u šablonima (HTML), kao i sve poruke servera (C#), i tražila tekstove
bez ijednog dijakritika sa rečima koje ga traže. Rezultat: šest pogodaka, svi u `plan-home.ts`.
Ostali pogoci su bili ispravan srpski kome dijakritik ne treba („Koji je tvoj pol?", „Korisnik ne
postoji.").

## Provera

- `dotnet test` 715, `npm test` **182** (bilo 181), `npm run build` prolaze.
- Uživo, 375 px: Incline Bench Press na 0 kg piše tekst sa šipkom, a Dumbbell Shoulder Press
  opšti. U čarobnjaku plana nema nijednog od starih tekstova, a novi se pojavljuju.
