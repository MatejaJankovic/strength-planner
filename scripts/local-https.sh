#!/usr/bin/env bash
# Pravi lokalni sertifikat za pristup aplikaciji sa telefona u istoj mreži preko HTTPS-a.
#
# Pokreni ga u Git Bash-u (Git za Windows), ne iz PowerShell-a: tamo `bash` može da bude WSL, koji
# vidi drugu mrežnu adresu od one koju telefon vidi.
#
# Zašto: instalacija na početni ekran, service worker i "ekran se ne gasi" rade samo u bezbednom
# kontekstu. Pregledač na računaru localhost smatra bezbednim, ali pregledač na telefonu adresu
# računara u mreži (http://192.168.x.x:4200) ne. Zato se pravi lokalni sertifikat koji telefon
# jednom prihvati.
#
# Šta radi: pravi sopstveni sertifikacioni autoritet (CA) i sertifikat servera za localhost, ime
# računara (i ime.local) i njegove IPv4 adrese u mreži.
#   - CA ide u $STRENGTH_PLANNER_CA_DIR (podrazumevano ~/.strength-planner-ca), van repozitorijuma i
#     zajednički za sve kopije repozitorijuma (worktree): telefon se uči jednom, ne po kopiji.
#   - Sertifikat i ključ servera idu u .certs/ ove kopije (u .gitignore), odakle ih čita razvojni server.
# CA se pravi jednom i čuva; sertifikat servera se pravi iznova svaki put, pa se pokreće i kad se
# promeni adresa računara. Posle toga treba ponovo pokrenuti `npm run start:lan`: server čita
# sertifikat samo pri pokretanju.
#
# Korišćenje:
#   scripts/local-https.sh                 # adrese se pročitaju sa računara
#   scripts/local-https.sh 192.168.1.20    # i još jedna adresa ili ime (npr. kad se menja mreža)
#
# Bez openssl-a (dolazi uz Git za Windows) ne radi; ništa se ne preuzima.
set -euo pipefail

# Git Bash inače pretvara "/CN=..." u putanju na disku.
export MSYS_NO_PATHCONV=1

cd "$(dirname "$0")/.."

CA_DIR="${STRENGTH_PLANNER_CA_DIR:-$HOME/.strength-planner-ca}"
# openssl je izvorni Windows program i ne razume MSYS putanju (/c/Users/...): cygpath je prevodi u
# oblik C:/Users/... koji razumeju i on i bash.
if command -v cygpath >/dev/null 2>&1; then
  CA_DIR="$(cygpath -m "$CA_DIR")"
fi
OUT=.certs
CA_KEY="$CA_DIR/ca.key"
CA_CRT="$CA_DIR/ca.crt"
SERVER_KEY="$OUT/server.key"
SERVER_CRT="$OUT/server.crt"
PHONE_CRT="$OUT/strength-planner-ca.crt"

fail() {
  echo "GREŠKA: $*" >&2
  exit 1
}

command -v openssl >/dev/null 2>&1 || fail "nema openssl-a. Dolazi uz Git za Windows (Git Bash)."

# Ključevi samo za vlasnika, i na sistemima gde to ima smisla.
umask 077
mkdir -p "$OUT" "$CA_DIR"

TMP="$(mktemp -d "$OUT/tmp.XXXXXX")"
trap 'rm -rf "$TMP"' EXIT

# openssl ispisuje tačkice i plusiće dok pravi ključ; to je šum. Greška se ispisuje tek kad ne uspe.
quiet() {
  "$@" 2>"$TMP/stderr" || { cat "$TMP/stderr" >&2; return 1; }
}

# --- adrese računara u mreži -------------------------------------------------------------
# LC_ALL=C: Windows ispisuje ipconfig u kodnoj strani sistema, pa bi UTF-8 sed pao na slovima sa
# kvačicama pre "IPv4" (npr. "Direcci¢n IPv4") i vratio smeće umesto adrese.
detect_ips() {
  if command -v ipconfig >/dev/null 2>&1 && ipconfig 2>/dev/null | LC_ALL=C grep -q 'IPv4'; then
    ipconfig | LC_ALL=C tr -d '\r' | LC_ALL=C sed -n 's/.*IPv4[^:]*: *\([0-9][0-9.]*\).*/\1/p'
  elif command -v hostname >/dev/null 2>&1; then
    hostname -I 2>/dev/null | tr ' ' '\n' || true
  fi
}

# Tačno četiri okteta 0-255.
is_ipv4() {
  local ip="$1" octet
  [[ "$ip" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] || return 1
  IFS=. read -r -a octets <<<"$ip"
  for octet in "${octets[@]}"; do
    (( 10#$octet <= 255 )) || return 1
  done
}

SAN="DNS:localhost,IP:127.0.0.1"
HOST_NAME="$(hostname 2>/dev/null || true)"
HOST_NAME="${HOST_NAME//[[:space:]]/}"
if [[ "$HOST_NAME" =~ ^[A-Za-z0-9.-]+$ ]]; then
  # Telefon golo ime računara u Windows mreži obično ne razreši, ime.local (mDNS) razreši.
  SAN="$SAN,DNS:$HOST_NAME,DNS:$HOST_NAME.local"
fi

SEEN="|"
FOUND_LAN=0
add_entry() {
  local entry="$1" detected="$2"
  entry="${entry//[[:space:]]/}"
  [ -z "$entry" ] && return 0
  # 127.x je već u spisku, a 169.254.x.x je adresa koju računar sam sebi dodeli bez mreže.
  case "$entry" in 127.*|169.254.*) return 0 ;; esac
  case "$SEEN" in *"|$entry|"*) return 0 ;; esac
  SEEN="$SEEN$entry|"

  if is_ipv4 "$entry"; then
    SAN="$SAN,IP:$entry"
    [ "$detected" = yes ] && FOUND_LAN=1
  elif [[ "$entry" == *:* ]]; then
    echo "Preskačem IPv6 adresu '$entry': sertifikat nosi samo IPv4 adrese." >&2
  elif [[ "$entry" =~ ^[0-9.]+$ ]]; then
    # Izgleda kao adresa, a nije ispravna (npr. 192.168.1.300): greška u kucanju, ne ime računara.
    fail "'$entry' nije ispravna IPv4 adresa."
  elif [[ "$entry" =~ ^[A-Za-z0-9.-]+$ ]]; then
    SAN="$SAN,DNS:$entry"
  else
    fail "'$entry' nije ni IPv4 adresa ni ime računara."
  fi
}

while IFS= read -r detected_ip; do
  add_entry "$detected_ip" yes
done < <(detect_ips)
for argument in "$@"; do
  add_entry "$argument" no
  FOUND_LAN=1
done

if [ "$FOUND_LAN" -eq 0 ]; then
  echo "UPOZORENJE: nije nađena nijedna adresa računara u mreži. Sertifikat će važiti samo za" >&2
  echo "localhost, pa ga telefon neće prihvatiti. Zadaj adresu: scripts/local-https.sh 192.168.1.20" >&2
fi

# --- sertifikacioni autoritet (jednom) ---------------------------------------------------
if [ -f "$CA_KEY" ] && [ -f "$CA_CRT" ]; then
  :
elif [ -f "$CA_KEY" ] || [ -f "$CA_CRT" ]; then
  # Novi CA preko polovine starog bi tiho poništio poverenje na svakom telefonu koji ga ima.
  fail "u $CA_DIR postoji samo jedan od ca.key i ca.crt. Vrati onaj koji fali ili obriši oba da bi se napravio novi CA (svaki telefon ga tada mora iznova da instalira)."
else
  echo "Pravim lokalni sertifikacioni autoritet u $CA_DIR (jednom)..."
  quiet openssl req -x509 -newkey rsa:3072 -nodes -sha256 -days 3650 \
    -keyout "$CA_KEY" -out "$CA_CRT" \
    -subj "/CN=StrengthPlanner lokalni CA" \
    -addext "basicConstraints=critical,CA:TRUE,pathlen:0" \
    -addext "keyUsage=critical,keyCertSign,cRLSign" \
    || fail "openssl ne ume da napravi CA (verovatno zastarela verzija bez -addext). Greška je iznad."
fi

# --- sertifikat servera (svaki put iznova) -----------------------------------------------
# Sve ide u privremene fajlove i seli se na pravo mesto tek kad lanac prođe proveru: neuspeo
# korak ne sme da ostavi novi ključ uz stari sertifikat (Node bi tada pao sa "key values mismatch",
# daleko od uzroka).
quiet openssl req -new -newkey rsa:2048 -nodes \
  -keyout "$TMP/server.key" -out "$TMP/server.csr" \
  -subj "/CN=StrengthPlanner razvoj" || fail "openssl ne ume da napravi zahtev za sertifikat."

cat > "$TMP/server.ext" <<EOF
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=$SAN
EOF

# 397 dana: nije potrebno ni za jedan telefon (ograničenje važi za javne CA), ali je kratak rok
# razuman za ključ koji stoji nešifrovan na disku.
quiet openssl x509 -req -in "$TMP/server.csr" -CA "$CA_CRT" -CAkey "$CA_KEY" -CAcreateserial \
  -out "$TMP/server.crt" -days 397 -sha256 -extfile "$TMP/server.ext" \
  || fail "openssl ne ume da potpiše sertifikat. SAN je: $SAN"

openssl verify -CAfile "$CA_CRT" "$TMP/server.crt" >/dev/null || fail "sertifikat ne prolazi proveru lanca."

mv "$TMP/server.key" "$SERVER_KEY"
mv "$TMP/server.crt" "$SERVER_CRT"
chmod 600 "$SERVER_KEY" "$CA_KEY" 2>/dev/null || true
cp "$CA_CRT" "$PHONE_CRT"

cat <<EOF

Gotovo. Sertifikat servera važi za:
  ${SAN//,/$'\n  '}

Sledeći koraci:
  1. Na računaru (u dva terminala; ako server već radi, pokreni ga iznova, čita sertifikat samo pri pokretanju):
       dotnet run --project src/StrengthPlanner.API --launch-profile http
       npm --prefix strength-planner-web run start:lan
     Windows zaštitni zid će pitati da li node.exe sme da prima veze: dozvoli za privatnu mrežu.
  2. Na telefonu, JEDNOM: pošalji sebi $PHONE_CRT (javni fajl, može mejlom ili AirDrop-om) i instaliraj ga
     kao pouzdan sertifikat (sertifikacioni autoritet).
       Android: Podešavanja > Bezbednost > Šifrovanje i akreditivi > Instaliraj sertifikat > CA sertifikat
       iPhone:  otvori fajl, pa Podešavanja > Profil preuzet > Instaliraj; zatim Podešavanja > Opšte >
                O uređaju > Podešavanje poverenja sertifikata > uključi "StrengthPlanner lokalni CA"
  3. Otvori na telefonu  https://<adresa računara>:4200  (telefon i računar moraju da budu u istoj mreži;
     neke Wi-Fi mreže izoluju uređaje jedan od drugog).

CA je u $CA_DIR. Ko ima ca.key može da izda sertifikat za bilo koji sajt telefonu koji veruje ovom CA-u:
ključ ne napušta računar, ne šalje se i ne commit-uje, a CA se sa telefona uklanja kad pristup više
nije potreban.
EOF
