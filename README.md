# MVCWiseGuardAlertsWatch

Complete ASP.NET Core 8 MVC conversion of the [WiseGuardAlertsWatch](https://github.com/shielddaluwala/WiseGuardAlertsWatch)
React/Base44 app. Every module of the original — map search, radar instances, WiseGuard
speech-to-text, dynamic data, settings, and authentication — has been converted to a
single .NET solution built on the `MVCSolutionBase` template.

## Modules converted

| Base44 module | .NET implementation |
|---|---|
| **Home dashboard** (rotating instance panels) | `HomeController` + `Views/Home/Index.cshtml`, 6-panel carousel (5 radar maps + WiseGuard), `wwwroot/js/dashboard.js` |
| **Map search** (`geocodeSearch` function) | `GeocodeService` (Nominatim, viewbox + haversine radius filter) + `/api/geocode/search` |
| **City/state geocode** (`geocodeCityState` function) | `GeocodeService.GeocodeCityStateAsync` + `/api/geocode/citystate` |
| **Radar instances** (RadarPanel: name multi-select, proxy select, loop, expanding radius, flash banner, pulses) | `dashboard.js` — per-instance Leaflet maps, expanding-radius multi-name threads, proxy-offset markers, priority color logic ("er"→red, "et"→yellow), flash banner, per-instance loop |
| **RadarConfig entity** (save/load per-instance selections) | `RadarConfig` model (instances JSON) + `RadarConfigController` CRUD |
| **SearchLocation / ProxyLocation entities** | EF Core models + Settings CRUD (locations with color/icon, proxies with lat/lon) |
| **AppSettings entity** (interval, radius step, max radius, custom location) | `AppSettings` model + Settings tab |
| **WiseGuard panel** (GuardPanel/WordConfigPanel/VideoPlayer) | `wwwroot/js/guard.js` — live Telugu speech recognition, 15s audio segments uploaded to `/api/guard/transcribe`, tracked-word cumulative counts persisted server-side, YouTube/video player with loop |
| **guardTranscribe function** | `TranscribeService` — configurable OpenAI Whisper-compatible endpoint (`appsettings.json: Transcription:*`) |
| **GuardVideo entity** | `GuardVideo` model + `GuardController` (add/select/delete, YouTube ID extraction) |
| **Dynamic data** (`dynamicDataModels` + `dynamicDataSearch` functions, OpenRouter/Ollama) | `DynamicDataService` + `DynamicDataController` (`/api/dynamicdata/models`, `/api/dynamicdata/search`), API keys stored AES-GCM encrypted (`CryptoService`) |
| **DynamicDataSettings entity** | `DynamicDataSettings` model + Settings tab (provider, keys, models) |
| **Auth** (Login/Register/ForgotPassword/ResetPassword pages, User entity) | Cookie authentication, PBKDF2 password hashing, reset-code flow (shown on screen — standalone app has no email provider) |
| **Settings page tabs** | `SettingsController` + `Views/Settings/Index.cshtml` (Locations, Proxy, Radar Configs, Guard, Dynamic Data, App Settings) |

## Project structure

```
MVCWiseGuardAlertsWatch/
├── MVCWiseGuardAlertsWatch.sln
└── MVCWiseGuardAlertsWatch/
    ├── Program.cs                  # DI, EF Core SQLite, cookie auth
    ├── AppDbContext.cs             # all 8 entity tables (auto-created on first run)
    ├── Models/                     # SearchLocation, ProxyLocation, AppSettings,
    │                               # RadarConfig, GuardVideo, GuardSettings,
    │                               # DynamicDataSettings, User, geocode DTOs
    ├── Services/                   # GeocodeService, DynamicDataService,
    │                               # TranscribeService, CryptoService (AES-GCM)
    ├── Controllers/                # Home, Settings, Account, Geocode (api),
    │                               # RadarConfig (api), Guard (api), DynamicData (api)
    ├── ViewModels/
    ├── Views/                      # Home, Settings, Account, Shared
    └── wwwroot/                    # css/site.css, js/dashboard.js, js/guard.js, js/settings.js
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Running

```bash
dotnet run
```

Open the URL shown in the console (default profile: `http://localhost:5193`). The SQLite
database (`wiseguard.db`) is auto-created and seeded on first run.

### GitHub Codespaces

The repo includes a `.devcontainer` (VS Code dev container with .NET 8). Open the repo
in a codespace, wait for the post-create restore, then:

```bash
dotnet run --launch-profile http   # or: ASPNETCORE_URLS=http://0.0.0.0:8080 dotnet run --no-launch-profile
```

Forwarded ports (5193 / 8080) open the app in the codespace's browser preview.

## Testing (E2E suite + recursive validation loop)

29 end-to-end test cases (catalog: `tests/TESTCASES.md`) covering auth, dashboard
structure, radar search (live Nominatim), radar configs, the WiseGuard panel,
settings/proxies, and JS-error health — verified for parity against
wiseguardalertswatch.base44.app.

Run the full recursive validation cycle (build -> restart server -> suite -> report):

```bash
python3 tests/e2e/run_validation.py            # --skip-build / --keep-server optional
```

- Report: `tests/e2e/report.json` (per-case pass/fail + console errors).
- Agent loop: on failures, fix the app/source for each failing case and re-run
  until `ALL TESTS PASSED` (that is how the suite itself was driven to green:
  23 -> 27 -> 28 -> 29 of 29).
- Live parity check L01 runs only when `E2E_LIVE_EMAIL` / `E2E_LIVE_PASSWORD`
  are set (the live app requires login; the check is read-only).
- Requires: .NET 8 SDK, Python 3 + `pip install playwright && playwright install chromium`.

## Database schema auto-upgrade

`EnsureCreated()` only creates a missing SQLite file — it never alters an
existing one, so databases created by older builds crashed on startup queries
with `SqliteException: no such column: p.DisplayName`. On boot,
`Services/DbSchemaUpgrader.Apply()` now compares the EF model against each
table's `PRAGMA table_info` and `ALTER TABLE ... ADD COLUMN`s anything missing
(NOT NULL columns get a type-appropriate DEFAULT). Verified by starting the
app against a stale fixture DB: the column is added and the dashboard renders.

## Dashboard (v2 — matches wiseguardalertswatch.base44.app)

- Light theme, 2x3 grid with all 6 instances visible (no rotating carousel).
- Global toolbar: config name input, saved-configs dropdown, Save, Start All, Stop All.
- Instances 1-5: free-text comma-separated search names (checkbox popover),
  Search/Stop toggle button, Loop checkbox, proxy-location dropdown with full
  geocoded display names ("No proxy (use search location)" default), Leaflet map
  with center pin + address pill, Idle/Running status with "N found · X.Xmi".
- Instance 6 (WiseGuard): "Speak in Telugu — see it as text", TAP TO START mic button,
  live transcript area, word tracker config popover, video player with maximize,
  Pause/Stop/Loop buttons and a progress slider, Repeat Counts chips with reset.
- Radar configs persist per-instance names/proxy/loop; proxy locations seed the five
  live-app samples (Tallada, Technology Park, Choppadandi, Agali, Venice).

### Configuration (appsettings.json)

| Key | Purpose |
|---|---|
| `ConnectionStrings:Default` | SQLite connection string |
| `Nominatim:UserAgent` | User-Agent for OpenStreetMap Nominatim |
| `Transcription:BaseUrl` / `ApiKey` / `Model` | OpenAI Whisper-compatible endpoint for the WiseGuard panel (leave empty to disable server transcription; browser speech recognition still works) |
| `Crypto:KeyFile` | AES key file for encrypting provider API keys |

### Provider keys

Dynamic data uses OpenRouter or Ollama. Add the API key in
**Settings → Dynamic Data** — it is stored AES-GCM encrypted, never in plaintext.

## Feature behavior notes

- **Search**: each instance runs one expanding-radius thread per selected name; radius
  grows by the configured step every interval until the max radius; with **Loop**
  enabled the sweep restarts from the base radius.
- **Proxy locations**: when selected, the map centers on the proxy and found results
  are offset relative to the proxy position.
- **Priority colors**: names containing "er" show red, "et" yellow, otherwise the
  name's configured color.
- **WiseGuard**: transcripts accumulate Telugu/Latin words only (model hallucinations
  on silence are filtered); tracked-word repeat counts persist server-side and reset
  per session start.
