# Fuentes de integración

Este repositorio reúne tres proyecciones de un mismo motor canónico. Las
ramas no son acumulativas: cada una conserva únicamente su superficie y el
núcleo común.

- PUPILA contiene la asistencia y el motor visual/perceptual.
- FARMAKSIA contiene el estudio investigativo y sus experimentos.
- LUCIDA contiene las integraciones de escritorio y el adaptador de
  Resolume.

core/ es la base compartida: operaciones, relaciones, trayectorias, rutas
nativas y memoria de casos verificados.

Las fuentes originales y sus estados previos se conservaron fuera del
checkout en el archivo local de MAK. El árbol activo usa nombres funcionales
para evitar que un nombre histórico determine la arquitectura actual.

La proyección del repositorio independiente `ligereza/PUPILA` queda en:

- `src/pupila/` y `apps/local_assistance/` → `PUPILA/assistance/`;
- `visual/` → `PUPILA/visual/`.

El código corresponde a `ligereza/PUPILA` `main` en `4f5ff01`. No se portan
bases SQLite, cachés ni la captura de capacidades específica de un equipo.
Las notas `NEXT.md` que ya existen en la rama son heredadas, no forman parte
de la proyección PUPILA y no son contratos activos.
