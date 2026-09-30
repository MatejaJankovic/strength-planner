# Opseg ponavljanja po ulozi vežbe

**Grana:** `feature/rep-ranges-by-role`

Šesta grana iz revizije nauke o treningu (odeljak F), nalaz F9, odluka D4.

## Problem

Svaka izolacija je nosila 8–12 ponavljanja, u oba bloka. To je opseg hipertrofije složenih
vežbi, a u bloku snage izolacija ga je dobijala kao pomoćni rad (runda 10).

- **Priručnik** (str. 10–11) opsege „za snagu, hipertrofiju i izdržljivost" naziva mitom i
  kaže da se viši opseg obično koristi za izolacione vežbe.
- **Literatura:** mišić raste slično u širokom opsegu opterećenja kad je serija blizu otkaza
  (Schoenfeld i sar. 2017; 2021).
- **Laki tegovi nisu mogli da napreduju.** Iz 8–12 uz RIR 1 može oko 13% više tereta. Korak
  od 2 kg na bučici od 8 kg je 25%, pa je grana 1 (#85) morala da uvede produžen cilj: korak
  tek posle 17 ponavljanja. Tako je izgledala skoro svaka izolacija u šablonima: bučica ispod
  20 kg, sajla ispod 25, mašina ispod 50.

## Rešenje

### Izolacija: 10–20, u oba bloka

`GoalPrescriptions.ForExercise` izolaciji daje 10–20 (`TrainingConstants.IsolationRepRangeMin`
i `IsolationMaxReps`), uz ciljni RIR bloka. Složene vežbe se ne menjaju: 3–6 za glavna dizanja
u bloku snage, a 8–12 za sve ostalo.

Iz 10–20 uz RIR 1 može oko 27% više tereta, pa korak od 2 kg na bučici od 8 kg staje u sam
opseg. Produžen cilj iz grane 1 za izolacije ostaje samo kod stvarno lakih tegova: bučica od
5 kg sa korakom od 2 kg (40%) dobija korak posle 25 ponavljanja.

### Granica ponavljanja prati opseg koji plan nosi

`Periodization.MaxReps` je Epley granica od 12, i opseg se ispod nje pomera (runda 10). Za
izolaciju bi ta granica 10–20 svela na 10–12, pa je uvedena `Periodization.MaxRepsFor(osnovni
vrh)`:

- za opseg koji se završava do 12, granica je 12;
- za opseg preko 12, granica je 20.

Granica se čita iz opsega, a ne iz tipa vežbe, iz istog razloga iz kog postoje
`BaseRepRangeMin/Max`: izolacija iz bloka napravljenog ranije nosi 8–12 i čita granicu 12, kao
i do sada. Opseg preko 12 može da ima samo izolacija, jer ugrađene složene vežbe idu najviše
do 12, a lični šablon složenoj vežbi ne dozvoljava više. Na granici od 20 izolacija se ponaša
kao 8–12 na granici od 12: pomeraj naviše koji granica proguta vraća se kao serija. Zato se
nijedan broj serija nije pomerio.

### Procena maksimuma ostaje do 12

Serija preko 12 ponavljanja i dalje ne daje e1RM. Serija izolacije od petnaest nije dokaz
maksimuma, a procene čitaju rekordi, trend i ocena umora.

Zbog toga su dve stvari morale da se promene:

- **Trend snage za granice volumena.** Ramena i listove treniraju samo izolacije, pa bi MAV i
  MEV za njih prestali da uče. `StrengthChange` zato serije preko granice poredi **na istoj
  težini**, po efektivnim ponavljanjima: (30 + sada) / (30 + tada) − 1. Na istoj težini je to
  tačno odnos koji bi dale dve procene. Poređenje se koristi samo kad vežba nema par procena,
  i samo kad je bar jedna od dve serije preko 12. Dve serije do 12 na istoj težini, koje je
  procena odbila (runda 11: efektivna ponavljanja predaleko), ostaju neuporedive. Za složene
  vežbe se ništa ne menja. Nedelja posle koraka nema par na istoj težini, pa ne čita ništa;
  to je tišina, ne pad.
- **Prenos težine između dva propisa.** `NextWeekLoad` je bez procene maksimuma težinu
  prevodio preko `EstimateOneRepMax(težina, dno opsega, RIR)`, a ta metoda baca izuzetak
  iznad 12 ponavljanja. Do sada je to bilo nedostižno. Lična izolacija na 15–20 bi pukla na
  prvom prelazu u nedelju sa drugim propisom. Prenos je odnos na krivoj, pa sada ide preko
  `ImpliedOneRepMax`, koji granicu ne čita i računa isto gde oba postoje.

### Lični šablon

- Izolacija sme do 20 ponavljanja, složena vežba do 12 (`GoalPrescriptions.MaxTemplateReps`).
- Anotacija na zahtevu pušta do 20, a servis odbija složenu vežbu preko 12. Tip vežbe se zna
  tek iz baze, a poruka imenuje vežbu i kaže zašto.
- Editor prati isto pravilo po vežbi. Nova izolacija dobija 10–20, a nova složena 8–12, kao u
  ugrađenom šablonu.
- Odbijen unos se sada vraća i u polje. Kad je vrednost u modelu već bila na granici, signal
  se nije menjao, pa je Angular ostavljao odbijen broj. To je isti kvar kao `MeasureInput` iz
  runde 8, samo u editoru šablona, gde je postojao i pre ove grane.

## Šta je merenje reklo suprotno od očekivanja

1. **Plan je tvrdio da e1RM izolacije niko ne čita.** Nije tačno: početna težina sledećeg
   bloka se izvodi iz maksimuma na zapisu, pa i za izolacije. Procenu i dalje daju serije od
   10 do 12 ponavljanja uz RIR do 3, a dupla progresija posle svakog koraka vraća izolaciju na
   dno opsega, pa takve serije dolaze redovno. Bez ijedne procene na zapisu, izolacija u novom
   bloku kreće bez predložene težine, kao i ranije. Ovo nije mereno na stvarnim podacima, jer
   dosadašnji zapisi potiču iz 8–12.
2. **Pad koji je čekao.** Izuzetak u `NextWeekLoad` nije otkrio nijedan test. Našao se tek
   pregledom svakog mesta koje zove `EstimateOneRepMax` sa opsegom umesto sa odrađenom
   serijom.
3. **Opseg nije pomerio nijednu seriju.** Kad se novo pravilo vrati, padaju samo testovi
   opsega (5), a nijedan test MEV/MRV šablona. Pravilo o progutanom pomeraju na 20 daje iste
   serije kao na 12.

## Provera

- `dotnet test`: **798** (bilo 773); `npm test` **192** (bilo 190); `npm run build` prolazi.
- Novi testovi (`IsolationRepRangeTests`):
  - granica prati opseg (12 / 20, a stari 8–12 ostaje na 12);
  - izolacija ostaje u opsegu i zadržava širinu u svakom modelu;
  - svaka trenažna nedelja izolacije je različita;
  - na granici od 20 serije su iste kao 8–12 na 12;
  - korak lake bučice (8 kg staje; 5 kg posle 25);
  - poređenje na istoj težini: rast, pad, druga težina, daleko od otkaza, i da par procena ima
    prednost;
  - prenos težine 15–20 → 13–18 bez izuzetka.
- `GoalPrescriptionTests` prepisan za novo pravilo; `CustomTemplateTests` proverava granice po
  tipu; dve nove vitest provere editora (granice po tipu i vraćanje u polje).
- Merenje vraćanjem (commit, vraćeno pravilo, rebuild), na 798 testova:
  - opseg izolacije vraćen na 8–12 obara 5;
  - granica uvek 12 obara 10;
  - bez poređenja na istoj težini obara 2;
  - prenos težine preko `EstimateOneRepMax` obara 1.
- Na klijentu, na 192 testa: granica editora uvek 12 obara 1, a bez vraćanja u polje obara 1.
- End-to-end, uživo:
  - blok hipertrofije (Upper/Lower, srednji nivo, linearan): složene vežbe 8–12, izolacije
    10–20 @RIR 2 u nedelji 1. U nedelji 5 je 6–10 i 8–18 @RIR 1.
  - blok snage: bench, veslanje, čučanj i RDL 3–6 @RIR 3, izolacije 10–20 @RIR 3.
  - bočno podizanje 8 kg × 20 @RIR 2 → sledeće nedelje **10 kg** × 10–20, bez produženog cilja.
  - lični šablon: bočno podizanje 12–20 se čuva. Bench Press 8–15 se odbija (400, „Bench Press
    je složena vežba i ide najviše do 12 ponavljanja…"), a 12–21 odbija anotacija.
  - editor (375 px, bez prelivanja): Lateral Raise dobija 10–20 sa granicom 20, a Bench Press
    8–12 sa granicom 12. Upisanih 15 u bench vraća polje na 12.

## Ograničenja

- Grafik e1RM trenda za izolacije je većinom prazan, jer procenu daju samo serije do 12.
  Poruka na grafiku to već kaže.
- Postojeći blokovi i ranije sačuvani lični šabloni zadržavaju opsege sa kojima su napravljeni.
