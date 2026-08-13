# Više šablona, i vežbe kojima se popunjavaju

**Grana:** `feature/more-templates`

Treća izmena izvedena iz priručnika (vidi [analizu](../analiza-prirucnika.md), stavka 8).
Zatvara ograničenje koje je [prethodna grana](experience-level.md) sama prijavila.

## Problem

Aplikacija je imala **tri** šablona — Full Body (3 dana), Upper/Lower (4 dana) i
Push/Pull/Legs (3 dana). Iz toga je sledilo četiri odvojena problema.

### 1. Naprednom vežbaču je trening bio krnj

Prethodna grana je uvela pravilo iz priručnika: napredni vežbač dobija **jednu složenu
vežbu po treningu**, ostalo izolacije. Ali postojeći šabloni su imali svega **dve
izolacije po danu**, pa mu je trening ispadao od tri vežbe. Pravilo je radilo ispravno —
nije imalo čime da popuni trening.

### 2. Katalog vežbi nije imao izolacije za grudi i leđa

Od 27 sistemskih vežbi, nijedna izolaciona nije pogađala grudi ni leđa. Za vežbača koji
sme jednu složenu vežbu po treningu to znači da se te dve grupe **nije imalo čime
dopuniti**. Šabloni nisu mogli da budu bolji od kataloga iz kojeg biraju.

### 3. Profil je tražio broj trenažnih dana koji se nigde nije koristio

Registracija pita „koliko dana nedeljno treniraš" (1–7), a ponuda je bila 3 ili 4 dana.
Korisnik sa dva ili šest dana nije imao šta da izabere, a raspored po danima u nedelji
(`GetSessionDate`) je poznavao samo trodnevni i četvorodnevni raspored — sve ostalo je
ređao u uzastopne dane bez ijednog dana odmora.

### 4. Čarobnjak je obećavao jedno, a generator davao drugo

Ekran za pravljenje plana je prikazivao **pun spisak** vežbi iz šablona, a generator ga je
skraćivao po nivou iskustva. Napredni vežbač je birao šablon sa šest vežbi po danu, a
dobijao tri.

## Rešenje

### Sedam šablona, za 2 do 6 dana nedeljno

| Ključ | Naziv | Dana |
|---|---|---|
| `full-body-2` | Full Body (2 dana) | 2 |
| `full-body` | Full Body | 3 |
| `push-pull-legs` | Push/Pull/Legs | 3 |
| `upper-lower` | Upper/Lower | 4 |
| `full-body-4` | Full Body (4 dana) | 4 |
| `upper-lower-ppl` | Upper/Lower + Push/Pull/Legs | 5 |
| `push-pull-legs-6` | Push/Pull/Legs ×2 | 6 |

Postojeća tri su zadržala svoje **ključeve**. Već generisani mezociklusi se ne diraju:
progresija i deload spajaju treninge po `DayLabel` unutar jednog mezociklusa, a nazivi dana
su nepromenjeni.

**Ali sadržaj tih šablona jeste promenjen**, i to se vidi na dugoročnim planovima. Blok koji
još nije generisan razrešava šablon tek kad dođe na red, pa dobija **novi** spisak vežbi.
Konkretno: `Upper A` je za srednji nivo bio 4 vežbe / 16 serija, sada je 6 vežbi / 24
serije. Ko usred dugoročnog plana pređe na ovu verziju, dobiće naredni blok obimniji nego
prethodni, a nove vežbe bez zabeleženog 1RM ulaze bez ciljne težine (isto stanje kao za
svakog novog korisnika — unosi se pri prvom treningu).

Alternativa bi bila da se sadržaj šablona zamrzne u bazi pri pravljenju plana, što je izmena
šeme neproporcionalna ovom radu — i trajno bi zaključala zatečene planove na šablone ispod
MEV, koje ova grana upravo ispravlja.

Svaki dan poštuje tri pravila, i sva tri su pokrivena testovima:

1. **Složene vežbe idu prve** — priručnik to i traži (*„Složene vežbe radiš pre izolacionih
   vežbi, jer su zamornije"*), a izbor po nivou iskustva se gradi upravo na tom redosledu.
2. **Dovoljno izolacija** — da naprednom vežbaču trening ne ostane krnj.
3. **Prva složena vežba dana se rotira** — napredni zadržava samo nju, pa bi šablon koji
   svaki dan otvara čučnjem njemu dao nedelju bez ijednog gornjeg pokreta.

Šabloni od pet i šest dana namerno nose **manje složenih vežbi i manje izolacija po
treningu**: pri većoj frekvenciji *„ključno je smanjiti volumen po treningu"*, jer se ista
mišićna grupa pogađa više puta nedeljno. Zato napredni vežbač na njima dobija pet vežbi po
treningu umesto šest — to je namerno, i test to razdvaja od greške.

### Šest novih izolacionih vežbi

`Cable Fly` i `Dumbbell Fly` (grudi), `Straight-Arm Pulldown` (leđa), `Rear Delt Fly`
(ramena), `Hammer Curl` (biceps), `Skull Crusher` (triceps).

Seeder ih ubacuje po nazivu pri startu — nema migracije jer nema izmene šeme.

### Raspored po danima u nedelji

Novi `TrainingWeekSchedule` (Domain) drži raspored za sve dužine nedelje:

| Dana | Raspored | Zašto |
|---|---|---|
| 2 | pon, čet | tri dana odmora — full body pogađa sve |
| 3 | pon, sre, pet | klasičan raspored |
| 4 | pon, uto, čet, pet | dva para, pauza u sredini nedelje |
| 5 | pon, uto, sre, pet, sub | jedan slobodan dan usred nedelje |
| 6 | pon–sub | šest treninga, nedelja slobodna |

Trodnevni i četvorodnevni raspored su **nepromenjeni** — test to izričito proverava, da
zatečeni planovi ne bi promenili datume.

### Predlog šablona iz profila

`/api/templates` sada vraća šablone **viđene očima korisnika**:

- dani su već skraćeni na njegov nivo iskustva, pa čarobnjak prikazuje tačno ono što će
  dobiti (problem 4);
- šablon koji odgovara broju njegovih trenažnih dana nosi oznaku `isSuggested` i unapred
  je izabran, uz značku „predlog za tebe".

Ako tačnog nema, bira se najduži koji **staje** u broj dana koje korisnik ima — bolje je
odraditi ceo kraći plan nego stalno preskakati treninge iz dužeg. Sedam dana dobija
šestodnevni, jedan dan dobija dvodnevni.

Keširanje šablona u frontendu je uklonjeno: odgovor sada zavisi od profila, pa bi posle
izmene nivoa iskustva ostao ustajao.

### Čarobnjak dugoročnog plana više ne drži svoj spisak

Ekran „Plan" je imao **zakucana tri šablona** u kodu. Sada ih učitava sa servera, pa se novi
šabloni pojavljuju i tamo, a blokovi se podrazumevano prave sa predloženim šablonom.

### Seeder više ne može da bude blokiran korisničkom vežbom

Ovo je našla revizija koda i jeste ozbiljno. `SeedExercisesAsync` je preskakao svaku seed
vežbu čiji naziv već postoji u tabeli — **uključujući korisničke vežbe**. Dok se spisak
seed vežbi nije menjao, to nije smetalo. Čim ova grana dodaje šest novih naziva u već
popunjenu bazu, otvara se ovakav scenario:

1. Korisnik A na ekranu Profil napravi svoju vežbu „Cable Fly" (danas prolazi — sistemske
   vežbe tog naziva još nema).
2. Ova verzija se pusti; seeder vidi „Cable Fly" u tabeli i **ne upisuje** sistemsku vežbu.
3. Korisniku B generisanje pada za **sve** šablone, jer se „Cable Fly" nalazi u svih sedam.

Ispravke:

- seeder poredi samo sa **sistemskim** vežbama, i to bez obzira na velika i mala slova;
- pošto sada obe vežbe mogu da postoje istovremeno, generator grupiše po nazivu i bira
  sistemsku — ranije bi `ToDictionary` pukao na dvostrukom ključu (greška 500);
- dodat je `ReconcileSystemExercisesAsync`: sistemska vežba sada prati katalog i po tipu,
  spravi i mišićima, ne samo po postojanju. Bez toga bi izmena u katalogu ostala samo u
  kodu — testovi bi je videli, a zatečena baza ne bi.

### Katalog vežbi se preselio u `Application`

Spisak sistemskih vežbi i granica volumena je živeo u `Infrastructure/Persistence/DbSeeder`.
Preselio se u `Application/Templates/ExerciseCatalog`, uz šablone koji ga referišu. Razlog
je konkretan: dok je bio u sloju baze, **nijedan test nije mogao da proveri** da li šabloni
pogađaju postojeće vežbe niti koliko volumena zaista propisuju. Seeder je sada samo upis.

## Provera

- `dotnet build`, `dotnet test` (**181 test**, bilo 141), `npm run build` — sve prolazi.
- **Nijedan šablon ne prelazi MRV** ni za jednu mišićnu grupu, ni na jednom nivou. To je
  najvažnija provera: plan koji već na startu stoji iznad MRV tera sistem u deload pre nego
  što je išta naučio o korisniku.
- Svaki šablon od tri dana naviše **dostiže MEV** za svaku grupu koju pogađa, mereno na
  srednjem nivou (referenca sistema).
- Svaki šablon pogađa svih osam velikih mišićnih grupa, na svakom nivou.
- End-to-end kroz pokrenutu aplikaciju, tri naloga sa različitim nivoom i brojem dana:

  | Nivo / dana | Predloženo | Vežbi po treningu |
  |---|---|---|
  | Početnik / 3 | Full Body | 5 |
  | Srednji / 4 | Upper/Lower | 6 |
  | Napredni / 6 | Push/Pull/Legs ×2 | 5 |

- End-to-end raspored: šestodnevni plan je legao na 17–22. 8. (nedelja slobodna),
  dvodnevni na 17. i 20. 8.
- Nedeljni volumen izračunat **iz stvarno generisanih planova** (preko API-ja, ne iz
  modela) za svih 7 šablona × 3 nivoa — nijedno prekoračenje MRV-a.
- U pregledaču: prijava, čarobnjak prikazuje 7 šablona, šestodnevni je označen kao predlog
  i unapred izabran, dvodnevni nosi upozorenje, plan se pravi u jednom kliku i daje
  4 nedelje × 6 treninga sa deload-om u četvrtoj. Ekran „Plan" nudi svih 7 šablona.
- Svih 7 šablona × 3 nivoa generisano **na praznoj bazi** (`sp_freshcheck`), da bi se
  potvrdilo da izmenjeni seeder radi i za novog korisnika, a ne samo kao dopuna postojeće
  baze: 33 vežbe, 10 mišićnih grupa, sve granice volumena sa MAV vrednošću.
- Scenario iz revizije odigran nad bazom: korisnička vežba „Cable Fly" napravljena pre
  nadogradnje **više ne sprečava** upis sistemske, i generisanje prolazi i za njenog
  vlasnika i za ostale korisnike.

## Poznata ograničenja

Sva tri su zabeležena u kodu (testovi u `WorkoutTemplateCatalogTests`), da ne bi tiho
nestala ili se pogoršala.

### Dvodnevni šablon je ispod MEV — i to piše u aplikaciji

Dva treninga nedeljno ne mogu da dostignu MEV za većinu grupa; nema dovoljno mesta u
nedelji. Šablon ostaje jer je bolji od nijednog, ali nosi vidljivo upozorenje u
čarobnjaku umesto da se pravi da je pun plan.

### Napredni vežbač ne može da dostigne sopstveni MEV ispod šest dana

Ovo **nije stvar šablona**. Naprednom nivou sistem daje 3 serije po vežbi i 6 vežbi po
treningu, a njegove granice volumena množi sa 1.2. Zbir skaliranih MEV vrednosti tada
premašuje ono što nedelja uopšte može da isporuči na manje od šest treninga — nijedan
šablon to ne može da popravi. Ograničenje je u konstantama nivoa, koje ova grana namerno
ne dira.

### Pravilo za složene vežbe je po treningu, ne po nedelji

Priručnik naprednom daje *„do 3 složene vežbe nedeljno"*, ali sistem primenjuje granicu
**po treningu**. Na šest treninga nedeljno to ispadne šest složenih vežbi. Napredni vežbač
koji hoće da ostane u okviru iz priručnika treba da bira šablon do tri dana nedeljno.

### Dugoročni plan u toku menja oblik narednog bloka

Opisano gore, uz šablone: blok koji još nije generisan koristi novi spisak vežbi. Nema
ispravke u kodu — zamrzavanje sadržaja šablona u bazi bi bilo neproporcionalno i
kontraproduktivno.

### Listovi i trbuh izostaju iz trodnevnog full body šablona

U trodnevnom rasporedu nema mesta za njih a da veće grupe ne padnu ispod MEV. Trodnevni
Push/Pull/Legs ih pokriva; trodnevni full body ne.
