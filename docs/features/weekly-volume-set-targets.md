# Predlog serija koji cilja nedeljni volumen

**Grana:** `feature/weekly-volume-set-targets`

## Problem: MAV je postojao, ali plan ga nije koristio

Aplikacija je već znala gde bi nedeljni volumen svakog mišića **trebalo** da padne. MAV je
uveden u [`stimulative-volume`](stimulative-volume.md) kao ciljna vrednost, prikazuje se u
analitici, i od [`adaptive-volume-landmarks`](adaptive-volume-landmarks.md) se uči iz
korisnikovih odgovora na volumen. Do plana nije stizao nikada.

Broj serija je dolazio iz jednog jedinog mesta — `ExperienceProgramming.StartingSetsPerExercise`,
koji vraća tri, četiri ili tri po nivou iskustva — a periodizacija ga je pomerala za jednu
kroz blok. **Ista brojka za svaku vežbu**, bez obzira na to koji mišić trenira i koliko
puta ga nedelja uopšte pogađa. Nedeljni volumen po mišiću je zato ispadao onako kako se
šablon slučajno sabere.

Koliko slučajno, pokazuje trodnevni Push/Pull/Legs na četiri serije po vežbi (vrednosti su
stimulativne nedeljne serije, cilj je MAV):

| Mišić | Pre | MAV |
|---|---|---|
| Ramena | 14 | 16 |
| Grudi | 12 | 16 |
| Leđa | 12 | 18 |
| Kvadricepsi | 8 | 14 |
| Biceps | 8 | 14 |
| Triceps | 8 | 12 |
| Zadnjica | 4 | 10 |

Sedam mišićnih grupa, sedam promašaja, i nijedan od njih korisnik nije imao kako da
ispravi — broj serija se nigde u aplikaciji nije ni prikazivao. `targetSets` je putovao
kroz DTO do Angulara i tamo se nije koristio ni na jednom ekranu.

Druga polovina problema je bila da nedelja nije imala pamćenje. Ako se u ponedeljak odradi
dve od šest serija potiska, četvrtak je i dalje nudio isti plan kao da se ništa nije
desilo.

## Rešenje

### 1. Raspodela serija po nedelji

Nov domenski algoritam `WeeklySetAllocation` bira broj serija za svaku vežbu nedelje tako
da zbir po mišiću padne što bliže MAV-u. Traži se lokalnim pretraživanjem: u svakom koraku
se uzima jedno pomeranje za ± jednu seriju koje najviše smanjuje cenu nedelje, dok takvog
pomeranja ima.

Cena je zbir po mišićima od tri člana:

| Član | Zašto |
|---|---|
| `\|volumen − MAV\|` | ono što se zapravo cilja |
| `4 × (volumen iznad MRV)` | promašen cilj košta napredak, probijen MRV košta naredne nedelje — zato četiri puta teže |
| `0.1 × (odstupanje od propisa)²` | ispravak se razliva po nedelji umesto da se sruči na prvi dan |

Propis nivoa iskustva ostaje **sidro, ne predlog koji se prepisuje**: vežba sme da se
pomeri najviše za dve serije od onoga što joj periodizacija propisuje, i nikada izvan
2–6 serija. Priručnik je izričit da nivo, a ne aritmetika, određuje oblik treninga
(*„napredni vežbač bi pregoreo od treninga početnika"*), pa bez te granice bi jedna velika
mišićna grupa ispod svog MAV-a nabijala serije početniku dok trening ne prestane da bude
početnički.

Dve stvari ispadaju same od sebe iz toga što se cena meri **po mišiću, a ne po vežbi**, i
obe su ono što bi trener i uradio:

- **Volumen se dodaje tamo gde je najjeftiniji.** Serija raspona diže samo grudi; serija
  potiska diže i ramena i triceps sa sobom. Kada su ramena i triceps već na cilju,
  algoritam sam bira izolaciju i ne dira složenu vežbu.
- **Nedostižan cilj se prilazi, a ne juri.** Dvodnevni full body nema gde da smesti volumen
  za većinu mišića; algoritam ode dokle granice dozvoljavaju i tu stane.

### 2. Prilagođavanje unutar nedelje

Isti poziv radi oba posla. Pri generisanju bloka svaka nedelja se rasporedi od nule; pri
**završetku treninga** se nedelje kojima je ostao makar jedan nedirnut trening rasporede
ponovo — ovaj put protiv onoga što je nedelja stvarno upisala.

Ključno svojstvo: raspodela **uvek kreće od propisa, nikada od svog prethodnog odgovora**.
Zato ponavljanje posle svakog treninga konvergira umesto da se nagomilava.

Odrađeni deo nedelje ulazi u račun **dvema merama**, kao i drugde u sistemu:

- koliko stimulusa još nedostaje do cilja — meri se **stimulativnim** serijama, jer serija
  zaustavljena pet ponavljanja pre otkaza nije zaradila mesto u volumenu;
- koliko je oporavka potrošeno — meri se **sirovim** serijama, jer oporavak troši svaka
  odrađena serija.

Bez druge mere bi nedelja od deset lakih serija delovala kao odmor i sistem bi na već
iscrpljenu nedelju dosuo još serija. Test
`Allocate_DoesNotRewardAWeekOfSetsFarFromFailureWithMoreSets` drži baš taj slučaj.

Menjaju se samo treninzi koji su još `Planned` **i nemaju nijednu upisanu seriju**. Trening
u koji je neko počeo da upisuje jeste trening u toku, šta god mu status govorio, i predlog
mu ne sme da se pomeri pod rukama. To ujedno drži i račun poštenim: njegove serije se broje
jednom, kao odrađen volumen, a nikada drugi put kao planiran.

Deload nedelje se preskaču u celosti — prepolovljene serije su ceo smisao te nedelje.

### 3. Šta korisnik vidi

- Svaka vežba u treningu dobija čip **„Serije 2 / 6"** — prvi put da se predlog uopšte
  prikazuje. Zeleno kada je ispunjen, crveno tek kada je trening zatvoren ispod njega: dok
  trening traje, manjak serija je stanje, a ne greška.
- Kada predlog odstupa od propisa, ispod čipova stoji rečenica zašto: *„Predlog serija je
  podignut sa 4 na 6, da bi nedelja ostala u ciljnoj zoni volumena."*
- Posle treninga se izlistaju predlozi koje je taj trening pomerio, sa mišićem koji izmenu
  objašnjava: *„Day C · Dumbbell Fly · 5 → 6 · Chest"*.

Prijavljuje se samo tekuća nedelja. Raspodela dodiruje i one koje tek dolaze (granice
volumena su se možda pomerile baš tim treningom), ali posledica koju korisnik može da vidi
na svom planu je ono što mu preostaje u ovoj nedelji.

### 4. Nova kolona

`ExercisePlan.PrescribedSets` pamti šta nivo iskustva i periodizacija propisuju, odvojeno
od `TargetSets` koji je predlog. Razlika između njih je tačno ono što je balansiranje
pomerilo — i sidro oko kojeg se pomera.

Migracija `AddPrescribedSets` puni zatečene redove iz `TargetSets`. Nula, koju kolona
dobija podrazumevano, ovde nije prazna nego **pogrešna** vrednost: prozor pomeranja se
računa oko propisa, pa bi propis nula svaki zatečen plan spustio na dve serije pri prvom
preračunu.

Deload je morao da prati: `DeloadService` je polazni broj serija bloka izvodio obrtanjem
periodizacije nad `TargetSets`. Sada je to pomerena vrednost, pa obrtanje čita
`PrescribedSets` — inače bi rasterećenje bilo izvedeno iz broja koji periodizacija nikada
nije propisala.

## Provera

`dotnet build`, `dotnet test` (276 testova, 17 novih), `npm run build` — sve prolazi.
Aplikacija je pokrenuta i prođena kroz pregledač.

**Raspodela pri generisanju**, isti PPL šablon kao u tabeli gore:

| Mišić | Pre | Posle | MAV |
|---|---|---|---|
| Trbušnjaci | 8 | **12** | 12 |
| Grudi | 12 | **16** | 16 |
| Zadnja loža | 10 | **11** | 11 |
| Ramena | 14 | **16** | 16 |
| Leđa | 12 | 16 | 18 |
| Biceps | 8 | 12 | 14 |
| Kvadricepsi | 8 | 12 | 14 |
| Listovi | 8 | 12 | 13 |
| Triceps | 8 | 11.5 | 12 |
| Zadnjica | 4 | 5 | 10 |

Četiri grupe tačno na MAV-u, ostale prišle, nijedna iznad MRV-a.

**Zadnjica ostaje daleko od cilja i to je tačno ponašanje.** PPL je nudi samo kroz čučanj i
rumunsko mrtvo, oba sa doprinosom 0.5. Dizanje mrtvog bi zadnjicu popravilo za jednu
seriju, ali bi zadnju ložu — koja stoji tačno na svom MAV-u — povuklo dve preko. Algoritam
je odbio tu razmenu. Ograničenje je u šablonu, ne u raspodeli.

**Prilagođavanje unutar nedelje**, full body, kroz pregledač: u Day A upisane dve serije
potiska od predloženih šest, ostalo propušteno. Po završetku treninga:

```
Day B  Overhead Press   4 → 6  (Triceps)
Day B  Deadlift         4 → 6  (Back)
Day B  Leg Extension    4 → 5  (Quads)
Day B  Rear Delt Fly    3 → 4  (Shoulders)
Day C  Front Squat      4 → 6  (Glutes)
Day C  Dumbbell Fly     5 → 6  (Chest)
Day C  Face Pull        4 → 5  (Back)
Day C  Leg Curl         4 → 6  (Hamstrings)
```

Zatim obrnut smer: u Day B upisane po **dve serije preko** predloga na svakoj vežbi.

```
Day C  Front Squat      6 → 3  (Quads)
Day C  Face Pull        5 → 2  (Shoulders)
Day C  Hammer Curl      5 → 3  (Biceps)
```

## Šta je provera pokvarila

**Balansiranje je poništavalo automatski deload koji je nastao u istom zahtevu.** Ovo je
bila najozbiljnija greška u grani i našla ju je tek revizija koda.

`DeloadService` pretvaranje nedelje u deload ostavlja u change trackeru — `nextWeek.IsDeload
= true` nije upisano dok ne dođe `SaveChanges`. Raspodela je pozivana **pre** tog upisa, a
ona bazu pita koje su nedelje deload. Baza je i dalje govorila `false`, pa je sveže
rasterećena nedelja uzimana kao obična i njene prepolovljene serije su vraćane ka MAV-u.

Izmereno na pokrenutoj aplikaciji, nedelja 1 odrađena sa svim serijama do otkaza (ocena
umora 0.70), nedelja 2 pretvorena u deload:

```
Back Squat             propis=2  predlog=4
Bench Press            propis=2  predlog=4
Leg Curl               propis=2  predlog=4
Straight-Arm Pulldown  propis=2  predlog=4
Cable Fly              propis=2  predlog=4
Lateral Raise          propis=2  predlog=3
```

Propis je bio tačan, oznaka deload-a je bila tačna — samo je predlog, jedino što korisnik
zaista vidi, bio vraćen na pun trenažni volumen. Korisnik bi dobio „deload" nedelju sa
istim brojem serija kao svaka druga, i to baš kada je sistem procenio da mu treba
rasterećenje.

Ispravka je premeštanje raspodele iza `SaveChanges`. Isti prozor je krio i suprotan slučaj:
nedelja koju `RestorePlannedDeloadAsync` vraća u trenažnu (`IsDeload = false`, takođe
neupisano) bila je preskočena u tom prolazu. Redosled je sada objašnjen komentarom na licu
mesta, jer se iz koda ne vidi da je nosiv.

Posle ispravke, ista nedelja: `propis=2, predlog=2` na svih šest vežbi, dok nedelja 3
ostaje izbalansirana (3-5-3-4-5-2).

**Prvo objašnjenje izmene bilo je pogrešno postavljeno.** Mišić koji stoji uz izmenu tražen
je u stanju **pre** raspodele: „koji mišić je najdalje od cilja". Za Hammer Curl je izlazio
prazan.

Razlog je bio stvaran, a ne kozmetički. Biceps je na početku stajao tačno na svom MAV-u.
Zgib je porastao zbog **leđa** i time povukao biceps preko cilja, pa je Hammer Curl morao
dole. Pritisak koji je izmenu izazvao u polaznom stanju **još nije postojao**, pa ga tamo
nije ni bilo moguće naći.

Objašnjenje sada polazi od konačne raspodele sa tom jednom vežbom vraćenom na propis —
dakle od pitanja *„šta bi ovom mišiću bilo da se ova vežba nije pomerila"*. Isti scenario
ponovljen posle ispravke vraća `Biceps`.

**Pohlepna pretraga je bez trećeg člana cene gomilala ispravak na početak nedelje.** Četiri
vežbe za grudi po tri serije, cilj šesnaest: pretraga je uzimala prvo poboljšanje na koje
naiđe i davala 5-5-3-3 umesto 4-4-4-4. Isti zbir, ali ceo ispravak na prvom danu i
poslednji dan ispod. Kazna za odstupanje od propisa raste sa kvadratom, pa druga serija na
istoj vežbi košta tri puta više od prve na sledećoj — i raspodela se izravna. Kazna je
namerno premala da nadglasa cilj: najviše što naplati po koraku je 0.3, a najmanje
poboljšanje koje ijedno pomeranje može da donese je 0.5.

## Poznata ograničenja

- **Prilagođavanje ne može da nadoknadi ono za šta u nedelji nema mesta.** Kod PPL-a se
  svaki mišić trenira jednom nedeljno, pa propuštene serije potiska nemaju gde da se vrate
  — jedini preostali prostor je u narednoj nedelji, a nju raspodela dodiruje tek kad na nju
  dođe red. Kod full body i upper/lower šablona, gde se mišić ponavlja, nadoknada radi u
  punom obimu.
- **Predlog se ne prilagođava dok trening traje**, nego pri njegovom završetku. To je bio
  svestan izbor: plan ne sme da se menja pod rukama nekome ko je usred serije.
- **Analitika i dalje prikazuje samo odrađen volumen**, ne i planiran. Ekran volumena zato
  ne pokazuje da plan cilja zonu — to se vidi samo na ekranu treninga.
- **`workout-session.scss` prelazi budžet od 6 kB** i posle ovog dodatka stoji na 7.59 kB.
  Prelazio ga je i pre (7.02 kB); granica na kojoj build pada je 8 kB, pa je dodatak
  namerno skraćen da ostane ispod nje. Datoteku treba razdvojiti pre sledećeg dodavanja.
- Pri proveri je nađena **postojeća greška u rasporedu**, nevezana za ovu granu: donji deo
  dugmeta „Završi trening" stoji ispod fiksne donje navigacije, pa klik na njegovu sredinu
  vodi na `/plan` umesto da završi trening. `main` rezerviše 112 px, a navigacija zauzima
  145 px. Prijavljeno zasebno.
