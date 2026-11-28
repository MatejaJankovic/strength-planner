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

**Za domen objavi samo A zapis (IPv4), bez AAAA.** Mreža u compose-u je samo IPv4, pa
aplikacija preko IPv6 ne vidi pravu adresu posetioca, a na toj adresi počiva ograničenje broja
zahteva. Samo uključivanje IPv6 na toj mreži nije dovoljno: API tada ne veruje zaglavljima koja
stižu preko IPv6 i vrti preusmeravanje na samog sebe.

### Isti skup fajlova pri svakoj naredbi

Na serveru u `.env` upiši, uz `APP_DOMAIN` i `TLS_EMAIL`:

```bash
COMPOSE_FILE=docker-compose.yml:docker-compose.tls.yml:docker-compose.backup.yml
```

Tada svaka `docker compose` naredba koristi sve navedene fajlove. Bez toga obično `docker compose up -d`
napravi API ponovo **bez** postavki za TLS, dok stari Caddy nastavi da radi, pa se na prvi
pogled ništa ne vidi.

### Nova verzija

```bash
docker compose run --rm backup backup.sh keep
git pull
docker compose pull db caddy
docker compose build --pull
docker compose up -d
```

`build --pull` osvežava samo imidže koji se prave ovde (API, web i backup); `postgres` i
`caddy` se dobijaju sa `pull`. Prva naredba pravi kopiju baze koja se ne briše, da pre nove
verzije, a naročito one sa migracijom, postoji stanje na koje se može vratiti.

> **Pokrenuto lokalno**, sa `APP_DOMAIN=localhost`, gde Caddy izdaje sertifikat iz sopstvenog
> lokalnog autoriteta: odgovor nosi `Strict-Transport-Security`, a `http://` preusmerava na
> `https://`. Izdavanje preko Let's Encrypt-a traži pravi domen i nije probano; posle prvog
> dizanja na serveru proveri ista dva ishoda na svom domenu.

HSTS namerno šalje Caddy, a ne nginx: preko čistog HTTP-a ga pregledači ignorišu, a ako se
aplikacija ikada posluži preko HTTP-a posle HTTPS-a, zaključao bi je. Caddy je jedino mesto
koje pouzdano zna da TLS postoji.

## Šta je izloženo spolja

Uz TLS portove spolja objavljuje samo Caddy, 80 i 443. `web` (nginx) tada nema nijedan
objavljen port, a API je vezan za petlju. I bez TLS-a je `web` vezan za petlju
(`127.0.0.1:8080`): aplikacija preko čistog HTTP-a otvara se samo sa iste mašine.

**Docker sam piše pravila zaštitnog zida**, pa objavljen port ostaje dostupan i kada ga UFW na
serveru „blokira“. Zato odbrana ne sme da počiva na UFW-u. Na Hetzner-u uz to uključi **Cloud
Firewall** u konzoli (stoji ispred mašine, pa ga Docker ne zaobilazi) i dozvoli ulaz samo na
80, 443 i 22.

`scripts/check-compose.py` proverava razrešenu compose konfiguraciju: šta je objavljeno,
restart politiku, rotaciju zapisa i postavke TLS varijante. Pokreće se u CI-ju, a ručno sa
`python scripts/check-compose.py`.

## Rad bez nadzora

- **Restart.** Svi servisi imaju `restart: unless-stopped`: podižu se posle pada procesa i
  posle restarta servera. Ručno zaustavljen servis ostaje zaustavljen.
- **Zapisi.** Svaki servis čuva najviše tri fajla po 10 MB, pa zapisi ne mogu da napune disk.
- **Nova verzija API-ja.** nginx adresu API kontejnera razrešava dok radi, pa ne mora da se
  restartuje kada se kontejner napravi ponovo. Posle promene adrese ili dužeg prekida API-ja
  oporavak obično traje do petnaestak sekundi, a duže ako ne uspe sama DNS pretraga.

## Šta je već podešeno

### Bezbednosna zaglavlja

`strength-planner-web/security-headers.conf` šalje CSP, `X-Content-Type-Options`,
`X-Frame-Options`, `Referrer-Policy` i `Permissions-Policy`, a `server_tokens off` sakriva
verziju nginx-a.

Zaglavlja se **uključuju u svaki `location` blok**, ne samo u `server`. To nije stilska
odluka: u nginx-u `add_header` iz spoljašnjeg bloka ne važi u `location`-u koji ima svoj
`add_header`. Da su ostala samo u `server` bloku, nestala bi tačno tamo gde su najpotrebnija
— na `index.html` i na skriptama, jer ti blokovi postavljaju `Cache-Control`.

`img-src` dozvoljava i `blob:`: slika profila stiže kroz API uz token, pa se prikazuje kao
`blob:` adresa. nginx uz to prima telo zahteva do 3 MB (`client_max_body_size`), iznad
granice od 2 MB koju za sliku drži sam API.

CSP dozvoljava `'unsafe-inline'` za stilove, jer ih Angular Material ubacuje inline. Za
skripte ga **ne** dozvoljava, a to je deo koji zaista štiti.

Nijedan tuđi izvor nije dozvoljen. Fontovi (Inter, Space Grotesk, Material Icons) dolaze iz
npm paketa i build ih smešta uz ostale fajlove aplikacije, pa pregledač posetioca ne kontaktira
Google niti bilo koga drugog. Dodavanje spoljnog izvora hvataju test `NoThirdPartyResourcesTests`
(izvorni fajlovi i CSP) i `scripts/check-production-index.py` (izgrađena aplikacija).

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

## Rezervne kopije

`docker-compose.backup.yml` dodaje servis `backup`. Svake noći pravi dump baze, šifruje ga i
šalje van servera alatom [restic](https://restic.net), čuva 14 dnevnih, 8 nedeljnih i 6
mesečnih kopija, a jednom nedeljno poslednju probno vrati u privremenu bazu.

### Podešavanje sa Hetzner Storage Box-om

1. U Hetzner konzoli napravi Storage Box i u „Change settings“ uključi **SSH support**.
2. Na serveru, iz korena repozitorijuma, napravi ključ i upiši ga na Storage Box:

   ```bash
   mkdir -p secrets/backup-ssh && chmod 700 secrets secrets/backup-ssh
   ssh-keygen -t ed25519 -N '' -f secrets/backup-ssh/id_ed25519
   cat secrets/backup-ssh/id_ed25519.pub | ssh -p23 uXXXXXX@uXXXXXX.your-storagebox.de install-ssh-key
   ssh-keyscan -p 23 -t ed25519 uXXXXXX.your-storagebox.de > secrets/backup-ssh/known_hosts
   ```

   `ssh-keyscan` veruje onome što prvi put dobije. Pre nego što mu poveruješ, uporedi otisak
   (`ssh-keygen -lf secrets/backup-ssh/known_hosts`) sa onim koji isti Storage Box pokaže tvom
   računaru, sa druge mreže.
3. Napravi fajl `secrets/backup-ssh/config`:

   ```
   Host storagebox
     HostName uXXXXXX.your-storagebox.de
     Port 23
     User uXXXXXX
     IdentityFile ~/.ssh/id_ed25519
     ConnectTimeout 30
     ServerAliveInterval 60
     ServerAliveCountMax 3
   ```
4. U `.env` upiši `RESTIC_PASSWORD`, ostavi `RESTIC_REPOSITORY=sftp:storagebox:strengthplanner`
   i dopuni red `COMPOSE_FILE` fajlom `docker-compose.backup.yml`.
5. `docker compose up -d --build`, pa `docker compose logs backup`. Na novoj instalaciji piše
   „Baza još nema nijedan nalog“. Kada napraviš svoj nalog, ne čekaj noć:

   ```bash
   docker compose run --rm backup backup.sh
   docker compose run --rm backup verify.sh
   ```

   Treba da pišu „Kopija je sačuvana“ i „Proba vraćanja uspela“.

U jedno skladište piše samo jedan server. Skripte pre svakog posla uklanjaju katance koje je
ostavio prekinut posao, pa drugi server ili tvoj računar ne smeju da menjaju skladište dok
ovaj radi.

Umesto Storage Box-a može svako restic skladište; za S3 se upiše `RESTIC_REPOSITORY=s3:...` i
dva `AWS_` ključa.

### Šta čuvaš sam

Kopija sadrži bazu: naloge, planove, treninge i slike profila. **Ne sadrži** `.env`. Prepiši
ceo `.env` u menadžer lozinki, a naročito `RESTIC_PASSWORD`: bez nje kopije ne može da pročita
niko, pa ni ti. SSH ključ se na novom serveru napravi ponovo, a TLS sertifikat Caddy sam
pribavi.

Na Storage Box-u uz to uključi automatske **snapshot-ove**: njima se upravlja iz Hetzner
konzole, pa su dodatna zaštita ako neko sa servera obriše kopije.

### Nadzor

`docker compose ps` prikazuje `backup` kao *unhealthy* ako poslednja uspešna kopija ima više
od 26 sati ili je poslednja proba vraćanja pala. To niko neće primetiti ako ne gleda, pa u
`BACKUP_PING_URL` upiši adresu nekog nadzora koji javlja kada javljanje izostane (na primer
Healthchecks.io). Poziva se bez ikakvih podataka, posle uspešne kopije ili probe vraćanja i
samo dok je servis zdrav; na instalaciji bez ijednog naloga javlja se i bez kopije.
Posao koji traje duže od `BACKUP_TIMEOUT` (3 sata) prekida se i ponavlja.

### Svakodnevne naredbe

```bash
docker compose run --rm backup restic snapshots   # spisak kopija
docker compose run --rm backup backup.sh          # kopija odmah
docker compose run --rm backup backup.sh keep     # kopija koja se ne briše
docker compose run --rm backup verify.sh          # proba vraćanja odmah
```

### Vraćanje

**Na novom serveru redosled je bitan: aplikacija se pokreće tek posle vraćanja.**

1. Postavi repozitorijum i `.env` sa **istim** `RESTIC_REPOSITORY` i `RESTIC_PASSWORD`, pa
   ponovi korake 2 i 3 iz podešavanja (nov SSH ključ). Korak 5 preskoči.
2. Pokreni samo bazu i pogledaj kopije:

   ```bash
   docker compose build
   docker compose up -d db
   docker compose run --rm backup restic snapshots
   ```
3. Izaberi kopiju po datumu i veličini i vrati je:

   ```bash
   docker compose run --rm backup restore.sh <id>
   ```
4. `docker compose up -d`

Na serveru koji već radi prvo zaustavi aplikaciju i raspored, pa nastavi od koraka 2:

```bash
docker compose stop api web backup
```

- Vraća se stanje iz trenutka kopije; sve posle nje se gubi, a **svi se prijavljuju ponovo**.
- Dotadašnja baza se ne briše, nego dobija ime `..._before_restore_<vreme>`. Kad proveriš da je
  sve u redu: `docker compose exec db dropdb -U strengthplanner <ime>`.
- Vraćanje se odbija dok API odgovara ili je neko povezan na bazu.
- Na novom serveru, pre vraćanja, servis namerno **ne** pravi kopiju prazne baze, da ne bi
  potisnuo pravu. Ako se aplikacija ipak pokrene pre vraćanja i neko se registruje, ta baza
  može postati poslednja kopija: zato se kopija bira po datumu, a ne uzima naslepo poslednja.
- Za vraćanje i probu vraćanja treba slobodnog prostora na disku za još jednu bazu i njen dump.

Servis `backup` ima i superkorisnički nalog baze, jer probno i pravo vraćanje prave i
preimenuju baze. Sadržaj kopije ipak upisuje aplikacijski nalog, a superkorisnik izvršava samo
naredbe zapisane u skriptama. Servis ne objavljuje nijedan port.

Ceo ciklus proverava `scripts/test-backup.py` (i CI), sa probnim SSH serverom umesto Storage
Box-a.

## Šta ostaje na tebi

- **Nadogradnja osnovnih imidža.** `postgres:16`, `caddy:2-alpine`, `aspnet:8.0` i
  `nginx-unprivileged:1.27` dobijaju bezbednosne ispravke; povremeno prođi kroz korake iz
  odeljka „Nova verzija“.
- **Migracije se izvršavaju pri svakom startu API-ja.** Zgodno za demo. Za pravu produkciju je
  uobičajeno razdvojiti korak migracije od pokretanja aplikacije, da nadogradnja ne bi menjala
  šemu u trenutku kad se dižu instance.
- **Pre prve prave isporuke pokreni migracije nad kopijom stvarne baze.** Probni sklop u testovima je SQLite i ne
  može da izvrši migraciju koja je pisana kao SQL nad PostgreSQL-om (poslednja takva je `SplitBackHeads`, koja
  prevodi naučene granice leđa na dve grupe). Uzmi `pg_dump`, vrati ga u zasebnu bazu, primeni migracije i uporedi
  broj redova i naučene granice pre i posle; tek onda u pravu bazu. Razvojna baza je do sada bila jedina na kojoj su
  takve migracije izvršene.

## Poznata ograničenja

- **Zaključavanje naloga posle promašaja treba doraditi** pre javnog puštanja: postepeno
  usporavanje umesto tvrdog zaključavanja, ili brojač vezan za par (nalog, IP). Oba menjaju
  ponašanje prijave, pa nisu dirana.
- **Zatečene kratke lozinke ostaju.** Pravilo o dužini važi kad se lozinka postavlja, ne kad
  se proverava, pa nalozi napravljeni ranije zadržavaju svoje. Prisilna promena traži polje
  „lozinka promenjena" i tok koji korisnika na to natera.
- **Registraciji nedostaje potvrda email-om.** Uvodi se zajedno sa slanjem pošte, a uz nju i
  potpuno ujednačen odgovor registracije.
- **Ograničenje broja zahteva deli budžet po IP adresi.** Više korisnika iza istog NAT-a
  (kućni ruter, teretana, fakultet) deli 20 zahteva u minutu na prijavi i promeni lozinke, 5
  na sat na registraciji i 60 u minutu na obnavljanju prijave i odjavi. Za prijavu je to
  prihvatljivo; ako postane smetnja, sve tri granice su na jednom mestu u `Program.cs`.
- **Tokeni stoje u `localStorage`.** Uobičajen kompromis za SPA — kolačić sa `httpOnly` bi tražio
  zaštitu od CSRF-a. CSP je ono što ovde nosi odbranu.
- **Odjava bez mreže** odjavljuje uređaj; prijava na serveru tada ističe sama, posle 30 dana.
  „Odjavi me svuda" postoji samo kao promena lozinke.
