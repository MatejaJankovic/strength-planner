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

- Testovi: 1352 → 1378 na serveru (18 za domensko pravilo, 6 za servis, uz ostale), 239 → 252 na
  klijentu (redovi, izbor opsega i mere, greška).
- **Živa provera je našla dva problema u mom dizajnu.** Podrazumevani opseg je počinjao sa 22 prazna
  reda (gore), a tonaža je ovde bila pisana kao `19 920 kg` dok sekcija odmah iznad piše `41,250 kg`;
  sada je ista oblika kao ta sekcija.
- Uživo, 375 px, na test nalogu u razvojnoj bazi: serije prvog plana pomerene unazad za tri i dve nedelje
  (SQL, samo taj nalog), pa drugi plan odrađen danas. Odgovor servera i ekran pokazuju dva bloka sa
  naslovima („Live check", „Drugi plan"), nedelju 16. nov kao prazan red i ukupno 54,760 kg; `scrollWidth`
  375 = `clientWidth` 375.

## Šta nije pokriveno

- Nema poređenja blok sa blokom (razlika u procentima) ni prosek po nedelji bloka: pregled pokazuje
  nedelje i granice, a poređenje prepušta oku.
- Nedelja se računa po UTC-u: serija podignuta u nedelju uveče po lokalnom vremenu može da upadne u
  sledeću nedelju ako je vežbač istočno od UTC-a. Serveru nedostaje vremenska zona naloga.
- Ceo skup serija opsega se čita i sabira u memoriji (kao i u nedeljnom volumenu). Za desetine hiljada
  serija bi se zbir preneo u bazu.
- Ni ovde, kao ni u drugim pregledima, nema izvoza: to je zasebna stavka.
