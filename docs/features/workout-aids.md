# Pomagala na treningu: pauza, ploče i zagrevanje

Runda 19, grana 3 od 5 (beleške i tamna tema ostaju). Telefon se između serija drži u ruci, pa ekran treninga
sad kaže ono što se inače računa napamet ili na satu: koliko traje pauza, koje ploče idu na šipku i čime se
zagrejati.

## Šta je urađeno

- **Pauza između serija.** `RestTimerService` + traka `app-rest-timer`. Kreće kad se sačuva serija na
  treningu u toku, ostaje zalepljena uz vrh ekrana, i pokazuje vreme uz cilj (1:30, 2:00, 3:00, 5:00 ili
  bez cilja). Kad cilj istekne, traka pozeleni, piše „Pauza je istekla" i telefon jednom zavibrira (gde
  pregledač to dozvoljava); sat nastavlja da broji.
- **Ploče.** Za složenu vežbu sa šipkom ispod težine piše šta ide sa svake strane (šipka 20 kg, ploče 25, 20,
  15, 10, 5, 2,5, 1,25 kg), a ako se težina ne može sastaviti, koliko ostaje.
- **Zagrevanje.** Pre prve radne serije iste vežbe: 40%, 60% i 80% radne težine (5, 3 i 1 ponavljanje),
  zaokruženo na korak vežbe; blizu same šipke je to prazna šipka. Podsetnik, ne serija: ne upisuje se, ne
  ulazi u volumen i ne dira progresiju.
- **Server (jedno polje).** `ExercisePlanDto.ExerciseType` ("Compound"/"Isolation"), popunjeno tamo gde je i
  `Equipment`, na putanji koju čita ekran treninga. Bez migracije: tip već stoji na vežbi. Prvi commit grane
  kaže „client-only"; polje stiže u sledećem, kad se na živom ekranu pokazalo da oprema nije dovoljna.

## Odluke

- **Vreme se računa po satu zida, ne brojanjem otkucaja.** Telefon između serija je upravo slučaj u kome
  pregledač usporava `setInterval` u pozadinskoj kartici; otkucaj samo osvežava prikaz, a tačnost ne zavisi od
  njega (test pomera sat zida bez otkucaja i očekuje pravu pauzu).
- **Pauza kreće pri čuvanju serije, ne kad server odgovori**, jer je serija odrađena u trenutku dodira. Ne
  kreće dok se ispravlja završen trening (tada se ništa ne diže), i staje kad se trening završi ili ekran
  napusti.
- **Cilj se pamti na telefonu, ne na nalogu.** `localStorage` pod ključem `strength-planner.rest-target`, u
  `try/catch`: privatan prozor ili blokiran prostor ostavljaju cilj važećim do kraja sesije.
- **Biranje cilja je sklopljeno iza dugmeta sa zastavicom.** Traka je zalepljena, pa svaki njen red oduzima
  prostor ekranu pred svaku seriju. Cilj se postavlja jednom; prvi oblik je imao izbor stalno vidljiv i bio
  visok 108 px, sada je 50 px.
- **Ploče i zagrevanje samo za složenu vežbu sa šipkom.** Pregib i skull crusher su u katalogu „Barbell", ali
  se rade lakom ili EZ šipkom; šipka od 20 kg i rampa tu zavaravaju. Oprema sama ne razlikuje čučanj od
  pregiba, pa je trebao tip.
- **Pretpostavka o šipci se piše u tekstu** („Po strani (šipka 20 kg): ..."), a težina manja od šipke se ne
  komentariše: aplikacija ne zna koju šipku lifter ima, pa bi „manje od šipke" tvrdilo nešto što ne zna.
- **Ploče se prikazuju dok se trening sprema ili traje**, ne na završenom ili preskočenom: tamo se ništa ne
  stavlja na šipku. Zagrevanje samo dok traje.
- **Računica pomagala ostaje na klijentu, ne u domenu.** Plan runde je za nju predviđao domen i xUnit. Ploče i
  zagrevanje su samo prikaz: nijedno pravilo na serveru ih ne čita, pa nema druge kopije koju bi trebalo držati
  usklađenom (za razliku od `step-absorption`, gde progresija na serveru i kartica na klijentu moraju da se slože
  i dele datoteku sa slučajevima). Kad zagrevanje jednog dana bude nešto što server upisuje ili računa, ono se
  seli u `Domain/Algorithms`.
- **Vibracija jednom po dostignutom cilju.** Isti cilj izabran ponovo ne ponavlja je; novi cilj koji je pauza
  već prešla vibrira; nova pauza je ponovo naoružava.

## Merenja

- Testovi: klijent 281 → 360, server 1378 → 1379 (jedan servisni test za tip vežbe).
- **Vraćanje pravila na committed stablu** (`features/workout`, 153 testa u početku), prva serija od 17
  izmena: 16 ruši bar jedan test, jedna ruši build (vidi niže). Tajmer: bez ponovnog osvežavanja kad kartica
  „prespava" 1; cilj 0 računat kao dostignut 1; vibracija na svakom otkucaju 2; isti cilj izabran ponovo 1;
  nova pauza ne naoružava vibraciju 1. Ekran: pauza kreće i pri ispravci 1; ne staje pri završetku 1; ne staje
  pri izlasku 1; zagrevanje i van treninga u toku 1; traka izvađena iz šablona 1. Ploče i zagrevanje: bez
  zaokruživanja ostatka 1; bez izbacivanja jednakih serija 1; serija na ili iznad radne težine 1; izbačena
  grana „prazna šipka" 1; zagrevanje i posle prve serije 2; ploče za svaku opremu 2.
- Druga serija, posle uvođenja tipa vežbe (162 testa): tip ignorisan 6; nepoznat tip računat kao složena 3;
  NaN kao prazna šipka 2; težina manja od šipke dobija tekst 2; pretpostavka o šipci nije napisana 3; blok
  ploča izvađen iz šablona 1; blok zagrevanja izvađen iz šablona 2; ekran uvek pita „Compound" 2 (ploče) i
  1 (zagrevanje). Ograničenje po statusu: 1 od 164. Server: bez `ExerciseType` u mapiranju pada 1 od 1.
- **Uživo, 375 px:** traka se lepi (posle skrolovanja od 926 px stoji na vrhu, 50 px visoka, bez horizontalnog
  preliva); sat otkucava (0:05 → 0:09 za 3,5 s); cilj 1:30 se vidi uz sat, izbor se zatvara i pamti kao 90; po
  isteku piše „Pauza je istekla", `1:40 / 1:30`, a hvatač je dobio tačno jedan poziv `[200,100,200]`; po
  završetku treninga trake nema; u ispravci završenog treninga dodata serija ne pokreće pauzu. Zagrevanje i
  ploče se vide samo uz Overhead Press i Deadlift, ne uz pregib, skull crusher, mašinu i kabl.

## Šta je pregled našao (ispravljeno)

Pregled je radio agent koji samo čita (kopija projekta van repozitorijuma, izmerene tvrdnje), a svaku tvrdnju sam
proverio pre ispravke.

- **Stanje „Pauza je istekla" je prelivalo stranicu bočno na uskim telefonima.** Sat, poruka koja se skuplja i tri
  dugmeta od 44 px delili su jedan red koji se nije prelamao: +32 px na 320 px, +43 px uz sat `12:41 / 5:00`, +9 px
  na 360 px uz sat `10:05 / 5:00`. Moja provera „bez preliva" je merena samo na 375 px. Poruka je sada u svom redu
  i red se prelama; uživo na 320, 360 i 375 px, uz satove do `62:10 / 5:00`, preliva nema ni u jednom stanju.
- **Neaktivna traka je svuda dodavala 16 px.** Host je stavka mreže ekrana i bez sadržaja zauzima red i razmak:
  razmak zaglavlje–kartice je bio 32 px umesto 16, i na planiranom, završenom i preskočenom treningu. Host se sada
  skriva dok je prazan (`:host(:empty)`, komentar koji ostavlja `@if` se ne računa); uživo 16 px.
- **Lepljenje je radilo samo zato što Chromium popušta.** Zalepljena stavka mreže ograničena je svojom oblašću u
  mreži (red visok koliko i traka), a Chromium je ograničava celim kontejnerom. Firefox i Safari tu mogu da se
  ponašaju po specifikaciji, a tad bi bio isti kvar kao u prvoj verziji. Host ekrana je sada kolona (flex), čiji je
  blok celo telo kontejnera u svim pregledačima; uživo traka ostaje na vrhu posle skrolovanja od 1500 px, a
  planiran, u toku i završen trening zadržavaju pune širine i nemaju preliv. `workout-session.scss` raste sa 6,17 na
  6,19 kB (tvrda granica je 8). **U Firefox-u i Safari-ju nije proveravano.**
- **Tri ponašanja koja su ispravna u kodu nisu držali testovi** (agent ih je pokvario u kopiji i svih 164 ostalo
  zeleno): drugi `start()` je pravio drugi otkucaj (`stop()` bi očistio samo poslednji), izmena već sačuvane serije
  bi pokrenula pauzu, i zagrevanje na završenom ili preskočenom treningu (moj test sa naslovom „ni posle završetka"
  otvarao je samo planiran). Sva tri sada imaju test; svaki, vraćen na staro, ruši po jedan.
- **Šum pokretnog zareza u zagrevanju** (`24.400000000000002 kg`) za korak koji nije stepen dvojke; agent je
  izbrojao 3806 od 13224 oznaka sa takvim korakom. Ista bolest kao u `plates.ts`, ispravljena samo tamo. Sada
  se zaokružuje na dve decimale, uz test preko 11 koraka i težina 25–300 kg.
- **Oprema se poredila osetljivo na velika slova**, iako server poredi bez toga i čuva vežbu napravljenu preko API-ja
  kako je uneta (`load-input-note.ts` to već radi). `barbell` sada važi isto što i `Barbell`.
- **`platesFor(Infinity)` ne bi stao** (`Infinity - 25` je opet `Infinity`). Ekran ne može da napravi takvu
  težinu, pa je zaštita jeftina. Njeno vraćanje na staro nije mereno: test bi se zakačio u beskonačnoj petlji
  umesto da padne.
- **Otvoren izbor cilja se vraćao sa sledećom pauzom.** Komponent živi koliko i ekran, pa je `choosing` preživljavao
  zatvaranje trake. Sada se gasi čim pauze nema (dugme ili kraj treninga); vraćanje ruši 2 testa.
- **Vodič je tvrdio više nego što je beleška priznavala**: „pokazuje pravu pauzu i kad je telefon bio zaključan".
  Pauza se ne pamti, pa osvežavanje stranice ili izbacivanje aplikacije iz memorije (iOS) je gasi; vodič to sada
  kaže, kao i ono što se dešava kad čuvanje serije ne uspe (pauza ostaje, novi pokušaj je vraća na nulu; test
  postoji, vraćanje ruši 1).
- **Nije menjano:** forma za sopstvenu vežbu početno nudi „Barbell" i „Compound", pa vežba sačuvana bez dodira oba
  izbora dobija ploče i zagrevanje. Oba izbora su vidljiva i korisnik ih postavlja sam; šta forma nudi kao
  početnu vrednost je odluka o formi za sopstvenu vežbu, ne o ovoj grani.

Merenje vraćanjem posle ispravki (173 testa): drugi `start()` bez zaštite 1; izmena serije pokreće pauzu 1;
zagrevanje na završenom i preskočenom 1; neuspelo čuvanje zaustavlja pauzu 1; zaokruživanje zagrevanja 1; oprema
osetljiva na velika slova 1; otvoren izbor cilja ostaje 2.

## Šta nije proveravano

- **Vibracija na pravom telefonu.** Uživo je provereno da se `navigator.vibrate` poziva jednom sa pravim
  obrascem; da li telefon zaista vibrira zavisi od uređaja (iPhone uglavnom nema Vibration API).
- **Pauza u pozadinskoj kartici na telefonu.** Računa se po satu, pa bi trebalo da prikaže pravo vreme kad se
  vratiš, ali to je pokriveno testom koji pomera sat, ne telefonom koji je bio zaključan.
- **Lepljenje u Firefox-u i Safari-ju.** Uživo je meren samo Chromium. Raspored je sada takav da specifikacija
  ne ostavlja dvosmislenost (stavka fleksa, blok je ceo kontejner), ali to nije isto što i proveriti.
- Obaveštenje dok je ekran zaključan ne postoji: traka se gleda, ne sluša. Zvuk takođe ne.

## Šta je ova grana namerno ostavila

- Šipka i ploče se ne mogu podesiti (šipka od 15 kg, bez ploče od 1,25...). Spisak važi okvirno i tako piše u
  vodiču.
- EZ šipka i fiksne šipke nemaju ni ploče ni zagrevanje; to je namera, ne propust.

## Šta je grana naučila usput

- **Prva verzija trake nije se lepila, a svi testovi su bili zeleni.** `position: sticky` stoji na samoj traci,
  a lepi se samo unutar roditelja, koji je host element visok koliko i traka. jsdom ne računa raspored, pa
  nijedan test to nije mogao da vidi; našlo se tek uživo (traka na −222 px posle skrolovanja). Lepi se host.
  Isti oblik kao SetLogDto u rundi 9: pravilo postoji, test postoji, a ono što korisnik vidi nije proveren.
- **Test koji tvrdi da čuva zaokruživanje, a ne može da padne.** „Pokretni zarez ne kvari 2.5 + 1.25" je prošao
  i bez zaokruživanja: sve ploče iz teretane su višekratnici od 1.25 i tačne su u binarnom zapisu. Zaokruživanje
  je potrebno za težinu sa korakom koji nije stepen dvojke (2.33 kg, ostatak 0.8999999999999986), pa test sad
  koristi takvu težinu; zaštita zagrevanja od serije iznad radne težine nije imala nijedan slučaj, pa dobija
  onaj sa korakom od 100 kg. Oba ispravljena pre merenja vraćanjem.
- **Tip vežbe se video tek na pravom ekranu.** Prva verzija je gledala samo opremu i na živom treningu je
  pokazivala „Manje od same šipke (20 kg)" uz pregib od 17,5 kg. Sad je to tip, a ne šipka.
- **Jedna izmena iz prve serije vraćanja ruši build, ne test.** `@if (false) { ... {{ plates }} }` ne prolazi
  prevođenje, pa „padanje" nije merenje testova; blok ploča je pri drugom prolazu izvađen u celosti.
- **Skripta za merenje se pokvarila dvaput pre prvog merenja:** umetanje u Python kroz bash heredoc je pretvorilo
  `\r\n` u stvarne prelome redova usred navodnika, a izlaz `npm test` se čitao u cp1252. Nijedna izmena nije
  primenjena u tim pokušajima; stablo je proveravano posle svakog.
