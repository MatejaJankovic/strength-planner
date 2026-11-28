#!/bin/bash
# Vraća kopiju na mesto prave baze. Prava baza se ne briše nego preimenuje.
#
#   docker compose stop api
#   docker compose run --rm backup restore.sh latest      (ili id iz `restic snapshots`)
#   docker compose up -d
source /usr/local/lib/backup/common.sh

[ $# -eq 1 ] || fail "navedi koju kopiju vraćaš: restore.sh latest, ili id iz \`restic snapshots\`."
snapshot=$1
staging="${POSTGRES_DB}_restore"
exists=$(query postgres "SELECT count(*) FROM pg_database WHERE datname = '$POSTGRES_DB'")

# Preimenovanje traži da na bazu niko nije povezan; provera ide pre dugog posla.
connected() {
    query postgres \
        "SELECT count(*) FROM pg_stat_activity WHERE datname = '$POSTGRES_DB' AND pid <> pg_backend_pid()"
}
[ "$(connected)" -eq 0 ] || fail "na bazu je neko povezan. Prvo zaustavi API: docker compose stop api"

log "Vraćam kopiju '$snapshot' u privremenu bazu $staging."
load_snapshot "$snapshot" "$staging"
accounts=$(account_count "$staging")
[ "$accounts" -gt 0 ] || fail "kopija nema nijedan nalog; prava baza nije dirana."

# Prijave iz kopije se brišu: među njima mogu biti one koje su posle nje odjavljene ili
# ugašene promenom lozinke. Svi se prijavljuju ponovo.
if [ "$(query "$staging" "SELECT to_regclass('public.\"LoginSessions\"') IS NOT NULL")" = t ]; then
    query "$staging" 'DELETE FROM "LoginSessions"' > /dev/null
fi

[ "$(connected)" -eq 0 ] || fail "neko se u međuvremenu povezao na bazu; prava baza nije dirana."

if [ "$exists" -eq 1 ]; then
    previous="${POSTGRES_DB}_before_restore_$(date -u +%Y%m%d%H%M%S)"
    query postgres "ALTER DATABASE \"$POSTGRES_DB\" RENAME TO \"$previous\"" > /dev/null
    log "Dotadašnja baza je sačuvana kao $previous."
fi
query postgres "ALTER DATABASE \"$staging\" RENAME TO \"$POSTGRES_DB\"" > /dev/null

log "Vraćeno: nalozi $accounts. Pokreni aplikaciju: docker compose up -d"
