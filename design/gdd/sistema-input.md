# Sistema de Input

> **Status**: In Design
> **Author**: user + agents
> **Last Updated**: 2026-07-13
> **Implements Pillar**: Infraestructura (habilita toda interacción del jugador con el mundo)

## Overview

El Sistema de Input es la capa de abstracción entre los dispositivos físicos del jugador (teclado/ratón, gamepad) y las acciones lógicas del juego. Traduce entradas crudas de hardware en **acciones semánticas** ("Mover", "Atacar", "Interactuar", "Esquivar") que el resto de sistemas consumen sin conocer qué tecla o botón las disparó. Construido sobre el Input System de Unity 6 (el `Input` legacy está deprecado), organiza estas acciones en **contextos conmutables** (action maps): Exploración, Combate, Diálogo y Menú/UI — de modo que la misma tecla puede significar cosas distintas según lo que el jugador esté haciendo, y los inputs de un contexto no se filtran a otro (requisito establecido en ADR-0001). Soporta teclado/ratón como esquema primario y gamepad de forma parcial, con auto-conmutación al detectar cambio de dispositivo, y permite **remapear controles en runtime** con persistencia. A nivel de jugador, el sistema es invisible cuando funciona: su efecto es un control responsivo y personalizable — la certeza de que pulsar algo produce la acción esperada, de inmediato y sin ambigüedad. Es Foundation: todo sistema con el que el jugador interactúa (Movimiento, Combate, Cámara, Diálogos, Menús) depende de él.

> **Nota de alcance**: la arquitectura concreta de la capa de abstracción (clase generada suscrita manualmente vs. componente `PlayerInput`, estructura de eventos) es una decisión de implementación que irá a un ADR, no a este GDD. Este documento define *qué* acciones y contextos existen y *cómo se comportan*; el ADR describirá *cómo* se implementa la capa.

## Player Fantasy

Sistema de infraestructura: el jugador no interactúa con él como un "sistema", sino a través de él con todo lo demás. La fantasía relevante es la **transparencia del control** — el input nunca debe llamar la atención sobre sí mismo. Un buen sistema de input se siente como una extensión directa de la intención del jugador: pulsas y ocurre, sin latencia perceptible, sin ambigüedad sobre qué hará una tecla en cada momento (por eso los contextos conmutables importan — atacar en combate, avanzar diálogo cuando hablas). La única fantasía "positiva" que el sistema encarna directamente es la **agencia sobre los propios controles**: poder remapear teclas y ajustar sensibilidad para que el juego se adapte al jugador, no al revés. Cumple su fantasía siendo responsivo, predecible e invisible.

*(Nota: `creative-director` no consultado — modo Lean, y el sistema es infraestructura sin fantasía directa. La sensación de control se materializa en Movimiento, Combate y Cámara, no aquí.)*

## Detailed Design

### Core Rules

1. **Acciones semánticas, no teclas.** Todo sistema consume *acciones* con nombre ("Mover", "Atacar", "Esquivar", "Interactuar"), nunca teclas o botones directos. El binding tecla→acción vive solo en el Input Actions Asset; ningún otro sistema conoce el hardware.

2. **Tres action maps (contextos).** Las acciones se agrupan en tres mapas conmutables:
   - **Gameplay**: Mover (Vector2), Mirar (Vector2), Saltar, Correr, Agacharse, Atacar, Esquivar, Interactuar, Cambiar arma/objeto, Abrir menú, Abrir mapa. Exploración y combate comparten este mapa — atacar está siempre disponible (no hay un "modo combate" que lo habilite/deshabilite).
   - **Diálogo**: Avanzar, Elegir opción (Vector2/navegación), Saltar diálogo, Cerrar. Suspende Gameplay.
   - **MenúUI**: Navegar (Vector2), Confirmar, Cancelar/Atrás, Pestaña siguiente/anterior. Suspende lo que haya debajo.

   > **Reconciliación con ADR-0001**: el ADR nombra cuatro contextos (exploración, combate, diálogo, menú). Este diseño los mapea a **tres** action maps: "exploración" y "combate" son el mismo mapa Gameplay, porque el juego es de acción y no existe un estado no-combativo donde atacar esté bloqueado. La intención de ADR-0001 (que los inputs de gameplay no se filtren a diálogo/menú y viceversa) se cumple íntegramente.

3. **Pila de contextos (stack).** Solo el mapa en la cima de la pila recibe input. Abrir diálogo o menú **empuja** su mapa a la pila (suspendiendo el de abajo); cerrarlo lo **saca** y restaura automáticamente el anterior. Esto maneja anidamiento (ej. menú abierto desde dentro de un diálogo) sin lógica manual de "volver a dónde".

4. **Un solo mapa activo a la vez.** Aunque la pila puede tener varios mapas, solo la cima está habilitada; los de abajo están suspendidos (no reciben ni buffean input). Regla invariante: nunca hay dos mapas procesando input simultáneamente.

5. **Buffer de input corto para acciones de gameplay.** Las acciones discretas de Gameplay (Atacar, Esquivar, Saltar) hechas dentro de una ventana `T_buffer` *antes* de que la acción esté disponible (por cooldown o animación en curso) se recuerdan y se ejecutan al abrirse la ventana. Las acciones continuas (Mover, Mirar) no se buffean — se leen por valor cada frame. El buffer se descarta si expira `T_buffer` sin que la acción se vuelva disponible.

6. **Esquemas de control con auto-conmutación.** Dos esquemas: **KB&M** (primario) y **Gamepad** (parcial). El sistema detecta qué dispositivo generó el último input y conmuta el esquema activo — esto afecta qué prompts se muestran (ver UI Requirements), no la lógica de acciones. Sin soporte touch.

7. **Rebinding en runtime con persistencia.** El jugador puede remapear cualquier acción de Gameplay y MenúUI (Diálogo hereda de MenúUI). Los overrides se guardan como JSON en configuración de dispositivo (`PlayerPrefs`), **no** en el save canónico de partida — son ajustes del jugador, no estado de partida. Al arrancar, los overrides se cargan antes de habilitar cualquier mapa.

8. **Sin input durante transiciones bloqueantes.** Mientras una carga de escena, un guardado síncrono de quit, o una cinemática no-interactiva están activos, ningún mapa recibe input (se empuja un mapa vacío o se deshabilita la pila).

### States and Transitions

| Estado (contexto activo) | Condición de Entrada | Condición de Salida | Comportamiento |
|--------------------------|----------------------|----------------------|----------------|
| **Gameplay** | Estado base tras cargar partida; o al cerrar diálogo/menú y quedar en el fondo de la pila | Se abre diálogo (→ push Diálogo), se abre menú (→ push MenúUI), o carga/cinemática (→ Suspendido) | Procesa mover, mirar, atacar, esquivar, saltar, etc. Buffer de gameplay activo |
| **Diálogo** | Push al iniciar una conversación | Push MenúUI (menú desde diálogo), o pop al terminar la conversación (→ restaura Gameplay) | Procesa avanzar/elegir/saltar/cerrar. Gameplay suspendido debajo |
| **MenúUI** | Push al abrir cualquier menú/pantalla | Pop al cerrar (→ restaura el mapa de debajo) | Procesa navegar/confirmar/cancelar. Todo lo de debajo suspendido |
| **Suspendido** | Carga de escena, guardado de quit, cinemática no-interactiva | Fin de la operación bloqueante (→ restaura la cima previa de la pila) | Ningún input se procesa ni buffea |
| **Rebinding** | El jugador inicia un remapeo interactivo desde MenúUI | Rebind completado o cancelado (→ vuelve a MenúUI) | Captura la siguiente pulsación como nuevo binding; ignora la acción normal de esa tecla |

### Interactions with Other Systems

| Sistema | Dirección | Interfaz |
|---------|-----------|----------|
| **Movimiento y Exploración** (Foundation, no diseñado) | Input → Movimiento | Provee las acciones `Mover` (Vector2), `Saltar`, `Correr`, `Agacharse` |
| **Sistema de Cámara** (Foundation, no diseñado) | Input → Cámara | Provee `Mirar` (Vector2, delta de ratón / stick derecho) |
| **Combate** (Core, no diseñado; ADR-0001) | Input → Combate | Provee `Atacar`, `Esquivar` como acciones discretas (con buffer); Combate NO lee dispositivos directamente |
| **Diálogos** (Feature, no diseñado) | Input ↔ Diálogos | Diálogos hace push del mapa Diálogo al iniciar y pop al terminar; consume `Avanzar`/`ElegirOpción`/`Saltar` |
| **Menús y UI** (Presentation, no diseñado) | Input ↔ Menús | Menús hace push/pop del mapa MenúUI; consume navegación. También aloja la pantalla de rebinding |
| **Sistema de Guardado/Persistencia** (Foundation, diseñado) | Input → PlayerPrefs (NO save) | Los rebindings persisten vía `PlayerPrefs` como settings de dispositivo, **no** vía el save canónico. Límite explícito: el save de partida no incluye configuración de controles |
| **Gestión de Escenas/Niveles** (Core, no diseñado) | Escenas → Input | Durante transiciones de escena, Escenas pone la pila en Suspendido |

**Nota de límite diseño/implementación**: la estructura de clases de la capa de abstracción (clase generada + wrapper, o componente `PlayerInput`), y el mecanismo exacto de la pila de contextos, son decisiones de implementación → ADR. El GDD fija las acciones, los tres mapas, la semántica de pila y el comportamiento de buffer/rebinding.

## Formulas

### F1. Ventana de buffer de input (`T_buffer`)

**Regla.** Cuando el jugador pulsa Atacar, Esquivar o Saltar y la acción **no** está disponible (cooldown activo, animación bloqueante en curso), la pulsación se guarda con una marca de tiempo. Si la acción se vuelve disponible dentro de `T_buffer` desde esa marca, se ejecuta inmediatamente al abrirse la ventana. Si `T_buffer` expira antes, el buffer se descarta sin efecto.

```
buffered = (t_pulsación + T_buffer) >= t_actual   → mientras t_actual < t_disponible
al llegar t_actual == t_disponible:
    si buffered aún vigente → ejecutar acción, limpiar buffer
    si no → no-op
```

- Solo 1 slot de buffer por acción (Atacar, Esquivar, Saltar cada una con su propio slot independiente). Una nueva pulsación de la misma acción **sobrescribe** la anterior (no se acumulan inputs).
- El buffer se limpia inmediatamente al cambiar de action map (push/pop de la pila) — no sobrevive a un cambio de contexto.

| Símbolo | Tipo | Rango | Descripción |
|---|---|---|---|
| `T_buffer` | float (segundos) | 0.08 – 0.20 | Ventana de anticipación antes de que la acción esté disponible |
| `t_pulsación` | float (segundos, timestamp) | — | Momento en que el jugador presionó el botón |
| `t_disponible` | float (segundos, timestamp) | — | Momento en que cooldown/animación libera la acción |

**Output range:** acción ejecutada en `[0, T_buffer]` segundos después de `t_disponible`, o descartada.

**Valor propuesto:** `T_buffer = 0.15s` (150ms) para Atacar y Esquivar; `T_buffer = 0.10s` (100ms) para Saltar (más corto porque saltar rara vez está bloqueado por cooldown, solo por no estar grounded — un buffer largo aquí se siente "flotante").

**Ejemplo:** el jugador pulsa Atacar en `t=1.000s` mientras `_meleeCooldownTimer` (código actual, `MeleeCooldown=0.5f`) aún tiene 0.08s restantes → disponible en `t=1.080s`. Como `1.080 - 1.000 = 0.080s ≤ T_buffer(0.15s)`, el ataque bufferizado se dispara en `t=1.080s`. Si en cambio el cooldown restante fuera 0.20s (disponible en `t=1.200s`), `0.200 > 0.15s` → el buffer expira en `t=1.150s` y la pulsación se descarta; el jugador tendría que volver a pulsar.

---

### F2. Dead zone radial de stick (gamepad)

**Regla.** El input crudo del stick (`raw`, componentes X/Y en rango [-1,1]) se trata como vector, no por eje, para evitar que el drift diagonal se cuele por un eje mientras el otro está en cero. Por debajo del umbral `DZ_inner` el input se anula por completo; por encima de `DZ_outer` se satura a magnitud 1; entre ambos se re-normaliza linealmente para que no haya salto brusco al cruzar el borde de la zona muerta.

```
mag_raw = |raw|                                    (magnitud del vector crudo, 0..~1.0)

si mag_raw <= DZ_inner:
    output = (0, 0)
si no:
    mag_clamped = min(mag_raw, DZ_outer)
    mag_norm = (mag_clamped - DZ_inner) / (DZ_outer - DZ_inner)     // 0..1
    output = normalize(raw) * mag_norm
```

| Símbolo | Tipo | Rango | Descripción |
|---|---|---|---|
| `raw` | Vector2 | magnitud 0..~1.0 | Input crudo del stick (X, Y) |
| `DZ_inner` | float | 0.10 – 0.20 | Umbral interno: por debajo, drift ignorado |
| `DZ_outer` | float | 0.90 – 0.98 | Umbral externo: por encima, saturado a magnitud 1 |
| `output` | Vector2 | magnitud 0..1.0 | Input normalizado listo para consumir por Movimiento/Cámara |

**Output range:** `|output| ∈ [0, 1]`, continuo, sin discontinuidad en `DZ_inner` ni en `DZ_outer`.

**Valores propuestos:** `DZ_inner = 0.15`, `DZ_outer = 0.95` (deja margen para sticks desgastados que no llegan a 1.0 físico).

**Ejemplo (Mover, stick izquierdo):** stick desviado a `raw=(0.10, 0.05)` → `mag_raw ≈ 0.112 < 0.15` → `output=(0,0)` (drift ignorado). Con `raw=(0.30, 0.0)` → `mag_raw=0.30` → `mag_norm=(0.30-0.15)/(0.95-0.15)=0.1875` → `output=(0.1875, 0)`, un input suave y proporcional en vez de saltar de 0 a 0.30 de golpe.

---

### F3. Sensibilidad y curva de respuesta de Mirar

**F3a — Ratón (lineal, frame-accumulated).** El delta de ratón por frame (`mouse.delta`, ya acumulado por Unity para ese frame) se escala linealmente. **No** se multiplica por `Time.deltaTime` — ya representa el movimiento del frame, no una velocidad continua.

```
rotY_delta = mouseDelta.x * SensMouse * (InvertY ? 1 : 1)      // yaw, no se invierte
rotX_delta = mouseDelta.y * SensMouse * (InvertY ? 1 : -1)     // pitch
```

Manteniendo la convención actual del código (`_xRotation -= mouseY`): con `InvertY=false`, `rotX_delta = -mouseDelta.y * SensMouse`; con `InvertY=true`, `rotX_delta = +mouseDelta.y * SensMouse`.

| Símbolo | Tipo | Rango | Descripción |
|---|---|---|---|
| `mouseDelta` | Vector2 (px) | típico -50..50/frame | Delta de ratón del frame, ya provisto por el Input System |
| `SensMouse` | float | 0.5 – 5.0 | Multiplicador de sensibilidad de ratón (reemplaza `mouseSensitivity=2f` actual) |
| `InvertY` | bool | true/false | Invierte el eje de pitch (mirar arriba/abajo) |

**Output range:** grados de rotación por frame; con clamp de pitch existente en código (`Mathf.Clamp(_xRotation, -80f, 80f)`) — se mantiene.

**Ejemplo:** `mouseDelta=(8, -3)` px, `SensMouse=2.0`, `InvertY=false` → `rotY_delta = 8*2.0 = 16°`, `rotX_delta = -(-3)*2.0 = 6°` (mirar hacia arriba).

**F3b — Stick derecho (velocidad angular, frame-rate independent, con curva opcional).** El stick representa una *velocidad* de giro, no un delta absoluto, así que aquí sí se multiplica por `Time.deltaTime`. Se aplica primero la dead zone radial (F2) y luego una curva de respuesta exponencial opcional para dar precisión en desviaciones pequeñas y velocidad alta en desviaciones grandes.

```
stick_dz = DeadZoneRadial(stickRaw)             // F2, magnitud 0..1
response = sign(stick_dz) * |stick_dz| ^ CurveExp   // por componente, o aplicar sobre magnitud y reproyectar

rotY_delta = response.x * SensStick * Time.deltaTime
rotX_delta = response.y * SensStick * Time.deltaTime * (InvertY ? 1 : -1)
```

| Símbolo | Tipo | Rango | Descripción |
|---|---|---|---|
| `SensStick` | float (°/s) | 90 – 360 | Velocidad angular máxima a deflexión completa |
| `CurveExp` | float | 1.0 – 3.0 | Exponente de la curva; `1.0` = lineal, `>1.0` = más precisión cerca del centro, aceleración hacia el borde |
| `Time.deltaTime` | float (s) | ~0.0166 a 60fps | Factor de independencia de framerate |

**Output range:** velocidad angular efectiva `0..SensStick °/s`, aplicada como `°/frame` tras multiplicar por `Time.deltaTime`.

**Valores propuestos:** `SensStick = 180°/s`, `CurveExp = 2.0` (curva cuadrática — sensación estándar en shooters/ARPGs de consola).

**Ejemplo:** stick desviado a magnitud `0.5` tras dead zone, `CurveExp=2.0` → `response = 0.5^2 = 0.25` (25% de velocidad, no 50% — más control fino en la mitad del recorrido). Con `SensStick=180°/s` y `Time.deltaTime=0.0166s` (60fps): `rotY_delta = 0.25 * 180 * 0.0166 ≈ 0.747°` ese frame. A deflexión completa (`response=1.0`): `rotY_delta = 180 * 0.0166 ≈ 2.99°/frame` ≈ giro completo de 360° en 2s.

## Edge Cases

- **Si un gamepad se desconecta mientras es el esquema activo**: el juego pausa inmediatamente (push del mapa MenúUI con un aviso "Reconecta el mando") para evitar que el jugador reciba daño sin control. Se reanuda al reconectar el mando o al usar teclado/ratón (que conmuta el esquema a KB&M). Si el esquema activo ya era KB&M, la desconexión del gamepad se ignora.
- **Si el jugador remapea una acción a una tecla ya usada por otra acción del mismo mapa**: se detecta la colisión y se ofrece "Intercambiar / Sobrescribir / Cancelar". No se permite dejar una acción sin binding por un descuido; si el jugador elige Sobrescribir y eso deja otra acción sin tecla, se marca esa acción como "sin asignar" con aviso visible.
- **Si se abre un menú/diálogo (push de contexto) mientras una acción de gameplay está pulsada**: al suspender el mapa Gameplay se envía un `cancel` a todas sus acciones activas (equivalente a soltar el botón). El personaje no queda "corriendo pegado" ni atacando bajo el menú. Al hacer pop y restaurar Gameplay, el jugador debe volver a pulsar para reactivar cualquier acción continua.
- **Si el jugador pulsa una acción de gameplay bufferizada y antes de que se libere la ventana se cambia de contexto** (push de diálogo/menú): el buffer se descarta (Core Rule 5) — la acción bufferizada no se ejecuta al volver a Gameplay. Un input de combate no debe dispararse "tarde" tras cerrar un menú.
- **Si dos acciones distintas se disparan en el mismo frame** (ej. Atacar y Esquivar simultáneos): ambas se entregan a sus sistemas consumidores en el mismo frame; el Sistema de Input no impone prioridad — la resolución de "qué gana" es responsabilidad del sistema consumidor (Combate decide si Esquivar cancela Atacar). Input solo garantiza que ambas acciones llegan.
- **Si el jugador conecta un gamepad por primera vez a mitad de partida**: el sistema lo detecta como dispositivo disponible pero **no** conmuta el esquema hasta que el jugador genere input desde él (evita conmutar por un mando que solo se enchufó para cargar). El primer input del gamepad conmuta el esquema y actualiza los prompts.
- **Si los rebindings guardados en `PlayerPrefs` están corruptos o referencian una tecla/dispositivo inexistente al cargar**: se descartan los overrides corruptos y se restauran los bindings por defecto de esas acciones (no se bloquea el arranque). Se avisa al jugador de que la configuración de controles se restableció.
- **Si el jugador remapea una acción a una tecla reservada del sistema** (ej. la tecla de pausa/menú, o teclas del OS): el rebinding rechaza esas teclas con un aviso ("Esta tecla está reservada"), manteniendo el binding anterior. La lista de teclas reservadas es un tuning knob.
- **Si llega input de un dispositivo no soportado** (ej. un volante, un segundo gamepad): se ignora silenciosamente. El juego es single-player con un solo jugador activo; no hay soporte multi-dispositivo simultáneo en el MVP.

## Dependencies

| Sistema | Dirección | Tipo | Interfaz / Nota |
|---------|-----------|------|-------------------|
| — (ninguna) | Upstream | — | Input es Foundation, no depende de ningún sistema para existir |
| **Movimiento y Exploración** | Downstream | Duro (para Movimiento) | Consume `Mover`, `Saltar`, `Correr`, `Agacharse` |
| **Sistema de Cámara** | Downstream | Duro (para Cámara) | Consume `Mirar` (con sensibilidad/curva de F3) |
| **Combate** | Downstream | Duro (para Combate) | Consume `Atacar`, `Esquivar` (con buffer F1). ADR-0001 exige que combate no lea dispositivos directamente |
| **Diálogos** | Downstream | Duro (mutuo) | Diálogos hace push/pop del mapa Diálogo; consume navegación de diálogo. Sin Input no hay avance de diálogo; sin el push de Diálogos, el mapa no se activa |
| **Menús y UI** | Downstream | Duro (mutuo) | Menús hace push/pop del mapa MenúUI y aloja la pantalla de rebinding; consume navegación de UI |
| **Gestión de Escenas/Niveles** | Downstream | Duro (para la suspensión) | Escenas pone la pila de Input en Suspendido durante transiciones |
| **Sistema de Guardado/Persistencia** | Lateral | Blando (límite explícito) | Los rebindings **NO** usan el save canónico — persisten en `PlayerPrefs` (settings de dispositivo). Se documenta como frontera: Guardado no serializa configuración de controles |

**Nota de dureza asimétrica**: desde la perspectiva de *Input*, sus dependencias son **blandas** — el sistema funciona (lee dispositivos, mantiene la pila) aunque los consumidores no existan aún. Desde la perspectiva de *cada consumidor*, la dependencia de Input es **dura** — sin acciones semánticas no hay interacción.

**Nota de bidireccionalidad**: de los downstream, solo **Sistema de Guardado/Persistencia** tiene GDD — y la relación es un *límite* deliberado (Guardado no toca rebindings), ya coherente con `sistema-guardado.md` (que serializa estado de partida, no settings). El resto aún no tiene GDD; cuando se diseñen, deben listar "depende de Sistema de Input" y consumir acciones semánticas, nunca dispositivos.

## Tuning Knobs

| Knob | Rango seguro | Default | Qué rompe en los extremos |
|------|--------------|---------|----------------------------|
| `T_buffer` (Atacar/Esquivar) | 0.08 – 0.20 s | 0.15 s | Por debajo de 0.08s: inputs "perdidos" en transiciones de animación (se siente poco responsivo). Por encima de 0.20s: acciones "fantasma" que se disparan tarde y sorprenden al jugador |
| `T_buffer` (Saltar) | 0.06 – 0.15 s | 0.10 s | Más corto que Atacar/Esquivar a propósito — un buffer largo de salto se siente "flotante" |
| `DZ_inner` (dead zone interna del stick) | 0.10 – 0.20 | 0.15 | Por debajo de 0.10: el drift del stick mueve al personaje solo. Por encima de 0.20: se pierde recorrido útil, el stick se siente "muerto" cerca del centro |
| `DZ_outer` (saturación externa del stick) | 0.90 – 0.98 | 0.95 | Por debajo de 0.90: el jugador no alcanza velocidad máxima. En 1.0: sticks desgastados que no llegan al máximo físico nunca saturan |
| `SensMouse` (sensibilidad de ratón) | 0.5 – 5.0 | 2.0 | Ajuste de jugador — extremos solo afectan comodidad individual, no rompen el sistema. Reemplaza el `mouseSensitivity=2` fijo del código |
| `SensStick` (velocidad angular del stick) | 90 – 360 °/s | 180 °/s | Por debajo de 90: girar la cámara se siente lento/pesado. Por encima de 360: incontrolable, imposible apuntar fino |
| `CurveExp` (curva de respuesta del stick) | 1.0 – 3.0 | 2.0 | 1.0 = lineal (sin precisión fina cerca del centro). Por encima de 3.0: el stick se siente inerte hasta casi el borde |
| `InvertY` (invertir eje de pitch) | true/false | false | Preferencia de jugador, sin efecto sistémico |
| Lista de teclas reservadas (no remapeables) | conjunto configurable | {pausa/menú, teclas del OS} | Demasiadas: el jugador no puede remapear libremente. Muy pocas: el jugador puede romper el acceso al menú de pausa dejándolo sin tecla |

**Interacciones entre knobs**: `SensStick` y `CurveExp` se tunean juntos — subir la curva reduce la velocidad efectiva en el rango medio, así que un `CurveExp` alto suele acompañarse de un `SensStick` algo mayor para compensar. `DZ_inner` y `DZ_outer` definen juntos el rango útil del stick: si se acercan demasiado, el stick pierde resolución (salta de cero a máximo en poco recorrido).

Nota: todos estos knobs son ajustes de jugador o de diseño (data-driven). El binding tecla→acción concreto vive en el Input Actions Asset, no aquí — el asset es la fuente de verdad de los bindings por defecto.

## Visual/Audio Requirements

> Sistema de infraestructura: superficie audiovisual mínima, centrada en comunicar el estado del input. No requiere Art Bible.

- **Prompts de botón contextuales**: los íconos/etiquetas de acción mostrados en el mundo y la UI (ej. "[E] Interactuar", "[F] Atacar") deben reflejar el esquema activo — glifos de teclado cuando el esquema es KB&M, glifos de gamepad cuando es Gamepad. Cambian en caliente al conmutar de dispositivo (Core Rule 6). El set de glifos de gamepad debe ser genérico (ABXY / caras) o adaptarse a la marca detectada si es viable — decisión de producción, no bloqueante.
- **Feedback de rebinding**: durante el estado Rebinding, la UI muestra claramente "Pulsa una tecla…" y, al capturar, confirma la nueva asignación. Un rebinding rechazado (tecla reservada o conflicto) muestra el aviso correspondiente (ver Edge Cases).
- **Aviso de reconexión de mando**: al desconectarse el gamepad activo, el overlay de pausa muestra "Reconecta el mando" (ver Edge Cases).
- **Audio**: ninguno propio del sistema de input. Los sonidos de confirmación/navegación de menús pertenecen a Menús y UI, no a Input.

## UI Requirements

- **Pantalla de configuración de controles** (dentro del menú de opciones): lista las acciones remapeables por mapa (Gameplay, MenúUI), muestra el binding actual de cada una, y permite iniciar un rebinding interactivo por acción. Incluye botón "Restaurar valores por defecto".
- **Sliders de sensibilidad y toggles**: `SensMouse`, `SensStick`, `CurveExp`, `InvertY` expuestos como controles ajustables con sus rangos de tuning (ver Tuning Knobs). Los cambios se aplican en vivo para que el jugador los pruebe.
- **Diálogo de conflicto de rebinding**: "Intercambiar / Sobrescribir / Cancelar" cuando una tecla ya está en uso (Edge Cases).
- **Indicador de acción sin asignar**: si una acción queda sin binding, se marca visualmente en la lista con un aviso.
- **Overlay de "Reconecta el mando"**: modal de pausa ante desconexión de gamepad.
- Toda esta UI la implementa **Menús y UI** (Presentation, no diseñado); Input expone los datos (lista de acciones, bindings actuales, estado de rebinding) que la UI consume.

> 📌 **UX Flag — Sistema de Input**: Este sistema tiene requisitos de UI reales (pantalla de controles/rebinding + sliders de sensibilidad). En Fase 4 (Pre-Producción), correr `/ux-design` para especificar esta pantalla antes de escribir epics. Las historias que la referencien deben citar `design/ux/controles.md`, no este GDD directamente.

## Acceptance Criteria

### Core Rules

**AC-01 — Acciones semánticas, no teclas**
GIVEN cualquier sistema consumidor (Movimiento, Cámara, Combate, Diálogos, Menús) suscrito al Sistema de Input, WHEN se inspecciona su código/API de consumo, THEN no referencia teclas ni botones directos (`KeyCode`, `GamepadButton`, etc.) — solo nombres de acciones ("Mover", "Atacar", "Esquivar", "Interactuar", etc.).

**AC-02 — Tres action maps**
GIVEN el Input Actions Asset cargado, WHEN se listan los action maps, THEN existen exactamente tres: Gameplay (Mover, Mirar, Saltar, Correr, Agacharse, Atacar, Esquivar, Interactuar, Cambiar arma/objeto, Abrir menú, Abrir mapa), Diálogo (Avanzar, Elegir opción, Saltar diálogo, Cerrar), MenúUI (Navegar, Confirmar, Cancelar/Atrás, Pestaña siguiente/anterior).

**AC-03 — Atacar siempre disponible en Gameplay**
GIVEN el jugador está en el mapa Gameplay (exploración, sin diálogo/menú abierto), WHEN se pulsa Atacar, THEN la acción se procesa sin necesidad de un "modo combate" previo.

**AC-04 — Pila push/pop**
GIVEN el mapa Gameplay activo, WHEN se abre un diálogo y luego, desde dentro de ese diálogo, se abre un menú, THEN la pila queda [Gameplay, Diálogo, MenúUI]; WHEN se cierra el menú, THEN la pila vuelve a [Gameplay, Diálogo] y Diálogo recibe input; WHEN se cierra el diálogo, THEN la pila vuelve a [Gameplay] y Gameplay recibe input.

**AC-05 — Un solo mapa activo**
GIVEN cualquier estado de la pila con 2 o más mapas apilados, WHEN se envía un input que correspondería a una acción de un mapa que no está en la cima, THEN esa acción no se dispara ni se buferiza (verificable: el sistema consumidor de ese mapa inferior no recibe el evento).

**AC-06 — Buffer solo en acciones discretas de Gameplay**
GIVEN el mapa Gameplay activo, WHEN se lee Mover o Mirar, THEN nunca generan buffer — se leen por valor cada frame sin memoria de pulsaciones pasadas.

**AC-07 — Auto-conmutación de esquema**
GIVEN el esquema activo es KB&M, WHEN el jugador genera el primer input desde un gamepad ya conectado, THEN el esquema activo conmuta a Gamepad y los prompts de UI cambian a glifos de gamepad en el mismo frame o el siguiente frame de render.

**AC-08 — Rebinding con persistencia en PlayerPrefs**
GIVEN el jugador remapea "Atacar" de la tecla A a la tecla B, WHEN se cierra y reinicia la aplicación (sin tocar el save de partida), THEN "Atacar" sigue vinculada a B; AND el save canónico de partida no contiene esa información (solo aparece en `PlayerPrefs`).

**AC-09 — Sin input en transiciones bloqueantes**
GIVEN una carga de escena, un guardado síncrono de quit, o una cinemática no-interactiva en curso, WHEN el jugador pulsa cualquier tecla/botón mapeado, THEN ninguna acción se ejecuta ni se buferiza durante ese intervalo.

### Formulas

**AC-10 — F1: buffer dentro de ventana (Atacar/Esquivar, 0.15s)**
GIVEN Atacar en cooldown con `t_disponible = t_pulsación + 0.08s` (≤0.15s), WHEN el jugador pulsa Atacar en `t_pulsación`, THEN el ataque se ejecuta en `t_disponible` (tolerancia ±1 frame a 60fps ≈ ±16.6ms).

**AC-11 — F1: buffer expira fuera de ventana (0.15s)**
GIVEN Atacar en cooldown con `t_disponible = t_pulsación + 0.20s` (>0.15s), WHEN el jugador pulsa Atacar y no vuelve a pulsar, THEN el ataque NO se ejecuta al llegar `t_disponible` (buffer descartado en `t_pulsación + 0.15s`).

**AC-12 — F1: buffer Saltar (0.10s)**
GIVEN Saltar no disponible con disponibilidad en `t_pulsación + 0.09s` (≤0.10s), WHEN el jugador pulsa Saltar, THEN el salto se ejecuta al volverse grounded; AND con disponibilidad en `t_pulsación + 0.12s` (>0.10s), el salto NO se ejecuta.

**AC-13 — F1: un solo slot por acción, sobrescritura**
GIVEN Atacar bufferizado en `t=1.000s` (no disponible), WHEN el jugador pulsa Atacar de nuevo en `t=1.050s` antes de que esté disponible, THEN solo se conserva la marca de `t=1.050s` (no se acumulan 2 ataques).

**AC-14 — F1: buffer independiente entre Atacar/Esquivar/Saltar**
GIVEN Atacar bufferizado y no disponible, WHEN el jugador pulsa Esquivar y Esquivar sí está disponible, THEN Esquivar se ejecuta inmediatamente sin afectar ni consumir el buffer de Atacar.

**AC-15 — F2: dead zone interna (drift ignorado, DZ_inner=0.15)**
GIVEN un stick con `raw=(0.10, 0.05)` (magnitud ≈0.112 < 0.15), WHEN se lee Mover, THEN `output=(0,0)` exacto.

**AC-16 — F2: renormalización lineal**
GIVEN un stick con `raw=(0.30, 0.0)`, WHEN se aplica la dead zone, THEN `output ≈ (0.1875, 0)` (tolerancia ±0.01), calculado como `(0.30-0.15)/(0.95-0.15)`.

**AC-17 — F2: saturación externa (DZ_outer=0.95)**
GIVEN un stick con magnitud `raw ≥ 0.95`, WHEN se lee el input, THEN `|output| = 1.0` exacto (saturado).

**AC-18 — F2: continuidad sin salto en los bordes**
GIVEN magnitudes de `raw` justo por debajo y por encima de `DZ_inner` (0.149 y 0.151), WHEN se compara `output`, THEN la diferencia de magnitud es proporcional al delta de entrada, sin discontinuidad perceptible >5% de rango.

**AC-19 — F3a: sensibilidad de ratón sin deltaTime**
GIVEN `mouseDelta=(8,-3)`, `SensMouse=2.0`, `InvertY=false`, WHEN se procesa Mirar en un frame, THEN `rotY_delta=16°` y `rotX_delta=6°` exactos, independientemente del framerate (no se multiplica por `Time.deltaTime`).

**AC-20 — F3a: InvertY**
GIVEN `mouseDelta.y=-3`, `SensMouse=2.0`, WHEN `InvertY=true` vs `false`, THEN `rotX_delta` tiene signo opuesto (-6° vs +6°).

**AC-21 — F3a: clamp de pitch ±80°**
GIVEN la rotación de pitch acumulada en 79°, WHEN se aplica un `rotX_delta` que la llevaría a 85°, THEN la rotación se clampea a 80° exactos.

**AC-22 — F3b: dependencia de deltaTime en stick**
GIVEN el stick a deflexión completa (`response=1.0`), `SensStick=180°/s`, WHEN se compara `rotY_delta` a 30fps (`deltaTime≈0.0333s`) contra 60fps (`deltaTime≈0.0166s`), THEN el delta por frame a 30fps es ≈ el doble que a 60fps, pero la velocidad angular efectiva es la misma (~180°/s).

**AC-23 — F3b: curva exponencial (CurveExp=2.0)**
GIVEN magnitud de stick tras dead zone = 0.5, `CurveExp=2.0`, THEN `response=0.25` (no 0.5), verificable comparando contra una curva lineal (`CurveExp=1.0`) en las mismas condiciones.

**AC-24 — F3b: velocidad angular máxima**
GIVEN deflexión completa (`response=1.0`), `SensStick=180°/s`, a 60fps, WHEN se mide `rotY_delta` en un frame, THEN es ≈ `2.99°` (±0.05°), y una rotación de 360° tarda ≈ 2.0s (±0.05s) de input sostenido.

### Estados

**AC-25 — Entrada a Gameplay**
GIVEN una partida recién cargada (sin diálogo/menú), WHEN el juego entra en control del jugador, THEN el mapa activo es Gameplay.

**AC-26 — Entrada a Diálogo**
GIVEN el mapa Gameplay activo, WHEN se inicia una conversación, THEN Diálogo se empuja y se vuelve el mapa activo; AND Gameplay deja de recibir input.

**AC-27 — Entrada a MenúUI**
GIVEN cualquier mapa activo (Gameplay o Diálogo), WHEN se abre un menú, THEN MenúUI se empuja y se vuelve activo; AND el mapa anterior deja de recibir input.

**AC-28 — Entrada a Suspendido**
GIVEN cualquier estado de la pila, WHEN inicia una carga de escena, guardado síncrono de quit, o cinemática no-interactiva, THEN el sistema entra en Suspendido y ningún mapa recibe ni buferiza input.

**AC-29 — Salida de Suspendido**
GIVEN el sistema en Suspendido, WHEN termina la operación bloqueante, THEN se restaura como activo el mapa que estaba en la cima antes de suspender (sin input fantasma de reanudación).

**AC-30 — Entrada a Rebinding**
GIVEN el mapa MenúUI activo en la pantalla de controles, WHEN el jugador inicia un remapeo, THEN el sistema entra en Rebinding, captura la siguiente pulsación como candidato, y la acción normal de esa tecla queda ignorada mientras tanto.

**AC-31 — Salida de Rebinding**
GIVEN el sistema en Rebinding, WHEN el remapeo se completa o cancela, THEN el sistema vuelve a MenúUI (no a Gameplay ni Diálogo directamente).

### Edge Cases

**AC-32 — Desconexión de gamepad activo → pausa**
GIVEN el esquema activo es Gamepad, WHEN el gamepad se desconecta, THEN el sistema empuja MenúUI con aviso "Reconecta el mando" en el mismo frame o el siguiente en que se detecta.

**AC-33 — Reanudación tras reconexión**
GIVEN el estado de AC-32, WHEN el gamepad se reconecta o el jugador genera input desde KB&M, THEN el aviso se retira y se hace pop de MenúUI (o el esquema conmuta a KB&M, según cuál disparó la reanudación). *(Mecanismo exacto: ver Open Question.)*

**AC-34 — Desconexión ignorada si KB&M es esquema activo**
GIVEN el esquema activo es KB&M, WHEN un gamepad conectado se desconecta, THEN no se dispara pausa ni aviso.

**AC-35 — Conflicto de rebind**
GIVEN "Atacar" ligada a F, WHEN el jugador intenta remapear "Interactuar" también a F (mismo mapa), THEN se muestra "Intercambiar / Sobrescribir / Cancelar" y no se aplica cambio hasta que elija.

**AC-36 — Rebind: Sobrescribir deja acción sin asignar**
GIVEN el conflicto de AC-35, WHEN el jugador elige "Sobrescribir", THEN "Interactuar" queda ligada a F y "Atacar" queda marcada como "sin asignar" en la lista.

**AC-37 — Cancel al hacer push con acción pulsada**
GIVEN el jugador manteniendo pulsado Correr en Gameplay, WHEN se abre un menú mientras Correr sigue pulsado, THEN Gameplay recibe un `cancel` para Correr antes de suspenderse; AND al cerrar el menú, Correr permanece inactivo hasta que el jugador vuelva a pulsarlo.

**AC-38 — Buffer descartado al cambiar de contexto**
GIVEN Atacar bufferizado en Gameplay (dentro de ventana, no disponible), WHEN se abre un diálogo/menú antes de que expire, THEN el buffer se descarta; AND al volver a Gameplay, Atacar NO se ejecuta automáticamente.

**AC-39 — Dos acciones en el mismo frame, ambas entregadas**
GIVEN el jugador pulsa Atacar y Esquivar en el mismo frame, WHEN se procesa el input, THEN ambos eventos se entregan a sus consumidores en ese frame — Input no descarta ni prioriza ninguno.

**AC-40 — Gamepad conectado a mitad de partida no conmuta hasta input**
GIVEN el esquema activo es KB&M y se conecta un gamepad, WHEN no hay input generado desde ese gamepad, THEN el esquema permanece en KB&M y los prompts no cambian.

**AC-41 — Rebindings corruptos en PlayerPrefs → defaults**
GIVEN un valor de `PlayerPrefs` con overrides corrupto o que referencia una tecla/dispositivo inexistente, WHEN el juego arranca y carga los overrides, THEN esas acciones se restauran a su binding por defecto, se muestra un aviso, y el arranque no falla.

**AC-42 — Rebind a tecla reservada rechazado**
GIVEN el jugador intenta remapear una acción a una tecla reservada (ej. pausa/menú), WHEN confirma, THEN el sistema lo rechaza, muestra "Esta tecla está reservada", y el binding anterior permanece sin cambios.

**AC-43 — Dispositivo no soportado ignorado**
GIVEN un dispositivo no soportado conectado (volante, segundo gamepad), WHEN genera input, THEN el sistema lo ignora silenciosamente — no conmuta esquema, no dispara acciones, no genera error visible.

## Open Questions

1. **ADR de la capa de abstracción de input** — estructura de clases (clase generada + wrapper vs. `PlayerInput` component), mecanismo concreto de la pila de contextos, y cómo se entrega `cancel` a un mapa suspendido. *Dueño: gameplay-programmer/engine-programmer. Resolver con `/architecture-decision`.*
2. **Refactor de `PlayerController.cs`** — hoy lee input acoplado (mouse look, combo, dodge directos). Debe migrarse a consumir acciones semánticas de este sistema. *Dueño: gameplay-programmer. Resolver en implementación, coordinar con el GDD de Movimiento/Cámara cuando existan.*
3. **Set de glifos de gamepad** — genérico ABXY vs. detección de marca (Xbox/PlayStation/Switch Pro). *Dueño: ux-designer/art-director. Decisión de producción, no bloqueante para MVP.*
4. **Soporte parcial de gamepad — alcance exacto** — technical-preferences dice "gamepad parcial"; falta definir qué acciones tienen binding de gamepad garantizado en MVP y cuáles quedan solo en KB&M. *Dueño: game-designer. Resolver antes de create-stories del sistema.*
5. **Accesibilidad de input** — remapeo completo ya cubre lo básico, pero toggle-vs-hold para acciones mantenidas (correr, agacharse, apuntar) es una mejora de accesibilidad no incluida en MVP. *Dueño: ux-designer. Diferido a post-MVP salvo que se priorice accesibilidad antes.*
6. **Mecanismo exacto de reanudación tras reconexión de gamepad** (gap de QA) — el Edge Case dice que se reanuda "al reconectar o al usar KB&M", pero no aclara si el pop del overlay de pausa es automático al reconectar o requiere una confirmación explícita del jugador (pulsar un botón). *Dueño: ux-designer/game-designer. Resolver antes de create-stories del sistema.*
7. **Regla de conflicto de rebind entre mapas distintos** (gap de QA) — el Edge Case de conflicto solo cubre colisión dentro del mismo mapa. Falta definir si remapear una tecla de Gameplay a una ya usada en MenúUI cuenta como conflicto (relevante porque MenúUI se solapa con Gameplay suspendido). *Dueño: game-designer/ux-designer. Resolver antes de create-stories del sistema.*
