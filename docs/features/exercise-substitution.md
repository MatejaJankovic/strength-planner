# Zamena vežbe za ostatak bloka

**Grana:** `feature/exercise-substitution`

Korak 8 plana posle revizije (6. 11. 2026), stavka A2. Odluka vlasnika: zamena važi za ceo
ostatak bloka, na treninzima tog dana koji čekaju.

## Problem

Generisan blok nije mogao da promeni vežbu. Ako je sprava zauzeta ili vežba smeta zglobu,
vežbač je mogao samo da je radi, da je preskoči, ili da napravi nov plan. Nov plan, međutim,
povlači nove maksimume i nov raspored.

## Šta je urađeno

- **Pravilo (`ExerciseSubstitution`, domen).** Zamena menja samo vežbu. Opseg, ciljni RIR i
  serije ostaju propis bloka za to mesto, pa je dozvoljena samo vežbi kojoj bi blok propisao
  isto. Propis ugrađenog bloka zavisi od tri stvari, pa se sve tri moraju poklapati:
  - isti glavni mišić (udeo 1.0). Inače bi se nedeljni volumen preselio na drugi mišić, a mesto
    bi prestalo da radi ono zbog čega ga šablon ima. Glave ramena su posle #109 tri različita
    mišića, pa bočno podizanje ne zamenjuje Rear Delt Fly;
  - isti tip. Izolacija na mestu složene vežbe bi ušla u budžet složenih vežbi u treningu;
  - za složenu vežbu, isti odgovor na `SuitsLowReps`. Iskorak na mestu čučnja bi u bloku snage
    nasledio 3-6, a to je runda 14 uklonila.

  Izdržaj bez opterećenja (Plank) se ne nudi, jer ga plan ne može ni propisati ni voditi.
- **Gde se menja.** U izabranom treningu i u svakom treningu istog dana bloka do kraja, koji još
  čeka (`Planned`). Izuzetak je izabrani trening, ako je započet a na vežbi nema serije.
  Odrađeni, započeti i preskočeni treninzi, kao i raniji treninzi istog dana, ostaju sa vežbom
  koju su imali. Vežba sa upisanom serijom se ne menja. Trening koji novu vežbu već ima ne
  dobija je dvaput.
- **Opterećenje.** Briše se. Računa se pri čitanju iz maksimuma nove vežbe, kao za svaku vežbu
  bez odrađenog treninga (runda 12), a posle prvog odrađenog dana ga vodi progresija.
- **Balansiranje.** Posle zamene se blok ponovo balansira, jer sekundarni udeli nove vežbe mogu
  biti drugačiji. Ne balansira se kad je vežbač izabrao „Prati moj šablon".
- **API.** `GET /api/sessions/plans/{planId}/alternatives` i
  `POST /api/sessions/plans/{planId}/substitute`. Oba su ograničena na korisnika (plan mora
  pripadati njegovom bloku, kandidati su sistemske i njegove vežbe).
- **Ekran.** Dugme „Zameni vežbu" stoji na kartici dok na vežbi nema serije, u planiranom i
  započetom treningu. Otvara izbor, rečenicu o tome šta se menja i „Zameni do kraja bloka".

## Usput

`WeeklyVolumeTargetResolver` je mišiće vežbe čitao sa `SelectMany` preko navigacije, kao upit
granica u #110, pa balansiranje nije moglo u test nad SQLite-om (APPLY). Prepisan je kao `Join`
istog značenja, a množenje udela i serija ide u memoriji, jer SQLite decimalni broj čuva kao
tekst.

## Provera

- `dotnet test`: **926** (bilo 914); `npm test`: 213; `npm run build` prolazi.
  `workout-session.scss` je na 7.61 kB (bilo 7.48), ispod tvrdih 8 kB.
- Merenje vraćanjem, jedna izmena po merenju:

| Vraćeno | Obara |
|---|---|
| bez uslova istog opsega (`SuitsLowReps`) | 3 |
| zamena i u ranijim nedeljama istog dana | 1 |
| zamena i u preskočenim treninzima | 1 (pre posebnog testa 0) |
| zamena i posle upisane serije | 1 |
| opterećenje se ne briše | 1 |

- Uživo, na 375 px, nalog za testiranje, blok snage Upper/Lower (srednji nivo), Upper A druge
  nedelje:
  - za Bench Press su ponuđeni Dumbbell Bench Press i Incline Bench Press. Push-up nije, jer ne
    podnosi opseg snage;
  - posle zamene kartica glasi Dumbbell Bench Press, a težina je prazna, jer nalog nema maksimum
    za tu vežbu;
  - u bazi je Upper A zamenjen u nedeljama 2 i 4, nedelja 1 (ranija) je ostala, kao i nedelja 3
    (započeta, sa upisanom kalibracionom serijom); Upper B nije diran;
  - nema prelivanja.

## Šta ovo ne radi

- Ne menja vežbu u već započetom ili odrađenom treningu.
- Ne nudi vežbu koju bi blok propisao drugačije. Za takvu promenu je pravo mesto lični šablon.
- Ne vraća zamenu. Ponovna zamena na staru vežbu radi isto kao prva.
