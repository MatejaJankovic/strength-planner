# Korak tega mora da stane u ono što serija može

**Grana:** `fix/load-step-absorption`

Prva grana iz revizije nauke o treningu (odeljak F), nalazi F1 i F2. Prethodne runde su
pravilo o koraku merile na težinama šipke. Lake težine (bučice, male mašine, sajle) jesu bile
u mreži testova svojstava — ali su ti testovi tvrdili **staro** pravilo (8 → 10 kg, 20 → 25 kg
na mašini), pa su grešku zaključavali umesto da je hvataju. Tamo je korak tega veliki u odnosu
na teret, i tu su tri pravila pošla naopako iz istog razloga.

## Problem

### F2 — korak koji serija ne može da podnese

Dupla progresija daje jedan korak kad sve serije stignu do vrha opsega, jer sledeći trening
kreće od dna. Taj povratak po Epley-u „plaća" dosta: vežbač na vrhu 8–12 sa RIR-om 1 može da
uzme oko **13%** više i da i dalje uradi 8 ponavljanja. Korak šipke na stvarnoj težini staje u
to. Korak bučice na lakoj težini ne staje:

| | Izmereno na kodu pre ove grane |
|---|---|
| Bočno podizanje 8 kg, 3 × 12 @RIR1 | predlog **10 kg** (+25%) |
| Po Epley-u na 10 kg uz RIR 1 | **3.4 ponavljanja** — oko pet ispod dna opsega |

Komentar u `ProgressionEngine` je tvrdio da je uslov za korak „tačno uslov da se sledeći
propis podigne". Dokaz je važio za **korišćenu** težinu, a predlagala se korišćena plus korak.

Koliko koji širok opseg upija (vrh opsega uz ciljni RIR, dno opsega dostižno bar do otkaza):

| Propis | Upija do | Korak ne staje ispod (bučice 2 / šipka i sajla 2.5 / mašina 5 kg) |
|---|---|---|
| 8–12 @RIR1 | ~13% | ~15 / ~19 / ~38 kg |
| 3–6 @RIR2 | ~15% | ~13 / ~16.5 / ~33 kg |
| 10–20 @RIR1 | ~27% | ~7 / ~9 / ~18 kg |

Uzak ili fiksan propis upija malo po samoj definiciji (11–12 @RIR2 ~7%, 5 × 5 @RIR2 ~6%, a uz
ciljni RIR ~2% i 0%), i zato ostaje na starom pravilu — vidi „Rešenje".

### F1 — korekcija koju korak ne može da izrazi

Korekcija po RIR-u je ograničena na ±10%, pa se zaokružuje na korak. Naniže: kad je 10% težine
najviše pola koraka — **težina ≤ 5 × korak** — i najveća korekcija se zaokruži nazad na istu
težinu. (Naviše važi samo ispod 5 koraka: zaokruživanje od nule podiže tačno pola koraka, pa
+10% na 10 kg daje 12.)

| Težina / korak | Serije 5/4/4 @RIR1 u 8–12 (korekcija −10%) | Pre | Sada |
|---|---|---|---|
| 10 kg / 2 (bučica) | 9 → zaokruženo 10 | **10** | 8 |
| 12.5 kg / 2.5 (sajla) | 11.25 → 12.5 | **12.5** | 10 |
| 25 kg / 5 (mašina) | 22.5 → 25 | **25** | 20 |
| 30 kg / 5 | 27 → 25 | 25 | 25 (nepromenjeno) |

Zajedno sa F2 to je bila zamka: vežbač skoči na 10 kg, ne stigne do opsega, a težina ostane
na 10 kg nedeljama.

### Deload na 100% težine

Isti koren: 0.9 × 10 kg = 9, zaokruženo na korak od 2 kg = **10**. Deload lakog tega bio je
deload samo po serijama. Izmereno za 8, 10 i 12.5 kg (koraci 2 i 2.5) i 25 kg (korak 5).

## Rešenje

### Dva režima (`StepAbsorption`)

- **Uzak ili fiksan propis** (5 × 5, 11–12 @RIR2, 3–4 @RIR3 — opseg uži od ciljnog RIR-a)
  zadržava pravilo iz runda 9 i 10 **na svakoj težini**. Ponavljanja su tamo propis, a korak
  nosi rezerva (odluka iz runde 10).
- **Širok opseg gde korak staje u propis** (`FitsAtTarget`: vežbač na vrhu opsega uz ciljni
  RIR bi ga podneo) — za 8–12 @RIR1 bučica od ~15 kg, šipka i sajla od ~19, mašina od ~38 —
  isto: pravilo iz runda 9 i 10 bez izmene.
- **Širok opseg ispod toga**: korak se daje samo kad **kapacitet svake serije** —
  ponavljanja plus rezerva koja je zaista ostala — upija povećanje (`Absorbs`): dno opsega
  posle njega ostaje dostižno, makar do otkaza. Povećanje ide do **sledeće težine na stalku**
  (`WeightMath.StepAbove`: 15 kg na koraku od 2 kg ide na 16, ne na zaokruženih 18), ili
  koliko sama korekcija traži ako je to više — nikad korak **i** korekcija povrh njega. Dok
  kapacitet ne upija korak, težina čeka: ne pada (vrh opsega nikad ne spušta opterećenje), a
  korekcija naviše radi isto kao unutar opsega.

### Cilj na ekranu (`RepsToEarnStep`, `step-absorption.ts`)

Ekranu treba broj ka kome se radi: ponavljanja koja uz **ciljni RIR** upijaju korak (17 za
8 kg na koraku od 2 kg; manje ponavljanja uz više rezerve upija isto). Računa ga klijent, iz
**težine koja se unosi** — server sudi o najtežoj seriji koja je zaista podignuta, pa cilj
izveden iz predloga ne bi važio čim vežbač uzme drugu bučicu. Pravilo je preneto u
TypeScript, a tabela u `step-absorption.spec.ts` je ista kao u `StepAbsorptionTests`.

Kartica prikazuje „Ponavljanja · cilj 17" i ispod težine objašnjenje. Napomena ispod unosa ne
obećava korak koji nije zarađen, a ni zadržavanje kad rezerva iznad cilja i dalje može da
podigne težinu.

**Uzak ili fiksan propis se ne produžava.** 5 × 5 je program, a ne opseg do šest (odluka iz
runde 10); tamo korak nosi rezerva, i napomena „ovde korak ide samo ako ostane rezerve" to već
kaže. **U deload nedelji cilj je vrh opsega, a napomene ćute**: posle deload-a se nastavlja od
težine zarađene pre njega, pa ništa upisano u njemu ne menja sledeće opterećenje.

### Korak naniže kad zaokruživanje obriše korekciju na granici

Korekcija naniže koja je dostigla −10% (najjači signal koji pravilo zna), a zaokruživanje ju je
vratilo na istu težinu, sada spušta težinu za **jedan korak** — osim kad bi spolja opterećena
vežba tako ostala prazna (bučica od 2 kg nema lakšu), gde težina ostaje. Manja korekcija naniže
(npr. −6%) i dalje ostavlja težinu, pa se napreduje ponavljanjima.

### Deload (`NextWeekLoad.DeloadLoad`)

Ako se 90% zaokruži nazad na punu težinu, deload ide korak ispod: 8 i 10 su jednako daleko od
9, a deload bira lakšu stranu. Tri težine ne mogu lakše i zadržavaju punu: težina od jednog
koraka (korak ispod bučice od 2 kg je prazna ruka, koju ekran s pravom prijavljuje kao grešku),
teret lakši od koraka (1 kg na koraku od 2 bi se zaokružio na nulu) i sama telesna masa bez
dodatog. Deload nikad nije teži od podignutog.

**Jedno pravilo za dva mesta**: nedelja posle završenog treninga i auto-deload su nosili svaki
svoju kopiju 90%. Pošto `DeloadService` nema testni harness, test
`TheDeloadFactor_IsAppliedInOnePlace` čita izvorni kod i odbija faktor van `NextWeekLoad`.

## Šta je revizija našla

Revizija je rađena u pet dimenzija (domen, deload, klijent, testovi, tvrdnje), a svaki nalaz
su tri nezavisna verifikatora pokušala da obore. Prošlo je (bar dva od tri): deload od **0 kg**
na težini od jednog koraka; produžen cilj **brojan dvaput** — jednom da upije korak, pa opet
kao rezerva za manjak RIR-a — pa je šipka od 30 kg u uskoj nedelji posle 13 do otkaza dobijala
32.5 kg (po Epley-u 9.7 ponavljanja, dno je 11); fiksnih 5 × 5 na običnoj šipci sa „ciljem 6";
ekran koji računa cilj iz predloga, a server iz podignutog; napomena koja obećava zadržavanje
koje korekcija naviše ruši. Deo verifikacija je prekinuo limit sesije, pa su preostali nalazi
(testovi, brojevi u ovom zapisu) provereni ručno — ispravni su bili i ispravljeni.

Drugi krug revizije, nad samim ispravkama, našao je da je **suđenje uskim propisima po
kapacitetu** ušlo u običan rad sa šipkom: uzak opseg upija malo po definiciji, pa je čučanj od
80 kg u 5 × 5 @RIR1, rađen tačno po propisu, prestao da napreduje (2880 naspram 2887.5), a
fiksnih 12 na bučici od 8 kg nije moglo da korakne ni uz RIR 5. Zato uski propisi sada ostaju na
starom pravilu. Isti krug je našao i korak naniže na 0 kg za težinu od jednog koraka (isti
defekt koji je deload već imao), skok 15 → 18 kg za težinu van mreže koraka, deload na nulu za
teret lakši od koraka i napomene u deload nedelji koje obećavaju posledice koje server ne
primenjuje. Glasanje je i ovde delom prekinuo limit sesije; potvrđena su dva nalaza (3/3 i
2/2), a ostali su provereni ručno.

Dve greške su izašle tek iz testova pisanih za prvi krug ispravki:

- **Korak plus korekcija na lakom tegu.** 12 kg uz 3 × 12 @RIR4: 12 × 1.09 + 2 = 15.08 →
  **16 kg**, gde po Epley-u ostaje 4.5 ponavljanja. Sada 14.
- **Vrh opsega je davao manje od serije ispod njega.** Prva ispravka prethodne greške davala je
  tačno jedan korak, a fiksnih 5 @RIR1 na 60 kg sa 4 ponavljanja uz RIR 5 (ispod opsega) dobija
  korekcijom 65 kg, dok je 5 uz RIR 5 dobijalo 62.5. Uhvatio test monotonosti čim je u mrežu
  dodat opseg 5–5. (Fiksni propisi su posle vraćeni na staro pravilo; isti slučaj u širokom
  opsegu je 40 kg u 11–12 @RIR1: 11 i 12 uz RIR 5 daju 45.)

## Provera

- `dotnet test`: **683** (bilo 613), `npm test`: **177** (bilo 150), oba build-a prolaze.
- Merenje vraćanjem na konačnom stablu (commit, pa vraćanje pravila u motoru, pa rebuild):

  | Vraćeno | Pada |
  |---|---|
  | režim lakih težina (staro pravilo svuda) | 10 |
  | korak naniže | 4 |
  | pravilo za deload | 9 |
  | sva tri | **23** (= 10 + 4 + 9) |

- Poređenje sa starom formulom (`ComputeNext_MatchesTheLegacyFormula_WhereNoChangeWasIntended`)
  pokriva preko 100 000 slučajeva. Ta formula je od pre runde 9, pa se na vrhu opsega poredi
  samo sesija bez manjka rezerve; zadržavanje uske nedelje posle otkaza tvrdi test svojstava
  za vrh opsega, u celoj mreži. Iz poređenja su, pored izuzetaka iz runde 9, izuzeti vrh
  širokog opsega gde korak ne staje u propis i korekcija na granici koju je zaokruživanje
  obrisalo. Mreže sada imaju i težine od jednog i dva koraka i lake težine van mreže koraka.
- End-to-end kroz API i pregledač, posle ispravki iz revizije. Ravan blok hipertrofije, lični
  šablon sa tri vežbe, jedan trening nedeljno:

  | Nedelja | Bočno podizanje (bučica, 2 kg) | Cable Fly (sajla, 2.5 kg) | Bench Press (kontrola) |
  |---|---|---|---|
  | 1: odrađeno | 8 kg × 12 × 3 @RIR1 | 12.5 kg × 5/4/4 @RIR1 | 60 kg × 12 × 3 |
  | 2: predlog | **8 kg** (bilo 10) | **10 kg** (bilo 12.5) | 62.5 kg |
  | 2: odrađeno | 8 kg × 17 × 3 **@RIR0** | 10 kg × 8 × 3 | 62.5 × 10 × 3 |
  | 3: predlog | **8 kg** — 17 bez rezerve ne upija korak | 10 kg | 62.5 kg |
  | 4 (deload) | **6 kg** (bilo 8) | **7.5 kg** (bilo 10) | 57.5 kg |

  Isto ponovljeno posle drugog kruga ispravki, sa istim rezultatom, i dodat lični šablon sa
  fiksnim propisima: čučanj 5 × 5 @RIR1 na 80 kg, tačno po propisu, ide na **82.5 kg** (u
  međuverziji je stajao na 80), a fiksnih 12 na bučici od 8 kg ide na 10 kg — staro pravilo,
  zabeleženo ograničenje ispod.

  Na ekranu (375 px), sa istom bučicom: „cilj 17" na 8 kg, 13 na 14 kg, 12 na 16 kg —
  cilj prati težinu koja se unosi, a napomena nestaje kad korak stane. Bez horizontalnog
  prelivanja (izmereno posle reload-a), bez grešaka u konzoli.

## Poznata ograničenja

- **Produžen cilj ide preko Epley granice od 12.** Serija od 17 ponavljanja ne daje procenu
  maksimuma, pa ni PR ni tačku u trendu, i ta vežba prestaje da osvežava svoj maksimum. A
  maksimum se koristi. Početna težina sledećeg bloka tada kreće od najnovijeg starijeg zapisa.
  Nedelja sa drugačijim propisom u periodizovanom bloku koristi procenu od pre produženja još
  do 56 dana, a posle toga težinu izvodi iz same težine (dno opsega uz ciljni RIR), što kod
  serija od 17 ponavljanja potcenjuje. Pravi odgovor je širi opseg za izolacije (10–20 upija
  ~27%), zasebna grana ove runde.
- **Na najlakšim težinama cilj je veliki**: 5 kg na mašini sa korakom 5, ili bučica od 2 kg,
  daju cilj od 45 ponavljanja; bučica od 4 kg 26, od 6 kg 20. Tu je rešenje finiji korak (ekran
  „Vežbe") ili širi opseg, a ne pravilo.
- **Pozitivna korekcija nigde nije proverena prema kapacitetu** — ni unutar opsega, ni na vrhu
  dok težina čeka. Na lakom tegu velika rezerva (RIR 5) i dalje može da zaokruži ceo korak koji
  kapacitet ne upija u potpunosti (15 kg u 11–12 @RIR2 → 17.5). To je bilo i ranije; strožija
  provera samo na vrhu je probana i odbačena, jer je vrh tada davao manje od iste serije ispod
  njega.
- **Uzak propis na lakom tegu i dalje uzima grub korak.** Fiksnih 12 na bučici od 8 kg posle
  12 uz RIR 1 ide na 10 kg, kao i ranije: ponavljanja se tu ne produžavaju, a po kapacitetu
  korak ne bi došao ni uz RIR 5. Lek je finiji korak (ekran „Vežbe") ili opseg umesto fiksnog
  broja.
- **Težina van mreže koraka u režimu gde korak staje** i dalje ide na zaokruženo „podignuto +
  korak" (17 kg na koraku od 2 kg → 20), kao pre ove grane - do jednog koraka više od sledeće
  težine na stalku (18). Težina van mreže nastaje samo kad je vežbač sam otkuca.
- **`UndoDeload` posle strožeg deload-a vraća manje.** Kad nema odrađene trenažne nedelje za
  taj dan, težina posle deload-a se vraća iz same deload težine (/0.9 pa naniže): iz 8 kg to je
  8, a ne 10. Taj put postoji samo kao rezerva (cela nedelja preskočena), i potcenjivanje je
  bezbedniji smer.
