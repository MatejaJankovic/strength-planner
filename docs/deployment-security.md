# Bezbedna isporuka

Šta je u isporuci već podešeno, i šta moraš da uradiš sam pre nego što aplikacija postane
dostupna sa interneta.

## Ono što moraš sam: TLS

**Ovo je jedina stavka koju kod ne može da reši umesto tebe, i najvažnija je.**

`docker compose up` diže aplikaciju na čistom HTTP-u. Lokalno je to u redu. Čim je adresa
dostupna nekom drugom, lozinka pri prijavi i svaki JWT putuju u čitljivom obliku — ko god je
na putanji (isti Wi-Fi, kompromitovan ruter, provajder) čita ih i preuzima nalog.

Aplikacija je pripremljena da radi iza TLS terminacije: nginx već prosleđuje
`X-Forwarded-Proto`, a API čita prosleđena zaglavlja.

Najjednostavnije rešenje je reverse proxy koji sam pribavlja sertifikat. Primer sa Caddy-jem
ispred `web` servisa:

```
tvoj-domen.rs {
    reverse_proxy web:8080
}
```

Caddy sam vadi i obnavlja Let's Encrypt sertifikat i sam preusmerava HTTP na HTTPS.

Ako TLS terminiraš u samom nginx-u, dodaj i HSTS — namerno ga nema u isporučenoj
konfiguraciji, jer ga pregledači preko čistog HTTP-a ignorišu, a ako se aplikacija ikada
posluži preko HTTP-a posle HTTPS-a, zaključao bi je:

```
add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
```

## Šta je već podešeno

### Bezbednosna zaglavlja

`strength-planner-web/security-headers.conf` šalje CSP, `X-Content-Type-Options`,
`X-Frame-Options`, `Referrer-Policy` i `Permissions-Policy`, a `server_tokens off` sakriva
verziju nginx-a.

Zaglavlja se **uključuju u svaki `location` blok**, ne samo u `server`. To nije stilska
odluka: u nginx-u `add_header` iz spoljašnjeg bloka ne važi u `location`-u koji ima svoj
`add_header`. Da su ostala samo u `server` bloku, nestala bi tačno tamo gde su najpotrebnija
— na `index.html` i na skriptama, jer ti blokovi postavljaju `Cache-Control`.

CSP dozvoljava `'unsafe-inline'` za stilove, jer ih Angular Material ubacuje inline. Za
skripte ga **ne** dozvoljava, a to je deo koji zaista štiti.

### Keširanje

Fajlovi sa heš-om u imenu se keširaju godinu dana; `index.html` nosi `no-cache`. Bez toga ga
pregledač kešira heuristički, pa korisnik posle nadogradnje ostaje na staroj verziji —
uključujući i staru verziju bezbednosnih ispravki.

### Kontejneri ne rade kao root

API koristi korisnika `app` iz zvaničnog `aspnet` imidža. Frontend koristi
`nginxinc/nginx-unprivileged`, koji radi kao korisnik 101 i sluša na 8080 — zvanični nginx
imidž se pokreće kao root da bi vezao port 80. Oba servisa imaju `no-new-privileges`.

### API nije izložen spolja

Objavljen je na `127.0.0.1:${API_PORT}`, pa se spolja do njega dolazi samo kroz nginx.

To nije kozmetika: ograničenje broja zahteva deli saobraćaj po IP adresi klijenta, koju čita
iz `X-Forwarded-For`. Da je API dostupan direktno, svako bi mogao sam da postavi to zaglavlje
i time zaobiđe ograničenje. Ovako ga postavlja samo nginx.

### Aplikacija se ne povezuje superkorisnikom

`db/init/01-app-role.sh` pravi nalog `APP_DB_USER` sa pravom da čita i piše podatke, ali bez
superkorisničkih prava. Ranije se aplikacija povezivala nalogom iz `POSTGRES_USER`, koji je u
`postgres` imidžu superkorisnik.

> **Pažnja pri nadogradnji postojeće instalacije.** Skripte iz
> `/docker-entrypoint-initdb.d` postgres pokreće **samo pri prvom pravljenju baze**. Ako već
> imaš `strengthplanner_pgdata` volumen, nalog neće biti napravljen i API neće moći da se
> poveže. Napravi ga jednom ručno:
>
> ```bash
> docker compose exec db psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c \
>   "CREATE ROLE strengthplanner_app LOGIN PASSWORD 'lozinka-iz-.env';"
> docker compose exec db psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c \
>   "GRANT USAGE, CREATE ON SCHEMA public TO strengthplanner_app;
>    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO strengthplanner_app;
>    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO strengthplanner_app;"
> ```

### Tajne

`.env` je u `.gitignore`; `.env.example` sadrži samo placeholder vrednosti. `appsettings.json`
ima **prazan** `Jwt:Key`. `docker-compose.yml` traži `POSTGRES_PASSWORD`, `APP_DB_PASSWORD` i
`JWT_KEY` kroz `:?` sintaksu, pa se bez njih ni ne pokreće.

API odbija da se pokrene ako je `Jwt:Key` kraći od 32 bajta — u svakom okruženju — i ako van
razvoja liči na placeholder. Napravi pravi ključ:

```bash
openssl rand -base64 48
```

## Šta ostaje na tebi

- **TLS**, kao gore.
- **Rezervne kopije baze.** Volumen `strengthplanner_pgdata` je jedino mesto gde podaci
  postoje.
- **Nadogradnja osnovnih imidža.** `postgres:16`, `aspnet:8.0` i `nginx-unprivileged:1.27`
  dobijaju bezbednosne ispravke; povremeno uradi `docker compose build --pull`.
- **Migracije se izvršavaju pri svakom startu API-ja.** Zgodno za demo. Za pravu produkciju je
  uobičajeno razdvojiti korak migracije od pokretanja aplikacije, da nadogradnja ne bi menjala
  šemu u trenutku kad se dižu instance.

## Poznata ograničenja

- **Ograničenje broja zahteva deli budžet po IP adresi.** Više korisnika iza istog NAT-a
  (kućni ruter, teretana, fakultet) deli 10 zahteva u minutu na auth rutama. Za prijavu i
  registraciju je to prihvatljivo; ako postane smetnja, granica je na jednom mestu u
  `Program.cs`.
- **JWT stoji u `localStorage`.** Uobičajen kompromis za SPA — kolačić sa `httpOnly` bi tražio
  zaštitu od CSRF-a. CSP je ono što ovde nosi odbranu.
- **Odjava je i dalje bez stanja.** Token važi do isteka (60 minuta). Promena lozinke ga
  poništava odmah, ali „odjavi me svuda" kao zasebna radnja ne postoji.
