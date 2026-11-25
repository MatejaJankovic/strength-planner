# Beleške uz trening i uz vežbu

Runda 19, grana 4 od 5 (tamna tema ostaje). Podešavanje sprave („sedište 4, pin 7"), napomena koja štiti
rame, ili kako je trening prošao: do sada nije imalo gde da se upiše, pa se pamtilo ili gubilo.

## Šta je urađeno

- **Beleška uz vežbu** (`UserExerciseNote`: korisnik, vežba, tekst). Pripada korisniku i vežbi, ne planu, pa se
  vidi pri svakom treningu te vežbe, u ovom bloku i u svim sledećim.
- **Beleška uz trening** (kolona `Note` na `WorkoutSession`).
- Dva `PUT` poziva (`/api/exercises/{id}/note`, `/api/sessions/{id}/note`), beleške na DTO-u treninga koji ekran
  već čita, i beleška treninga u unosu istorije.
- Ekran treninga: komponenta `app-note-field` pod zaglavljem (trening) i na svakoj kartici (vežba). Istorija
  prikazuje belešku treninga skraćenu na dva reda.
- Migracija `AddWorkoutNotes`: nullable kolona i nova tabela, ništa ne menja u postojećim redovima. Primenjena na
  razvojnu bazu.

## Odluke

- **Beleška uz vežbu je po vežbi, ne po planu.** Glavna upotreba je podešavanje sprave, koje se ne menja iz nedelje
  u nedelju. Beleška vezana za plan morala bi da se prenosi iz nedelje u nedelju, a zamena vežbe bi ostavila belešku
  o staroj vežbi na novoj. Ovako zamena samo menja koja beleška se čita; ona o prvoj vežbi ostaje sačuvana za dan
  kad se vraćaš na nju.
- **Beleška nije podatak o treningu.** Ništa u planiranju ni u progresiji je ne čita, pa se piše u svakom statusu
  treninga, i na završenom i na preskočenom, i ne prolazi kroz životni ciklus treninga: direktan `UPDATE` jedne
  kolone, bez provere statusa i bez zastavice ispravke. Uživo provereno na završenom treningu čiji je prozor ispravke
  zatvoren (`canCorrect: false`): beleška se upisuje, a status i `revisionPending` ostaju.
- **Granica je 500 znakova.** Zahtev ograničava tekst kakav je stigao (razmaci se broje), a servis ga proverava
  ponovo posle skraćivanja razmaka. Pravi klijent ne može da pošalje 500 znakova plus razmake (polje broji i
  razmake), pa razliku vidi samo neposredan poziv API-ja: 500 znakova sa razmakom na kraju vraća 400. Server skida
  razmake sa krajeva i vraća ono što je sačuvao; polje prikazuje to, a ne ono što je otkucano. Prazna ili samo
  razmaci znači „nema beleške": red se briše, a ne čuva kao prazan tekst. Broj 500 stoji na četiri mesta (zahtev,
  servis, dve kolone) i u `note-field.ts`, i svako ima test koji pada ako se razilazi.
- **Dva upisa istovremeno.** Prvi upis beleške uz vežbu iz dve kartice ili sa dva uređaja nailazi na jedinstveni
  indeks. Upis je idempotentan, pa se čita i radi ponovo (poslednji upis pobeđuje); ako ni to ne uspe, odgovor je
  409, kao kod koraka opterećenja. Beleška treninga je jedan `UPDATE`, pa tu nema trke.
- **Kad se vežba zameni, kartica počinje ispočetka.** Kartica je vezana za plan, a zamena zadržava id plana, pa
  isti komponent ostaje. Prikazana beleška, otvorena izmena i odgovor na čuvanje koje je krenulo za staru vežbu
  prate id vežbe.
- **Vidi je samo vlasnik**, i filterom vlasništva i uslovom u upitu. Dva korisnika na istoj sistemskoj vežbi imaju
  nezavisne beleške; tuđa sopstvena vežba je 404, ne beleška uz nešto što korisnik ne vidi.
- **Istorija prikazuje samo belešku treninga**, skraćenu na dva reda; cela je na ekranu treninga. Beleške uz vežbe
  se tamo ne prikazuju.
- **Otkucano a nesačuvano se gubi pri izlasku sa ekrana.** Beleška se čuva na „Sačuvaj"; vodič to kaže.

## Merenja

- Testovi: server 1379 → 1413, klijent 360 → 391.
- **Vraćanje pravila na committed stablu, server** (1401 testa): bez skraćivanja razmaka 10; granica merena pre
  skraćivanja 1; bez granice 2; brisanje beleške ostavlja red 1; upis kroz životni ciklus (postavlja zastavicu
  ispravke) 4; beleška uz vežbu nije na DTO-u plana 1; beleška treninga nije na DTO-u 1; istorija bez beleške 1;
  bez kaskade sa naloga 1.
- **Tri izmene daju 0, i to je namerno, ali je jedna od njih otkrila rupu.** Eksplicitna provera vlasnika pri
  upisu beleške treninga, čitanje beleški po korisniku, i sam filter vlasništva: svaka je sama suvišna, jer je
  druga dva pokrivaju. Za filter to nije dovoljno: nijedan test nije tvrdio da postoji. `OwnershipFilterTests` je
  ručno vođen spisak tabela sa korisničkim podacima i nova tabela nije bila na njemu. Sada jeste; bez filtera pada 1.
- **Vraćanje pravila, klijent** (382 testa): beleška uz trening i uz vežbu zamenjene 23; polje ne prati server
  (običan signal umesto `linkedSignal`) 8; sačuvan tekst se ne prikazuje 4; greška se ne prikazuje 1; granica
  dužine skinuta sa polja 1; otkazivanje zadržava otkucano 1; vežbi se šalje id plana umesto id vežbe 13; beleška
  treninga izvađena iz šablona 3; beleška uz vežbu bez vrednosti 1; istorija bez beleške 2; servis šalje belešku
  uz vežbu na adresu treninga 35.
- **Uživo, 375 i 320 px** (dev server, migracija primenjena): beleška uz trening i uz vežbu se čuvaju, server je
  skinuo razmake sa „  Letve na 7, pojas zategnut  "; ista beleška uz vežbu se vidi u treningu u nedelji 3, a
  beleška treninga ne; beleška na završenom treningu; istorija je pokazuje; server prima 500 znakova (200) i
  odbija 501 (400); 500 znakova bez razmaka na 320 px ne prelazi ivicu ni u prikazu ni u izmeni, a istorija ga
  skraćuje na dva reda.
- **Uživo, zamena vežbe:** beleška „Letve na 7" sačuvana uz Back Squat; posle zamene za Front Squat kartica ne
  pokazuje staru belešku i nudi „Dodaj belešku uz vežbu"; posle zamene nazad beleška se vraća sa servera. Polje je
  u fokusu odmah po otvaranju (`activeElement` je `TEXTAREA`).

## Šta je pregled našao (ispravljeno)

Pregled je radio agent koji samo čita (kopija projekta van repozitorijuma, izmerene tvrdnje); svaku tvrdnju sam
proverio pre ispravke.

- **Zamenjena vežba je nasleđivala belešku prethodne, a otvorena izmena je preživljavala zamenu** (greška, srednja).
  Beleška sačuvana uz vežbu A dok je server za nju još govorio „nema" ostajala je na kartici vežbe B kad i B nema
  belešku (ulaz `null` → `null` ne poništava `linkedSignal`), izmena bi sačuvala otkucano uz B, a odgovor na čuvanje
  za A mogao je da stigne na B. Tvrdnja iz vodiča „važi beleška te druge vežbe" je držala na serveru, ali ne u
  ekranu. Isprobano uživo (zamena Back Squat → Front Squat i nazad). Ispravljeno čitanjem `targetId` u izvedenim
  signalima; četiri testa na nivou komponente plus jedan za kasnu grešku. Vraćanje na staro: saved 1, izmena 2,
  kasni odgovor 1, ostalo na 1.
- **Prvi upis iz dve kartice davao je 500** (jedinstveni indeks, `DbUpdateException`, bez mapiranja), što je
  protivno tvrdnji „poslednji upis pobeđuje" iz ove beleške i iz opisa PR-a. Ispravljeno ponovnim pokušajem i 409.
  Trku nije bilo moguće dobiti običnim testom, pa je presretač ubacuje namerno: drugi pisac upiše između čitanja i
  čuvanja. Tri trke (prvi upis, izmena obrisanog reda, brisanje obrisanog reda) i trajan neuspeh; bez ponovnog
  pokušaja pada 4, bez pretvaranja u 409 pada 1.
- **„Granica se meri posle skraćivanja" je tačno samo u servisu.** Zahtev je prvi i broji razmake. Tekst u belešci i
  PR-u je ispravljen, a postoji i test koji kaže tačno to (zahtev odbija, servis prihvata).
- **Pravila koja nijedan test nije držao** (agent ih je pokvario u kopiji, 0 padova): jedinstveni indeks (bez njega
  `ToDictionaryAsync` u čitanju beleški bi bacio izuzetak i ekran treninga bi dao 500), dužine obe kolone, atribut
  na zahtevu, broj 500 koji klijent ponavlja, i `Restrict` ključ od beleški (a i od podešavanja vežbe, što je
  izostavljeno i ranije) ka vežbi. Sada svako ima test: bez indeksa pada 2, kolona beleške 1, kolona treninga 1,
  atribut 2, broj 1, ključ 1 (plus dva testa ključa u `AccountDeletionTests`).
- **`UpdatedAt` se pisao a nikad nije čitao**, pa je uklonjen. Migracija je pravljena iznova (nije bila gurnuta),
  što je značilo vratiti razvojnu bazu korak unazad: beleške iz prve provere uživo su otišle sa njom.
- **Testovi koji su tvrdili više nego što proveravaju:** „može se pisati u svakom statusu" je proveravao samo da se
  nudi dugme (preimenovan), a `restTimer.running()` ne može da padne jer ništa ne povezuje beleške i pauzu
  (uklonjeno).
- **DTO polja koja se ne popunjavaju svuda.** `MesocycleService` i `MesocycleGenerator` prave iste DTO-ove ručno i
  ne popunjavaju `Note` i `ExerciseNote`, a komentar je govorio „null kad je nema". Isti oblik kao `SetLogDto` iz
  runde 9. Nijedan ekran ih tamo ne čita; komentar sada kaže da se popunjavaju samo na putanji ekrana treninga.
- **Telo bez polja briše belešku** (`{}` ili pogrešno ime polja): PUT zamenjuje belešku, a zahtev bez polja je
  prazna beleška. Prihvatljivo, i zapisano u komentaru zahteva.
- **Pristupačnost i upotrebljivost.** Polje se otvara u fokusu (jedan dodir otvara tastaturu; da li iOS to dozvoljava
  van pokreta korisnika nije proveravano), svako „Izmeni" kaže uz šta je, brojač i greška su vezani za polje, polje
  ne završava ispod zalepljene trake pauze, a dugme glasi „Dodaj belešku uz vežbu" umesto „Dodaj belešku: beleška uz
  vežbu".

Merenje vraćanjem posle ispravki: serija na serveru 8 izmena (1413 testova) i na klijentu 9 (390 testova); jedna
je bila ekvivalentna izmena (greška za staru vežbu prikazana uz novu: posle zamene je izmena zatvorena, pa se
greška ne vidi i `edit()` je briše), pa je zaštita uklonjena umesto testirana.

## Šta nije proveravano

- **Tastatura na iPhone-u.** Polje ima 16 px, da pregledač ne uveća stranicu pri fokusu, i dobija fokus čim se
  otvori; da li iOS zbog toga otvara i tastaturu nije probano na uređaju.
- **Prava trka na PostgreSQL-u.** Trke su dobijene presretačem u SQLite-u (jedna konekcija), što dokazuje da se
  `DbUpdateException` i `DbUpdateConcurrencyException` hvataju i da ponovni pokušaj radi; ne dokazuje kako će
  Npgsql prijaviti istu povredu jedinstvenog indeksa pod pravim opterećenjem.
- Snimak ekrana: pregledač u panelu je u ovom prolazu davao sitne snimke, pa je izgled proveren merenjem iz DOM-a
  (širine, visine, preliv), ne okom.

## Šta je grana naučila usput

- **Test koji nije mogao da padne, i merenje koje ga je uhvatilo.** „Drugi dodir ne šalje drugi zahtev" je klikao
  dugme koje je u tom trenutku onemogućeno: onemogućeno dugme ne šalje događaj, pa zaštita u `save()` nije ni
  dotaknuta, i njeno uklanjanje je davalo 0. Zaštita je za poziv koji ne ide kroz dugme, pa test sada poziva
  `save()` direktno; uklanjanje sada ruši 1.
- **Dva moja pogrešna očekivanja u testovima**, oba pročitana pre promene koda: „Levo rame: spusti hvat" ima 22
  znaka, ne 21, a vežba 1 u prikazu je potisak (bez beleške), pa je niz `[null, "Sedište 4, pin 7"]`.
- **Prekrivanje zaštita skriva koja nema test.** Tri provere vlasništva su se međusobno pokrivale, pa je svako
  pojedinačno merenje dalo 0; tek je pitanje „šta tvrdi da filter postoji" našlo da ništa ne tvrdi.
