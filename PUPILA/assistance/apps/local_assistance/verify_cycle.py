from __future__ import annotations

import copy
import argparse
import json
import tempfile
from pathlib import Path

from apps.local_assistance.lucida_consumer import LucidaConsumerSession
from apps.local_assistance.server import CONTEXT, EVENTS
from pupila.runtime.engine import EngineError, PupilaEngine


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--lucida-root", required=True, help="Path to the LUCIDA Resolume adapter package root")
    args = parser.parse_args()
    with tempfile.TemporaryDirectory() as folder:
        db = Path(folder) / "cycle.sqlite3"
        clock = lambda: 1788724810000
        def open_engine():
            return PupilaEngine(db, clock=clock, stale_after_ms=10_000)
        engine = open_engine()
        engine.ingest_batch(copy.deepcopy(EVENTS), CONTEXT, {"participant-a": True, "participant-b": True})
        before = engine.snapshot("pupila-demo")
        proposal = next(p for p in before["proposals"] if p["state"] == "proposed")
        accepted = engine.decide("pupila-demo", proposal["proposalId"], proposal["version"], "accept", "cycle-accept")
        restarted = open_engine().snapshot("pupila-demo")
        reverted = open_engine().decide("pupila-demo", proposal["proposalId"], proposal["version"], "revert", "cycle-revert", revert_decision_id="cycle-accept")
        new_event = copy.deepcopy(EVENTS[0]); new_event.update({"event_id": "cycle-new-evidence", "sequence": 6, "payload": {"focused": False}, "source_timestamp": "2026-09-06T20:00:07+00:00", "received_timestamp": "2026-09-06T20:00:07+00:00"})
        after_evidence = open_engine().ingest_event(new_event, CONTEXT, None)["state"]
        invalidated = open_engine().set_consent("pupila-demo", "participant-b", False)
        reconnected = open_engine().snapshot("pupila-demo")
        views = [before["lucida"], accepted["state"]["lucida"], reverted["state"]["lucida"], after_evidence["lucida"], invalidated["state"]["lucida"]]
        with LucidaConsumerSession(args.lucida_root) as consumer:
            consumer.accept_snapshot(views[0])
            for view in views[1:]: consumer.apply(view)
            applied_checkpoint = consumer.checkpoint()
        with LucidaConsumerSession(args.lucida_root) as resumed:
            resumed.accept_snapshot(views[-1], recovery=True)
            resumed_checkpoint = resumed.checkpoint()
        lucida = {"appliedUpdateCount": applied_checkpoint["applied_delta_count"], "finalProposalCount": len(views[-1]["pending_proposals"]), "loadedLucidaPath": resumed_checkpoint["view"]["session_id"], "resumedProposalCount": len(resumed_checkpoint["view"]["pending_proposals"])}
        checks = {"unknown_proposal": False, "cross_session": False, "stale_version": False, "blocked_without_consent": False}
        try: engine.decide("pupila-demo", "invented", 1, "accept", "cycle-bad")
        except EngineError: checks["unknown_proposal"] = True
        try: engine.decide("other", proposal["proposalId"], proposal["version"], "accept", "cycle-cross")
        except EngineError: checks["cross_session"] = True
        try: engine.decide("pupila-demo", proposal["proposalId"], 99, "accept", "cycle-old")
        except EngineError: checks["stale_version"] = True
        blocked_engine = PupilaEngine(Path(folder) / "blocked.sqlite3")
        blocked = blocked_engine.ingest_event(copy.deepcopy(EVENTS[0]), CONTEXT, False)
        checks["blocked_without_consent"] = blocked["status"] == "blocked"
        result = {"before": {"revision": before["revision"], "participants": before["pupila"]["participantCount"], "proposalCount": before["pupila"]["proposalCount"]}, "accept": {"effect": accepted["effect"], "proposalCount": accepted["state"]["pupila"]["proposalCount"]}, "restart": {"proposalCount": restarted["pupila"]["proposalCount"], "revision": restarted["revision"]}, "revert": {"effect": reverted["effect"], "proposalCount": reverted["state"]["pupila"]["proposalCount"]}, "newEvidence": {"proposalVersion": next(p["version"] for p in after_evidence["proposals"] if p["state"] == "proposed")}, "invalidation": {"participants": invalidated["state"]["pupila"]["participantCount"], "proposalCount": invalidated["state"]["pupila"]["proposalCount"]}, "reconnected": {"proposalCount": reconnected["pupila"]["proposalCount"]}, "lucidaConsumer": {"appliedUpdateCount": lucida["appliedUpdateCount"], "finalProposalCount": lucida["finalProposalCount"], "resumedProposalCount": lucida["resumedProposalCount"], "resumedSession": lucida["loadedLucidaPath"]}, "negativeChecks": checks}
        print(json.dumps(result, ensure_ascii=False, indent=2))
        if not all(checks.values()) or accepted["state"]["pupila"]["proposalCount"] != 0 or reverted["state"]["pupila"]["proposalCount"] != 1 or invalidated["state"]["pupila"]["proposalCount"] != 0 or lucida["finalProposalCount"] != 0 or lucida["resumedProposalCount"] != 0:
            raise SystemExit("cycle verification failed")


if __name__ == "__main__":
    main()
