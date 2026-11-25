# Lokalni HTTPS za telefon

Runda 19, grana 1 od 4. Preduslov za instalaciju na početni ekran i za „ekran se ne gasi" (sledeća
grana): service worker, manifest sa instalacijom i Wake Lock rade samo u bezbednom kontekstu. Računar
`localhost` smatra bezbednim, ali pregledač na telefonu adresu računara u mreži (`http://192.168.x.x:4200`)
ne smatra, pa bi sve to na telefonu bilo tiho isključeno.

## Šta je urađeno

- `scripts/local-https.sh` pravi lokalni sertifikacioni autoritet (jednom) i sertifikat servera za
  `localhost`, ime računara (golo i `ime.local`) i njegove IPv4 adrese u mreži (plus adrese i imena
  zadata kao argumenti). **CA ide u `~/.strength-planner-ca`** (van repozitorijuma, zajednički za sve
  kopije repozitorijuma: ovaj projekat se radi u više worktree-ova, a telefon ne sme da uči novi CA po
  kopiji), **sertifikat i ključ servera u `.certs/`** (u `.gitignore` i `.dockerignore`), odakle ih čita
  razvojni server. Koristi samo `openssl` (dolazi uz Git za Windows); ništa se ne preuzima. Sertifikat
  servera traje 397 dana, CA deset godina.
- Konfiguracija `lan` u Angularu (`npm run start:lan`): HTTPS sa tim sertifikatom, sluša na svim
  adresama, sopstveno okruženje (`environment.lan.ts`) sa relativnim `/api`, i proksi
  (`proxy.conf.json`) koji `/api` prosleđuje na API na računaru.
- Obična komanda `npm start` i okruženje `development` nisu menjani: i dalje traže
  `http://localhost:5241/api`.

## Zašto proksi, a ne HTTPS na API-ju

Telefon je tražio dve adrese (aplikaciju i API), a „localhost" na telefonu je sam telefon. Sa proksijem
telefon traži samo jednu adresu i jedan izvor, preko HTTPS-a: nema mešovitog sadržaja (HTTPS stranica
koja traži `http://` API), nema CORS-a, i API ne mora da sluša ni na čemu osim na `localhost`.
API pokretan sa `--launch-profile http` ostaje kakav je bio.

## Merenja

- Sertifikat: `openssl verify` prolazi; SAN nosi `localhost`, `127.0.0.1`, ime računara i adrese u
  mreži; `serverAuth`; `CA:FALSE`; CA ima `CA:TRUE, pathlen:0`.
- Razvojni server u konfiguraciji `lan`, preko `curl --cacert`: HTTPS 200 na `localhost` i na adresi
  računara u mreži; bez CA-a veza se odbija (telefon pre instalacije CA-a vidi isto); `/api` kroz
  proksi vraća 401 bez tokena, a prijava 200. `openssl s_client` potvrđuje i IP adresu (u sertifikatu:
  OK; van njega: „IP address mismatch") i ime računara.
- `ng build --configuration lan` ne sadrži `localhost:5241` nigde, a nosi relativni `/api`.
- **Greška koja je izgledala kao greška sertifikata:** `curl` na Windowsu (Schannel) je odbijao
  lanac sa `CERT_TRUST_REVOCATION_STATUS_UNKNOWN`, jer privatni CA nema CRL. `openssl s_client` je isti
  lanac potvrdio, a `curl --ssl-no-revoke` takođe. Telefoni ne traže opoziv za korisnički instalirane CA.

## Šta je pregled našao (ispravljeno)

Pregled agentom je našao propuste u skripti, i svaki je ponovljen i ispravljen:

- **Neuspelo potpisivanje je ostavljalo ključ koji ne odgovara sertifikatu, bez poruke.** Ključ se pravio
  pre potpisivanja, a greške su išle u `/dev/null`: pogrešno otkucana adresa (`192.168.1.300`) je tiho
  prekidala skriptu, a `start:lan` bi pao sa „key values mismatch", daleko od uzroka. Sada se sve pravi u
  privremenom direktorijumu i seli tek kad lanac prođe proveru, greške se ispisuju, adrese se proveravaju
  (okteti 0-255) i skripta ne ostavlja ni `.csr` ni `.ext`. Moja prva ispravka je pustila `192.168.1.300`
  kao *ime* računara (`DNS:192.168.1.300`) i tiho napravila sertifikat; to je našao sopstveni test, pa
  broj sa tačkama koji nije ispravna adresa sada prekida skriptu.
- **Parsiranje `ipconfig` zavisi od jezika Windowsa**: izlaz je u kodnoj strani sistema, pa UTF-8 `sed`
  pada na slovima pre „IPv4" i pravi smeće umesto adrese. Sada radi pod `LC_ALL=C`, i skripta
  upozorava kad ne nađe nijednu adresu u mreži (sertifikat tada važi samo za `localhost`).
- **Iz PowerShell-a `bash` može da bude WSL** (ovde je prvi na putanji `C:\Windows\System32\bash.exe`),
  koji vidi drugu adresu: README i zaglavlje skripte sada kažu da se pokreće u Git Bash-u.
- **CA je bio u radnom stablu**, pa je svaki worktree pravio svoj, a telefon bi skupljao „StrengthPlanner
  lokalni CA" po kopiji. CA je sada u `~/.strength-planner-ca`.
- **Polovina CA-a se tiho menjala novim** (poverenje na svakom telefonu bi nestalo): sada je greška.
- Ključevi se prave sa `umask 077` i `chmod 600`; golo ime računara dobija i `ime.local`; IPv6 se preskače
  umesto da uđe u sertifikat kao nevažeće ime; `.certs/` je i u `.dockerignore` (kontekst izgradnje API-ja
  je koren repozitorijuma, pa bi ključ inače išao Docker-u).
- **Pogrešan razlog za 397 dana**: Apple-ovo ograničenje važi za javne CA, ne za korisnički instalirane.
  Rok je i dalje kratak, jer ključ stoji nešifrovan na disku.
- Iz pregleda još: skripta ne radi na macOS-u (`declare -A`, `ipconfig`); pretpostavljeno je samo Git za
  Windows. `lan` izgradnja je razvojna (`production: false`), pa će sledeća grana morati da registruje
  service worker i ovde, ne samo u produkciji.

## Šta nije proveravano

- **Instalacija CA-a na pravom telefonu i otvaranje aplikacije sa njega.** Nije bilo uređaja; lanac,
  imena i protokol su provereni sa računara, ali ne i ponašanje Androida ili iPhone-a (npr. iPhone traži
  da se poverenje CA-a posebno uključi, što skripta ispisuje). To je prvo što treba isprobati.
- Pregled u Browser panelu aplikacije ne veruje lokalnom CA-u, pa ekran preko HTTPS-a nije otvaran
  iz njega; ekrani su proveravani preko obične konfiguracije.

## Šta treba znati

- Razvojni server u `lan` konfiguraciji sluša na svim adresama: dok radi, svaki uređaj u istoj mreži
  može da otvori aplikaciju i da kroz proksi stigne do API-ja. Koristi se samo u poverljivoj mreži i dok
  je potrebno. (Angular to i sam upozorava.)
- Ko ima `.certs/ca.key` može da izda sertifikat za bilo koji sajt telefonu koji veruje ovom CA-u. Ključ
  ne napušta računar, ne šalje se i ne commit-uje (`.certs/` je ignorisan), a CA se sa telefona uklanja
  kad pristup više nije potreban.
- API ograničava broj zahteva po adresi pošiljaoca (prijava 20 u minuti, registracija 5 na sat); kroz
  proksi svi zahtevi stižu sa `127.0.0.1`, pa dele jedan budžet, a `X-Forwarded-For` se ne veruje (loopback
  nije u poznatim mrežama, što je dobro: uređaj iz mreže ne može da se predstavi drugom adresom). Posledica
  je da svaki uređaj u mreži, dok server radi, može sa oko 21 zahteva u minuti da potroši budžet prijave
  vlasnika, i da (5 na sat) pravi naloge u razvojnoj bazi. Za razvoj u kućnoj mreži to je prihvatljivo, ali
  je razlog više za „samo u poverljivoj mreži".
- Ovo je za razvoj. Produkcioni HTTPS je `docker-compose.tls.yml` i ne dira se.
