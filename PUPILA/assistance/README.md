# PUPILA

PUPILA is a local-first engine for translating a task from an interface a
person already knows to an interface they do not know yet.

The engine does not copy a visual interface, execute clicks, or assume that
two applications have the same internal implementation. It compares declared
capabilities, roles, actions, labels, modalities, and preconditions, then
returns a reviewable candidate set with evidence and uncertainty.

## Boundary

```text
interface snapshot A + task intent
        -> PUPILA association
        -> candidate mappings + evidence + ambiguity
        -> human or host application decides whether to apply
```

The first slice is deliberately provider-independent. An agent may supply a
snapshot or explain a mapping, but PUPILA remains the contract and verifier;
it is not a chatbot and it never commits an external action.

## Run

```text
python -m unittest discover -s tests -v
```

## Product boundary

The repository contains generic algorithms and contracts only. It does not
contain MAK works, private archives, credentials, user recordings, or an
application-specific corpus.
