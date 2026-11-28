#!/bin/bash
# Jedna rezervna kopija: dump baze u šifrovano skladište, pa brisanje starih kopija.
#
#   backup.sh          obična kopija
#   backup.sh keep     kopija koja se ne briše, npr. pre nove verzije
source /usr/local/lib/backup/common.sh

tags=()
case "${1:-}" in
    "") ;;
    keep) tags=(--tag keep) ;;
    *) fail "nepoznat argument '$1'; dozvoljen je samo keep." ;;
esac

succeeded() {
    date -u +%s > "$STATE_DIR/last-success"
    report_if_healthy
}

take_turn
ensure_repository

# Prazna baza se ne kopira. Na novom serveru, pre vraćanja, takva kopija bi postala „poslednja"
# i potisnula pravu iz istog dana.
accounts=$(account_count "$POSTGRES_DB") || fail "baza nije dostupna, kopija nije napravljena."
if [ "$accounts" -eq 0 ]; then
    if has_snapshot; then
        fail "baza nema nijedan nalog, a kopije postoje. Ako je ovo oporavak, pokreni restore.sh."
    fi
    log "Baza još nema nijedan nalog, nema šta da se kopira."
    succeeded
    exit 0
fi

# `--stdin-from-command`: ako pg_dump padne, kopija se ne upisuje. Sa običnim cevovodom bi se
# prekinut dump sačuvao kao uspešan. Bez kompresije u dumpu, da restic može da prepozna iste
# delove između noći i sam ih sažme.
PGPASSWORD="$APP_DB_PASSWORD" restic backup --quiet \
    "${SNAPSHOT_FILTER[@]}" "${tags[@]}" \
    --stdin-filename "$DUMP_NAME" \
    --stdin-from-command -- \
    pg_dump --format=custom --compress=0 --no-password --username "$APP_DB_USER" \
        --lock-wait-timeout="${DUMP_LOCK_WAIT:-10min}" "$POSTGRES_DB"
log "Kopija je sačuvana: nalozi $accounts."

apply_retention
log "Stare kopije su obrisane po pravilu čuvanja."

succeeded
