# Šta priručnik traži, i šta je od toga urađeno

Analiza *Džepnog priručnika o programiranju treninga* (Dušan Petrović, Igor Maljik)
naspram stanja StrengthPlanner-a, uz pet ranije isporučenih grana.

Dokument je pisan kao spisak predloga. **Sada nosi i ishod svakog** — status stoji uz samu
stavku, uz granu na kojoj je urađena ili razlog zbog kog nije.

## Ishod

| # | Stavka | Ishod |
|---|---|---|
| 1 | Ne broji se svaka serija | ✅ `feature/stimulative-volume` |
| 2 | Nedostaje MAV | ✅ ista grana |
| 3 | Nedelje su ravne | ✅ `feature/periodization-models` |
| 4 | Nivo iskustva se ne koristi | ✅ `feature/experience-level` |
| 5 | Propušten trening | ⛔ preskočeno — odluka korisnika |
| 6 | Zagrevanje | ⛔ preskočeno — odluka korisnika |
| 7 | Pauze između serija | ⛔ preskočeno — odluka korisnika |
| 8 | Obrasci pokreta | ◐ rešeno drugačije — `feature/more-templates` |
| 9 | Osećaj pred trening | ⛔ preskočeno — odluka korisnika |
| 10 | Volumen održavanja i prioriteti | ⛔ preskočeno — odluka korisnika |

Stavka 8 je jedina rešena drugačije nego što je predlagano: umesto modela obrazaca pokreta
i algoritma nad njim, unapred su sastavljeni bolji šabloni, a pokrivenost se proverava
testovima. Tako je i bilo traženo.

Svaka urađena stavka ima svoj zapis u [`docs/features/`](features/), sa opisom šta je
urađeno, zašto, šta je revizija koda našla i kako je ispravljeno.

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

> **Status: urađeno** — grana [`feature/stimulative-volume`](features/stimulative-volume.md)
> (zajedno sa stavkom 2).
>
> `StimulativeVolume` daje pun kredit seriji do RIR 3, pola za RIR 4 i ništa iznad toga.
> Ispostavilo se da jedna skala nije dovoljna: pitanja o **stimulusu** (da li volumen
> raste) i pitanja o **zamoru** (koliko je nedelja bila teška) traže različite mere, pa
> `VolumeResponse` sada nosi obe — stimulativne i sirove serije. Prva verzija je usmerila
> signal zamora na stimulativnu meru i time pokvarila automatski deload.

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

> **Status: urađeno** — ista grana kao stavka 1.
>
> MAV je dodat kao ciljna vrednost, seedovan po mišićnoj grupi i uključen u adaptaciju.
> Cilj se uči zajedno sa MEV i MRV, ali sa ograničenim pomeranjem kroz pojas — bez toga je
> u prvoj verziji odlutao na MRV−1, pa je „cilj" prestao da bude cilj.

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

> **Status: urađeno** — grana [`feature/experience-level`](features/experience-level.md).
>
> Nivo iskustva sada određuje **četiri** stvari: broj vežbi po treningu i odnos složenih i
> izolacionih, broj serija po vežbi, gde počinju granice volumena (×0.8 / ×1.0 / ×1.2) i
> da li umor uopšte može da povuče deload — početnik nema prag, kako priručnik i traži.
>
> Ograničenje koje je ta grana sama prijavila (napredni vežbač je dobijao svega tri vežbe)
> zatvoreno je u [`feature/more-templates`](features/more-templates.md).

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

> **Status: preskočeno — odluka korisnika.**
>
> *„Ako preskočim trening danas, uraditi ću isti set vežbi sutra."* Trening se ne vezuje za
> datum toliko čvrsto da bi propuštanje trebalo modelovati; redosled treninga ostaje isti,
> samo se pomera u vremenu.

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

> **Status: preskočeno — odluka korisnika.** Zagrevanje se ne planira u aplikaciji.

---

## 7. Pauze između serija se ne pominju

**Priručnik:**

> *"Složene vežbe: pauze preko 3 minuta. Izolacione vežbe: pauze od 1 do 3 minuta."*
> *"Duže pauze omogućavaju postizanje većeg stimulusa uz manje serija."*

**Šta aplikacija radi sada:** ništa. Iako `Exercise.Type` (Compound/Isolation) **već
postoji** i nosi tačno onu informaciju koja je za ovo potrebna.

**Predlog:** tajmer pauze koji se pokreće po upisu serije, sa podrazumevanim trajanjem po
tipu vežbe. Najmanja izmena na ovom spisku sa direktnom koristi na treningu.

> **Status: preskočeno — odluka korisnika.** *„Pauziraću koliko mi treba."* Pauza se ne
> propisuje ni ne meri.

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

> **Status: preskočeno — odluka korisnika.** Osećaj pred trening se ne prikuplja i ne
> utiče na plan.

---

## 10. Volumen održavanja i prioriteti

**Priručnik:**

> *"Mišićne partije koje nisu prioritet možeš trenirati s volumenom za održavanje
> (oko ⅓ volumena potrebnog za rast)."*

**Predlog:** korisnik označi 1-2 prioritetne grupe; ostale dobijaju MEV/3 kao cilj. Metafora
sa ciglama i zamkom iz priručnika je gotovo objašnjenje za korisnika.

> **Status: preskočeno — odluka korisnika.** Volumen održavanja i prioritetne mišićne
> grupe ostaju van opsega.

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

## Kojim redom je urađeno

Predloženi redosled je poštovan gde je imao smisla, uz jednu izmenu: šabloni (stavka 8) su
urađeni **pre** periodizacije, jer je prethodna grana prijavila da napredni vežbač na
zatečenim šablonima dobija krnj trening — a taj problem se nije mogao rešiti bez novih
šablona i novih izolacionih vežbi.

| Grana | Stavke | PR |
|---|---|---|
| [`feature/stimulative-volume`](features/stimulative-volume.md) | 1, 2 | #8 |
| [`feature/experience-level`](features/experience-level.md) | 4 | #9 |
| [`feature/more-templates`](features/more-templates.md) | 8 | #10 |
| [`feature/periodization-models`](features/periodization-models.md) | 3 | #11 |

Stavke 5, 6, 7, 9 i 10 su preskočene odlukom korisnika, ne zbog cene.
