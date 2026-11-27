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

### Zašto sesija pamti dva tokena

Kada bi stari token prestao da važi u trenutku izdavanja novog, svaki izgubljen odgovor u
teretani bi odjavio vlasnika i izgledao kao krađa. Zato prijava pamti token za koji se zna da
ga klijent ima i onaj izdat posle njega. Prvi važi dok se drugi ne pojavi. Kopiran token se
i dalje hvata: kada oba držaoca upotrebe prijavu, jedan donese token koji nije nijedan od ta
dva, redosled nije bitan.

## Provera

- 1875 testova na serveru i 496 na klijentu.
- Pravila su uklanjana jedno po jedno iz koda: za svako padne bar jedan test, osim za poziv
  provere iz `Program.cs` (nijedan test ne pokreće ceo API; provereno uživo).
- Uživo, sa pristupnim tokenom skraćenim na oko minut:
  - serija upisana posle isteka: 401, obnavljanje 200, ista serija 201, bez izlaska sa ekrana;
  - dva taba učitana u istoj milisekundi: obnavljanje je poslao samo jedan;
  - odjava: stari pristupni token odmah 401, drugi tab prešao na prijavu;
  - ponovljen zamenjen token gasi prijavu (zapis u logu), promena lozinke gasi ostale.
- Migracija `AddLoginSessions` primenjena na razvojnu bazu.

## Pri nadogradnji

Tokeni izdati ranije nemaju oznaku prijave, pa se **svako jednom ponovo prijavljuje**.

## Ograničenja

- Odjava bez mreže gasi prijavu samo na uređaju; na serveru ističe sama.
- Bez bezbednog konteksta (aplikacija otvorena preko običnog HTTP-a sa druge adrese) nema Web
  Locks, pa dva taba mogu istovremeno da obnove i odjave vlasnika. Isporuka je preko HTTPS-a.
- Nema gornje granice trajanja: prijava koja se koristi bar jednom u 30 dana ne ističe.
