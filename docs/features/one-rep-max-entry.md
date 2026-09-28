# Test-serija i brisanje zapisa na ekranu „Poznati maksimumi"

Nalazi D23a i D19 iz pregleda logike treninga. Oba su na istom ekranu.

## D23a — rad obećava unos test-serije, ekran ga nije imao

Slučaj korišćenja 3 u radu kaže: *„за главне вежбе корисник уноси познати 1RM **или податке
тест-серије (тежина, понављања, RIR) из којих систем процењује e1RM**"*. Postojao je samo
prvi put — polje za broj.

Sada postoji i drugi: dugme **„Izračunaj iz test-serije"** u redu vežbe otvara tri polja
(težina, ponavljanja, RIR), a server iz njih računa procenu.

### Tri odluke koje ovo nosi

**1. Upisuje se kao procena (`Estimated`), ne kao ručni unos.** To i jeste — Epley procena
iz serije. Ručni unos ima posebnu moć: poništava starije procene, jer je izjava vežbača o
njegovom maksimumu. Test-serija nema razloga da to radi, jer je **iste vrste** kao procene
iz odrađenih treninga; nadmeće se sa njima po zatečenom pravilu (najbolja u prozoru od 56
dana).

**2. Prolazi kroz isti predikat kao upisana serija** — `E1RmCalculator.CanEstimateFrom`.
Bez toga bi ovaj ekran bio rupa kroz koju se vraća tačno ono što je deveta runda uklonila:
100 kg × 12 uz RIR 5 „čita" 156.7 kg, a ista serija do otkaza 140 — naduvana vrednost koja
bi zatim osam nedelja bila startna za blok. Granice su 12 ponavljanja i RIR 3, i to su
**iste konstante** koje domen koristi (test to i drži).

**3. Epley ostaje na serveru.** Nema pregleda procene dok kucaš. Formula je trenažno pravilo,
a kopija u Angular komponenti bi bila drugi izvor istine za isti broj — tačno ono što
`CLAUDE.md` traži da se izbegne. Red prikaže vrednost koju je server vratio.

### Vežbe koje diže sopstvena masa

Ranije je njihov red pisao samo *„Ne unosi se"*. Sada im test-serija radi, i to je **jedini**
način da zgib dobije startno opterećenje pre prvog treninga: kilogrami u polju su ono što je
dodato, a telesnu masu server pridoda iz profila, pa dvosmislenosti nema.

Izmereno uživo, vežbač od 80 kg: zgib sa 0 dodatih × 8 uz RIR 2 → **106.7 kg** ukupno
(80 × (1 + 10/30)).

## D19 — zapis se nije mogao obrisati

Deveta runda je već ispravila veći deo ovog nalaza: ekran od tada prikazuje vrednost **od
koje plan zaista polazi** (bira je `OneRepMaxBaseline`), a ručni unos poništava starije
procene, pa se maksimum može spustiti. Ostalo je da se pogrešan zapis ne može ukloniti.

`DELETE /api/onerepmax/{id}` briše jedan zapis vežbača. Pošto ekran prikazuje baš onu
vrednost koju plan koristi, brisanje je način da pogrešan broj prestane da bude ta vrednost.

Ekran se posle brisanja **čita ponovo**, umesto da pogađa šta je sada tekuća vrednost — iza
obrisanog zapisa može da stoji stariji, i tada on dolazi na njegovo mesto.

## Šta je izmereno

Uživo:

| Unos | Rezultat |
|---|---|
| Bench 100 kg × 5 uz RIR 1 | **120.0 kg** (100 × (1 + 6/30)) |
| Zgib 0 dodatih × 8 uz RIR 2, telo 80 kg | **106.7 kg** ukupno |
| Bench 110 kg × 3 uz RIR 0, kroz ekran | **121.0 kg** |
| Bench 100 kg × 12 uz **RIR 5** | odbijeno, 400 |
| Bench 100 kg × **15** | odbijeno, 400 |
| Bench **0 kg** × 5 | odbijeno, 400 — nema šta da se skalira |
| brisanje procene od 136.7 | prošlo; na njeno mesto došla procena od **120.0** |
| brisanje **tuđeg** zapisa | 404, red ostaje u bazi |

## Šta je pregled uhvatio

Tri stvari koje je uvela **ova** grana:

1. **Red za zgib je i dalje pisao „Ne unosi se"**, tik uz dugme koje ga sada unosi. Sada
   piše „Samo iz serije" i objašnjava zašto se broj ne kuca.
2. **Procena iz test-serije se upisivala u keš kao tekuća vrednost** — a tekuću vrednost
   bira pravilo nad svim zapisima. Nova procena ne mora da bude ona. Izmereno pre ispravke:
   posle serije od 60 kg × 3 ekran je pokazivao **66 kg**, dok je plan polazio od **121**.
   To je tačno nesklad između ekrana i generatora zbog kog nalaz D19 i postoji — vraćen
   mojom izmenom, na klijentu. Sada se spisak čita ponovo, isto kao posle brisanja.
3. **Neuspeh tog osvežavanja se gutao** (`error: () => {}`). Izmena je prošla na serveru, pa
   bi tiho zadržan stari spisak prikazivao broj koji plan više ne koristi. Sada se prijavljuje.

## Testovi

`TestSetEntryTests` (8 tvrdnji): da se granice na zahtevu poklapaju sa domenskim
konstantama (`EpleyRepCap`, `E1RmMaxRir`) — test protiv razlaženja, jer su to ista odluka
napisana dva puta; da je nula kilograma dozvoljena na zahtevu a da domen nad ukupnim
opterećenjem odlučuje da li znači nešto; i koliko ograda vredi u brojevima.

Na klijentu `one-rep-max.service.spec.ts` (3 tvrdnje) drži pravilo koje je pregled uhvatio:
ni test-serija ni brisanje ne smeju da pogađaju koja je vrednost sada tekuća, a ručni unos
ostaje jedini koji to sme — jer on poništava starije procene.

Ukupno: 603 → 611 testova na serveru, 139 → 142 na klijentu.

## Poznato ograničenje

Stilski fajl ovog ekrana je posle izmene na **6.01 kB**, tek preko granice upozorenja od
6 kB. Tvrda granica je 8 kB, pa prostora ima — ali je vredno zabeležiti uz istu napomenu o
ekranu za trening, koji je na 7.98 kB od 8.
