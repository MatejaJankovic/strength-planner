#!/bin/bash
# Jedna rezervna kopija: dump baze u šifrovano skladište, pa brisanje starih kopija.
source /usr/local/lib/backup/common.sh

succeeded() {
    date -u +%s > "$STATE_DIR/last-success"
    if [ -n "${BACKUP_PING_URL:-}" ]; then
        curl -fsS -m 20 --retry 3 -o /dev/null "$BACKUP_PING_URL" \
            || log "UPOZORENJE: nadzor nije primio javljanje."
    fi
}

ensure_repository

# Prazna baza se ne kopira. Na novom serveru, pre vraćanja, takva kopija bi postala „poslednja"
# i potisnula pravu iz istog dana.
if [ "$(account_count "$POSTGRES_DB")" -eq 0 ]; then
    if has_snapshot; then
        fail "baza nema nijedan nalog, a kopije postoje. Ako je ovo oporavak, pokreni restore.sh."
    fi
    log "Baza još nema nijedan nalog, nema šta da se kopira."
    succeeded
    exit 0
fi

# Briše samo katance koje je ostavio prekinut proces.
restic unlock > /dev/null

# `--stdin-from-command`: ako pg_dump padne, kopija se ne upisuje. Sa običnim cevovodom bi se
# prekinut dump sačuvao kao uspešan. Bez kompresije u dumpu, da restic može da prepozna iste
# delove između noći i sam ih sažme.
PGPASSWORD="$APP_DB_PASSWORD" restic backup --quiet \
    "${SNAPSHOT_FILTER[@]}" \
    --stdin-filename "$DUMP_NAME" \
    --stdin-from-command -- \
    pg_dump --format=custom --compress=0 --no-password --username "$APP_DB_USER" "$POSTGRES_DB"
log "Kopija je sačuvana."

# `--keep-within 3d` čuva sve iz poslednja tri dana, da nova kopija ne obriše raniju iz istog
# dana pre nego što neko primeti da nešto nije u redu.
restic forget --quiet \
    "${SNAPSHOT_FILTER[@]}" \
    --keep-within 3d \
    --keep-daily "${KEEP_DAILY:-14}" \
    --keep-weekly "${KEEP_WEEKLY:-8}" \
    --keep-monthly "${KEEP_MONTHLY:-6}" \
    --prune
log "Stare kopije su obrisane po pravilu čuvanja."

succeeded
