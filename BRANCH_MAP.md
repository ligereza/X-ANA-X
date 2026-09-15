# Mapa de ramas e integración

## Fuente canónica

X-ANA-X es el repositorio de integración del motor común. Las fuentes
separadas aportan trabajo de dominio, pero no definen por sí solas la
arquitectura completa. El repositorio remoto es:

https://github.com/ligereza/X-ANA-X

Las ramas de trabajo son cuatro:

| Rama | Contenido | Destino funcional |
|---|---|---|
| main | Núcleo compartido sin superficie de dominio | core/ |
| PUPILA | Asistencia y representación visual/perceptual | PUPILA/assistance y PUPILA/visual |
| FARMAKSIA | Investigación, contratos, evidencia y experimentos | FARMAKSIA/farmaksia |
| LUCIDA | Integraciones de escritorio | LUCIDA/ y LUCIDA/resolume/adapter |

Cada rama contiene core/ y solamente su superficie. No se deben fusionar
ramas de dominio entre sí para “acumular” trabajo.

## Dirección del trabajo

- Trabajo sobre relaciones, trayectorias, capacidades o memoria de casos:
  rama main del motor y luego las ramas que lo consuman.
- Trabajo de asistencia o percepción: rama PUPILA.
- Trabajo de investigación: rama FARMAKSIA.
- Trabajo de integración de aplicaciones: rama LUCIDA.
- El trabajo móvil y multiusuario de XIO queda fuera de este runtime hasta
  que exista un contrato explícito.

Si un cambio nace en un repositorio separado, primero se conserva allí su
commit y después se porta como un commit identificable a la rama equivalente
de X-ANA-X. Si nace en X-ANA-X, no se replica a ciegas en el repositorio
separado: se registra su commit de origen y se decide si necesita backport.

## Regla para agentes

Al comenzar, leer README.md, IMPORT_SOURCES.md y este archivo de la rama
activa. Verificar la rama con git branch --show-current y el estado con
git status --short. No crear otra rama para una variante que ya tiene una
ruta funcional. No incluir caches, worktrees, resultados generados ni datos
privados en un commit de integración.

Un cambio está listo para portarse cuando tiene: código o contrato, prueba
ejecutable, límites declarados, commit identificable y una ruta de destino
única. La documentación histórica puede conservar su vocabulario original,
pero nunca debe usarse para inventar una nueva superficie activa.

## Validación mínima

- PUPILA association: from `PUPILA/assistance/`, `PYTHONPATH=src python -m unittest discover -s tests -v`
- PUPILA local app: from `PUPILA/assistance/`, `PYTHONPATH=src python -m unittest discover -s apps/local_assistance/tests -v`
- PUPILA visual: from `PUPILA/visual/`, `PYTHONPATH=src python -m unittest discover -s tests -v`
- Optional PUPILA→LUCIDA replay: from `PUPILA/assistance/`, `PYTHONPATH=src python -m apps.local_assistance.verify_cycle --lucida-root <LUCIDA/resolume/adapter>`
- LUCIDA/resolume/adapter: PYTHONPATH=tools:. python -m pytest -q -o addopts=
- LUCIDA compartido: PYTHONPATH=LUCIDA python -m pytest -q -o addopts= LUCIDA/tests
- core: requiere .NET disponible; si no existe, declarar la compilación como
  no verificada.
