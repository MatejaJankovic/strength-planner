# Volumen naprednog nivoa

**Grana:** `fix/experience-volume-consistency`

Sedma grana iz revizije nauke o treningu (odeljak F), nalaz F4, odluka D3.

## Problem

Kod naprednog nivoa su dve poluge vukle na suprotne strane.

- `StartingSetsPerExercise` je pratio priručnik („manji volumen, uz napredne tehnike") i
  naprednom davao **3** serije po vežbi, manje od srednjeg nivoa (4).
- `LandmarkScale` je naprednom množio granice volumena sa **1.2**, najviše od tri nivoa.

Propis je tako bio ispod vežbačevog MEV-a, a razliku je krpilo balansiranje. Priručnikov manji
volumen podrazumeva napredne tehnike (drop set, rest-pause), koje nose ostatak stimulusa, a
njih aplikacija ne modeluje. Bez njih je manje serija samo manje stimulusa, i to baš za vežbača
kome granice stoje najviše. Kod treniranih više serija daje više hipertrofije (Schoenfeld i sar.
2019).

Test `LevelConstants_CapTheWeekBelowTheAdvancedLifterOwnMev` je to beležio i tražio da bude
obrisan „kad se konstante usklade". Obrazloženje skale je za početnika bilo naopako: „serija
početnika je slabiji stimulus" bi tražila **više** serija, a ne manje.

## Rešenje

- Napredni nivo kreće sa **4** serije po vežbi, kao srednji. Granice ×1.2 ostaju.
- Obrazloženje skale je ispravljeno. Početnik raste i na manjem volumenu i ima manji radni
  kapacitet, a napredni treba više volumena da bi i dalje rastao.
- Test sa zbirom je zamenjen sa dva nova:
  - `AHigherVolumeBand_NeverStartsWithFewerSetsPerExercise`: nivo sa višim granicama ne
    kreće sa manje serija. To je pravilo koje je bilo prekršeno.
  - `AnAdvancedHypertrophyWeek_FallsBelowMev_OnlyWhereRecorded`: tačan spisak (šablon,
    mišić) gde napredni i posle balansiranja ostaje ispod MEV-a, sa uzrokom za svaki par
    (opisano ispod). Prva verzija ovog testa je proveravala samo grudi i leđa na
    Push/Pull/Legs i za oba krivila granicu po treningu; review je pokazao da je to tačno samo
    za grudi.

## Šta je merenje reklo suprotno od očekivanja

1. **Test koji je trebalo da padne nije pao.** Stari test je poredio zbir serija (3 dana × 6
   vežbi × serije) sa zbirom MEV vrednosti svih grupa (90). Sa 4 serije je 72 < 90, pa je
   prolazio i dalje. Račun je bio pogrešan u samom temelju: serija složene vežbe puni više
   grupa, pa zbir MEV-a nije ono što nedelja mora da isporuči. Pravi manjak propisa, meren po
   mišiću preko svake trenažne nedelje ugrađenih šablona od tri dana naviše:

   | | 3 serije | 4 serije |
   |---|---|---|
   | Hipertrofija, ravan model (od 240) | 153 | 75 |
   | Hipertrofija, linearan model - podrazumevani (od 400) | 223 | 123 |
   | Snaga, ravan model (od 228) | 87 | 27 |
2. **Na ekranu Analitika se broj grupa ispod MEV-a skoro ne menja.** Plan je tvrdio da vežbač
   koji odradi tačno propisano vidi „ispod MEV-a". Uživo, ista nedelja odrađena po planu sa
   starim i sa novim pravilom daje:

   | Šablon | 3 serije | 4 serije |
   |---|---|---|
   | Upper/Lower | 0 od 10 grupa ispod MEV-a | 0 od 10 |
   | Push/Pull/Legs (3 dana) | 4 od 10 (leđa 7.5, grudi 11, gluteusi 2.5, zadnja loža 5) | 4 od 10 (leđa 9, grudi 11, gluteusi 3, zadnja loža 6) |

   Balansiranje je manjak već krpilo. U simulaciji je nedelja hipertrofije podigla 155 od 195
   vežbi, i to 131 do granice pomeraja (+2). Sa 4 serije ih podiže 118, a do granice 80. Posle
   balansiranja je ispod MEV-a ostalo, sa 3 pa sa 4 serije: hipertrofija ravan 36 → 33,
   hipertrofija linearan 69 → 55, snaga ravan 24 → 6, snaga linearan 47 → 15. Izmena je dakle
   najviše promenila **ko nosi serije**: propis umesto balansiranja, a kod bloka snage i sam
   ishod.
3. **Ono što ostaje ima dva uzroka, i prva verzija ove grane ih je pomešala.** Tvrdila je da
   granica serija po treningu drži i grudi i leđa na Push/Pull/Legs. Review je izmerio uzrok
   za svaki par tako što je ponovio balansiranje bez granice:

   - **Granica po treningu (11).** MEV naprednog za grudi i leđa je 12. Mišić koji se trenira
     u jednom treningu nedeljno staje na 11, a bez granice bi balansiranje doseglo MEV. To su
     grudi na Push/Pull/Legs (bez granice 18) i leđa na Upper/Lower + Push/Pull/Legs, gde Upper
     dan naprednom zadržava samo bench (bez granice 12).
   - **Sastav treninga.** U hipertrofiji napredni dobija jednu složenu vežbu po treningu
     (priručnikovo „do 3 složene nedeljno"), pa MEV ne doseže ni bez granice. Leđa na
     Push/Pull/Legs imaju samo jedno veslanje (9 i bez granice). Gluteusi i zadnja loža volumen
     dobijaju iz složenih vežbi. Listovi i trbuh na Full Body imaju po jednu vežbu nedeljno.

   Rečenica „nijedna konstanta to ne popravlja" je bila netačna. Manjak na grudima je posledica
   izabrane skale (×1.2 daje MEV 12 naspram granice 11), a manjak iz sastava posledica budžeta
   složenih vežbi - obe su konstante nivoa. Ova grana ih ne menja, jer je to odluka o modelu,
   ne ispravka greške.

U osnovnoj nedelji ravnog bloka hipertrofije spisak je 11 parova, i test ga drži tačno:

| Šablon | Ispod MEV-a | Uzrok |
|---|---|---|
| Full Body | gluteusi, listovi, trbuh | sastav |
| Push/Pull/Legs (3 dana) | grudi | granica po treningu |
| Push/Pull/Legs (3 dana) | leđa, zadnja loža, gluteusi | sastav |
| Full Body (4 dana) | gluteusi | sastav |
| Upper/Lower + Push/Pull/Legs | leđa | granica po treningu |
| Upper/Lower + Push/Pull/Legs | gluteusi | sastav |
| Push/Pull/Legs x2 | gluteusi | sastav |

Upper/Lower (4 dana) i Legs Specialization u toj nedelji nemaju nijedan. U linearnom modelu,
koji je podrazumevani, prva nedelja nosi seriju manje. Tada i Upper/Lower ima gluteuse ispod
MEV-a (u bloku snage biceps), Full Body i Full Body (4 dana) grudi, a Legs Specialization
leđa. Od druge nedelje Upper/Lower nema nijedan par. Zato uputstvo naprednom preporučuje
Upper/Lower.

## Provera

- `dotnet test`: **803** (bilo 802: jedan test obrisan, dva dodata); `npm test` 192 i
  `npm run build` prolaze.
- Merenje vraćanjem (commit, 4 → 3, rebuild): obara **2** od 803. Pada
  `AHigherVolumeBand_NeverStartsWithFewerSetsPerExercise`, i
  `AnAdvancedHypertrophyWeek_FallsBelowMev_OnlyWhereRecorded`, jer sa 3 serije spisak nije isti.
  Tada su ispod MEV-a i grudi na Full Body i Full Body (4 dana) i leđa na Legs Specialization,
  a gluteusi na Upper/Lower + Push/Pull/Legs i Push/Pull/Legs x2 nisu. Sa 4 serije ta dva para
  padaju na 4.5 od 5. To su jedina dva para koja je ova izmena gurnula ispod MEV-a, i test ih
  drži. Zašto balansiranje gluteusima tu daje manje nije ispitano.
- Proba sa 4 serije pre izmene: ceo paket zelen, uključujući i test MRV-a za svaki šablon na
  naprednom nivou. Taj test gleda samo osnovnu nedelju. Posle balansiranja nijedna nedelja ne
  prelazi MRV ni na jednom nivou. Sam propis prelazi u periodizovanim nedeljama: kod
  naprednog 0 nedelja-mišića sa 3 serije i 68 sa 4 (28 u modelima koje čarobnjak nudi).
  Srednji nivo već ima 305, a početnik 244. Propis bez balansiranja dobija samo vežbač koji
  za ugrađeni šablon izabere „Prati moj šablon". Ta opcija se nudi za svaki blok, a njen
  tekst kaže samo da volumen „može ostati ispod cilja". To je zapisano kao zaseban zadatak.
- End-to-end, uživo (napredni, hipertrofija, ravan model, nedelja 1 odrađena tačno po planu,
  podrazumevane granice): propis 4 serije po vežbi, a Analitika daje brojeve iz tabele iznad.
  Isto je pušteno i sa vraćenim pravilom (3 serije), posle rebuild-a i restarta API-ja.

## Ograničenja

- Postojeći blokovi zadržavaju broj serija sa kojim su generisani. Nivo se čita sa bloka
  (runda 12), a serije sa plana.
- Za naprednog u hipertrofiji ostaje 11 parova (šablon, mišić) ispod MEV-a u osnovnoj nedelji
  (tabela iznad). Najviše ih je na Push/Pull/Legs od tri dana.

## Posle review-a

Review (tri recenzenta, po dva nezavisna proveravača za svaki nalaz) je potvrdio svih 12
nalaza. Nijedan nije visok; ispravljeno je:

- test strukturne granice je zamenjen tačnim spiskom sa uzrokom, i opis uzroka iznad;
- Upper/Lower + Push/Pull/Legs je dodat u uputstvo i u belešku (leđa, granica po treningu);
- brojevi sada kažu na koji se model odnose, uz podrazumevani linearni;
- rečenica o MRV-u je svedena na ono što je provereno;
- primer u uputstvu za lični šablon („polovio bi tri serije") i beleška iz runde 2
  (`more-templates.md`) su usklađeni sa 4 serije.
