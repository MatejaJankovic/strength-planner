# syntax=docker/dockerfile:1

# Zakačena verzija: alat koji čita rezervne kopije ne sme sam da se promeni ispod njih.
FROM restic/restic:0.20.0 AS restic

# Ista glavna verzija kao baza u docker-compose.yml: pg_dump mora biti bar toliko nov koliko
# i server, a kopija se vraća istim alatima.
FROM postgres:16

# ssh za SFTP skladište (Hetzner Storage Box), curl za javljanje spoljnom nadzoru.
RUN apt-get update \
    && apt-get install -y --no-install-recommends openssh-client curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=restic /usr/bin/restic /usr/local/bin/restic
COPY *.sh /usr/local/lib/backup/

RUN chmod 755 /usr/local/lib/backup/*.sh \
    && ln -s /usr/local/lib/backup/backup.sh /usr/local/lib/backup/restore.sh \
             /usr/local/lib/backup/verify.sh /usr/local/bin/

# Ulazna skripta kreće kao root samo da bi prepisala SSH ključ, pa prelazi na korisnika
# `postgres`; sve ostalo radi bez root prava.
ENTRYPOINT ["/usr/local/lib/backup/entrypoint.sh"]
CMD ["/usr/local/lib/backup/schedule.sh"]
