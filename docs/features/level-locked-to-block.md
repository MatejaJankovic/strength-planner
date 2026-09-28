# Nivo iskustva pripada bloku, ne profilu

Nalaz D18 iz pregleda logike treninga.

## Problem

Nivo iskustva povlači četiri stvari. Dve odlučuje generator jednom, pri pravljenju bloka
(broj serija na startu, koliko vežbi ulazi u trening). Druge dve se čitaju **iznova posle
svake završene nedelje**:

- skalirane granice volumena (`ExperienceProgramming.ScaleLandmarks`), a preko njih i
  nedeljni cilj i balansiranje serija,
- prag iznad kog umor sam propisuje deload (`ExperienceProgramming.DeloadThreshold`).

Obe su čitale **profil**. Vežbač koji promeni nivo usred bloka time prekraja plan koji je
već propisan — a uputstvo na dva mesta obećava suprotno („Tekući blok ostaje nepromenjen",
„Izmena **ne dira blok koji je već generisan**").

Izmereno uživo, blok napravljen na srednjem nivou pa profil prebačen na napredni:

| Mišić | Kako je stajalo | Posle promene profila |
|---|---|---|
| Chest | MEV 10 · **cilj 16** · MRV 22 | MEV 12 · **cilj 19** · MRV 26 |
| Back | 10 · **18** · 25 | 12 · **22** · 30 |
| Quads | 8 · **14** · 20 | 10 · **17** · 24 |
| Shoulders | 8 · **16** · 26 | 10 · **19** · 31 |

Prag deload-a se u istom potezu spuštao sa 0.60 na 0.50. U drugom smeru je gore: spuštanje
na početnika **uklanja** auto-deload u celosti, jer početnik prag nema — blok koji je imao
automatsko rasterećenje prestaje da ga ima, usred sebe.

## Rešenje

`Mesocycle` pamti nivo sa kojim je generisan, iz istog razloga iz kog već pamti
`PeriodizationModel` i `SetAllocation`: to su odluke koje se čitaju i pošto blok postoji.

Oba čitaoca sada pitaju `BlockExperienceLevel.ForBlockAsync`, jedno mesto — kao
`ComparableWeek` iz prethodne runde. Dva pozivaoca koja postavljaju isto pitanje ne smeju
da na njega odgovore različito.

`GetEffectiveAsync` uz korisnika traži i blok, jer pojas koji vraća skalira baš taj nivo.
Svi pozivaoci su `mesocycleId` ionako imali u ruci.

Profil sada i **piše** ono što je uputstvo tvrdilo, ispod polja na kome se odluka donosi:
*„Važi za sledeći blok. Blok koji je u toku zadržava nivo sa kojim je napravljen."*

## Migracija

`AddMesocycleExperienceLevel` popunjava kolonu **iz profila**, a ne iz nule tipa.

To nije kozmetika. Pošto su oba čitaoca do sada čitala profil, profilov tekući nivo *jeste*
ono od čega je svaki zatečeni blok računat — pa ga upisati znači zadržati zatečeno
ponašanje. Nula (`Beginner`) bi 151 od 160 blokova u razvojnoj bazi preimenovala u
početnički: 19 naprednih bi izgubilo pojas ×1.2, a 132 srednja bi ostala **bez praga za
deload uopšte**.

Izmereno posle migracije: raspodela 9 / 132 / 19 je ista kao u profilima, nijedan red se ne
razlikuje od svog profila, kolona je bez podrazumevane vrednosti i bez `NULL`-a.

## Šta je provereno

Uživo, nalog na srednjem nivou sa ravnim hipertrofijskim blokom:

1. Ekran „Analitika" pokazuje Chest MEV 10 · cilj 16 · MRV 22.
2. Profil se menja na **Napredni** i čuva.
3. Isti blok, isti ekran: **10 · 16 · 22** — nepromenjeno.
4. Nov plan napravljen posle izmene: **12 · 19 · 26** — nov nivo važi za nov blok.

Baza to potvrđuje sa druge strane: profil je 2, blok je ostao 1.

## Poznato ograničenje ovog testiranja

**Vraćanje starog pravila ne obara nijedan test — 0 od 581.** Pravilo živi u upitu unutar
servisa, a projekat nema test harness za servise; domenski testovi proveravaju skaliranje
pojasa, ne to ko je pitan za nivo. Ovo je isti oblik kao `SetLogDto` iz devete runde i
opseg izolacija iz desete: tačno pravilo, zeleni testovi, i samo živa aplikacija kao dokaz.
Zato je merenje gore uzeto u oba smera — sa ispravkom i sa vraćenim starim pravilom, uz
ponovni build i restart API-ja između, pošto `git checkout --` vraća izvor a ne binarni
izlaz.

Tri nova testa (`MesocycleLevelTests`) pokrivaju ono što se odavde može pokriti: da kolona
postoji, da je nenullable i istog tipa kao u profilu, i koliko tačno pomera pojas i prag —
dakle *koliko je vredelo* to razdvojiti.

Uz to, jedna moja pretpostavka je pala na merenju: EF-ov `GetDefaultValue()` za vrednosni
tip vraća njegovu nulu bez obzira na to da li je podrazumevana vrednost ikada
konfigurisana — isto javljaju i `SetAllocation` i `PeriodizationModel`, koji je nemaju. Test
koji je tvrdio da kolona nema default je zato tvrdio nešto što model ne izražava, i uklonjen
je; to je tvrdnja o migraciji, ne o modelu.

Ukupno: 578 → 581 testova na serveru, 139 na klijentu (bez izmena).
