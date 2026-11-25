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
- **Granica je 500 znakova i meri se posle skraćivanja razmaka.** Server skida razmake sa krajeva i vraća ono što
  je sačuvao; polje prikazuje to, a ne ono što je otkucano. Prazna ili samo razmaci znači „nema beleške": red se
  briše, a ne čuva kao prazan tekst. Granica je na tri mesta (DTO `[MaxLength]`, servis, kolona) i u `note-field.ts`;
  `NotePolicy` i komentar to vezuju.
- **Vidi je samo vlasnik**, i filterom vlasništva i uslovom u upitu. Dva korisnika na istoj sistemskoj vežbi imaju
  nezavisne beleške; tuđa sopstvena vežba je 404, ne beleška uz nešto što korisnik ne vidi.
- **Istorija prikazuje samo belešku treninga**, skraćenu na dva reda; cela je na ekranu treninga. Beleške uz vežbe
  se tamo ne prikazuju.
- **Otkucano a nesačuvano se gubi pri izlasku sa ekrana.** Beleška se čuva na „Sačuvaj"; vodič to kaže.

## Merenja

- Testovi: server 1379 → 1402, klijent 360 → 382.
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

## Šta nije proveravano

- **Tastatura na iPhone-u.** Polje ima 16 px, da pregledač ne uveća stranicu pri fokusu, ali to nije probano na
  uređaju.
- **Dva uređaja istovremeno.** Poslednji upis pobeđuje; za jednog korisnika ne smeta, i nije rešavano.
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
