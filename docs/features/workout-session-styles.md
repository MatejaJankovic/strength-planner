# Stilovi ekrana treninga

**Grana:** `chore/workout-session-styles`

Peti korak plana posle revizije (6. 11. 2026), stavka R7.

## Problem

`workout-session.scss` je bio na **7.93 kB** kompajliranog CSS-a, a tvrda granica
`anyComponentStyle` je 8 kB. Runda 12 je to najavila, a u rundi 13 je sledeća izmena tog ekrana
oborila build (8.05 kB). Svaka sledeća napomena na kartici vežbe bi ga ponovo oborila.

## Šta je urađeno

Spojeni su blokovi koji su ponavljali ista svojstva, bez izmene izgleda:

- poruka o grešci i poruka o završenom treningu (isti okvir, druga boja);
- „Pokušaj ponovo" i glavna akcija (puna dugmad);
- dugme nazad i dugmad sa ikonom (centriranje);
- dugme za dodavanje serije i odustajanje;
- dva skupa čipova (identična);
- beleška u rezimeu i beleška ispod unosa;
- tri liste bez oznaka (rezime, odrađene serije, pomereni predlozi);
- font naslova kartice i rezimea.

## Provera

- Kompajlirano: **7.93 → 7.48 kB**, ispod 7.5 kB koliko je plan tražio.
- Obe verzije SCSS-a su kompajlirane i, selektor po selektor, upoređen je konačan skup svojstava.
  Razlika je pet stavki:
  - četiri su ista vrednost zapisana drugačije (`margin: 0` uz `margin-top: 0.55rem` umesto
    `margin: 0.55rem 0 0`, isto za `padding`);
  - jedina stvarna promena je beleška ispod unosa, sa 0.8 na 0.82 rem, kao susedna beleška u
    rezimeu.
- `npm test` 204 i `npm run build` prolaze; `dotnet test` 882, bez izmena.
- Uživo, na 375 px: ekran treninga izgleda isto, bez prelivanja; izračunati stilovi glavne akcije,
  dugmeta nazad, naslova kartice i čipova su isti kao pre.
