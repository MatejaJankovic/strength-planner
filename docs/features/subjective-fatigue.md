# Odgovor vežbača o umoru u oceni umora nedelje

**Grana:** `feature/subjective-fatigue`

Runda 20, stavka 20.2. Plan je tražio da se pre gradnje simulira dvoje: da li novi signal popravlja pogađanje
deload-a, i da li je nezavisan od postojećih (runda 15 je pokazala da „dva signala" mogu biti jedan uzrok izbrojan
dvaput).

## Problem

Ocena umora je čitala četiri signala, i sva četiri su izvedena iz onoga što je upisano (rezerva, otkazi, procena
maksimuma, volumen). Vežbač koji se oseća iscrpljeno a i dalje pogađa brojeve ne daje ocenu ničemu.

## Šta je urađeno

- **Pitanje.** Uz „Završi trening" stoji neobavezno „Kako se osećaš posle treninga?", od 1 (sveže) do 5 (iscrpljen).
  Šalje se **sa završetkom** (`POST /api/sessions/{id}/complete`, telo `{ "perceivedFatigue": 4 }`, ili prazno):
  ocena nedelje se računa kad se završi njen poslednji trening, pa odgovor iz tog treninga mora da bude u bazi do tada.
  Upisuje se u `WorkoutSession.PerceivedFatigue` (nova kolona, `int?`, uz proveru 1-5 u bazi; migracija
  `AddPerceivedFatigue`, primenjena na lokalnu bazu, bez popunjavanja: stari treninzi nemaju odgovor).
- **Signal (`SubjectiveFatigue`, domen).** Ne čita se nivo odgovora nego **porast**: prosek odgovora nedelje nad
  prosekom prosečnih odgovora ranijih trenažnih nedelja istog bloka. Nedelje rasterećenja se ne broje (u njima se
  vežbač oseća bolje, pa bi spustile osnovu). Nema odgovora ili nema osnove (prva nedelja) znači nula, ne „ravno".
- **Potvrda.** Porast ulazi u ocenu tek kad je i **prethodna trenažna nedelja** porasla za bar pola poena
  (`FatigueEvaluator.ConfirmedSubjectiveRise`). Nedelja bez odgovora ne potvrđuje ništa.
- **Težina 0.20.** Ocena sada ima pet signala; odgovor nosi 0.20 (porast od jednog poena je pun signal). Ostale težine
  i pragovi nisu menjani, pa ocena više nije ograničena sa 1 nego sa **1.20**.
- **Ekran.** Komponenta `FatigueRating` (pet polja, `role="radiogroup"`, ponovni dodir poništava izbor) iznad
  „Završi trening", samo dok trening traje. Tekstovi „od 1" uz ocenu na pregledu i u rezimeu su izbačeni, jer ocena
  sme da pređe 1.

## Merenje pre gradnje

`docs/simulations/subjective_fatigue.py`, po uzoru na `fatigue_signal_noise.py` (isti model šuma, isti blokovi).
**Parametri modela za odgovor su pretpostavke**, jer aplikacija odgovor do sada nije prikupljala: koliko odgovor
prati stvaran umor (`TRAG`) i koliko deli uzrok sa slabim danom (`RHO`: 0 znači da odgovor ne zna ništa o njemu, 0.9
da je gotovo isto što i on). Zato se `RHO` menja, a ne bira.

Periodizovan blok od šest nedelja, srednji nivo (prag 0.60), udeo blokova sa deload-om iz ocene:

| Dizajn | RHO | +1%/ned., MRV (lažni) | -2%/ned., MAV | -3%/ned., MRV (uhvaćen) |
|---|---|---|---|---|
| danas (bez odgovora) | - | 2.7% | 12.5% | 55.2% |
| dodaje se, težina 0.20 | 0 / 0.5 / 0.9 | 19.7 / 32.2 / 44.0% | 26.2 / 29.5 / 32.4% | 92.6 / 94.7 / 96.0% |
| dodaje se, težina 0.25 | 0 / 0.5 / 0.9 | 24.6 / 37.8 / 50.2% | 50.7 / 61.2 / 69.5% | 94.5 / 96.0 / 97.1% |
| sa potvrdom, težina 0.25 | 0 / 0.5 / 0.9 | 6.1 / 6.4 / 5.9% | 27.1 / 30.7 / 33.8% | 75.0 / 72.9 / 70.3% |
| **sa potvrdom, težina 0.20 (izgrađeno)** | 0 / 0.5 / 0.9 | **5.3 / 5.8 / 5.4%** | 21.2 / 24.6 / 28.9% | **72.5 / 71.0 / 69.0%** |

- **Nepotvrđen odgovor nije prihvatljiv.** Vežbač koji napreduje na MRV-u dobijao bi lažno rasterećenje u 20-50%
  blokova, tim više što odgovor više deli sa slabim danom. Slab dan spušta snagu i rezervu, a diže i odgovor: tri
  signala iz jednog uzroka. Korelacija člana odgovora sa članom rezerve u nedeljama bez pravog umora je 0.00, +0.20,
  +0.42 i +0.68 za RHO 0, 0.3, 0.6 i 0.9 (sa padom snage skoro ista), pa „nezavisnost" nije osobina signala nego
  pretpostavka koja zavisi od ponašanja vežbača.
- **Potvrda je ono što ga čini upotrebljivim, i izabrana je ista kao za snagu (runda 15).** Lažno rasterećenje se
  drži oko 5-6% (danas 2.7%) bez obzira na RHO, a pad od 3% nedeljno se hvata u oko 70% blokova umesto u 55%.
  Cena je +2.6 do 3.1 poena lažnih rasterećenja za +14 do 17 poena uhvaćenih padova.
- **Težina 0.20, a ne 0.25.** Rezerva nosi 0.35; 0.35 + 0.25 je tačno prag 0.60, pa bi par koji deli uzrok sam
  pokretao rasterećenje, a to je upravo ono što je runda 11 uklonila. Sa 0.20 par daje 0.55 i treći signal mora da
  se pridruži. Test `TheReserveAndTheAnswer_CannotTriggerADeloadWithoutAThirdSignal` to drži.
- **Ako odgovor loše prati stvaran umor** (`TRAG` 0.75 umesto 1.5, RHO 0.5), uhvaćeno -3% pada na 61.2% (i dalje iznad
  55.2%), -2% na MAV-u na 19.4%.
- **Ravan blok od četiri nedelje: ništa se ne menja**, kao ni kod pada snage: druga nedelja nema prethodnu koja bi
  je potvrdila, a posle treće sledi planirani deload. Nepotvrđen odgovor bi tamo dao 7-19% lažnih rasterećenja na
  MRV-u, pa je potvrda ostala bez izuzetka.
- **Napredni nivo (prag 0.50):** gotovo isto kao danas na MRV-u (65.7% naspram 65.0%: tamo rezerva i volumen sami
  dostižu 0.50, što je poznato od runde 15), a -2% na MAV-u 42.5% naspram 29.6%.

Odluka je dakle: izgraditi potvrđen signal, a u izveštaju reći da je dobit **pretpostavljena, ne izmerena**. Kad budu
postojali stvarni odgovori, `TRAG` i `RHO` se mogu proceniti iz baze i ova tabela ponoviti.

## Odluke vredne pamćenja

- **Odgovor se piše uz završetak, ne naknadno.** Naknadni poziv za poslednji trening nedelje stizao bi posle ocene.
  Posledica: odgovor se ne menja kasnije, ni kroz ispravku završenog treninga (ispravka ponovo računa nedelju iz
  odgovora koji je već upisan).
- **Granica potvrde je pola poena**, upola manje od porasta koji daje punu težinu: nedelja koja očigledno nije
  porasla nije druga nedelja istog.
- **„Prethodna nedelja" je prethodna trenažna nedelja, ne poslednja koja je odgovorila.** Nedelja bez odgovora ne
  potvrđuje ništa, ma koliko ranije nedelje bile visoke.
- **Obavezna polja umesto podrazumevanih.** `WeeklyFatigue` je dobio dva nova polja bez podrazumevane vrednosti, kao
  i `PreviousE1RmChangeShare` u rundi 15: podrazumevana nula bi tiho isključila član za svakog ko ga zaboravi. Zato
  je servis, jedino mesto koje pravi taj zapis, prvo prestao da se prevodi.

## Šta je usput nađeno

- **Ni jedna nedelja sa prethodnom nije mogla da se zatvori u testu nad bazom.** `GetStrengthChangeAsync` je mišiće
  vežbe čitao kroz `SelectMany` preko navigacije, što SQLite ne ume da prevede (APPLY): `SessionCompletionTests` su
  zatvarali samo prvu nedelju, koja nema prethodnu, pa se to nikad nije videlo. Prvi test koji je zatvorio treću
  nedelju pao je tu. Upit je prepisan kao `join`, istog značenja, kao u susednoj metodi i kao u rundi 17.

## Provera

REZULTATI

## Šta ovo ne radi

- Ne prikuplja odgovor posle treninga (samo uz završetak) i ne menja ga kasnije.
- Ne prikazuje istoriju odgovora ni grafikon; vidi se samo kroz ocenu umora.
- Ne zamenjuje nijedan postojeći signal i ne pokreće rasterećenje sam.
- Ne pomaže ravnom bloku od četiri nedelje.
- Ne zna šta je „dobar" odgovor: ko uvek kaže 4 nema porast.
- Ne proverava pretpostavke simulacije (`TRAG`, `RHO`): za to su potrebni stvarni odgovori.
