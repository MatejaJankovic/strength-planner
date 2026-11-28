#!/bin/bash
# Priprema ono za šta treba root, pa prelazi na korisnika `postgres`.
#
# SSH ključ stiže montiran samo za čitanje, u vlasništvu korisnika sa hosta. Korisnik u
# kontejneru ga tako ne može pročitati, a ssh uz to odbija ključ sa preširokim pravima, pa se
# prepisuje u njegov folder sa pravima 600.
set -euo pipefail

home=$(getent passwd postgres | cut -d: -f6)

if [ -d /ssh-src ] && [ -n "$(ls -A /ssh-src)" ]; then
    install -d -m 700 -o postgres -g postgres "$home/.ssh"
    cp -L /ssh-src/* "$home/.ssh/"
    chown postgres:postgres "$home"/.ssh/*
    chmod 600 "$home"/.ssh/*
fi

chown postgres:postgres /state /cache

# Skladište na lokalnom disku (putanja, a ne adresa) mora biti upisivo istom korisniku.
case "${RESTIC_REPOSITORY:-}" in
    /*)
        mkdir -p "$RESTIC_REPOSITORY"
        chown postgres:postgres "$RESTIC_REPOSITORY"
        ;;
esac

exec gosu postgres "$@"
