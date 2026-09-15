"""Reproducible measurement of batch amortization, plus an independent oracle in tests."""
import copy
import json
import statistics
import tempfile
import time
from pathlib import Path

from apps.local_assistance.server import CONTEXT, EVENTS
from pupila.runtime.engine import PupilaEngine


def timed(size: int, repetitions: int = 7) -> dict:
    events = []
    for index in range(size):
        event = copy.deepcopy(EVENTS[index % len(EVENTS)])
        event["event_id"] = f"bench-{size}-{index}"
        event["peer_id"] = "participant-a" if index % 2 == 0 else "participant-b"
        event["sequence"] = index // 2 + 1
        events.append(event)
    batch_samples, replay_samples = [], []
    for _ in range(repetitions):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "batch.sqlite3"
            engine = PupilaEngine(path, clock=lambda: 1788724810000, stale_after_ms=10_000)
            started = time.perf_counter()
            engine.ingest_batch(events, CONTEXT, {"participant-a": True, "participant-b": True})
            engine.snapshot("pupila-demo")
            batch_samples.append((time.perf_counter() - started) * 1000)
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "replay.sqlite3"
            engine = PupilaEngine(path, clock=lambda: 1788724810000, stale_after_ms=10_000)
            started = time.perf_counter()
            for event in events:
                engine.ingest_event(event, CONTEXT, {"participant-a": True, "participant-b": True}.get(event["peer_id"]))
            engine.snapshot("pupila-demo")
            replay_samples.append((time.perf_counter() - started) * 1000)
    return {"events": size, "repetitions": repetitions, "batchReconcileMedianMs": round(statistics.median(batch_samples), 2), "perEventReconcileMedianMs": round(statistics.median(replay_samples), 2), "amortization": round(statistics.median(replay_samples) / statistics.median(batch_samples), 2)}


if __name__ == "__main__":
    print(json.dumps({"benchmark": [timed(size) for size in (7, 14, 28, 56)]}, indent=2))
