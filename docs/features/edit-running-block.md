# Izmena bloka koji teče: dodavanje i uklanjanje vežbe

**Grana:** `feature/edit-running-block`

Runda 20, stavka 20.1. Odluka vlasnika: menja se samo sastav dana (dodavanje i uklanjanje vežbe).
Menjanje modela periodizacije već generisanog bloka ostaje van aplikacije.

## Problem

Generisan blok se nije mogao menjati: jedino što se moglo bilo je da se vežba zameni sličnom (A2). Vežbač
koji želi bočno podizanje na dan sa potiskom, ili hoće da izbaci vežbu koja ga boli, morao je da pravi nov
plan, a nov plan povlači nove maksimume i nov raspored.

## Šta je urađeno

- **Propis vežbe je jedna funkcija (`ExercisePrescriptions.For`, domen).** Generator je račun radio ugrađen u
  petlju koja pravi blok. Dodavanje vežbe traži iste brojeve (opseg iz cilja i tipa vežbe, RIR iz cilja, serije
  iz nivoa, pomeraj iz modela i nedelje), pa su dve kopije bile pitanje vremena kad će se razići. Generator sada
  zove ovu funkciju; svih 1413 testova koji su postojali prošlo je bez izmene, pre bilo kakvog novog koda.
- **Gde važi.** U izabranom treningu i u svakom treningu **istog dana** do kraja bloka koji još čeka, kao kod
  zamene. Izabrani trening je uključen i kad je u toku. Trening sa upisanom serijom je počet, ma šta piše u
  statusu (isto pravilo kao kod balansiranja nedelje), pa ne dobija i ne gubi vežbu. Odrađeni i preskočeni
  treninzi i raniji treninzi istog dana ostaju.
- **Dodavanje.** Vežba ide na kraj dana. Dobija ono što bi joj blok propisao da je od početka u šablonu:
  `PrescribedSets`, opseg, osnovni opseg (`BaseRepRangeMin/Max`) i RIR iz propisa te nedelje, a `TargetWeightKg`
  ostaje prazan, pa se težina računa pri čitanju iz maksimuma (runda 12). Ponuda su sistemske i vežbačeve
  vežbe koje se rade sa opterećenjem (isto pravilo kao kod zamene: Plank se ne nudi) i koje dan još nema, sa
  mišićem koji direktno rade.
- **Uklanjanje.** Vežba se uklanja iz istih treninga. Dan mora da zadrži bar jednu vežbu (ni jedan trening koji
  bi ostao prazan ne sme da se izmeni, da dan u nekim nedeljama ne ostane bez sadržaja), a redosled se zbija:
  prva vežba u danu je i kalibraciona (`CalibrationSet`), pa rupa na početku ne sme da ostane.
- **Balansiranje.** Posle izmene se nedelje koje čekaju ponovo balansiraju, osim kad je vežbač izabrao „Prati
  moj šablon". Izmena i balansiranje su u jednoj transakciji.
- **Blok koji je imao rasterećenje zbog umora ne prima nove vežbe.** Posle njega nedelje ne prate raspored
  bloka: oslobođena planirana nedelja preuzima fazu one koja je postala rasterećenje (runda 5), pa propis po
  broju nedelje nije onaj koji nedelja nosi. Uklanjanje je u takvom bloku moguće.
- **API.** `GET /api/sessions/{id}/addable-exercises`, `POST /api/sessions/{id}/exercises` i
  `DELETE /api/sessions/plans/{planId}`. Sva tri su ograničena na korisnika.
- **Ekran.** „Dodaj vežbu" na dnu treninga koji čeka ili traje, dok dan ima manje od 12 vežbi. „Ukloni vežbu"
  na kartici, pod istim uslovom kao zamena, i dok u danu ostaje bar jedna druga. Obe su male komponente
  (`AddExercise`, `RemoveExercise`) sa zajedničkim stilom, da `workout-session.scss` ne raste.

## Odluke vredne pamćenja

- **Čitanje polaznog broja serija po mestu u danu moralo je da se suzi.** Deload polovi polazni broj serija iz
  osnovne nedelje (runde 10 i 14), a vežbu koja tamo nema reda (zamenjenu, a sada i dodatu) tražio je po rednom
  broju u danu. To je bilo tačno za zamenu, koja čuva mesto. Posle uklanjanja se redosled zbije, pa dodata vežba
  dobija broj koji je nosila druga vežba koja je i dalje tu, i deload bi joj pripisao njen broj serija. Mesto se
  sada preuzima samo kad je vežba koja ga je držala otišla iz tog dana.
- **Polazni broj dodate vežbe se izvodi, i to je ovde tačno.** Izvođenje (`BaseSetsFrom`) nije bezbedno za redove
  starijih pravila i za donju granicu serija, zbog čega je runda 10 čitala osnovnu nedelju. Redovi koje piše
  ova funkcija nisu stariji pravilo, a polazni broj iz nivoa je ≥ 3, pa donja granica ne radi. To je provereno,
  ne pretpostavljeno: test prolazi svaku kombinaciju cilja, tipa, modela i nivoa i u svakoj nedelji koja nije
  rasterećenje izvodi nazad broj koji nivo propisuje.
- **Poznata granica: ukloni pa dodaj (ili zameni) na isto mesto.** Ako se ukloni poslednja vežba dana i doda
  druga, dodata nasleđuje polazni broj serija uklonjene, jer se podaci ne razlikuju od zamene (isto mesto, nova
  vežba). Isto važi kad se zamenjena vežba nađe na mestu uklonjene. Razlika postoji samo kod ličnog šablona sa
  različitim brojem serija po vežbi, a posledica je broj serija u rasterećenju.
- **Razlog odbijanja ne može da zavisi od teksta greške.** Server u produkciji ne šalje tekst izuzetka
  (`ProblemDetails.Detail` je isti kao `Title`, vidi `docs/security.md`), a `extractErrorMessage` čita `Title`.
  Uživo je odgovor na odbijeno dodavanje bio `"title": "Request conflicts with current state."`, a poruka na
  srpskom je stajala samo u `detail`, koji u produkciji nije tu. Zato ekran ne nudi ono što server odbija
  (završen trening, jedina vežba dana, pun dan), razlog koji se ne može znati unapred stiže kao podatak u
  odgovoru `addable-exercises` (`unavailable`), a za trku (stanje se promenilo između klika i odgovora) postoji
  srpska poruka po statusu (`blockEditError`).

## Šta je pregled našao (ispravljeno)

Pregled je radio agent koji samo čita; nalaz 1 sam ponovio testom pre ispravke (crven: 2 serije umesto 5).

- **Vežba dodata kasno ostajala je sa propisom rasterećenja u nedelji koja više nije rasterećenje** (greška u
  podacima). Dodaj vežbu u petoj nedelji dok je četvrta još na čekanju: postoji samo u petoj i u šestoj (planirani
  deload). Kad umor četvrte nedelje povuče deload na petu, šesta se oslobađa i preuzima fazu pete, a polazni broj
  serija se traži u osnovnoj nedelji i u trenažnim nedeljama - dodata vežba nije ni u jednoj, jer se „referenca"
  ne pravi iz nedelje koja je postala deload ni iz one koja se oslobađa. Plan je tada preskakan (`continue`), pa je
  šesta nedelja, običan trening, ostajala sa 2 serije uz RIR 4 na toj vežbi, a sa punim propisom na ostalim.
  Do toga ne može doći sa zamenom, jer ona ne pravi vežbu koja postoji samo u poslednje dve nedelje. Ispravljeno:
  vežba koja nigde nema osnovu kreće od serija koje propisuje nivo bloka, što je za dodatu vežbu uvek tačno.
  Nijedan raniji test nije pokrivao `RestorePlannedDeloadAsync` sa dodatom vežbom; oba testa deload-a iz ove grane
  su stizala samo do `ApplyDeloadAsync`.
- **Poruka za odbijeno uklanjanje je savetovala nešto što ne pomaže.** Uklanjanje je odbijeno kad bi neki kasniji
  trening ostao bez vežbe (dan A: u petoj nedelji ukloni A, pa iz treće ukloni B), a ekran je govorio „osveži ekran".
  Ekran ne može da zna za kasnije nedelje, a razlog u produkciji ne stiže (isti problem zbog koga je `Unavailable`).
  Poruka sada navodi oba uzroka, bez saveta koji ne pomaže.
- **Dve rečenice u dokumentaciji su tvrdile više nego što kod radi** (započeti treninzi; trening iz koga se menja se
  menja i kad je u toku), i jedna granica nije bila navedena (pun trening se preskače tiho). Ispravljeno gore.
- Proveravano i bez primedbe: izbor ciljnih treninga, redosled posle dodavanja i uklanjanja, praćenje u EF-u,
  vlasništvo nad svim upitima, transakcija, dvostruko slanje, tokeni tema u `_block-edit.scss`, i da ograničenje
  na mesto u deload-u ne lomi zamenu (jedini pao test je onaj koji je namenjen tom ograničenju).

## Šta je usput nađeno

- **Poruke posle zamene, preskakanja i vraćanja na plan nisu nestajale.** Tri mesta su postavljala poruku
  direktno, a gasi je samo `showToast`. Poruka stoji iznad donje trake i pokrivala je dugme ispod nje: posle
  prve izmene se „Dodaj vežbu" nije mogla pritisnuti, što se videlo tek pri pravom pritisku (traka je bila
  ispod poruke po `elementFromPoint`). Sve ide kroz `showToast`.
- **9 px prelivanja na 375 px.** Izbor vežbe traži širinu najduže opcije („Single-Leg Romanian Deadlift
  (Hamstrings)"), što je ista greška koju je runda 8 našla na `/onboarding`. Rešenje je isto:
  `grid-template-columns: minmax(0, 1fr)`.
- **Test koji je padao zbog načina na koji je pisan.** Nov red sa unapred zadatim ključem, dodat u kolekciju već
  praćenog roditelja, EF vidi kao postojeći pa traži UPDATE (`DbUpdateConcurrencyException`). Servis dodaje preko
  skupa (`_db.ExercisePlans.Add`) i bio je ispravan; u testovima su isto uradili samo oni koji su pravili
  serije.

## Provera

- `dotnet test`: **1634** (bilo 1413); `npm test`: **450** (bilo 425); `dotnet build` i `npm run build` prolaze.
  Dva stila (`plan-home`, `one-rep-max-setup`) i `workout-session.scss` su i dalje iznad 6 kB upozorenja, kao
  pre grane; `workout-session.scss` je netaknut (6.48 kB), jer su nove komponente dobile svoj stil.
- Merenje vraćanjem, jedna izmena po merenju, na zapisanom stablu. Server (222 testa u 4 klase):

| Vraćeno | Obara |
|---|---|
| mesto u deload-u se preuzima i kad vežba koja ga je držala još stoji | 1 |
| bez balansiranja posle izmene | 1 |
| dodavanje u blok koji je imao rasterećenje zbog umora | 1 |
| redosled se ne zbija posle uklanjanja | 2 |
| trening sa upisanom serijom je meta izmene | 1 |
| može da se ukloni jedina vežba dana | 1 |
| raniji trening koji čeka dobija vežbu | 1 |
| dodata vežba ne gleda nedelju (uvek osnovni propis) | 1 |

  Klijent: poruka posle dodavanja ne nestaje 1; poruka posle zamene 1 (pre posebnog testa 0); poruka posle
  preskakanja 1 (pre posebnog testa 0, kao i za zamenu); jedina vežba dana ima „Ukloni" 1; pun dan nudi
  dodavanje 1; izmena prepisuje nesačuvane unose 1; opšti naslov servera umesto srpske poruke 6; razlog koji
  stiže kao podatak se ne prikazuje 1.

- **Generator: 0 od 1627 testova.** Zajednička funkcija je testirana (192 + 1 slučaj, uključujući proveru da
  se polazni broj serija izvodi nazad iz svake nedelje), ali ništa nije pokretalo pravi `MesocycleGenerator`.
  Kad generator zaboravi tip vežbe, serije iz ličnog šablona, njegov opseg ili nivo bloka, ceo skup je ostajao
  zelen; kad zajednička funkcija ignoriše tip vežbe pada samo njen sopstveni test. To je isti oblik kao
  `SetLogDto` (runda 9) i izolacija (runda 10): pravilo je pod proverom, a njegova primena nije. Dodata je
  `MesocycleGeneratorPrescriptionTests` (pravi katalog preko `DbSeeder`, pravi šabloni, brojevi upisani, a ne
  izračunati istom funkcijom): iste četiri izmene sada obaraju 2, 1, 1 i 2 testa.
- Uživo, lokalni API i 375 px, nalog za testiranje, blok snage od četiri nedelje, Day A druge nedelje:
  - dodat je Incline Dumbbell Curl: u nedeljama 2-4, ne i u prvoj; 10-20 ponavljanja uz RIR 2, a u nedelji
    rasterećenja 2 serije uz RIR 4 (ostale vežbe dana imaju isto);
  - uklonjen kroz ekran: kartica nestaje, ostaje 6 vežbi, poruka „Vežba je uklonjena do kraja bloka." nestaje
    posle ~3 s; vraćen kroz „Dodaj vežbu" sa izborom, i nova kartica ima 0 / 6, 10-20, RIR 2;
  - završen trening: sva tri poziva daju 409, bez tokena 401;
  - bez prelivanja posle ispravke (pre nje 9 px).

## Šta ovo ne radi

- Ne menja model periodizacije ni šablon već generisanog bloka.
- Ne dodaje vežbu u blok koji je imao rasterećenje zbog umora, ni u druge započete treninge (trening iz koga
  se menja menja se i kad je u toku).
- Trening koji je već pun (12 vežbi) preskače se tiho: dodata vežba tada postoji u nekim nedeljama dana, a u
  tom ne. Ekran to ne kaže.
- Ne nudi vežbu bez opterećenja (Plank).
- Ne pamti uklonjenu vežbu: ako se kasnije doda, kreće od propisa bloka.
- Dva istovremena dodavanja iste vežbe nisu zaštićena jedinstvenim ključem u bazi (kao ni zamena); transakcija
  štiti od polovične izmene, ne od trke dva zahteva.
