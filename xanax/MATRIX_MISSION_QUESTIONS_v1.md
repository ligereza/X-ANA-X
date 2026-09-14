# MATRIX / XANAX — 22 preguntas del motor funcional

## Regla

Estas preguntas no buscan aprender Titan ni grandMA3 completos. Buscan definir
un motor que pueda trasladar una misión humana entre dos superficies distintas.
A y B son solamente superficies de prueba.

La unidad de aprendizaje es:

`mision -> accion abstracta -> relacion A/B -> transformacion visual -> gesto -> resultado`

Las relaciones posibles son: `common`, `reordered`, `renamed`, `split`,
`merged`, `composed`, `context_shift`, `A_only`, `B_only`, `lossy` y `unknown`.

## Misión y percepción

### OQ-001 — Invariancia de la misión

¿Cuál es la misión mínima que debe seguir funcionando aunque cambien la
versión, la resolución, la escala o el layout de la superficie?

Salida: contrato de misión independiente del software.

### OQ-002 — Reconocimiento funcional

¿Qué evidencia permite reconocer una affordance por su papel, apariencia,
posición relativa y resultado, en vez de reconocerla solo por sus píxeles?

Salida: firma perceptual de la función.

### OQ-003 — Vocabulario de gestos

¿Qué gesto mínimo necesita cada misión: click, doble click, arrastre, rueda,
tecla, combinación o secuencia?

Salida: vocabulario de entrada por misión. La primera capa será mouse y
teclado.

### OQ-004 — Relación entre superficies

Cuando una misión aparece en A y B, ¿es común, reordenada, renombrada,
dividida, fusionada, compuesta, contextual o inexistente?

Salida: clasificación de la relación, sin inventar equivalencias.

### OQ-005 — Aprender mirando la superficie real

¿Cómo puede el operador revelar la posición y el contexto real de la misión,
probarla y volver a la composición aprendida sin perder foco ni control?

Salida: contrato PEEK/ON/OFF. Decisión tomada: PEEK muestra lo real; ON/OFF
controla la proyección y el input.

## Transformación y pérdida

### OQ-006 — Condiciones de disponibilidad

¿Qué condiciones externas hacen que una misión esté disponible, bloqueada o
parcial, sin convertir esas condiciones en conocimiento que MATRIX deba
memorizar?

Salida: predicados de disponibilidad separados del aprendizaje.

### OQ-007 — Ruta de ejecución

¿Qué camino puede llevar la misión a la superficie destino conservando su
intención: control nativo, accesibilidad, superficie visual u otra ruta?

Salida: ruta elegida por misión, no una regla global por software.

### OQ-008 — Equivalencia dinámica

¿Qué propiedades deben conservarse para que dos comportamientos dinámicos
sean la misma misión: cambio, orden, fase, velocidad, tiempo, repetición,
feedback y estado final?

Salida: criterio de equivalencia para acciones animadas o temporales.

### OQ-009 — Transformación de conjuntos ordenados

¿Cómo se transforma una operación sobre una selección ordenada cuando el
destino usa otro orden, grupos, alas, bloques, grid o subelementos?

Salida: transformación abstracta de orden y estructura, sin aprender nombres
de herramientas concretas.

### OQ-010 — Separación entre explorar y afectar

¿Cómo puede una misión inspeccionarse, previsualizarse o ensayarse sin
alterar el estado live ni producir una salida no deseada?

Salida: frontera entre lectura, ensayo y ejecución.

### OQ-011 — Continuidad de identidad

¿Qué identidad y estado deben acompañar a una misión cuando el operador cambia
de superficie, objeto, contexto o representación?

Salida: identidad canónica y reglas de pérdida explícitas.

### OQ-012 — Control frente a reconstrucción

¿La misión solo necesita operar una superficie o también necesita transportar y
reconstruir información para que el resultado exista en el destino?

Salida: separación entre adaptación visual y reconstrucción estructural.

## Alcance y seguridad del motor

### OQ-013 — Evidencia mínima para ejecutar

¿Qué evidencia debe existir antes de que una relación deje de ser una
predicción y se convierta en una acción permitida?

Salida: umbral de evidencia, reversibilidad y rollback por misión.

### OQ-014 — Familias de misión

¿Las misiones de media, video, imagen, pools o contenido temporal comparten el
motor principal o requieren una familia de adaptadores separada?

Salida: límite de alcance sin contaminar el núcleo.

### OQ-015 — Naturaleza del transporte

¿Qué clase de transporte necesita cada misión: evento puntual, control
continuo, seguimiento espacial o fuente de salida?

Salida: tipo de transporte independiente del canal usado.

### OQ-016 — Puertas de capacidad

¿Qué predicados deben cumplirse para permitir observar, editar, visualizar,
ensayar o emitir una misión?

Salida: `capability_gate` por misión, con estados permitidos y bloqueados.

### OQ-017 — Misión mínima de prueba

¿Cuál es el caso más pequeño que puede demostrar que una relación A/B es
correcta o falsarla rápidamente?

Salida: benchmark mínimo, reversible y repetible.

### OQ-018 — Recinto seguro

¿Qué aislamiento, interruptor y condición de apagado impiden que una prueba
visual o de input produzca una salida física no autorizada?

Salida: sandbox operativo y kill-switch de la misión.

## Representación y aprendizaje

### OQ-019 — Estado suficiente

¿Cuál es la representación mínima del estado que MATRIX necesita para
entender y repetir una misión, sin aprender todos los datos internos del
software?

Salida: estado abstracto: intención, precondiciones, superficie, gesto,
resultado y evidencia.

### OQ-020 — Dependencias de la misión

¿Qué elementos deben viajar junto a una misión para que el resultado pueda
reproducirse, y cuáles son detalles innecesarios de la implementación?

Salida: cierre de dependencias y separación entre esencial y accesorio.

### OQ-021 — Transformación del input

¿Cómo debe transformarse el gesto cuando la superficie destino reordena,
divide, desplaza o compone la función conocida por el operador?

Salida: función `gesto_origen + contexto -> gesto_destino`, con posibilidad de
una secuencia y no solo una coordenada.

### OQ-022 — Prueba del resultado

¿Qué cambio observable confirma que la misión se ejecutó correctamente y cómo
se distingue ese resultado de un cambio visual accidental o de una acción
parcial?

Salida: feedback verificable, estado aceptado/rechazado y aprendizaje del
caso.

## Prioridad resultante

El motor debe cerrar primero OQ-001 a OQ-005, luego OQ-004, OQ-007, OQ-010,
OQ-013, OQ-017, OQ-021 y OQ-022 con misiones pequeñas. Las preguntas sobre
versiones, API, licencias, archivos y protocolos quedan como adaptadores de
ejecución, no como el contenido central del aprendizaje.
