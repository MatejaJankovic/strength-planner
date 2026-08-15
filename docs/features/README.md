# Šta je urađeno, po granama

Beleške za pregled, ne tekst za rad. Svaka strana ima isti raspored: **problem** (šta je
aplikacija radila i zašto to nije bilo dobro), **rešenje**, **provera** i **poznata
ograničenja** — uključujući ono što je revizija koda našla i kako je ispravljeno.

## Prvi krug — „future improvements" iz zaključka rada

| Grana | O čemu je | PR |
|---|---|---|
| [Korak opterećenja po vežbi](per-exercise-weight-step.md) | Šipka i bučice ne idu istim koracima; korak se izvodi iz sprave, uz mogućnost ručne izmene | #2 |
| [Serije do otkaza](failed-reps-logging.md) | Zastavica otkaza i broj urađenih ponavljanja, uz simetričnu korekciju opterećenja | #3 → #7 |
| [Adaptivne MEV/MRV granice](adaptive-volume-landmarks.md) | Granice volumena se uče iz odgovora korisnika umesto da stoje na populacionom proseku | #4 |
| [Automatski deload](auto-deload.md) | Rasterećenje se pokreće iz izmerenog zamora, a ne iz kalendara | #5 |
| [Makrociklusi](macrocycles.md) | Lanac blokova; sledeći se generiše kad prethodni završi, od 1RM vrednosti koje tada važe | #6 |

## Drugi krug — izvedeno iz priručnika

Analiza sa ishodom po stavci: [`../analiza-prirucnika.md`](../analiza-prirucnika.md).

| Grana | O čemu je | PR |
|---|---|---|
| [Stimulativni volumen i MAV](stimulative-volume.md) | Serija daleko od otkaza se ne broji isto; dodat MAV kao ciljna vrednost | #8 |
| [Nivo iskustva određuje program](experience-level.md) | Nivo iz profila konačno utiče na plan — četiri poluge umesto nijedne | #9 |
| [Više šablona](more-templates.md) | Sedam šablona za 2–6 dana nedeljno, plus izolacione vežbe kojih uopšte nije bilo | #10 |
| [Periodizacija po nedeljama](periodization-models.md) | Nedelje unutar bloka više nisu iste: ravan, linearan i obrnut raspored | #11 |

## Peti krug — predlog serija koji cilja volumen

| Grana | O čemu je | PR |
|---|---|---|
| [Predlog serija po nedeljnom volumenu](weekly-volume-set-targets.md) | Broj serija se bira tako da nedelja padne u ciljnu zonu svakog mišića, i prilagođava se kada trening ne ispuni predlog | #37 |

## Šesti krug — ispravke po spisku korisnika

| Grana | O čemu je | PR |
|---|---|---|
| [Raspored na telefonu i tekst](mobile-layout-and-copy.md) | Navigacija u dva reda, izbor nedelje prelomljen, naslov plana preko pola ekrana, meniji bloka nečitljivi; plus izmene teksta i uklanjanje dugih crta | #39 |
| [Polja profila](profile-fields.md) | Pol je bio slobodan tekst pa se izabrana vrednost nije prikazivala; sada je enum. Uklonjeno "Treninga nedeljno", koje je služilo samo oznaci "predlog za tebe" | #40 |
| [Lični šabloni treninga](custom-workout-templates.md) | Sam biraš dane, vežbe, serije i opseg ponavljanja; auto-regulacija nastavlja da radi nad tvojim brojevima | #41 |

## Ako čitaš samo jedno

[Periodizacija po nedeljama](periodization-models.md) — to je bio najveći raskorak između
onoga što rad tvrdi i onoga što je kod radio.

## Ako te zanima šta je pošlo naopako

Svaka strana ima odeljak o ograničenjima i ispravkama posle revizije. Najzanimljivije:

- [Stimulativni volumen](stimulative-volume.md) — jedna skala nije bila dovoljna;
  razdvajanje pitanja o stimulusu od pitanja o zamoru došlo je tek pošto je prva verzija
  pokvarila automatski deload.
- [Više šablona](more-templates.md) — korisnička vežba istog naziva mogla je da spreči upis
  sistemske i time zaključa generisanje plana **svim ostalim** korisnicima.
- [Nivo iskustva](experience-level.md) — pravilo je radilo ispravno, ali nije imalo čime da
  popuni trening; ograničenje je zatvorila tek sledeća grana.

## Bezbednost

Pregled cele aplikacije, ispravke i ono što ostaje otvoreno:

- [`../security.md`](../security.md) — šta je zatvoreno i kako je provereno
- [`../deployment-security.md`](../deployment-security.md) — koraci pri isporuci (TLS,
  sertifikati, nadogradnja postojeće baze)

| Grana | O čemu je | PR |
|---|---|---|
| `fix/session-data-leak` | Keširani maksimumi prethodnog korisnika preživljavali su odjavu | #12 |
| `fix/account-security` | Lozinke, ograničenje zahteva, nabrajanje naloga, promena lozinke | #13 |
| `fix/deployment-hardening` | Zaglavlja, kontejneri bez root-a, nalog baze, zavisnosti | #14 |
