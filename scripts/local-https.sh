#!/usr/bin/env bash
# Pravi lokalni sertifikat za pristup aplikaciji sa telefona u istoj mreži preko HTTPS-a.
#
# Zašto: instalacija na početni ekran, service worker i "ekran se ne gasi" rade samo u bezbednom
# kontekstu. Pregledač na računaru localhost smatra bezbednim, ali pregledač na telefonu adresu
# računara u mreži (http://192.168.x.x:4200) ne. Zato se pravi lokalni sertifikat koji telefon
# jednom prihvati.
#
# Šta radi: pravi sopstveni sertifikacioni autoritet (CA) i sertifikat servera za localhost, ime
# računara i njegove IPv4 adrese u mreži. Sve ide u .certs/ (u .gitignore: privatni ključevi ne
# ulaze u repozitorijum). CA se pravi jednom i čuva; sertifikat servera se pravi iznova svaki put,
# pa se pokreće i kad se promeni adresa računara.
#
# Korišćenje:
#   scripts/local-https.sh                 # adrese se pročitaju sa računara
#   scripts/local-https.sh 192.168.1.20    # i još jedna adresa ili ime (npr. kad se menja mreža)
#
# Sertifikat servera traje 397 dana: toliko Apple najviše dozvoljava za sertifikat servera.
# Bez openssl-a (dolazi uz Git za Windows) ne radi; ništa se ne preuzima.
set -euo pipefail

# Git Bash inače pretvara "/CN=..." u putanju na disku.
export MSYS_NO_PATHCONV=1

cd "$(dirname "$0")/.."

OUT=.certs
CA_KEY="$OUT/ca.key"
CA_CRT="$OUT/ca.crt"
SERVER_KEY="$OUT/server.key"
SERVER_CRT="$OUT/server.crt"
PHONE_CRT="$OUT/strength-planner-ca.crt"

if ! command -v openssl >/dev/null 2>&1; then
  echo "Nema openssl-a. Dolazi uz Git za Windows (Git Bash)." >&2
  exit 1
fi

mkdir -p "$OUT"

# --- adrese računara u mreži -------------------------------------------------------------
detect_ips() {
  if command -v ipconfig >/dev/null 2>&1; then
    ipconfig | tr -d '\r' | sed -n 's/.*IPv4[^:]*: *\([0-9][0-9.]*\).*/\1/p'
  elif command -v hostname >/dev/null 2>&1; then
    hostname -I 2>/dev/null | tr ' ' '\n'
  fi
}

SAN="DNS:localhost,IP:127.0.0.1"
HOST_NAME="$(hostname 2>/dev/null || true)"
if [ -n "$HOST_NAME" ]; then
  SAN="$SAN,DNS:$HOST_NAME"
fi

declare -A SEEN=()
for entry in $(detect_ips) "$@"; do
  entry="${entry//[[:space:]]/}"
  [ -z "$entry" ] && continue
  # 127.x je već u spisku, a 169.254.x.x je adresa koju računar sam sebi dodeli bez mreže.
  case "$entry" in 127.*|169.254.*) continue ;; esac
  [ -n "${SEEN[$entry]:-}" ] && continue
  SEEN[$entry]=1

  if [[ "$entry" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    SAN="$SAN,IP:$entry"
  else
    SAN="$SAN,DNS:$entry"
  fi
done

# --- sertifikacioni autoritet (jednom) ---------------------------------------------------
if [ ! -f "$CA_KEY" ] || [ ! -f "$CA_CRT" ]; then
  echo "Pravim lokalni sertifikacioni autoritet (jednom)..."
  openssl req -x509 -newkey rsa:3072 -nodes -sha256 -days 3650 \
    -keyout "$CA_KEY" -out "$CA_CRT" \
    -subj "/CN=StrengthPlanner lokalni CA" \
    -addext "basicConstraints=critical,CA:TRUE,pathlen:0" \
    -addext "keyUsage=critical,keyCertSign,cRLSign" 2>/dev/null
fi

# --- sertifikat servera (svaki put iznova) -----------------------------------------------
openssl req -new -newkey rsa:2048 -nodes \
  -keyout "$SERVER_KEY" -out "$OUT/server.csr" \
  -subj "/CN=StrengthPlanner razvoj" 2>/dev/null

cat > "$OUT/server.ext" <<EOF
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=$SAN
EOF

openssl x509 -req -in "$OUT/server.csr" -CA "$CA_CRT" -CAkey "$CA_KEY" -CAcreateserial \
  -out "$SERVER_CRT" -days 397 -sha256 -extfile "$OUT/server.ext" 2>/dev/null

rm -f "$OUT/server.csr" "$OUT/server.ext"
cp "$CA_CRT" "$PHONE_CRT"

openssl verify -CAfile "$CA_CRT" "$SERVER_CRT" >/dev/null

cat <<EOF

Gotovo. Sertifikat servera važi za:
  ${SAN//,/$'\n  '}

Sledeći koraci:
  1. Na računaru:   npm --prefix strength-planner-web run start:lan
                    (i API: dotnet run --project src/StrengthPlanner.API --launch-profile http)
  2. Na telefonu, JEDNOM: instaliraj $PHONE_CRT kao pouzdan sertifikat (sertifikacioni autoritet).
       Android: Podešavanja > Bezbednost > Šifrovanje i akreditivi > Instaliraj sertifikat > CA sertifikat
       iPhone:  pošalji fajl sebi, otvori ga, instaliraj profil, pa Podešavanja > Opšte > O uređaju >
                Podešavanje poverenja sertifikata > uključi "StrengthPlanner lokalni CA"
  3. Otvori na telefonu  https://<adresa računara>:4200

Privatni ključevi su u $OUT/ i ne smeju da se šalju ni da se commit-uju. Ko ima ca.key može da
izda sertifikat za bilo koji sajt telefonu koji veruje ovom CA-u: ne instaliraj ga na uređaj koji
nije tvoj, a kad ti telefon više ne treba pristup, ukloni CA sa njega.
EOF
