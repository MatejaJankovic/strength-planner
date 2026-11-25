# Analitika kroz sve blokove

Runda 18, grana 4 od 4. Tonaža i nedeljni volumen su se čitali po jedan blok, pa se nije videlo ni šta
je bilo pre tekućeg bloka, ni kako jedan blok stoji prema prethodnom. e1RM trend i lični rekordi su
već čitali celu istoriju; to je ostalo kakvo je bilo.

## Šta je urađeno

- `GET /api/analytics/trend?weeks=` vraća, po kalendarskoj nedelji (ponedeljak, UTC), tonažu, broj
  serija, stimulativne serije ukupno i po mišiću, blok kome nedelja pripada, plan, da li je deload i
  da li u njoj počinje blok. `weeks` je koliko nedelja unazad (do 520); 0 je cela istorija.
- Grupisanje je domensko pravilo (`WeeklyTrend`, čist kod sa testovima): nedelja pripada bloku sa
  najviše njenih serija (pri izjednačenju onom kojim se kasnije treniralo); deload je nedelja u kojoj
  je bar polovina serija iz deload nedelje; serije se broje istim kreditom kao ekran nedeljnog
  volumena (`StimulativeVolume`), da se dva ekrana ne razilaze o istoj nedelji.
- Klijent: celina „Kroz sve blokove" u analitici: opseg (8 nedelja, 6 meseci, godinu dana, sve), mera
  (tonaža, serije ukupno, serije jednog mišića), trake po nedeljama, naslov bloka iznad nedelje u
  kojoj počinje, oznaka za deload, prazan red za nedelju bez treninga.
- Pregled po bloku ostaje, jer cilj nedelje i granice volumena imaju smisla samo unutar bloka.

## Odluke

- **Po kalendarskoj nedelji, ne po nedelji bloka.** Blokovi su različite dužine (4 ili 6 nedelja) i ne
  počinju istog dana, pa se „nedelja 3" dva bloka ne može staviti jednu uz drugu. Serija se računa u
  nedelju u kojoj je podignuta (`PerformedAt`), a ne kad je trening završen.
- **Nedelje pre prvog treninga se ne prikazuju**, ni u fiksnom opsegu. Prvi nacrt je vraćao tačno
  zadati broj nedelja, pa je podrazumevani opseg od šest meseci otvarao 22 reda „bez treninga" pre
  prvog. Praznine posle početka treninga ostaju: pauza je podatak.
- Mišići se čitaju iz posebnog upita, ne preko `SelectMany` kroz katalog: taj spoj ne prevodi SQLite
  (APPLY), a probni sklop bi onda ostao bez provere servisa (runda 17).

## Merenja

- Testovi: 1352 → 1378 na serveru (20 za domensko pravilo: 12 testova i 8 slučajeva teorije, i 6 za
  servis), 239 → 258 na klijentu (redovi, izbor opsega i mere, greška, ponovni pokušaj).
- Vraćanje pravila na committed stablu: nedelja koja počinje nedeljom ruši 8 testova; vodeće prazne
  nedelje zadržane 7; kredit serije ignorisan 2; deload samo kad su sve serije deload 1; telesna masa
  izostavljena iz tonaže 1.
- **Živa provera je našla dva problema u mom dizajnu.** Podrazumevani opseg je počinjao sa 22 prazna
  reda (gore), a tonaža je ovde bila pisana kao `19 920 kg` dok sekcija odmah iznad piše `41,250 kg`;
  sada je ista oblika kao ta sekcija.
- Uživo, 375 px, na test nalogu u razvojnoj bazi: serije prvog plana pomerene unazad za tri i dve nedelje
  (SQL, samo taj nalog), pa drugi plan odrađen danas. Odgovor servera i ekran pokazuju dva bloka sa
  naslovima („Live check", „Drugi plan"), nedelju 16. nov kao prazan red i ukupno 54,760 kg; `scrollWidth`
  375 = `clientWidth` 375.

## Šta je pregled našao (ispravljeno)

- **Izbor mišića i prikaz su se razilazili posle promene opsega.** Dok se učitavao drugi opseg, lista
  mišića je bila prazna, pa su opcije nestale i pregledač je izabrao prvu („Tonaža"), a mera je ostala
  mišić: lista je pisala tonažu dok su redovi pokazivali serije mišića. Isti zamku sa `[value]`
  koja se ne prepisuje, kad se signal nije promenio, opisuje i `profile-home.ts`. Lista sada pamti
  mišiće iz poslednjeg odgovora dok se učitava novi, a mišić koji u novom opsegu nije trenirao vraća
  meru na tonažu, pa lista i redovi govore isto. Dva testa; pre ispravke prvi pada.
- **„Pokušaj ponovo" nije radio** za isti opseg: dodir na aktivni opseg ne menja signal, pa nema
  zahteva. Greška sada ima dugme koje ponavlja isti opseg.
- Tekuća nedelja je dobila oznaku „u toku" (njen zbir je delimičan i izgledao je kao pad, što je
  upravo ono što pregled treba da spreči); naslov bloka ne ponavlja ime plana kad ga ime bloka već
  sadrži; uz granicu godine uz nedelju stoji i godina; dugačko ime bloka se lomi umesto da probije
  ekran; granica od 520 nedelja je imenovana konstanta; mrtva dodela u `WeeklyTrend.Build` je
  uklonjena; test koji je tvrdio da proverava upit sa opsegom (a opseg filtrira i domen) je
  preimenovan u ono što stvarno proverava.
- Navodi o zastarelim izjavama: `CLAUDE.md` i `docs/teza-uskladjivanje.md` su još navodili „analitiku
  cele istorije" kao van obima, a uputstvo je u istoriji tvrdilo da analitika čita jedan blok.

## Šta nije pokriveno

- Nema poređenja blok sa blokom (razlika u procentima) ni prosek po nedelji bloka: pregled pokazuje
  nedelje i granice, a poređenje prepušta oku.
- Nedelja se računa po UTC-u. Istočno od UTC-a (Srbija, UTC+1 ili +2) serija upisana u prvim satima
  ponedeljka po lokalnom vremenu (pre 1 ili 2 ujutru) pada u prethodnu nedelju; zapadno od UTC-a bi u
  sledeću upala nedelja uveče. Serveru nedostaje vremenska zona naloga. (Prvi tekst ove rečenice je
  pokazivao na pogrešnu stranu.)
- Ceo skup serija opsega se čita i sabira u memoriji (kao i u nedeljnom volumenu). Za desetine hiljada
  serija bi se zbir preneo u bazu.
- Ni ovde, kao ni u drugim pregledima, nema izvoza: to je zasebna stavka.
- Pregled navodi i da `trend.scss` ponavlja pravila `tonnage.scss` (traka, red): treća kopija je
  povod za izdvajanje, a ovo je druga. Istorija se čita sa današnjim težinama mišića po vežbi, kao i
  nedeljni volumen, pa se stare nedelje preračunaju kad se katalog promeni.
