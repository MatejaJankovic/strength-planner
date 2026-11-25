# Istorija treninga

Runda 18, grana 3 od 4. Analitika čita jedan blok, a ekran plana tekući, pa je blok koji se
završio nestajao iz vida. Nije postojalo mesto koje nabraja šta je stvarno odrađeno.

## Šta je urađeno

- `GET /api/history?page=&pageSize=` vraća završene i preskočene treninge kroz sve planove i blokove,
  od najnovijeg (`TrainingHistoryService`). Po treningu: dan, nedelja, plan (naziv plana, ili
  bloka kad je sam), cilj, broj serija, tonaža (sa telesnom masom, kao u analitici) i vežbe koje
  su imale serije. Strane se stežu na 1..50, ne odbijaju.
- Klijent: ekran `/history` (četvrta pločica na dashboardu profila), grupisan po mesecima, „Učitaj
  još", prazno stanje i greška sa ponovnim pokušajem. Dodir otvara trening, pa i ispravku iz #118.
- Spisak živi u ekranu, ne u servisu koji živi na nivou cele aplikacije: ništa ne ostaje iza
  odjave (treća runda).

## Datum

Poređa se po tome kad je trening urađen, a to nije uvek zapisano (`HistoryDate`, domen):
vreme završetka, pa poslednja serija (trening završen pre nego što se vreme beležilo), pa datum
koji mu je plan dao, pa nedelja bloka u kojoj je bio. Poslednje dve su pretpostavke i
`dateKnown` je tada `false`; ekran piše „nedelja N", ne datum.

## Merenja

- **Prvi test je našao grešku u mom kodu.** `Dictionary.GetValueOrDefault` na `DateTime` vrati
  `0001-01-01`, a ne „nema", pa je trening bez serija dobio „poznat" datum iz godine 1 i pao na
  kraj spiska. Tri testa su pala; popravljeno traženjem u rečniku.
- **Preskočen trening je uživo ispao u budućnosti.** Datum izveden iz plana za nedelju 2 je
  30. novembar, a danas je 25., pa je preskočen trening stajao iznad svega odrađenog. Preskakanje
  se desilo u prošlosti, a nema `SkippedAt`, pa se izveden datum steže na „sada" (`HistoryDate.Of`
  uzima `now`; stvaran datum se nikad ne dira). Dva testa.
- Testovi: 1327 → 1352 na serveru (15 za istoriju, uz ostale iz pregleda #118), 223 → 239 na
  klijentu (oblikovanje i ekran).
- Vraćanje pravila na committed stablu: poredak od najstarijeg ruši 2 testa; trening bez serija sa
  podrazumevanim datumom 3; izveden datum koji se ne steže na „sada" 1; upit zaglavlja bez
  filtra po korisniku 1 (test sklopa ima i globalni filter vlasnika, pa tuđi nalog vidi prazno
  samo uz eksplicitan uslov).
- Uživo, 375 px: istorija sa tri treninga (dva završena, jedan preskočen), plan i nedelja, 35 i
  34 serije, tonaža 19 920 i 21 330 kg, vežbe skraćene na tri uz „+3"; `scrollWidth` 375 =
  `clientWidth` 375. Pločica „Istorija" na profilu vodi na ekran.

## Šta je pregled našao (ispravljeno)

- **Neuspelo „Učitaj još" je brisalo ceo spisak** i nudilo „Pokušaj ponovo" koje vraća na prvu
  stranicu. Sada greška stoji uz dugme, učitano ostaje, a ponovni pokušaj traži istu stranicu.
  Isti trening koji bi se pojavio na dve stranice (završen između dva čitanja) prikazuje se jednom.
- **Serija dodata pri ispravci starog treninga ga je vraćala na današnji dan.** Trening bez
  vremena završetka datira se po poslednjoj seriji, a nova serija je dobijala `PerformedAt = sada`.
  Sada serija dodata pri ispravci dobija vreme poslednje serije tog treninga. Test je prvo pao
  jer je moja postavka imala sledeći trening iste nedelje već završen, pa je prozor ispravke bio
  zatvoren: ispravka je u pravu, a test je morao da ga otvori.
- **Veliki broj strane je prelivao ofset** (`(page - 1) * pageSize` u `int`): od oko 43 miliona uz
  50 po strani vraćao je prvu stranicu umesto prazne. Ofset je sada `long`.
- Izjednačenje datuma, nedelje i dana je davalo nestabilan poredak između stranica; dodat je
  poslednji ključ (`Id`).
- Manje: upit poslednje serije čita samo treninge bez vremena završetka (ostalima ne treba);
  „21 trening" umesto „21 treninga"; preskočen trening više ne piše „nedelja N" dvaput; dugme za
  još ima `aria-busy`; test prekoračenja strane proverava tačne granice (1 i 50), a testovi više ne
  zavise od sata (datumi su iz 2024).

Pregled navodi i da dugme za nazad iz otvorenog treninga uvek vodi na početni ekran, ne na
istoriju (postojeće ponašanje ekrana treninga, nije menjano), i da datum zavisi od trenutka
završetka, ne od dana kad je rađeno (ko trenira u ponedeljak a „Završi" klikne u četvrtak vidi
četvrtak).

## Šta nije pokriveno

- Ceo spisak se učitava iz baze za jedan upit zaglavlja pa se poređa i deli u memoriji (stotine
  malih redova; u razvojnoj bazi 425 završenih treninga jednog vlasnika). Za desetine hiljada
  treninga bi trebalo preneti poređanje u bazu; datum nije kolona, pa to nije trivijalno.
- Nema filtera (po planu, po vežbi) i pretrage. Istorija je spisak, ne analitika.
- Obrisan plan ne ostavlja treninge u istoriji: brisanje plana briše i njegove blokove (sedma
  runda). To piše u uputstvu.
