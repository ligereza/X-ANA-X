# PUPILA Visual

CPU geometry and measurement utilities for visual assistance. The package
turns declared observations into auditable rays, 3-D points and screen-plane
intersections; it does not treat an infrared image as a depth camera.

Metric stereo requires intrinsics for both cameras and their relative pose.
Without physical calibration evidence, callers must retain
`CALIBRATION_REQUIRED` or `CALIBRATION_EVIDENCE_REQUIRED`; valid synthetic
parameters never authorize publishing metric depth. A flat screen model does
not represent curved displays or multiple monitors.

The tracker, GPU inference, camera drivers, permissions and user interface are
separate layers. This package contains no camera capture or network client.
Experiments and fixtures are synthetic, except the separately opt-in
capability probe, which lists device formats without reading frames.

From this directory in PowerShell:

```powershell
$env:PYTHONPATH = "src"
python -B -m unittest discover -s tests -v
python -B experiments/characterize_noise.py
python -B experiments/characterize_calibration.py
python -B experiments/audit_model_calibration.py
python -B experiments/characterize_distribution.py
```

Install the listed numerical dependency with `python -m pip install -r
requirements.txt` only if it is not already available. Synthetic results are
fixture statistics, not physical calibration or production tolerances.
