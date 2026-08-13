# Šta priručnik traži, a aplikacija (još) ne radi

Analiza *Džepnog priručnika o programiranju treninga* (Dušan Petrović, Igor Maljik)
naspram trenutnog stanja StrengthPlanner-a, uključujući pet upravo isporučenih grana.

Ovo su predlozi za dalji rad — nije implementirano. Poređano po odnosu vrednosti i cene.

---

## Prvo: šta priručnik potvrđuje

Pre spiska nedostataka, vredi zabeležiti gde se priručnik i postojeći model **slažu**,
jer je to materijal za odbranu:

| Priručnik | Aplikacija |
|---|---|
| *"jedno ponavljanje je ekvivalentno povećanju opterećenja za otprilike 2-3%"* | `RpeCorrectionPerPoint = 0.03` — korekcija od 3% po RIR poenu je tačno to |
| *"Progressive overload nije: dodavanje serija"* | Progresija menja opterećenje i ponavljanja, nikad broj serija |
| *"Pravi volumen za tebe ćeš odrediti testiranjem i prilagođavanjem tokom dužeg perioda"* | Adaptivne MEV/MRV granice rade upravo to |
| *"Planiraj [deload] na vreme kako bi izbegao negativne efekte nakupljenog zamora"* | Auto-deload iz ocene umora, umesto čekanja četvrte nedelje |
| *"Mesečna [periodizacija]: Dugoročan plan kroz blokove treninga"* | Makrociklusi |
| *"Otkaz se javlja kada ne možeš da uradiš više nijedno ponavljanje sa pravilnom tehnikom"* | Zastavica otkaza na seriji |

Nezavisna literatura potvrđuje i prag blizine otkaza: serije više od ~4-5 ponavljanja od
otkaza znatno slabije stimulišu rast, dok je pojas RIR 0-3 „sweet spot".

---

## 1. Ne broji se svaka serija — a aplikacija broji

**Priručnik, dva mesta:**

> *"Svaka serija treba da bude urađena sa minimalno RIR 4, jer veći RIR neće omogućiti
> dovoljno stimulativnih ponavljanja."*

> *"Jedini volumen koji se računa je volumen koji sadrži mehaničku tenziju, odnosno serije
> koje su odrađene do ili blizu otkaza."*

**Šta aplikacija radi sada:** `VolumeService` sabira doprinose svih serija jednako.
Serija sa RIR 5 ulazi u nedeljni volumen isto kao serija sa RIR 1. Korisnik koji odradi
20 laganih serija vidi „iznad MRV" i dobija poruku da smanji volumen — iako po priručniku
nije uradio **ništa** stimulativno.

**Predlog:** doprinos serije skalirati blizinom otkaza, u domenu, uz test:

```
RIR 0-3  → doprinos 1.0   (stimulativno)
RIR 4    → doprinos 0.5   (granično)
RIR 5+   → doprinos 0.0   (nije volumen, samo zamor)
```

**Zašto je ovo najvrednija izmena:** ista skala odmah popravlja **tri** stvari koje su
već u kodu — nedeljni volumen u analitici, učenje MEV/MRV granica, i signal „volumen
naspram MRV" u oceni umora. Sve tri sada veruju broju serija umesto stimulusu.

Cena: mala. Jedna čista domenska funkcija i njeno provlačenje kroz `VolumeService`,
`VolumeLandmarkService` i `DeloadService`.

---

## 2. Nedostaje MAV — srednja granica

**Priručnik navodi tri granice, ne dve:**

> *"Maksimalni adaptivni volumen (MAV) — Optimalan volumen treninga pri kojem se telo
> adekvatno oporavlja i stimuliše hipertrofiju... obično se kreće u rangu 8-20 serija
> nedeljno po mišićnoj partiji, odnosno 4-8 serija po treningu."*

**Šta aplikacija radi sada:** zna samo MEV i MRV, pa je „optimalno" sve između — za grudi
raspon od 10 do 22 serije. To nije cilj, to je odsustvo cilja.

**Predlog:** dodati MAV kao ciljnu vrednost. Ekran volumena tada ne kaže samo „nisi ispod
i nisi iznad" nego „cilj ti je ~14 serija, na 9 si". Adaptivni mehanizam koji već uči MEV
i MRV može da uči i MAV po istom pravilu.

---

## 3. Nedelje su ravne — a to nije periodizacija

**Priručnik daje konkretnu shemu linearne periodizacije:**

> *Nedelja 1: 3x10 RIR2-3 · Nedelja 2: 4x8 RIR1-2 · Nedelja 3: 4x6 RIR1 ·
> Nedelja 4: 5x5 RIR0-1 · Nedelja 5: 5xMAX RIR0 · Nedelja 6: DELOAD*

I inverznu, koja ide obrnutim smerom.

**Šta aplikacija radi sada:** `MesocycleGenerator` upisuje **isti** broj serija, isti
rep-opseg i isti ciljni RIR u sve četiri nedelje. Menja se samo opterećenje, i to kroz
auto-regulaciju. To je progresivno opterećenje — ali nije periodizacija, iako rad tako
naziva model.

**Predlog:** `TrainingWeek` dobija propisane parametre (serije, rep-opseg, ciljni RIR)
umesto da ih nasleđuje od cilja. Blok makrociklusa bira model: *ravan* (današnje
ponašanje, zadržati kao podrazumevano), *linearni* ili *inverzni*.

**Zašto vredi:** ovo je najveći raskorak između onoga što rad tvrdi i onoga što kod radi,
a nadovezuje se pravo na tek isporučene makrociklusе — svaki blok bi nosio svoj model.

> **Status: urađeno** — grana [`feature/periodization-models`](features/periodization-models.md).
>
> Tri modela: **ravan** (4 nedelje, podrazumevan i identičan ranijem ponašanju), **linearan**
> i **obrnut** (po 6 nedelja). Model se bira po bloku dugoročnog plana i određuje i trajanje
> bloka.
>
> Propis nije upisan u `TrainingWeek` nego u `ExercisePlan`, gde serije, rep-opseg i RIR
> ionako već stoje — pa nije trebala nijedna nova kolona za sam propis, samo `PeriodizationModel`
> na mezociklusu i na bloku plana.
>
> Uz to je moralo i opterećenje: kada naredna nedelja traži drugačiji rep-opseg, težina se
> **preračunava iz najsvežijeg e1RM-a** umesto da se nosi iz prethodne nedelje uvećana za
> korak. Bez toga bi periodizacija menjala ponavljanja, a opterećenje bi ostalo od nekog
> drugog zadatka.

---

## 4. Nivo iskustva se prikuplja, a ne koristi

**Priručnik razlikuje tri nivoa vrlo konkretno:**

| | Početnik | Srednji | Napredni |
|---|---|---|---|
| Složene vežbe | 2-3 po treningu | 1-2 po treningu | do 3 **nedeljno** |
| Volumen | srednji | veći | manji |
| Deload | *„ne treba da razmišlja o ovome"* | povremeno | obavezno planirati |

**Šta aplikacija radi sada:** `Profile.ExperienceLevel` se traži pri registraciji i posle
se **nigde ne čita**. Početnik i napredni vežbač dobijaju identičan plan.

**Predlog:** iz nivoa izvesti početni volumen, odnos složenih i izolacionih vežbi, i
politiku deload-a. Za auto-deload konkretno: početniku prag podići (ili ga isključiti),
naprednom spustiti — priručnik je tu izričit.

---

## 5. Propušten trening ne postoji kao pojam

**Priručnik:**

> *"Ako ti raspored dozvoljava, pomeri trening za jedan dan. Ako ne, preskoči trening.
> Jedan propušteni trening nije smak sveta."*

**Šta aplikacija radi sada:** `SessionStatus` ima samo `Planned`, `InProgress`,
`Completed`. Preskočen trening zauvek ostaje „planiran".

**Ovo nije samo kozmetika.** Revizija auto-deload grane je našla da jedna nezavršena
sesija trajno blokira učenje granica volumena i ocenu umora za tu nedelju — jer se nedelja
nikada ne prepozna kao gotova. U četvoronedeljnom mezociklusu to je trećina signala.

**Predlog:** dodati `Skipped`. Nedelja je gotova kada je svaka sesija `Completed` ili
`Skipped`. Rešava i priručnikov slučaj i rupu iz revizije.

---

## 6. Zagrevanje se ne planira

**Priručnik daje RAMP model i konkretan primer potencijacije:**

> *"ako je radna serija 150kg x 5, zagrevaćemo se sa 60kg x 5, zatim 90kg x 3, pa
> 120kg x 2, pre nego što pređemo na radnu seriju."*

**Šta aplikacija radi sada:** `SetLog` izričito kaže *„zagrevanje se ne prati"*. Korisnik
dobije ciljnih 150 kg i sam smišlja put dotle.

**Predlog:** iz ciljnog opterećenja izračunati rampu (~40% x5, ~60% x3, ~80% x2,
zaokruženo na korak vežbe — što grana `feature/per-exercise-weight-step` već zna) i
prikazati je kao podsetnik. Ne upisuje se kao volumen, jer nije stimulativna.

Cena je mala, a vidljivost velika: to je prva stvar koju korisnik radi na treningu.

---

## 7. Pauze između serija se ne pominju

**Priručnik:**

> *"Složene vežbe: pauze preko 3 minuta. Izolacione vežbe: pauze od 1 do 3 minuta."*
> *"Duže pauze omogućavaju postizanje većeg stimulusa uz manje serija."*

**Šta aplikacija radi sada:** ništa. Iako `Exercise.Type` (Compound/Isolation) **već
postoji** i nosi tačno onu informaciju koja je za ovo potrebna.

**Predlog:** tajmer pauze koji se pokreće po upisu serije, sa podrazumevanim trajanjem po
tipu vežbe. Najmanja izmena na ovom spisku sa direktnom koristi na treningu.

---

## 8. Obrasci pokreta ne postoje u modelu

**Priručnik gradi ceo izbor vežbi oko obrazaca** — Squat, Hinge, Lunge, Bridge,
Horizontal/Vertical Push, Horizontal/Vertical Pull, plus izolacioni obrasci — i upozorava:

> *"Da bi izbegao asimetrije i disbalanse u snazi koji vremenom mogu prerasti u hronične
> povrede, veoma je važno da trening programiraš tako da svi mišići dobiju adekvatan
> stimulus."*

**Šta aplikacija radi sada:** šabloni su tvrdo kodirani spiskovi **imena vežbi**. Sistem
ne zna da su čučanj i leg press isti obrazac, pa ne može ni da proveri pokrivenost ni da
ponudi zamenu.

**Predlog:** `MovementPattern` na vežbi. Otvara dve stvari odjednom:

- **provera pokrivenosti** — „ovaj mezociklus nema nijedan Hinge obrazac";
- **zamena vežbe** — danas nemoguća. Priručnik je izričit da vežbu treba birati prema
  sopstvenoj građi i mobilnosti (*„Ako imaš problem sa učenjem tehnike kreni od stabilnijih
  varijacija"*), a korisnik trenutno ne može da zameni nijednu vežbu u planu.

Ovo je verovatno najkorisnija veća izmena, ali i najskuplja: traži proširenje seed podataka
za svih 27 vežbi.

> **Status: urađeno drugačije nego što je predloženo** — grana
> [`feature/more-templates`](features/more-templates.md).
>
> Po dogovoru se **ne uvodi** `MovementPattern` ni algoritam nad obrascima. Umesto toga su
> unapred sastavljeni šabloni prošireni sa tri na sedam (2–6 dana nedeljno), a katalog
> vežbi je dobio šest izolacija koje su nedostajale — među njima i prve izolacione vežbe
> za grudi i leđa.
>
> Pokrivenost mišićnih grupa se time ne proverava u vreme izvršavanja nego **testovima nad
> samim šablonima**: svaki šablon mora da pogodi svih osam velikih grupa, da ostane ispod
> MRV na svakom nivou i da dostigne MEV na referentnom nivou. Zamena vežbe u planu ostaje
> nemoguća, kao i ranije.

---

## 9. Osećaj pred trening ne utiče ni na šta

**Priručnik:**

> *"Ako se osećaš loše zbog manjka sna, loše ishrane ili stresa, smanji intenzitet treninga
> i fokusiraj se na glavne vežbe."*

**Predlog:** kratko pitanje pre treninga (san / energija / bol) koje spusti predloženo
opterećenje za taj dan. Uklapa se u priču rada o auto-regulaciji, jer je danas jedini ulaz
u regulaciju ono što se desilo *posle* serije, a ne stanje *pre* nje.

---

## 10. Volumen održavanja i prioriteti

**Priručnik:**

> *"Mišićne partije koje nisu prioritet možeš trenirati s volumenom za održavanje
> (oko ⅓ volumena potrebnog za rast)."*

**Predlog:** korisnik označi 1-2 prioritetne grupe; ostale dobijaju MEV/3 kao cilj. Metafora
sa ciglama i zamkom iz priručnika je gotovo objašnjenje za korisnika.

---

## Šta bih ostavio po strani

- **Napredne tehnike** (myo-set, drop-set, rest-pause, cluster). Priručnik ih izričito ne
  preporučuje početnicima i srednjem nivou, a traže modelovanje tipova serija — velika
  izmena za korisnika kome sistem i nije namenjen.
- **Unilateralni rad i asimetrije.** Traži evidenciju po strani tela; zanimljivo, ali menja
  model logovanja iz korena.
- **Tehnika i mobilnost.** Priručnik im daje dosta prostora, ali to je sadržaj (video,
  tekst), ne algoritam — ne uklapa se u tezu rada o determinističkim pravilima.

---

## Predloženi redosled

| # | Izmena | Cena | Zašto tim redom |
|---|---|---|---|
| 1 | Stimulativni volumen (blizina otkaza) | mala | popravlja tri postojeća mehanizma odjednom |
| 2 | `Skipped` status | mala | zatvara i rupu koju je revizija našla |
| 3 | Pauze po tipu vežbe | mala | podatak već postoji |
| 4 | Zagrevanje iz ciljne težine | mala | oslanja se na korak po vežbi |
| 5 | MAV kao treća granica | srednja | dopunjuje adaptivne granice |
| 6 | Nivo iskustva u generisanju | srednja | polje se već prikuplja |
| 7 | Periodizacija po nedeljama | veća | najveći raskorak sa tvrdnjom rada |
| 8 | Obrasci pokreta + zamena vežbe | veća | traži proširenje seed podataka |
| 9 | Osećaj pred trening | srednja | lepo se uklapa u auto-regulaciju |
| 10 | Volumen održavanja i prioriteti | srednja | nadogradnja na MAV |

Prve četiri stavke zajedno su otprilike jedna grana i pokrivaju četiri odvojena mesta gde
aplikacija odstupa od priručnika.
