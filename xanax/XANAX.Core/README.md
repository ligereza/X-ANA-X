# XANAX Core

This is the executable core behind the A/B translation idea.

It has three layers:

1. `FixturePersonality` maps canonical physical attributes to DMX values, including 8/16-bit resolution and virtual channels. `GdtfProfileLoader` imports the same channel/function structure from a `.gdtf` archive or `description.xml`.
2. `BehaviorModel` describes how a program's controls produce canonical behavior.
3. `MissionBridge` computes the canonical change caused by a source gesture and solves the target change with a numerical Jacobian and damped least squares.
4. `PersonalityBridge` decodes one DMX personality into canonical physical values and re-encodes those values in another personality, even when channel order and resolution differ.
5. `DerivedCapabilityRule` expresses a real derived realization (for example RGB to CMY) and marks it separately from a direct capability.
6. `MissionCatalog` loads the A/B crosswalk and plans a canonical mission with source route, target route, relation, preconditions and target steps.

The core does not assume that the target has the same names, widgets, or number of controls. A target may need multiple parameters to reproduce one source change. If the target model is locally incapable of the requested change, the solver reports that instead of inventing an equivalence.

The runner contains a bidirectional center/amplitude <-> minimum/maximum test and a DMX personality encoding test.
