#!/bin/bash
# Zajedničko za skripte rezervnih kopija. Učitava se sa `source`, ne pokreće se.
set -euo pipefail

: "${POSTGRES_DB:?}" "${POSTGRES_USER:?}" "${POSTGRES_PASSWORD:?}"
: "${APP_DB_USER:?}" "${APP_DB_PASSWORD:?}"
: "${RESTIC_REPOSITORY:?}" "${RESTIC_PASSWORD:?}"

export PGHOST="${PGHOST:-db}"
export RESTIC_CACHE_DIR=/cache

STATE_DIR=/state
DUMP_NAME=strengthplanner.dump
# Stalno ime, a ne ime kontejnera: čuvanje i brisanje kopija se grupiše po njemu, pa bi nov
# kontejner inače počinjao svoju grupu i stare kopije nikad ne bi bile obrisane.
SNAPSHOT_HOST=strengthplanner
SNAPSHOT_FILTER=(--host "$SNAPSHOT_HOST" --tag db)
# Dump skinut radi vraćanja; briše ga `remove_dump`, koji svaka skripta zove pri izlasku.
SCRATCH_DUMP=

log() {
    printf '%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*"
}

fail() {
    log "GREŠKA: $*" >&2
    exit 1
}

# Superkorisnik izvršava samo naredbe zapisane ovde, i nikada ne čita tabele aplikacije.
# Sadržaj baze piše aplikacijski nalog: funkcija ili pogled koji on napravi izvršili bi se sa
# pravima onoga ko ih čita, pa bi superkorisnik time tom nalogu predao ceo server baze.
as_admin() {
    local tool=$1
    shift
    PGPASSWORD="$POSTGRES_PASSWORD" "$tool" --username "$POSTGRES_USER" --no-password "$@"
}

as_app() {
    local tool=$1
    shift
    PGPASSWORD="$APP_DB_PASSWORD" "$tool" --username "$APP_DB_USER" --no-password "$@"
}

# Jedna vrednost iz zadate baze, pod zadatim nalogom.
admin_query() {
    as_admin psql -X -v ON_ERROR_STOP=1 --dbname "$1" -tAc "$2"
}

app_query() {
    as_app psql -X -v ON_ERROR_STOP=1 --dbname "$1" -tAc "$2"
}

# Broj naloga u bazi; nula i kada tabele još nema (prazna baza pre prve migracije). Upit koji ne
# uspe je greška, a ne nula: prazna baza i nedostupna baza ne smeju da liče.
account_count() {
    local exists
    exists=$(app_query "$1" "SELECT to_regclass('public.\"AspNetUsers\"') IS NOT NULL") || return 1
    if [ "$exists" != t ]; then
        echo 0
        return
    fi
    app_query "$1" 'SELECT count(*) FROM "AspNetUsers"'
}

# Jedan posao u isto vreme: raspored i ručne naredbe dele volumen /state, pa i ovaj katanac.
# Drži ga proces, pa ga prekinut posao sam pušta.
take_turn() {
    exec 9> "$STATE_DIR/lock"
    flock -w "${TURN_WAIT_SECONDS:-3600}" 9 || fail "drugi posao sa kopijama još traje."
}

# Napravi skladište ako ga nema. Pogrešna lozinka ili nedostupan server ne smeju da liče na
# „nema ga": tada bi se pravilo novo pored pravog.
#
# Zatim ukloni katance. Prekinut posao (restart servera, ubijen kontejner) ostavi katanac koji
# restic sam ne bi uklonio pola sata, a posle uzastopnih prekida ni tada. Brisanje je bezbedno
# jer u skladište piše samo ovaj server, a `take_turn` jemči da drugi posao ovde ne traje.
ensure_repository() {
    local status=0
    local output
    output=$(restic cat config --no-lock 2>&1) || status=$?
    case $status in
        0) ;;
        10)
            log "Skladište ne postoji, pravim ga."
            restic init
            ;;
        *)
            printf '%s\n' "$output" >&2
            fail "skladište nije dostupno (restic je izašao sa $status)."
            ;;
    esac
    restic unlock --remove-all --quiet
}

has_snapshot() {
    local list
    list=$(restic snapshots --no-lock "${SNAPSHOT_FILTER[@]}" --json) || fail "spisak kopija nije pročitan."
    grep -q '"short_id"' <<< "$list"
}

# Pravilo čuvanja. `--keep-within 3d` čuva sve iz poslednja tri dana, da nova kopija ne obriše
# raniju iz istog dana pre nego što neko primeti da nešto nije u redu; kopije napravljene sa
# `backup.sh keep` ne brišu se nikada.
apply_retention() {
    restic forget --quiet \
        "${SNAPSHOT_FILTER[@]}" \
        --keep-within 3d \
        --keep-daily "${KEEP_DAILY:-14}" \
        --keep-weekly "${KEEP_WEEKLY:-8}" \
        --keep-monthly "${KEEP_MONTHLY:-6}" \
        --keep-tag keep \
        --prune
}

# Javlja spoljnom nadzoru samo kada je sve u redu, po istom pravilu kao healthcheck. Nadzor se
# oglašava kada javljanje izostane.
report_if_healthy() {
    if [ -n "${BACKUP_PING_URL:-}" ] && /usr/local/lib/backup/healthcheck.sh; then
        curl -fsS -m 20 --retry 3 -o /dev/null "$BACKUP_PING_URL" \
            || log "UPOZORENJE: nadzor nije primio javljanje."
    fi
}

remove_dump() {
    if [ -n "$SCRATCH_DUMP" ]; then
        rm -f "$SCRATCH_DUMP"
        SCRATCH_DUMP=
    fi
}

# Vraća kopiju u novu bazu zadatog imena; postojeća baza tog imena se briše. Bazu pravi i u
# njoj, dok je još prazna, daje prava superkorisnik; sadržaj upisuje aplikacijski nalog, pa
# ništa iz kopije ne može da se izvrši sa većim pravima od njegovih. Prava odgovaraju onima iz
# db/init/01-app-role.sh.
load_snapshot() {
    local snapshot=$1 target=$2
    SCRATCH_DUMP=$(mktemp /tmp/restore.XXXXXX)
    # Prvo cela kopija na disk: pg_restore tada ne može da krene nad prekinutim prenosom.
    restic dump "${SNAPSHOT_FILTER[@]}" "$snapshot" "/$DUMP_NAME" > "$SCRATCH_DUMP"
    as_admin dropdb --if-exists --force "$target"
    as_admin createdb --template template0 "$target"
    as_admin psql -X -q -v ON_ERROR_STOP=1 --dbname "$target" -v app="$APP_DB_USER" -v db="$target" <<'SQL'
GRANT CONNECT ON DATABASE :"db" TO :"app";
GRANT USAGE, CREATE ON SCHEMA public TO :"app";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO :"app";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO :"app";
SQL
    as_app pg_restore --dbname "$target" --no-owner --no-privileges \
        --single-transaction --exit-on-error "$SCRATCH_DUMP"
    remove_dump
}
