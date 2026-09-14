import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).parents[1] / "src"))

from pupila import (  # noqa: E402
    InterfaceElement,
    InterfaceSnapshot,
    Task,
    TaskStep,
    associate_interfaces,
    translate_task,
)


class PUPILAAssociationTests(unittest.TestCase):
    def setUp(self):
        self.source = InterfaceSnapshot(
            "known-app",
            "1",
            (
                InterfaceElement(
                    "export",
                    "button",
                    "Exportar proyecto",
                    "export",
                    ("mouse", "keyboard"),
                    ("file_export",),
                ),
            ),
        )
        self.target = InterfaceSnapshot(
            "new-app",
            "2",
            (
                InterfaceElement(
                    "save-as",
                    "button",
                    "Export file",
                    "export",
                    ("mouse",),
                    ("file_export",),
                ),
                InterfaceElement("render", "button", "Render", "render", ("touch",), ("render",)),
            ),
        )

    def test_returns_mapping_with_evidence_and_never_executes(self):
        result = associate_interfaces(self.source, self.target)
        self.assertEqual(result.status, "CANDIDATES_AVAILABLE")
        self.assertEqual(result.candidates[0].target_element_id, "save-as")
        self.assertIn("action_exact", result.candidates[0].evidence)
        self.assertEqual(result.provenance["execution"], "not_performed")

    def test_ambiguous_result_keeps_candidates(self):
        ambiguous = InterfaceSnapshot(
            "ambiguous",
            "1",
            (
                InterfaceElement("a", "button", "Export", "export"),
                InterfaceElement("b", "button", "Export", "export"),
            ),
        )
        result = associate_interfaces(self.source, ambiguous, ambiguity_margin=0.5)
        self.assertEqual(result.status, "AMBIGUOUS_CANDIDATES")
        self.assertEqual(len(result.for_source("export")), 2)

    def test_task_translation_does_not_hide_unavailable_step(self):
        task = Task("export-task", (TaskStep("step-1", "export the project", "export"),))
        translated = translate_task(task, self.source, self.target)
        self.assertEqual(translated["steps"][0]["status"], "MAPPED")
        self.assertEqual(translated["steps"][0]["execution"], "not_performed")

    def test_missing_source_mapping_is_review_required(self):
        task = Task("unknown-task", (TaskStep("step-1", "publish", "missing"),))
        translated = translate_task(task, self.source, self.target)
        self.assertEqual(translated["status"], "REVIEW_REQUIRED")
        self.assertEqual(translated["steps"][0]["status"], "UNAVAILABLE")

    def crowded_target(self):
        """Three targets over the threshold with one clear winner."""

        return InterfaceSnapshot(
            "crowded",
            "1",
            (
                InterfaceElement("best", "button", "Exportar proyecto", "export", (), ("file_export",)),
                InterfaceElement("weaker", "button", "Export image", "export", (), ("file_export",)),
                InterfaceElement("weakest", "button", "Export audio", "export", (), ("file_export",)),
            ),
        )

    def test_retained_runners_up_do_not_claim_ambiguity(self):
        target = self.crowded_target()
        task = Task("export-task", (TaskStep("step-1", "export the project", "export"),))
        translated = translate_task(task, self.source, target)
        self.assertEqual(translated["steps"][0]["status"], "MAPPED")
        self.assertEqual(translated["status"], "MAPPED_FOR_REVIEW")
        self.assertEqual(len(translated["steps"][0]["target_candidates"]), 3)

    def test_margin_decides_ambiguity_and_not_candidate_count(self):
        target = self.crowded_target()
        result = associate_interfaces(self.source, target)
        self.assertEqual(result.status, "CANDIDATES_AVAILABLE")
        self.assertEqual(result.ambiguous_sources, ())
        self.assertFalse(result.is_ambiguous("export"))
        ranked = result.for_source("export")
        self.assertEqual(len(ranked), 3)
        self.assertGreater(ranked[0].score - ranked[1].score, result.provenance["ambiguity_margin"])

    def test_ambiguity_is_reported_for_the_source_that_has_it(self):
        two_intents = InterfaceSnapshot(
            "known-app",
            "1",
            (
                InterfaceElement("export", "button", "Exportar proyecto", "export", (), ("file_export",)),
                InterfaceElement("render", "button", "Render video", "render", (), ("render",)),
            ),
        )
        mixed = InterfaceSnapshot(
            "new-app",
            "2",
            (
                InterfaceElement("save-as", "button", "Export file", "export", (), ("file_export",)),
                InterfaceElement("export-copy", "button", "Export file", "export", (), ("file_export",)),
                InterfaceElement("render-now", "button", "Render video", "render", (), ("render",)),
            ),
        )
        result = associate_interfaces(two_intents, mixed)
        self.assertEqual(result.status, "AMBIGUOUS_CANDIDATES")
        self.assertEqual(result.ambiguous_sources, ("export",))
        self.assertTrue(result.is_ambiguous("export"))
        self.assertFalse(result.is_ambiguous("render"))
        self.assertEqual(result.provenance["ambiguous_source_count"], 1)

        task = Task(
            "mixed-task",
            (
                TaskStep("step-1", "export the project", "export"),
                TaskStep("step-2", "render the video", "render"),
            ),
        )
        translated = translate_task(task, two_intents, mixed)
        by_step = {item["step_id"]: item["status"] for item in translated["steps"]}
        self.assertEqual(by_step, {"step-1": "AMBIGUOUS", "step-2": "MAPPED"})
        self.assertEqual(translated["status"], "REVIEW_REQUIRED")


if __name__ == "__main__":
    unittest.main()
