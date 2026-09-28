# Indirektan kredit više ne troši budžet koji pripada direktnom radu

Nalazi E1 i E2 iz pregleda logike treninga.

## Problem

Nedeljni volumen se broji po mišićnoj grupi: vežba ulazi celom serijom u mišić koji radi
glavni posao i polovinom u one koji pomažu. Kada taj *pomažući* udeo ode u grupu koja ga
zapravo ne radi, budžet se troši na pogrešnom mestu — a balansiranje, koje nedelju gađa u
MAV, tada skida serije sa vežbe koja mišić jedino i gradi.

Katalog je to i sam govorio, na dva mesta, u dve protivrečnosti:

| Isti obrazac pokreta | Jedan je imao | Drugi nije |
|---|---|---|
| horizontalni potisak | `Bench Press` → `Shoulders 0.5` | `Push-up` → ništa za ramena |
| iskorak / čučanj na jednoj nozi | `Bulgarian Split Squat` → `Hamstrings 0.5` | `Split Squat` → ništa za zadnju ložu |

Dakle nije bila u pitanju procena — model je već sadržao oba odgovora i koristio pogrešan.

**Zašto je pogrešan baš taj:**

- **Ramena.** Prednji deltoid u potisku radi i raste. Ali „Shoulders" je **zbirna grupa**, a
  njen MAV se troši na bočni i zadnji deltoid — glave koje potisak ne radi. Dok je potisak
  punio taj budžet, balansiranje je grupu videlo blizu cilja i skidalo serije sa bočnog
  podizanja. Vertikalni potisci (`Overhead Press`, `Dumbbell Shoulder Press`) zadržavaju
  rame kao **primarni** mišić: oni bočni deltoid zaista opterećuju.
- **Zadnja loža.** U čučnju radi izometrijski — drži odnos kolena i kuka, ne skraćuje se pod
  opterećenjem. Serija čučnja zato nije pola serije za zadnju ložu ma koliko teška bila.
  Zglobovi kuka koji se zaista savijaju (`Deadlift`, `Hip Thrust`, `Romanian Deadlift`)
  zadržavaju svoj udeo.

## Izmereno pre izmene

Nedeljni volumen koji trening **stvarno dobije** na srednjem nivou (posle skraćivanja po
nivou iskustva), pre i posle:

| Šablon | Ramena | Zadnja loža |
|---|---|---|
| `upper-lower-x3` | 26 → **20** | 16 → **12** |
| `push-pull-legs-6` | 24 → **20** | 16 → **14** |
| `full-body-4` | 22 → **20** | 14 → **12** |
| `full-body` | 18 → **16** | 12 → **10** |
| `upper-lower-ppl` | 16 → **12** | 16 → **14** |
| `push-pull-legs` | 14 → **12** | 10 → **8** |
| `legs-specialization` | 12 → **8** | 14 → **10** |
| `full-body-2` | 10 → **8** | 10 → **8** |
| `upper-lower` | 8 → **8** | 16 → **14** |

MAV za zadnju ložu je 11: bila je preko njega u **7 od 9** šablona, sada u **5**. Kod
`upper-lower-x3` je pre izmene **polovina** njenog volumena dolazila od čučnjeva i iskoraka
(8 od 16).

## Šta je izmena otkrila

Zatečen test `TemplatesOfThreeDaysOrMore_ReachMevAtTheReferenceLevel` je pao:

> `Intermediate upper-lower/Shoulders: 4.0 serija je ispod MEV 8.`

To nije bila greška izmene nego **nalaz**: šablon Upper/Lower je do MEV-a za ramena stizao
samo preko potisaka. Sastav treninga uzima izolacije redom, a `Rear Delt Fly` je u „Upper B"
stajao **poslednji** — iza ruku — pa do srednjeg nivoa nikada nije ni stizao. Jedina direktna
serija za rame u celom šablonu bilo je bočno podizanje.

Zadnji deltoid je pomeren ispred ruku. Sada je 8 serija, kao i pre — ali dve vežbe koje rame
zaista rade, umesto jedne i dva potiska.

## Migracija

Nema je. `DbSeeder.ReconcileSystemExercisesAsync` već usklađuje sistemske vežbe sa katalogom
pri svakom pokretanju. Provereno uživo: pre restarta baza je imala
`Bench Press → Shoulders 0.50` i `Back Squat → Hamstrings 0.50`, posle restarta ih nema, a
`Overhead Press` i dalje ima `Shoulders 1.00`.

## Izmereno posle izmene, uživo

Generisan Upper/Lower blok (napredni nivo), nedelja 1:

| Mišić | Kredit | Odakle |
|---|---|---|
| Shoulders | **15.0** | Lateral Raise 5, Face Pull 5, Rear Delt Fly 5 — sve tri direktne |
| Hamstrings | **12.5** | Leg Curl 5, Leg Curl 5, Deadlift 5×0.5 |

Nijedan potisak i nijedan čučanj ne ulaze u te dve brojke.

## Šta ovo ne rešava

**Grupe „Ramena" i „Leđa" ostaju zbirne.** Jedan cilj i dalje pokriva prednji, bočni i zadnji
deltoid, odnosno latove, gornja leđa i opružače. Pravi lek je razdvajanje grupa — ali ono
traži nove `MEV/MAV/MRV` vrednosti po glavi, migraciju naučenih ličnih granica svakog
korisnika i prepravku analitike. Priručnik iz koga su zatečene vrednosti izvedene daje samo
opseg („obično 8–20 serija nedeljno"), pa bi nove brojeve trebalo izabrati — a to je odluka
o modelu, ne ispravka greške.

Umesto toga je zbirnost **rečena** vežbaču, na ekranu volumena, uz uputstvo šta da radi kada
mu grupa stoji u zoni a jedan njen deo zaostaje.

Isto važi i za `Deadlift → Back 1.0`, koje pregled pominje: u zbirnoj grupi koja uključuje
opružače leđa, pun kredit za mrtvo dizanje je odbranjiv, pa nije diran.

## Testovi

Nema novih. Zatečeni paket je pokrio izmenu bolje nego što bi je pokrio nov test:
`ExerciseCatalogTests` drži konvenciju doprinosa, a `WorkoutTemplateCatalogTests` proverava
da svaki šablon od tri dana naviše dostiže MEV i ne prelazi MRV na sva tri nivoa — i to je
test koji je uhvatio rupu u šablonu Upper/Lower.

Ukupno: 613 testova na serveru (bez izmena), 144 na klijentu (bez izmena).
