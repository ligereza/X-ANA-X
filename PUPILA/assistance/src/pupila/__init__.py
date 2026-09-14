"""Provider-independent interface association primitives."""

from .association import (
    AssociationResult,
    InterfaceElement,
    InterfaceSnapshot,
    Task,
    TaskStep,
    associate_interfaces,
    translate_task,
)

__all__ = [
    "AssociationResult",
    "InterfaceElement",
    "InterfaceSnapshot",
    "Task",
    "TaskStep",
    "associate_interfaces",
    "translate_task",
]
