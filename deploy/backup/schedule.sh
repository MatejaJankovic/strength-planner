#!/bin/bash
# Glavni proces kontejnera: kopija pri pokretanju, pa svakog dana u BACKUP_HOUR (UTC), i proba
# vraćanja kad od poslednje prođe VERIFY_EVERY_DAYS dana.
source /usr/local/lib/backup/common.sh

hour=${BACKUP_HOUR:-2}
verify_every=$(( ${VERIFY_EVERY_DAYS:-7} * 86400 ))
retry_after=3600

trap 'exit 0' TERM INT

# Čekanje u pozadini, da `docker compose stop` ne mora da ga prekine silom.
pause() {
    sleep "$1" &
    wait $!
}

seconds_until_next_run() {
    local now next
    now=$(date -u +%s)
    next=$(date -u -d "today $hour:00" +%s)
    if [ "$next" -le "$now" ]; then
        next=$(date -u -d "tomorrow $hour:00" +%s)
    fi
    echo $(( next - now ))
}

verification_due() {
    local last=0
    [ -f "$STATE_DIR/last-verify" ] && last=$(cat "$STATE_DIR/last-verify")
    [ -f "$STATE_DIR/verify-failed" ] || [ $(( $(date -u +%s) - last )) -ge "$verify_every" ]
}

log "Rezervne kopije: svakog dana u $hour:00 UTC."
while true; do
    # Neuspeh ne gasi kontejner: sledeći pokušaj je za sat vremena, a do tada se na nedostatak
    # uspešne kopije žali healthcheck.
    if /usr/local/lib/backup/backup.sh; then
        if has_snapshot && verification_due; then
            /usr/local/lib/backup/verify.sh || log "GREŠKA: proba vraćanja nije uspela."
        fi
        pause "$(seconds_until_next_run)"
    else
        log "Kopija nije uspela, ponavljam za sat vremena."
        pause "$retry_after"
    fi
done
