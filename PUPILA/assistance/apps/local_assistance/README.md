# PUPILA Local Assistance

Runnable local prototype for direct, reviewable assistance. It demonstrates
the cycle signal → proposal → explicit decision → state → optional LUCIDA
projection using synthetic input only.

The server binds to `127.0.0.1`; the default SQLite state file is local and
ignored by Git. The prototype does not collect real pointer, keyboard, gaze,
camera or application telemetry, infer mental state, or execute host actions.
Those integrations require separate, explicit adapters and consent policy.

Start from the repository root in PowerShell:

```powershell
$env:PYTHONPATH = "src"
python -B -m apps.local_assistance.server 8765 apps/local_assistance/work/pupila-state.sqlite3
```

Open `http://127.0.0.1:8765`. To run the local API and temporal tests:

```powershell
$env:PYTHONPATH = "src"
python -B -m unittest discover -s apps/local_assistance/tests -v
```

The optional LUCIDA replay check needs an explicit adapter path:

```powershell
python -B -m apps.local_assistance.verify_cycle --lucida-root "C:\path\to\LUCIDA\resolume\adapter"
```

The engine package is `src/pupila/runtime/`. FARMAKSIA's experiment remains
research evidence and is not an execution-time dependency.
