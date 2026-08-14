# Bezbedna isporuka

Šta je u isporuci već podešeno, i šta moraš da uradiš sam pre nego što aplikacija postane
dostupna sa interneta.

## TLS

`docker compose up` diže aplikaciju na čistom HTTP-u. Lokalno je to u redu. Čim je adresa
dostupna nekom drugom, lozinka pri prijavi i svaki JWT putuju u čitljivom obliku — ko god je
na putanji (isti Wi-Fi, kompromitovan ruter, provajder) čita ih i preuzima nalog.

**Zato TLS više nije uputstvo nego dodatni compose fajl:**

```bash
APP_DOMAIN=tvoj-domen.rs TLS_EMAIL=ti@primer.com \
  docker compose -f docker-compose.yml -f docker-compose.tls.yml up -d
```

Caddy sam pribavlja i obnavlja Let's Encrypt sertifikat, sam preusmerava HTTP na HTTPS i sam
šalje HSTS. Uz njega se pale i `Security__RequireHttps` i `Security__ProxyCount` na API-ju —
sve u istom fajlu, pa nema koraka koji se zaboravlja.

Šta Let's Encrypt traži da bi izdao sertifikat: domen mora da pokazuje na tu mašinu, a
portovi 80 i 443 moraju da budu dostupni sa interneta. Traži se i Docker Compose v2.24 ili
noviji.

> **Nije pokrenuto.** Docker nije bio dostupan pri pisanju ovoga, pa su compose fajl i
> Caddyfile provereni čitanjem. Posle prvog dizanja proveri da odgovor nosi
> `Strict-Transport-Security` i da `http://` završava na `https://`.

HSTS namerno šalje Caddy, a ne nginx: preko čistog HTTP-a ga pregledači ignorišu, a ako se
aplikacija ikada posluži preko HTTP-a posle HTTPS-a, zaključao bi je. Caddy je jedino mesto
koje pouzdano zna da TLS postoji.

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

Pravilo propušta i `fonts.gstatic.com`, i to nije propust. Angular pri build-u pretvara
`<link>` ka Google Fonts u ugrađene `@font-face` blokove, ali **sami fajlovi fontova i dalje
dolaze sa gstatic-a**. Provereno tako što je produkcijski build posluzen sa strožim
pravilom: Inter, Space Grotesk i Material Icons svi završe sa `status: error`, a
`document.fonts.check('24px "Material Icons"')` vrati `false` — dakle ikone bi nestale sa
ekrana.

Jače rešenje je **posluživanje fontova sa istog porekla**: tada oba Google izvora ispadaju
iz pravila, aplikacija radi bez interneta, a IP adresa korisnika ne odlazi trećoj strani pri
svakom otvaranju stranice. To je izmena u build-u, ne u konfiguraciji nginx-a, pa ovde nije
urađena.

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
> poveže.
>
> Pusti istu skriptu ručno, umesto da prepisuješ naredbe. Ona lozinku prosleđuje kao psql
> promenljivu; prepisana `CREATE ROLE ... PASSWORD '...'` naredba se lomi na lozinci sa
> apostrofom, a to je tačno ono zbog čega skripta i izgleda ovako:
>
> ```bash
> docker compose exec -e APP_DB_USER -e APP_DB_PASSWORD -e POSTGRES_USER -e POSTGRES_DB \
>   db sh /docker-entrypoint-initdb.d/01-app-role.sh
> ```
>
> Skripta je idempotentna — provereno, drugo pokretanje preskače pravljenje naloga i samo
> ponovi grantove — pa je bezbedno pustiti je i kad nisi siguran da li nalog već postoji.
>
> **Uz to prepiši vlasništvo nad zatečenim tabelama.** Migracije se izvršavaju pri svakom
> startu i menjaju šemu; tabele koje je napravio stari superkorisnički nalog ostaju u
> njegovom vlasništvu, pa bi prva naredna migracija pala na „must be owner of table".
> Grantovi iznad daju pravo na *podatke*, ne na izmenu strukture:
>
> ```bash
> docker compose exec db psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" >   -c "REASSIGN OWNED BY $POSTGRES_USER TO strengthplanner_app;"
> ```
>
> Na novoj instalaciji ovo ne treba: tabele od početka pravi sam aplikacijski nalog.

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

- **Rezervne kopije baze.** Volumen `strengthplanner_pgdata` je jedino mesto gde podaci
  postoje.
- **Nadogradnja osnovnih imidža.** `postgres:16`, `aspnet:8.0` i `nginx-unprivileged:1.27`
  dobijaju bezbednosne ispravke. Dependabot sada mesečno otvara PR za njih, ali sam build
  moraš da pustiš: `docker compose build --pull`.
- **Migracije se izvršavaju pri svakom startu API-ja.** Zgodno za demo. Za pravu produkciju je
  uobičajeno razdvojiti korak migracije od pokretanja aplikacije, da nadogradnja ne bi menjala
  šemu u trenutku kad se dižu instance.

## Poznata ograničenja

- **Ciljano zaključavanje tuđeg naloga i dalje je moguće.** Identity zaključava nalog posle
  pet promašaja na pet minuta, što je oko jednog zahteva u minutu — daleko ispod praga
  ograničenja broja zahteva. Ko zna tuđ email može da mu drži nalog zaključanim. Prava
  rešenja su postepeno usporavanje umesto tvrdog zaključavanja ili vezivanje brojača za par
  (nalog, IP); oba menjaju ponašanje prijave, pa nisu dirana.
- **Zatečene kratke lozinke ostaju.** Pravilo o dužini važi kad se lozinka postavlja, ne kad
  se proverava, pa nalozi napravljeni ranije zadržavaju svoje. Prisilna promena traži polje
  „lozinka promenjena" i tok koji korisnika na to natera.
- **Registracija i dalje odaje da li email ima nalog** — ne porukom, koja je ujednačena, nego
  statusnim kodom (400 naspram 200). Pravo rešenje je potvrda email-om.

- **Ograničenje broja zahteva deli budžet po IP adresi.** Više korisnika iza istog NAT-a
  (kućni ruter, teretana, fakultet) deli 20 zahteva u minutu na auth rutama, i 5 na sat na
  registraciji. Za prijavu je to prihvatljivo; ako postane smetnja, obe granice su na
  jednom mestu u `Program.cs`.
- **JWT stoji u `localStorage`.** Uobičajen kompromis za SPA — kolačić sa `httpOnly` bi tražio
  zaštitu od CSRF-a. CSP je ono što ovde nosi odbranu.
- **Odjava je i dalje bez stanja.** Token važi do isteka (60 minuta). Promena lozinke ga
  poništava odmah, ali „odjavi me svuda" kao zasebna radnja ne postoji.
