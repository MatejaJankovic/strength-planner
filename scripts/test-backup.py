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

import json
import os
import pathlib
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


def snapshots() -> list[dict]:
    output = tool("restic", "snapshots", "--json", "--host", "strengthplanner", "--tag", "db").stdout
    return json.loads(output[output.index("["):])


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
        "API_PORT": str(API_PORT),
    }
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
                         "  IdentityFile ~/.ssh/id_ed25519\n",
        "client/known_hosts": f"[storagebox]:23 {host_public}\n",
        "server/authorized_keys": client_public + "\n",
        "server/host_key": host_private,
    }
    for name, content in files.items():
        (WORK / name).write_bytes(content.replace("\r\n", "\n").encode("utf-8"))


def run() -> None:
    compose("up", "-d", "db", "api", "storagebox")
    wait_for_api()

    assert "nema šta da se kopira" in tool("backup.sh").stdout
    assert snapshots() == []
    passed("prazna baza na novoj instalaciji se ne kopira")

    anna = register("anna")
    status, _ = call("POST", "/macrocycles", {
        "name": "Plan pre kopije", "startDate": "2026-01-05T00:00:00Z",
        "blocks": [{"goal": 1, "templateKey": "full-body", "periodizationModel": 0}]}, anna["token"])
    assert status == 201, f"plan: {status}"
    tool("backup.sh")
    assert len(snapshots()) == 1
    passed("kopija stiže u skladište preko SFTP-a na portu 23")

    for name, environment, expected in (
            ("pg_dump koji padne", {"APP_DB_PASSWORD": "pogresna"}, "password authentication failed"),
            ("pogrešna lozinka skladišta", {"RESTIC_PASSWORD": "pogresna"}, "restic je izašao sa 12"),
            ("nedostupno skladište", {"RESTIC_REPOSITORY": "sftp:box@ne-postoji:kopije"}, "skladište nije dostupno")):
        result = tool("backup.sh", check=False, environment=environment)
        output = result.stdout + result.stderr
        assert result.returncode != 0, f"{name}: skripta je uspela"
        assert expected in output, f"{name}: nema poruke „{expected}“\n{output[-2000:]}"
        assert len(snapshots()) == 1, f"{name}: nastala je nova kopija"
        passed(f"{name}: greška, bez nove kopije")

    # Po jedna stara kopija za 57 dana, pa pravilo čuvanja.
    tool("bash", "-c",
         "for day in $(seq 4 60); do restic backup --quiet --host strengthplanner --tag db "
         "--time \"$(date -u -d \"$day days ago\" '+%Y-%m-%d 02:00:00')\" "
         "--stdin-filename strengthplanner.dump --stdin-from-command -- echo stara; done")
    assert len(snapshots()) == 58
    tool("backup.sh")
    kept = sorted(snapshot["time"][:10] for snapshot in snapshots())
    age = lambda day: (time.time() - time.mktime(time.strptime(day, "%Y-%m-%d"))) / 86400
    assert 17 <= len(kept) <= 30, f"ostalo je {len(kept)} kopija: {kept}"
    assert age(kept[0]) > 45, f"najstarija zadržana je od {kept[0]}"
    assert all(any(abs(age(day) - wanted) < 1.5 for day in kept) for wanted in range(4, 14)), kept
    passed(f"pravilo čuvanja: od 59 kopija ostalo {len(kept)}, najstarija {kept[0]}")

    compose("up", "-d", "backup")
    wait_until("proba vraćanja pri pokretanju", lambda: "Proba vraćanja uspela: nalozi 1" in logs("backup"))
    assert healthy()
    assert sql("postgres", "SELECT count(*) FROM pg_database WHERE datname LIKE '%_verify'") == "0"
    passed("servis pri pokretanju pravi kopiju, probno je vraća i briše privremenu bazu")

    compose("exec", "-T", "backup", "touch", "-d", "30 hours ago", "/state/last-success")
    assert not healthy()
    passed("bez uspešne kopije duže od 26 sati kontejner nije zdrav")

    compose("stop", "backup")
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

    refused = tool("restore.sh", "latest", check=False)
    assert refused.returncode != 0 and "na bazu je neko povezan" in refused.stdout + refused.stderr
    assert sql("strengthplanner", 'SELECT count(*) FROM "AspNetUsers"') == "0"
    passed("vraćanje se odbija dok API radi")

    compose("stop", "api", "backup")
    missing = tool("restore.sh", check=False)
    assert missing.returncode != 0 and "navedi koju kopiju" in missing.stdout + missing.stderr
    output = tool("restore.sh", "latest").stdout
    assert "Vraćeno: nalozi 1" in output, output
    compose("up", "-d", "api")
    wait_for_api()

    status, account = call("POST", "/auth/login", {"email": "anna@example.test", "password": PASSWORD})
    assert status == 200, f"prijava posle vraćanja: {status}"
    status, plan = call("GET", "/macrocycles/active", token=account["token"])
    assert status == 200 and plan["name"] == "Plan pre kopije", (status, plan)
    assert login("bojan") == 401
    assert call("POST", "/auth/refresh", {"refreshToken": anna["refreshToken"]})[0] == 401
    passed("posle vraćanja postoje nalog i plan iz kopije, kasniji nalog ne, stara prijava ne važi")

    # Aplikacijski nalog i dalje sme da upisuje, i migracije prolaze nad vraćenom bazom.
    register("ceca")
    api_log = logs("api")
    assert "42501" not in api_log and "permission denied" not in api_log, api_log[-3000:]
    owners = sql("strengthplanner",
                 "SELECT string_agg(DISTINCT tableowner, ',') FROM pg_tables WHERE schemaname = 'public'")
    assert owners == "strengthplanner_app", owners
    assert sql("postgres", "SELECT count(*) FROM pg_database WHERE datname LIKE '%_before_restore_%'") == "1"
    passed("aplikacija piše u vraćenu bazu, vlasnik tabela je isti, prethodna baza je sačuvana")

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
