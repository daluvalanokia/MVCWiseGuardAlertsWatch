# Test Case Catalog — MVCWiseGuardAlertsWatch

Reference behavior: the live Base44 app <https://wiseguardalertswatch.base44.app/>.
Runner: `python3 tests/e2e/run_validation.py` (build -> restart server -> suite -> report).
Report: `tests/e2e/report.json` (per-case pass/fail + JS console errors).

## Auth

| ID | Case | Expected |
|----|------|----------|
| T01 | Login page renders | Email, Password inputs + submit present |
| T02 | Valid login | Lands on dashboard |
| T03 | Anonymous `/` | Redirects to /Account/Login |
| T04 | Logout | Returns to login page |

## Dashboard structure

| ID | Case | Expected |
|----|------|----------|
| T05 | Grid render | 6 instance cards, 5 Leaflet maps |
| T06 | Toolbar | Config name input, saved-configs select, Save, Start All, Stop All |
| T07 | Instance controls (1-5) | names input `alert, park`, Search button, Loop checkbox |
| T08 | Proxy dropdown | "No proxy" + 5 seeded full display names (Tallada, Technology Park, Choppadandi, Agali, Venice) |
| T09 | Initial state | All Idle, pill = "Search location" |

## Radar search

| ID | Case | Expected |
|----|------|----------|
| T10 | Proxy select | Map recenters to proxy coords, pill shows full name |
| T11 | Search toggle | Running/Stop, then Idle on stop |
| T12 | Start/Stop All | All 5 Running, then all Idle |
| T13 | Geocode API | POST /api/geocode/search finds `park` near Technology Park |
| T14 | Found rendering | Result markers on map + `N found · X.Xmi` counter |

## Radar configs

| ID | Case | Expected |
|----|------|----------|
| T15 | Save config | Appears in dropdown |
| T16 | Persistence | Still listed after reload (server-side) |
| T17 | Load config | Applies names / proxy / loop to instances |
| T18 | Delete config | DELETE /api/radarconfig/{id} removes it |

## WiseGuard (instance 6)

| ID | Case | Expected |
|----|------|----------|
| T19 | State API | GET /api/guard/state returns trackedWords/cumulativeCounts/videos |
| T20 | State persist | POST state; chips render with counts after reload |
| T21 | Word tracker UI | Add word shows chip; remove deletes it |
| T22 | Video CRUD | Add YouTube video, select, delete via /api/guard/videos |
| T23 | Video controls | Pause/Stop/Loop, progress slider, maximize toggle |
| T24 | Mic + transcript | "TAP TO START", Telugu placeholder |

## Settings

| ID | Case | Expected |
|----|------|----------|
| T25 | Settings tabs | >= 3 tabs render |
| T26 | Proxy lifecycle | Geocode-add appears in settings table + dashboard select; delete removes |

## Health

| ID | Case | Expected |
|----|------|----------|
| T27 | Zero JS errors | No pageerror/console.error during the whole suite |

## Live parity (optional)

| ID | Case | Expected |
|----|------|----------|
| L01 | Live dashboard | 6 instances render on wiseguardalertswatch.base44.app |

L01 runs only when `E2E_LIVE_EMAIL` / `E2E_LIVE_PASSWORD` are set (the live app
requires login; the check is read-only).

## Recursive integration loop (agent workflow)

After any change to the app or source:

1. `python3 tests/e2e/run_validation.py` — build, restart, run all cases.
2. If failures: read `report.json`, fix the app/source for each failing case,
   rebuild, re-run. Repeat until every case passes (the loop).
3. Commit and push only when the suite is green.

The loop is intentionally driven by the agent: the orchestrator never edits
source by itself; it reports precise failures so the fix -> re-validate cycle
converges quickly.
