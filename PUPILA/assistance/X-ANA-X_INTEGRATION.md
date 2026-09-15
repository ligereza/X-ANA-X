# Integración con X-ANA-X

## Superficies activas

La rama `PUPILA` de X-ANA-X integra el repositorio independiente en estas
rutas:

| PUPILA | X-ANA-X/PUPILA |
|---|---|
| `src/pupila/` | `PUPILA/assistance/src/pupila/` |
| `apps/local_assistance/` | `PUPILA/assistance/apps/local_assistance/` |
| `visual/` | `PUPILA/visual/` |

La primera ruta contiene asociación analógica y runtime temporal; la segunda
es una aplicación local de referencia; la tercera contiene geometría y
medición visual. Cada componente conserva pruebas y límites propios.

## Dirección y límites

El trabajo de dominio se conserva primero en este repositorio y luego se porta
a la rama homóloga de X-ANA-X con procedencia. Las decisiones compartidas de
X-ANA-X/core se reciben desde X-ANA-X y se propagan a las ramas consumidoras.

FARMAKSIA mantiene el experimento 090 como evidencia de investigación. El
runtime PUPILA ya no importa ni necesita ese checkout para ejecutarse. La
aplicación opcional consume una superficie LUCIDA solo cuando se le entrega
explícitamente el directorio del adaptador.

La aplicación usa eventos sintéticos y consentimiento explícito; no infiere
estados mentales, no captura cámara/teclado/puntero, no envía telemetría y no
ejecuta acciones en la aplicación anfitriona.

## Ramas

| Rama | Destino en X-ANA-X |
|---|---|
| `main` | `PUPILA/assistance/` y `PUPILA/visual/` |
| `fix/ambiguity-consistency` | cambios de asociación hacia `PUPILA/assistance/` |

Antes de portar cambios, verificar Git, ejecutar las pruebas de la superficie
afectada y conservar el commit de origen. No portar bases SQLite, capturas,
cachés, worktrees ni trazas reales.
