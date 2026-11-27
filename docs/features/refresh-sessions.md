# Prijava koja traje

## Problem

Prijava je trajala tačno koliko i pristupni token, 60 minuta. Duži trening se završavao na
ekranu za prijavu: prvi zahtev posle isteka dobijao je 401 i aplikacija je odjavljivala.

## Rešenje

Od ponuđenih rešenja izabran je token za obnavljanje u bazi, sa 30 dana neaktivnosti.

**Server.** Tabela `LoginSessions`, red po prijavljenom uređaju. `POST /api/auth/refresh`
menja token za nov par i pomera istek za 30 dana, `POST /api/auth/logout` briše prijavu.
Pristupni token nosi oznaku prijave i odbija se čim prijave nema. Pravilo koji se token
prima je u `Application/Security/RefreshTokenPolicy.cs`.

**Klijent.** Zahtev odbijen sa 401 obnavlja token i šalje se ponovo. Više zahteva deli jedno
obnavljanje, a tabovi se redaju preko Web Locks, jer dvaput upotrebljen token izgleda kao
krađa. Neuspeh zbog mreže ili zauzetog servera **ne** odjavljuje. Odjava ili drugi nalog u
drugom tabu prate se i u ovom.

### Zašto je token potpisan

Oznaka prijave stoji i u svakom pristupnom tokenu. Bez potpisa bi bila dovoljna da se tuđi
uređaj odjavi, a server ne bi umeo da razlikuje svoj zamenjen token od izmišljenog niza. Sa
potpisom: izmišljen niz ne dira ništa, a svaki token koji je server izdao gasi svoju prijavu
pri odjavi, i kada je već zamenjen.

### Zašto sesija pamti dva tokena

Kada bi stari token prestao da važi u trenutku izdavanja novog, svaki izgubljen odgovor u
teretani bi odjavio vlasnika i izgledao kao krađa. Zato prijava pamti token za koji se zna da
ga klijent ima i onaj izdat posle njega. Prvi važi dok se drugi ne pojavi. Kopiran token se
i dalje hvata: kada oba držaoca upotrebe prijavu, jedan donese token koji nije nijedan od ta
dva, redosled nije bitan.

## Provera

- 1882 testa na serveru i 513 na klijentu.
- Pravila su uklanjana jedno po jedno iz koda i za svako sa spiska u skripti padne bar jedan
  test. Ono što testovi ne dotiču je sklop u `Program.cs` (poziv provere tokena i registracija
  granice broja zahteva), jer nijedan test ne pokreće ceo API; provereno uživo.
- Uživo, sa pristupnim tokenom skraćenim na oko minut:
  - serija upisana posle isteka: 401, obnavljanje 200, ista serija 201, bez izlaska sa ekrana;
  - dva taba učitana u istoj milisekundi: obnavljanje je poslao samo jedan;
  - odjava: stari pristupni token odmah 401, drugi tab prešao na prijavu;
  - ponovljen zamenjen token gasi prijavu (zapis u logu), promena lozinke gasi ostale.
- Migracija `AddLoginSessions` primenjena na razvojnu bazu.

## Šta je pregled našao

- Odjava već zamenjenim tokenom ostavljala je prijavu živom, pa je onaj ko ga je zamenio ostajao
  prijavljen. Sama oznaka prijave bila je dovoljna da se uređaj odjavi preko obnavljanja. Oboje
  rešava potpis na tokenu.
- Čekanje na bravu među tabovima nije imalo rok.
- Odgovor na promenu lozinke koji stigne posle odjave ponovo je prijavljivao uređaj.
- Stranica greške od nginx-a ispisivala se kao poruka dok se API ponovo podiže.
- Više pravila moglo je da se ukloni a da nijedan test ne padne (brava, ponovna provera posle
  odbijenog obnavljanja, provera security stamp-a, drugi deo uslovnog upisa, atributi na dve nove
  rute), pa je rečenica „za svako pravilo padne test“ bila netačna.

## Pri nadogradnji

Tokeni izdati ranije nemaju oznaku prijave, pa se **svako jednom ponovo prijavljuje**.

## Ograničenja

- Odjava bez mreže gasi prijavu samo na uređaju; na serveru ističe sama.
- Bez bezbednog konteksta (aplikacija otvorena preko običnog HTTP-a sa druge adrese) nema Web
  Locks, pa dva taba mogu istovremeno da obnove i odjave vlasnika. Isporuka je preko HTTPS-a.
- Nema gornje granice trajanja: prijava koja se koristi bar jednom u 30 dana ne ističe.
- Ako bi server obradio obnavljanje od kog je klijent već odustao posle njegovog ponovljenog
  pokušaja, vlasnik bi se pri sledećem obnavljanju morao ponovo prijaviti. To nije izazvano ni u
  jednom merenju: prekinut zahtev server otkazuje.
