# Local assistance integration

`PupilaEngine` lives in `src/pupila/runtime/`; this application serves a small
local UI and HTTP API. The runtime owns the durable event log, consent epochs,
temporal expiry, versioned proposals, idempotent decisions and audit history.
Snapshots are pure; `tick()` materializes due temporal transitions.

The local seed is explicitly synthetic. Storage is SQLite on the user's
machine. Proposal acceptance only changes PUPILA state; it does not trigger an
Adobe, Resolume, console or network action. `LucidaConsumerSession` is an
optional isolated replay consumer and requires an explicit path to a LUCIDA
adapter package.

Reusable event, view and render-plan code was ported from the FARMAKSIA 090
experiment. The copied modules are maintained and tested inside PUPILA, so
the application never adds a FARMAKSIA directory to `sys.path`.
