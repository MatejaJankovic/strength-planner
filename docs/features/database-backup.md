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
  koja ostaje pod drugim imenom.
- **Prijave se pri vraćanju brišu**, jer kopija može da oživi prijavu koja je posle nje
  odjavljena.

## Provera

`scripts/test-backup.py`, na pravim kontejnerima, sa SSH serverom na portu 23 umesto Storage
Box-a; pokreće ga i CI:

- prazna baza se ne kopira; kopija stiže preko SFTP-a;
- `pg_dump` koji padne, pogrešna lozinka skladišta i nedostupno skladište daju grešku i nijednu
  novu kopiju;
- od 59 kopija raspoređenih na 60 dana ostaju samo one po pravilu;
- servis pri pokretanju pravi i probno vraća kopiju; stara kopija ga čini nezdravim;
- obrisani volumeni baze i stanja: prazan server ne pravi kopiju, vraćanje se odbija dok API
  radi, a posle njega postoje nalog i plan iz kopije, kasniji nalog ne, stara prijava ne važi,
  aplikacija upisuje, a prethodna baza je sačuvana.

## Ograničenja

- Pravi Storage Box nije proban; test koristi običan OpenSSH server na istom portu.
- Kopija je jednom dnevno: u najgorem slučaju gubi se jedan dan.
- Ko dobije pristup serveru ima i ključ skladišta, pa može da obriše kopije. Zato uputstvo
  traži snapshot-ove na Storage Box-u.
- Za kopiju i probu vraćanja treba privremenog prostora na disku veličine baze.
