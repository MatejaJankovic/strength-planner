# Ograničenje redova na vlasnika (row-level security)

## Šta je bilo

Kontrola pristupa je živela isključivo u servisima: svaki upit je sam pisao
`Where(x => x.UserId == userId)`. To je radilo — unakrsni pristup je testiran nad
pokrenutim API-jem i ništa nije prošlo — ali je garancija bila „niko nije zaboravio",
ponovljena u šezdesetak upita.

Problem sa takvom garancijom je što se njen prekid ne vidi. Upit bez uslova radi
savršeno sve dok u bazi postoji jedan nalog. Greška se pojavi tek kod drugog korisnika,
i to kao curenje podataka, a ne kao pad.

Jedan upit se već oslanja na redosled poziva umesto na sam sebe:
`MacrocycleService.RegenerateBlockAsync` dohvata blok samo po identifikatoru. Tamo je
bezbedno jer je blok trenutak ranije preuzet upitom koji jeste ograničen na korisnika —
ali ta bezbednost stoji u sledu naredbi, a prva izmena redosleda je gubi.

## Šta je urađeno

`AppDbContext` sada svakoj tabeli sa korisničkim podacima dodaje globalni filter
(`HasQueryFilter`). Tabele koje nemaju svoju `UserId` kolonu — nedelje, treninzi,
planovi vežbi, serije, blokovi plana — dobijaju isto pravilo preko navigacije do
vlasnika. Sistemske vežbe ostaju vidljive svima, korisničke samo svom tvorcu.

Servisi zadržavaju svoje eksplicitne uslove. Oni su i dalje glavna provera i daju bolju
poruku o grešci (404 sa objašnjenjem umesto prazne liste); filter je sloj ispod njih.

## Dokaz da filter zaista nosi odbranu

Uklonjen je uslov po `userId` iz `MesocycleService.BuildDetailsQuery`, tako da je filter
jedino što je ostalo. Korisnik B je tražio mezociklus korisnika A:

| | odgovor |
|---|---|
| filter uključen | 404 |
| filter isključen | **200, ceo plan u telu odgovora** |

Uz to, pun prelaz nad pokrenutim API-jem: dva naloga, B poseže za svakim resursom
korisnika A (mezociklus, plan, trening, serije, analitika, custom vežba) — svih jedanaest
pokušaja vraća 404. Svi tokovi korisnika A i dalje rade.

## Zamka na koju se naletelo

Vlasnik se čita **pri svakom upitu**, a ne jednom u konstruktoru.

Kontekst nastane već pri proveri tokena — `JwtBearer` događaj traži `UserManager` da
uporedi security stamp — a `HttpContext.User` u tom trenutku još nije postavljen. Pošto je
kontekst scoped, vrednost zapamćena tada ostaje do kraja zahteva. Prva verzija je to
radila i posledica je bila da korisnik svoje podatke vidi kao nepostojeće: generisanje
mezociklusa je padalo sa 404 „Plan was not found".

## Zašto `null`, a ne `Guid.Empty`

Kada korisnika nema (migracije, seed pri pokretanju, anonimne rute), filter poredi sa
`null`. Poređenje sa `null` u SQL-u nije tačno ni za jedan red.

Prazan `Guid` bi bio vrednost kao i svaka druga: red koji bi ga iz bilo kog razloga poneo
u koloni vlasnika postao bi vidljiv baš u kontekstu bez korisnika. Danas takav red ne može
da nastane, ali tada bi tu sigurnost nosio podatak, a ne filter.

Kod vežbi su obe strane poređenja nullable, a EF za takav slučaj generiše i granu
„oba su NULL". Zato ispred poređenja stoji provera da korisnik uopšte postoji — bez nje bi
custom vežba bez upisanog tvorca bila vidljiva tačno kada korisnika nema.

## Testovi

`OwnershipFilterTests` proverava dve stvari po tabeli: da filter postoji **i da zaista
čita vlasnika zahteva**. Druga provera nije formalnost — `HasQueryFilter(x => true)` je
filter kao i svaki drugi i prošao bi prvu, a to je tačno onaj izraz kojim je gore izmereno
curenje. Poseban test dokazuje da provera pada na filteru koji propušta sve.

Tabele su nabrojane ručno, da nova tabela sa korisničkim podacima mora svesno da se doda.
Šifarnici (mišićne grupe, podrazumevani MEV/MRV) namerno nemaju filter: zajednički su
svim nalozima, a filter bi pokvario seed pri pokretanju, kada korisnika nema.

## Šta ovim nije rešeno

Filteri idu preko navigacija, pa svaki nivo ponovo prošeta ceo lanac do vlasnika. Upit
koji traži redni broj poslednje serije je od jednog pogotka po indeksu postao ugnežđeni
spoj kroz četiri tabele, sa istim uslovom ponovljenim pet puta. Nad stvarnim podacima to
traje 2–10 ms i za ovu aplikaciju je prihvatljivo, ali cena raste sa brojem serija.

Pravo rešenje je `UserId` kolona na svakoj tabeli, čime svaki filter postaje poređenje
jedne kolone. To je izmena šeme i zaseban posao.

Ovo je ograničenje **na nivou aplikacije**, ne baze. Postgres RLS bi štitio i od
kompromitovanog naloga ka bazi, ali klijent ovde nikada ne drži pristup bazi — do podataka
se dolazi samo kroz API — pa je realan scenario upit koji je zaboravio uslov, i njega ovo
pokriva.
