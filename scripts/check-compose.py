"""Proverava razrešenu compose konfiguraciju: šta je izloženo spolja, restart i rotaciju zapisa.

Pokretanje (iz korena repozitorijuma, traži Docker Compose 2.24 ili noviji, ne i pokrenut Docker):

    python scripts/check-compose.py

Zašto postoji. Ove osobine se ne vide ni u jednom testu aplikacije, a gube se jednom linijom:
objavljen port 8080 uz TLS (put mimo HTTPS-a), servis bez `restart` politike (ostaje ugašen posle
restarta servera) i zapisi bez gornje granice (pune disk). Čita se `docker compose config`, dakle
ono što compose zaista radi posle spajanja fajlova, a ne tekst fajlova.
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

# Vrednosti koje compose traži da bi razrešio fajlove. Ništa se ne pokreće.
ENVIRONMENT = {
    "POSTGRES_PASSWORD": "check",
    "APP_DB_PASSWORD": "check",
    "JWT_KEY": "check",
    "APP_DOMAIN": "example.test",
    "TLS_EMAIL": "check@example.test",
}

LOOPBACK = {"127.0.0.1", "::1"}


def resolve(files: list[str]) -> dict:
    # `--env-file` na prazan fajl: lokalni `.env` ne sme da promeni ishod provere.
    command = ["docker", "compose", "--env-file", os.devnull]
    for name in files:
        command += ["-f", name]
    command += ["config", "--format", "json"]
    result = subprocess.run(
        command, cwd=ROOT, env={**os.environ, **ENVIRONMENT}, capture_output=True, text=True, encoding="utf-8")
    if result.returncode != 0:
        sys.exit(f"`{' '.join(command)}` nije uspeo:\n{result.stderr}")
    return json.loads(result.stdout)


def check(config: dict, public_services: set[str]) -> list[str]:
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


def main() -> int:
    problems = [f"[bez TLS-a] {problem}" for problem in check(resolve(BASE), public_services=set())]

    with_tls = resolve(TLS)
    problems += [f"[uz TLS] {problem}" for problem in check(with_tls, public_services={"caddy"})]

    if with_tls["services"]["web"].get("ports"):
        problems.append("[uz TLS] web: ne sme da objavljuje nijedan port, spolja se dolazi samo kroz caddy")

    published = {str(port.get("published")) for port in with_tls["services"]["caddy"].get("ports") or []}
    if published != {"80", "443"}:
        problems.append(f"[uz TLS] caddy: očekivani portovi 80 i 443, a objavljeni su {sorted(published)}")

    for problem in problems:
        print(f"::error::{problem}" if os.environ.get("GITHUB_ACTIONS") else problem)
    print(f"Problema u compose konfiguraciji: {len(problems)}")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
