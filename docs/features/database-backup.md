# Rezervne kopije

## Problem

Podaci su postojali samo u Docker volumenu na jednoj mašini.

## Rešenje

`docker-compose.backup.yml` dodaje servis `backup` (`deploy/backup`), na imidžu `postgres:16`
sa alatom restic 0.20.0:

- **noćna kopija**: `pg_dump` pod aplikacijskim nalogom, direktno u šifrovano skladište van
  servera (SFTP na Storage Box ili S3), uz čuvanje 14 dnevnih, 8 nedeljnih i 6 mesečnih;
- **nedeljna proba vraćanja** u privremenu bazu, i pri prvom pokretanju;
- **oporavak** jednom naredbom, `restore.sh`;
- healthcheck i neobavezno javljanje spoljnom nadzoru.

Uputstvo je u [`../deployment-security.md`](../deployment-security.md), odeljak „Rezervne
kopije“.

### Odluke

- **restic**, jer šifrovanje, čuvanje, uklanjanje duplikata i SFTP i S3 dolaze zajedno.
- **Dump ide kroz `--stdin-from-command`**: kada `pg_dump` padne, kopija se ne upisuje. Cevovod
  bi sačuvao prekinut dump kao uspešan.
- **Prazna baza se ne kopira** ako kopije već postoje. Nov server posle kvara bi inače napravio
  „poslednju“ kopiju bez ijednog naloga, a pravilo čuvanja bi njome zamenilo pravu iz istog
  dana. Uz to se čuva sve iz poslednja tri dana.
- **Vraćanje ne briše**: kopija se vrati u novu bazu, proveri, pa zameni mesta sa postojećom,
  koja ostaje pod drugim imenom. Odbija se dok API odgovara ili je neko povezan.
- **Sadržaj kopije upisuje aplikacijski nalog**, a superkorisnik samo pravi, preimenuje i briše
  baze. Ono što taj nalog zapiše u bazu tako se nikada ne izvrši sa većim pravima.
- **Jedan posao u isto vreme**, preko katanca na zajedničkom volumenu, pa se pre svakog posla
  smeju ukloniti katanci u skladištu koje je ostavio prekinut posao.
- **Nadzoru se javlja samo dok je servis zdrav**, a posao koji se zaglavi prekida se.
- **`backup.sh keep`** pravi kopiju koja se ne briše, za stanje pre nove verzije.
- **Prijave se pri vraćanju brišu**, jer kopija može da oživi prijavu koja je posle nje
  odjavljena.

## Provera

`scripts/test-backup.py`, na pravim kontejnerima, sa SSH serverom na portu 23 umesto Storage
Box-a; pokreće ga i CI:

- prazna baza se ne kopira, a baza koja ne odgovara je greška; kopija stiže preko SFTP-a;
- `pg_dump` koji padne, pogrešna lozinka skladišta i nedostupno skladište daju grešku i nijednu
  novu kopiju;
- pravilo čuvanja nad stalnim datumima daje tačno očekivani skup, uz kopiju koja se čuva zauvek;
- neuspela proba vraćanja čini servis nezdravim, ućutkuje javljanje i ne ostavlja dump na disku;
- funkcija koju aplikacijski nalog podmetne u bazu ne izvršava se pod superkorisnikom;
- zaostao katanac posle ubijenog posla i skladište koje ćuti ne zaustavljaju naredne kopije;
- obrisani volumeni baze i stanja: prazan server ne pravi kopiju, vraćanje se odbija dok API
  radi ili je neko povezan, kopija bez naloga se ne vraća, a posle vraćanja postoje nalog i plan
  iz kopije, kasniji nalog ne, stara prijava ne važi, aplikacija upisuje i menja šemu.

## Šta je pregled našao

- Greška pri upitu čitala se kao prazna baza, pa je pogrešna lozinka davala „uspeh“ bez kopije.
- Katanac posle prekinutog posla zaustavljao je sve naredne kopije i vraćanje, a zaglavljena
  veza ceo raspored.
- Vraćanje pod superkorisnikom izvršavalo je sadržaj baze sa njegovim pravima.
- Nadzoru se javljalo i dok proba vraćanja pada; neuspele probe ostavljale su dump na disku.
- Uputstvo za nov server pokretalo je aplikaciju pre vraćanja.

## Ograničenja

- Pravi Storage Box nije proban; test koristi običan OpenSSH server na istom portu.
- Kopija je jednom dnevno: u najgorem slučaju gubi se jedan dan.
- Raspored po satu i nedeljni razmak probe vraćanja nisu pokriveni testom.
- Automatske snapshot-ove na Storage Box-u, zaštitu ako se kopije obrišu sa servera, treba
  uključiti u Hetzner konzoli.
