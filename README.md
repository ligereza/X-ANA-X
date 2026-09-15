# X-ANA-X

Repositorio base de un motor que transforma una misión de usuario entre
superficies de software.

Flujo común:

misión -> estado canónico -> relación -> propuesta -> verificador

El núcleo compartido vive en core/. Contiene operaciones canónicas,
trayectorias, firmas de live show, composición de capacidades y aprendizaje
basado en casos verificados.

Las tres ramas de trabajo son:

- PUPILA: asistencia directa dentro de una interfaz y transferencia
  analógica desde una interfaz conocida hacia otra por aprender. Su runtime
  local conserva consentimiento, vigencia y propuestas revisables; el
  prototipo usa eventos sintéticos. PUPILA/visual contiene medición geométrica
  y funciones perceptuales con evidencia de calibración explícita.
- FARMAKSIA: estudio investigativo de neurociencia, teoría del color,
  pantallas, percepción y evaluación.
- LUCIDA: integración transparente con aplicaciones. Sus superficies son
  Adobe, Resolume y Multi. El adaptador específico de Resolume vive en
  LUCIDA/resolume/adapter.

PUPILA decide qué ayuda necesita la persona. El núcleo común convierte esa
intención en una propuesta comprobable. LUCIDA la presenta o la conecta con
la aplicación anfitriona sin asumir control físico. FARMAKSIA aporta
hipótesis y evidencia; no convierte una hipótesis en una afirmación.

XIO queda como futura fuente móvil y multiusuario para LUCIDA/Multi. No forma
parte del runtime local de esta primera integración.
