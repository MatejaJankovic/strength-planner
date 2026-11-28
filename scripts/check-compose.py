"""Proverava razrešenu compose konfiguraciju: šta je izloženo spolja, restart, rotaciju zapisa i
postavke koje TLS varijanta mora da ima.

Pokretanje (iz korena repozitorijuma, traži Docker Compose 2.24 ili noviji, ne i pokrenut Docker):

    python scripts/check-compose.py

Zašto postoji. Ove osobine se ne vide ni u jednom testu aplikacije, a gube se jednom linijom:
objavljen port 8080 uz TLS (put mimo HTTPS-a), servis bez `restart` politike (ostaje ugašen posle
restarta servera), zapisi bez gornje granice (pune disk) ili API koji uz TLS ne zna da ima dva
proxy-ja ispred sebe. Čita se `docker compose config`, dakle ono što compose zaista radi posle
spajanja fajlova, a ne tekst fajlova.
"""
from __future__ import annotations

import json
import os
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
BASE = ["docker-compose.yml"]
TLS = ["docker-compose.yml", "docker-compose.tls.yml"]
# Ovako stoji na serveru.
SERVER = TLS + ["docker-compose.backup.yml"]

# Vrednosti koje compose traži da bi razrešio fajlove. Ništa se ne pokreće.
ENVIRONMENT = {
    "POSTGRES_PASSWORD": "check",
    "APP_DB_PASSWORD": "check",
    "JWT_KEY": "check",
    "APP_DOMAIN": "example.test",
    "TLS_EMAIL": "check@example.test",
    "RESTIC_REPOSITORY": "sftp:check:check",
    "RESTIC_PASSWORD": "check",
    "PRIVACY_CONTROLLER_NAME": "Check Person",
    "PRIVACY_CONTACT_EMAIL": "privacy@example.test",
}

# Bez ovih javna (TLS) varijanta ne sme ni da se razreši: aplikacija bi prikupljala lične podatke, a
# stranica o privatnosti ne bi govorila ko za njih odgovara.
PRIVACY_VARIABLES = ("PRIVACY_CONTROLLER_NAME", "PRIVACY_CONTACT_EMAIL")

LOOPBACK = {"127.0.0.1", "::1"}


def run_config(files: list[str], environment: dict) -> tuple[list[str], subprocess.CompletedProcess]:
    # `--env-file` na prazan fajl: lokalni `.env` ne sme da promeni ishod provere.
    # `--profile *`: servis iza profila se inače izostavlja iz ispisa, a može da se pokrene.
    command = ["docker", "compose", "--env-file", os.devnull, "--profile", "*"]
    for name in files:
        command += ["-f", name]
    command += ["config", "--format", "json"]
    result = subprocess.run(
        command, cwd=ROOT, env=environment, capture_output=True, text=True, encoding="utf-8")
    return command, result


def check_required(files: list[str], variables: tuple[str, ...]) -> list[str]:
    """Svaka od zadatih promenljivih mora da bude obavezna: prazna ne sme da prođe."""
    problems: list[str] = []
    for variable in variables:
        environment = {**os.environ, **ENVIRONMENT, variable: ""}
        _, result = run_config(files, environment)
        if result.returncode == 0:
            problems.append(f"{variable}: sme da ostane prazno, a mora biti obavezno")
        elif variable not in result.stderr:
            problems.append(f"{variable}: razrešavanje je palo iz drugog razloga:\n{result.stderr}")
    return problems


def resolve(files: list[str]) -> dict:
    command, result = run_config(files, {**os.environ, **ENVIRONMENT})
    if result.returncode != 0:
        sys.exit(f"`{' '.join(command)}` nije uspeo:\n{result.stderr}")
    return json.loads(result.stdout)


def check(config: dict, public_services: set[str]) -> list[str]:
    """Pravila koja važe za svaki servis."""
    problems: list[str] = []
    for name, service in config["services"].items():
        if service.get("restart") != "unless-stopped":
            problems.append(f"{name}: nema `restart: unless-stopped`")

        logging = service.get("logging") or {}
        options = logging.get("options") or {}
        if logging.get("driver") != "json-file" or not options.get("max-size") or not options.get("max-file"):
            problems.append(f"{name}: zapisi se ne rotiraju (json-file sa max-size i max-file)")

        if service.get("network_mode") == "host":
            problems.append(f"{name}: `network_mode: host` izlaže sve portove")

        for port in service.get("ports") or []:
            if name not in public_services and port.get("host_ip") not in LOOPBACK:
                problems.append(
                    f"{name}: port {port.get('published')} je objavljen spolja "
                    f"(adresa: {port.get('host_ip') or 'sve'})")
    return problems


def check_base(config: dict) -> list[str]:
    problems = check(config, public_services=set())

    environment = config["services"]["api"].get("environment") or {}
    if str(environment.get("Security__RequireHttps", "")).lower() == "true":
        problems.append("api: HTTPS se ne sme tražiti bez proxy-ja koji ga pruža (petlja preusmeravanja)")

    return problems


def check_tls(config: dict) -> list[str]:
    problems = check(config, public_services={"caddy"})
    services = config["services"]

    if services["web"].get("ports"):
        problems.append("web: ne sme da objavljuje nijedan port, spolja se dolazi samo kroz caddy")

    caddy = services.get("caddy") or {}
    ports = sorted(
        (str(port.get("published")), str(port.get("target")), port.get("host_ip") or "")
        for port in caddy.get("ports") or [])
    if ports != [("443", "443", ""), ("80", "80", "")]:
        problems.append(f"caddy: očekivani su 80 i 443 na svim adresama, a objavljeno je {ports}")

    if not any(volume.get("target") == "/data" and volume.get("type") == "volume"
               for volume in caddy.get("volumes") or []):
        problems.append("caddy: /data mora biti trajan volumen, inače se sertifikat traži pri svakom pravljenju")

    environment = services["api"].get("environment") or {}
    for key, expected in (
            ("Security__RequireHttps", "true"),
            ("Security__ProxyCount", "2"),
            ("Privacy__ControllerName", ENVIRONMENT["PRIVACY_CONTROLLER_NAME"]),
            ("Privacy__ContactEmail", ENVIRONMENT["PRIVACY_CONTACT_EMAIL"])):
        if str(environment.get(key)) != expected:
            problems.append(f"api: {key} mora biti {expected}, a jeste {environment.get(key)}")

    return problems


def check_server(config: dict) -> list[str]:
    problems = check_tls(config)

    backup = config["services"].get("backup") or {}
    if not (backup.get("healthcheck") or {}).get("test"):
        problems.append("backup: bez healthcheck-a se neuspele kopije ne vide")
    if backup.get("ports"):
        problems.append("backup: ne sme da objavljuje nijedan port")

    return problems


def main() -> int:
    # Poruke imaju slova van Windows kodne strane; preusmeren ispis bi inače pukao na njima.
    sys.stdout.reconfigure(encoding="utf-8")

    problems = [f"[bez TLS-a] {problem}" for problem in check_base(resolve(BASE))]
    problems += [f"[uz TLS] {problem}" for problem in check_tls(resolve(TLS))]
    problems += [f"[uz TLS i kopije] {problem}" for problem in check_server(resolve(SERVER))]
    problems += [f"[uz TLS] {problem}" for problem in check_required(TLS, PRIVACY_VARIABLES)]

    for problem in problems:
        print(f"::error::{problem}" if os.environ.get("GITHUB_ACTIONS") else problem)
    print(f"Problema u compose konfiguraciji: {len(problems)}")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
