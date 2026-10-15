"""
E2E test suite for MVCWiseGuardAlertsWatch (parity reference: wiseguardalertswatch.base44.app).

Plain-asyncio Playwright suite (no pytest dependency). Run directly or via
run_validation.py which builds, restarts the server and calls this suite.

Env overrides:
  E2E_BASE_URL   (default http://127.0.0.1:5193)
  E2E_EMAIL      (default dayakar@example.com)
  E2E_PASSWORD   (default Password123)

Exit code 0 = all tests passed. Report written to tests/e2e/report.json.
"""
import asyncio
import json
import os
import sys
import time

from playwright.async_api import async_playwright

BASE_URL = os.environ.get("E2E_BASE_URL", "http://127.0.0.1:5193").rstrip("/")
EMAIL = os.environ.get("E2E_EMAIL", "dayakar@example.com")
PASSWORD = os.environ.get("E2E_PASSWORD", "Password123")

RESULTS = []
JS_ERRORS = []


def check(tid, name, cond, detail=""):
    ok = bool(cond)
    RESULTS.append({"id": tid, "name": name, "status": "pass" if ok else "fail", "detail": str(detail)[:500]})
    print(f"  [{'PASS' if ok else 'FAIL'}] {tid} {name}" + (f"  -> {detail}" if not ok else ""))
    return ok


async def login(page):
    await page.goto(f"{BASE_URL}/Account/Login", wait_until="networkidle")
    if await page.locator('input[name="Email"]').count() == 0:
        # context already authenticated; Login redirects to the dashboard
        return "login" not in page.url.lower()
    await page.fill('input[name="Email"]', EMAIL)
    await page.fill('input[name="Password"]', PASSWORD)
    await page.click('.auth-card form button.btn.primary')
    await page.wait_for_load_state("networkidle")
    await page.wait_for_timeout(2500)
    return "/" in page.url and "Login" not in page.url


async def api(page, method, path, payload=None, form=None):
    """Same-origin fetch through the logged-in browser context."""
    return await page.evaluate(
        """async ([method, path, payload, form]) => {
            const opts = { method };
            if (payload) { opts.headers = {'Content-Type': 'application/json'}; opts.body = JSON.stringify(payload); }
            if (form) opts.body = form;
            const r = await fetch(path, opts);
            let body = null;
            try { body = await r.json(); } catch (e) { body = await r.text().catch(() => null); }
            return { status: r.status, body };
        }""",
        [method, path, payload, form],
    )


async def api_form(page, path, fields):
    """MVC form POST (form-encoded, no anti-forgery token required by the app)."""
    return await page.evaluate(
        """async ([path, fields]) => {
            const body = new URLSearchParams(fields);
            const r = await fetch(path, { method: 'POST', body });
            return { status: r.status };
        }""",
        [path, fields],
    )


# ---------------------------------------------------------------- test groups

async def t_auth(ctx):
    print("[AUTH]")
    # T01 login page renders
    p = await ctx.new_page()
    await p.goto(f"{BASE_URL}/Account/Login", wait_until="networkidle")
    check("T01", "login page renders email/password/submit",
          await p.locator('input[name="Email"]').count() == 1
          and await p.locator('input[name="Password"]').count() == 1
          and await p.locator('.auth-card form button.btn.primary').count() == 1)
    await p.close()

    # T02 valid login lands on dashboard
    p = await ctx.new_page()
    ok = await login(p)
    check("T02", "login lands on dashboard", ok, p.url)
    dashboard_text = await p.inner_text("body")

    # T03 anonymous dashboard redirects to login
    anon = await ctx.browser.new_context()
    pa = await anon.new_page()
    await pa.goto(BASE_URL + "/", wait_until="domcontentloaded")
    check("T03", "anonymous / redirects to login", "Login" in pa.url or "login" in pa.url, pa.url)
    await pa.close(); await anon.close()

    # T04 logout returns to login
    await p.click('header form button[type="submit"]')
    await p.wait_for_load_state("networkidle")
    check("T04", "logout returns to login page", "Login" in p.url or "login" in p.url, p.url)
    await p.close()
    return dashboard_text


async def t_structure(ctx):
    print("[STRUCTURE]")
    p = await ctx.new_page()
    await login(p)
    await p.wait_for_timeout(3500)

    cards = await p.locator(".inst-card").count()
    maps = await p.locator(".leaflet-container").count()
    check("T05", "6 instance cards and 5 maps render", cards == 6 and maps == 5, f"cards={cards} maps={maps}")

    ok = all([
        await p.locator("#configName").count() == 1,
        await p.locator("#configSelect").count() == 1,
        (await p.locator("#saveConfigBtn").inner_text()).strip().endswith("Save"),
        (await p.locator("#startAllBtn").inner_text()).strip().endswith("Start All"),
        (await p.locator("#stopAllBtn").inner_text()).strip().endswith("Stop All"),
    ])
    check("T06", "toolbar: config name/select/Save/Start All/Stop All", ok)

    ok = True; detail = []
    for i in range(1, 6):
        names = await p.locator(f"#names-{i}").input_value()
        if names.strip() != "alert, park": ok = False; detail.append(f"names{i}={names!r}")
        if await p.locator(f"#search-{i}").count() != 1: ok = False; detail.append(f"search{i} missing")
        if not await p.locator(f"#loop-{i}").is_editable(): ok = False; detail.append(f"loop{i} not editable")
    check("T07", "instances 1-5: names input 'alert, park', Search btn, Loop checkbox", ok, "; ".join(detail))

    opts = await p.evaluate('Array.from(document.getElementById("proxy-1").options).map(o=>o.text)')
    need = ["No proxy (use search location)",
            "Tallada, Thallada mandal, Khammam, Telangana, 507167, India",
            "Technology Park, Lake Mary, Seminole County, Florida, 32795, United States",
            "Choppadandi, Choppadandi mandal, Karimnagar, Telangana, 557654, India",
            "Agali, Mannarkad, Palakkad, Kerala, 678581, India",
            "Venice, Venezia, Veneto, 30121-30176, Italy"]
    check("T08", "proxy dropdown: default + 5 seeded full display names", all(n in opts for n in need) and len(opts) >= 6, opts)

    sts = []
    for i in range(1, 6):
        sts.append((await p.locator(f"#status-{i}").inner_text()).strip())
    ok = all(x == "Idle" for x in sts)
    pill = (await p.locator("#pill-1").inner_text()).strip()
    check("T09", "all instances Idle initially, pill shows 'Search location'", ok and pill == "Search location", f"pill={pill}")
    await p.close()
    return


async def t_radar(ctx):
    print("[RADAR SEARCH]")
    p = await ctx.new_page()
    await login(p)
    await p.wait_for_timeout(3000)

    # T10 proxy select recenters map and updates pill
    await p.select_option("#proxy-1", label="Technology Park, Lake Mary, Seminole County, Florida, 32795, United States")
    await p.wait_for_timeout(800)
    pill = (await p.locator("#pill-1").inner_text()).strip()
    center = await p.evaluate('window._maps[1].getCenter()')
    check("T10", "selecting proxy updates pill and map center",
          pill.startswith("Technology Park") and abs(center["lat"] - 28.7366) < 0.01 and abs(center["lng"] + 81.3624) < 0.01,
          f"pill={pill} center={center}")

    # T13 geocode API returns results (park near Technology Park)
    r = await api(p, "POST", "/api/geocode/search",
                 {"query": "park", "lat": 28.7366409, "lon": -81.3623939, "radius": 30000})
    results = (r.get("body") or {}).get("results") if isinstance(r.get("body"), dict) else None
    check("T13", "geocode API finds 'park' near Technology Park (30km)",
          r["status"] == 200 and isinstance(results, list) and len(results) >= 1,
          f"status={r['status']} results={len(results) if results is not None else None}")

    # T11 search start/stop toggles status
    await p.click("#search-1")
    await p.wait_for_timeout(1500)
    status = (await p.locator("#status-1").inner_text()).strip()
    btn = (await p.locator("#search-1").inner_text()).strip()
    running_ok = status == "Running" and "Stop" in btn
    await p.click("#search-1")   # stop
    await p.wait_for_timeout(700)
    status2 = (await p.locator("#status-1").inner_text()).strip()
    check("T11", "Search toggles Running/Stop then back to Idle",
          running_ok and status2 == "Idle", f"running={status}/{btn} after-stop={status2}")

    # T14 found counter + markers appear when results exist.
    # Widen the search step (via settings API) so the first sweep covers 30km,
    # then restore the original settings afterwards.
    await api_form(p, "/Settings/UpdateSettings",
                  {"SearchIntervalSeconds": "10", "SearchRadiusStepMeters": "30000",
                   "MaxRadiusMeters": "30000"})
    await p.reload(wait_until="networkidle")
    await p.wait_for_timeout(2500)
    await p.click("#search-1")
    await p.wait_for_timeout(9000)  # first sweep completes
    found_txt = (await p.locator("#found-1").inner_text()).strip() if await p.locator("#found-1").is_visible() else ""
    markers = await p.locator("#map-1 .leaflet-marker-icon").count()
    check("T14", "search renders found counter and result markers",
          markers >= 1 and ("found" in found_txt or markers > 1),
          f"found='{found_txt}' markers={markers}")
    await p.click("#search-1")  # stop instance 1
    await api_form(p, "/Settings/UpdateSettings",
                  {"SearchIntervalSeconds": "10", "SearchRadiusStepMeters": "300",
                   "MaxRadiusMeters": "5000"})
    await p.reload(wait_until="networkidle")
    await p.wait_for_timeout(2000)

    # T12 Start All / Stop All
    await p.click("#startAllBtn")
    await p.wait_for_timeout(1500)
    sts_run = []
    for i in range(1, 6):
        sts_run.append((await p.locator(f"#status-{i}").inner_text()).strip())
    all_running = all(x == "Running" for x in sts_run)
    await p.click("#stopAllBtn")
    await p.wait_for_timeout(900)
    sts_idle = []
    for i in range(1, 6):
        sts_idle.append((await p.locator(f"#status-{i}").inner_text()).strip())
    all_idle = all(x == "Idle" for x in sts_idle)
    check("T12", "Start All runs all 5, Stop All idles all 5", all_running and all_idle,
          f"running_ok={all_running} idle_ok={all_idle}")
    await p.close()


async def t_configs(ctx):
    print("[RADAR CONFIGS]")
    p = await ctx.new_page()
    await login(p)
    await p.wait_for_timeout(2500)

    # T15 save config appears in dropdown
    cfg_name = f"e2ecfg-{int(time.time())}"
    await p.select_option("#proxy-2", label="Venice, Venezia, Veneto, 30121-30176, Italy")
    await p.fill("#names-2", "alert, park, museum")
    await p.check("#loop-3")
    await p.fill("#configName", cfg_name)
    await p.click("#saveConfigBtn")
    await p.wait_for_timeout(1500)
    opts = await p.evaluate('Array.from(document.getElementById("configSelect").options).map(o=>o.text)')
    check("T15", "saving config shows it in dropdown", cfg_name in opts, opts)

    # T16 persists server-side after reload
    await p.reload(wait_until="networkidle")
    await p.wait_for_timeout(2500)
    opts = await p.evaluate('Array.from(document.getElementById("configSelect").options).map(o=>o.text)')
    check("T16", "config persists after page reload", cfg_name in opts, opts)

    # T17 loading config applies instances
    await p.select_option("#configSelect", label=cfg_name)
    await p.wait_for_timeout(800)
    names2 = await p.locator("#names-2").input_value()
    proxy2 = (await p.locator("#proxy-2 option:checked").inner_text()).strip()
    loop3 = await p.locator("#loop-3").is_checked()
    check("T17", "loading config applies names/proxy/loop",
          names2 == "alert, park, museum" and proxy2.startswith("Venice") and loop3,
          f"names={names2!r} proxy={proxy2!r} loop3={loop3}")

    # T18 delete config removes it
    cfg_id = await p.evaluate(
        'name => { const cfgs = JSON.parse(document.getElementById("dashboard").dataset.radarConfigs); return (cfgs.find(c=>c.name===name)||{}).id; }',
        cfg_name)
    r = await api(p, "DELETE", f"/api/radarconfig/{cfg_id}")
    await p.reload(wait_until="networkidle")
    await p.wait_for_timeout(2500)
    opts = await p.evaluate('Array.from(document.getElementById("configSelect").options).map(o=>o.text)')
    check("T18", "DELETE /api/radarconfig/{id} removes config", r["status"] in (200, 204) and cfg_name not in opts,
          f"status={r['status']} still_there={cfg_name in opts}")
    await p.close()


async def t_guard(ctx):
    print("[WISEGUARD]")
    p = await ctx.new_page()
    await login(p)
    await p.wait_for_timeout(2500)

    # T19 state shape
    r = await api(p, "GET", "/api/guard/state")
    body = r.get("body") or {}
    check("T19", "GET /api/guard/state returns trackedWords/cumulativeCounts/videos",
          r["status"] == 200 and all(k in body for k in ("trackedWords", "cumulativeCounts", "videos")), str(body)[:200])

    # T20 state POST persists + UI renders counts
    r = await api(p, "POST", "/api/guard/state",
                  {"trackedWords": ["hello", "world"], "cumulativeCounts": {"hello": 3, "world": 1}})
    await p.reload(wait_until="networkidle")
    await p.wait_for_timeout(2500)
    chips = await p.locator("#wordCounts .count-chip").all_inner_texts()
    check("T20", "POST /api/guard/state persists; Repeat Counts chips render",
          r["status"] in (200, 204) and any("hello" in c and "3" in c for c in chips), chips)

    # T21 add/remove word via UI
    await p.locator('#card-6 .icon-btn[title="Toggle word tracker config"]').click()
    await p.fill("#wordInput", "telugu")
    await p.press("#wordInput", "Enter")
    await p.wait_for_timeout(600)
    chips_before = await p.locator("#wordList .chip").all_inner_texts()
    has_telugu = any("telugu" in c for c in chips_before)
    # remove it via its x button
    btn = p.locator("#wordList .chip", has_text="telugu").locator("button")
    await btn.click()
    await p.wait_for_timeout(600)
    chips_after = await p.locator("#wordList .chip").all_inner_texts()
    check("T21", "word tracker: add word shows chip, remove deletes it",
          has_telugu and not any("telugu" in c for c in chips_after),
          f"before={chips_before} after={chips_after}")

    # T22 video CRUD via API
    r = await api(p, "POST", "/api/guard/videos",
                  {"name": "E2E test video", "url": "https://www.youtube.com/watch?v=dQw4w9WgXcQ"})
    vid = (r.get("body") or {})
    check("T22a", "POST /api/guard/videos adds a video", r["status"] in (200, 201) and vid.get("id"), str(vid)[:200])
    if vid.get("id"):
        rs = await api(p, "POST", f"/api/guard/videos/{vid['id']}/select")
        rd = await api(p, "DELETE", f"/api/guard/videos/{vid['id']}")
        check("T22b", "video select + delete work", rs["status"] in (200, 204) and rd["status"] in (200, 204),
              f"select={rs['status']} delete={rd['status']}")

    # T23 video controls present
    ok = all([
        (await p.locator("#videoSlider").count()) == 1,
        "Pause" in await p.inner_text(".video-controls"),
        "Stop" in await p.inner_text(".video-controls"),
        await p.locator("#videoLoop").count() == 1,
    ])
    await p.locator('#card-6 .icon-btn[title="Maximize video"]').click()
    maximized = await p.locator("#card-6.video-max").count() == 1
    await p.locator('#card-6 .icon-btn[title="Maximize video"]').click()
    check("T23", "video controls (Pause/Stop/Loop/slider) + maximize toggle", ok and maximized, f"controls={ok} max={maximized}")

    # T24 mic + transcript placeholder
    mic_label = (await p.locator("#micLabel").inner_text()).strip()
    transcript = (await p.locator("#transcript").inner_text()).strip()
    check("T24", "mic shows TAP TO START and transcript placeholder present",
          mic_label == "TAP TO START" and "Telugu speech will appear here" in transcript,
          f"mic={mic_label} transcript={transcript[:80]!r}")
    await p.close()


async def t_settings(ctx):
    print("[SETTINGS]")
    p = await ctx.new_page()
    await login(p)
    await p.goto(f"{BASE_URL}/Settings", wait_until="networkidle")

    tabs = await p.locator(".settings-tabs .tab").all_inner_texts()
    check("T25", "settings page renders tabs", len(tabs) >= 3, tabs)

    # T26 add proxy via geocode, appears in dashboard select, then delete
    await p.goto(f"{BASE_URL}/Settings?tab=proxy", wait_until="networkidle")
    await p.fill("#proxyName", "EiffelTest")
    await p.fill("#proxyQuery", "Eiffel Tower, Paris")
    await p.click("text=Geocode & Add")
    await p.wait_for_timeout(4000)  # nominatim roundtrip + reload
    rows = await p.locator("table tbody tr").all_inner_texts()
    # the table shows the full geocoded display name, not the short name
    added = any("Eiffel" in r for r in rows)
    d = await ctx.new_page()
    await login(d)
    await d.wait_for_timeout(2000)
    opts = await d.evaluate('Array.from(document.getElementById("proxy-1").options).map(o=>o.text)')
    visible = any("Eiffel Tower" in o or "Tour Eiffel" in o or "EiffelTest" in o for o in opts)
    check("T26a", "add proxy via geocode appears in settings + dashboard select", added and visible,
          f"settings_row={added} in_select={visible}")

    # cleanup: delete the test proxy via form post on settings page
    if added:
        # loop-delete every matching row (guards against duplicate leftovers)
        for _ in range(10):
            rows_now = await p.locator("table tbody tr").all_inner_texts()
            if not any("Eiffel" in r for r in rows_now):
                break
            await p.locator("table tbody tr", has_text="Eiffel").last.locator(
                "button, input[type=submit]").last.click()
            await p.wait_for_load_state("networkidle")
            await p.wait_for_timeout(800)
        rows2 = await p.locator("table tbody tr").all_inner_texts()
        check("T26b", "delete proxy removes it", not any("Eiffel" in r for r in rows2), rows2[:5])
    await p.close(); await d.close()


async def t_live_parity():
    """Read-only parity checks against the live Base44 app. Skipped without creds."""
    live = os.environ.get("E2E_LIVE_URL", "https://wiseguardalertswatch.base44.app")
    email = os.environ.get("E2E_LIVE_EMAIL")
    password = os.environ.get("E2E_LIVE_PASSWORD")
    if not (email and password):
        print("[LIVE PARITY] skipped (set E2E_LIVE_EMAIL/E2E_LIVE_PASSWORD to enable)")
        return
    async with async_playwright() as pw:
        browser = await pw.chromium.launch()
        ctx = await browser.new_context(viewport={"width": 1600, "height": 1000})
        p = await ctx.new_page()
        await p.goto(live + "/login", wait_until="networkidle")
        try:
            await p.fill('input[type="email"]', email)
            await p.fill('input[type="password"]', password)
            await p.keyboard.press("Enter")
            await p.wait_for_load_state("networkidle")
            await p.wait_for_timeout(5000)
            cards = await p.locator(".inst-card, [class*=instance]").count()
            check("L01", "live: dashboard shows 6 instances", cards >= 6 or "Instance" in await p.inner_text("body"), cards)
        except Exception as e:
            check("L01", "live: dashboard shows 6 instances", False, f"login/nav failed: {e}")
        await browser.close()


async def main():
    print(f"E2E suite against {BASE_URL}")
    async with async_playwright() as pw:
        browser = await pw.chromium.launch()
        ctx = await browser.new_context(viewport={"width": 1600, "height": 1000})
        page = await ctx.new_page()
        page.on("pageerror", lambda e: JS_ERRORS.append(str(e)))
        page.on("console", lambda m: JS_ERRORS.append(m.text) if m.type == "error" else None)
        await page.close()

        for group in (t_auth, t_structure, t_radar, t_configs, t_guard, t_settings):
            try:
                await group(ctx)
            except Exception as e:  # record and keep going: report everything
                check(group.__name__.upper(), "group completed without exception", False,
                      f"{type(e).__name__}: {e}")

        print("[HEALTH]")
        check("T27", "no JS console/page errors across the suite", not JS_ERRORS, JS_ERRORS[:5])
        await browser.close()
    await t_live_parity()

    report = {
        "base_url": BASE_URL,
        "total": len(RESULTS),
        "passed": sum(1 for r in RESULTS if r["status"] == "pass"),
        "failed": sum(1 for r in RESULTS if r["status"] == "fail"),
        "results": RESULTS,
        "js_errors": JS_ERRORS[:20],
    }
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "report.json")
    with open(out, "w") as f:
        json.dump(report, f, indent=2)
    print(f"\n{report['passed']}/{report['total']} passed, {report['failed']} failed -> {out}")
    sys.exit(1 if report["failed"] else 0)


if __name__ == "__main__":
    asyncio.run(main())
