# Pregled bloka na ekranu „Plan"

**Grana:** `feature/macrocycle-block-preview`

## Problem: plan je pokazivao samo naslove

Vremenska linija blokova je za svaki blok pisala cilj, naziv šablona, model i trajanje - i
tu se završavalo. Šta blok zapravo **sadrži** nije se moglo videti nigde:

- za blok koji je u toku, vežbe se vide tek na ekranu „Trening", i to samo za njega;
- za blok koji **čeka red** nije postojao nijedan ekran. A to je baš onaj blok koji hoćeš
  da proveriš pre nego što plan pustiš da radi mesecima.

## Rešenje: blok se otvara na mestu

Zaglavlje bloka je sada prekidač. Klik otvara pregled ispod njega, drugi klik ga zatvara.
Otvoren je najviše jedan blok.

**Blok koji je generisan i blok koji čeka red nisu ista stvar**, i pregled ih ne prikazuje
kao istu:

| Stanje bloka | Odakle podaci | Šta se vidi |
|---|---|---|
| Generisan (ima mezociklus) | `GET /api/mesocycles/{id}` | dani prve nedelje sa vežbama i `serije×ponavljanja` |
| Čeka red (nema mezociklus) | spisak šablona | dani i vežbe šablona, **bez brojeva** |

Blok na čekanju nema serije ni opterećenja zato što još nisu izračunati - nastaju kad mu
dođe red, od 1RM vrednosti koje tada važe. Prikazati bilo kakav broj tu značilo bi izmisliti
ga, pa pregled umesto toga kaže šta se čeka.

Kod generisanog bloka pregled uzima **prvu nedelju**, uz napomenu da kasnije nedelje
periodizacija pomera. Nedelje se sortiraju po broju pre uzimanja prve - redosled iz odgovora
nije garancija, a test to i proverava tako što nedelje namerno stižu obrnuto.

Nije dodata nijedna nova ruta ni krajnja tačka: oba izvora su već postojala.

## Uklonjeno dugme „Idi na trening"

Stajalo je ispod vremenske linije, a vodi tačno tamo gde vodi i prvi tab donje navigacije,
koji je uvek na ekranu. Dva puta do istog mesta, od kojih jedan zauzima celu širinu.

## Detalj koji je oblikovao markup

Sadržaj `<button>`-a sme da bude samo **fraziranje**, pa `<p>` i `<div>` unutar njega nisu
dozvoljeni. Zaglavlje bloka je zato preraspoređeno u `<span>`-ove, a traka napretka - koja je
`<div role="progressbar">` - ostaje **izvan** prekidača, ispod njega. Da je ostala unutra,
markup bi bio neispravan, a traka bi postala deo klikabilne površine bez razloga.

## Provera

- `npm run build` i `npm test` (**32**, sa 3 nova) prolaze.
- Testovi drže baš razliku između dva stanja bloka: da generisan blok traži svoj mezociklus
  i uzima nedelju broj 1 i kad nedelje stignu van redosleda, da blok na čekanju **ne** traži
  mezociklus nego šablon, i da drugi klik zatvara pregled.
- Prolaz kroz aplikaciju na širini telefona - rezultat se upisuje ovde posle provere u
  pregledaču.
