#!/usr/bin/env bash
# reset_database.sh — clear the WiseGuardAlertsWatch database.
#
# The app recreates the schema and re-seeds defaults on the next start:
#   - admin / admin@123 (default admin account)
#   - 5 default proxy locations, default radar config, guard state
#
# Usage:  ./scripts/reset_database.sh            (back up + delete)
#         ./scripts/reset_database.sh --force    (delete without backup)
set -e
cd "$(dirname "$0")/.."
WEB=MVCWiseGuardAlertsWatch
DB="$WEB/wiseguard.db"

if [ ! -f "$DB" ]; then
    echo "No database found at $DB — nothing to clear."
    exit 0
fi

# Stop any running instance so SQLite files can be removed safely.
if [ -x /proc ] && grep -q 0A /proc/net/tcp* 2>/dev/null; then
    python3 - <<'PY' || true
import glob, os, signal
targets = set()
for f in ('/proc/net/tcp', '/proc/net/tcp6'):
    try:
        for line in open(f).read().splitlines()[1:]:
            fld = line.split()
            if fld[3] == '0A' and int(fld[1].split(':')[1], 16) == 5193:
                targets.add(fld[9])
    except OSError:
        pass
for d in glob.glob('/proc/[0-9]*'):
    try:
        cmd = open(d + '/cmdline', 'rb').read().decode(errors='ignore')
        if 'dotnet' in cmd and 'WiseGuard' in cmd:
            os.kill(int(d.split('/')[-1]), signal.SIGKILL)
            print('stopped', d.split('/')[-1])
    except (OSError, ValueError):
        pass
PY
fi

if [ "$1" != "--force" ] && command -v cp >/dev/null; then
    BAK="$DB.bak-$(date +%Y%m%d-%H%M%S)"
    cp "$DB" "$BAK" 2>/dev/null && echo "backup: $BAK"
fi

rm -f "$DB" "$DB-wal" "$DB-shm"
echo "cleared: $DB"
echo ""
echo "Next start re-seeds defaults. Then log in with:"
echo "    admin / admin@123"
echo "Start with:"
echo "    cd $WEB && dotnet run"
