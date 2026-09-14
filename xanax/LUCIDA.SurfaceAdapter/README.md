# LUCIDA Surface Adapter — primer prototipo nativo

Este ejecutable prueba la idea real: una ventana objetivo se captura, sus regiones se recomponen con logica tipo Avolites y el click puede traducirse a la posicion original.

Al iniciar permanece oculto para no bloquear Windows. Enfoca primero la ventana objetivo y presiona `Ctrl+Alt+T`; entonces aparece la superficie.

## Atajos

- `Ctrl+Alt+T`: captura la ventana que esta en primer plano como objetivo.
- `Ctrl+Alt+L`: alterna superficie compuesta y `PEEK` de la interfaz real.
- `Ctrl+Alt+A`: arma o desarma el envio de clicks. Arranca desarmado.
- `Ctrl+Alt+Q`: salir.

El prototipo no toca el codigo del host ni crea plugins. El input armado usa una traduccion de coordenadas a la ventana capturada. Antes de usarlo con grandMA3, se debe calibrar cada region con capturas reales y validar los estados parciales.

## Compilar

```powershell
dotnet build .\LUCIDA.SurfaceAdapter.csproj -c Release
```
