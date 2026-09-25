# Granice volumena uče iz snage, ne iz RIR-a

Nalaz C15 iz pregleda logike treninga — najveći deo odeljka C.

## Problem

Sve tri granice — MEV, MAV i MRV — pomerao je **isti** signal: odstupanje RIR-a od cilja,
dakle koliko su serije „delovale" u odnosu na propis.

Ali RIR je iskaz o **opterećenju**, a opterećenje ispravlja progresija. Nedelja koja je bila
laka znači da su tegovi bili laki, i sledeći trening već dobija teže. Čitati istu činjenicu
još jednom kao „ovom vežbaču treba više volumena" znači da jedan uzrok pomera dva točka — a
sa balansiranjem serija između njih, to je zatvorena petlja.

Izmereno nad domenskim kodom, nedelja na MAV-u čije su serije dva RIR poena lakše od plana:

| Šta se pomerilo | Iz | U |
|---|---|---|
| MAV (granica volumena) | 16 | **17** |
| Opterećenje (progresija, iste serije) | 100 kg | **107.5 kg** |

Vežbač koji podcenjuje težine dobija i teže tegove **i** više serija, iz jednog istog
podatka.

**Pravilo za MEV je uz to bilo obrnuto.** Glasilo je: nedelja na MEV-u koja je bila laka →
MEV raste. Minimalna efektivna doza je pitanje o stimulusu: nedelja na MEV-u koja je
**donela napredak** znači da je minimum **niži** nego što se mislilo, a ne viši.

## Pravilo

Svaka granica čita dokaz koji njoj pripada.

| Granica | Pitanje | Dokaz |
|---|---|---|
| **MRV** | oporavak | odstupanje RIR-a, udeo otkaza, **i stvaran pad snage** |
| **MAV** | stimulus | promena snage |
| **MEV** | stimulus | promena snage |

- **MRV** gore kada je nedelja bila na ≥ 90% plafona i ostavila rezervu; dole kada je bila
  iznad MEV-a i pokazala umor. Pad snage je od sada treći znak umora.
- **MAV** se sudi samo kada je nedelja zaista trenirala blizu cilja. Napredak → cilj radi,
  ne dira se. Nema napretka a nema ni pada → stimulus je premali za toliko rada, cilj gore.
  Pad → cilj previsok, dole.
- **MEV** se sudi samo kada je nedelja bila na donjoj granici. Napredak → minimum dole.
  Pad → minimum gore.

**Nedelja bez uporedivog merenja ne pomera ni MEV ni MAV.** Prva nedelja bloka, ili nedelja
čija se ponavljanja ne poklapaju sa prethodnom, nema šta da kaže o stimulusu. Ćutanje nije
isto što i ravna nedelja. Plafon oporavka sme da se pomeri i tada, jer njega nose otkazi i
RIR — iskazi o oporavku, ne o stimulusu.

**Prag je jedan procenat**, simetrično. Najmanji stvaran pomak koji vežbač može da napravi
je jedan korak tega: 2.5 kg je 2.5% na stotinu i 5% na četrdeset kilograma. Ispod jednog
procenta je zaokruživanje, pa je nedelja **ravna** — bez dokaza u bilo kom smeru, umesto
slabog dokaza u nekom.

## Kako se promena snage pripisuje mišiću

Po pravilu iz [`comparable-strength-change.md`](comparable-strength-change.md): ista vežba,
isti broj efektivnih ponavljanja, tolerancija jednog. Mišiću se pripisuju **sve** vežbe koje
ga opterećuju, i primarno i sekundarno, sa istom težinom.

Doprinos (1.0 primarno, 0.5 sekundarno) meri koliko je vežba nosila **volumena** za taj
mišić; ovde se pita nešto drugo — da li je ono što mišić diže poraslo. Mišić koji je
ograničavajući faktor u sekundarnoj ulozi o tome govori jednako.

## Šta je izmereno

Uživo, ravan hipertrofijski blok (isti propis svake nedelje, pa isti volumen), grudi:

| Nedelja | Šta je upisano | MEV/MAV/MRV |
|---|---|---|
| — | početno stanje | 10/**16**/22 |
| 1 | 10 ponavljanja na propisanom opterećenju | 10/16/22 — nema poređenja, ništa se ne pomera |
| 2 | ista ponavljanja, **isto** opterećenje | 10/**17**/22 — snaga stoji na ciljnom volumenu, cilj gore |
| 3 | ista ponavljanja, **+5 kg** | 10/**17**/22 — napredak, cilj se ne dira |

Vraćanje starih pravila (MEV i MAV vođeni RIR-om) obara **8 od 576** testova.

## Šta ovo ne rešava

Signal snage je dostupan u oko dve trećine nedelja koje imaju prethodnu (merenje u
[`comparable-strength-change.md`](comparable-strength-change.md)). U ostalima granice
**stoje**. To je namerno: bolje nepokretno nego pokretno po pogrešnom signalu — ali znači i
da se granice uče sporije nego ranije.

Uz izmene iz iste runde (nezavisni signali umora, poredivo poređenje snage) sistem je u
celini **manje sklon** da sam pomera stvari: i deload i granice traže jasniji dokaz nego
pre. Ako se ispostavi da je presporo, mesto za podešavanje je prag od jednog procenta i
`MaxWeeklyStep`, a ne vraćanje RIR-a u pitanje o stimulusu.

## Testovi

`VolumeAdaptationTests` (32 tvrdnje): petlja iz merenja koja više ne postoji (nedelja na
MAV-u sa rezervom ne pomera cilj, a progresija na istim serijama i dalje diže opterećenje
sa 100 na 107.5), napredak na MEV-u koji spušta minimum, pad koji ga diže, nedelja koja je
samo bila lagodna a ne pomera ništa, nedelja bez merenja koja ne pomera ni MEV ni MAV ali
sme da spusti MRV, pad snage kao znak umora za plafon, i prag koji razdvaja promenu od
zaokruživanja. Sva zatečena pravila o pojasu, koraku i lutanju od seed-a ostaju sa svojim
testovima.

Ukupno: 568 → 576 testova na serveru, 139 na klijentu (bez izmena).
