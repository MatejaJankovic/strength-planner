#!/bin/bash
# Zdrav samo ako je poslednja uspešna kopija mlađa od 26 sati i poslednja proba vraćanja nije pala.
[ ! -f /state/verify-failed ] && [ -n "$(find /state/last-success -mmin -1560 2>/dev/null)" ]
