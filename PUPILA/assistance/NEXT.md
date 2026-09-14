# NEXT — open work, observations, suggestions

Written from memory at the end of the 2026-09-07 session, without re-reading
the tree. Re-measure any number here before acting on it. Not a contract.

## What changed

`translate_task` derived a step's status from how many candidates were kept, so
any source with more than one viable target came back `AMBIGUOUS` while
`associate_interfaces` reported `CANDIDATES_AVAILABLE` for the same input. The
two public entry points contradicted each other about the one thing this module
exists to report. Ambiguity is now recorded per source element, where it is
measured.

## The numbers nobody has justified

This is the largest open item, and it is not a bug.

The scoring weights are 0.30 for an exact role, 0.30 for an exact action, 0.20
scaled by label overlap, 0.15 scaled by capability overlap and 0.05 for a
shared modality. `min_score` defaults to 0.35 and `ambiguity_margin` to 0.08,
and a viable set is cut to three candidates.

None of those has a written derivation, and no test pins why any of them is
that value rather than another. A caller cannot tell whether 0.35 is a measured
threshold or a first guess that survived. For a module that returns evidence
and uncertainty as its product, the weights are part of the claim.

Suggestion: before tuning them, write the case that decides one. A single pair
of interfaces where a weight change flips a decision is worth more than a
sweep.

## A real limitation of the current matcher

Token normalisation strips diacritics and keeps runs of two or more
alphanumerics, then compares sets. So "Exportar proyecto" and "Export file"
share no label token: `exportar` and `export` are different strings.

That is fine when both interfaces are described in one language. It is a
problem for the stated purpose -- translating a task from an interface a person
knows to one they do not -- because those two interfaces are exactly where the
declared labels are likely to differ in language. The engine currently leans on
`role` and `action`, which are the fields a host is least likely to fill
consistently.

## Not audited

`InterfaceSnapshot` carries `interface_id` and `version` strings and nothing
verifies either. There is no provenance for where a snapshot came from, when it
was taken, or whether the described interface still looks like that. The
boundary says a host supplies snapshots; nothing records which host, or when.

The `preconditions` and `required_capabilities` fields are declared on the
dataclasses and never read by the matcher.
