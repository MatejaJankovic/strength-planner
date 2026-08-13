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
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    DO \$\$
    BEGIN
        IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '${APP_DB_USER}') THEN
            CREATE ROLE ${APP_DB_USER} LOGIN PASSWORD '${APP_DB_PASSWORD}';
        END IF;
    END
    \$\$;

    GRANT CONNECT ON DATABASE ${POSTGRES_DB} TO ${APP_DB_USER};
    GRANT USAGE, CREATE ON SCHEMA public TO ${APP_DB_USER};

    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO ${APP_DB_USER};
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO ${APP_DB_USER};

    ALTER DEFAULT PRIVILEGES IN SCHEMA public
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO ${APP_DB_USER};
    ALTER DEFAULT PRIVILEGES IN SCHEMA public
        GRANT USAGE, SELECT ON SEQUENCES TO ${APP_DB_USER};
EOSQL
