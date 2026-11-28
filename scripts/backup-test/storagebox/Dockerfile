# Probni SSH server koji u testu rezervnih kopija glumi Storage Box. Ne isporučuje se.
FROM postgres:16

RUN apt-get update \
    && apt-get install -y --no-install-recommends openssh-server \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --shell /bin/sh box \
    && usermod -p '*' box \
    && mkdir -p /run/sshd

COPY entrypoint.sh /entrypoint.sh
ENTRYPOINT ["/bin/sh", "/entrypoint.sh"]
