import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from lucida.surface_projection import (
    SurfaceProjectionProposalV1,
    SurfaceProjectionSafetyV1,
    SurfaceProjectionV1,
)


ROOT = Path(__file__).parent
CONTRACT = ROOT / "xanax_lucida_grandma3_projection.json"


def overlap(a, b):
    return not (
        a["x"] + a["width"] <= b["x"]
        or b["x"] + b["width"] <= a["x"]
        or a["y"] + a["height"] <= b["y"]
        or b["y"] + b["height"] <= a["y"]
    )


def main():
    raw = json.loads(CONTRACT.read_text(encoding="utf-8"))
    projection = SurfaceProjectionV1(
        host_id=raw["host_id"],
        surface_id=raw["surface_id"],
        status=raw["status"],
        proposal=SurfaceProjectionProposalV1(
            proposal_id="xanax-titan-grandma3-001",
            reason="Show Titan analogies on a read-only grandMA3 visual surface",
            evidence=(
                "source:avolites-workspace-manual",
                "target:grandma3-view-and-window-manual",
                "fixture:farmaxia-083",
            ),
            execution_mode="proposal_only",
            requires_explicit_approval=True,
            reversible=True,
        ),
        safety=SurfaceProjectionSafetyV1(
            proposal_only=True,
            automatic_actions=False,
            external_side_effects=False,
            host_opened=False,
        ),
    )
    regions = raw["target_regions_normalized"]
    for region in regions:
        assert 0 <= region["x"] <= 1
        assert 0 <= region["y"] <= 1
        assert 0 < region["width"] <= 1
        assert 0 < region["height"] <= 1
        assert region["x"] + region["width"] <= 1
        assert region["y"] + region["height"] <= 1
    for index, left in enumerate(regions):
        for right in regions[index + 1 :]:
            assert not overlap(left, right), (left["task_id"], right["task_id"])
    assert raw["projection"]["changes_host_code"] is False
    assert raw["projection"]["creates_plugin"] is False
    assert raw["projection"]["injects_input"] is False
    assert raw["projection"]["writes_showfile"] is False
    assert raw["tests"]["execution"] == "blocked"
    print(
        json.dumps(
            {
                "result": "XANAX_LUCIDA_SURFACE_VERIFIED",
                "projection": projection.to_dict(),
                "mapped_tasks": len(raw["xanax_analogies"]),
                "regions": len(regions),
                "execution": "blocked",
                "web_reference_only": raw["evidence"]["web_references_are_semantic_only"],
            },
            ensure_ascii=False,
        )
    )


if __name__ == "__main__":
    main()
