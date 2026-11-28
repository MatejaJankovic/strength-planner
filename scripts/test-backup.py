"""Ceo ciklus rezervnih kopija na pravim kontejnerima: kopija, gubitak baze, vraćanje.

Pokretanje (iz korena repozitorijuma, traži pokrenut Docker; traje nekoliko minuta):

    python scripts/test-backup.py

Zašto postoji. Kopija koju niko nije vratio nije provereno rešenje, a vraćanje se kvari tiho:
nova migracija, nova tabela ili promena prava na bazi mogu da učine da se dump pravi uredno, a
da se aplikacija nad vraćenom bazom ne podigne. Skripta pravi sopstveni compose projekat
(`sp-backup-test`) sa nasumičnim tajnama i SSH serverom koji glumi Storage Box, i na kraju ga
briše sa svim volumenima. Postojeće kontejnere i volumene ne dira.
"""
from __future__ import annotations

import datetime
import json
import os
import pathlib
import re
import secrets
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parent.parent
WORK = ROOT / "scripts" / "backup-test" / ".work"
PROJECT = "sp-backup-test"
API_PORT = 15999
API = f"http://127.0.0.1:{API_PORT}/api"
PASSWORD = "backup-test-" + secrets.token_urlsafe(12)
APP_ROLE = "strengthplanner_app"
# Adresa koja ne postoji: javljanje nadzoru se vidi po upozorenju da nije stiglo.
PING_ATTEMPT = "nadzor nije primio javljanje"
SECRETS: dict[str, str] = {}


def compose(*arguments: str, check: bool = True) -> subprocess.CompletedProcess:
    command = ["docker", "compose", "-p", PROJECT, "--env-file", str(WORK / "env"),
               "-f", "docker-compose.yml", "-f", "docker-compose.backup.yml",
               "-f", "scripts/backup-test/compose.yml", *arguments]
    result = subprocess.run(command, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if check and result.returncode != 0:
        raise AssertionError(f"`{' '.join(arguments)}` je izašao sa {result.returncode}:\n"
                             f"{result.stdout[-3000:]}\n{result.stderr[-3000:]}")
    return result


def tool(*command: str, check: bool = True, environment: dict | None = None) -> subprocess.CompletedProcess:
    """Jedna komanda u novom backup kontejneru, kao `docker compose run --rm backup ...`."""
    overrides = [part for key, value in (environment or {}).items() for part in ("-e", f"{key}={value}")]
    return compose("run", "--rm", "-T", *overrides, "backup", *command, check=check)


def output_of(result: subprocess.CompletedProcess) -> str:
    return result.stdout + result.stderr


def snapshots(environment: dict | None = None) -> list[dict]:
    output = tool("restic", "snapshots", "--no-lock", "--json", "--host", "strengthplanner", "--tag", "db",
                  environment=environment).stdout
    return json.loads(output[output.index("["):])


def refused(name: str, expected: str, *command: str, environment: dict | None = None) -> str:
    """Komanda koja mora da padne sa zadatom porukom."""
    result = tool(*command, check=False, environment=environment)
    output = output_of(result)
    assert result.returncode != 0, f"{name}: komanda je uspela\n{output[-2000:]}"
    assert expected in output, f"{name}: nema poruke „{expected}“\n{output[-2000:]}"
    return output


def sql(database: str, statement: str) -> str:
    return compose("exec", "-T", "db", "psql", "-U", "strengthplanner", "-d", database,
                   "-v", "ON_ERROR_STOP=1", "-tAc", statement).stdout.strip()


def call(method: str, path: str, body: dict | None = None, token: str | None = None) -> tuple[int, object]:
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    request = urllib.request.Request(
        API + path, method=method, data=None if body is None else json.dumps(body).encode(), headers=headers)
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as error:
        return error.code, None


def as_app(database: str, statement: str) -> str:
    return compose("exec", "-T", "-e", f"PGPASSWORD={SECRETS['APP_DB_PASSWORD']}", "db", "psql", "-h", "127.0.0.1",
                   "-U", APP_ROLE, "-d", database, "-v", "ON_ERROR_STOP=1", "-tAc", statement).stdout.strip()


def hold_connection(statements: str) -> None:
    """Veza koja stoji otvorena u pozadini, dok je `release_connections` ne prekine."""
    compose("exec", "-d", "db", "psql", "-U", "strengthplanner", "-d", "strengthplanner",
            "-c", f"{statements} SELECT pg_sleep(600);")
    wait_until("veza u pozadini", lambda: sql(
        "postgres", "SELECT count(*) FROM pg_stat_activity WHERE query LIKE '%pg_sleep(600)%' "
                    "AND state = 'active' AND pid <> pg_backend_pid() AND wait_event = 'PgSleep'") == "1")


def release_connections() -> None:
    sql("postgres", "SELECT pg_terminate_backend(pid) FROM pg_stat_activity "
                    "WHERE query LIKE '%pg_sleep(600)%' AND pid <> pg_backend_pid()")
    wait_until("prekid veze u pozadini", lambda: sql(
        "postgres", "SELECT count(*) FROM pg_stat_activity WHERE datname = 'strengthplanner' "
                    "AND query LIKE '%pg_sleep(600)%' AND pid <> pg_backend_pid()") == "0")


def wait_until(description: str, condition, seconds: int = 180) -> None:
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        try:
            if condition():
                return
        except OSError:
            pass
        time.sleep(2)
    raise AssertionError(f"isteklo je {seconds} s: {description}\n{compose('logs', '--tail', '60', check=False).stdout}")


def wait_for_api() -> None:
    wait_until("API se nije podigao", lambda: call("GET", "/auth/me")[0] == 401)


def logs(service: str) -> str:
    return compose("logs", "--no-color", service).stdout


def healthy() -> bool:
    return compose("exec", "-T", "backup", "/usr/local/lib/backup/healthcheck.sh", check=False).returncode == 0


def register(name: str) -> dict:
    status, account = call("POST", "/auth/register", {
        "email": f"{name}@example.test", "password": PASSWORD, "displayName": name, "age": 30,
        "bodyweightKg": 80, "experienceLevel": 1})
    assert status == 200, f"registracija {name}: {status}"
    return account


def login(name: str) -> int:
    return call("POST", "/auth/login", {"email": f"{name}@example.test", "password": PASSWORD})[0]


def passed(message: str) -> None:
    print(f"ok  {message}", flush=True)


def prepare() -> None:
    shutil.rmtree(WORK, ignore_errors=True)
    (WORK / "client").mkdir(parents=True)
    (WORK / "server").mkdir(parents=True)
    values = {
        "POSTGRES_PASSWORD": secrets.token_hex(16),
        "APP_DB_PASSWORD": secrets.token_hex(16),
        "JWT_KEY": secrets.token_hex(32),
        "RESTIC_PASSWORD": secrets.token_hex(16),
        "RESTIC_REPOSITORY": "sftp:storagebox:backups",
        "BACKUP_SSH_DIR": "./scripts/backup-test/.work/client",
        "BACKUP_PING_URL": "http://nadzor.invalid/javljanje",
        "API_PORT": str(API_PORT),
    }
    SECRETS.update(values)
    (WORK / "env").write_text("".join(f"{key}={value}\n" for key, value in values.items()), encoding="utf-8")

    compose("build", "api", "backup", "storagebox")

    # Ključevi se prave u kontejneru, da skripta ne traži ssh-keygen na domaćinu.
    def keypair() -> tuple[str, str]:
        output = compose("run", "--rm", "-T", "--no-deps", "--entrypoint", "sh", "backup", "-c",
                         "ssh-keygen -q -t ed25519 -N '' -C test -f /tmp/key && cat /tmp/key && echo @@ "
                         "&& cat /tmp/key.pub").stdout
        private, public = output.split("@@\n")
        return private, public.strip()

    client_private, client_public = keypair()
    host_private, host_public = keypair()
    files = {
        "client/id_ed25519": client_private,
        # Isti oblik koji uputstvo traži na serveru.
        "client/config": "Host storagebox\n  HostName storagebox\n  Port 23\n  User box\n"
                         "  IdentityFile ~/.ssh/id_ed25519\n  ConnectTimeout 30\n"
                         "  ServerAliveInterval 60\n  ServerAliveCountMax 3\n"
                         # Isti server bez SSH rokova: posao nad njim može da prekine samo rok posla.
                         "Host storagebox-bez-roka\n  HostName storagebox\n  Port 23\n  User box\n"
                         "  IdentityFile ~/.ssh/id_ed25519\n",
        "client/known_hosts": f"[storagebox]:23 {host_public}\n",
        "server/authorized_keys": client_public + "\n",
        "server/host_key": host_private,
    }
    for name, content in files.items():
        (WORK / name).write_bytes(content.replace("\r\n", "\n").encode("utf-8"))


def expected_retention(times: list[datetime.datetime], kept_by_tag: set) -> set:
    """Pravilo čuvanja zapisano drugi put, nezavisno od skripte: 3 dana, 14/8/6, i oznaka keep."""
    newest_first = sorted(times, reverse=True)
    keep = set(kept_by_tag)
    keep |= {moment for moment in times if moment > newest_first[0] - datetime.timedelta(days=3)}
    for count, bucket in ((14, lambda moment: moment.date()),
                          (8, lambda moment: moment.isocalendar()[:2]),
                          (6, lambda moment: (moment.year, moment.month))):
        seen: list = []
        for moment in newest_first:
            key = bucket(moment)
            if key not in seen and len(seen) < count:
                seen.append(key)
                keep.add(moment)
    return keep


def check_retention() -> None:
    """Stalni datumi u zasebnom skladištu, da ishod ne zavisi od dana kada se test pokreće."""
    day = lambda year, month, date, hour=2: datetime.datetime(year, month, date, hour)
    last = day(2026, 3, 31)
    times = [last - datetime.timedelta(days=back) for back in range(0, 20)]
    times += [day(2026, 3, 30, 9), day(2026, 3, 29, 15), day(2026, 3, 29, 9)]
    times += [day(2026, 3, 8) - datetime.timedelta(weeks=week) for week in range(0, 10)]
    times += [day(year, month, date) for year, month in
              ((2025, 8), (2025, 9), (2025, 10), (2025, 11), (2025, 12), (2026, 1)) for date in (3, 17)]
    times = sorted(set(times))
    marked = day(2025, 2, 1)
    repository = {"RESTIC_REPOSITORY": "sftp:storagebox:retention"}

    def seed(moment: datetime.datetime, extra: str = "") -> str:
        return (f"restic backup --quiet --host strengthplanner --tag db {extra} --time '{moment:%Y-%m-%d %H:%M:%S}' "
                "--stdin-filename strengthplanner.dump --stdin-from-command -- echo stara")

    script = ["set -e", "restic init >/dev/null", seed(marked, "--tag keep")] + [seed(moment) for moment in times]
    script.append("source /usr/local/lib/backup/common.sh; apply_retention")
    tool("bash", "-c", "\n".join(script), environment=repository)

    kept = {datetime.datetime.strptime(snapshot["time"][:19], "%Y-%m-%dT%H:%M:%S")
            for snapshot in snapshots(repository)}
    expected = expected_retention(times + [marked], {marked})
    assert kept == expected, (f"višak: {sorted(kept - expected)}\nmanjak: {sorted(expected - kept)}")
    passed(f"pravilo čuvanja: od {len(times) + 1} kopija ostalo tačno {len(kept)}, među njima označena iz 2025.")


def run() -> None:
    compose("up", "-d", "db", "api", "storagebox")
    wait_for_api()

    output = tool("backup.sh").stdout
    assert "nema šta da se kopira" in output and PING_ATTEMPT in output, output
    assert snapshots() == []
    passed("prazna baza na novoj instalaciji se ne kopira, a nadzoru se javlja da je sve u redu")

    output = refused("nedostupna baza", "baza nije dostupna", "backup.sh", environment={"APP_DB_PASSWORD": "pogresna"})
    assert "nema šta da se kopira" not in output and PING_ATTEMPT not in output, output
    passed("baza koja ne odgovara je greška, a ne prazna baza")

    anna = register("anna")
    status, _ = call("POST", "/macrocycles", {
        "name": "Plan pre kopije", "startDate": "2026-01-05T00:00:00Z",
        "blocks": [{"goal": 1, "templateKey": "full-body", "periodizationModel": 0}]}, anna["token"])
    assert status == 201, f"plan: {status}"
    output = tool("backup.sh").stdout
    assert "Kopija je sačuvana: nalozi 1." in output and PING_ATTEMPT in output, output
    assert len(snapshots()) == 1
    passed("kopija stiže u skladište preko SFTP-a na portu 23")

    hold_connection('BEGIN; LOCK TABLE "SetLogs" IN ACCESS EXCLUSIVE MODE;')
    refused("pg_dump koji padne", "canceling statement due to", "backup.sh", environment={"DUMP_LOCK_WAIT": "3s"})
    release_connections()
    assert len(snapshots()) == 1
    passed("pg_dump koji padne ne upisuje kopiju, i ne čeka katanac u nedogled")

    for name, environment, expected in (
            ("pogrešna lozinka skladišta", {"RESTIC_PASSWORD": "pogresna"}, "restic je izašao sa 12"),
            ("nedostupno skladište", {"RESTIC_REPOSITORY": "sftp:box@ne-postoji:kopije"}, "skladište nije dostupno")):
        refused(name, expected, "backup.sh", environment=environment)
        assert len(snapshots()) == 1, f"{name}: nastala je nova kopija"
        passed(f"{name}: greška, bez nove kopije")

    check_retention()

    tool("backup.sh", "keep")
    assert sum("keep" in snapshot["tags"] for snapshot in snapshots()) == 1
    passed("`backup.sh keep` pravi kopiju koju pravilo čuvanja ne briše")

    # Proba vraćanja koja padne: nezdravo, bez javljanja, i bez zaostalog dumpa.
    output = tool("bash", "-c", "verify.sh; verify.sh; echo ostaci=$(ls /tmp | grep -c '^restore')",
                  environment={"APP_DB_PASSWORD": "pogresna"}, check=False).stdout
    assert "ostaci=0" in output and PING_ATTEMPT not in output, output
    assert tool("/usr/local/lib/backup/healthcheck.sh", check=False).returncode != 0
    output = tool("backup.sh").stdout
    assert "Kopija je sačuvana" in output and PING_ATTEMPT not in output, output
    output = tool("verify.sh").stdout
    assert "Proba vraćanja uspela" in output and PING_ATTEMPT in output, output
    assert tool("/usr/local/lib/backup/healthcheck.sh", check=False).returncode == 0
    passed("neuspela proba vraćanja čini servis nezdravim i ućutkuje javljanje, dok neka ne uspe")

    # Aplikacijski nalog podmeće funkciju koja bi ga, izvršena pod superkorisnikom, uzdigla.
    as_app("strengthplanner", """CREATE FUNCTION public.escalate() RETURNS boolean LANGUAGE plpgsql AS $f$
        BEGIN IF current_setting('is_superuser') = 'on' THEN EXECUTE 'ALTER ROLE strengthplanner_app SUPERUSER'; END IF;
        RETURN true; END $f$; CREATE TABLE public.probe (x int CHECK (public.escalate())); INSERT INTO probe VALUES (1);""")
    tool("backup.sh")
    tool("verify.sh")
    assert sql("postgres", f"SELECT rolsuper FROM pg_roles WHERE rolname = '{APP_ROLE}'") == "f"
    as_app("strengthplanner", "DROP TABLE public.probe; DROP FUNCTION public.escalate();")
    tool("backup.sh")
    passed("sadržaj kopije se pri vraćanju ne izvršava pod superkorisnikom")

    # Posao ubijen dok drži katanac na skladištu.
    name = f"{PROJECT}-slow"
    compose("run", "-d", "--name", name, "backup", "restic", "check", "--read-data", "--limit-download", "1")
    wait_until("katanac na skladištu", lambda: tool("restic", "list", "locks", "--no-lock").stdout.strip() != "")
    subprocess.run(["docker", "kill", name], check=True, capture_output=True)
    subprocess.run(["docker", "rm", "-f", name], check=True, capture_output=True)
    assert tool("restic", "list", "locks", "--no-lock").stdout.strip() != ""
    assert "Kopija je sačuvana" in tool("backup.sh").stdout
    passed("katanac koji ostane posle ubijenog posla ne zaustavlja naredne kopije")

    # Skladište koje primi vezu pa ćuti.
    name = f"{PROJECT}-hung"
    compose("pause", "storagebox")
    compose("run", "-d", "--no-deps", "--name", name, "-e", "BACKUP_TIMEOUT=20s",
            "-e", "RESTIC_REPOSITORY=sftp:storagebox-bez-roka:backups", "backup")
    try:
        wait_until("prekid zaglavljenog posla", lambda: "Kopija nije uspela" in output_of(
            subprocess.run(["docker", "logs", name], capture_output=True, text=True, encoding="utf-8")), 120)
    finally:
        subprocess.run(["docker", "rm", "-f", name], check=True, capture_output=True)
        compose("unpause", "storagebox")
    passed("zaglavljeno skladište ne zaustavlja raspored: posao se prekida i ponavlja")

    refused("pogrešan sat", "BACKUP_HOUR", "/usr/local/lib/backup/schedule.sh", environment={"BACKUP_HOUR": "24"})
    passed("neispravan BACKUP_HOUR se odbija pre prve kopije")

    compose("exec", "-T", "backup", "true", check=False)
    tool("bash", "-c", "rm -f /state/last-verify")
    compose("up", "-d", "backup")
    wait_until("proba vraćanja pri pokretanju", lambda: "Proba vraćanja uspela: nalozi 1" in logs("backup"))
    wait_until("brisanje privremene baze", lambda: sql(
        "postgres", "SELECT count(*) FROM pg_database WHERE datname LIKE '%_verify'") == "0", 60)
    assert healthy()
    passed("servis pri pokretanju pravi kopiju, probno je vraća i briše privremenu bazu")

    compose("exec", "-T", "backup", "touch", "-d", "30 hours ago", "/state/last-success")
    assert not healthy()
    passed("bez uspešne kopije duže od 26 sati kontejner nije zdrav")

    started = time.monotonic()
    compose("stop", "backup")
    assert time.monotonic() - started < 30, "servis se nije zaustavio na signal"
    passed("zaustavljanje servisa ne čeka da ga Docker ubije")

    register("bojan")
    before = len(snapshots())

    # Server je izgubljen: nova mašina, prazan volumen baze i prazno stanje kopija.
    compose("rm", "-sf", "db", "api", "backup")
    for volume in ("strengthplanner_pgdata", "backup_state", "backup_cache"):
        subprocess.run(["docker", "volume", "rm", f"{PROJECT}_{volume}"], check=True, capture_output=True)
    compose("up", "-d", "db", "api", "backup")
    wait_for_api()
    wait_until("odbijanje kopije prazne baze", lambda: "a kopije postoje" in logs("backup"))
    assert len(snapshots()) == before and not healthy()
    passed("prazan nov server ne pravi kopiju preko postojećih i nije zdrav")

    refused("vraćanje dok API radi", "API radi", "restore.sh", "latest")
    compose("stop", "api", "backup")
    hold_connection("")
    refused("vraćanje dok je neko povezan", "na bazu je neko povezan", "restore.sh", "latest")
    release_connections()
    assert sql("strengthplanner", 'SELECT count(*) FROM "AspNetUsers"') == "0"
    passed("vraćanje se odbija dok API radi ili je neko povezan na bazu")

    refused("bez izabrane kopije", "navedi koju kopiju", "restore.sh")
    output = tool("bash", "-c",
                  "PGPASSWORD=$APP_DB_PASSWORD restic backup --host strengthplanner --tag db "
                  "--stdin-filename strengthplanner.dump --stdin-from-command -- "
                  "pg_dump --format=custom -U $APP_DB_USER -h db postgres").stdout
    # restic uz to pomene i prethodnu kopiju, pa se čita red o upravo sačuvanoj.
    empty = re.search(r"snapshot ([0-9a-f]+) saved", output).group(1)
    refused("kopija bez naloga", "kopija nema nijedan nalog", "restore.sh", empty)
    tool("restic", "forget", empty)
    assert sql("postgres", "SELECT count(*) FROM pg_database WHERE datname LIKE '%\\_restore'") == "0"
    passed("kopija bez naloga se ne vraća na mesto baze")

    output = tool("restore.sh", "latest").stdout
    assert "Vraćeno: nalozi 1" in output, output
    compose("up", "-d", "api")
    wait_for_api()

    status, account = call("POST", "/auth/login", {"email": "anna@example.test", "password": PASSWORD})
    assert status == 200, f"prijava posle vraćanja: {status}"
    status, plan = call("GET", "/macrocycles/active", token=account["token"])
    assert status == 200 and plan["name"] == "Plan pre kopije", (status, plan)
    assert login("bojan") == 401
    status = call("POST", "/auth/refresh", {"refreshToken": anna["refreshToken"]})[0]
    assert status == 401, f"stara prijava posle vraćanja: {status}"
    passed("posle vraćanja postoje nalog i plan iz kopije, kasniji nalog ne, stara prijava ne važi")

    # Aplikacijski nalog i dalje sme da upisuje i da menja šemu, što migracije traže.
    register("ceca")
    api_log = logs("api")
    assert "42501" not in api_log and "permission denied" not in api_log, api_log[-3000:]
    owners = sql("strengthplanner",
                 "SELECT string_agg(DISTINCT tableowner, ',') FROM pg_tables WHERE schemaname = 'public'")
    assert owners == APP_ROLE, owners
    assert sql("strengthplanner", f"SELECT has_schema_privilege('{APP_ROLE}', 'public', 'CREATE')") == "t"
    as_app("strengthplanner", "CREATE TABLE public.next_migration (x int); DROP TABLE public.next_migration;")
    assert sql("postgres", "SELECT count(*) FROM pg_database WHERE datname LIKE '%_before_restore_%'") == "1"
    passed("aplikacija piše i menja šemu vraćene baze, a prethodna baza je sačuvana")

    compose("restart", "api")
    wait_for_api()
    assert login("anna") == 200
    tool("backup.sh")
    tool("verify.sh")
    passed("posle oporavka kopija i proba vraćanja ponovo rade")


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    try:
        prepare()
        run()
        print("Rezervne kopije: ceo ciklus je prošao.")
        return 0
    except AssertionError as error:
        print(f"PAD: {error}")
        return 1
    finally:
        if WORK.exists():
            compose("down", "-v", "--remove-orphans", check=False)
            shutil.rmtree(WORK, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
