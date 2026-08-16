# Red sa serijama i ponavljanjima u uređivaču šablona

**Grana:** `fix/template-editor-layout`

## Problem: jedna oznaka se prelama, ceo red ispada kriv

Uređivač ličnog šablona je za svaku vežbu imao **tri ravnopravna polja** u jednom redu:

```
grid-template-columns: repeat(3, minmax(0, 1fr));
```

sa oznakama `Serija`, `Ponavljanja od` i `do`. Na širini telefona kolona nosi oko 90px, pa
se srednja oznaka - jedina od tri koja ima dve reči - prelama u dva reda. Polja su
poravnata po vrhu svoje ćelije, pa unos ispod prelomljene oznake kreće **niže** od svojih
suseda.

Rezultat je red u kome tri broja stoje na dve različite visine, a oznake se čitaju kao
`Serija | Ponavljanja do | od` umesto kao jedna vrednost i jedan opseg.

## Rešenje: dve grupe umesto tri polja

Opseg ponavljanja **jeste jedna vrednost**, pa se sada tako i prikazuje:

```
SERIJE        PONAVLJANJA
[  3  ]       [  8  ] - [ 12 ]
```

- `.exrow__numbers` ima dve kolone (`1fr` za serije, `2fr` za opseg) umesto tri.
- Iznad oba polja opsega stoji **jedna** oznaka `Ponavljanja`. Pošto oznaka ima jednu reč,
  nema šta da se prelomi, pa se ni polja ne mogu razići po visini.
- Između polja stoji crtica, ista kao u prikazu na kartici šablona (`3×8-12`). Nosi
  `aria-hidden`, jer je ukras a ne sadržaj.
- `align-items: start` drži obe grupe poravnate po vrhu i kada bi se nešto ipak prelomilo.

## Pristupačnost

Vidljiva oznaka je sada zajednička za dva polja, pa svako polje dobija svoju **skrivenu**
oznaku (`Ponavljanja od`, `Ponavljanja do`) preko `.sr-only`. Čitač ekrana i dalje čita
tačno koje polje je u fokusu; izgubila bi se informacija da je ostala samo zajednička
oznaka.

`id` atributi polja (`sets-`, `rmin-`, `rmax-`) su namerno nepromenjeni - po njima se vezuju
oznake, a i testovi komponente ih koriste.

## Provera

- `npm run build` i `npm test` (22 testa) prolaze; testovi uređivača rade nad istim
  poljima jer su im `id`-jevi ostali isti.
- Prolaz kroz aplikaciju na širini telefona - rezultat se upisuje ovde posle provere u
  pregledaču.
