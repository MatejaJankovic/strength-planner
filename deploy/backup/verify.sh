#!/bin/bash
# Proba vraćanja: kopija se vrati u privremenu bazu, proveri i obriše. Prava baza se ne dira.
#
#   verify.sh [kopija]      (podrazumevano poslednja)
source /usr/local/lib/backup/common.sh

snapshot=${1:-latest}
target="${POSTGRES_DB}_verify"

# Ostaje na disku dok neka kasnija provera ne uspe, pa je kontejner do tada „unhealthy".
touch "$STATE_DIR/verify-failed"
trap 'as_admin dropdb --if-exists --force "$target" || true' EXIT

restic check --quiet
load_snapshot "$snapshot" "$target"

accounts=$(account_count "$target")
[ "$accounts" -gt 0 ] || fail "vraćena kopija nema nijedan nalog."
migrations=$(query "$target" 'SELECT count(*) FROM "__EFMigrationsHistory"')
[ "$migrations" -gt 0 ] || fail "vraćena kopija nema istoriju migracija."

rm -f "$STATE_DIR/verify-failed"
date -u +%s > "$STATE_DIR/last-verify"
log "Proba vraćanja uspela: nalozi $accounts, migracije $migrations."
