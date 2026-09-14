"""Generic, reviewable association between two interface descriptions.

PUPILA is intentionally not an automation runtime.  It proposes how a task
or capability in one interface may correspond to an element in another one.
The host decides whether a proposal is safe and how to present it.
"""

from __future__ import annotations

from dataclasses import dataclass
import re
import unicodedata
from typing import Any, Iterable


SCHEMA = "pupila:interface-association:0.1"
ALGORITHM = "weighted-declared-evidence:0.1"


def _tokens(value: str | None) -> set[str]:
    normalized = unicodedata.normalize("NFKD", str(value or "").lower())
    normalized = "".join(char for char in normalized if not unicodedata.combining(char))
    return set(re.findall(r"[a-z0-9]{2,}", normalized))


def _tuple(values: Iterable[str] | None) -> tuple[str, ...]:
    return tuple(sorted({str(value).strip().lower() for value in (values or ()) if str(value).strip()}))


@dataclass(frozen=True)
class InterfaceElement:
    """A declared actionable or informational element in an interface."""

    element_id: str
    role: str
    label: str = ""
    action: str = ""
    modalities: tuple[str, ...] = ()
    capabilities: tuple[str, ...] = ()
    preconditions: tuple[str, ...] = ()

    def __post_init__(self) -> None:
        if not self.element_id.strip():
            raise ValueError("element_id is required")
        if not self.role.strip():
            raise ValueError("role is required")

    def evidence_tokens(self) -> set[str]:
        return (
            _tokens(self.role)
            | _tokens(self.label)
            | _tokens(self.action)
            | set().union(*(_tokens(value) for value in self.capabilities))
        )


@dataclass(frozen=True)
class InterfaceSnapshot:
    """A versioned, host-provided description of one interface state."""

    interface_id: str
    version: str
    elements: tuple[InterfaceElement, ...]

    def __post_init__(self) -> None:
        if not self.interface_id.strip() or not self.version.strip():
            raise ValueError("interface_id and version are required")
        identifiers = [element.element_id for element in self.elements]
        if len(identifiers) != len(set(identifiers)):
            raise ValueError("interface element ids must be unique")

    def by_id(self) -> dict[str, InterfaceElement]:
        return {element.element_id: element for element in self.elements}


@dataclass(frozen=True)
class TaskStep:
    """A task step anchored to an element in the known interface."""

    step_id: str
    intent: str
    source_element_id: str | None = None
    required_capabilities: tuple[str, ...] = ()

    def __post_init__(self) -> None:
        if not self.step_id.strip() or not self.intent.strip():
            raise ValueError("step_id and intent are required")


@dataclass(frozen=True)
class Task:
    task_id: str
    steps: tuple[TaskStep, ...]


@dataclass(frozen=True)
class MappingCandidate:
    source_element_id: str
    target_element_id: str
    score: float
    evidence: tuple[str, ...]


@dataclass(frozen=True)
class AssociationResult:
    schema: str
    algorithm: str
    source_interface: str
    target_interface: str
    candidates: tuple[MappingCandidate, ...]
    status: str
    provenance: dict[str, Any]
    ambiguous_sources: tuple[str, ...] = ()

    def for_source(self, source_element_id: str) -> tuple[MappingCandidate, ...]:
        return tuple(candidate for candidate in self.candidates if candidate.source_element_id == source_element_id)

    def is_ambiguous(self, source_element_id: str) -> bool:
        """Say whether this one source could not be told apart, not whether any could.

        Retaining runners-up for inspection is not the same claim as being
        unable to choose. A caller asking about one step needs the margin that
        was actually measured for that step.
        """

        return source_element_id in self.ambiguous_sources


def _candidate(source: InterfaceElement, target: InterfaceElement) -> MappingCandidate:
    score = 0.0
    evidence: list[str] = []
    source_role = _tokens(source.role)
    target_role = _tokens(target.role)
    if source_role and source_role == target_role:
        score += 0.30
        evidence.append("role_exact")
    elif source_role.intersection(target_role):
        score += 0.15
        evidence.append("role_overlap")

    source_action = _tokens(source.action)
    target_action = _tokens(target.action)
    if source_action and source_action == target_action:
        score += 0.30
        evidence.append("action_exact")
    elif source_action.intersection(target_action):
        score += 0.15
        evidence.append("action_overlap")

    source_label = _tokens(source.label)
    target_label = _tokens(target.label)
    if source_label and target_label:
        overlap = len(source_label & target_label) / max(len(source_label | target_label), 1)
        if overlap:
            score += 0.20 * overlap
            evidence.append("label_overlap")

    source_capabilities = set(_tuple(source.capabilities))
    target_capabilities = set(_tuple(target.capabilities))
    if source_capabilities and target_capabilities:
        overlap = len(source_capabilities & target_capabilities) / max(len(source_capabilities | target_capabilities), 1)
        if overlap:
            score += 0.15 * overlap
            evidence.append("capability_overlap")

    source_modalities = set(_tuple(source.modalities))
    target_modalities = set(_tuple(target.modalities))
    if source_modalities and target_modalities and source_modalities & target_modalities:
        score += 0.05
        evidence.append("modality_overlap")

    return MappingCandidate(
        source_element_id=source.element_id,
        target_element_id=target.element_id,
        score=round(min(score, 1.0), 6),
        evidence=tuple(evidence),
    )


def associate_interfaces(
    source: InterfaceSnapshot,
    target: InterfaceSnapshot,
    *,
    min_score: float = 0.35,
    ambiguity_margin: float = 0.08,
) -> AssociationResult:
    """Return a candidate set without executing or selecting silently.

    A source element is marked ambiguous when its best and second-best target
    are too close.  The result intentionally retains both candidates so a host
    or person can inspect the evidence.
    """

    if not 0.0 <= min_score <= 1.0 or not 0.0 <= ambiguity_margin <= 1.0:
        raise ValueError("thresholds must be between zero and one")
    candidates: list[MappingCandidate] = []
    ambiguous_sources: list[str] = []
    matched_sources = 0
    for source_element in source.elements:
        ranked = sorted(
            (_candidate(source_element, target_element) for target_element in target.elements),
            key=lambda item: (-item.score, item.target_element_id),
        )
        viable = [item for item in ranked if item.score >= min_score]
        if not viable:
            continue
        matched_sources += 1
        candidates.extend(viable[:3])
        if len(viable) > 1 and viable[0].score - viable[1].score < ambiguity_margin:
            ambiguous_sources.append(source_element.element_id)

    if not candidates:
        status = "NO_MATCH"
    elif ambiguous_sources:
        status = "AMBIGUOUS_CANDIDATES"
    else:
        status = "CANDIDATES_AVAILABLE"
    return AssociationResult(
        schema=SCHEMA,
        algorithm=ALGORITHM,
        source_interface=source.interface_id,
        target_interface=target.interface_id,
        candidates=tuple(candidates),
        status=status,
        provenance={
            "source_version": source.version,
            "target_version": target.version,
            "matched_source_count": matched_sources,
            "ambiguous_source_count": len(ambiguous_sources),
            "min_score": min_score,
            "ambiguity_margin": ambiguity_margin,
            "execution": "not_performed",
        },
        ambiguous_sources=tuple(ambiguous_sources),
    )


def translate_task(
    task: Task,
    source: InterfaceSnapshot,
    target: InterfaceSnapshot,
    *,
    min_score: float = 0.35,
    ambiguity_margin: float = 0.08,
) -> dict[str, Any]:
    """Translate task steps into target candidates, preserving uncertainty."""

    associations = associate_interfaces(
        source,
        target,
        min_score=min_score,
        ambiguity_margin=ambiguity_margin,
    )
    translated: list[dict[str, Any]] = []
    for step in task.steps:
        source_id = step.source_element_id
        candidates = associations.for_source(source_id) if source_id else ()
        if not candidates:
            status = "UNAVAILABLE"
        elif source_id is not None and associations.is_ambiguous(source_id):
            status = "AMBIGUOUS"
        else:
            status = "MAPPED"
        translated.append({
            "step_id": step.step_id,
            "intent": step.intent,
            "status": status,
            "target_candidates": [
                {
                    "element_id": candidate.target_element_id,
                    "score": candidate.score,
                    "evidence": list(candidate.evidence),
                }
                for candidate in candidates
            ],
            "execution": "not_performed",
        })
    return {
        "schema": SCHEMA,
        "task_id": task.task_id,
        "source_interface": source.interface_id,
        "target_interface": target.interface_id,
        "steps": translated,
        "status": "REVIEW_REQUIRED" if any(item["status"] != "MAPPED" for item in translated) else "MAPPED_FOR_REVIEW",
        "provenance": associations.provenance,
    }
