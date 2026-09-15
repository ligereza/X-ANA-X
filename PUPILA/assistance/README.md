# PUPILA

PUPILA is a local-first assistance engine with two related capabilities:

- `src/pupila/association.py` proposes mappings from a task in an interface a
  person knows to one they are learning. Results include evidence and
  ambiguity; PUPILA does not execute actions.
- `src/pupila/runtime/` turns explicitly supplied interaction signals into
  consent-aware, expiring proposals. The local prototype uses synthetic
  events; it does not infer a person's mental state or collect real input by
  itself.
- `visual/` contains a CPU geometry and measurement kernel. Metric depth stays
  unauthorized until physical calibration evidence is supplied and verified.

The runnable prototype is at `apps/local_assistance/`. It stores state in a
local SQLite file, binds its HTTP server to loopback, and presents proposals
for explicit review. Its optional LUCIDA consumer requires an explicit path;
it does not launch or control an Adobe, Resolume, or lighting application.

## Validation

From the repository root in PowerShell:

```powershell
$env:PYTHONPATH = "src"
python -B -m unittest discover -s tests -v
python -B -m unittest discover -s apps/local_assistance/tests -v
$env:PYTHONPATH = "visual/src"
python -B -m unittest discover -s visual/tests -v
```

The geometry tests require NumPy from `visual/requirements.txt`. Visual
characterization fixtures are synthetic and are not physical calibration.
