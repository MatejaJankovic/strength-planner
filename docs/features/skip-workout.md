# Preskočen trening: nedelja koja ima rupu sme da se zatvori

Nalaz D20 iz pregleda logike treninga.

## Problem

Nedelja je bila gotova tek kada je **svaki** trening u njoj završen. To pitanje postavljaju
tri različita mesta:

| Gde | Šta je čekalo |
|---|---|
| `DeloadService` | ocena umora nedelje, a time i auto-deload |
| `VolumeLandmarkService` | učenje MEV/MAV/MRV granica iz te nedelje |
| `MacrocycleService` | generisanje **sledećeg bloka** dugoročnog plana |

Jedan trening koji vežbač nikada neće odraditi zato drži nedelju otvorenom zauvek — a ako je
to poslednji nedovršen trening bloka, plan tu stoji.

Izmereno nad razvojnom bazom: **devet nedelja** ima rupu, a kasnija nedelja istog bloka je
već odrađena. Nijedna od njih nema ocenu umora ni obračunat volumen. Šire uzev, 35 nedelja
ima i završenih i nezavršenih treninga, i nijedna od te 35 nije obračunata.

Zaobilaženje koje je uputstvo praktično nudilo — započni pa završi prazan trening — nije
bilo bezopasno: prazan završen trening pokreće balansiranje, koje ostatku nedelje dodaje
serije.

## Rešenje

`SessionStatus` dobija **`Skipped`**, a `SessionLifecycle` nosi pravilo koje sva tri upita
dele.

Spisak statusa koji nedelju zatvaraju stoji kao **skup**, a ne kao ispisan uslov, jer isto
pitanje postavlja i baza: `Contains` se prevodi, pa obe strane čitaju jedan spisak umesto da
ponavljaju logički izraz koji bi se razišao onog dana kada se doda nov status. Provereno u
generisanom SQL-u:

```sql
WHERE ... AND w."Status" NOT IN ('Completed', 'Skipped')
```

Preskakanje je **povratno i bez gubitka**: propis, opsezi i sve upisane serije ostaju
netaknuti, a „Vrati na plan" vraća trening među planirane. Preskočiti se sme samo trening
koji nije započet — onaj koji je u toku nosi upisane serije, pa bi preskakanje moralo da
odluči šta sa njima, a nijedan odgovor na to nije istinit o onome što se desilo.

**Vraćanje na plan ne poništava istoriju.** Deload ili sledeći blok koji su u međuvremenu
nastali ostaju: u tom trenutku zaista nije bilo šta da se čeka.

## Šta je namerno ostalo strože

Granice volumena i dalje traže da je **svaki** trening odrađen. Nedelja sa preskočenim danom
je uradila manje nego što je propisala, pa o tome koliko volumena vežbaču treba ne govori
ništa. Isto pravilo po kome ni signal snage bez uporedive nedelje ne pomera ništa: ćutanje
nije merenje.

Ocena umora se, nasuprot tome, računa — i to u bezbednom smeru. Nedelja sa manje rada daje
niži volumenski signal, pa pre ugasi deload nego što ga izmisli.

## Šta je izmereno

Uživo, dvoblokovni plan (Full Body 2 dana, 8 treninga u bloku), jedan trening završen i
sedam preskočenih:

| | Sa ispravkom | Sa starim pravilom |
|---|---|---|
| Blok 1 | `completed`, 1 odrađen + 7 preskočenih | 1 odrađen + 7 preskočenih |
| **Blok 2** | **generisan**, svoj mezociklus | **nema ga** (`mesocycleId: null`) |

Sa starim pravilom ni ponovno otvaranje ekrana „Plan" ne pomaže: samopopravka
(`EnsureCurrentBlockAsync`) postavlja isto pitanje, pa i ona vidi nedovršen blok.

Nedelja sa jednim preskočenim danom, ostala tri odrađena: dobila je ocenu umora (0.000 —
tri uredne serije, bez prethodne nedelje za poređenje), a `VolumeAdaptedAt` je ostao prazan.
Tačno kako je i zamišljeno: zatvorila se, ali nije učila.

Vraćanje pravila (skup zatvaranja nazad na samo `Completed`) obara **3 od 597** testova.

## Šta ovo povlači, a nije bilo traženo

**Preskakanje ne prebacuje serije, ali sledeći završen trening to uradi.** Izmereno u istoj
nedelji: preskakanje dana za noge nije pomerilo ništa (27/30/30/24 ostaje), ali kada je
posle toga završen prvi trening, balansiranje je drugi dan za noge podiglo sa **24 na 30**
serija. To nije novo pravilo — to je zatečeni alokator koji nedelju gađa u ciljni volumen, a
preskočen dan mu više nije odredište. Ograničeno je zatečenim klamovima (najviše 6 serija po
vežbi, lutanje ±2). Zapisano je i u uputstvu, da vežbača ne iznenadi.

## Šta je pregled uhvatio

Dve rupe koje je otvorila **ova** izmena, obe na nivou API-ja:

1. **Preskočena sesija je i dalje primala serije.** `EnsureSessionIsEditable` je odbijao
   samo završene. Serija upisana u preskočen trening bi bila rad koji nijedan obračun nije
   video — nedelja je već ocenjena, a možda je i sledeći blok generisan. Sada odbija svaku
   sesiju od koje ništa ne preostaje, sa porukom koja razdvaja dva slučaja.
2. **`start` na preskočenoj sesiji je vraćao 200 i nije menjao ništa.** `StartAsync` je
   proveravao samo „završena", a menjao status samo ako je „planirana" — preskočena je
   padala između. Sada je izričito odbijena: „predomislio sam se" ima svoje dugme.

Obe provereno uživo: `409` sa jasnom porukom.

## Poznato ograničenje

Stilski fajl ekrana za trening (`workout-session.scss`) je posle ove izmene na **7.96 kB**,
a tvrda granica je 8 kB (`anyComponentStyle` u `angular.json`). Bio je preko granice
upozorenja od 6 kB i pre ove grane, na 7.6 kB. Dugme za preskakanje zato nosi oblik postojeće
glavne akcije (`.cta`) sa tišim tonom, umesto svog bloka pravila — ali sledeća izmena tog
ekrana će morati da ga rastereti.

## Testovi

`SessionLifecycleTests` (16 tvrdnji): šta zatvara nedelju a šta ne, da svaki status pripada
tačno jednoj od dve grupe (test protiv zaborava kada se doda nov status), da se spisak koji
upiti šalju bazi poklapa sa predikatom, ko sme da se preskoči i ko da se vrati, i da nedelja
sa rupom zatvara blok ali ne uči granice.

Ukupno: 581 → 597 testova na serveru, 139 na klijentu (bez izmena).
