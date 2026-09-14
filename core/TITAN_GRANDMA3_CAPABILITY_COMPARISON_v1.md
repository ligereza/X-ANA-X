# Titan <-> grandMA3: comparison of documentation, capabilities and limits

Status: documentary comparison, not live validation.

Date: 2026-09-10.

Scope: Avolites Titan and MA Lighting grandMA3 onPC as operator systems for
live programming and playback. The comparison is for XANAX/LUCIDA and does
not claim that a showfile can be converted automatically.

## Executive decision

Titan and grandMA3 solve the same family of lighting-control problems, but
they do not organize meaning in the same places. A valid bridge must translate
the operator's intent and the host's state, then choose a native route in the
target. A pixel-to-pixel inverse is insufficient.

The safest live order is:

1. select fixtures or groups;
2. edit a basic attribute with verified context;
3. recall a reusable value;
4. inspect and edit a cue/sequence;
5. operate playback/executor;
6. effects, recipes, timecode and advanced priority behavior.

The first two are the only candidates for the first live slice. The remaining
ones need structural translation and explicit loss reporting.

## Coverage map

| Area | Shared capability | Titan model | grandMA3 model | XANAX consequence |
|---|---|---|---|---|
| Patch | Fixtures are patched to a DMX address and a control identity | Personality + fixture/dimmer handle; personality is embedded in the show after patching | Fixture type + attributes + parameters + FID/CID + universe/stage data | `fixture_identity` must preserve both handle/ID and parameter model |
| Selection | Select one or more controllable objects | Fixture buttons, groups, keypad and pages | Fixture Sheet, Group Pool, command line, Preset Pool; parent/subfixture hierarchy | Never assume one Titan fixture equals one grandMA3 object |
| Groups | Reusable fixture selection | Group stores selection order and optional 2D layout | Group stores selection order and grid position; recipes can use groups | Map group as a selection intent, not as a universal value reference |
| Selection transform | Spread values across a selected set | Fan and Align use selection order and attribute wheels | MAtricks, Selection Grid, Blocks, Groups, Wings, Width, Shuffle, Invert, Transform and Align | Treat as a separate intent, not as a wheel remap |
| Attributes | Modify pan, tilt, dimmer, color, gobo, beam or effects | IPCGBESFX banks, wheels, Attribute Editor, Programmer | Contextual Encoder Bar, encoder page/layer, feature group, Fixture Sheet, Programmer | Context must be part of the click mapping |
| Reusable values | Recall stored values for selected fixtures | Palette families such as Position, Colour and Gobo | Preset pools by feature group; Selective/Global/Universal; recipes | Palette -> Preset is partial and may change scope or source priority |
| Programming state | Temporary values can be recorded | Programmer, record by channel/fixture, tracking and cue merge | Programmer layers, active values, filters, worlds, recipes | State readback is mandatory before translating a store/update action |
| Environment | Edit without immediately changing stage output | Blind mode and Visualiser | Preview environment plus live/output views; preview side effects depend on operation | Peek is visual only; Blind/Preview are programming states |
| Cue content | Store a look with values and timing | Cue, Chase, Cue List and Timeline | Cue inside Sequence; Cue Parts, layers, recipes and multi-step phaser | One visible row may not represent the same semantic unit |
| Playback | Fire, fade, flash, stop and release live material | Cue/Chase/Cue List stored on a Playback handle or fader | Executor controls a Sequence from the Sequence Pool | Map `playback_intent`, not `playback_button` |
| Effects | Animate attribute values across fixtures | Shape Generator, Key Frame Shapes, Pixel Mapper | Phasers, recipes, shapes, layers and value generators | Preserve phase, spread, timing and source; do not flatten to “effect” |
| Timing | Automate transitions or shows | Cue/cue-list timings, overlaps, tracking, timecode sources | Cue timing layers plus Timecode Shows, tracks, events and slots | Time must be represented separately from the visual row |
| Output priority | Resolve multiple sources into DMX output | HTP for intensity, LTP for other channels, playback priority | External input, tester, parked, super, programmer, playback priorities, masters, selection overrides | A visually identical result may have different ownership and future behavior |
| Workspace | Operator can build a personal surface | Workspace Windows, layouts, Users and Handle Worlds | Screens, Views, Screen Configurations, User Profiles, Worlds, Filters and Layouts | Recompose visuals, but retain the host's context graph |
| Restriction/filtering | Limit what an operator can see or change | User/handle-world separation and show organization | User rights, Worlds, Filters, input/output filters | A hidden object is not an absent object; peek must show restrictions |
| Automation/API | Repeat actions or trigger behavior | Macros plus official Titan Web API with HTTP/JSON, handles, TitanId and playback scripts | Macros plus command line, OSC and Lua plugins with object APIs | Prefer native semantic routes on both systems; mouse is fallback |
| Remote | Operate away from the main surface | Titan Remote app, separate programmer per remote | Web Remote, OSC, MIDI/DMX remotes and command-line control | XANAX should use native remote/command routes before pixel injection |
| Network | Multi-user and backup | TitanNet sessions, master/slave, multi-user and backup | MA Sessions, Master/GlobalMaster/Connected stations | Do not assume synchronized state means identical UI geometry |
| Transport | Carry a control or output event | DMX lines, Art-Net, sACN, TitanNet, Titan API, MIDI/SMPTE and Titan Remote | DMX I/O, Art-Net, sACN, OSC, MIDI/MSC, DMX remotes, PSN, Web Remote and Sessions | Store transport context separately from semantic intent and output ownership |
| Remote semantics | Describe what an external signal does | Remote/API/MIDI/SMPTE paths can carry triggers or controls | DMX Remote can trigger a target, MIDI can encode note/CC behavior, PSN carries 3D position, and external inputs can win output priority | Classify `event_trigger`, `continuous_control`, `spatial_tracking` and `output_source` separately |
| Windows surface input | Turn the learned surface into a real operation | Requires a native route or calibrated OS input; overlay visuals do not move the host click alone | Same Windows boundary; UIA patterns may be semantic, SendInput is OS-level and focus/integrity constrained | Inspect UIA first; use foreground-verified input injection only as fallback |
| Capability gate | Determine what the concrete installation can really do | AvoKey/hardware, Titan Go/Simulator mode, console/TNP limits, MIDI/timecode availability and possible Simulator spoilers | onPC hardware unlock, parameter count, DMX-key/viz-key/processing-unit state, session and visualizer limits | Check capability before geometry or live input; do not infer output from a visible menu |
| Local installation | Bind the documentary model to the actual host | Titan 19.1.205.5, Titan Go, Titan Simulator and WebAPI are installed | grandMA3 2.4.2.2 is installed; 2.3.1 data is also present | Version-sensitive behavior must be checked locally; installed files do not prove license, hardware or output |
| Show schema | Learn how project state is actually exposed | Local `.d4z` shows are ZIP containers with `Show.xml`, fixture partitions, Capture and explicit entity pools | Local `.show` demos carry a `GMA3` binary signature and are not ZIP/text | Do not assume symmetric parsing; use host-native/export routes and preserve show version provenance |
| Show file | Save project state | Show, users, handle worlds, patch and programmed content | Patch, fixture profiles, cues, timings, 3D, users and profiles | Never rewrite source showfiles while researching or learning |
| Data propagation | Reuse or transfer rig/show data | Patch and Capture synchronization | Clone across show objects, multipatch, GDTF/MVR and layouts | Read-only/high-risk class; never treat as surface operation |
| Media | Control visual content alongside lighting | Synergy for Ai/Prism and CITP media-server fixtures | Images, Videos, Sounds, Bitmaps, NDI and media pools | Separate media intent from lighting fixture intent |
| Data context | Separate operator/show working contexts | Users and Handle Worlds | Data Pools, User Profiles, Worlds and Preview Pool 128 | Pool/profile identity is part of state |
| 3D/visualization | Visualize rig and output context | Capture Visualiser integration is part of Titan workflow | grandMA3 3D Viewer and stage/fixture geometry | Use 3D/geometry as evidence only after version and patch are known |

## 1. Patch and fixture identity

### Similarity

Both systems hide raw DMX implementation behind a fixture definition. The
operator works with fixture attributes rather than manually calculating every
DMX channel.

### Difference

Titan uses a personality file and patches the fixture to a button, fader or
macro/executor handle. The personality tells Titan which channel is dimmer and
how fixture-specific controls behave. Once patched, the personality is
embedded in the show.

grandMA3 exposes a Patch menu with fixture types, attribute definitions,
parameter lists, DMX universes, stages and curves. Its documentation treats
parameters as the internal functions that are calculated before being scaled
to DMX output. This is a deeper internal vocabulary than a simple “channel”.

Sources: [Titan personalities](https://manual.avolites.com/docs/fixture-personalities/),
[Titan patching](https://manual.avolites.com/docs/next/quick-start/patching-fixtures/),
[grandMA3 patch](https://help.malighting.com/grandMA3/2.4/HTML/patch.html),
[grandMA3 parameters](https://help.malighting.com/grandMA3/2.4/HTML/system_parameter.html).

### XANAX rule

`Fixture 12` is not enough as an identity. Store:

`host`, `id_type`, `fixture_id`, `subfixture_path`, `personality_or_fixture_type`,
`attribute_definition`, `dmx_address`, `selection_context`.

## 2. Selection, groups and layouts

### Similarity

Both systems treat selection order and spatial organization as meaningful for
distributed effects. Both provide reusable groups and visual layouts.

### Difference

Titan groups retain selection order and can retain a 2D layout for shapes and
pixel mapping. grandMA3 groups retain selection order and grid position, while
the system also supports parent/subfixture hierarchy and separate Layout
objects.

grandMA3 explicitly separates group identity from the values stored in cues or
presets. A group can be a source in a recipe, but a cue is not automatically a
live reference to every group that was used while programming it.

Sources: [Titan groups](https://manual.avolites.com/docs/14.0/controlling-fixtures/fixture-groups/),
[grandMA3 groups](https://help.malighting.com/grandMA3/2.3/HTML/group.html),
[grandMA3 selection](https://help.malighting.com/grandMA3/2.4/HTML/operate_select_fixtures.html),
[grandMA3 layouts](https://help.malighting.com/grandMA3/2.4/HTML/layout_create.html).

### XANAX rule

The first safe mapping is `select_fixture_set`. It must reveal whether the
destination is a parent fixture, a subfixture, a group, or a layout element.

## 3. Attributes and the Programmer

### Similarity

Both have a temporary programming state and both let the operator manipulate
attributes before storing or recalling them.

### Difference

Titan's visible grammar starts with fixed attribute banks and wheels. The bank
selection tells the operator whether the wheels address intensity, position,
colour, gobo, beam, effect or special functions.

grandMA3's Encoder Bar is context-sensitive. Encoder page, feature group,
layer, channel function, link mode and current selection affect the meaning of
the visible encoder. The Fixture Sheet can also display programmer and playback
layers.

Sources: [Titan attributes](https://manual.avolites.com/docs/next/controlling-fixtures/changing-fixture-attributes/),
[grandMA3 views](https://help.malighting.com/grandMA3/2.3/HTML/qsg_first_view.html),
[grandMA3 Encoder Bar](https://help.malighting.com/grandMA3/2.3/HTML/ws_encoder_bar.html),
[grandMA3 Encoder Toolbar](https://help.malighting.com/grandMA3/2.4/HTML/ws_eb_encoder_toolbar.html).

### XANAX rule

For a mapped wheel or button, the record must include:

`attribute`, `feature_group`, `page`, `layer`, `link_mode`, `selection`,
`programmer_state`, `expected_output_change`.

This is why moving the mouse alone cannot solve the problem.

## 4. Palette versus Preset

### Similarity

Both store reusable values which can be recalled for selected fixtures and can
be used while building cues.

### Difference

Titan palettes are grouped by functions such as position, colour and gobo and
can be referenced by cues. grandMA3 presets have feature-group pools and modes
that determine the scope of stored values. Presets can also include recipes and
phaser layers; the relationship can be recast into cues or extracted into the
programmer.

Sources: [Titan palettes](https://manual.avolites.com/docs/next/palettes/),
[grandMA3 presets](https://help.malighting.com/grandMA3/2.4/HTML/presets.html),
[grandMA3 use preset](https://help.malighting.com/grandMA3/2.4/HTML/presets_use.html),
[grandMA3 recipes](https://help.malighting.com/grandMA3/2.3/HTML/recipes.html).

### XANAX rule

Use `reusable_value` as the common intent, but show the conversion class:

- `direct`: same attribute scope and reusable reference;
- `partial`: value can be recalled but scope/mode differs;
- `lossy`: recipe, phaser or priority information is discarded;
- `unsupported`: no safe destination route.

## 5. Cues, sequences and playbacks

### Similarity

Both store looks, values, timing and sequences for live operation.

### Difference

Titan defines a Cue as a look, a Chase as a timed sequence, a Cue List as a
linked sequence and a Timeline as a timed sequence of playbacks. When these
objects are stored on a control, the control is a Playback.

grandMA3 organizes Cues inside Sequences. Cues may contain hard values,
preset references and recipes; values live in Cue Parts. An Executor is a
handle/control for a Sequence in the Sequence Pool, not the owner of the
sequence data.

Sources: [Titan cues](https://manual.avolites.com/docs/cues/),
[Titan playback](https://manual.avolites.com/docs/cues/cue-playback/),
[grandMA3 cues and sequences](https://help.malighting.com/grandMA3/2.4/HTML/cue_sequence.html),
[grandMA3 executors](https://help.malighting.com/grandMA3/2.3/HTML/executor.html).

### XANAX rule

`playback_control` must resolve ownership before sending Go, fader, flash or
release. The visual control may be familiar, but the data object behind it is
not interchangeable.

## 6. Effects and dynamic values

### Similarity

Both systems can animate fixture attributes across time and across fixture
selection order or spatial layout.

### Difference

Titan provides a Shape Generator, Key Frame Shapes and Pixel Mapper. Key Frame
Shapes can contain multiple layers, timing curves, phase, spread and overlap.

grandMA3 uses phasers with steps, curves, phase and layers. Phaser recipes can
be stored in presets or cue parts. The Generator pool can create dynamic
absolute values, including random variation.

Sources: [Titan effects](https://manual.avolites.com/docs/16.0/effects/),
[Titan key-frame shapes](https://manual.avolites.com/docs/effects/key-frame-shapes/),
[grandMA3 cues and sequences](https://help.malighting.com/grandMA3/2.4/HTML/cue_sequence.html),
[grandMA3 phaser example](https://help.malighting.com/grandMA3/2.4/HTML/phaser_create_dimmer.html),
[grandMA3 generator](https://help.malighting.com/grandMA3/2.4/HTML/generator.html).

### XANAX rule

Never translate all effects to a generic “FX” button. Preserve:
`steps`, `phase`, `spread`, `overlap`, `speed`, `curve`, `layer`, `source` and
whether the effect is stored or currently running.

## 7. Timing and timecode

### Similarity

Both support timed cues and external time sources for repeatable shows.

### Difference

Titan attaches timing, tracking, overlap and timecode behavior to cues and cue
lists. Its documentation describes cue-list timecode sources and timed cue
execution.

grandMA3 stores Timecode Shows in a pool, organized into track groups, tracks,
time ranges and events. Timecode Slots can use internal time, SMPTE/LTC, MIDI
timecode or ArtTimeCode.

Sources: [Titan cue-list timing](https://manual.avolites.com/docs/12.0/cue-lists/cue-list-timing/),
[Titan cue-list options](https://manual.avolites.com/docs/cue-lists/cue-list-options/),
[grandMA3 Timecode Show](https://help.malighting.com/grandMA3/2.4/HTML/timecode.html),
[grandMA3 external connections](https://help.malighting.com/grandMA3/2.4/HTML/timecode_external_connections.html).

### XANAX rule

Time must be a separate canonical object, not inferred from where a cue row is
drawn. A Timecode Show event and a Titan cue timestamp may produce a similar
stage result while having different edit semantics.

## 8. Output ownership and priority

### Similarity

Both resolve multiple sources into a final DMX output and both use HTP/LTP
concepts.

### Difference

Titan documents HTP for dimmer/intensity and LTP for other channels, with
playback priority affecting conflicts.

grandMA3 documents a broader priority chain: external inputs, DMX Tester,
parked values, super-priority playbacks, programmer, normal playback
priorities, Grand Master, World Masters, Group Masters and selection overrides.

Sources: [Titan HTP/LTP](https://manual.avolites.com/docs/cues/cue-playback/),
[grandMA3 DMX priorities](https://help.malighting.com/grandMA3/2.4/HTML/dmx_priorities.html),
[grandMA3 worlds and filters](https://help.malighting.com/grandMA3/2.4/HTML/worldfilter.html).

### XANAX rule

A successful visual click is not enough. XANAX must predict who owns the
resulting value and whether a later playback will override it.

## 9. Workspace, views, worlds and filters

### Similarity

Both allow an operator to save a preferred working surface and to separate
operators or tasks.

### Difference

Titan's primary visual organization is workspace windows, saved layouts,
users and Handle Worlds containing handle layouts.

grandMA3 separates screens, views, screen configurations, user profiles,
worlds, filters, pool windows and layouts. Worlds restrict fixtures/attributes;
filters can block values from store/recall or mask sheets.

Sources: [Titan workspace windows](https://manual.avolites.com/docs/titan-basics/workspace-windows/),
[Titan multi-user](https://manual.avolites.com/docs/13.0/titan-basics/multi-user-operation/),
[grandMA3 windows/views](https://help.malighting.com/grandMA3/2.4/HTML/wvm.html),
[grandMA3 worlds/filters](https://help.malighting.com/grandMA3/2.4/HTML/worldfilter.html),
[grandMA3 layouts](https://help.malighting.com/grandMA3/2.4/HTML/layout_create.html).

### XANAX rule

Do not interpret an absent button as an absent capability. It may be hidden by
a user profile, view, world, filter, screen configuration or page.

## 10. Macros, plugins and semantic control

### Similarity

Both can repeat actions and expose command-like control paths beyond direct
mouse interaction.

### Difference

Titan's documented macro behavior is a sequence of keypresses that the console
can replay, but Titan also exposes an official Web API. The Titan API uses
HTTP/JSON, supports `Get` and `Set`, enumerates programmed handles, exposes
`TitanId` and `UserNumber`, can set a selected fixture attribute into the
Programmer by control/function name, and includes scripts for firing, setting
levels and killing playbacks.

grandMA3 has a keyword command line, OSC access to the command line and Lua
plugins with object-free and object APIs. The plugin layer can go deeper than
normal macros and can be assigned to executors or view buttons.

Sources: [Titan macros](https://manual.avolites.com/docs/cue-lists/theatre-programming/),
[Titan Web API](https://api.avolites.com/19.2/),
[Titan API reference](https://api.avolites.com/19.2/api/),
[Titan Programmer API example](https://api.avolites.com/19.0/api/Programmer.Editor.Fixtures.SetControlValueByName.html),
[grandMA3 command syntax](https://help.malighting.com/grandMA3/2.4/HTML/command_syntax_keywords.html),
[grandMA3 OSC](https://help.malighting.com/grandMA3/2.4/HTML/remote_inputs_osc.html),
[grandMA3 plugins](https://help.malighting.com/grandMA3/2.4/HTML/plugins.html).

### XANAX rule

The adapter priority is:

1. native command/API route on the target;
2. native remote protocol;
3. accessibility/semantic UI route;
4. calibrated pixel/input route;
5. blind coordinate injection is forbidden.

This is the main capability discovery that changes the implementation plan.

## 11. Remote operation and networking

### Similarity

Both systems support networked collaboration and backup behavior.

### Difference

TitanNet links consoles for multi-user operation and synchronized backup; Titan
Remote provides a mobile interface with a separate programmer.

grandMA3 uses Sessions with station roles and a session master; Web Remote can
show and operate a station from a browser. Its In & Out menu includes DC,
MIDI, DMX, OSC, PSN and MVR-related configuration.

Sources: [Titan multi-user](https://manual.avolites.com/docs/13.0/titan-basics/multi-user-operation/),
[Titan remote](https://manual.avolites.com/docs/remote-control/),
[grandMA3 session](https://help.malighting.com/grandMA3/2.4/HTML/network_session.html),
[grandMA3 Web Remote](https://help.malighting.com/grandMA3/2.4/HTML/network_webremote.html),
[grandMA3 Remote In/Out](https://help.malighting.com/grandMA3/2.4/HTML/remote_inputs.html).

### XANAX rule

Before intercepting mouse input, test whether the target show can expose a
safe command or remote channel. A semantic remote route is more stable than a
screen coordinate and teaches LEARNING the actual operation instead of a
particular layout.

## 12. Showfile and version boundary

Titan and grandMA3 both persist project state beyond visible windows. grandMA3
documents that a showfile can contain patch, fixture profiles, cues, timings,
3D information, users and profiles, and that saving in a newer software
version moves the file forward. Titan stores users and Handle Worlds in the
showfile as well as programmed lighting data.

Sources: [grandMA3 show files](https://help.malighting.com/grandMA3/2.4/HTML/show_file_management.html),
[Titan multi-user and Handle Worlds](https://manual.avolites.com/docs/13.0/titan-basics/multi-user-operation/).

### XANAX rule

The adapter must be read-only while learning. No source showfile should be
rewritten, migrated or silently normalized. Learning records should reference
the showfile, version, user profile and surface configuration as provenance.

## 13. Fan, Align and MAtricks

### Similarity

Both systems can distribute an attribute over an ordered fixture selection.
This is the family of operations behind fans, aligned values, spreading and
effects that depend on fixture order.

### Difference

Titan Fan and Align use the fixture selection order and can spread values from
a source fixture or across a selected range. The result is closely tied to the
attribute wheels and the current Programmer state.

grandMA3 separates this into Selection Grid and MAtricks. MAtricks can divide
a selection by axes, blocks, groups, wings and width, and can also affect fade,
delay, speed, phase, shuffle, invert and transform. The grid is a spatial
relationship for selection and effects, not necessarily the physical position
in the 3D Viewer.

Sources: [Titan Fan and Align](https://manual.avolites.com/docs/13.0/controlling-fixtures/using-the-select-buttons-and-wheels/),
[grandMA3 MAtricks](https://help.malighting.com/grandMA3/2.4/HTML/matricks.html),
[grandMA3 Selection Grid](https://help.malighting.com/grandMA3/2.4/HTML/operate_selection.html),
[grandMA3 Align](https://help.malighting.com/grandMA3/2.4/HTML/operate_align.html).

### XANAX rule

Add a separate canonical intent: `selection_transform`. It is not an
attribute edit. The mapping must preserve selection order/grid, transform
parameters, target attribute and Programmer state.

## 14. Live, Blind, Preview and tracking

### Similarity

Both systems distinguish programming state from stage output and provide ways
to edit or inspect cues while a show is running.

### Difference

Titan Blind allows programming changes without affecting the current stage
output while still showing the changes in the Visualiser. Titan also exposes
record modes, tracking, block/follow-on cues, Include and shape tracking.

grandMA3 Preview isolates the programmer from live output and can preview a
sequence or cue. However, storing or updating in Preview can still affect the
live environment, especially when updating a running or tracked cue. Preview
is shared by users with the same user profile. grandMA3 also exposes tracking
distance, Cue Only, Break, Cue Parts, Content Sheet and Track Sheet.

Sources: [Titan Blind](https://manual.avolites.com/docs/13.0/cues/creating-a-cue/),
[Titan tracking](https://manual.avolites.com/docs/cue-lists/cue-list-options/),
[grandMA3 Preview](https://help.malighting.com/grandMA3/2.4/HTML/preview.html),
[grandMA3 Sequence Sheet](https://help.malighting.com/grandMA3/2.4/HTML/cue_sequence_sheet.html),
[grandMA3 Tracking Distance](https://help.malighting.com/grandMA3/2.4/HTML/cue_tracking_distance.html),
[grandMA3 Cue Only](https://help.malighting.com/grandMA3/2.4/HTML/cue_tracking_cue-only.html).

### XANAX rule

`peek` is not the same as `preview`. Peek changes what the operator sees;
Preview/Blind changes the programming environment. They must be separate
controls and separate state in LEARNING.

## 15. Cloning, multipatch, MVR and visualizer data

### Similarity

Both systems can connect patch information to a visual representation of the
rig and can reuse show data when the rig changes.

### Difference

grandMA3 Clone can propagate data throughout patch, sequences, groups,
presets, worlds and layouts. grandMA3 also has multipatch fixtures and MVR
export, with parent/child and subfixture relationships.

Titan integrates Capture Visualiser. Its Capture stage is saved with the show,
and a linked standalone Capture installation can synchronize fixture patch,
position, orientation, legend and user number with Titan.

grandMA3 additionally documents GDTF import and MVR exchange for parametric and
geometric rig data between consoles, visualizers and CAD-like tools. The Titan
sources reviewed here establish Capture as its documented visualizer path; they
do not establish a Titan equivalent to GDTF/MVR. That remains version- and
license-dependent, not a presumed absence.

Sources: [grandMA3 Clone](https://help.malighting.com/grandMA3/2.4/HTML/operate_clone.html),
[grandMA3 Patch and Multipatch](https://help.malighting.com/grandMA3/2.4/HTML/patch.html),
[grandMA3 MVR](https://help.malighting.com/grandMA3/2.4/HTML/ok_mvr.html),
[grandMA3 GDTF](https://help.malighting.com/grandMA3/2.4/HTML/ft_import_gdtf.html),
[Titan Capture show files](https://manual.avolites.com/docs/capture-visualiser/capture-show-files/),
[Titan Capture link](https://manual.avolites.com/docs/15.1/capture-visualiser/linking-the-console-to-stand-alone-capture/).

### XANAX rule

Cloning is not just copying a visible fixture button. A clone operation can
rewrite many downstream objects. It belongs to a separate, high-risk class and
must remain read-only in the learning phase.

## 16. Kill, release and active-output audit

### Similarity

Both systems let the operator find active playback material and stop or
release it.

### Difference

Titan distinguishes killing a playback from releasing its attributes. Release
can use masks and times, while LTP values may remain after a simple fader-down
operation.

grandMA3 has a Running Playbacks window and an Off Menu. These can show active
sequences, macros, timecodes, presets, timers, sound files and generators, and
can filter by object type or originating user.

Sources: [Titan playback release](https://manual.avolites.com/docs/cues/cue-playback/),
[Titan playback groups](https://manual.avolites.com/docs/13.0/cues/playback-groups/),
[grandMA3 Running Playbacks](https://help.malighting.com/grandMA3/2.4/HTML/executor_running_playbacks.html).

### XANAX rule

The canonical action must distinguish `kill`, `off`, `release`, `release_mask`
and `fade_to_zero`. Showing the same red “stop” button for all of them would
teach the wrong behavior.

## 17. New architecture consequence

The research adds two dimensions to the earlier crosswalk:

`selection_transform` and `programming_environment`.

The complete operation model is now:

`intent -> object -> selection_model -> context -> environment -> source_state -> target_route -> observed_result`

For example, “fan the pan of group 4” requires more than a target button:

- selected object and selection order/grid;
- Fan or MAtricks mode;
- pan attribute and current Programmer state;
- live, blind or preview environment;
- output ownership and expected result;
- route used to execute it;
- observed change and rollback behavior.

## 18. Media, video and Data Pools

### Similarity

Both systems can combine lighting control with visual media and can expose
media content through the operator surface.

### Difference

Titan uses Synergy to control networked Ai media servers or Prism, including
media playback, effects, uploads, screen fixtures and layers. Titan can also
use CITP active fixtures with other media servers to obtain thumbnails and
layer information.

grandMA3 stores Images, Videos, Sounds and Bitmaps in pools. Videos can be
used in appearances, bitmaps and NDI sources; sounds can be played, assigned
to executors or used in timecode. grandMA3 also separates show material into
Data Pools, with Data Pool 128 reserved for Preview.

Sources: [Titan Synergy](https://manual.avolites.com/docs/synergy/),
[Titan CITP](https://manual.avolites.com/docs/16.0/networking/using-active-fixtures/),
[grandMA3 file formats](https://help.malighting.com/grandMA3/2.4/HTML/file_formats.html),
[grandMA3 videos](https://help.malighting.com/grandMA3/2.4/HTML/videos.html),
[grandMA3 Data Pools](https://help.malighting.com/grandMA3/2.4/HTML/datapool.html).

### XANAX rule

Media actions must not be reduced to “fixture selection”. The canonical object
must identify `lighting_fixture`, `media_surface`, `media_layer`, `video`,
`bitmap`, `sound` or `data_pool`, because the same visible button can affect a
different subsystem and a different master.

## 19. Transport and external-control plane

### Similarity

Both systems can receive and send DMX through physical interfaces and through
Art-Net or sACN over Ethernet. Both can therefore be placed inside a larger
network where another console, node, visualizer or media system participates.

### Difference

The transport is not the operation itself. Titan organizes output through DMX
lines and network nodes, exposes sACN input for programming, and documents
TitanNet, Titan Remote, MIDI/SMPTE and the Titan Web API as separate control or
network paths. Titan's system and processing limits also vary with the concrete
console, PC Suite mode and TitanNet processors.

grandMA3 exposes Ethernet DMX through Art-Net and sACN, physical DMX I/O,
DMX remotes, MIDI remotes, MSC, OSC, Web Remote, PSN and Sessions. Its manual
also documents an explicit priority chain in which external inputs, tester,
parked values, programmer, playback priorities, HTP/LTP and masters can
compete. The same MIDI note or DMX level can consequently trigger an action
without owning the final output value.

This distinction is essential for XANAX. grandMA3 DMX Remotes use DMX
channels as triggers and can be configured to fire when the session's DMX
calculation changes. MIDI Remotes interpret notes, attack/decay and control
changes. PSN transports identified 3D positions. These are three different
learning targets even if all are physically received through a network or
connector.

Sources: [Titan DMX network setup](https://manual.avolites.com/docs/quick-start/dmx-network-setup/),
[Titan DMX Settings](https://manual.avolites.com/docs/next/system-settings/dmx-output-mapping/),
[Titan Remote](https://manual.avolites.com/docs/remote-control/),
[Titan network ports](https://manual.avolites.com/docs/12.0/networking/ports-used-by-titan),
[grandMA3 Ethernet DMX](https://help.malighting.com/grandMA3/2.4/HTML/dmx_ethernet.html),
[grandMA3 DMX In and Out](https://help.malighting.com/grandMA3/2.4/HTML/dmx.html),
[grandMA3 Remote In and Out](https://help.malighting.com/grandMA3/2.4/HTML/remote_inputs.html),
[grandMA3 MIDI Remotes](https://help.malighting.com/grandMA3/2.4/HTML/remote_inputs_midi.html),
[grandMA3 MSC](https://help.malighting.com/grandMA3/2.4/HTML/remote_inputs_msc.html),
[grandMA3 DMX Priorities](https://help.malighting.com/grandMA3/2.4/HTML/dmx_priorities.html).

### XANAX rule

Add a `transport_context` and `transport_kind` to every executable mapping. It must record the
route (`titan_api`, `command_line`, `osc`, `midi`, `dmx_remote`, `web_remote`,
`surface_input`), interface and port, source/target host, session/user, and
whether the transport is an `event_trigger`, `continuous_control`,
`spatial_tracking` or `output_source`, and whether it owns output priority.
The learning record must never teach “MIDI note 36 equals button 1” without
also storing the resolved semantic target and observed result.

The route priority is now explicit:

1. native semantic API, command line, plugin or object-aware remote;
2. native remote protocol;
3. accessibility/semantic UI if available;
4. calibrated visual input as the last fallback.

This does not make every route safe. API, OSC, MIDI and DMX can all reach a
live system, so XANAX still needs an arm switch, a read-only probe mode and a
reversible test show before it sends writes.

## 20. Windows surface input: visual remap versus actual click

### Similarity

Both Titan and grandMA3 run as Windows applications in the local setup, so
XANAX can place a visual projection above or beside them and learn a familiar
composition. Both can potentially be inspected through Windows UI Automation;
the answer depends on the controls actually exposed by the application.

### Difference

The overlay is only the visual layer. Windows UI Automation is semantic when a
control provider exposes patterns such as Invoke, RangeValue, Selection, Grid,
Transform or SynchronizedInput. Those patterns are not guaranteed for every
custom lighting-control surface.

If the host exposes no usable pattern, `SendInput` can synthesize mouse and
keyboard events, but that is an OS-level fallback: the target must be in the
foreground on an interactive desktop and the caller is subject to UIPI
integrity restrictions. It is not a hidden proxy that changes the host's
internal model; it is a real input event whose target and focus must be
verified.

Sources: [Windows UI Automation overview](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-uiautomationoverview),
[UI Automation control patterns](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-controlpatternsoverview),
[UI Automation control pattern interfaces](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/control-patterns-and-interfaces),
[SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput),
[Windows UI Automation input guidance](https://learn.microsoft.com/en-us/windows/apps/dev-tools/winapp-cli/ui-automation).

### XANAX rule

The route must be explicit in the mapping:

1. UIA/object-aware `Invoke`, `RangeValue`, `Selection`, `Grid` or another
   supported pattern;
2. host-native API, command line or remote protocol;
3. calibrated `SendInput`/mouse/keyboard route with foreground, focus, window
   identity and result verification;
4. no operation if the target cannot be verified.

The learned action is therefore not “click the visual button at x,y”. It is
“resolve this semantic target, confirm the capability and host context, then
send this route and observe this result”. The ON/OFF/PEEK shortcut remains
valuable, but it controls the projection and safety state; it does not itself
solve input routing.

## 21. Capability gates: software is not the same as live output

### Similarity

Both ecosystems let an operator build or inspect show data in software before
the final physical output is available. That makes a low-risk learning mode
possible: XANAX can learn object/context mappings against a test show without
connecting to fixtures.

### Difference

grandMA3 documents that onPC can preprogram and visualize without parameter
unlocking hardware, but a pure onPC system cannot output DMX until hardware
unlocks parameters. In an onPC-only configuration, the documented maximum is
4096 parameters; the software by itself contributes no parameters. Hardware
and session composition change what can be emitted and how much can be
calculated.

Titan documents a different gate. Titan PC Suite modes depend on AvoKey or
compatible hardware. Titan Go, T1/T2 and Simulator have different universe,
MIDI and timecode capabilities; the full-output Simulator mode can inject
periodic random DMX values (“spoilers”), so it is not an innocent live-output
test surface.

Sources: [grandMA3 onPC](https://help.malighting.com/grandMA3/2.4/HTML/onpc.html),
[grandMA3 parameters](https://help.malighting.com/grandMA3/2.4/HTML/system_parameter.html),
[grandMA3 parameter expansion](https://help.malighting.com/grandMA3/2.4/HTML/system_parameter_expand.html),
[grandMA3 DMX output](https://help.malighting.com/grandMA3/2.4/HTML/qsg_output_dmx.html),
[Titan Simulator](https://manual.avolites.com/docs/titan-basics/titan-simulator/),
[Titan T1/T2](https://manual.avolites.com/docs/next/about-the-consoles/t1-and-t2/),
[Titan licensing](https://manual.avolites.com/docs/system-settings/recovering-reinstalling-the-console/).

### XANAX rule

The bridge must report a capability state before it reports “available”:

`software_installed -> semantic_editing -> parameter_unlock -> dmx_output -> visualizer_access -> remote_input -> session_state`

Each edge is a fact to verify, not a guess. The learning record can be valid
in `semantic_editing` while `dmx_output` is unavailable. This is useful: the
operator can learn the mapping without risking a fixture, and the same
crosswalk can later be promoted to output only when the host reports the
required gate as open.

## 22. Local version binding

The Windows host contains the following concrete software metadata:

- Avolites Titan, Titan Go, Titan Simulator and WebAPI: file version
  `19.1.205.5`.
- grandMA3 onPC: file version `2.4.2.2`.
- grandMA3 data from `2.3.1` is also present.
- Titan show files exist under the local Documents/Titan/Shows tree in `.d4z`
  and `.d4b` forms, including autosaves and quicksaves.
- At the inventory moment, the Avolites ACN Gateway, CITP 19.1, Expert USB
  and WebAPI 19.1 services were stopped; no matching Titan/grandMA3/Capture
  process or listener on TCP 4430, 4431 or 30000 was observed.

This is an inventory fact, not a capability claim. No Titan or grandMA3
process was running during the inventory, and no AvoKey, DMX-key, viz-key,
processing unit, session master, unlocked parameter count or output status was
established. Therefore the next test is not “click the overlay”: it is a
read-only capability probe against an isolated copy of a show.

The documentation baseline remains useful, but it must be treated as
version-sensitive. The Titan Web API pages reviewed include 19.0/19.2
references while the installed Titan is 19.1; grandMA3 documentation is the
2.4 family while the installed executable is 2.4.2.2. XANAX must retain the
version in every observation so a later change cannot silently contaminate the
learning set.

## 23. Show schema and extraction boundary

The local Titan evidence makes the show schema partially inspectable without
opening the application. A `.d4z` is a ZIP container with `Show.xml`,
`CaptureShow.c2p`, fixture XML partitions and DMX/PixelMapper partitions. The
XML carries its own software/file version, desk identity, version history,
show settings, DMX lines/modules and entity pools. A current local PROMUSIC
QuickSave identifies itself as Titan `19.1.205.5`, FileVersion `313`, Desk
`Titan Editor`; the older Avolites Demo Show retains a history from older Titan
versions.

The local grandMA3 demo files are different: their first bytes are the `GMA3`
signature and they are not ZIP archives or plain text. That is enough to prove
a format boundary, not enough to claim the contents are inaccessible. The
official answer is host-native extraction: Show Creator can export selected
object types with dependencies, and Partial Show Read can inspect/import
selected parts of a show. The latter has side effects and prerequisites, so it
is not a passive binary reader. MVR/GDTF and command-line/plugin routes add
specific exchange paths, not a license to invent a general parser.

### XANAX rule

Add `show_schema` and `provenance` before semantic learning:

`container -> version -> object_schema -> dependency_graph -> semantic_operation -> surface_projection`

For Titan, a read-only structural index can start from the container. For
grandMA3, LEARNING must use Show Creator export, an official exchange format or
a controlled host session. PSR/import is reserved for an isolated copy because
it can clear Programmer or merge references. A filename extension is not an
ontology.

Sources: [grandMA3 Import/Export](https://help.malighting.com/grandMA3/2.4/HTML/import-export.html),
[grandMA3 Partial Show Read](https://help.malighting.com/grandMA3/2.4/HTML/sc_psr.html),
[grandMA3 Show Creator](https://help.malighting.com/grandMA3/2.4/HTML/show-creator.html),
[grandMA3 Features 2.4](https://help.malighting.com/grandMA3/2.4/HTML/rn_features-2-4.html),
[grandMA3 Show File Handling](https://help.malighting.com/grandMA3/2.4/HTML/show_file_management.html).

## Final engineering model

The canonical operation is:

`intent -> object -> context -> source_state -> capability_gate -> transport_kind -> transport_context -> target_route -> observed_result`

The visual layer is only a projection of that operation. It may imitate Titan
while the host is grandMA3, or the reverse, but the projection must reveal the
real target context through peek and outline. If the target has more structure,
the bridge reports `partial` or `lossy`; it does not invent equivalence.

Detailed machine-readable map: `xanax_bidirectional_crosswalk_v1.json`.
Research ledger: `work/agent-ledger/20260910-xanax-bidirectional/research.md`.
