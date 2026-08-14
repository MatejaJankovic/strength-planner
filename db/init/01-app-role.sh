#!/bin/sh
# Pravi nalog kojim se aplikacija povezuje na bazu.
#
# Ranije se aplikacija povezivala nalogom iz POSTGRES_USER, a to je u postgres imidžu
# superkorisnik — vlasnik svega, sa pravom da menja i briše šemu. Nije mu to trebalo:
# aplikacija čita i piše redove, a šemu menja EF migracijama pri startu.
#
# Nalog dobija pravo nad podacima i nad postojećim objektima, plus podrazumevana prava
# na sve što migracije naprave kasnije. Ostaje mu i pravo da pravi tabele u javnoj šemi,
# jer migracije rade pod njim — ali nije superkorisnik, pa ne može van svoje baze.
#
# Vrednosti se prosleđuju kao psql promenljive, a ne ubacuju u tekst upita.
# Ranije su išle direktno u SQL, pa je lozinka sa apostrofom prekidala string: u najboljem
# slučaju skripta pukne i baza ostane bez naloga, u najgorem se ostatak lozinke protumači
# kao SQL. Ovde `:'ime'` znači „ubaci kao literal, sa navodnicima i escape-om", a `:"ime"`
# isto to za identifikator — isti posao koji parametrizovan upit radi u kodu.
#
# Heredoc je pod navodnicima ('EOSQL'), pa ljuska ništa ne razrešava unutra; sve vrednosti
# ulaze isključivo kroz -v.
set -eu

# Prazna vrednost ne sme da prođe tiho. Sa praznom lozinkom PostgreSQL napravi nalog bez
# lozinke (provereno: rolpassword ostaje NULL), pa se aplikacija posle ne može prijaviti —
# a greška se vidi tek kao neuspela veza pri startu API-ja, daleko od mesta gde je nastala.
for required in POSTGRES_USER POSTGRES_DB APP_DB_USER APP_DB_PASSWORD; do
    eval "value=\${$required:-}"
    if [ -z "$value" ]; then
        echo "01-app-role.sh: $required nije postavljen. Vidi .env.example." >&2
        exit 1
    fi
done

psql -v ON_ERROR_STOP=1 \
    --username "$POSTGRES_USER" \
    --dbname "$POSTGRES_DB" \
    -v app_user="$APP_DB_USER" \
    -v app_password="$APP_DB_PASSWORD" \
    -v db_name="$POSTGRES_DB" <<'EOSQL'
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'app_user', :'app_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = :'app_user')
\gexec

GRANT CONNECT ON DATABASE :"db_name" TO :"app_user";
GRANT USAGE, CREATE ON SCHEMA public TO :"app_user";

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO :"app_user";
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO :"app_user";

ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO :"app_user";
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO :"app_user";
EOSQL
