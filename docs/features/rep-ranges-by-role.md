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
  tačno odnos koji bi dale dve procene. Poređenje se koristi samo kad jedna od dve nedelje za
  tu vežbu nema nijednu procenu, i samo kad je bar jedna od dve serije preko 12. Kad obe
  nedelje imaju procenu, odlučuje procena, pa i kad kaže da par nije uporediv. Dve serije do 12 na istoj težini, koje je
  procena odbila (runda 11: efektivna ponavljanja predaleko), ostaju neuporedive. Za složene
  vežbe se ništa ne menja. Nedelja posle koraka nema par na istoj težini, pa ne čita ništa;
  to je tišina, ne pad.
- **Prenos težine između dva propisa.** Kad sledeća nedelja ima drugi propis, `NextWeekLoad`
  je težinu izvodio iz najbolje procene u prozoru od 56 dana. Serije izolacije preko 12
  procenu ne upisuju, pa ta procena potiče iz neke ranije, lakše serije. Review je to našao,
  a E2E potvrdio: posle 8 kg × 12 u nedelji 1 i 10 kg × 20 u nedelji 2, nedelja 3 bi dobila
  8 kg iz procene od 11.7 kg, dok je progresija tražila 12. Za opseg koji ide preko 12
  težina se sada prenosi sa sopstvene težine treninga (progresija, inače referenca), istim
  putem kojim se prenosila i za vežbu bez ijedne procene. Složene vežbe i dalje čitaju
  maksimum.

  Taj put je prevodio težinu preko `EstimateOneRepMax(težina, dno opsega, RIR)`, a ta metoda
  baca izuzetak iznad 12 ponavljanja. Lična izolacija na 15–20 bi pukla na prvom prelazu u
  nedelju sa drugim propisom. Prenos je odnos na krivoj, pa sada ide preko
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
2. **Težina koja pada posle serije na vrhu opsega.** Prva verzija grane je prošla sve testove
   i E2E, a propuštala je upravo ono zbog čega je opseg proširen. E2E je proverio samo prelaz
   iz nedelje 1 u nedelju 2, gde je propis isti. Review je našao nedelju 3 (8 kg umesto 12),
   i to je izmereno i uživo. Isti oblik greške kao runda 10 (pravilo tačno, aplikacija ne):
   test je proveravao opseg, a ne ono što sledeća nedelja dobija.
3. **Poređenje na istoj težini je išlo šire nego što je pisalo.** Pokretalo se kad god par
   procena nije uporediv. Bench 100 × 9 pa 100 × 6 uz 80 × 16 pa 80 × 13 bi tako čitao −6.4%,
   iako je procena taj par odbila po pravilu iz runde 11. Sada važi samo za nedelju bez
   procene, kako je beleška i tvrdila. Cena: izolacija čije završne serije u obe nedelje
   padnu na 12 ili manje, sa neuporedivim procenama, ne čita ništa, iako bi isti teret
   rekao rast. Nije mereno koliko je to često.
4. **Predlog pri čitanju je imao istu grešku.** Nedelja čiji prethodni trening istog dana
   nije odrađen (preskočen, ili još nije stigao) nema upisan cilj, pa ga ekran računa pri
   čitanju iz maksimuma na zapisu. Za izolaciju je to ponovo stara procena. Sada se prenosi
   sa poslednje upisane težine iste vežbe u bloku, istim pravilom (`NextWeekLoad`). Kartica
   tada kaže da je predlog prenet, a ne da je iz maksimuma. Izmereno uživo: nedelja 3
   preskočena, a nedelja 4 dobija 12 kg umesto 8.
5. **Povratak posle deload-a naduvavao je težinu.** Kad deload nema tačku nastavka (ceo
   prethodni trening je preskočen), težina se vraća sa `UndoDeload`, ali je prevođena preko
   propisa deload-a (RIR cilja + 2). Kablovsko letenje od 30 kg bi se vratilo na 32.5, što
   je korak koji niko nije zaradio. Greška je postojala i ranije za vežbu bez maksimuma, a
   ova grana ju je proširila na svaku izolaciju. Vraćena težina sada ide uz RIR cilja. Ovo
   je provereno računom, ne uživo, jer taj put traži auto-deload i preskočen dan pre njega.
6. **Pad koji je čekao.** Izuzetak u `NextWeekLoad` nije otkrio nijedan test. Našao se tek
   pregledom svakog mesta koje zove `EstimateOneRepMax` sa opsegom umesto sa odrađenom
   serijom.
7. **Test je zadavao korak koji vežba nema.** Polazna težina bočnog podizanja je proveravana
   sa korakom od 2.5 kg, a bučica ima korak od 2 kg: aplikacija daje 28 kg, ne 27.5. Isti
   nalaz kao runda 9, tačka 7. Test sada čita korak iz sprave.
8. **Opseg nije pomerio nijednu seriju.** Kad se novo pravilo vrati, padaju samo testovi
   opsega (5), a nijedan test MEV/MRV šablona. Pravilo o progutanom pomeraju na 20 daje iste
   serije kao na 12.

## Provera

- `dotnet test`: **802** (bilo 773); `npm test` **192** (bilo 190); `npm run build` prolazi.
- Novi testovi (`IsolationRepRangeTests`):
  - granica prati opseg (12 / 20, a stari 8–12 ostaje na 12);
  - izolacija ostaje u opsegu i zadržava širinu u svakom modelu;
  - svaka trenažna nedelja izolacije je različita;
  - na granici od 20 serije su iste kao 8–12 na 12;
  - korak lake bučice (8 kg staje; 5 kg posle 25);
  - poređenje na istoj težini: rast, pad, druga težina, daleko od otkaza, i da par procena ima
    prednost;
  - prenos težine 15–20 → 13–18 bez izuzetka;
  - scenario iz review-a: 10 kg × 20, progresija 12, stara procena 11.2 → 12 kg, a složena
    vežba i dalje iz maksimuma;
  - kad obe nedelje imaju procenu, poređenje na istoj težini ne učestvuje;
  - izolacija bez ijedne poznate težine i dalje ima maksimum kao rezervu.
- Mreže svojstava progresije i upijanja koraka sada sadrže i opsege 10–20, 9–19 i 8–18.
- `GoalPrescriptionTests` prepisan za novo pravilo; `CustomTemplateTests` proverava granice po
  tipu; dve nove vitest provere editora (granice po tipu i vraćanje u polje).
- Merenje vraćanjem (commit, vraćeno pravilo, rebuild), na 801 test (pre poslednjeg testa
  za rezervu):
  - opseg izolacije vraćen na 8–12 obara 5;
  - granica uvek 12 obara 10;
  - bez poređenja na istoj težini obara 2;
  - prenos težine preko `EstimateOneRepMax` obara 1;
  - maksimum i za opseg preko 12 obara 1;
  - poređenje na istoj težini posle svakog odbijenog para obara 1.
- Na klijentu, na 192 testa: granica editora uvek 12 obara 1, a bez vraćanja u polje obara 1.
- End-to-end, uživo:
  - blok hipertrofije (Upper/Lower, srednji nivo, linearan): složene vežbe 8–12, izolacije
    10–20 @RIR 2 u nedelji 1. U nedelji 5 je 6–10 i 8–18 @RIR 1.
  - blok snage: bench, veslanje, čučanj i RDL 3–6 @RIR 3, izolacije 10–20 @RIR 3.
  - bočno podizanje 8 kg × 20 @RIR 2 → sledeće nedelje **10 kg** × 10–20, bez produženog cilja.
  - kroz promenu propisa: 8 kg × 12 @RIR 2 u nedelji 1 (procena 11.7), 10 kg × 20 u nedelji 2
    (bez procene) → nedelja 3, sa propisom 10–20 @RIR 1, dobija **12 kg**.
  - lični šablon: bočno podizanje 12–20 se čuva. Bench Press 8–15 se odbija (400, „Bench Press
    je složena vežba i ide najviše do 12 ponavljanja…"), a 12–21 odbija anotacija.
  - editor (375 px, bez prelivanja): Lateral Raise dobija 10–20 sa granicom 20, a Bench Press
    8–12 sa granicom 12. Upisanih 15 u bench vraća polje na 12.
  - predlog pri čitanju: nedelja 3 preskočena, nedelja 4 (9–19 @RIR 1) nudi bočno podizanje
    **12 kg** i Cable Fly 12.5 kg sa tekstom „Predlog je prenet sa poslednje težine ove vežbe
    u bloku…". Bench u istom treningu i dalje nosi predlog iz maksimuma.

## Ograničenja

- Grafik e1RM trenda za izolacije je većinom prazan, jer procenu daju samo serije do 12.
  Poruka na grafiku to već kaže.
- Postojeći blokovi i ranije sačuvani lični šabloni zadržavaju opsege sa kojima su napravljeni.
- Unutar bloka izolacija nosi svoju težinu kroz svaki propis, i kad je trening preskočen.
  **Novi blok** je i dalje
  počinje iz procene na zapisu. Ta procena najčešće dolazi iz prvih treninga posle koraka,
  na težini na kojoj je vežbač i završio, pa blok kreće sa te težine na dnu opsega. Ako je
  procena iz ranije, lakše težine, novi blok kreće lakše i dupla progresija ga vraća za
  nekoliko nedelja. To nije mereno na stvarnim podacima.
