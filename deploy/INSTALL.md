# Deploying NexusUnleashed on NU-Linux (Ubuntu VPS)

The clean engine ships as a self-contained single binary — no .NET install on the
box, matching how the current realm deploys.

## 1. Build the bundle (on the dev machine)

```
deploy/publish.sh            # -> ./publish (binary + content + config + unit)
```

## 2. Copy to the VPS

```
rsync -a publish/ user@nu-linux:/opt/nexusunleashed/
```

## 3. Database

The engine reads accounts from `authdb` (`account` table: id, email, s, v,
gameToken, ...) and world data from `worlddb`. Point `realm.json` at the same
MariaDB the current realm uses (or a copy). Edit the connection strings — set the
real password, replacing `CHANGE_ME`.

## 4. Service

```
sudo useradd -r -s /usr/sbin/nologin nexus         # once
sudo cp /opt/nexusunleashed/nexusunleashed.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now nexusunleashed
sudo systemctl status nexusunleashed
```

## 5. Verify

The host logs the listeners it opened:

```
sts login server listening (...)
world server listening (...)
```

Point a client's realm-list host entry at the VPS. Login (STS + SRP) is
implemented; world entry lands as its protocol is pinned via the capture proxy.

## Ports

| port | service |
|---|---|
| 6600 | STS login (SRP handshake) |
| 23115 | auth |
| 24000 | world |

Open these in the VPS firewall as needed.
