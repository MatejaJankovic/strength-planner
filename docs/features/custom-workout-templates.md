# Lični šabloni treninga

**Grana:** `feature/custom-workout-templates`

Do sada je izbor plana bio izbor jednog od sedam ugrađenih šablona. Sve ostalo je sistem
odlučivao sam: koje vežbe iz šablona zaista ulaze u trening (nivo iskustva ih skraćuje),
koliko serija (nivo iskustva, pa preraspodela ka MAV-u), koji opseg ponavljanja i RIR (cilj
i periodizacija), i koliko kilograma (procena 1RM).

Sada korisnik može da sastavi svoj šablon: dani, vežbe u svakom danu, i za svaku vežbu broj
serija i opseg ponavljanja.

## Šta ostaje automatsko, a šta ne

Ovo je bila glavna odluka i vredi je zapisati, jer se tiče onoga što rad tvrdi.

Lični šablon **ne isključuje auto-regulaciju**. Ono što korisnik upiše je propis **prve
nedelje**, a ne konačan broj:

| | Ugrađen šablon | Lični šablon |
|---|---|---|
| Koje vežbe ulaze u trening | bira nivo iskustva | **tačno one koje izabereš** |
| Serije (start) | nivo iskustva | **tvoj unos** |
| Opseg ponavljanja | cilj | **tvoj unos** |
| RIR | cilj | cilj |
| Kretanje kroz nedelje | periodizacija | periodizacija |
| Preraspodela serija ka MAV-u | da | da |
| Deload (planirani i automatski) | da | da |
| Opterećenje iz 1RM i progresija | da | da |

Razlog za takvu podelu: plan koji ništa ne prilagođava ne bi pokazivao nijedan algoritam iz
rada, a plan koji prepisuje sve što je korisnik uneo ne bi bio "lični". Ovako je tvoj unos
polazna tačka i **sidro**, a sistem se od nje odmiče onoliko koliko podaci traže.

## Periodizacija nije morala da se menja

Ovo je bilo prijatno iznenađenje. `Periodization.ForWeek` već prima **osnovu** kao
parametar:

```csharp
ForWeek(model, weekNumber, baseRepRangeMin, baseRepRangeMax, baseTargetRir, baseSets)
```

Za ugrađen šablon se u nju šalje opseg cilja i broj serija iz nivoa iskustva. Za vežbu iz
ličnog šablona se šalje ono što je korisnik uneo - i to je cela izmena. Isti raspored koji
pomera propis cilja pomera i njegove brojeve, jedan blok kasnije nema posebnu granu.

Posledica koju vredi videti na primeru, jer je pravilo koje deluje neočekivano dok se ne
napiše: **deload polovi tvoje serije, ne podrazumevane.** Vežba sa 6 serija u deload nedelji
nosi 3; ista vežba iz ugrađenog šablona na srednjem nivou nosi 2 (jer polazi od 4).

## Granice unosa nisu izmišljene

Serije 2-10, ponavljanja 3-12. Donje granice i gornja granica ponavljanja **preuzete su iz
`Periodization`**, ne odabrane iznova:

- ispod 2 serije vežba prestaje da se trenira (`Periodization.MinSets`),
- ispod 3 ponavljanja blok više nije hipertrofija (`MinReps`),
- iznad 12 ponavljanja Epley procena ne radi (`MaxReps = TrainingConstants.EpleyRepCap`), pa
  e1RM trend, rekordi i ocena umora ostaju bez podatka.

Da su granice unosa šire, propis nedelje bi vrednost tiho svukao nazad i korisnik bi uneo
12-16 a u planu video nešto drugo. Test `TheFormBoundsMatchWhatAWeekCanActuallyExpress`
zaključava tu vezu.

## Jedan ključ za dva izvora šablona

Zahtevi za mezociklus i blok dugoročnog plana nose `templateKey` kao string. Ugrađeni
zadržavaju svoje (`full-body`, `upper-lower`), lični dobijaju `custom:{guid}`. Ključ mora da
preživi u bazi, jer se blok dugoročnog plana generiše tek kada mu dođe red - ponekad mesecima
kasnije.

Ključ se razrešavao na **četiri** mesta (pravljenje mezociklusa, provera bloka, generisanje
bloka kad dođe na red, naziv bloka u pregledu), svako pozivom
`WorkoutTemplateCatalog.GetByKey`. Umesto da svako od njih nauči i za lične šablone, uveden
je `IWorkoutTemplateResolver` koji vraća isti oblik za oba izvora. Skraćivanje ugrađenog
šablona po nivou iskustva preselilo se tamo, pa generator sada ima jednu putanju umesto dve.

Test `NoBuiltInKeyCanBeMistakenForACustomOne` proverava da nijedan ugrađen ključ ne sadrži
dvotačku - inače bi isti string pokazivao na dva različita šablona.

## Šta je moralo da se popravi usput

**Automatski deload je prepisivao korisnikov opseg ponavljanja.** Kada umor povuče deload
ranije, planirani deload na kraju bloka se oslobađa i preuzima propis žrtvovane nedelje. Taj
propis se računa iz **osnove**, a osnova se do sada čitala sa cilja:

```csharp
goal?.RepRangeMin ?? plan.RepRangeMin
```

Dok su svi planovi u bloku delili opseg cilja, to je bilo tačno. Vežba iz ličnog šablona ima
svoju, pa bi ista računica tiho vratila 8-12 preko onoga što je korisnik uneo.

Za serije taj problem ne postoji jer `Periodization.BaseSetsFrom` obrće pomeraj. Za opseg
ponavljanja takav postupak **ne može da postoji**: `ForWeek` opseg i odseca na granice, pa
osnove 11-12 i 12-12 daju istu nedelju i iz nje se ne zna od koje se pošlo. To je i zapisano
kao test.

Zato `ExercisePlan` sada pamti `BaseRepRangeMin` i `BaseRepRangeMax`. Migracija ih popunjava
za zatečene planove iz cilja njihovog mezociklusa - 10.976 redova, 9.176 hipertrofija (8-12)
i 1.800 snaga (3-6), nijedan nije ostao na nuli.

## Brisanje šablona

Već generisan mezociklus ne zavisi od šablona: vežbe, serije i opsezi su prepisani u plan.
Blok dugoročnog plana koji **još čeka svoj red** zavisi, jer se generiše iz ključa tek tada.
Brisanje takvog šablona se zato odbija sa objašnjenjem, umesto da plan kasnije pukne.

## Provera

- `dotnet build`, `dotnet test` (282 testa), `npm run build`, `npm test` - sve prolazi.
- Sedam novih xUnit testova u `CustomTemplateTests`.
- **Devet novih testova komponente** (`custom-templates.spec.ts`). Editor radi nad
  ugnježdenim spiskom (dani, pa vežbe u danu), a takav spisak je lako pogrešno adresirati -
  prva verzija jeste. Testovi zato ne gledaju izgled nego adresiranje i pravila koja server
  posle odbija:
  - izmena i brisanje vežbe pogađaju dan koji je zaista izabran (regresija za grešku sa
    `$index` u ugnježdenom `@for`),
  - serije i ponavljanja ostaju unutar granica koje propis nedelje ume da izrazi,
  - opseg ostaje opseg kada se donja granica podigne preko gornje,
  - dan ne nudi vežbu koju već ima, i dva dana ne mogu da nose isti naziv,
  - telo zahteva nosi dane i vežbe onim redom kojim su unete.
- Tri migracije primenjene na lokalnu bazu; popunjavanje osnovnog opsega provereno upitom.
- API se podiže čist sa novim grafom zavisnosti.

### Prolaz kroz aplikaciju sa prijavljenim nalogom

Napravljen je lični šablon **"Moj Upper/Lower"** sa namerno neuobičajenim brojevima, da bi
se u planu videlo čiji su:

| Dan | Vežba | Uneto |
|---|---|---|
| Dan 1 | Bench Press | 6 serija, 5-8 |
| Dan 1 | Barbell Row | 4 serije, 6-10 |
| Dan 2 | Back Squat | 5 serija, 4-6 |

Od njega je napravljen mezociklus, cilj hipertrofija, **linearan** raspored, nalog na
naprednom nivou. Rezultat:

**Trening nosi tačno izabrane vežbe.** Dan 1 ima dve, Dan 2 jednu. Ugrađen šablon bi
naprednom vežbaču dao šest vežbi po danu - `SessionComposition` se za lični šablon ne
primenjuje, kako je i namera.

**Nedelja 3 je osnova i vraća unos neizmenjen:** Bench Press 6×5-8, Barbell Row 4×6-10,
Back Squat 5×4-6.

**Ostale nedelje su pomerene istim rasporedom kao propis cilja.** Za Bench Press:

| Nedelja | Faza | Serije | Ponavljanja | RIR |
|---|---|---|---|---|
| 1 | volumen (+3 pon., +1 serija) | 7 | 8-11 | 2 |
| 3 | osnova | 6 | 5-8 | 1 |
| 5 | intenzitet (-2 pon., -1 serija) | 5 | 3-6 | 1 |
| 6 | deload | **3** | **5-8** | 1 |

Dva reda u toj tabeli su ono zbog čega je pola ovog posla i urađeno:

- **deload polovi 6 na 3**, a ne podrazumevane 4 na 2 - dakle polazi od korisnikovog broja;
- **deload vraća 5-8**, a ne 8-12 iz cilja. Bez `BaseRepRangeMin/Max` upravo bi tu unos bio
  tiho prepisan.

**Preraspodela ka MAV-u radi i dalje**, i vidi se da sidro ostaje korisnikovo: Barbell Row
u nedelji 1 stoji na `propisano 5 / predloženo 6`, u nedelji 3 na `4 / 6`.

**Lični šablon je ponuđen i u dugoročnom planu** - izabran u bloku čarobnjaka, a pregled
plana ispisuje njegov naziv ("Moj Upper/Lower · Linearan, 6 ned."), što proverava i
razrešavanje naziva po ključu `custom:{guid}`.

## Poznata ograničenja

- **Ponavljanja iznad 12 nisu dozvoljena.** Za vežbe koje se tradicionalno rade u 15-20
  ponavljanja (list, trbušnjaci) to je stvarno ograničenje. Podizanje granice traži da e1RM
  prestane da bude jedina mera napretka, što je veći zahvat od ovog.
- **Redosled vežbi u danu se ne može menjati prevlačenjem**; menja se brisanjem i ponovnim
  dodavanjem. Redosled ima značenje - složene vežbe idu prve - pa je vredan kasnije dorade.
- **Lični šablon ne nosi upozorenje** kao dvodnevni ugrađeni ("ispod minimalnog volumena").
  Preraspodela ka MAV-u i dalje radi, pa se odstupanje vidi u analitici volumena, ali ne i
  pri izboru.
