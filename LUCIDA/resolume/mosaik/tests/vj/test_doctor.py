import json
import sys
from pathlib import Path

TOOLS_ROOT = Path(__file__).resolve().parents[2] / "tools"
sys.path.insert(0, str(TOOLS_ROOT))

from mosaik.doctor import doctor_text_report, run_doctor
from mosaik_cli import main


def test_doctor_report_is_read_only_and_has_bounded_checks():
    report = run_doctor(Path(__file__).resolve().parents[2])

    assert report["tool"] == "MOSAIK Doctor"
    assert report["read_only"] is True
    assert report["safety"]["files_written"] is False
    assert report["safety"]["hardware_changed"] is False
    assert {check["status"] for check in report["checks"]} <= {"PASS", "WARN", "FAIL"}
    assert "MOSAIK DOCTOR" in doctor_text_report(report)


def test_doctor_cli_writes_json_report(tmp_path, capsys):
    report_path = tmp_path / "doctor.json"

    exit_code = main(["doctor", "--report", str(report_path)])

    assert exit_code in {0, 1}
    assert report_path.is_file()
    assert json.loads(report_path.read_text(encoding="utf-8"))["read_only"] is True
    assert "MOSAIK DOCTOR" in capsys.readouterr().out
