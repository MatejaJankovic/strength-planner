# Pad snage se meri poredivim sa poredivim

Nalaz C17 iz pregleda logike treninga — i nalaz koji je merenje **preusmerilo**.

## Šta je nalaz tvrdio

Da e1RM član ocene umora poredi nedelje sa **različitim opsezima ponavljanja**: Epley greška
raste sa brojem ponavljanja, pa prelaz sa 11–12 na 6–10 može da izgleda kao pad snage.

## Šta merenje kaže

**Taj mehanizam u aplikaciji ne postoji.** Kada se propis nedelje promeni, opterećenje se
**ponovo izvodi** iz najsvežije procene (pravilo iz devete runde), pa se procena sama sa
sobom poklapa. Izmereno u živoj aplikaciji, linearni blok, nedelja odrađena na vrhu opsega
pa nedelja na dnu:

| Nedelja | Propis | Opterećenje | Procena |
|---|---|---|---|
| 1 | 8–12 @RIR 2 | 105.0 kg | 154.0 |
| 2 | 8–12 @RIR 1 | **117.5 kg** | 152.8 |

Promena: **−0.8%**. Signalu treba 5% za punu težinu, pa je ocena umora ista i sa starim i sa
novim pravilom (**0.103** u obe verzije). Isto važi i za sam pomeraj prozora: nad domenskim
kodom, uz istu poziciju u opsegu, najgora uzastopna promena je **−0.42%** kod hipertrofije,
**−3.29%** kod snage (linearno) i **−1.32%** kod snage (obrnuto).

**Ali artefakt postoji — u ravnom bloku.** Tamo je propis svake nedelje isti, pa se
opterećenje **prenosi** uz korak umesto da se izvodi iz sveže procene. Ista provera, ravan
hipertrofijski blok, opet vrh pa dno (oba puta po propisu):

| Nedelja | Propis | Opterećenje | Procena (Bench, Squat) |
|---|---|---|---|
| 1 | 8–12 @RIR 1 | 107.5 kg | 154.1 |
| 2 | 8–12 @RIR 1 | **110.0 kg** | **143.0** |

Na glavnim dizanjima to je **−7.2%**, prosek nedelje **−3.5%**, a ocena umora dobija
**0.174** — iz nedelje u kojoj ništa nije pošlo naopako. Ravan blok je i **podrazumevani**
model.

Dakle nalaz je pokazao na pogrešan blok: pomeranje prozora je slučaj koji se sam ispravlja,
a prenošenje opterećenja je slučaj koji ne.

> Jedno merenje iz moje prve probe je bilo pogrešno i ispravljam ga naglas: dobio sam
> **−9.3% do −9.9%** za „vrh pa dno" tako što sam opterećenje obe nedelje izveo iz **fiksnog**
> maksimuma od 140 kg. Nijedna putanja u aplikaciji to ne radi — procena iz nedelje ulazi u
> opterećenje sledeće. Taj broj opisuje vežbača kome se procena nikada ne osvežava.

## Pravilo

Poredi se ono što je uporedivo: **ista vežba, isti broj efektivnih ponavljanja**, uz
toleranciju od jednog (`StrengthChange.ComparableRepSpread`). Porede se procene, a ne sirova
opterećenja, da bi to jedno ponavljanje razlike bilo **naplaćeno** a ne prećutano; kada su
ponavljanja jednaka, to je isti odnos.

Nema uporedivog para → **nema dokaza**, pa član nosi nulu. Isto pravilo je i ranije važilo
za nedostatak uporedive nedelje: nedostatak podatka se ne čita kao pad.

Referenca je **najbolja** uporediva serija prethodne nedelje — to je ono što je vežbač tada
mogao, i to je konzervativan smer za otkrivanje pada.

Poređenje je premešteno iz `DeloadService` u domen (`StrengthChange`), gde može da se
testira; servis je zadržao dva upita i predaje serije.

## Šta ovo menja u praksi

| Slučaj | Staro | Novo |
|---|---|---|
| Ravan blok, vrh pa dno (po propisu) | ocena +0.174 | **0** |
| Periodizovan blok, vrh pa dno | 0.103 | 0.103 (nepromenjeno) |
| Isti broj ponavljanja, manje opterećenja | pad, ceo | **pad, ceo** |
| 11 naspram 12 ponavljanja | poredi se | poredi se (Epley plaća razliku) |
| 12 naspram 9 ponavljanja | poredilo se | **ne poredi se** |

Pad snage koji je stvaran i dalje se vidi ceo: iste serije sa manjim opterećenjem daju
tačno taj procenat.

## Šta je izmereno

- Vraćanje starog poređenja (bez uslova uporedivosti) obara **2 od 566** testova.
- Živa aplikacija, ravan blok: **0.174 → 0**. Periodizovan blok: **0.103 → 0.103**.
- Pojedinačne procene iz baze: 154.1 → 143.0 na glavnim dizanjima (−7.2%), 57.3 → 55.3 na
  izolacijama (−3.5%), 105 → 117.5 kg u periodizovanom bloku (−0.8%).

## Testovi

`StrengthChangeTests` (13 tvrdnji): slučaj ravnog bloka sa starom merom ostavljenom kao
oracle, slučaj periodizovanog bloka koji pokazuje da artefakta tamo nema, stvaran pad na
istim ponavljanjima, napredak, tolerancija od jednog ponavljanja, dva ponavljanja razlike
koja nisu uporediva, vežba bez para, serija iz koje sistem ionako ne procenjuje
(`CanEstimateFrom`), prazna nedelja, i to da se uzima najbolja uporediva serija — i tekuće
i prethodne nedelje.

Ukupno: 555 → 568 testova na serveru, 139 na klijentu (bez izmena).
