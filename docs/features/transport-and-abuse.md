# Zaglavlja, TLS, heširanje i automati

## Bezbednosna zaglavlja na samom API-ju

nginx ih je slao već ranije, ali samo na saobraćaj koji zaista prođe kroz njega. API se
pokreće i bez njega — u razvoju, u testovima, i u svakoj isporuci gde neko objavi port ili
promeni proxy. Do sada je tada odgovor išao go:

```
HTTP/1.1 401 Unauthorized
Server: Kestrel
```

Sada:

```
Cache-Control: no-store
Content-Security-Policy: default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: no-referrer
Permissions-Policy: geolocation=(), microphone=(), camera=()
```

Pravilo je uže nego na frontendu, i to je tačno a ne strogo: API vraća isključivo JSON, pa
mu ništa ne treba. Ako se takav odgovor ipak negde prikaže kao dokument, nema šta da se
izvrši. `Cache-Control: no-store` je tu jer odgovori nose lične trenažne podatke i nemaju
šta da traže u deljenom kešu.

`Server: Kestrel` više ne izlazi. Besplatan podatak o tome šta se traži i koje ranjivosti
okvira ima smisla probati; nginx svoju verziju krije već, ovo je isti potez na drugom kraju.

## TLS je sada jedna naredba, a ne uputstvo

TLS je bio jedina preostala stavka koju kod nije mogao da reši. Uputstvo koje kaže „stavi
neki reverse proxy" u praksi znači da to niko ne uradi, pa sada postoji
`docker-compose.tls.yml` sa Caddy-jem:

```bash
APP_DOMAIN=tvoj-domen.rs TLS_EMAIL=ti@primer.com \
  docker compose -f docker-compose.yml -f docker-compose.tls.yml up -d
```

Caddy sam pribavlja i obnavlja Let's Encrypt sertifikat, sam preusmerava HTTP na HTTPS i
sam šalje HSTS. Uz TLS se pali i `Security__RequireHttps` na API-ju, pa tek tada i on
preusmerava i šalje HSTS.

Zašto to nije podrazumevano: u kontejneru API sluša čist HTTP, a TLS se prekida ispred
njega. Preusmeravanje bez proxy-ja napravi petlju. Zato je uslovljeno konfiguracijom i
uključuje se zajedno sa TLS-om, u istom fajlu.

HSTS namerno šalje Caddy, jer je on jedino mesto koje pouzdano zna da TLS postoji. U nginx
konfiguraciji ga i dalje nema: preko čistog HTTP-a ga pregledači ignorišu, a aplikaciju bi
zaključao ako se ikada posluži bez TLS-a.

### Dva proxy-ja, tri stvari koje su morale da se poklope

Prva verzija ovoga nije radila ništa, i to se videlo tek merenjem. Sa
`Security__RequireHttps=true` i zahtevom preko čistog HTTP-a odgovor je bio **401, bez
preusmeravanja i bez HSTS-a**, uz `Failed to determine the https port for redirect` u logu.
Tri odvojena uzroka:

1. **Preusmeravanje nije znalo port.** `UseHttpsRedirection` ga traži među adresama na
   kojima Kestrel sluša, a iza proxy-ja on sluša samo čist HTTP — pa ga ne nađe, zapiše
   upozorenje i propusti zahtev. Sada se port zadaje eksplicitno. Izmereno posle ispravke:
   307 na `https://.../api/templates`, i upozorenja više nema.

2. **nginx je prepisivao šemu.** Caddy prekine TLS i dalje šalje čist HTTP, a nginx je
   slao `X-Forwarded-Proto $scheme` — dakle svoju šemu, „http" — preko onoga što je Caddy
   postavio. API bi zato i pod punim TLS-om svaki zahtev video kao HTTP: HSTS se ne bi
   slao nikada, a da je preusmeravanje radilo, vrtelo bi se u petlji. Sada nginx prosleđuje
   zaglavlje koje je dobio, a svoju šemu koristi samo kada ga nema.

3. **Adresa klijenta bi se izgubila.** `ForwardLimit` je bio 1, što odgovara lancu
   klijent → nginx → API. Sa Caddy-jem ih je dva: Caddy upiše klijenta u `X-Forwarded-For`,
   nginx dopiše Caddy-jevu adresu. Uzimao bi se samo poslednji upis, pa bi **svi korisnici
   dobili istu adresu i jednu zajedničku particiju** ograničenja broja zahteva — jedan
   posetilac bi iscrpeo registraciju za sve. Broj proxy-ja se sada podešava, i
   `docker-compose.tls.yml` ga postavlja na 2 jer taj drugi proxy sam i dodaje.

> **Šta jeste, a šta nije pokrenuto.** Preusmeravanje i odsustvo upozorenja su izmereni nad
> pokrenutim API-jem. Ostalo — prosleđivanje šeme kroz nginx, broj proxy-ja, i sam Caddy —
> traži pokrenutu Docker mrežu, a Docker nije bio dostupan: prosleđena zaglavlja se namerno
> uzimaju u obzir samo sa privatnih adresa, pa se lanac ne može odglumiti sa petlje. Pre
> oslanjanja uraditi `docker compose ... up` na domenu koji zaista pokazuje na tu mašinu i
> proveriti da odgovor nosi `Strict-Transport-Security`.

## Heširanje lozinki

Lozinke i dalje hešira Identity (PBKDF2-HMAC-SHA512, so po lozinci) — sopstvenog heširanja
i dalje nema i ne treba ga biti. Jedini deo koji ima smisla dizati je broj iteracija, jer on
određuje koliko košta pogađanje ako baza ikada iscuri. .NET 8 podrazumeva 100.000; OWASP za
SHA-512 preporučuje 210.000, i toliko je sada.

Izmena je unazad kompatibilna: broj iteracija je upisan u sam heš, pa se zatečene lozinke i
dalje proveravaju svojim starim brojem i niko nije izbačen iz naloga. Cena je jedno
heširanje po prijavi, ne po zahtevu.

## Automati

Registracija je jedino mesto gde neko ko nije prijavljen može da pravi zapise. Dobila je dve
stvari.

**Svoju, mnogo užu granicu: 5 na sat po adresi**, umesto zajedničkih 20 u minutu. Ta
zajednička granica je namerno labava da prijava ostane upotrebljiva kada nekoliko ljudi deli
izlaznu adresu; za registraciju je to previše, jer se nalog pravi jednom, a dvadeset naloga
u minutu je isključivo automat.

Provereno da su budžeti odvojeni: posle iscrpljene registracije prijava i dalje vraća 200.

| pokušaj registracije | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| odgovor | 200 | 200 | 200 | 429 | 429 | 429 | 429 |

**Zamku (honeypot).** Skriveno polje koje čovek ne vidi, ne može da tabuje do njega, ne
može mu doći ni čitačem ekrana, i koje pregledač ne popunjava automatski. Automat koji redom
puni sva polja ga popuni, i takva registracija se odbija istom porukom kao svaki drugi
neuspeh — da se ne oda šta ga je otkrilo.

Provereno nad pokrenutim API-jem: popunjeno polje → 400, prazno → 200. Provereno i u
pregledaču: polje je van ekrana (`left: -9999px`), `aria-hidden`, `tabindex="-1"`,
`autocomplete="off"`, i ne pojavljuje se u stablu pristupačnosti. Prava registracija kroz
formular i dalje prolazi do onboardinga.

Skriva se stilom, a ne atributom `hidden` ni `type="hidden"`, jer jednostavniji automati
upravo ta dva preskaču.

**Ovo nije CAPTCHA i ne treba ga tako čitati.** Zamka zaustavlja opšte automate, ne nekoga
ko je pročitao ovaj kod. CAPTCHA nije uzeta svesno: značila bi slanje IP adrese svakog
korisnika Google-u ili Cloudflare-u pri svakoj registraciji i rupu u CSP-u za njihove
skripte, a pravu granicu ovde ionako postavlja broj registracija po adresi. Ako aplikacija
ikada postane meta ozbiljnijeg spama, mesto za Turnstile ili hCaptcha je isti onaj
kontroler, uz izmenu CSP-a.
