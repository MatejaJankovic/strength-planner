#!/bin/bash
# Vraća kopiju na mesto prave baze. Prava baza se ne briše nego preimenuje.
#
#   docker compose stop api backup
#   docker compose up -d db
#   docker compose run --rm backup restic snapshots       (izaberi kopiju po datumu)
#   docker compose run --rm backup restore.sh <id>        (ili latest)
#   docker compose up -d
source /usr/local/lib/backup/common.sh

[ $# -eq 1 ] || fail "navedi koju kopiju vraćaš: restore.sh latest, ili id iz \`restic snapshots\`."
snapshot=$1
staging="${POSTGRES_DB}_restore"

# Preimenovanje traži da na bazu niko nije povezan. API koji miruje ne drži vezu, pa se pita i
# on sam; sve provere idu pre dugog posla.
if curl -s -m 5 -o /dev/null "http://${API_HOST:-api}:8080/api/auth/me"; then
    fail "API radi. Prvo ga zaustavi: docker compose stop api"
fi
connected() {
    admin_query postgres \
        "SELECT count(*) FROM pg_stat_activity WHERE datname = '$POSTGRES_DB' AND pid <> pg_backend_pid()"
}

take_turn
ensure_repository
exists=$(admin_query postgres "SELECT count(*) FROM pg_database WHERE datname = '$POSTGRES_DB'")
open=$(connected)
[ "$open" -eq 0 ] || fail "na bazu je neko povezan ($open). Zatvori te veze pa pokreni ponovo."

if [ "$snapshot" = latest ]; then
    restic snapshots "${SNAPSHOT_FILTER[@]}" --latest 1 --compact
else
    restic snapshots --compact "$snapshot"
fi

trap 'remove_dump; as_admin dropdb --if-exists --force "$staging" || true' EXIT
log "Vraćam kopiju '$snapshot' u privremenu bazu $staging."
load_snapshot "$snapshot" "$staging"
accounts=$(account_count "$staging") || fail "vraćena baza nije čitljiva; prava baza nije dirana."
[ "$accounts" -gt 0 ] || fail "kopija nema nijedan nalog; prava baza nije dirana."

# Prijave iz kopije se brišu: među njima mogu biti one koje su posle nje odjavljene ili
# ugašene promenom lozinke. Svi se prijavljuju ponovo.
sessions=$(app_query "$staging" "SELECT to_regclass('public.\"LoginSessions\"') IS NOT NULL")
if [ "$sessions" = t ]; then
    app_query "$staging" 'DELETE FROM "LoginSessions"' > /dev/null
fi

open=$(connected)
[ "$open" -eq 0 ] || fail "neko se u međuvremenu povezao na bazu; prava baza nije dirana."

if [ "$exists" -eq 1 ]; then
    previous="${POSTGRES_DB}_before_restore_$(date -u +%Y%m%d%H%M%S)"
    admin_query postgres "ALTER DATABASE \"$POSTGRES_DB\" RENAME TO \"$previous\"" > /dev/null
    log "Dotadašnja baza je sačuvana kao $previous."
fi
admin_query postgres "ALTER DATABASE \"$staging\" RENAME TO \"$POSTGRES_DB\"" > /dev/null

log "Vraćeno: nalozi $accounts. Pokreni aplikaciju: docker compose up -d"
