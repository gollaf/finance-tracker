# Operations runbook

Day-to-day commands for the public demo on the Oracle Cloud VM: getting in,
checking health, deploying, backups, and what to do when something breaks.
Why things are set up this way is in the ADRs linked from each section.

Placeholders: `<server>` is the VM's public IP, `<host>` its DuckDNS name
(`PUBLIC_HOST`), `<sha>` the first 7 characters of a master commit. Server
commands run in the deployment folder, `~/finance-tracker`, as `ubuntu`.

## What runs where

```
internet ── :80/:443 ── caddy (172.30.0.10) ── api:8080 ── postgres
                                                  │
                    worker ── rabbitmq ───────────┘
                    migrate (runs once per deploy, then exits)

Reachable only through an SSH tunnel:
  api                 127.0.0.1:5000
  RabbitMQ UI         127.0.0.1:15672

On the server, outside Docker:
  finance-tracker-backup.timer    nightly backup, 03:00 UTC
  backups/                        last 7 days of backups
Oracle Object Storage bucket      every backup, kept 30 days
```

| File in `~/finance-tracker` | Purpose | Changes by |
|---|---|---|
| `compose.yaml`, `Caddyfile` | the services and the proxy | hand, from the repository |
| `.env` | versions, passwords, host, upload link (secret, `chmod 600`) | hand; `IMAGE_TAG` by `deploy.sh` |
| `deploy.sh`, `backup.sh`, `restore.sh`, `restore-drill.sh` | operations | hand, from the repository |

## Getting in

```bash
ssh ubuntu@<server>
```

The API directly and the RabbitMQ UI, through a tunnel (from the laptop;
the window stays open while the tunnel is in use):

```bash
ssh -N -L 5050:127.0.0.1:5000 -L 15674:127.0.0.1:15672 ubuntu@<server>
```

Then `http://localhost:5050/scalar/v1` and `http://localhost:15674`
(user `financetracker`, password `RABBITMQ_PASSWORD` from `.env`).

## Is it healthy?

```bash
docker compose ps                                   # every service "running"; migrate "exited (0)"
curl -s http://127.0.0.1:5000/health/ready          # Healthy = database reachable and migrated
docker compose logs --tail 50 api                   # also: worker, caddy, migrate, postgres
docker stats --no-stream                            # memory and CPU per container
free -h; df -h /                                    # the VM's memory and disk
```

From anywhere: `https://<host>/health/live` answers `Healthy`.

The certificate's dates (Caddy renews it by itself, well before expiry):

```bash
echo | openssl s_client -connect <host>:443 -servername <host> 2>/dev/null | openssl x509 -noout -dates
```

## Deploying

Normal path ([ADR 0031](./adr/0031-deploying-from-github-actions-over-ssh.md)):
merge to `master` -> CI passes -> GitHub **Actions** -> **Deploy** -> the
waiting run -> **Review deployments** -> `production` -> **Approve and
deploy**. `deploy.sh` backs up, pulls, switches `IMAGE_TAG`, restarts
(migrations first) and waits up to 90 seconds for readiness; otherwise it
switches back.

| Task | How |
|---|---|
| Go back to an earlier version | Open that version's old Deploy run -> **Re-run jobs** |
| Deploy by hand | `bash deploy.sh sha-<sha>` on the server |
| Which version runs | `grep IMAGE_TAG .env` |
| Server files changed in a commit | copy them, then `docker compose up -d` (below) |

Server files are never changed by a deploy. When `compose.yaml`, the
`Caddyfile` or a script changes on `master`:

```bash
curl -fsSLO https://raw.githubusercontent.com/gollaf/finance-tracker/<sha>/deploy/server/<file>
docker compose up -d        # after compose.yaml or Caddyfile; check env.example for new .env values first
```

A changed network section in `compose.yaml` needs `docker compose down`
first. Never add `-v` to `down`: it deletes the database volume.

## Backups

[ADR 0030](./adr/0030-database-backups-to-object-storage.md). Every night,
and before every deploy.

```bash
bash backup.sh                                      # a backup now
bash restore-drill.sh                               # prove the newest one restores
ls -lh backups/                                     # local copies and sizes
journalctl -u finance-tracker-backup.service -n 30  # what the last runs printed
systemctl list-timers finance-tracker-backup.timer  # when the next one runs
```

Replace the live database with a backup (deletes the current data):

```bash
docker compose stop api worker
docker compose exec postgres dropdb --username=postgres financetracker
bash restore.sh backups/financetracker-<timestamp>.dump financetracker
docker compose start api worker
```

On a new server: download the file from the bucket in the Oracle console,
`scp` it into `backups/`, run `docker compose up -d postgres`, then the
`dropdb` and `restore.sh` lines above, then `docker compose up -d`.

## Things that expire or drift

| What | When | What to do |
|---|---|---|
| Backup upload link (`BACKUP_UPLOAD_URL`) | the expiry date set when it was created | Create a new write-only pre-authenticated request on the bucket, put it in `.env`, run `bash backup.sh` |
| HTTPS certificate | renewed by Caddy automatically | Only if renewal fails: see "Site not reachable" below |
| DuckDNS record | never, unless the VM's public IP changes | Update the IP on duckdns.org |
| Deploy SSH key | never; rotate if it may have leaked | See "Rotating the deploy key" |
| Oracle idle reclamation | Always Free VMs idle for 7 days can be reclaimed | Check CPU, network and memory in the instance's **Metrics** |

### Rotating the deploy key

1. On the laptop: `ssh-keygen -t ed25519 -C "github-actions-deploy" -f <new key file>`, empty passphrase.
2. On the server: in `~/.ssh/authorized_keys`, replace the key on the
   `restrict,command=...` line with the new public key; keep the prefix.
3. In GitHub: **Settings** -> **Environments** -> `production` -> update
   `DEPLOY_SSH_KEY` with the new private key.
4. Test with only that key:
   `ssh -o IdentitiesOnly=yes -i <new key file> ubuntu@<server> whoami`
   must answer `Expected an image tag ...`, not `ubuntu`.

## Firewalls

- **Oracle security list** (Networking -> the VCN -> public subnet's
  security list): ingress TCP 22, 80, 443 from `0.0.0.0/0`. Nothing else.
- **The VM's iptables**: leave as they are. Docker's published ports pass
  through Docker's own rules, ahead of Oracle's REJECT rule
  ([ADR 0029](./adr/0029-https-with-caddy-on-a-duckdns-name.md)). Never run
  `netfilter-persistent save`: it would store Docker's temporary rules.
- Ports published on `127.0.0.1` (the API, the RabbitMQ UI) are unreachable
  from outside whatever the firewalls say.

## When something breaks

| Symptom | Look at | Usual cause |
|---|---|---|
| Site not reachable, or certificate error | `docker compose ps`, `docker compose logs caddy` | Caddy down; DuckDNS IP wrong (`Resolve-DnsName <host>`); port 80/443 rule missing |
| `502` from the site | `docker compose logs api` | The API is starting or crashed; Caddy is fine |
| `429 Too Many Requests` | the `Retry-After` header | Rate limit ([ADR 0028](./adr/0028-rate-limiting-and-forwarded-headers.md)); wait |
| Deploy job failed | the job's log in GitHub | Read the last lines: backup failed, image not found, or not ready and rolled back |
| `Host key verification failed` in the Deploy job | `DEPLOY_KNOWN_HOSTS` | Must be `<server> ssh-ed25519 <key>`, the server's current host key |
| Nightly backup failed | `journalctl -u finance-tracker-backup.service` | Upload link expired (`curl: (22) ... 404/403`), or the database is down |
| Disk filling up | `docker system df` | Old images: `docker image prune --all` (unused only; a rollback pulls again) |
| Sign-ins stopped working after a restore | -- | Shouldn't happen: the Data Protection keys are in the database and restored with it |
