# XANAX — motor de transformacion visual y funcional

## Cambio de eje: la mision antes que el software

Titan y grandMA3 no son el curriculum de LEARNING. Son dos superficies de
prueba para un motor mas general: permitir que una persona opere una funcion
que conoce, aunque el programa destino la distribuya, nombre o componga de
otra manera.

La unidad que se aprende no es `Fixture`, `Patch`, `Preset` ni `Executor`.
La unidad que se aprende es una mision observable:

`mision -> accion abstracta -> transformacion de superficie -> gesto -> resultado`

Ejemplos de mision: seleccionar un conjunto, cambiar una propiedad, guardar
un estado reutilizable, disparar una escena, inspeccionar el resultado o
volver a la interfaz nativa para aprender. El software solo aporta la
superficie de origen, la superficie de destino y la evidencia del resultado.

## Ciencia de la diferencia

Para cada funcion se construyen dos conjuntos funcionales, `A` y `B`, y un
grafo de relaciones. No se busca una tabla de botones equivalentes ni una
bijeccion de nombres.

- `common`: la misma mision y el mismo efecto existen en ambos sistemas.
- `A_only` / `B_only`: la funcion existe en un sistema y no en el otro.
- `reordered`: existe, pero cambia de posicion o de orden visual.
- `renamed`: cambia el nombre, pero conserva la mision y el efecto.
- `split`: una accion de A requiere dos o mas pasos en B.
- `merged`: varias acciones de A aparecen como una sola en B.
- `composed`: no existe un control unico, pero puede reconstruirse con
  primitivas del destino.
- `context_shift`: el mismo gesto cambia de significado segun pagina, capa,
  seleccion o estado.
- `lossy`: el destino o el regreso no conserva toda la informacion.
- `unknown`: aun no existe evidencia suficiente para operar.

Formalmente, el motor no intenta `A -> B` como una sustitucion uno a uno.
Busca una relacion de mision:

`f(mision_A, contexto) -> {acciones_B, precondiciones, feedback, perdida}`

La funcion inversa se calcula por separado. Esto permite que un boton
invertido, una accion dividida en dos menus o una funcion sin equivalente
sean diferencias matematicas visibles y no errores ocultos.

Cada aprendizaje queda como un caso minimo con cinco piezas: mision,
evidencia visual, gesto esperado, transformacion necesaria y resultado
observado. LEARNING aprende relaciones entre casos; no necesita memorizar todos
los manuales internos de Titan o grandMA3.

## Regla principal

La traduccion no es una tabla de nombres ni una inversion de coordenadas. Es una traduccion en dos pasos:

`interfaz de origen -> intencion canonica -> interfaz de destino`

El camino de regreso usa la misma capa canonica:

`interfaz de destino -> intencion canonica -> interfaz de origen`

Esto evita afirmar que dos objetos son iguales solo porque ambos se llaman Fixture, Attribute, Cue o Playback.

## Capa canonica que LEARNING debe aprender

Cada mision se representa con estas dimensiones, en este orden:

1. `mission`: que resultado humano se quiere obtener.
2. `primitive`: seleccionar, modificar, guardar, recuperar, disparar, liberar,
   inspeccionar o navegar.
3. `relation`: una de las categorias `common`, `split`, `merged`, `composed`,
   `reordered`, `context_shift`, `lossy` o `unknown`.
4. `preconditions`: que debe estar seleccionado, visible o activo.
5. `source_surface` y `target_surface`: regiones, capas, orden y composicion
   visual, sin confundirlas con el significado.
6. `gesture_transform`: como se transforma click, drag, rueda, tecla o
   secuencia de gestos.
7. `state_effect`: que cambio observable debe producirse.
8. `feedback`: señal visual o de estado que confirma o rechaza la accion.
9. `loss` y `evidence`: que no se conserva y como se comprobó.

Los detalles internos del software pasan a una capa de adaptacion posterior.
Se consultan solo cuando hacen falta para ejecutar o verificar la mision.

## Diferencias que no se pueden ocultar

| Intencion | Titan conocido | grandMA3 posible | Resultado |
|---|---|---|---|
| Seleccionar un conjunto | Group o Fixture button | Group Pool, Fixture Sheet o seleccion por comando | parcial; depende de si se quiere seleccionar o solo leer |
| Editar atributo | Attribute Control y wheels | Encoder Bar, feature group, layer y encoder page | parcial; el contexto del programmer es distinto |
| Reusar valor | Palette | Preset Pool, preset object o recipe | parcial; no colapsar preset y recipe |
| Leer una secuencia | Cue List / Playback View | Sequence Sheet / Content Sheet | parcial; sequence, cue y cue part tienen otra estructura |
| Ejecutar playback | Playback handle y fader | Executor, Playback Control o sequence sin executor | parcial; no asumir que toda sequence tiene executor |
| Transformar seleccion | Fan y Align sobre orden de seleccion | MAtricks, Selection Grid, Wings, Blocks, Width, Shuffle, Invert y Transform | parcial; es una intencion distinta de editar un atributo |
| Editar sin salida live | Blind y Programmer | Preview, Programmer, Track Sheet y Content Sheet | relacionado; peek solo cambia la vista, no el entorno de programacion |
| Propagar datos de show | Patch y sincronizacion con Capture | Clone, multipatch, subfixtures, MVR, layouts y 3D | alto riesgo; read-only durante aprendizaje |
| Detener salida | Kill, Release, mascaras y tiempos | Running Playbacks, Off Menu y Off por objeto | no confundir kill, off, release y fade-to-zero |
| Extraer show schema | D4Z/Show.xml y WebAPI/Capture | Show Creator Export, dependencias, PSR, MVR/GDTF y plugins | exportar antes de importar; PSR solo en copia aislada |
| Transportar input | Titan API, TitanNet, Remote, MIDI/SMPTE y DMX | Command Line, OSC, MIDI/MSC, DMX Remote, PSN, Web Remote y Sessions | clasificar trigger, control continuo, tracking u output |
| Comprobar capacidad | AvoKey, modo Titan, universos y TNP | onPC unlock, parametros, DMX-key/viz-key, procesamiento y sesion | software instalado no significa salida habilitada |

El estado `partial` no es un error. Es la señal de que XANAX debe enseñar la diferencia antes de permitir una accion.

## Flujo bidireccional en vivo

1. Capturar la interfaz activa y determinar version, show, capability gate y anclas visuales.
2. Resolver el show schema y la identidad del objeto antes de mirar la coordenada.
3. Detectar la intencion del operador en la superficie tipo Titan o grandMA3.
4. Clasificar el input como trigger, control continuo, tracking espacial u output source.
5. Buscar la operacion destino; no buscar solo un rectangulo parecido.
6. Elegir la ruta nativa semantica antes de proyectar la superficie.
7. Proyectar el recorte de la interfaz destino en la composicion conocida.
8. Traducir click, drag, rueda o tecla solo si la operacion tiene evidencia suficiente.
9. Observar el cambio en la interfaz destino y volver a resolver el estado canonico.
10. Mostrar la respuesta en la composicion origen con perdida, capacidad y transporte visibles.

### Ruta nativa antes de la superficie

La investigacion encontro una capacidad decisiva: Titan tiene una Web API
oficial por HTTP/JSON. Puede enumerar handles, obtener `TitanId` y
`UserNumber`, leer/escribir propiedades, colocar valores en el Programmer y
controlar playbacks. grandMA3 documenta command line, OSC y plugins Lua con
APIs propias.

Por eso XANAX debe intentar, en este orden:

1. API/command line semantica del host;
2. remote protocol nativo;
3. UI Automation o accesibilidad;
4. input calibrado sobre la superficie;
5. inyeccion ciega de coordenadas: prohibida.

Fuentes: `https://api.avolites.com/19.2/`,
`https://help.malighting.com/grandMA3/2.4/HTML/remote_inputs_osc.html` y
`https://help.malighting.com/grandMA3/2.4/HTML/plugins.html`.

La inspeccion local agrego dos reglas: los `.d4z` Titan son contenedores
estructurales legibles en copia, mientras los `.show` grandMA3 tienen firma
binaria `GMA3` y deben pasar por Show Creator Export, PSR, MVR/GDTF, plugins o
la ruta del host. La presencia del ejecutable no prueba que la capacidad de
salida este abierta: los servicios Avolites estaban detenidos y no habia
listener activo durante el inventario.

## Shortcut de aprendizaje y posicion real

La superficie necesita un atajo global para alternar entre la composicion aprendida y la interfaz real del programa destino. La primera asignacion propuesta es `Ctrl+Alt+L` (`LUCIDA peek`).

- `LUCIDA ON`: se muestra la composicion conocida; el mouse usa el mapa transformado.
- `LUCIDA PEEK`: se revela la interfaz real; el mouse vuelve a coordenadas nativas y el contorno de color marca la region correspondiente.
- `LUCIDA OFF`: se devuelve el control visual y de input completamente al programa anfitrion.

El usuario puede mantener el atajo para mirar y soltarlo para regresar a la superficie aprendida. El peek no abre una segunda interfaz: revela temporalmente el origen de la misma accion.

### Codigo de contorno

- `cyan`: correspondencia directa y verificada.
- `yellow`: correspondencia parcial o ruta de mas de un menu.
- `violet`: diferencia de estructura que el usuario debe aprender.
- `red`: region desconocida, bloqueada o insegura para operar.
- `green`: accion confirmada por el resultado observado.

El mismo `mapping_id` aparece en la superficie reubicada y en la posicion real. Si el usuario esta en una funcion de Titan y activa el peek, ve exactamente donde vive la funcion en grandMA3. Ese cambio visual es evidencia de aprendizaje, no solo decoracion.

## Regla de ida y vuelta

Una traduccion solo se considera reversible si conserva:

- la intencion;
- el objeto y su identidad;
- el contexto de programacion o reproduccion;
- el estado de ejecucion;
- la informacion que el destino tiene y el origen no.

Si el destino contiene mas informacion —por ejemplo cue parts, recipes, layers o tiempos separados— el regreso a Titan debe marcar `lossy` y mostrar la diferencia. Nunca debe borrar silenciosamente esa informacion.

## Responsabilidad de cada componente

- `LEARNING` en MAK aprende los pares: intencion, diferencias, evidencia y resultado.
- `XANAX` convierte una operacion conocida en una explicacion analogica y detecta el quiebre semantico.
- `LUCIDA` compone los recortes y presenta la superficie de trabajo.
- El adaptador de input traduce coordenadas y gestos solo dentro de una ventana objetivo verificada.

La composicion visual puede parecer Titan en ambos sentidos, pero el significado siempre viene de la capa canonica y del estado real del software activo.

## Verificacion documental — 2026-09-10

Esta seccion separa lo que esta respaldado por manuales oficiales de lo que aun es una decision de diseno.

### Lo que Titan realmente organiza

- Los atributos se presentan por bancos `Intensity`, `Position`, `Colour`, `Gobo`, `Beam`, `Effect`, `Special` y `FX`; las ruedas editan los valores y el Programmer conserva el cambio.
- Un Group conserva una seleccion de fixtures y su orden; tambien puede conservar una disposicion 2D. El orden participa en shapes, fan y overlap.
- Una Palette es un valor reutilizable que las cues pueden referenciar.
- Una Cue List es una secuencia enlazada de cues. Cuando una cue, chase o cue list se almacena en un control, ese control es un Playback.

### Lo que grandMA3 realmente organiza

- La seleccion puede venir de Fixture Sheet, Group Pool, command line o Preset Pool; ademas existe seleccion jerarquica de fixture y subfixture.
- El Group conserva seleccion, orden y grid position, pero grandMA3 documenta que cues y presets no conservan por si mismos la referencia al group; Recipes si pueden usar grupos como fuente.
- El Preset no es solo una palette renombrada: tiene modos Selective, Global y Universal, feature groups y puede incorporar Recipes.
- El Encoder Bar es contextual: encoder page, channel function, layer y feature group determinan que significa el control visible.
- La Sequence vive en Sequence Pool. El Executor es un control que envia comandos a una sequence; no es la misma relacion de propiedad que Titan muestra en un Playback.
- Una cue puede contener cue parts, capas, tiempos y recipes, por lo que una fila visual no siempre representa una unidad semantica equivalente.

### Consecuencia de ingenieria

La primera version no debe intentar que cualquier boton se convierta en cualquier boton. Debe implementar un `canonical_crosswalk` con estas columnas:

`intent`, `object`, `context`, `show_schema`, `capability_gate`, `transport_kind`, `transport_context`, `source_state`, `target_route`, `loss`, `evidence`, `visual_status`, `input_status`.

La primera rebanada viva sera `selection` y luego `attribute_edit`. `reusable_value`, `cue_sequence` y `playback_control` quedan despues porque son traducciones de estructura, no simples traducciones espaciales.

El archivo ejecutable existente es solamente un scaffold tecnico de captura/composicion. No demuestra aun que un click en la superficie recombinada produzca la misma operacion en Titan o grandMA3. La habilitacion de input queda bloqueada hasta capturar y comprobar las dos ventanas reales.

Mapa detallado: `xanax_bidirectional_crosswalk_v1.json`.
Bitacora trazable: `work/agent-ledger/20260910-xanax-bidirectional/research.md`.
