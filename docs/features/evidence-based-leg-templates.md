# Šest novih vežbi, dva nova šablona, i zašto ne bro split

Katalog je do sada imao 32 vežbe i 7 šablona, i nijedna vežba za noge nije tražila bučice
ili telesnu težinu — čučanj, RDL, mrtvo dizanje, hip thrust, leg press/curl/extension su
sve bile šipka ili mašina. Ova runda pokriva to, dodaje dva šablona koja to koriste, i
menja dve postojeće vežbe u dva postojeća šablona.

## Kako je istraživanje urađeno

Tri nezavisna istraživanja (frekvencija/volumen, izbor vežbi, struktura podele), pa jedna
sinteza koja ih spaja u jedan predlog — sve preko weba, ne iz sećanja. Pet najbitnijih
citata je ručno provereno (WebSearch, ne samo poverenje modelu): **Vera-Cartagena i sar.
(2026, Applied Sciences 16(4):1940)**, **RP Strength "11-set rule"**, **Neto i sar. (2020,
J Sports Sci Med 19(1):195-203)**, **Schoenfeld/Grgic/Krieger (2019, J Sports Sci
37(11):1286-1295)** i **Mo i sar. (2023, Frontiers in Physiology 14:1264604)** — svih pet
postoji i tačno je preneto (brojevi iz Mo i sar. se poklapaju do decimale). Preostalih
~15 citata (Collins 2021, Kassiano 2025, Sugimoto 2026, Baz-Valle 2022, Damas 2019, i
ostali) **nisu pojedinačno provereni** — korisnik je odlučio da je to prihvatljivo za ovaj
obim rada, uz jasnu napomenu ovde da nisu.

## Šest novih vežbi

| Vežba | Oprema | Mišići | Citat |
|---|---|---|---|
| Bulgarian Split Squat | Bučica | Quads 1.0, Glutes 0.5, Hamstrings 0.5 | Vera-Cartagena 2026 ✓, Neto 2020 ✓, Mackey & Riemann 2021 |
| Split Squat | Telesna težina | Quads 1.0, Glutes 0.5 | Vera-Cartagena 2026 ✓ — jedina vežba u katalogu bez ikakve opreme |
| Walking Lunge | Bučica | Quads 1.0, Glutes 0.5, Hamstrings 0.5 | Vera-Cartagena 2026 ✓, Jeong & Kim 2025 |
| Goblet Squat | Bučica | Quads 1.0, Glutes 0.5 | Collins i sar. (2021, J Strength Cond Res 35(10):2661-2668) |
| Step-Up | Bučica | Glutes 1.0, Quads 0.5, Hamstrings 0.5 | Neto 2020 ✓ (najviši tier aktivacije gluteusa) |
| Single-Leg Romanian Deadlift | Bučica | Hamstrings 1.0, Glutes 0.5 | Mo i sar. 2023 ✓ — jedina unilateralna zgibna vežba u katalogu |

✓ = ručno provereno kroz web pretragu, brojevi i nalazi se poklapaju sa izvorom.

Četiri od šest ulaze u šablone ispod (Bulgarian Split Squat, Single-Leg Romanian Deadlift,
Step-Up kao nove, Goblet Squat kroz izmenu postojećeg). **Walking Lunge i Split Squat
namerno nisu ni u jednom ugrađenom šablonu** — u katalogu su radi ličnih šablona i
direktnog logovanja, prvenstveno zato što je Split Squat jedina vežba u celom katalogu bez
ikakve opreme i taj slučaj zaslužuje mesto u katalogu i bez svog šablona.

Doprinosi drže postojeću konvenciju kataloga (tačno jedan primarni mišić po vežbi, ostali
sekundarni na 0.5) — ovo nije nešto što `ExerciseService`-ova validacija nameće (samo
proverava da je svaka pojedinačna vrednost 1.0 ili 0.5, ne koliko ih ima), pa je novi test
[`ExerciseCatalogTests.EveryExercise_HasExactlyOnePrimaryMuscle`](../../tests/StrengthPlanner.Tests/ExerciseCatalogTests.cs)
dodat da tu konvenciju čuva ubuduće — istraživanje je prvobitno predložilo dva primarna
mišića za dve vežbe, i taj test bi to uhvatio.

## Bro split je namerno izostavljen

Klasična podela (jedna mišićna grupa po treningu, ~1x nedeljno) je eksplicitno razmotrena
i **odbačena**, ne zato što je nauka zabranjuje — Schoenfeld/Grgic/Krieger (2019) i dve
studije sa izjednačenim volumenom (Brigatto 2019; Gomes 2018) nisu našle razliku u rastu
mišića između 1x i 2x nedeljno kad je nedeljni broj serija isti — nego zato što se ne
uklapa u ono što ova aplikacija već radi:

1. **`WeeklySetAllocation`** (runda 5) preraspoređuje preostale serije mišićne grupe na
   preostale treninge nedelje kad jedan trening promaši cilj. Mišić treniran jednom
   nedeljno nema nijedan preostali trening za to — auto-regulacija, centralna teza rada,
   na bro splitu nema šta da radi.
2. **Brojevi iz samog koda**: `Back` ima najveći MAV od svih grupa (`ExerciseCatalog.
   VolumeLandmarks`: Mev=10, Mav=18, Mrv=25), a `ExperienceProgramming.LandmarkScale`
   množi sve granice sa 1.2 za napredne — nedeljni cilj za leđa je već ~22 serije na 2x
   nedeljno (~11 po treningu). Sve to u jedan trening bi prešlo granicu gde praktičarska
   literatura (RP Strength, nije peer-reviewed) upozorava da kvalitet serije opada.

## Dva nova šablona

**Upper/Lower x3 (6 dana)** — jedini šablon koji dostiže 3x nedeljno po mišiću; svi
postojeći staju na 2x. **Legs Specialization (5 dana)** — blok specijalizacije, noge tri
puta nedeljno, gornje telo dva, sa napomenom da nema istraživanja o optimalnoj dužini
ovakvog bloka.

### Prvi pokušaj je probio MRV — merenje, ne pretpostavka

Sinteza istraživanja je prvobitno predložila dane sa 2 složene + 4 izolacione vežbe po
danu, kopirajući gustinu koju svaki POSTOJEĆI šablon već koristi. Test
`EveryTemplate_StaysUnderMrvForEveryMuscle` je to odmah uhvatio:

```
Intermediate upper-lower-x3/Quads: 26.0 serija prelazi MRV 20.
Intermediate upper-lower-x3/Hamstrings: 24.0 serija prelazi MRV 16.
Intermediate legs-specialization/Quads: 22.0 serija prelazi MRV 20.
Intermediate legs-specialization/Hamstrings: 28.0 serija prelazi MRV 16.
```

(i slično za Beginner). Uzrok: svaki POSTOJEĆI šablon u katalogu drži frekvenciju nogu na
2x nedeljno bez izuzetka — čak i petodnevni Upper/Lower+PPL hibrid. Ista gustina vežbi po
danu, primenjena na 3x nedeljno, linearno gura nedeljni volumen 1.5x uvis, i probija plafon
koji su postojeći šabloni očigledno već približavali.

Popravka: `Leg Curl` i `Leg Extension` (jedine dve izolacije za noge u katalogu) sada se
javljaju samo **jednom** kroz tri dana za noge umesto na sva tri — treći dan dobija
neutralnu vežbu (`Face Pull` / `Straight-Arm Pulldown`) koja ne dodaje kvadricepsima ni
zadnjoj loži. Redosled unutar dana je takođe nameran: prva složena vežba svakog dana je,
gde god je moguće, jedna od tri nove — napredni vežbač po pravilu (`MaxCompoundsPerSession
= 1`) vidi samo prvu složenu vežbu dana, pa raspored otvarača određuje da li uopšte sretne
novu vežbu.

Posle popravke, svi nivoi prolaze sa margином (Intermediate primer, MRV u zagradi):
Quads 18.0 (20), Hamstrings 14.0-16.0 (16).

## Izmene dva postojeća šablona

Odobreno pre implementacije, sa jasnim naznakom da su ovo jedina dva mesta gde je
postojeći šablon dirnut:

- **Push/Pull/Legs** — `Leg Press` → `Goblet Squat` u danu "Legs" (isti vektor mišića,
  Quads 1.0/Glutes 0.5, bez rizika po testove ili nedeljni volumen).
- **Upper/Lower** — `Leg Press` → `Bulgarian Split Squat` u danu "Lower A" (nije
  neutralno — dodaje Hamstrings 0.5 koji Leg Press nema; traži klupu i ravnotežu, gore za
  apsolutnog početnika).

Obe izmene su na **istoj poziciji** (treća, poslednja složena vežba dana) kao vežba koju
menjaju, pa se ponašanje `SessionComposition`-a ne menja: isti nivo iskustva i dalje bira
istu vežbu na tom mestu ili je izostavlja, samo pod novim imenom.

## Provereno u živoj aplikaciji

Kroz pravi API poziv i registrovan test nalog (Intermediate):

- Svih 6 novih vežbi vidljivo na `/exercises` sa tačnom opremom i mišićima.
- Oba nova šablona vidljiva u izboru šablona pri kreiranju plana, sa tačnim danima i
  upozorenjima.
- Makrociklus generisan sa `Legs Specialization` šablonom, obrnuta periodizacija: 5 dana
  × 6 nedelja = 30 treninga, nedelja 6 tačno obeležena kao DELOAD.
- Otvoren prvi trening (`Legs A`): svih 6 vežbi prisutno, MAV alokator (runda 5) je
  ispravno podigao broj serija za Calf Raise (3→5) i spustio za Plank/Cable Crunch (3→2)
  "da bi nedelja ostala u ciljnoj zoni volumena" — potvrda da cela cev (šablon → generisanje
  → MAV rebalans) radi sa novim vežbama.
- Push/Pull/Legs i Upper/Lower izmene potvrđene: na istoj (trećoj) poziciji, isto
  ponašanje kao pre izmene — Intermediate izostavlja treću složenu vežbu (pre: Leg Press,
  sad: Goblet Squat / Bulgarian Split Squat), Beginner bi je video.

Testovi: `dotnet test` 356 prolazi (bilo 350; +6 novih u
[`ExerciseCatalogTests.cs`](../../tests/StrengthPlanner.Tests/ExerciseCatalogTests.cs),
jedan preimenovan u `WorkoutTemplateCatalogTests.cs` da dozvoli upozorenje na više od
jednog šablona). Nijedan postojeći test nije izmenjen da bi prošao — svi su prošli tako
što je sadržaj šablona popravljen, ne provera.
