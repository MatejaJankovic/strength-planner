-- Briše nalog i sve što je njegovo, po adresi e-pošte. Treba samo kada korisnik to ne može sam:
-- na primer kada se posle vraćanja baze iz kopije ponovo pojavi nalog koji je bio obrisan.
-- Redosled je isti kao u AuthService.DeleteAccountAsync: šabloni i sopstvene vežbe nemaju strani ključ
-- ka nalogu, pa ih brisanje naloga samo ne bi odnelo. Test DeleteAccountScriptTests izvršava ovaj fajl.
--
--   docker compose exec -T db sh -c 'psql -U "$APP_DB_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -v email="$0"' \
--     adresa@primer.rs < deploy/delete-account.sql
BEGIN;

CREATE TEMPORARY TABLE removed AS
    SELECT "Id" FROM "AspNetUsers" WHERE "NormalizedEmail" = upper(:'email');

DELETE FROM "UserWorkoutTemplates" WHERE "UserId" IN (SELECT "Id" FROM removed);
DELETE FROM "AspNetUsers" WHERE "Id" IN (SELECT "Id" FROM removed);
DELETE FROM "Exercises" WHERE "CreatedByUserId" IN (SELECT "Id" FROM removed);

SELECT count(*) AS "obrisano naloga" FROM removed;
DROP TABLE removed;

COMMIT;
