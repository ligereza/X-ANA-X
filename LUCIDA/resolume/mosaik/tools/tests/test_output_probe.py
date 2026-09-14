import json
from pathlib import Path
import subprocess
from unittest.mock import patch

import pytest
from jsonschema import Draft202012Validator

from tools.mosaik.media import MosaikError
from tools.mosaik.output_probe import probe_windows_output


ROOT = Path(__file__).resolve().parents[2]
SCHEMA = ROOT / "schemas" / "nayade-output-probe.schema.json"


def _wmi_payload():
    return {
        "adapters": [
            {
                "Name": "NVIDIA Test Adapter",
                "DriverVersion": "999.0",
                "CurrentHorizontalResolution": 1920,
                "CurrentVerticalResolution": 1080,
                "CurrentRefreshRate": 60,
                "VideoModeDescription": "1920 x 1080 (32 bit) (60Hz)",
            }
        ],
        "monitors": [
            {
                "ManufacturerName": [78, 86, 0],
                "UserFriendlyName": [77, 79, 83, 65, 73, 75, 0],
                "ProductCodeID": [1, 2, 0],
                "Active": True,
            }
        ],
    }


def test_windows_output_probe_normalizes_adapter_and_edid_without_serials():
    completed = subprocess.CompletedProcess(
        args=["pwsh"],
        returncode=0,
        stdout=json.dumps(_wmi_payload()),
        stderr="",
    )
    with (
        patch("tools.mosaik.output_probe.platform.system", return_value="Windows"),
        patch("tools.mosaik.output_probe.shutil.which", return_value="pwsh"),
        patch("tools.mosaik.output_probe.subprocess.run", return_value=completed) as run,
    ):
        report = probe_windows_output()

    assert report["status"] == "PASS"
    assert report["output_signal"] == {
        "resolution": "1920x1080",
        "refresh_hz": 60,
        "color_range": "unknown",
        "color_space": "unknown",
    }
    assert report["displays"][0]["name"] == "MOSAIK"
    assert "serial" not in json.dumps(report).casefold()
    assert report["safety"]["external_side_effects"] is False
    command = run.call_args.args[0]
    assert "-NoProfile" in command
    assert "-NonInteractive" in command
    schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
    assert list(Draft202012Validator(schema).iter_errors(report)) == []


def test_windows_output_probe_stops_a_timed_out_query():
    with (
        patch("tools.mosaik.output_probe.platform.system", return_value="Windows"),
        patch("tools.mosaik.output_probe.shutil.which", return_value="pwsh"),
        patch(
            "tools.mosaik.output_probe.subprocess.run",
            side_effect=subprocess.TimeoutExpired("pwsh", 0.1),
        ),
    ):
        with pytest.raises(MosaikError, match="excedió"):
            probe_windows_output(timeout_seconds=0.1)


def test_windows_output_probe_rejects_non_windows_hosts():
    with patch("tools.mosaik.output_probe.platform.system", return_value="Linux"):
        with pytest.raises(MosaikError, match="sólo está disponible en Windows"):
            probe_windows_output()
