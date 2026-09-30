# Tekst na ekranu koji nije govorio istinu ili nije bio srpski

**Grana:** `fix/ui-copy`

Dva nalaza sa E2E pregleda granice serija po treningu (#87). Nijedan nije vezan za pravila
treninga, pa su izdvojeni u svoju granu.

## Upozorenje na 0 kg je pominjalo šipku uz svaku spravu

Uz svaku vežbu koja se opterećuje spolja, a stoji na 0 kg, ekran je pisao „prazna šipka je već
oko 20 kg" — i uz Cable Fly, Triceps Pushdown i bučice, koje šipku nemaju. Beleška runde 13 u
`CLAUDE.md` tvrdi da je ta rečenica bila greška prve verzije i da je ispravljena. U kodu nije:
na `main`-u pre ove grane `git log -S "prazna šipka"` vraća samo #83, dakle rečenica se od
tada nije menjala. (Da li je nacrt pre commit-a bio drugačiji, istorija ne može da kaže —
beleška navodi verziju bez „već", a ušla je verzija sa njim.)

- `loadInputNote` sada vraća `zero-on-barbell` za šipku i `zero-on-loaded` za ostalo.
- Ni kod šipke se 20 kg više ne tvrdi kao činjenica o vežbi. Skull Crusher je u katalogu
  `Barbell`, a radi se najčešće sa EZ šipkom od oko pola toga, pa tekst kaže „i prazna šipka ima
  težinu (olimpijska oko 20 kg)".
- Tekst upozorenja je izašao iz šablona ekrana u `zeroLoadWarning`, pored funkcije koja bira
  napomenu, i test proverava **sam tekst**: rečenica o šipci stoji samo uz šipku, i nigde ne
  tvrdi da je šipka „oko 20 kg". Prva verzija ove grane je testirala samo izbor napomene — a
  greška iz runde 13 je bila u rečenici, ne u izboru, pa je takav test ne bi uhvatio (nalaz
  revizije).
- Sprava se čita bez obzira na velika i mala slova, kao na serveru: vežba koju korisnik napravi
  preko API-ja čuva se onako kako je otkucana („barbell").

## Sedam tekstova bez dijakritika

Opisi rasporeda nedelja i načina brojanja serija u `plan-home.ts` bili su otkucani bez
dijakritika: „Cetvrta je deload", „Krece sa vise ponavljanja nego sto si uneo", „Prati moj
sablon", „podesava… po misicu", „tacno… moze… nece". Sedmi je podnaslov prvog koraka
registracije: „Korisnicko ime koje stoji na tvom profilu."

Sedmi je našla revizija, ne moja pretraga, i to je vredno zapisati: pretraga je bila heuristika
sa spiskom reči, a „korisnik" je na spisku bio samo za poruke servera. Posle toga su prošle još
dve: jedna gradi rečnik od svih reči koje projekat negde piše sa dijakritikom i traži iste reči
bez njega, druga sa širim spiskom korena. Obe prolaze kroz string literale klijenta (TS),
tekst šablona (HTML) i poruke servera (C#). Nijedna nije našla još jedan pravi pogodak — svaki
preostali je ispravan srpski („odgovara pretrazi", „Lozinka", „stoji"). To je i dalje heuristika,
a ne dokaz da ih nema.

## Provera

- `dotnet test` 715, `npm test` **184** (bilo 181), `npm run build` prolaze.
- Uživo, 375 px: Incline Bench Press na 0 kg piše tekst sa šipkom, a Dumbbell Shoulder Press
  opšti. U čarobnjaku plana nema nijednog od starih tekstova, a novi se pojavljuju.
