#!/bin/bash
# Glavni proces kontejnera: kopija pri pokretanju, pa svakog dana u BACKUP_HOUR (UTC), i proba
# vraćanja kad od poslednje prođe VERIFY_EVERY_DAYS dana.
source /usr/local/lib/backup/common.sh

hour=${BACKUP_HOUR:-2}
case "$hour" in
    [0-9] | 0[0-9] | 1[0-9] | 2[0-3]) ;;
    *) fail "BACKUP_HOUR mora biti sat od 0 do 23, a jeste '$hour'." ;;
esac
days=${VERIFY_EVERY_DAYS:-7}
[[ $days =~ ^[1-9][0-9]*$ ]] || fail "VERIFY_EVERY_DAYS mora biti ceo broj dana, a jeste '$days'."

# Pola dana manje: prošla proba je zabeležena malo posle sata kopije, pa bi se pun razmak
# navršio tek dan kasnije.
verify_after=$(( days * 86400 - 43200 ))
retry_after=3600
# Posao koji ne odgovara (zaglavljena veza ka skladištu) prekida se, da raspored ne stane.
limit=${BACKUP_TIMEOUT:-3h}

child=
stop() {
    if [ -n "$child" ]; then
        # `timeout` prosleđuje signal celoj grupi, pa restic stigne da ukloni svoj katanac.
        kill -TERM "$child" 2> /dev/null || true
        wait "$child" 2> /dev/null || true
    fi
    exit 0
}
trap stop TERM INT

run() {
    local status=0
    timeout --kill-after=1m "$limit" "$@" &
    child=$!
    wait "$child" || status=$?
    child=
    return "$status"
}

# Čekanje u pozadini, da `docker compose stop` ne mora da ga prekine silom.
pause() {
    sleep "$1" &
    child=$!
    wait "$child" || true
    child=
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
    [ -f "$STATE_DIR/verify-failed" ] || [ $(( $(date -u +%s) - last )) -ge "$verify_after" ]
}

log "Rezervne kopije: svakog dana u $hour:00 UTC."
while true; do
    # Neuspeh ne gasi kontejner: sledeći pokušaj je za sat vremena, a do tada se na nedostatak
    # uspešne kopije žali healthcheck.
    if run /usr/local/lib/backup/backup.sh; then
        if verification_due && (has_snapshot); then
            run /usr/local/lib/backup/verify.sh || log "GREŠKA: proba vraćanja nije uspela."
        fi
        pause "$(seconds_until_next_run)"
    else
        log "Kopija nije uspela, ponavljam za sat vremena."
        pause "$retry_after"
    fi
done
