#!/usr/bin/env python3
"""
Recursive validation orchestrator for MVCWiseGuardAlertsWatch.

Cycle: build -> (re)start server -> run E2E suite -> write report.
Exit 0 only when every test passes. On failure the failing cases are printed
with details; fix the app/source and re-run until the suite is green.

Usage:
  python3 tests/e2e/run_validation.py [--port 5193] [--skip-build] [--keep-server]

Requires: .NET 8 SDK (dotnet on PATH), Python 3 + playwright (chromium installed).
"""
import argparse
import json
import os
import signal
import subprocess
import sys
import time
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
WEB = os.path.join(REPO, "MVCWiseGuardAlertsWatch")


def kill_port(port):
    """Best-effort kill of whatever listens on the port (Linux /proc scan, then pgrep)."""
    for pid_dir in __import__("glob").glob("/proc/[0-9]*"):
        try:
            with open(os.path.join(pid_dir, "cmdline"), "rb") as f:
                cmd = f.read().replace(b"\0", b" ").decode(errors="ignore")
            if "dotnet" in cmd and REPO in cmd:
                os.kill(int(pid_dir.split("/")[-1]), signal.SIGKILL)
        except (OSError, ValueError):
            continue
    for pat in (f"MVCWiseGuardAlertsWatch.dll", "dotnet run"):
        try:
            subprocess.run(["pkill", "-f", pat], capture_output=True, timeout=10)
        except (FileNotFoundError, subprocess.TimeoutExpired):
            pass  # pkill not available; /proc scan above covers it
    time.sleep(1.5)


def build(skip=False):
    if skip:
        print("[1/4] build skipped")
        return True
    print("[1/4] dotnet build ...")
    r = subprocess.run(["dotnet", "build", os.path.join(REPO, "MVCWiseGuardAlertsWatch.sln"), "--nologo"],
                       capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stdout[-3000:]); print(r.stderr[-3000:])
        return False
    return "Build succeeded" in (r.stdout + r.stderr) or r.returncode == 0


def start_server(port):
    print(f"[2/4] starting server on 127.0.0.1:{port} ...")
    env = dict(os.environ, ASPNETCORE_URLS=f"http://127.0.0.1:{port}", ASPNETCORE_ENVIRONMENT="Development")
    proc = subprocess.Popen(
        ["dotnet", "run", "--no-build", "--no-launch-profile", "--nologo", "--project", WEB],
        cwd=WEB, env=env, stdout=open(os.path.join(HERE, "server.log"), "w"), stderr=subprocess.STDOUT)
    deadline = time.time() + 90
    while time.time() < deadline:
        try:
            with urllib.request.urlopen(f"http://127.0.0.1:{port}/Account/Login", timeout=2) as r:
                if r.status == 200:
                    print(f"      server up (pid {proc.pid})")
                    return proc
        except Exception:
            time.sleep(1)
    print("      server failed to start; tail of server.log:")
    print(open(os.path.join(HERE, "server.log")).read()[-2000:])
    return None


def run_suite(port):
    print("[3/4] running E2E suite ...")
    env = dict(os.environ, E2E_BASE_URL=f"http://127.0.0.1:{port}")
    r = subprocess.run([sys.executable, os.path.join(HERE, "test_suite.py")], env=env)
    return r.returncode == 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--port", type=int, default=int(os.environ.get("E2E_PORT", "5193")))
    ap.add_argument("--skip-build", action="store_true")
    ap.add_argument("--keep-server", action="store_true")
    args = ap.parse_args()

    print(f"=== recursive validation: MVCWiseGuardAlertsWatch @ port {args.port} ===")
    kill_port(args.port)
    if not build(args.skip_build):
        sys.exit(2)
    proc = start_server(args.port)
    if not proc:
        sys.exit(2)
    try:
        ok = run_suite(args.port)
    finally:
        if not args.keep_server:
            proc.terminate()
            try: proc.wait(10)
            except Exception: proc.kill()
        else:
            print(f"[4/4] server left running (pid {proc.pid})")
    print("[4/4] " + ("ALL TESTS PASSED" if ok else "FAILURES PRESENT - fix and re-run"))
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
