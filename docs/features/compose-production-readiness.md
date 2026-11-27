# Isporuka na serveru

Priprema za pokretanje na serveru: Docker konfiguracija i nginx. U kontejnerima se aplikacija
ponašala drugačije nego u razvoju, a ta razlika nije imala nijednu proveru.

## Problem

Izmereno na pokrenutom sklopu pre izmena, sa `docker-compose.tls.yml`:

- **Put mimo HTTPS-a.** `web` je objavljivao port 8080 na svim adresama. Aplikacija se otvarala
  preko čistog HTTP-a sa adrese računara u mreži (200), a prijava poslata tim putem dobijala je
  preusmeravanje (307) tek pošto je lozinka već otišla čitljiva.
- **Bez restart politike.** `db`, `api` i `web` imali su `restart: no`. Ugašen API proces ostao
  je ugašen (`exited`, 0 restarta).
- **Zapisi bez granice** u compose konfiguraciji. Na Docker Desktop-u podrazumevani drajver
  (`local`) rotira sam, a na Linux serveru je podrazumevani `json-file` bez gornje granice.
- **Slika profila iznad 1 MB** dobijala je nginx-ovu stranicu `413`, a API prima do 2 MB.
- **Slika profila nije se prikazivala ni kada prođe.** CSP je dozvoljavao `img-src 'self' data:`,
  a slika se prikazuje kao `blob:` adresa; u konzoli pregledača stajalo je da je blokirana.
  Razvojni server ne šalje CSP, pa se tamo to nije videlo.
- **Trajni 502 posle nove verzije API-ja.** nginx je pamtio adresu API kontejnera iz trenutka
  svog pokretanja. Kada je API napravljen ponovo i dobio drugu adresu, prijava je vraćala 502
  i posle 20 pokušaja.

## Rešenje

- `web` se vezuje za `127.0.0.1`; u TLS varijanti `ports: !reset []` uklanja port potpuno
  (compose inače spaja liste portova iz dva fajla). Traži Compose 2.24 ili noviji, što je
  uputstvo već tražilo.
- `restart: unless-stopped` i `json-file` sa `max-size: 10m`, `max-file: 3` na svim servisima.
- `client_max_body_size 3m` u nginx-u; granica tela zahteva za sliku dobila je ime
  (`ImageFormat.MaximumRequestSizeBytes`) da bi test mogao da je uporedi sa nginx-om.
- `img-src 'self' data: blob:`.
- nginx `upstream` sa `resolve` i Docker-ovim DNS-om (`127.0.0.11`, `valid=10s`).

## Provera

Isti sklop, napravljen iznova od praznog volumena:

| | Pre | Posle |
|---|---|---|
| `http://<adresa u mreži>:8080` uz TLS | 200 | ne odgovara |
| Ugašen API proces | ostaje ugašen | ponovo radi (prijava 200 za 7 s) |
| Ugašen PostgreSQL proces | nije mereno | ponovo radi |
| API dobije novu adresu | 502 i dalje | 200 za 15 s |
| Slika 1,1 MB i 2 MB | 413 | 200 |
| Slika 2 MB + 1 bajt | 413 | 400 „Slika je veća od 2 MB.“ |
| Prikaz slike u pregledaču | blokiran CSP-om | prikazana |

- Uz TLS: `Strict-Transport-Security` na odgovoru i `308` sa `http://` na `https://`.
- Ekrani trening, sesija, plan, analitika, istorija, profil, izmena profila, podešavanja, vežbe,
  šabloni i unos maksimuma otvoreni su na produkcijskom buildu iza nginx-a: konzola bez ijedne
  poruke.
- Skripta za bazu je u gitu bez izvršnog bita, pa je na Linux-u postgres imidž **uvlači**
  (`sourcing`) umesto da je pokrene. Probano tako, sa lozinkom koja sadrži apostrof i SQL
  fragment: baza se podigne, nalog postoji i nije superkorisnik.

Nove provere: `ProxyUploadLimitTests`, `ContentSecurityPolicyImageTests` i
`scripts/check-compose.py` (u CI-ju).

## Ograničenja

- Let's Encrypt nije probano: lokalno Caddy koristi sopstveni autoritet.
- Restart celog servera nije simuliran; provereno je da politika stoji i da se pad procesa
  oporavlja.
- Slika između 2 MB i 3 MB (iznad granice tela zahteva) dobija opštu poruku o neuspeloj
  validaciji umesto poruke o veličini, a iznad 3 MB nginx-ovu stranicu. Ekran obe zaustavlja
  pre slanja.
- Hetzner Cloud Firewall i rezervne kopije baze su koraci na serveru, van repozitorijuma.
