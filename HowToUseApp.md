# Kako se koristi Strength Planner

Uputstvo za novog korisnika, korak po korak — od registracije do prvog završenog bloka.
Pisano je na srpskom jer je i ceo interfejs aplikacije na srpskom, pa se nazivi dugmadi
poklapaju sa onim što vidiš na ekranu.

Aplikacija je **lični trener za snagu**: ti unosiš šta si odradio, a sistem računa šta
treba da uradiš sledeći put. Ne pamti tuđe podatke i nema deljenje — jedan nalog, jedan
vežbač.

---

## Sadržaj

1. [Pre nego što počneš](#1-pre-nego-što-počneš)
2. [Korak 1 — Registracija](#korak-1--registracija)
3. [Korak 2 — Poznati maksimumi (1RM)](#korak-2--poznati-maksimumi-1rm)
4. [Korak 3 — Prvi plan](#korak-3--prvi-plan)
5. [Korak 4 — Ekran „Trening"](#korak-4--ekran-trening)
6. [Korak 5 — Logovanje treninga](#korak-5--logovanje-treninga)
7. [Korak 6 — Rezime posle treninga](#korak-6--rezime-posle-treninga)
8. [Korak 7 — Analitika](#korak-7--analitika)
9. [Korak 8 — Profil, podešavanja i vežbe](#korak-8--profil-podešavanja-i-vežbe)
10. [Nedeljni ritam](#nedeljni-ritam-kako-ovo-izgleda-u-praksi)
11. [Pravila koja sistem primenjuje](#pravila-koja-sistem-primenjuje)
12. [Česta pitanja i problemi](#česta-pitanja-i-problemi)
13. [Šta aplikacija ne radi](#šta-aplikacija-ne-radi)

---

## 1. Pre nego što počneš

Aplikacija se otvara u pregledaču. Najlakše pokretanje celog sistema (baza + API + web):

```bash
docker compose up --build
```

Zatim otvori `http://localhost:8080`.

Za razvojni režim (bez Docker-a) potrebni su lokalni PostgreSQL, .NET SDK 8 i Node ≥ 22:

```bash
dotnet run --project src/StrengthPlanner.API
```

```bash
npm --prefix strength-planner-web start
```

Detalji su u [`README.md`](README.md). Interfejs je pravljen za telefon — radi i na
desktopu, ali je najprijatniji na uskom ekranu, jer se u teretani koristi telefonom.

Donja navigacija ima četiri stavke i to je cela aplikacija:

| Stavka | Šta je tamo |
|---|---|
| **Trening** | aktivni blok, spisak nedelja i treninga, ulaz u logovanje |
| **Plan** | dugoročni plan — lanac blokova; **ovde se plan pravi i briše** |
| **Analitika** | e1RM trend, nedeljni volumen, tonaža, lični rekordi |
| **Profil** | pregled podataka, pa dalje na statistiku, vežbe i šablone; olovka za izmenu, zupčanik za nalog |

---

## Korak 1 — Registracija

Otvori `/register` (ili „Registruj se" sa ekrana za prijavu). Registracija je čarobnjak:
**jedno pitanje po ekranu**, sa trakom napretka na vrhu i strelicom nazad ako želiš da
ispraviš prethodni odgovor. Osam koraka:

| # | Pitanje | Ograničenje | Na šta utiče |
|---|---|---|---|
| 1 | **Ime** | obavezno, do 64 karaktera | naslov tvog profila |
| 2 | **Email i lozinka** | email do 256 karaktera; lozinka **bar 10**, najviše 128 | prijava |
| 3 | **Pol** | opciono — ima i „Ne želim da navedem" | evidencija |
| 4 | **Telesna masa (kg)** | 30–300 | evidencija; drži je ažurnom |
| 5 | **Visina (cm)** | 100–250, opciono | evidencija |
| 6 | **Uzrast** | 14–90 | evidencija |
| 7 | **Nivo iskustva** | Početnik / Srednji nivo / Napredni | **bitno** — menja ceo program |
| 8 | **Poznati maksimumi (1RM)** | vidi sledeći korak | početna opterećenja |

Masa, visina i uzrast unose se klizačem sa velikim brojem, a ispod njega stoji i polje za
precizan unos ako ti je lakše da otkucaš. **Vrednost je unapred popunjena** — pomeri klizač
ako nije tačna, jer se ono što ostaviš upisuje u profil.

Nalog nastaje tek posle sedmog koraka, kada pritisneš **„Napravi nalog"**. Ako odustaneš na
petom ekranu, ništa nije napravljeno.

Jedno polje zaista menja plan, pa ga popuni iskreno.

**Nivo iskustva** povlači četiri stvari odjednom:

| | Početnik | Srednji nivo | Napredni |
|---|---|---|---|
| Vežbi po treningu | 5 | 6 | 6 |
| Najviše složenih vežbi po treningu | 3 | 2 | 1 |
| Serija po vežbi na startu | 3 | 4 | 3 |
| Granice volumena (MEV/MAV/MRV) | ×0.8 | ×1.0 | ×1.2 |
| Automatski deload zbog umora | **ne** | da (prag 0.60) | da (prag 0.50) |

Početnik namerno **ne** dobija rani deload: procena RIR-a je kod početnika najnepouzdanija,
a nepotreban deload košta celu nedelju napretka. Planirani deload na kraju bloka i dalje
dobija.

Koliko puta nedeljno treniraš ne unosiš nigde. To bira šablon treninga na kasnijem ekranu:
šablon od tri dana *jeste* „tri treninga nedeljno".

> **Nema oporavka lozinke.** U sistemu nema slanja mejlova, pa ne postoji „zaboravljena
> lozinka". Ako izgubiš lozinku, gubiš nalog i sve u njemu. Sačuvaj je u menadžeru lozinki.

Posle „Napravi nalog" aplikacija te vodi na osmi korak — unos maksimuma. Ako osvežiš
stranicu na tom koraku, ostaješ na njemu; ne vraća te na početak.

---

## Korak 2 — Poznati maksimumi (1RM)

Ekran **„Poznati maksimumi"** traži procenu maksimuma za jedno ponavljanje. Iz tog broja
se računaju **početna radna opterećenja u prvoj nedelji** prvog bloka.

Ponuđeno je pet osnovnih vežbi: Back Squat, Bench Press, Deadlift, Overhead Press,
Barbell Row.

**Kako se unosi:**

1. Za svaku vežbu podesi vrednost — dugmad `−` i `+` menjaju po 2.5 kg, ili ukucaj broj
   direktno (0–500 kg).
2. Klikni **Sačuvaj** za tu vežbu. Brojač na vrhu prati koliko je sačuvano („Sačuvano 2 / 5").
3. Ako vežbaš još nešto što ti je bitno, izaberi je u **„Dodaj vežbu iz kataloga"** i
   dodaj joj 1RM na isti način.
4. **Nastavi na plan**.

**Ako ne znaš svoj 1RM**, imaš tri opcije, sve tri legitimne:

- Proceni iz najbolje serije koju pamtiš. Približna Epley formula:
  `1RM ≈ težina × (1 + ponavljanja / 30)`. Primer: 100 kg × 5 → `100 × (1 + 5/30) ≈ 117 kg`.
- Preskoči vežbu. Trening će za nju pisati *„Nema 1RM za ovu vežbu — unesi težinu po
  osećaju"*, ti prvi put uneseš šta si stvarno radio, i od tog trenutka sistem računa
  dalje sam.
- Unesi konzervativno. Prenizak start se popravi za nedelju-dve, previsok znači promašene
  serije od prvog dana.

> **Kasnija izmena:** ovaj ekran nema stavku u navigaciji. Ako hoćeš da ga ponovo otvoriš
> (npr. posle pauze, da resetuješ startna opterećenja), idi ručno na adresu `/onboarding`.
> Novi unos ne briše stari — dodaje se novija vrednost, a sistem koristi **najbolju iz
> poslednjih 56 dana**.

---

## Korak 3 — Prvi plan

Trening nastaje **samo kroz dugoročni plan** (makrociklus). Zvuči kao mnogo za početak, ali
nije: plan sme da ima **samo jedan blok**, i to je tačno ono što drugde zovu „jedan
mezociklus". Kad kasnije poželiš više, dodaješ blokove istom čarobnjaku.

Zato je **A** plan od jednog bloka, **B** plan od više njih, a **C** je za one koji hoće da
sami sastave šablon pre toga.

### A) Plan od jednog bloka (preporučeno za početak)

Kartica **„Plan"** → **„Napravi plan"**. Za svaki blok biraš četiri stvari:

**1. Šablon.** Sedam ugrađenih, plus tvoji lični ako si ih napravio (vidi **C**). Svaki
šablon pokazuje dane i vežbe koje nosi.

| Šablon | Dana nedeljno |
|---|---|
| Full Body (2 dana) | 2 |
| Full Body | 3 |
| Push/Pull/Legs | 3 |
| Upper/Lower | 4 |
| Full Body (4 dana) | 4 |
| Upper/Lower + Push/Pull/Legs | 5 |
| Push/Pull/Legs x2 | 6 |

Ako šablon nosi upozorenje (npr. dvodnevni: *„za većinu mišića ostaju ispod minimalnog
volumena za rast"*), ono se prikazuje odmah ispod naziva. Pročitaj ga pre izbora.

Šablon je **ponuda, ne propis**: svaki dan u katalogu ima više vežbi nego što trening
zaista dobija, a koliko ih uđe i koje odlučuje tvoj nivo iskustva (složene vežbe uvek prve).

**2. Cilj.**

| Cilj | Opseg ponavljanja | Ciljni RIR |
|---|---|---|
| **Snaga** | 3–6 | 2 |
| **Hipertrofija** | 8–12 | 1 |

**3. Raspored kroz nedelje (periodizacija).**

| Model | Trajanje | Kako izgleda |
|---|---|---|
| **Ravan** | 4 nedelje | isti propis svake nedelje; napredak nosi dupla progresija |
| **Linearan** | 6 nedelja | kreće volumenom (više ponavljanja, lakše), završava intenzitetom |
| **Obrnut** | 6 nedelja | teško dok si svež, volumen pred kraj |

U svakom modelu je **poslednja nedelja deload** (rasterećenje).

**4. Naziv i datum početka.** Naziv se predlaže iz šablona; datum je danas, ali ga možeš
pomeriti. Datumi treninga se iz njega razmeštaju kroz nedelju.

Klik na **„Napravi plan"** generiše ceo blok odjednom — sve nedelje, svi treninzi, sve
vežbe sa serijama, opsegom ponavljanja i ciljnim RIR-om. Aplikacija te vodi na ekran
„Trening".

> Pravljenje novog plana **gasi prethodni aktivni** (ne briše ga — samo prestaje da bude
> aktivan). Brisanje je zasebno dugme, opisano niže.

### B) Više blokova u lancu

Isti čarobnjak, samo dodaš blokove: do **6**, dugmetom „Dodaj blok". Podrazumevano smenjuju
ciljeve (hipertrofija → snaga → …), ali svakom bloku sam biraš cilj, raspored i šablon.
Ispod se vidi ukupno trajanje plana.

Ključna stvar: **generiše se samo prvi blok**. Ostali čekaju svoj red i prave se tek kad
prethodni završiš — od 1RM vrednosti koje tada važe, a ne od današnjih. Zato blok koji je
na čekanju piše *„Generiše se kad prethodni blok bude gotov"*.

Na ekranu „Plan" se posle toga vidi vremenska linija blokova sa statusom (**U toku**,
**Na čekanju**, **Završen**) i trakom napretka `odrađeno / ukupno treninga`.

**Klik na blok ga otvara.** Šta ćeš videti zavisi od toga da li je već generisan:

| Stanje bloka | Šta se vidi |
|---|---|
| generisan | dani prve nedelje sa vežbama i `serije×ponavljanja` |
| na čekanju | dani i vežbe šablona, **bez brojeva** |

Blok na čekanju nema brojeve zato što još nisu izračunati — nastaju kad mu dođe red.

**Brisanje plana** je na istom ekranu, ispod vremenske linije. Traži potvrdu i **trajno
briše ceo plan**: sve blokove, sve treninge i sve odrađene serije u njima. Procene 1RM
preživljavaju (e1RM trend ostaje), ali tonaža i volumen tih blokova nestaju. Pojedinačan
blok se ne briše — plan je celina.

### C) Lični šablon — svoje vežbe, serije i ponavljanja

Ako ti nijedan ugrađeni šablon ne odgovara, napravi svoj: **„Profil"** → dugme
**„Šabloni"**, ili direktno `/templates`.

Biraš sve sam:

- **dane** — koliko ih ima i kako se zovu (1–7);
- **vežbe u svakom danu** (1–12), iz kataloga i iz svojih vežbi, redosledom kojim ih dodaš;
- **broj serija** za svaku vežbu (2–10);
- **opseg ponavljanja** za svaku vežbu (3–12, donja granica ne sme biti veća od gornje).

Možeš imati do 20 sačuvanih šablona.

Granice za serije i ponavljanja nisu proizvoljne — iste su one koje periodizacija ionako
primenjuje. Da su šire, uneo bi broj koji bi ti plan tiho promenio. Gornja granica od 12
ponavljanja ima poseban razlog: preko nje Epley procena ne važi, pa serija ne bi dala
podatak za e1RM trend, rekorde ni ocenu umora.

Dva pravila koja aplikacija ne dozvoljava da prekršiš: ista vežba se ne sme pojaviti dvaput
u istom danu, i dva dana ne smeju nositi isti naziv.

**Razlika u odnosu na ugrađene šablone.** Ugrađeni šablon je *ponuda* — nosi više vežbi
nego što trening dobija, a nivo iskustva bira koliko ih uđe. **Lični šablon je propis**: u
trening ulazi tačno ono što si izabrao, onim redom kojim si izabrao. Ako upišeš dve vežbe u
dan, trening ima dve vežbe, i kad si napredan i kad si početnik.

**Šta i dalje radi sistem.** Tvoji brojevi su **propis prve nedelje i sidro**, a ne
konačna reč:

| Radi i dalje | Šta to znači za tvoje brojeve |
|---|---|
| Periodizacija | tvoj opseg se pomera kroz nedelje kao i svaki drugi (linearan model: više ponavljanja na startu, manje pred kraj) |
| Deload | poslednja nedelja polovi **tvoj** broj serija i vraća **tvoj** opseg ponavljanja |
| Ciljni volumen (MAV) | predlog serija se pomera ka nedeljnom cilju mišića, ali ostaje blizu tvog broja |
| Progresija iz serija | opterećenje raste iz onoga što stvarno odradiš, isto kao inače |

Primer (napredan nalog): uneseš Bench Press **6 serija × 5–8**, cilj hipertrofija, linearan
raspored.

| Nedelja | Šta piše u treningu |
|---|---|
| 1 | `7 × 8–11` |
| 3 (osnova) | `6 × 5–8` — tačno ono što si uneo |
| 5 | `5 × 3–6` |
| 6 (deload) | `3 × 5–8` |

Deload polovi **tvojih šest** na tri; da si uzeo ugrađeni šablon, polovio bi tri serije
koliko naprednom vežbaču sledi po nivou, i dobio bi dve. I vraća **tvoj** opseg `5–8`, a ne
`8–12` koji nosi cilj hipertrofije.

Lični šablon se bira i u dugoročnom planu (**B**), u padajućem meniju „Šablon treninga" za
svaki blok.

**Izmena i brisanje** idu sa istog ekrana, ali ne znače isto za planove koji već postoje i
za one koji tek treba da se naprave:

- **Već generisan mezociklus se ne menja.** Vežbe, serije i opsezi su prepisani u sam plan
  kad je napravljen, pa izmena šablona ne dira blok koji je u toku.
- **Blok dugoročnog plana koji čeka svoj red — menja se.** On se generiše tek kad mu dođe
  red, iz šablona kakav bude tada.
- **Brisanje takvog šablona aplikacija odbija**, uz poruku da ga koristi blok koji još nije
  generisan. Prvo obriši taj plan, ili sačekaj da blok bude odrađen.

---

## Korak 4 — Ekran „Trening"

Ovo je početni ekran i tu ćeš provoditi najviše vremena.

Na vrhu su naziv plana i tri oznake: **Cilj**, **Početak**, **Trajanje**. Ispod je spisak
nedelja, a u svakoj nedelji spisak treninga.

Šta se čita sa ekrana:

- **„Sledeći trening"** — prvi trening koji nije završen. To je ono što treba da uradiš.
- **Status svakog treninga** — `Planirano`, `U toku`, `Završeno`.
- **Broj vežbi** u tom treningu.
- **Deload** oznaka na nedelji rasterećenja. Ako piše **„Deload zbog umora"**, tu nedelju
  je pomerio sistem na osnovu izmerenog zamora, a ne kalendar.
- **„Umor 0.42"** — ocena umora izračunata iz završene nedelje (0 = odmoran, 1 = svi
  signali na maksimumu). Pojavljuje se tek kad je nedelja gotova.

Ovaj ekran **ništa ne pravi i ništa ne briše** — on je samo za trening. Blok koji je u toku
pripada planu, pa bi prekid usred njega ostavio plan bez bloka. I pravljenje novog plana i
brisanje zatečenog stoje na kartici **„Plan"**.

---

## Korak 5 — Logovanje treninga

Klik na trening otvara ekran sesije. Redosled je uvek isti:

### 1. Započni trening

Dugme **„Započni trening"**. Dok trening nije započet, polja za unos su zaključana — to je
namerno, da se plan ne bi „popunjavao" kod kuće.

### 2. Za svaku vežbu

Svaka vežba je jedna kartica i na njoj piše šta se traži:

- **Opseg** — npr. `8–12` ponavljanja.
- **RIR cilj** — koliko ponavljanja treba da ostane u rezervi (npr. `1`).
- **Težina serije** — predlog sistema; dugmad `−` i `+` menjaju je za **korak te vežbe**
  (šipka 2.5 kg, bučice 2 kg, mašina 5 kg — podesivo na ekranu „Vežbe").

Za svaku odrađenu seriju:

1. Podesi **težinu** (ako se razlikuje od predloga).
2. Podesi **ponavljanja** (1–100).
3. Izaberi **RIR** (0–5) — koliko si ponavljanja mogao još da uradiš.
4. Ako je serija išla **do otkaza**, čekiraj **„Serija do otkaza"**. RIR se tada
   automatski tretira kao 0, a ispod se pojavi objašnjenje šta to znači za sledeći trening.
5. **Dodaj seriju**.

Serija se odmah pojavljuje u spisku ispod (`80 kg × 10 · RIR 1`). Sledeća serija nasleđuje
težinu, ponavljanja i RIR prethodne — otkaz se **ne** nasleđuje, jer je izuzetak, ne
pravilo.

Grešku ispravljaš ikonicama pored serije: **olovka** za izmenu, **kanta** za brisanje.

> **Zašto je RIR važan.** Iz razlike između ciljnog i stvarnog RIR-a sistem računa
> korekciju opterećenja za sledeći put. Pogrešan RIR = pogrešan sledeći trening. Ako
> nisi siguran, proceni konzervativno (radije reci da je ostalo više nego manje).

### 3. Završi trening

Dugme **„Završi trening"** na dnu. Tek tada se sve obračunava. Završen trening se ne može
ponovo otvoriti — ostaje samo za pregled.

Ne moraš uneti sve serije koje plan traži; sistem računa sa onim što si stvarno uneo. Vežba
bez ijedne serije jednostavno prenosi isto opterećenje u narednu nedelju.

---

## Korak 6 — Rezime posle treninga

Odmah po završetku dobijaš karticu **„Trening završen"** i to je najkorisniji ekran u
aplikaciji. Za svaku vežbu piše:

- **e1RM** — procenjeni maksimum iz najbolje serije tog dana.
- **PR** — oznaka ako je to novi lični rekord.
- **Sledeće** — opterećenje predloženo za istu vežbu u narednoj nedelji, sa strelicom `↑`
  ako je povećano.

Povremeno se pojave i dve posebne poruke:

**„Nedelja N je pretvorena u deload."** — sistem je iz upravo završene nedelje izmerio
dovoljno umora da rasterećenje pomeri unapred. Serije su prepolovljene, opterećenje spušteno
na 90% onoga što si stvarno koristio. Ako je blok već imao planirani deload, on otpada —
mezociklus nosi jedno rasterećenje.

**„Blok X od Y je otvoren."** — završio si ceo blok dugoročnog plana, pa je sledeći
generisan odmah, od tvojih sadašnjih 1RM vrednosti, i već je aktivan.

---

## Korak 7 — Analitika

Kartica **„Analitika"** ima četiri celine. Dok nemaš nijedan plan ni rekord, umesto njih
stoji prazan ekran; kad plan postoji, celine se prikazuju, ali su bez podataka dok ne
završiš prvi trening.

**e1RM trend.** Biraš vežbu iz padajućeg spiska, dobijaš grafik procenjenog maksimuma kroz
vreme, poslednju vrednost i promenu u odnosu na prethodnu. Ovo je glavna mera napretka.
Procena postoji samo za serije **do 12 ponavljanja** — iznad toga formula nije pouzdana, pa
se ne beleži.

**Nedeljni volumen.** Za izabrani mezociklus i nedelju, po mišićnim grupama: koliko si
stimulativnih serija odradio i gde to pada u odnosu na tvoje granice.

- **MEV** — minimum ispod kog nema stimulusa
- **MAV** — ciljna vrednost, gađaj ovaj marker
- **MRV** — plafon iznad kog nema oporavka

Boja trake govori da li si ispod, u zoni ili iznad. **Ne broji se svaka serija isto**:
serija sa RIR 0–3 (ili do otkaza) ulazi cela, RIR 4 ulazi upola, a serija dalja od otkaza
donosi zamor ali ne i volumen. Granice se **uče iz tvojih podataka** — posle svake završene
nedelje pomeraju se najviše za jednu seriju, i najviše 50% od podrazumevane vrednosti.
Dugme **„Vrati podrazumevane granice"** poništava naučeno.

**Nedeljna tonaža.** Zbir `težina × ponavljanja` po nedeljama, sa označenim deload
nedeljama. Korisno da se vidi da li blok stvarno raste.

**Lični rekordi.** Po vežbi: najbolji e1RM i najveća podignuta težina, sa datumom.

---

## Korak 8 — Profil, podešavanja i vežbe

Ekran **Profil** je pregled i raskrsnica. Na vrhu su tvoja slika, ime i email, a desno dve
ikonice: **olovka** vodi na izmenu podataka, **zupčanik** na podešavanja naloga. Ispod
imena stoje pročitani podaci (uzrast, masa, visina, nivo, pol) — prikazuju se samo ona koja
su popunjena. Pod njima je **Dashboard** sa tri dugmeta: **Statistika**, **Vežbe**,
**Šabloni**.

### Izmeni profil (olovka)

**Slika profila.** „Promeni sliku" bira fajl sa telefona. Prihvataju se JPEG, PNG i WebP, do
2 MB. Slika se čuva odmah po izboru, ne čeka „Sačuvaj". „Ukloni sliku" je vraća na krug sa
prvim slovom imena.

**Ime.** Stoji kao naslov profila. Ako ga ostaviš prazno, piše email.

**Podaci o tebi.** Pol, uzrast, telesna masa, visina, nivo iskustva. Masa i nivo se menjaju
kroz vreme — drži ih ažurnim. Izmena **ne dira blok koji je već generisan**; primenjuje se
na sledeći koji se napravi.

### Podešavanja (zupčanik)

**Lozinka.** Trenutna + nova + potvrda. Promena lozinke **odjavljuje sve ostale uređaje**.
Potvrda nije formalnost — pošto nema oporavka lozinke, greška u kucanju znači trajan gubitak
naloga.

**Odjava.** Briše token iz pregledača. Podaci ostaju na nalogu.

**Brisanje naloga.** Nepovratno. Traži **trenutnu lozinku i otkucanu reč `OBRIŠI`** —
lozinka štiti od nekoga kome je telefon ostao otključan, otkucana reč od tebe samog. Briše
nalog, profil, sve planove i odrađene treninge, maksimume, sopstvene vežbe i šablone. Nema
rezervne kopije i nema načina da se to vrati.

### Vežbe (dugme na dashboardu)

Ceo katalog, **jedan red po vežbi**. U redu stoje naziv, oznake, mišićne grupe i korak
opterećenja.

**Korak opterećenja** je najmanji skok težine dostupan za tu vežbu. Podrazumevano se izvodi
iz sprave (šipka 2.5 kg, bučice 2 kg, mašina 5 kg), ali ako tvoja teretana ima druge tegove,
promeni ga ovde (0.5, 1, 1.25, 2, 2.5, 5 ili 10 kg). Izmenjene vežbe nose oznaku
„izmenjeno" i dugme za povratak na podrazumevano. **Ovo je jedno od korisnijih
podešavanja** — pogrešan korak znači da ti sistem predlaže težine koje ne možeš da složiš.

**Sopstvene vežbe** nose oznaku „tvoja". Dugme **„+"** u zaglavlju otvara formu za novu:
naziv, tip (složena / izolaciona), oprema, primarna mišićna grupa i opciono sekundarne
(svaka se u volumenu broji kao pola serije). Vežba ulazi u analitiku volumena i može joj se
dodeliti 1RM.

Pretraga radi nad istim spiskom u koji dodaješ, pa pre dodavanja proveri da vežba već ne
postoji pod drugim imenom.

### Poznati maksimumi, kasnije

Maksimume ne unosiš samo pri registraciji. Kartica **„Poznati maksimumi"** na profilu vodi
na isti ekran, pa ih ažuriraj kad napreduješ — sledeći blok kreće od novih brojeva.

---

## Nedeljni ritam (kako ovo izgleda u praksi)

Kad je sve podešeno, korišćenje je kratko i uvek isto:

**Pre treninga** — otvori „Trening", nađi onaj sa oznakom „Sledeći trening", klikni.

**U teretani** — „Započni trening", pa posle svake serije: težina, ponavljanja, RIR,
„Dodaj seriju". Telefon ostaje otvoren dok ne završiš.

**Posle treninga** — „Završi trening", pogledaj rezime: da li je nešto PR, koliko je
predloženo za sledeći put.

**Na kraju nedelje** — otvori „Analitika" i proveri volumen: da li su mišićne grupe u zoni
oko MAV-a. Ako je nešto stalno ispod MEV-a, u sledećem bloku uzmi šablon sa više dana ili
dodaj vežbu za tu grupu.

**Na kraju bloka** — deload nedelju odradi kako je propisana (lakše je namerno, to nije
gubljenje vremena). Zatim: ako plan ima sledeći blok, on se otvara sam; ako je plan bio od
jednog bloka, napravi nov — startna opterećenja se povuku iz procena koje su nastale tokom
prethodnog bloka.

---

## Pravila koja sistem primenjuje

Ovo je jezgro aplikacije. Ne moraš ga znati da bi je koristio, ali objašnjava zašto brojevi
izgledaju kako izgledaju.

**Dupla progresija.** Prvo rasteš u ponavljanjima ka vrhu opsega, pa tek onda u težini.
Kad **sve** serije jedne vežbe stignu do vrha opsega, sledeći put dobijaš **jedan korak**
više težine.

**Korekcija po RIR-u.** Sledeće opterećenje se koriguje za `(prosečan RIR − ciljni RIR) × 3%`,
ograničeno na **±10%**. Lakše nego traženo → težina raste; teže → pada. Serija do otkaza
ispod donje granice opsega ulazi kao negativan RIR, srazmerno promašenim ponavljanjima —
zato korekcija naniže može da dosegne isti plafon kao naviše.

**Procena maksimuma (e1RM).** Epley formula preko efektivnih ponavljanja
(`ponavljanja + RIR`), samo za serije do 12 ponavljanja. Serije iz deload nedelje se
namerno **ne** upisuju — submaksimalne su, pa bi veštački oborile trend.

**Start novog bloka.** Uzima se **najbolja** procena iz poslednjih **56 dana**, ne poslednja
— poslednji zapis može biti sa lošeg dana. Ako u tom prozoru nema ničega, uzima se najnoviji
zapis ikada.

**Automatski deload.** Iz svake završene nedelje se računa ocena umora (0–1) iz četiri
signala: odstupanje RIR-a, udeo serija do otkaza, pad procenjenog 1RM i volumen u odnosu na
MRV. Nijedan signal sam ne može da pokrene deload — najteži nosi 0.35 naspram praga 0.60, pa
se bar dva moraju složiti. Kad se pokrene: serije prepolovljene, opterećenje 90% stvarno
korišćenog.

**Sastav treninga.** Iz spiska vežbi u danu uzimaju se prvo složene (do broja koji tvoj nivo
dozvoljava), pa izolacione dok se ne popune mesta. Trening nikad nema manje od tri vežbe.

Ovo važi **samo za ugrađene šablone**. Lični šablon se ne prekraja: u trening ulaze tačno
tvoje vežbe, pa i dan sa jednom vežbom ostaje dan sa jednom vežbom.

**Periodizovane nedelje.** Kad se propis menja iz nedelje u nedelju (linearan i obrnut
model), opterećenje se ne prenosi kroz „+ jedan korak" nego se ponovo izvodi iz najsvežije
procene 1RM-a i propisa te nedelje. Nedelja koja pada sa 10 na 5 ponavljanja mora da bude
osetno teža, a ne ista uvećana za korak.

---

## Česta pitanja i problemi

**Nemam 1RM ni za jednu vežbu — mogu li da počnem?**
Da. Prva nedelja neće imati predložena opterećenja, na karticama će pisati *„unesi težinu po
osećaju"*. Od drugog treninga sistem računa sam.

**Zaboravio sam lozinku.**
Nema oporavka. Ne postoji slanje mejlova, pa ni reset. Nalog je izgubljen.

**„Previše pokušaja. Sačekaj … sekundi pa probaj ponovo."**
Registracija je ograničena na **5 pokušaja po satu** sa iste adrese, prijava i promena
lozinke na **20 u minuti**. Sačekaj koliko poruka kaže i pokušaj ponovo.

**Uneo sam pogrešnu seriju.**
Dok trening traje — olovka za izmenu, kanta za brisanje. Kad je trening završen, više se ne
menja; jedina opcija je brisanje celog plana, što gubi sve.

**Preskočio sam trening iz sredine nedelje.**
Nije problem. Treninzi se ne moraju raditi redom i sistem ne prepisuje ciljeve već
odrađenim treninzima. Datum je predlog, ne rok.

**Promenio sam nivo iskustva usred bloka.**
Tekući blok ostaje nepromenjen — namerno, da se plan u toku ne bi prekrajao ispod ruku.
Novi nivo važi za sledeći generisani blok.

**Napravio sam lični šablon, a plan ne pokazuje broj serija koji sam uneo.**
Tako i treba. Tvoj broj je *sidro*, a predlog se pomera ka nedeljnom cilju volumena tog
mišića i menja se kroz nedelje po izabranom rasporedu. Ako želiš da vidiš tačno svoje
brojeve, pogledaj nedelju koja je osnova rasporeda (kod linearnog i obrnutog modela to je
treća). Nivo iskustva ovde **ne** menja broj serija — kod ličnog šablona polazi se od tvog
unosa, ne od nivoa.

**Ne mogu da obrišem lični šablon.**
Koristi ga blok dugoročnog plana koji još nije generisan. Obriši taj plan ili sačekaj da
blok bude odrađen, pa će brisanje proći.

**Volumen mi je stalno „iznad" za neku grupu.**
Znači da prelaziš svoj MRV. Granice se same spuštaju posle završenih nedelja, ali možeš i
odmah da uzmeš šablon sa manje preklapanja ili da smanjiš broj serija.

**Volumen mi je „ispod" iako radim mnogo serija.**
Verovatno su serije predaleko od otkaza. Sa RIR 5+ serija ne ulazi u volumen uopšte, sa
RIR 4 ulazi upola. Ili idi bliže otkazu, ili dodaj serije.

**Blok na ekranu „Plan" piše da čeka, a prethodni je gotov.**
Otvori ekran „Plan" — sistem pri čitanju sam popravlja plan koji je ostao bez tekućeg
bloka i generiše sledeći.

---

## Šta aplikacija ne radi

Namerno je van opsega, da ne bi bilo iznenađenja:

- nema slanja mejlova → nema potvrde naloga ni oporavka lozinke;
- nema više jezika (interfejs je samo na srpskom);
- nema offline režima ni instalacije kao aplikacije (PWA);
- **ne može se promeniti periodizacija već generisanog bloka** — pravi se novi;
- nema deljenja plana, trenera ni više korisnika na jednom nalogu;
- analitika pokriva tekuće blokove, ne celu istoriju kroz sve planove.
