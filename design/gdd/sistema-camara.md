# Sistema de Cámara

> **Status**: In Design
> **Author**: user + agents
> **Last Updated**: 2026-07-13
> **Implements Pillar**: Infraestructura (habilita la percepción del mundo y la exploración)

## Overview

El Sistema de Cámara controla la ventana del jugador al mundo de Oasis: es un sistema híbrido de **primera y tercera persona** con la primera persona como modo primario (ADR-0001). En primera persona, la cámara se ancla a la cabeza del personaje para máxima inmersión — el jugador *es* el Renacido cruzando el Járnviðr. En tercera persona, orbita detrás del personaje a una distancia configurable, dando visión táctica del entorno y del propio avatar en combate. Como capa técnica, gestiona el encuadre según el modo, la **colisión de cámara** (evita que la cámara atraviese paredes acercándose al personaje mediante `SphereCast`), el suavizado del movimiento para eliminar tirones, y la transición entre modos. Consume la acción `Mirar` del Sistema de Input (con la sensibilidad y curva ya definidas ahí — no las recalcula) y expone su **origen y dirección** a Combate, que los usa como punto de partida de la detección de golpes (ADR-0001 FR2: la detección debe funcionar en ambos modos). A nivel de jugador, la cámara es su sentido de la vista: cuando funciona, desaparece; cuando falla (atraviesa geometría, se sacude, pierde el encuadre en combate), rompe la inmersión de golpe. Es Foundation: Combate, HUD y Movimiento dependen de su encuadre y de la dirección que define.

> **Nota de alcance**: la tecnología concreta (cámara custom vs. Cinemachine) y la estructura del rig son decisiones de implementación que irán a un ADR, no a este GDD. Este documento define *qué* modos existen y *cómo se comportan* (colisión, suavizado, transición, origen de detección); el ADR decide *cómo* se construyen.

## Player Fantasy

La cámara es el **sentido de la vista** del jugador, y su fantasía cambia con el modo. En **primera persona** — el modo primario — la fantasía es *estar ahí*: sentir el peso del Járnviðr sobre ti, la claustrofobia de una ruina de las Torres Caídas, la tensión de no saber qué hay detrás de la esquina en las Tierras Muertas. El Resplandor brilla en tu propia visión, no en la de un avatar. En **tercera persona**, la fantasía es *verte sobrevivir*: encuadrar a tu Renacido y su Bestia del Vínculo avanzando juntos, leer el campo de batalla completo cuando un grupo de Vaciados te rodea, apreciar la silueta de tu linaje. La cámara cumple su fantasía siendo **invisible cuando fluye y fiable cuando importa**: nunca debe atravesar una pared, sacudirse sin motivo, ni perder el objetivo en combate. La peor traición de una cámara no es verse mal — es hacer que el jugador muera por algo que no pudo ver. Su fantasía silenciosa es la **confianza**: el jugador nunca debe culpar a la cámara de una muerte.

*(Nota: `creative-director` no consultado — modo Lean. El tono se ancla directamente a los lugares y criaturas ya canónicos de la Biblia de Lore.)*

## Detailed Design

### Core Rules

1. **Dos modos, primera persona primaria.** La cámara opera en **Primera Persona (1P)** o **Tercera Persona (3P)**. 1P es el modo por defecto al iniciar partida (ADR-0001). El jugador **conmuta libremente** entre ambos mediante una acción semántica `CambiarCámara` (hoy el código lee `vKey` directo — debe migrarse a la acción de Input).

2. **Consumo de `Mirar`.** La cámara consume la acción `Mirar` (Vector2) del Sistema de Input, ya procesada con sensibilidad y curva (GDD de Input F3). La cámara **no** recalcula sensibilidad ni lee dispositivos — recibe yaw/pitch delta listos para aplicar.

3. **Primera persona:**
   - La cámara se ancla a la cabeza del personaje (`cameraHolder`). Posición y rotación siguen ese punto exactamente.
   - El **cuerpo rota con la cámara en yaw** (girar horizontalmente gira al personaje); el pitch (mirar arriba/abajo) solo afecta la cámara, con clamp `±80°` (heredado del GDD de Input / código actual).
   - No hay colisión de cámara (la cámara está dentro de la cabeza); el clipping de geometría muy cercana se maneja con el near-plane de render y ocultando/ajustando el arma en pantalla.

4. **Tercera persona (orbital libre):**
   - La cámara orbita alrededor del personaje a `distance` configurable, con un pivote a `height` sobre el punto base. `Mirar` rota la cámara en **yaw y pitch** alrededor del pivote (pitch con clamp `±[pitchMin, pitchMax]`).
   - El **cuerpo NO rota con la cámara al mirar** — el jugador puede orbitar para ver alrededor sin girar al personaje. El cuerpo se **reorienta hacia la dirección de la cámara solo al actuar** (moverse, atacar): al pulsar Mover o Atacar, el personaje gira hacia el yaw actual de la cámara.
   - **Colisión de cámara**: un `SphereCast` de radio `collisionRadius` desde el pivote hacia la posición deseada de la cámara; si golpea geometría, la cámara se acerca hasta el punto de impacto (menos el radio) para no atravesar paredes. Ya implementado en `CameraController.cs`.

5. **Suavizado.** La posición de la cámara en 3P se interpola hacia su objetivo con un factor de suavizado (`smoothSpeed`, `Lerp` frame-rate-aware) para eliminar tirones. La **corrección por colisión se aplica más rápido que el alejamiento** (ver Formulas) — acercarse a una pared debe ser inmediato (evita clipping), alejarse al despejarse debe ser suave (evita saltos bruscos).

6. **Transición entre modos.** Al conmutar 1P→3P, la cámara arranca detrás del personaje (según su yaw actual) y hace una transición suave hacia la posición orbital. Al conmutar 3P→1P, la cámara transiciona hacia la cabeza. Durante la transición, el modo activo para detección de golpes es el modo **destino** (para no dejar el origen indefinido).

7. **Origen de detección de golpes.** La cámara expone su **origen** (posición) y **dirección** (forward). Combate los consume como punto de partida de `SphereCast` para ataques a distancia y de la orientación de ataques (ADR-0001 FR2). El origen difiere por modo: en 1P es la cabeza; en 3P es la cámara orbital (o, para melee, puede usarse la posición del cuerpo — lo define Combate).

8. **Cámara desacoplada del jerárquico.** La cámara no es hija del personaje en el árbol de escena (evita heredar sacudidas de animación). Sigue al personaje por lógica, no por parentesco (ya así en el código actual).

### States and Transitions

| Estado | Condición de Entrada | Condición de Salida | Comportamiento |
|--------|----------------------|----------------------|----------------|
| **PrimeraPersona** | Estado inicial de partida; o conmutar desde 3P | Acción `CambiarCámara` → TransiciónA3P | Cámara en la cabeza; cuerpo rota con yaw; pitch clamp ±80°; sin colisión |
| **TerceraPersona** | Fin de TransiciónA3P | Acción `CambiarCámara` → TransiciónA1P | Orbital libre; cuerpo reorienta al actuar; colisión activa; suavizado |
| **TransiciónA3P** | `CambiarCámara` desde 1P | Transición completa → TerceraPersona | Interpola cámara de la cabeza a la posición orbital; modo de golpes = 3P |
| **TransiciónA1P** | `CambiarCámara` desde 3P | Transición completa → PrimeraPersona | Interpola cámara de la posición orbital a la cabeza; modo de golpes = 1P |
| **Suspendida** | Carga de escena, cinemática, menú de pausa | Fin de la operación → restaura el modo previo | La cámara no procesa `Mirar` (Input está Suspendido); mantiene su última posición o cede a una cámara de cinemática |

### Interactions with Other Systems

| Sistema | Dirección | Interfaz |
|---------|-----------|----------|
| **Sistema de Input** (Foundation, diseñado) | Input → Cámara | Provee `Mirar` (Vector2 procesado) y `CambiarCámara` (acción discreta). La cámara no lee dispositivos |
| **Combate** (Core, no diseñado; ADR-0001) | Cámara → Combate | Expone `GetCameraOrigin()` y `GetCameraForward()` como origen/dirección de detección de golpes; debe funcionar en 1P y 3P (FR2) |
| **Movimiento y Exploración** (Foundation, no diseñado) | Cámara → Movimiento | Expone el yaw de la cámara; en 3P el movimiento es relativo a la cámara y reorienta el cuerpo hacia ella al moverse |
| **HUD** (Presentation, no diseñado) | Cámara → HUD | El HUD consulta el modo activo (1P/3P) para mostrar/ocultar retícula y ajustar elementos según encuadre |
| **Gestión de Escenas/Niveles** (Core, no diseñado) | Escenas → Cámara | Durante transiciones/cinemáticas, Escenas pone la cámara en Suspendida o cede a una cámara de cinemática |
| **Metabolismo de Resplandor** (Foundation, no diseñado) | Resplandor → Cámara (efecto visual) | Zonas de Resplandor pueden aplicar post-procesado a la cámara (viñeta, distorsión verde-amarilla) — la cámara expone su target de post-proceso; el efecto lo define Resplandor |

**Nota de límite diseño/implementación**: la tecnología (cámara custom vs. Cinemachine), la estructura del rig orbital y el mecanismo exacto de la transición son decisiones de implementación → ADR. El GDD fija los modos, sus reglas de comportamiento, la colisión, el suavizado asimétrico y el contrato de origen de golpes.

## Formulas

### F1. Órbita de 3ª persona (yaw/pitch → posición cartesiana)

**Pivote:**

```
P_pivot = cameraHolder.position + Vector3.up * height
```

**Clamp de pitch:**

```
pitch = clamp(pitch + Δpitch, pitchMin, pitchMax)
yaw   = wrapAngle(yaw + Δyaw)   // sin clamp, orbital libre 360°
```

**Posición cartesiana relativa al pivote** (forma recomendada con quaternion, evita errores de signo):

```
rot = Quaternion.Euler(pitch, yaw, 0)
P_desired = P_pivot + (rot * Vector3.back) * distance
```

| Símbolo | Tipo | Rango | Descripción |
|---|---|---|---|
| `yaw` | float (°) | [0°, 360°) | Rotación horizontal acumulada de `Mirar.x` |
| `pitch` | float (°) | [`pitchMin`, `pitchMax`] | Rotación vertical acumulada de `Mirar.y`, clamped |
| `pitchMin` | float (°) | típico `-40°` | Límite inferior (mirar hacia abajo) |
| `pitchMax` | float (°) | típico `+70°` | Límite superior; asimétrico porque mirar muy hacia abajo en 3P mete la cámara en el suelo/personaje antes que mirar hacia arriba |
| `distance` | float (m) | `[1.5, 6.0]` | Distancia orbital deseada (ya existe: 4) |
| `height` | float (m) | `[1.0, 2.0]` | Altura del pivote sobre la base del personaje (ya existe: 1.5) |
| `P_pivot` | Vector3 | — | Punto de origen de la órbita |
| `P_desired` | Vector3 | — | Posición deseada de cámara antes de colisión/suavizado |

**Output range:** `P_desired` está siempre a exactamente `distance` metros de `P_pivot` (antes de colisión).

**Ejemplo:** `height=1.5`, `distance=4`, `yaw=180°` (cámara detrás en +Z), `pitch=20°`. Con `rot = Euler(20°,180°,0)`, `rot*Vector3.back ≈ (0, 0.342, 0.940)` → `offset ≈ (0, 1.37, 3.76)` → `P_desired ≈ (0, 2.87, 3.76)` relativo al personaje.

### F2. Colisión de cámara (SphereCast)

Formaliza la base ya presente en `CameraController.cs`:

```
desiredDistance = distance
hit = SphereCast(origin: P_pivot, radius: collisionRadius,
                  direction: dirToDesired, maxDistance: distance)

if hit:
    correctedDistance = max(minDistance, hit.distance - collisionRadius)
else:
    correctedDistance = desiredDistance

P_target = P_pivot + dirToDesired * correctedDistance
```

| Símbolo | Tipo | Rango | Descripción |
|---|---|---|---|
| `collisionRadius` | float (m) | `[0.1, 0.4]` | Radio de la esfera del SphereCast (ya existe: 0.2) |
| `hit.distance` | float (m) | `[0, distance]` | Distancia al primer impacto de geometría |
| `minDistance` | float (m) | típico `0.1–0.3` | Distancia mínima absoluta (evita cámara en el pivote; hoy literal `0.1`, se propone nombrarla) |
| `correctedDistance` | float (m) | `[minDistance, distance]` | Distancia efectiva tras colisión |

**Output range:** `correctedDistance ∈ [minDistance, distance]`, nunca excede `distance` ni penetra la pared golpeada (el margen `collisionRadius` evita que la esfera de la cámara siga atravesando el collider).

**Ejemplo:** `distance=4`, `collisionRadius=0.2`. El SphereCast golpea una pared a `hit.distance=1.5`. `correctedDistance = max(0.1, 1.5-0.2) = 1.3`. La cámara se coloca a 1.3m del pivote en vez de 4m.

### F3. Suavizado asimétrico (acercar rápido / alejar suave)

**Regla de selección de velocidad:**

```
if correctedDistance < currentDistance:
    k = smoothSpeedIn      // acercar (colisión) → rápido
else:
    k = smoothSpeedOut     // alejar (despejar) → suave
```

**Forma recomendada — exponencial frame-rate-independent** (reemplaza el `Lerp(pos, target, smoothSpeed*dt)` actual, que no compone correctamente entre frames de distinto `dt`; la forma exponencial sí):

```
t = 1 - exp(-k * Δt)
thirdPersonPosition = Vector3.Lerp(thirdPersonPosition, P_target, t)
```

Equivalente Unity: `Vector3.Lerp(current, target, 1 - Mathf.Exp(-k * Time.deltaTime))`.

*(Alternativa simple, consistente con el código actual: `smoothSpeed * Time.deltaTime` clamped a `[0,1]` — aceptable a 60fps estable, pero diverge si el frame-rate cae. Se recomienda la forma exponencial precisamente porque el juego apunta a 60fps con posibles caídas en combate con muchos Vaciados.)*

| Símbolo | Tipo | Rango | Descripción |
|---|---|---|---|
| `smoothSpeedIn` | float (1/s) | `[15, 30]` | Velocidad de acercamiento por colisión (casi instantáneo, evita clipping) |
| `smoothSpeedOut` | float (1/s) | `[4, 8]` | Velocidad de alejamiento al despejar (suave, evita saltos) |
| `k` | float (1/s) | = `smoothSpeedIn` o `smoothSpeedOut` | Velocidad seleccionada este frame |
| `Δt` | float (s) | `Time.deltaTime`, ~0.0166 a 60fps | Paso de tiempo del frame |
| `t` | float | `(0, 1]` | Factor de interpolación exponencial de este frame |
| `currentDistance` | float (m) | — | `(thirdPersonPosition - P_pivot).magnitude` del frame anterior |

**Output range:** `t ∈ (0,1]`; con `k=24` (in) y `Δt=0.0166s`, `t ≈ 0.33` por frame (converge en ~4-5 frames, ~70ms). Con `k=6` (out), `t ≈ 0.096` por frame (converge en ~25-30 frames, ~450ms).

**Ejemplo:** `currentDistance=4.0`, `correctedDistance=1.3` (colisión) → usa `smoothSpeedIn=24`. A 60fps: `t = 1-exp(-24*0.0166) ≈ 0.331`. `newDistance ≈ 4.0 + (1.3-4.0)*0.331 ≈ 3.11` — se acerca rápido en el primer frame. Si al frame siguiente la pared desaparece y `correctedDistance` vuelve a `4.0`, se usa `smoothSpeedOut=6`: `t = 1-exp(-6*0.0166) ≈ 0.095`, avance mucho más gradual.

**Nota**: los valores de ejemplo (`smoothSpeedIn=24`, `smoothSpeedOut=6`, `pitchMin=-40°`, `pitchMax=70°`) son puntos de partida sugeridos, formalizados como Tuning Knobs — no finales.

## Edge Cases

- **Si en 3P el personaje entra en un espacio tan estrecho que `correctedDistance` queda forzada al `minDistance` de forma sostenida**: la cámara auto-conmuta temporalmente a 1ª persona (sin acción del jugador) para evitar una vista pegada a la nuca inservible. Al despejarse el espacio (la colisión deja de forzar el mínimo durante un umbral breve), vuelve automáticamente a 3P con el mismo ángulo previo. El HUD refleja el modo efectivo actual.
- **Si el jugador vuelve de una suspensión (diálogo, cinemática, menú) que ocurrió en 3P**: la cámara restaura el mismo modo (3P) y el mismo yaw/pitch/distancia que tenía antes de suspenderse — continuidad exacta, sin resetear el encuadre.
- **Si el jugador mantiene el pitch al extremo (mirando recto arriba/abajo) y se mueve**: la dirección de movimiento se calcula solo con el **yaw** (horizontal); el pitch nunca empuja al personaje hacia el suelo o el cielo. En 1P el clamp `±80°` ya evita el gimbal; en 3P el clamp `pitchMin/pitchMax` hace lo propio.
- **Si `CambiarCámara` se pulsa durante una transición ya en curso** (ej. spam de la tecla): la transición actual se invierte hacia el nuevo destino desde su posición interpolada actual (no se reinicia desde cero ni se encola) — evita saltos y acumulación de transiciones.
- **Si el SphereCast de colisión golpea un collider no deseado** (ej. un trigger, el propio personaje, un enemigo): la máscara de colisión (`collisionMask`) excluye por capas al jugador, enemigos y triggers — solo geometría de entorno sólida acerca la cámara. Un enemigo entre cámara y personaje **no** hace saltar la cámara.
- **Si el personaje muere y cae mientras la cámara está en 1P**: la cámara sigue anclada a la cabeza durante la animación de muerte (hereda el movimiento del `cameraHolder`); si eso produce un encuadre desagradable, Muerte y Resurrección puede solicitar una transición a una cámara de muerte dedicada (fuera del alcance de este sistema — se expone el hook de "ceder control a cámara externa").
- **Si dos zonas de post-procesado de Resplandor se solapan** (efecto visual sobre la cámara): la cámara solo expone su target de post-proceso; la resolución de qué efecto aplica cuando hay solapamiento es responsabilidad del Metabolismo de Resplandor, no de la cámara.
- **Si `Mirar` entrega un delta enorme en un solo frame** (ej. un hitch de frame-rate acumula input, o un ratón de alta DPI): el pitch se clampa siempre a sus límites (no hay overshoot que voltee la cámara); el yaw simplemente rota mucho ese frame pero sin romper (yaw es libre 360°). No se aplica suavizado al input de mirada — la cámara responde 1:1 al `Mirar` ya procesado por Input (el suavizado es solo para la posición en 3P, no para la rotación).

## Dependencies

| Sistema | Dirección | Tipo | Interfaz / Nota |
|---------|-----------|------|-------------------|
| **Sistema de Input** | Upstream | Duro | La cámara consume `Mirar` (Vector2 procesado) y `CambiarCámara`. Sin Input no hay control de cámara. Único upstream duro |
| **Combate** | Downstream | Duro (mutuo) | Cámara expone `GetCameraOrigin()`/`GetCameraForward()`; Combate los usa como origen de detección de golpes en 1P y 3P (ADR-0001 FR2) |
| **Movimiento y Exploración** | Downstream | Duro (para Movimiento en 3P) | Cámara expone el yaw; el movimiento en 3P es relativo a la cámara y reorienta el cuerpo hacia ella al actuar |
| **HUD** | Downstream | Blando | El HUD consulta el modo activo (1P/3P) para retícula/encuadre. El HUD funciona sin este dato pero se enriquece con él |
| **Gestión de Escenas/Niveles** | Downstream | Duro (para suspensión) | Escenas pone la cámara en Suspendida durante transiciones/cinemáticas y puede ceder a una cámara de cinemática |
| **Metabolismo de Resplandor** | Downstream | Blando (efecto visual) | Resplandor aplica post-procesado al target de cámara. La cámara funciona sin Resplandor; el efecto es puramente visual |
| **Muerte y Resurrección** | Downstream | Blando (hook) | Muerte puede solicitar ceder el control a una cámara de muerte dedicada vía el hook de "cámara externa" |
| **Sistema de Guardado/Persistencia** | Lateral | Blando (límite) | El modo de cámara actual (1P/3P) **puede** guardarse como preferencia, pero es opcional — no es estado crítico de partida. Decisión abierta (ver Open Questions) |

**Nota de dureza asimétrica**: la Cámara tiene **un** upstream duro (Input) — a diferencia de Guardado/Input que eran Foundation puros sin upstream. Hacia abajo, Combate y Movimiento la necesitan de forma dura (origen de golpes, dirección de movimiento); HUD, Resplandor y Muerte la usan de forma blanda (enriquecen pero no bloquean).

**Nota de bidireccionalidad**: de sus dependencias, solo **Input** y **Guardado** tienen GDD. La dependencia de Input es coherente — el GDD de Input lista `Mirar` como acción que Cámara consume (aunque Input no nombra "Cámara" explícitamente en su tabla; se puede añadir en un pase de consistencia). El resto aún no tiene GDD; deben listar "depende de Sistema de Cámara" cuando se diseñen.

## Tuning Knobs

| Knob | Rango seguro | Default | Qué rompe en los extremos |
|------|--------------|---------|----------------------------|
| `distance` (distancia orbital 3P) | 1.5 – 6.0 m | 4.0 m | Por debajo de 1.5: la cámara queda casi dentro del personaje. Por encima de 6.0: encuadre lejano, pierde intimidad y el personaje se ve pequeño |
| `height` (altura del pivote) | 1.0 – 2.0 m | 1.5 m | Muy bajo: la cámara mira los pies. Muy alto: vista "cenital" poco natural |
| `pitchMin` (límite mirar abajo, 3P) | -60 – -20° | -40° | Muy bajo (hacia -60): la cámara se mete bajo el suelo/personaje. Muy alto (hacia -20): no se puede mirar hacia abajo lo suficiente en plataformeo |
| `pitchMax` (límite mirar arriba, 3P) | +50 – +85° | +70° | Muy alto: casi cenital, desorienta. Muy bajo: no se puede mirar hacia arriba (ver Torres Caídas altas) |
| `collisionRadius` (radio SphereCast) | 0.1 – 0.4 m | 0.2 m | Muy pequeño: la cámara clippea esquinas finas. Muy grande: la cámara se acerca demasiado pronto ante geometría lejana |
| `minDistance` (distancia mínima tras colisión) | 0.1 – 0.3 m | 0.1 m | Por debajo de 0.1: la cámara entra en el modelo del personaje. Por encima de 0.3: en espacios estrechos la cámara clippea antes de auto-conmutar a 1P |
| `smoothSpeedIn` (velocidad de acercamiento por colisión) | 15 – 30 /s | 24 /s | Por debajo de 15: la cámara clippea la pared antes de acercarse. Por encima de 30: acercamiento tan brusco que se nota el salto |
| `smoothSpeedOut` (velocidad de alejamiento al despejar) | 4 – 8 /s | 6 /s | Por debajo de 4: la cámara tarda demasiado en volver a la distancia normal. Por encima de 8: el alejamiento se siente brusco, pierde el propósito del suavizado asimétrico |
| `transitionDuration` (duración de transición 1P↔3P) | 0.2 – 0.6 s | 0.35 s | Por debajo de 0.2: el cambio de modo es un corte brusco. Por encima de 0.6: se siente lento, el jugador espera |
| `narrowSpaceThreshold` (umbral para auto-conmutar a 1P) | 0.1 – 0.5 s | 0.25 s | Tiempo sostenido en `minDistance` antes de auto-conmutar a 1P. Muy corto: conmuta por roces momentáneos. Muy largo: aguanta demasiado una vista mala |

**Interacciones entre knobs**: `smoothSpeedIn` y `smoothSpeedOut` deben mantener `In > Out` — es el corazón del suavizado asimétrico; si se igualan, se pierde el comportamiento "acercar rápido / alejar suave". `collisionRadius` y `minDistance` juntos definen cuándo se dispara la auto-conmutación a 1P: un `collisionRadius` grande con `minDistance` grande hace que espacios moderadamente estrechos fuercen el modo 1P antes. `distance` y `pitchMin` interactúan: a mayor distancia, un `pitchMin` bajo mete la cámara en el suelo más fácilmente.

Nota: `SensMouse`, `SensStick`, `CurveExp`, `InvertY` **no** son knobs de este sistema — pertenecen al Sistema de Input (la cámara consume `Mirar` ya procesado). No duplicar aquí.

## Visual/Audio Requirements

> Sistema mixto: tiene superficie visual real (es la cámara), pero la mayoría del "arte" que se ve a través de ella pertenece a otros sistemas. Aquí solo lo que la cámara controla directamente.

- **Transición de modo (1P↔3P)**: interpolación suave de posición y FOV durante `transitionDuration` (~0.35s), sin corte brusco. Opcionalmente un leve cambio de FOV durante la transición para dar sensación de "zoom out/in". No debe marear.
- **Target de post-procesado**: la cámara expone su volumen/target de post-proceso (URP Volume) para que otros sistemas apliquen efectos — Metabolismo de Resplandor (viñeta verde-amarilla, distorsión en zonas radiactivas), daño (tinte rojo al recibir golpes, lo define Combate/HUD). La cámara **no** define estos efectos, solo el punto donde se aplican.
- **Screen-shake (hook)**: la cámara expone un hook de sacudida que Combate/impactos pueden invocar (magnitud + duración). El shake se aplica como offset aditivo sobre la posición final, sin romper el suavizado ni la colisión. La intensidad debe ser sutil (accesibilidad: un knob para reducir/desactivar shake).
- **Ocultado de arma en 1P cerca de geometría**: en 1ª persona, si el personaje se acerca mucho a una pared, el arma en pantalla se ajusta/oculta para no clippear (coordinación con el sistema de armas/Combate). La cámara señala la proximidad; el ajuste del arma lo hace Combate.
- **Audio**: la cámara no genera audio propio. Sí puede ser el **punto de escucha** (AudioListener) — su posición determina el audio espacial 3D. En 3P, decisión de diseño: el listener sigue a la cámara o al personaje (ver Open Questions).

## UI Requirements

- **Retícula/crosshair según modo**: en 1P se muestra una retícula central (apuntado); en 3P, retícula contextual o ninguna según el estado de combate. El HUD consulta el modo de cámara para decidir (ver Dependencies). La cámara solo expone el modo; el HUD dibuja.
- **Opciones de cámara** (en el menú de opciones): FOV (campo de visión), distancia de 3ª persona, intensidad de screen-shake (incluido desactivar, por accesibilidad), y toggle de auto-conmutación a 1P en espacios estrechos (para jugadores que prefieran mantener 3P). `InvertY` **no** va aquí — pertenece a Input.
- Toda esta UI la implementa **Menús y UI** / **HUD** (Presentation, no diseñados); la cámara expone modo, FOV y los knobs consultables.

> 📌 **UX Flag — Sistema de Cámara**: Este sistema aporta a la UI (retícula por modo + opciones de cámara). En Fase 4 (Pre-Producción), la retícula debe especificarse en la spec de HUD (`design/ux/hud.md`) y las opciones en la de menús — no en este GDD directamente.

## Acceptance Criteria

### Core Rules

- **AC1 (2 modos, 1P default)**: GIVEN se inicia una nueva partida, WHEN la escena de juego carga, THEN la cámara está en estado `PrimeraPersona` (no `TerceraPersona`).
- **AC2 (conmutación libre)**: GIVEN la cámara está en `PrimeraPersona` o `TerceraPersona` (fuera de transición y no Suspendida), WHEN el jugador ejecuta `CambiarCámara`, THEN la cámara inicia la transición hacia el modo opuesto.
- **AC3 (consumo de `Mirar` sin leer dispositivos)**: GIVEN la cámara está activa, WHEN se le entrega un valor `Mirar` (Vector2) ya procesado, THEN la cámara aplica yaw/pitch delta directamente sin leer ratón/stick/dispositivo alguno (no existe lectura de input crudo en el componente de cámara).
- **AC4 (1P: cámara=cabeza, cuerpo rota con yaw, clamp ±80°)**: GIVEN modo `PrimeraPersona`, WHEN `Mirar.x` cambia, THEN la cámara y el cuerpo rotan en yaw idénticamente; WHEN `Mirar.y` cambia, THEN solo la cámara rota en pitch, clampado a `[-80°, +80°]`, sin afectar la rotación del cuerpo.
- **AC5 (3P orbital libre, cuerpo reorienta solo al actuar)**: GIVEN modo `TerceraPersona`, WHEN el jugador aplica `Mirar` sin pulsar Mover ni Atacar, THEN la cámara orbita (yaw libre 360°, pitch clamp `[pitchMin, pitchMax]`) y el yaw del cuerpo permanece sin cambios; WHEN el jugador pulsa Mover o Atacar, THEN el cuerpo rota hacia el yaw actual de la cámara.
- **AC6 (colisión solo en 3P)**: GIVEN modo `PrimeraPersona`, WHEN la cámara está cerca de geometría, THEN no se ejecuta ningún `SphereCast` de colisión de cámara. GIVEN modo `TerceraPersona`, WHEN el `SphereCast` desde el pivote hacia `P_desired` golpea geometría, THEN la distancia se corrige según F2.
- **AC7 (suavizado asimétrico)**: GIVEN modo `TerceraPersona`, WHEN `correctedDistance < currentDistance` (colisión), THEN se usa `k = smoothSpeedIn`; WHEN `correctedDistance >= currentDistance` (despeje), THEN se usa `k = smoothSpeedOut`.
- **AC8 (transición)**: GIVEN `CambiarCámara` desde `PrimeraPersona`, WHEN la transición inicia, THEN la cámara arranca detrás del personaje según el yaw actual y converge a la posición 3P en `transitionDuration` (default 0.35s), sin corte instantáneo.
- **AC9 (origen de golpes por modo)**: GIVEN modo `PrimeraPersona`, WHEN Combate llama `GetCameraOrigin()`, THEN retorna la posición de la cabeza (`cameraHolder`). GIVEN modo `TerceraPersona`, THEN retorna la posición de la cámara orbital.
- **AC10 (cámara desacoplada del jerárquico)**: GIVEN el árbol de escena en ejecución, WHEN se inspecciona el `Transform.parent` del GameObject de cámara, THEN no es descendiente del GameObject del personaje.

### Formulas

- **AC11 (F1 pitch clamp)**: GIVEN modo `TerceraPersona`, WHEN se aplica `Δpitch` que excedería el rango, THEN `pitch` queda clampado en `[-40°, +70°]` sin overshoot ni voltear la cámara.
- **AC12 (F1 posición a `distance` del pivote)**: GIVEN `height=1.5`, `distance=4`, sin colisión, WHEN se calcula `P_desired` para cualquier yaw/pitch válido, THEN `|P_desired - P_pivot| == 4.0` (tolerancia de punto flotante).
- **AC13 (F2 colisión pared a 1.5m)**: GIVEN `distance=4`, `collisionRadius=0.2`, `minDistance=0.1`, WHEN el `SphereCast` reporta `hit.distance=1.5`, THEN `correctedDistance == max(0.1, 1.5-0.2) == 1.3` y la cámara se posiciona a 1.3m del pivote (no a 4m).
- **AC14 (F3 acercar: smoothSpeedIn=24, ~70ms)**: GIVEN `currentDistance=4.0`, `correctedDistance=1.3`, a 60fps (`Δt≈0.0166s`), WHEN se aplica un frame de suavizado, THEN se usa `k=24` y `t ≈ 0.33`; la distancia converge a <5% del objetivo en ~4-5 frames (~70ms).
- **AC15 (F3 alejar: smoothSpeedOut=6, ~450ms)**: GIVEN `currentDistance=1.3`, `correctedDistance=4.0`, a 60fps, WHEN se aplica un frame de suavizado, THEN se usa `k=6` y `t ≈ 0.095`; la distancia converge a <5% del objetivo en ~25-30 frames (~450ms).
- **AC16 (F3 regla de selección)**: GIVEN cualquier frame en `TerceraPersona`, WHEN `correctedDistance < currentDistance`, THEN se usa `smoothSpeedIn`; en cualquier otro caso, `smoothSpeedOut`. Nunca se usa `smoothSpeedOut` para un evento de colisión activa en el mismo frame.

### States

- **AC17**: GIVEN estado `PrimeraPersona`, WHEN se ejecuta `CambiarCámara`, THEN el estado pasa a `TransiciónA3P` (nunca directo a `TerceraPersona`).
- **AC18**: GIVEN estado `TerceraPersona`, WHEN se ejecuta `CambiarCámara`, THEN el estado pasa a `TransiciónA1P`.
- **AC19**: GIVEN estado `TransiciónA3P`, WHEN la interpolación completa (converge o transcurre `transitionDuration`), THEN pasa a `TerceraPersona` y el origen de golpes reportado fue 3P durante toda la transición.
- **AC20**: GIVEN estado `TransiciónA1P`, WHEN la interpolación completa, THEN pasa a `PrimeraPersona` y el origen de golpes reportado fue 1P durante toda la transición.
- **AC21**: GIVEN cualquier estado activo (1P, 3P o en transición), WHEN ocurre carga de escena, cinemática, o se abre el menú de pausa, THEN el estado pasa a `Suspendida` y la cámara deja de procesar `Mirar`.

### Edge Cases

- **AC22 (espacio estrecho → auto-conmuta a 1P)**: GIVEN modo `TerceraPersona`, WHEN `correctedDistance` permanece forzada a `minDistance` de forma continua durante `narrowSpaceThreshold` (default 0.25s), THEN la cámara conmuta automáticamente a `PrimeraPersona` sin acción del jugador, y el HUD refleja el modo efectivo (1P).
- **AC23 (restaurar tras suspensión)**: GIVEN la cámara estaba en `TerceraPersona` con yaw/pitch/distancia específicos antes de entrar en `Suspendida`, WHEN la suspensión termina, THEN restaura exactamente el mismo modo, yaw, pitch y distancia previos (sin resetear encuadre).
- **AC24 (pitch extremo → movimiento solo yaw)**: GIVEN pitch en su límite (±80° en 1P o `pitchMin`/`pitchMax` en 3P), WHEN el jugador se mueve, THEN la dirección de movimiento se calcula únicamente con el yaw de la cámara (el pitch no afecta la componente de movimiento).
- **AC25 (spam de CambiarCámara durante transición)**: GIVEN la cámara está en `TransiciónA3P` en curso (posición interpolada parcial), WHEN se ejecuta `CambiarCámara` de nuevo, THEN el estado pasa a `TransiciónA1P` iniciando desde la posición interpolada actual (no desde 1P ni 3P puros, y sin reiniciar ni encolar transiciones).
- **AC26 (SphereCast ignora enemigos/triggers)**: GIVEN un enemigo o trigger se interpone entre el pivote y `P_desired` en 3P, WHEN se ejecuta el `SphereCast` de colisión, THEN el `hit` no incluye ese enemigo/trigger (excluidos vía `collisionMask`) y la distancia no se corrige por su presencia; solo geometría de entorno sólida dispara la corrección.

## Open Questions

1. **ADR de la cámara** — cámara custom vs. Cinemachine, estructura del rig orbital, y mecanismo de transición y de "ceder a cámara externa" (muerte/cinemática). *Dueño: gameplay-programmer/engine-programmer. Resolver con `/architecture-decision`.*
2. **Refactor de `CameraController.cs`** — hoy la 3ª persona está anclada detrás (usa `playerBody.forward`); este GDD especifica orbital libre. Además el toggle lee `vKey` directo → migrar a la acción `CambiarCámara` de Input, y migrar el `Lerp` a la forma exponencial de F3. *Dueño: gameplay-programmer. Resolver en implementación.*
3. **AudioListener en 3ª persona** — ¿el punto de escucha sigue a la cámara (audio coherente con lo que se ve) o al personaje (audio coherente con dónde está el avatar)? Afecta la percepción espacial del sonido. *Dueño: audio-director/game-designer. Resolver antes de integrar Audio.*
4. **¿Se guarda el modo de cámara (1P/3P) como preferencia?** — decisión abierta con Guardado: guardarlo como setting de jugador (PlayerPrefs, como Input) o no persistirlo (arrancar siempre en 1P). *Dueño: game-designer. Resolver en un pase de consistencia con Guardado/Input.*
5. **Añadir "Cámara" a la tabla de dependencias del GDD de Input** — Input lista `Mirar`/`CambiarCámara` como acciones pero no nombra a Cámara como consumidor. Pase de consistencia menor. *Dueño: game-designer. Resolver con `/consistency-check` o edición directa.*
6. **`narrowSpaceThreshold` durante una transición en curso** (gap de QA) — no está definido si el temporizador de auto-conmutación a 1P por espacio estrecho corre mientras hay una `TransiciónA3P`/`TransiciónA1P` activa. *Dueño: gameplay-programmer/game-designer. Resolver antes de create-stories del sistema.*
7. **Epsilon de convergencia para QA** (gap de QA) — los criterios de suavizado (F3, ~70ms/~450ms) usan aproximaciones ("converge en ~4-5 frames"). Para pass/fail sin ambigüedad, QA necesita un umbral exacto (ej. ±2% de la distancia objetivo). *Dueño: qa-lead. Fijar en `/qa-plan`.*
