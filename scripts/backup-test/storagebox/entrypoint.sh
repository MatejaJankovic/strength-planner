#!/bin/sh
# Isto kao Storage Box: SSH na portu 23, prijava samo ključem.
set -eu

install -d -m 700 -o box -g box /home/box/.ssh
install -m 600 -o box -g box /keys/authorized_keys /home/box/.ssh/authorized_keys
install -m 600 /keys/host_key /etc/ssh/test_host_key
chown box:box /home/box

exec /usr/sbin/sshd -D -e -p 23 -h /etc/ssh/test_host_key -o PasswordAuthentication=no
