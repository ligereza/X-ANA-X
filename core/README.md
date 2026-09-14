# XANAX + LUCIDA — Titan hacia grandMA3

Esta prueba separa LEARNING de la capa de interfaz. LEARNING permanece en MAK. Aqui XANAX traduce misiones entre superficies distintas mediante un estado canonico, modelos de comportamiento, personalidades DMX y rutas nativas. LUCIDA queda como capa superficial opcional.

## Resultado

La primera superficie usa pixeles de una referencia grandMA3, recortados y reubicados en una composicion tipo workspace Titan. No muestra dos interfaces lado a lado. El motor, sin embargo, no depende de esa superficie y trabaja sobre misiones canonicas:

- seleccionar fixtures o grupos;
- controlar atributos;
- recuperar valores reutilizables;
- leer cues y sequences;
- reconocer playbacks y executors;
- traducir operaciones con rutas nativas HTTP/OSC cuando existe evidencia suficiente;
- convertir el resultado a patches DMX normales o multi-break y codificarlo como Art-Net o sACN.

La palabra `partial` es intencional. La similitud visual no demuestra equivalencia semantica. Si la documentacion y la imagen no coinciden, la region se bloquea como `unknown`.

La traduccion funciona en ambos sentidos. No se invierten directamente las coordenadas: cada accion pasa por una intencion canonica porque Fixture, Attribute, Palette, Cue, Sequence, Playback y Executor no tienen la misma estructura en Titan y grandMA3. La ingenieria completa esta en `BIDIRECTIONAL_ENGINEERING.md`.

La superficie tambien tiene un `LUCIDA peek`: `Ctrl+Alt+L` alterna la composicion conocida con la posicion real del programa destino. El contorno conserva el mismo mapping_id y usa color para indicar si la relacion es directa, parcial, estructural, confirmada o bloqueada.

El nucleo ejecutable de XANAX esta en `XANAX.Core`. `FixturePersonality` convierte atributos canonicos a canales DMX, `GdtfProfileLoader` importa personalidades desde GDTF/`description.xml`, `PersonalityBridge` traduce entre personalidades con distinto orden y resolucion, `DerivedCapabilityRule` realiza una funcion ausente mediante una regla equivalente explicita, `MissionCatalog` convierte el crosswalk A/B en planes de mision canonicos, `BehaviorModel` describe como un programa transforma sus controles en comportamiento y `MissionBridge` traduce cambios entre modelos mediante un Jacobiano numerico con minimos cuadrados amortiguados. `CanonicalHybridRuntime` modela fades y transiciones temporales, `TrajectoryComparator` compara salidas durante un intervalo, `XanaxMissionEngine` une misión, estado, comandos y gate, y `MissionCaseStore`/`MissionRelationLearner` conservan y recuperan aprendizaje basado únicamente en evidencia observada. `XANAX.Core.Runner` contiene las verificaciones del núcleo, pero no se ejecuta automáticamente.

## Capa permitida

`grandMA3 -> recortes -> reubicacion tipo Titan -> etiqueta XANAX -> overlay LUCIDA`

El código de compilación no abre Titan ni grandMA3, no envía clicks, no usa red, no escribe showfiles y no crea plugins. El transporte nativo y la salida DMX existen como capas explícitas, pero requieren pasar por `ExecutionGate`; la proyección visual sigue siendo `pending_approval`, `proposal_only` y `reversible`.

## Evidencia usada

- Avolites Workspace Windows: https://manual.avolites.com/docs/next/titan-basics/workspace-windows/
- Avolites PC Screen Layout: https://manual.avosupport.de/docs/about-the-consoles/t3/
- grandMA3 View and Window Setup: https://help.malighting.com/grandMA3/2.3/HTML/qsg_first_view.html
- grandMA3 Sequence Sheet: https://help.malighting.com/grandMA3/2.3/HTML/cue_sequence_sheet.html
- grandMA3 Playback Cues: https://help.malighting.com/grandMA3/2.0/HTML/cue_playback.html

## Verificacion local

Desde la raiz de esta copia de LUCIDA:

```powershell
py .\xanax\run_xanax_lucida_projection.py
```

La salida esperada es `XANAX_LUCIDA_SURFACE_VERIFIED` con cinco tareas, cinco regiones sin solapamiento y ejecucion bloqueada.
