# Slične složene vežbe ne idu u uzastopne dane

**Grana:** `fix/session-spacing`

Druga grana iz revizije nauke o treningu (odeljak F), nalaz F6.

## Problem

Priručnik (str. 3) traži bar jedan dan pauze između benča i potiska za ramena, između mrtvog
dizanja i čučnja, „kao i bilo koju kombinaciju složenih vežbi koje angažuju slične mišiće".
Datumi treninga se biraju samo po broju dana u nedelji (`TrainingWeekSchedule`), a redosled
dana dolazi iz šablona — i nijedan test nije proveravao šta se time nađe u uzastopnim danima.

Izmereno probom nad pravim katalogom, za sva tri nivoa:

| Šablon | Uzastopni dani | Šta se sudara |
|---|---|---|
| Legs Specialization | Legs A → B → C (pon, uto, sre) | noge tri dana zaredom, na **svakom** nivou |
| Legs Specialization | Upper A → Upper B (pet, sub) | bench pa incline bench, veslanje pa zgib (početnik, srednji) |
| Full Body (4 dana) | Day A → B (pon, uto) | čučanj pa RDL (početnik, srednji) |
| Full Body (4 dana) | Day C → D (čet, pet) | leg press pa front squat (početnik, srednji) |

## Rešenje

- **Legs Specialization** menja redosled dana: Legs A, Upper A, Legs B, Upper B, Legs C. Noge
  padaju prvog, trećeg i šestog dana od početka bloka, gornji deo drugog i petog (pon, sre, sub
  i uto, pet kad blok počne u ponedeljak). Vežbe i dani su isti.
- **Full Body (4 dana)** svaki dan nosi vežbu za noge kao drugu složenu (čučanj, RDL, leg press,
  front squat), a početnik i srednji nivo je zadržavaju, pa nijedan redosled na
  pon/uto/čet/pet ne izbegava sudar. Četiri treninga u sedam dana uvek imaju bar jedan par
  uzastopnih dana (četiri razmaka od bar dva dana traže osam dana), pa šablon sada nosi
  sopstveni raspored: **prvi, treći, četvrti i šesti dan** (pon, sre, čet, sub). Jedini
  uzastopni par je treći–četvrti: RDL pa leg press. To je i dalje par iz pravila (mrtvo pa
  čučanj), prihvaćen kao najblaži mogući — čučanj na mašini posle pregiba u kuku — umesto
  čučnja pa RDL-a (pon/uto/čet/sub) ili front squata pa čučnja preko vikenda (pon/sre/pet/ned).
  Napredni vežbač zadržava samo prvu složenu vežbu dana (čučanj, veslanje, potisak, zgib) i
  sukoba nije ni imao.
- **Prelaz između blokova plana.** Sledeći blok je počinjao dan posle poslednjeg treninga
  prethodnog, pa je svaki blok ispadao iz mreže nedelja koju raspored šablona podrazumeva. Sa
  novim redosledom bi Legs Specialization (noge u subotu) počinjao sledeći blok nogama u
  nedelju. Sada blok kreće tamo gde se završavaju nedelje prethodnog (`MacrocyclePlanner.
  NextBlockStart`), a kasnije samo ako bi to bilo pre dana posle poslednjeg treninga ili u
  prošlosti. Nađeno u reviziji.
- `WorkoutTemplate` i `ResolvedTemplate` dobijaju opciono `DayOffsets`, a generator ga koristi
  preko `TrainingWeekSchedule.OffsetFor(dani, indeks, raspored)`. Neispravan raspored (pogrešan
  broj dana, dva treninga istog dana, van nedelje) pada na podrazumevani umesto da napravi dva
  treninga istog datuma.

Lični šabloni se ne diraju: redosled dana je tamo korisnikov izbor. Već generisani blokovi
zadržavaju svoje datume.

### Kako se meri „slično"

Porodicom pokreta izvedenom iz doprinosa mišićima, u testu: sve što radi kvadriceps, zadnju
ložu ili gluteus su „noge"; inače primarni mišić — grudi i ramena su guranje, leđa povlačenje.

Prva proba je merila „deli bilo koji mišić" i proglasila sukobom Upper/Lower, Upper/Lower x3,
Upper/Lower + PPL i Push/Pull/Legs x2 (trodnevni Push/Pull/Legs nema uzastopne dane), jer dan
sa veslanjem ili zgibom pa dan sa RDL-om ili mrtvim dizanjem dele grupu „Back". Ta grupa sabira lat i kičmene mišiće — isto
ograničenje zbirnih grupa koje je runda 13 imenovala. Priručnik takav par ne navodi, i nijedan
takav program ga ne izbegava, pa bi test tražio nešto što nije pravilo.

## Provera

- `dotnet test`: **693** (bilo 683), `npm run build` prolazi.
- Nov test `NoTwoConsecutiveDays_TrainTheSameMovementFamily` prolazi kroz svaki šablon i nivo i
  dozvoljava tačno jedan imenovan par: Full Body (4 dana), Day B → Day C. Raspored koji bi sukob
  pomerio na drugi par pada. Vraćen stari katalog, test pada sa tačno sukobima iz tabele iznad.
- `MacrocyclePlannerTests`: sledeći blok počinje na mreži nedelja prethodnog, a nikad pre dana
  posle poslednjeg treninga ni u prošlosti.
- `TrainingWeekScheduleTests`: ispravan raspored šablona se koristi, četiri vrste neispravnog
  padaju na podrazumevani, indeks van nedelje i dalje baca izuzetak.
- End-to-end kroz API, početak ponedeljak 5. 10. 2026:

  | Šablon | Prva nedelja |
  |---|---|
  | Legs Specialization | pon Legs A · uto Upper A · sre Legs B · pet Upper B · sub Legs C |
  | Full Body (4 dana) | pon Day A · sre Day B · čet Day C · sub Day D |
  | Upper/Lower (kontrola) | pon Upper A · uto Lower A · čet Upper B · pet Lower B — nepromenjeno |

  Prelaz između blokova, plan od dva bloka Full Body (2 dana) od ponedeljka 5. 10: poslednji
  trening prvog bloka je u četvrtak 29. 10, a drugi blok počinje u **ponedeljak 2. 11** (ranije
  bi počeo u petak 30. 10).

  Ekran „Trening" na 375 px prikazuje naizmenične dane; bez horizontalnog prelivanja (izmereno
  posle reload-a — snimak pre reload-a je izgledao odsečeno, kao u rundi 13).

## Poznata ograničenja

- **Full Body (4 dana) i dalje ima jedan uzastopni par** (treći–četvrti dan, RDL pa leg press)
  i on je po priručniku sukob, samo najblaži mogući. Da ga nema, šablon bi morao da ima dan bez
  vežbe za noge.
- **Datumi su predlog.** Aplikacija ne sprečava da vežbač odradi dva treninga zaredom; test
  čuva samo ono što plan predlaže.
- **Lični šablon može da složi slične dane jedan za drugim**; ekran šablona na to ne upozorava.
