# Linajes (Clases)

> **Status**: In Design
> **Author**: user + agents
> **Last Updated**: 2026-07-06
> **Implements Pillar**: Ser el puente entre dos mundos / Descubrir la verdad

## Overview

Linajes (Clases) define las cinco identidades jugables de Oasis — Kael, Sira, Davan, Vael y Ren — como estructuras de datos (árbol de habilidades, stats base, nodos desbloqueables) y como la decisión de identidad central que el jugador toma al elegir un Renacido. Cada linaje expone un conjunto fijo de stats de combate (`baseDamage`, `critChance`, `critMultiplier` — contrato definido en ADR-0001) y un árbol de nodos de habilidad propio que Progresión de Personaje consulta para gestionar el desbloqueo. A nivel de jugador, el linaje no es solo una clase mecánica: es la única capa de identidad que el Renacido conserva completa entre expediciones — a diferencia del equipo (perdido al morir) o el conocimiento (parcialmente borrado), el linaje y sus habilidades son permanentes, ancladas al linaje Adaptado en la sangre del personaje.

## Player Fantasy

**Ser reconocido por lo que siempre fuiste, aunque no lo recuerdes.** Elegir un linaje no es elegir una build — es descubrir gradualmente una identidad que estaba en tu sangre antes de que los Custodios te la borraran. La fantasía central es la tensión entre *sentirse competente desde el primer combate* (tus reflejos, tu instinto, tu cuerpo ya "saben" pelear como Vael o rastrear como Kael) y *no saber por qué* hasta que el exterior te lo revela pieza por pieza. Cada linaje debe hacer que su mecánica se sienta como un recuerdo del cuerpo, no como una hoja de stats: Kael no "tiene bono de percepción", Kael *siente* el Resplandor como si ya lo hubiera hecho miles de veces. La progresión mecánica y el misterio narrativo del linaje (sección 11 de la Biblia de Lore) deben sentirse como la misma revelación, nunca como sistemas separados.

*(Nota: `creative-director` no fue consultado — modo Lean. Esta sección usa directamente los "misterios revelados" ya definidos por linaje en la Biblia de Lore como fuente. Revisar con dirección creativa antes de producción si se quiere refinar tono.)*

## Detailed Design

### Core Rules

1. Cada uno de los 5 linajes (Kael, Sira, Davan, Vael, Ren) define un conjunto fijo de **stats base de combate**, dentro de los rangos fijados por ADR-0001: `baseDamage` (5–200), `critChance` (0.0–0.4), `critMultiplier` (1.5–2.5). Estos valores exactos por linaje se definen en la sección Formulas.
2. Cada linaje tiene un **árbol de habilidades propio** con forma de tronco + ramas:
   - **Tronco**: 3-4 nodos de habilidad básica, obligatorios, desbloqueados en orden lineal (no requieren elección).
   - **Ramas**: 2-3 especializaciones por linaje que divergen del tronco (ej. Vael: rama "Resistencia" vs. rama "Furia"). Un nodo de rama requiere el nodo anterior de la misma rama Y el nodo de tronco correspondiente.
3. Los nodos se desbloquean gastando **puntos de habilidad**. Linajes define la estructura del árbol (nodos, costos, requisitos); Progresión de Personaje define cuántos puntos existen y cuándo se otorgan (contrato ya documentado en systems-index.md).
4. **Respec (reasignación)**: el jugador puede resetear el árbol de un linaje completo a cambio de un ítem consumible raro — un **Fragmento de Memoria Adaptada**, obtenible solo en el exterior. Reforzar el vínculo con el lore: los Custodios "reescriben el cerebro desde una plantilla base" al morir (Biblia de Lore, sección 7); el fragmento representa memoria Adaptada estable que resiste ese borrado y permite reconstruir el árbol de otra forma.
5. Cada nodo expone: `id`, `nombre`, `rama` (tronco/rama-A/rama-B), `costo_puntos`, `requisitos` (lista de ids de nodos previos), `efecto` (referencia a modificador de stat o habilidad activa/pasiva).
6. Linajes NO define la mecánica de la Bestia del Vínculo — solo expone el mapeo `linaje → bestia_id` (ej. `Kael → cristalino`) como dato de referencia para que Bestias del Vínculo (sistema separado, aún no diseñado) lo consuma.

### States and Transitions

| Estado | Condición de Entrada | Condición de Salida | Comportamiento |
|--------|----------------------|----------------------|----------------|
| **Bloqueado** | Requisitos no cumplidos (nodo previo no desbloqueado) | Requisitos cumplidos → Desbloqueable | No seleccionable en UI, mostrado atenuado |
| **Desbloqueable** | Requisitos cumplidos, puntos disponibles ≥ costo | Jugador gasta puntos → Desbloqueado | Resaltado en UI, invita a interacción |
| **Desbloqueado** | Jugador confirma gasto de puntos | Respec ejecutado → vuelve a Bloqueado (si no cumple requisitos) o Desbloqueable | Efecto activo permanentemente; stat/habilidad aplicada |
| **Perdido por Muerte** | Muerte y Resurrección dispara "pierde 1 punto de habilidad" | Recuperado si el jugador vuelve a cumplir el costo | El nodo más recientemente desbloqueado pasa a Desbloqueable (no se pierde el progreso del árbol completo, solo el último nodo — ver Edge Cases) |

### Interactions with Other Systems

| Sistema | Dirección | Interfaz |
|---------|-----------|----------|
| **Progresión de Personaje** (Core, no diseñado) | Progresión → Linajes | Progresión otorga puntos de habilidad; consulta la estructura de nodos de Linajes (`GetAvailableNodes()`, `UnlockNode(id)`) y aplica el estado de desbloqueo. Linajes es dueño de la estructura; Progresión es dueño del ritmo de entrega de puntos. |
| **Combate** (Core, no diseñado; contrato en ADR-0001) | Linajes → Combate | Linajes expone `baseDamage`, `critChance`, `critMultiplier` por linaje como parte de `DamageData` (struct ya definida en ADR-0001). |
| **Muerte y Resurrección** (Foundation, no diseñado) | Muerte → Linajes | Al morir, Muerte y Resurrección invoca `RevertLastUnlockedNode()` sobre el árbol activo, aplicando la pérdida de "1 punto de habilidad" ya canónica en el lore. |
| **Bestias del Vínculo** (Core, no diseñado) | Linajes → Bestias | Linajes expone el mapeo estático `linaje → bestia_id` (dato de solo lectura, sin lógica). |
| **Inventario y Equipo** (Core, no diseñado) | Inventario → Linajes | El ítem "Fragmento de Memoria Adaptada" (a registrar) es consumido por Linajes al ejecutar un respec; Inventario gestiona su posesión y cantidad. |
| **Menús y UI** (Presentation, no diseñado) | Linajes → UI | Linajes expone el árbol completo con estados por nodo para que la UI renderice el árbol de habilidades. |

## Formulas

### 1. Stats de Combate por Linaje

Los 5 linajes distribuyen sus valores dentro de los rangos fijados por ADR-0001 (`baseDamage` 5–200, `critChance` 0.0–0.4, `critMultiplier` 1.5–2.5) reflejando su rol de combate: Vael (tanque/melee bruto) maximiza `baseDamage` y minimiza `critChance`; Ren (sigilo/infiltración) maximiza `critChance` y `critMultiplier` a costa de `baseDamage` bajo; Sira (soporte/médica) tiene el perfil ofensivo más débil de los cinco, coherente con su rol no-combativo; Kael y Davan ocupan el rango medio con perfiles distintos entre sí.

`baseDamage`, `critChance` y `critMultiplier` se definen como valores fijos por linaje (no una fórmula derivada) porque son inputs directos a `DamageData` por diseño de ADR-0001 — cada linaje es un punto de datos, no un cálculo.

**Variables:**
| Variable | Símbolo | Tipo | Rango | Descripción |
|----------|---------|------|-------|-------------|
| Daño base del linaje | `baseDamage` | float | 5–200 | Input a `DamageData.BaseDamage` antes de multiplicadores de arma/skill |
| Probabilidad de crítico | `critChance` | float | 0.0–0.4 | Input a `DamageData.CritChance` |
| Multiplicador de crítico | `critMultiplier` | float | 1.5–2.5 | Input a `DamageData.CritMultiplier` |

**Output Range:** Valores fijos por linaje (tabla abajo), todos dentro de rango ADR-0001.

**Tabla de valores por linaje:**

| Linaje | `baseDamage` | `critChance` | `critMultiplier` | Justificación de diseño |
|--------|-------------|--------------|-------------------|--------------------------|
| **Vael** (Forjados — cuerpo a cuerpo, tanque, resistencia extrema) | 85 | 0.10 | 1.7 | Mayor `baseDamage` del elenco: su fantasía es golpear fuerte y sostenido, no depender de la varianza del crítico. `critChance` deliberadamente el más bajo — Vael no necesita "momentos de suerte", su daño ya es confiable por defecto. `critMultiplier` bajo refuerza una curva de daño plana y predecible (rol tanque: DPS estable, no burst) |
| **Kael** (Errantes — rastreo, ranged, percepción) | 55 | 0.18 | 1.9 | Daño medio-bajo por impacto individual. `critChance` medio-alta representa precisión de rastreador — golpea puntos débiles que detecta con su percepción del Resplandor |
| **Sira** (Sanadores — médica, soporte, curación) | 35 | 0.12 | 1.6 | El `baseDamage` más bajo del elenco — su kit de habilidades invierte en curación/soporte, no en daño directo. `critChance` y `critMultiplier` bajos consistentemente: Sira no está diseñada para depender del crítico como fuente de daño |
| **Davan** (Reconstructores — ingeniería, gadgets, tecnología) | 45 | 0.22 | 2.1 | `baseDamage` bajo-medio (su daño real viene de gadgets con `skillMultiplier` alto, no del golpe base). `critChance` y `critMultiplier` notablemente altos: los gadgets tecnológicos golpean puntos críticos/debilidades de blindaje de forma más consistente que un ataque físico |
| **Ren** (Puentes — infiltración, sigilo) | 40 | 0.35 | 2.4 | `critChance` y `critMultiplier` más altos del elenco (cerca del cap de ADR-0001: 0.4 y 2.5) — la fantasía de Ren es el golpe de sigilo/backstab. `baseDamage` bajo compensa: fuera de un golpe crítico, Ren es el linaje más débil en DPS sostenido, empujando al jugador hacia el juego de sigilo en vez de combate frontal |

**Example:** Vael ataca con arma cuerpo a cuerpo básica (`weaponMultiplier` 1.2, sin skill activa `skillMultiplier` 1.0, zona segura `radiationMultiplier` 1.0, sin bestia activa `beastMultiplier` 1.0):
`damageRaw = 85 × 1.2 × 1.0 × 1.0 × 1.0 = 102`. Con 10% de probabilidad de crítico ×1.7 → `115.6` en el golpe crítico, `102` en el golpe normal. Comparar con Ren en las mismas condiciones de arma: `damageRaw = 40 × 1.2 = 48` normal, pero con 35% de probabilidad de `48 × 2.4 = 115.2` — Ren iguala el pico de daño de Vael solo en el crítico, nunca en sostenido.

### 2. Costo de Nodo de Habilidad

El costo de nodo de habilidad se define como:
`costo_puntos = costoBase_rama × (1 + profundidad × factorProfundidad)`

Donde `costoBase_rama` depende de si el nodo pertenece al tronco (obligatorio, más barato para no bloquear progresión básica) o a una rama de especialización (más caro, ya que representa una decisión de build). `profundidad` es la posición del nodo dentro de su secuencia (0-indexed).

**Variables:**
| Variable | Símbolo | Tipo | Rango | Descripción |
|----------|---------|------|-------|-------------|
| Costo base según tipo de nodo | `costoBase_rama` | int | 1 (tronco) / 2 (rama) | Tronco = 1 punto base; Rama = 2 puntos base |
| Profundidad del nodo en su secuencia | `profundidad` | int | 0–3 | 0 = primer nodo de tronco/rama; máximo 3 de profundidad por rama |
| Factor de escalado por profundidad | `factorProfundidad` | float | 0.5 (constante) | Tuning knob: cuánto más caro es cada nodo sucesivo |
| Costo final del nodo | `costo_puntos` | int | 1–8 | Redondeado hacia arriba (`Ceil`) — los puntos de habilidad son unidades discretas |

**Output Range:** 1–8 puntos por nodo (tronco: 1–3, ramas: 2–8).

**Example:**
- Nodo de tronco, profundidad 0: `costo = 1 × (1 + 0 × 0.5) = 1` punto.
- Nodo de tronco, profundidad 2: `costo = 1 × (1 + 2 × 0.5) = 2` puntos.
- Nodo de rama "Furia" de Vael, profundidad 0: `costo = 2 × (1 + 0 × 0.5) = 2` puntos.
- Nodo de rama "Furia" de Vael, profundidad 2 (nodo final, más especializado): `costo = 2 × (1 + 2 × 0.5) = 4` puntos.

Esto produce un árbol donde el tronco completo cuesta ~5 puntos (accesible temprano) y una rama completa cuesta ~9 puntos (inversión significativa, empuja al jugador a comprometerse con una especialización en vez de repartir puntos superficialmente entre ramas).

### 3. Stats Secundarios: HP Base y Stamina Base

Los stats secundarios se definen como valores fijos por linaje (mismo criterio que la sección 1 — inputs directos a `PlayerStats`/`IDamageable`, no derivados en runtime). La distribución sigue una relación inversa con `baseDamage`: el linaje con mayor daño ofensivo (Vael) tiene la mayor supervivencia pasiva (HP), mientras Sira compensa su bajo daño con curación activa, no con un pool de HP grande — reforzando que es "frágil" en combate directo por diseño.

**Variables:**
| Variable | Símbolo | Tipo | Rango | Descripción |
|----------|---------|------|-------|-------------|
| Vida base del linaje | `HP_base` | int | 80–150 | Input a `PlayerStats`/`IDamageable`; consumido por `Health.MaxHealth` |
| Stamina base del linaje | `Stamina_base` | int | 60–120 | Consumido por sistema de resistencia (esquive, sprint, ataques pesados) |

**Output Range:** HP 80–150, Stamina 60–120.

**Tabla de valores por linaje:**

| Linaje | `HP_base` | `Stamina_base` | Justificación de diseño |
|--------|-----------|-----------------|--------------------------|
| **Vael** (tanque) | 170 | 110 | Máximo en ambos stats — debe absorber golpes prolongados y sostener acciones físicas sin agotarse |
| **Kael** | 120 | 100 | Perfil equilibrado con leve sesgo a stamina — necesita resistencia para explorar/evadir |
| **Davan** | 110 | 90 | Perfil medio-bajo en ambos — compensa fragilidad física con gadgets, no con supervivencia bruta |
| **Ren** (sigilo/crit) | 105 | 120 | HP bajo pero máxima stamina del elenco — su supervivencia es evitar el golpe, no resistirlo |
| **Sira** (soporte/médica) | 100 | 60 | Mínimo en ambos stats — no diseñada para primera línea; su baja stamina refuerza que debe jugarse detrás de aliados, apoyándose en curación en vez de esquiva o resistencia física. Piso elevado a 100 (revisión post-borrador) para que no muera en 1-2 golpes ante enemigos tardíos |

**Example:** Vael entra en combate con `HP_base = 170`. Recibe un golpe de un Vaciado con `damageNet = 40` tras defensa → queda en 130/170 (76%), holgado para seguir peleando. Sira en la misma situación con `HP_base = 100` queda en 60/100 (60%) — menos margen que Vael ante el mismo golpe, pero ya no al borde de la muerte con un solo impacto adicional.

## Edge Cases

- **Si el jugador intenta ejecutar un respec sin poseer un Fragmento de Memoria Adaptada**: el botón de respec aparece deshabilitado en la UI con un tooltip indicando el ítem faltante. La acción nunca llega a ejecutarse — no hay estado de error que manejar en el sistema.
- **Si Muerte y Resurrección invoca `RevertLastUnlockedNode()` sobre un árbol con 0 nodos desbloqueados**: la llamada no tiene efecto (no-op). No se puede perder progreso que no existe; relevante para Renacidos recién creados o que acaban de ejecutar un respec completo.
- **Si dos nodos de ramas distintas comparten el mismo nodo de tronco como requisito**: ambos pueden desbloquearse de forma independiente y simultánea si el jugador tiene puntos suficientes para ambos — el diseño no impone exclusividad entre ramas (Core Rules #2).
- **Si el jugador intenta desbloquear un nodo sin puntos de habilidad suficientes**: el nodo permanece en estado Desbloqueable (no se descuenta ni se aplica parcialmente); la UI muestra el costo y los puntos actuales disponibles.
- **Si el jugador ejecuta un respec completo**: todos los nodos del árbol vuelven a estado Bloqueado excepto el primer nodo de tronco (profundidad 0), que vuelve a Desbloqueable de inmediato — evita que el respec deje al jugador sin ninguna acción posible de progresión. Los puntos gastados previamente se devuelven en su totalidad.
- **Si `RevertLastUnlockedNode()` elimina un nodo de tronco que tiene nodos de rama dependientes ya desbloqueados**: los nodos de rama dependientes también vuelven a Bloqueado en cascada (no pueden existir nodos desbloqueados cuyo requisito ya no se cumple). Esto puede revertir más de "1 punto de habilidad" en términos de nodos afectados, aunque solo cuenta como una pérdida narrativa — ver Open Questions si esto requiere ajuste de balance.

## Dependencies

| Sistema | Dirección | Tipo | Interfaz / Nota |
|---------|-----------|------|-------------------|
| — (ninguna) | Upstream | — | Linajes es Foundation, no depende de ningún otro sistema |
| **Progresión de Personaje** | Downstream | Duro | Consume la estructura de nodos de Linajes para gestionar desbloqueo (contrato ya documentado en systems-index.md) |
| **Combate** | Downstream | Duro | Consume `baseDamage`, `critChance`, `critMultiplier` por linaje vía `DamageData` (ADR-0001) |
| **Muerte y Resurrección** | Downstream | Duro | Invoca `RevertLastUnlockedNode()` sobre el árbol activo al morir |
| **Bestias del Vínculo** | Downstream | Duro | Lee el mapeo `linaje → bestia_id`. Clasificado como duro (no blando) porque el vínculo bestia-linaje es un gancho narrativo central del lore (Biblia de Lore, sección "Las Bestias del Vínculo") — sin este mapeo, Bestias del Vínculo pierde su identidad narrativa y se reduce a compañeros genéricos intercambiables |
| **Inventario y Equipo** | Downstream | Duro | Provee el ítem "Fragmento de Memoria Adaptada" que Linajes consume para respec |
| **Sistema de Guardado/Persistencia** | Downstream | Duro | Guardado serializa el estado de desbloqueo del árbol (qué nodos, no los stats). Linajes expone `GetUnlockedNodes()`/`RestoreUnlockedNodes(data)` como su contrato `ISaveable`. Sin Guardado, el progreso del árbol no persiste entre sesiones ni muertes. Ver `design/gdd/sistema-guardado.md` |
| **Menús y UI** | Downstream | Duro | Renderiza el árbol de habilidades y sus estados por nodo |

**Nota de bidireccionalidad**: de estos sistemas downstream, solo **Sistema de Guardado/Persistencia** tiene GDD hasta ahora — su dependencia de Linajes ya está registrada de forma bidireccional (`sistema-guardado.md` lista Linajes en su tabla de Dependencies). El resto aún no tiene GDD; cuando cada uno se diseñe, debe listar "depende de Linajes" y respetar exactamente estos nombres de interfaz (`GetAvailableNodes()`, `UnlockNode(id)`, `RevertLastUnlockedNode()`, `GetUnlockedNodes()`/`RestoreUnlockedNodes(data)`, mapeo `linaje → bestia_id`).

## Tuning Knobs

| Knob | Rango seguro | Qué rompe en los extremos |
|------|--------------|----------------------------|
| `baseDamage` por linaje | 5–200 (rango ADR-0001) | Por debajo de 30: el linaje se vuelve inviable en combate directo sin depender 100% de crítico/skills. Por encima de 150: rompe la curva de Combate diseñada para picos de 100-115 |
| `critChance` por linaje | 0.0–0.4 (rango ADR-0001) | Ver regla de interacción abajo — no tunear de forma aislada |
| `critMultiplier` por linaje | 1.5–2.5 (rango ADR-0001) | Ver regla de interacción abajo — no tunear de forma aislada |
| `HP_base` por linaje | 100–170 | Por debajo de 100: cualquier golpe de enemigo tardío mata en 1-2 hits, ya no da margen de reacción. Por encima de 170: Vael se vuelve intrasanable para el balance de IA de Enemigos (aún no diseñada) |
| `Stamina_base` por linaje | 60–120 | Por debajo de 60: el linaje no puede completar una secuencia básica de esquive+ataque sin quedarse sin stamina |
| `costoBase_rama` (tronco vs. rama) | 1–2 (tronco) / 2–4 (rama) | Si se igualan (tronco = rama): desaparece el incentivo de diseño de "tronco accesible, rama es inversión" |
| `factorProfundidad` | 0.3–0.6 | Por debajo de 0.3: los árboles se vuelven planos, cualquier build cuesta casi lo mismo. Por encima de 0.6: el último nodo de una rama se vuelve prohibitivamente caro y mata la especialización profunda |

**Regla de no-ramas-rotas (guardrail obligatorio):** `critChance × critMultiplier` no puede exceder **0.9** para ningún linaje. Es la salvaguarda contra combinaciones que, sumadas a bestia/radiación/skill del pipeline de Combate (ADR-0001), producirían outliers que rompan `maxDamageClamp`. Verificación de los valores actuales: Ren ya está en el límite (`0.35 × 2.4 = 0.84`, dentro del margen) — cualquier ajuste futuro a Ren debe re-validarse contra este guardrail con `/balance-check` antes de aceptarse.

**Interacciones entre knobs**: `critChance` × `critMultiplier` deben tunearse juntos, nunca por separado (ver guardrail arriba). `factorProfundidad` interactúa con `costoBase_rama`: si ambos suben a la vez, una rama completa puede superar los puntos totales que Progresión de Personaje planea otorgar durante todo el juego — validar cruzado cuando Progresión se diseñe.

## Visual/Audio Requirements

> **Nota de alcance**: el proyecto todavía no tiene un Art Bible formal (se autoría normalmente con `/art-bible` antes del GDD). Esta sección deriva sus guías directamente de la Biblia de Lore y de las reglas ya definidas en este documento (Core Rules, Player Fantasy) — no inventa una dirección de arte nueva. **Debe ser validada y refinada por el Art Bible cuando se autore**, especialmente paletas exactas (hex), estilo de shader/VFX y dirección de iluminación.

### 1. Feedback visual/sonoro de eventos de Linajes

**Desbloqueo de nodo de habilidad:**
- VFX: pulso de luz que se origina en el nodo y recorre las líneas de conexión del árbol hacia el nodo de tronco raíz — refuerza visualmente "esto siempre estuvo conectado a tu identidad", no una "adquisición" externa. Color del pulso = color de acento del linaje (ver sección 2).
- El nodo pasa de atenuado (Bloqueado/Desbloqueable) a iluminado con un leve glow persistente en su estado Desbloqueado — debe distinguirse a simple vista de un nodo solo "disponible".
- Audio: un sonido corto y orgánico (no "campanita" de UI genérica) — sugerido: un latido/pulso grave con una capa de armónico agudo, evocando "recuerdo aflorando" más que "logro desbloqueado". Debe compartir familia sonora entre los 5 linajes (mismo diseño base) pero con el tono/timbre ajustado por linaje para reforzar identidad sonora individual.
- Duración objetivo: 0.6–1.0s, no bloqueante — el jugador debe poder seguir navegando el árbol de inmediato.

**Ejecución de respec (consumo de Fragmento de Memoria Adaptada):**
- Debe leerse como un evento narrativamente más pesado que un desbloqueo individual — es "reescribir la sangre", no "resetear un build". VFX de mayor escala: el árbol completo se desvanece con un efecto de "disolución en partículas" (motivo Resplandor, ver sección 3) antes de reconstruirse desde el nodo de tronco inicial.
- El ítem consumido (Fragmento de Memoria Adaptada) debe tener su propio VFX de consumo distintivo en el momento de confirmación — sugerido: el fragmento se fragmenta en motas de luz verde-doradas que convergen hacia el retrato/avatar del linaje antes de disparar la disolución del árbol.
- Audio: capa distinta y más prolongada que el desbloqueo de nodo (2–3s), con un descenso tonal al inicio (pérdida/borrado) seguido de un ascenso al reconstruirse el primer nodo (recuperación) — refleja la mecánica de "vuelve a Desbloqueable de inmediato" (Edge Cases).
- Requiere confirmación explícita del jugador antes de disparar el VFX completo (es destructivo y no debe animarse por error de clic).

**Diferenciación visual entre los 5 linajes en el árbol de habilidades (UI):**
- Cada árbol usa el color de acento de su linaje (sección 2) de forma consistente en: líneas de conexión, glow de nodos desbloqueados, marco del panel del árbol.
- El icono de cada nodo debe usar un lenguaje de forma compartido entre linajes (mismo set de siluetas base para tronco/rama-A/rama-B) para que la lectura de "qué tipo de nodo es" sea instantánea sin importar el linaje activo — la diferenciación es de color/motivo, no de layout.
- El nodo de vínculo con la Bestia (dato de referencia `linaje → bestia_id`) debe ser visualmente reconocible dentro del árbol aunque Linajes no controle su lógica — sugerido: un ícono silueteado de la bestia correspondiente (Cristalino, Ciervo Luminoso, Lobo Ferroso, Blindado, Espectro) en el nodo de tronco final, sin necesidad de asset 3D final todavía.

### 2. Restricciones de estilo visual y animación por linaje

| Linaje | Paleta de acento (a validar en Art Bible) | Motivo visual recurrente | Nota de animación |
|--------|---------------------------------------------|---------------------------|--------------------|
| **Kael** (Errantes) | Azul eléctrico (ligado a los ojos/cristales de El Cristalino) sobre base neutra | Fragmentos de cristal / facetas angulares en iconografía de nodos | Transiciones de UI más "instantáneas" y precisas (percepción/instinto) — sin easing suave, corte seco |
| **Sira** (Sanadores) | Verde-dorado bioluminiscente (ligado a las astas de El Ciervo Luminoso) | Motivos orgánicos curvos, ramificaciones tipo vena/raíz | Transiciones suaves, pulso lento tipo "respiración" — refuerza rol de soporte/calma |
| **Davan** (Reconstructores) | Ámbar/naranja industrial con acentos metálicos (ligado a placas de El Lobo Ferroso) | Iconografía angular tipo circuito/engranaje | Animación con micro-glitch o "encendido" tecnológico al desbloquear — nunca orgánico |
| **Vael** (Forjados) | Rojo óxido/tierra oscura (ligado a la piel irradiada de El Blindado) | Motivos de placas superpuestas, grietas | Animación de mayor peso — easing lento y pesado, sin rebote ligero |
| **Ren** (Puentes) | Violeta/gris translúcido (ligado a El Espectro) | Formas que se desvanecen en los bordes, semi-transparencia | Transiciones rápidas con fade en vez de movimiento — nunca un desplazamiento sólido, siempre "aparece/desaparece" |

**Restricciones transversales a los 5:**
- Ningún color de acento de linaje puede competir visualmente con el verde-amarillo/verde-dorado reservado para el Resplandor como fenómeno del mundo (radiación) — evitar solapamiento directo con Sira, que ya usa verde-dorado; su tono debe ser claramente más "vital/bioluminiscente" y menos "tóxico" que el Resplandor ambiental. Este conflicto de paleta es candidato prioritario para resolución en el Art Bible.
- La iconografía de nodos no debe usar metáforas de "hoja de stats" (números flotantes, iconos de espada/escudo genéricos) — coherente con Player Fantasy: la progresión se lee como recuerdo del cuerpo, no como estadística.

### 3. Principios de lore que informan el sistema

- **El Resplandor como motivo visual transversal**: el desbloqueo de nodos y el respec deben usar variantes del lenguaje visual del Resplandor (partículas verde-doradas, luz que fluye por líneas/venas) porque el linaje Adaptado es, en la biología del lore, la relación del personaje con el Resplandor — el árbol de habilidades no es una metáfora abstracta de progreso, es una representación de algo físico en la sangre del Renacido.
- **"Recordar en la sangre"**: todo feedback de desbloqueo debe evitar el lenguaje visual de "obtener/comprar" (chispas doradas de recompensa, iconos de check verde genéricos) y favorecer el lenguaje de "revelar/aflorar" (algo que ya estaba ahí se hace visible) — esto es una restricción dura derivada de Player Fantasy, no una preferencia estética.
- **Permanencia del linaje frente a la fragilidad del equipo/conocimiento**: dado que el linaje es la única capa de identidad que sobrevive completa entre expediciones (Overview), su UI y VFX deben transmitir solidez y permanencia (nodos desbloqueados nunca deben "parpadear" o verse frágiles) en contraste deliberado con cómo deberían sentirse, visualmente, el equipo perdido o el conocimiento borrado en otros sistemas (fuera de alcance aquí, pero referencia de contraste para el Art Bible).
- **Contraste Oasis (seguro/verde/fértil) vs. exterior (páramo radiactivo)**: el sistema de Linajes vive en menús (fuera del mundo 3D), por lo que este contraste no aplica directamente a la UI del árbol, pero sí debe respetarse en cualquier escena de fondo o ambientación del menú de personaje si la hubiera — no ambientar el menú de Linajes con paleta de "exterior páramo", ya que el árbol representa identidad interna del personaje, no el entorno.

**Pendiente de validación**: esta sección usa el lore y las reglas del GDD como única fuente disponible. Cuando se autore el Art Bible formal (`/art-bible`), debe: (a) fijar valores hex exactos para las 5 paletas de acento y resolver el conflicto Sira/Resplandor señalado arriba, (b) confirmar o ajustar el lenguaje de VFX de "disolución en partículas" contra el estilo de shader/VFX general del proyecto, y (c) validar que los 5 motivos visuales recurrentes sean consistentes con el diseño de las Bestias del Vínculo cuando ese sistema se diseñe.

## UI Requirements

- **Pantalla de árbol de habilidades**: una vista por linaje (accesible solo para el linaje activo o los 5 en un selector de tabs), mostrando tronco + ramas con layout de grafo dirigido (no lista plana) — refleja la estructura de dependencias real definida en Core Rules.
- **Estado visual por nodo**: Bloqueado (atenuado, no interactuable), Desbloqueable (resaltado, interactuable), Desbloqueado (glow persistente) — mapea 1:1 a los estados de la sección States and Transitions.
- **Tooltip por nodo** (hover/tap): nombre, efecto, costo en puntos, requisitos pendientes si está Bloqueado.
- **Contador de puntos de habilidad disponibles**, visible permanentemente en la pantalla del árbol.
- **Botón de Respec**: deshabilitado si el jugador no posee un Fragmento de Memoria Adaptada (Edge Cases); si está habilitado, requiere un **modal de confirmación explícita** antes de ejecutar (es destructivo, según Visual/Audio Requirements).
- **Indicador del vínculo de Bestia** en el nodo de tronco final: ícono de la bestia correspondiente, sin tooltip de mecánica (esa lógica pertenece a Bestias del Vínculo, aún no diseñado) — solo referencia visual.
- Esta pantalla es responsabilidad de implementación del sistema **Menús y UI** (Presentation, no diseñado) — Linajes solo expone los datos que esta UI consume (ver Dependencies).

> 📌 **UX Flag — Linajes**: Este sistema tiene requisitos de UI reales (pantalla de árbol de habilidades completa). En Fase 4 (Pre-Producción), correr `/ux-design` para crear una spec de UX de esta pantalla **antes** de escribir epics/historias. Las historias que referencien esta UI deben citar `design/ux/arbol-habilidades.md`, no este GDD directamente.

## Acceptance Criteria

### Árbol de habilidades: estructura tronco + ramas

**AC-1 — Tronco lineal obligatorio**
GIVEN un linaje recién creado (0 nodos desbloqueados)
WHEN se consulta `GetAvailableNodes()`
THEN solo el nodo de tronco de profundidad 0 aparece en estado Desbloqueable, y ningún nodo de rama aparece en estado Desbloqueable (todos los nodos de rama están en Bloqueado).

**AC-2 — Orden lineal del tronco sin elección**
GIVEN un árbol de linaje donde el tronco tiene nodos de profundidad 0, 1, 2 y 3 (según el linaje tenga 3 o 4 nodos de tronco)
WHEN el jugador desbloquea el nodo de tronco de profundidad `N`
THEN únicamente el nodo de tronco de profundidad `N+1` pasa de Bloqueado a Desbloqueable; ningún otro nodo de tronco cambia de estado, y no existe ninguna opción de UI que permita elegir entre dos nodos de tronco distintos en el mismo paso.

**AC-3 — Requisito doble de nodo de rama (rama anterior Y tronco correspondiente)**
GIVEN un nodo de rama de profundidad 1 (ej. "Furia" profundidad 1 de Vael) cuyo requisito declarado es [nodo de rama "Furia" profundidad 0 desbloqueado] Y [nodo de tronco correspondiente desbloqueado]
WHEN solo uno de los dos requisitos está cumplido
THEN el nodo permanece en estado Bloqueado y `UnlockNode(id)` rechaza la operación.

**AC-4 — Ramas independientes, sin exclusividad**
GIVEN dos nodos de ramas distintas del mismo linaje (ej. Vael "Resistencia" profundidad 0 y Vael "Furia" profundidad 0) que comparten el mismo nodo de tronco como requisito, y el nodo de tronco requerido ya está desbloqueado
WHEN el jugador tiene puntos de habilidad suficientes para desbloquear ambos nodos de rama
THEN puede desbloquear ambos de forma independiente y simultánea (en cualquier orden), y el sistema no impone ningún bloqueo de exclusividad entre "Resistencia" y "Furia".

**AC-5 — Cantidad de ramas por linaje**
GIVEN cualquiera de los 5 linajes
WHEN se inspecciona la definición completa de su árbol de habilidades
THEN el árbol contiene entre 2 y 3 ramas de especialización (además del tronco), y el tronco contiene entre 3 y 4 nodos.

### Costo de nodos y gasto de puntos

**AC-6 — Costo de nodo de tronco, profundidad 0**
GIVEN un nodo de tronco de cualquier linaje en profundidad 0
WHEN se calcula su `costo_puntos` con `costoBase_rama = 1` y `factorProfundidad = 0.5`
THEN el resultado es exactamente `1` punto (`1 × (1 + 0 × 0.5) = 1`).

**AC-7 — Costo de nodo de tronco, profundidad 2**
GIVEN un nodo de tronco de cualquier linaje en profundidad 2
WHEN se calcula su `costo_puntos` con `costoBase_rama = 1` y `factorProfundidad = 0.5`
THEN el resultado es exactamente `2` puntos (`1 × (1 + 2 × 0.5) = 2`).

**AC-8 — Costo de nodo de rama, profundidad 0 y profundidad 2**
GIVEN un nodo de rama (ej. rama "Furia" de Vael) en profundidad 0
WHEN se calcula su `costo_puntos` con `costoBase_rama = 2` y `factorProfundidad = 0.5`
THEN el resultado es exactamente `2` puntos (`2 × (1 + 0 × 0.5) = 2`);
AND GIVEN el nodo final de la misma rama en profundidad 2
WHEN se aplica la misma fórmula
THEN el resultado es exactamente `4` puntos (`2 × (1 + 2 × 0.5) = 4`).

**AC-9 — Redondeo hacia arriba del costo**
GIVEN un nodo cuyo cálculo de `costo_puntos` produce un valor no entero
WHEN el sistema calcula el costo final
THEN el valor se redondea hacia arriba con `Ceil` (ej. `1.3` → `2`), nunca hacia abajo ni al entero más cercano.

**AC-10 — Bloqueo de desbloqueo sin puntos suficientes**
GIVEN un nodo Desbloqueable con `costo_puntos = 4` y el jugador tiene `3` puntos de habilidad disponibles
WHEN el jugador invoca `UnlockNode(id)` sobre ese nodo
THEN la operación es rechazada, el nodo permanece en estado Desbloqueable, no se descuenta ningún punto, y la UI muestra el costo (`4`) junto con los puntos actuales disponibles (`3`).

### Respec (reasignación)

**AC-11 — Respec exitoso con ítem disponible**
GIVEN un jugador que posee al menos 1 Fragmento de Memoria Adaptada y un árbol de linaje con nodos desbloqueados
WHEN el jugador ejecuta la acción de respec
THEN se consume 1 Fragmento de Memoria Adaptada del inventario, todos los nodos del árbol vuelven a estado Bloqueado excepto el nodo de tronco de profundidad 0 (que pasa a Desbloqueable de inmediato), y todos los puntos de habilidad gastados previamente se devuelven en su totalidad.

**AC-12 — Botón de respec deshabilitado sin ítem**
GIVEN un jugador que posee 0 unidades de Fragmento de Memoria Adaptada
WHEN el jugador abre la pantalla de árbol de habilidades de un linaje
THEN el botón de respec aparece deshabilitado con un tooltip que indica el ítem faltante, y ninguna llamada a la lógica de respec se ejecuta ni produce un estado de error en el sistema.

**AC-13 — Respec nunca deja el árbol sin acción de progresión disponible**
GIVEN un árbol de linaje inmediatamente después de un respec completo (AC-11)
WHEN se consulta `GetAvailableNodes()`
THEN al menos un nodo (el nodo de tronco de profundidad 0) está en estado Desbloqueable, nunca en estado Bloqueado.

### Reversión de nodo por muerte (`RevertLastUnlockedNode`)

**AC-14 — Reversión simple del último nodo desbloqueado**
GIVEN un árbol de linaje donde el último nodo desbloqueado es un nodo de tronco sin nodos de rama dependientes ya desbloqueados
WHEN Muerte y Resurrección invoca `RevertLastUnlockedNode()`
THEN ese nodo pasa de Desbloqueado a Desbloqueable, ningún otro nodo del árbol cambia de estado, y el resto del progreso se conserva intacto.

**AC-15 — No-op sobre árbol vacío**
GIVEN un árbol de linaje con 0 nodos desbloqueados
WHEN Muerte y Resurrección invoca `RevertLastUnlockedNode()`
THEN la llamada no tiene ningún efecto (no-op): ningún nodo cambia de estado, no se generan errores, y el estado del árbol antes y después de la llamada es idéntico.

**AC-16 — Cascada de bloqueo al revertir un nodo de tronco con dependientes de rama**
GIVEN un árbol de linaje donde un nodo de tronco tiene uno o más nodos de rama dependientes ya desbloqueados
WHEN `RevertLastUnlockedNode()` revierte ese nodo de tronco específico
THEN el nodo de tronco pasa a Desbloqueable, Y todos los nodos de rama dependientes que ya no cumplen sus requisitos pasan en cascada a estado Bloqueado, incluso si esto revierte más de un nodo en total.

### Mapeo linaje → bestia

**AC-17 — Mapeo estático de solo lectura por linaje**
GIVEN los 5 linajes definidos (Kael, Sira, Davan, Vael, Ren)
WHEN se consulta el mapeo `linaje → bestia_id` expuesto por Linajes
THEN cada uno de los 5 linajes retorna exactamente un `bestia_id` fijo y no nulo, el mapeo no contiene lógica condicional, y Linajes no expone ninguna mecánica de comportamiento de la Bestia del Vínculo asociada.

### Stats de combate por linaje (valores fijos)

**AC-18 — Vael**: `baseDamage = 85`, `critChance = 0.10`, `critMultiplier = 1.7`, dentro de rangos ADR-0001.
**AC-19 — Kael**: `baseDamage = 55`, `critChance = 0.18`, `critMultiplier = 1.9`.
**AC-20 — Sira**: `baseDamage = 35`, `critChance = 0.12`, `critMultiplier = 1.6` — el valor más bajo de `baseDamage` entre los 5.
**AC-21 — Davan**: `baseDamage = 45`, `critChance = 0.22`, `critMultiplier = 2.1`.
**AC-22 — Ren**: `baseDamage = 40`, `critChance = 0.35`, `critMultiplier = 2.4` — los valores más altos de `critChance` y `critMultiplier` entre los 5.

### Stats secundarios: HP base y Stamina base

**AC-23 — Vael**: `HP_base = 170`, `Stamina_base = 110` — ambos máximos del elenco.
**AC-24 — Kael**: `HP_base = 120`, `Stamina_base = 100`.
**AC-25 — Davan**: `HP_base = 110`, `Stamina_base = 90`.
**AC-26 — Ren**: `HP_base = 105`, `Stamina_base = 120` — Stamina máxima del elenco.
**AC-27 — Sira**: `HP_base = 100`, `Stamina_base = 60` — ambos mínimos del elenco.

### Guardrail de balance

**AC-28 — `critChance × critMultiplier` no excede 0.9 para ningún linaje**
GIVEN los 5 linajes con sus valores de `critChance` y `critMultiplier` definidos
WHEN se calcula `critChance × critMultiplier` para cada uno
THEN ningún resultado excede `0.9`: Vael = `0.17`, Kael = `0.342`, Sira = `0.192`, Davan = `0.462`, Ren = `0.84` (el más cercano al límite, dentro de margen). Cualquier ajuste futuro que produzca un resultado > 0.9 debe rechazarse hasta re-validarse con `/balance-check`.

## Open Questions

1. **Economía de puntos de Progresión de Personaje no definida** — los criterios de aceptación de este GDD asumen "puntos disponibles" como precondición dada, pero cuántos puntos existen y cuándo se otorgan depende de Progresión de Personaje (no diseñado aún). *Dueño: game-designer/systems-designer. Resolver al diseñar Progresión de Personaje.*
2. **Texto exacto del tooltip de respec deshabilitado no definido** — UI Requirements especifica el comportamiento pero no el copy. *Dueño: ux-designer / writer. Resolver en `/ux-design` de la pantalla de árbol de habilidades.*
3. **Interacción `factorProfundidad` × `costoBase_rama` vs. presupuesto total de puntos de Progresión** — el GDD advierte que una rama completa podría superar los puntos totales que Progresión planea otorgar durante todo el juego, pero ese total no existe todavía. *Dueño: systems-designer. Resolver al diseñar Progresión de Personaje, validar con `/consistency-check`.*
4. **Ambigüedad en "Perdido por Muerte → Recuperado"** (States and Transitions) — aclarar si "recuperar" un nodo revertido es idéntico a un desbloqueo normal (Desbloqueable → Desbloqueado) o si implica un flujo de UI/sistema distinto. Recomendación: tratarlo como idéntico a un desbloqueo normal, sin estado especial. *Dueño: game-designer. Resolver antes de implementación.*
5. **Validación pendiente de Visual/Audio Requirements contra Art Bible formal** — paletas exactas (hex), conflicto de color Sira/Resplandor, y consistencia con Bestias del Vínculo quedan pendientes hasta correr `/art-bible`. *Dueño: art-director. Resolver antes de producción de assets.*
