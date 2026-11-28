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

log() {
    printf '%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*"
}

fail() {
    log "GREŠKA: $*" >&2
    exit 1
}

# Superkorisnik: pravi, briše i preimenuje baze pri proveri i vraćanju.
as_admin() {
    local tool=$1
    shift
    PGPASSWORD="$POSTGRES_PASSWORD" "$tool" --username "$POSTGRES_USER" --no-password "$@"
}

# Jedna vrednost iz zadate baze.
query() {
    as_admin psql -X -v ON_ERROR_STOP=1 --dbname "$1" -tAc "$2"
}

# Broj naloga u bazi; nula i kada tabele još nema (prazna baza pre prve migracije).
account_count() {
    if [ "$(query "$1" "SELECT to_regclass('public.\"AspNetUsers\"') IS NOT NULL")" != t ]; then
        echo 0
        return
    fi
    query "$1" 'SELECT count(*) FROM "AspNetUsers"'
}

# Napravi skladište ako ga nema. Pogrešna lozinka ili nedostupan server ne smeju da liče na
# „nema ga": tada bi se pravilo novo pored pravog.
ensure_repository() {
    local status=0
    local output
    output=$(restic cat config 2>&1) || status=$?
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
}

has_snapshot() {
    restic snapshots "${SNAPSHOT_FILTER[@]}" --json | grep -q '"short_id"'
}

# Vraća kopiju u novu bazu zadatog imena; postojeća baza tog imena se briše.
load_snapshot() {
    local snapshot=$1 target=$2
    local dump
    dump=$(mktemp /tmp/restore.XXXXXX)
    # Prvo cela kopija na disk: pg_restore tada ne može da krene nad prekinutim prenosom.
    restic dump "${SNAPSHOT_FILTER[@]}" "$snapshot" "/$DUMP_NAME" > "$dump"
    as_admin dropdb --if-exists --force "$target"
    as_admin createdb --template template0 "$target"
    as_admin pg_restore --dbname "$target" --single-transaction --exit-on-error "$dump"
    rm -f "$dump"
}
