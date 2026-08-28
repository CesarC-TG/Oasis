# Sistema de Guardado/Persistencia

> **Status**: In Design
> **Author**: user + agents
> **Last Updated**: 2026-07-07
> **Implements Pillar**: El precio de sobrevivir (la muerte cuesta) / Descubrir la verdad (progreso narrativo persistente)

## Overview

El Sistema de Guardado/Persistencia serializa el estado completo de la partida del jugador a almacenamiento persistente y lo restaura de forma fiable al cargar, garantizando que ningún progreso se pierda entre sesiones ni entre muertes. Como capa de datos, es la fuente de verdad de todo lo que cambia durante el juego: nodos de habilidad desbloqueados por linaje, inventario y equipo, reputación de facciones, flags de la máquina de estado narrativo, valores de relación de personajes, vínculos de bestia y flags de diálogo. Su responsabilidad no es solo *escribir* esos datos, sino *restaurar el mundo a un estado limpio* antes de aplicarlos — reseteando el estado estático y los suscriptores de eventos (como el `CombatEventBus` de ADR-0001) para que una carga nunca arrastre residuos de la sesión anterior. A nivel de jugador, el sistema es invisible cuando funciona: su efecto es la continuidad — la certeza de que salir del juego, morir en el exterior, o volver al Oasis nunca borra lo que costó ganar. Es un sistema Foundation del que dependen prácticamente todos los sistemas con estado; su fiabilidad es un requisito, no una característica.

> **Nota de alcance**: el formato concreto de serialización, la ruta de archivos y el versionado de esquema son decisiones de implementación que irán a un ADR dedicado (`/architecture-decision`), no a este GDD. Este documento describe *qué* se guarda y *cuándo*; el ADR describirá *cómo* se serializa.

## Player Fantasy

Este es un sistema de infraestructura: el jugador no interactúa con él directamente ni debe pensar en él. La única "fantasía" relevante es la **ausencia de ansiedad** — el jugador nunca debe preguntarse "¿se guardó mi progreso?" ni temer perder horas de avance por un cierre inesperado o una muerte. La fantasía que Guardado *habilita* (pero no encarna) es el peso de la muerte del juego: para que "morir cuesta equipo y conocimiento" (pilar *El precio de sobrevivir*) tenga significado, el estado antes y después de morir debe persistir con exactitud — la pérdida debe ser real y recordada, nunca un glitch. El sistema cumple su fantasía siendo invisible y absolutamente fiable.

*(Nota: `creative-director` no consultado — modo Lean, y el sistema es infraestructura sin fantasía directa. La experiencia emocional real vive en Muerte y Resurrección, no aquí.)*

## Detailed Design

### Core Rules

1. **Un único slot canónico por partida.** Existe exactamente un archivo de guardado activo por partida (`save canónico`). Tanto el autoguardado como el guardado manual escriben sobre ese mismo archivo — no se ramifican en múltiples puntos de restauración.
2. **Copia de seguridad rotativa (anti-corrupción).** Antes de sobrescribir el save canónico, el sistema copia el archivo actual a un backup rotativo (mínimo 1 nivel). Si un guardado falla o el archivo canónico se detecta corrupto al cargar, el sistema ofrece restaurar desde el backup. El backup **no** es accesible al jugador como "partida anterior" — es infraestructura de recuperación, no save scumming.
3. **Autoguardado en momentos clave.** El sistema guarda automáticamente en estos disparadores: (a) cruzar El Paso hacia el exterior, (b) volver al Oasis, (c) morir y revivir en la Cámara de Jano, (d) completar un hito narrativo, (e) al cerrar el juego de forma limpia (quit). El jugador no invoca estos guardados.
4. **Guardado manual = guardar y salir seguro.** El jugador puede forzar un guardado desde el menú de pausa. Escribe al save canónico (igual que el autoguardado), no crea un slot separado. Su propósito es permitir cerrar el juego con la certeza de que el estado actual quedó grabado, no crear un punto de restauración.
5. **La muerte se graba.** El autoguardado del disparador (c) captura el estado *posterior* a la muerte (equipo perdido, punto de habilidad revertido, respawn en la Cámara). No existe ningún mecanismo para recargar un estado anterior a la muerte — la penalización es permanente (pilar *El precio de sobrevivir*).
6. **La carga restaura a estado limpio antes de aplicar datos.** Al cargar, el sistema primero resetea todo el estado estático y los suscriptores de eventos (incluido `CombatEventBus`, ADR-0001), y solo entonces aplica los datos serializados. Una carga nunca arrastra residuos de la sesión de juego anterior.
7. **Alcance de la serialización.** El save canónico contiene: nodos de habilidad desbloqueados por linaje activo, inventario y equipo, reputación de facciones, flags de la máquina de estado narrativo, valores de relación de personajes, vínculos de bestia activos, flags de diálogo, posición/zona actual del jugador, y metadatos (versión de esquema, timestamp, linaje elegido). **No** serializa datos de diseño estáticos (stats base de linaje, definiciones de árbol) — esos viven en config.
8. **Guardado atómico.** La escritura del save nunca deja el archivo canónico a medias: se escribe a un archivo temporal y se renombra sobre el canónico solo tras completarse con éxito. Un corte de energía a mitad de guardado deja intacto el save anterior.

### States and Transitions

| Estado | Condición de Entrada | Condición de Salida | Comportamiento |
|--------|----------------------|----------------------|----------------|
| **Idle** | Sistema inicializado, sin operación pendiente | Se dispara un guardado o carga | Escucha disparadores; no hace I/O |
| **Saving** | Disparador de autoguardado o guardado manual | Escritura atómica completada → Idle; fallo → SaveError | Serializa estado, escribe a temp, rota backup, renombra atómicamente |
| **Loading** | El jugador carga la partida o arranca continuación | Restauración completa → Idle; archivo corrupto → CorruptionRecovery | Resetea estado estático, deserializa, aplica datos al mundo |
| **CorruptionRecovery** | Save canónico corrupto/ilegible al cargar | Backup válido restaurado → Loading; sin backup válido → LoadFailed | Ofrece restaurar desde backup rotativo |
| **SaveError** | Fallo de I/O durante Saving (disco lleno, permisos) | Reintento exitoso → Idle; abandono → Idle (con save anterior intacto) | Notifica al jugador; el save canónico previo queda intacto (garantía atómica) |
| **LoadFailed** | Ni canónico ni backup son válidos | Jugador inicia nueva partida o sale | Notifica pérdida; no fabrica estado falso |

### Interactions with Other Systems

| Sistema | Dirección | Interfaz |
|---------|-----------|----------|
| **Linajes** (Foundation, diseñado) | Linajes → Guardado | Guardado serializa el estado de desbloqueo del árbol (qué nodos, no los stats). Expone `GetUnlockedNodes()` / `RestoreUnlockedNodes(data)`. |
| **Muerte y Resurrección** (Foundation, no diseñado) | Muerte → Guardado | Al revivir, Muerte dispara el autoguardado (c). El save captura el estado post-penalización. |
| **Inventario y Equipo** (Core, no diseñado) | Inventario ↔ Guardado | Guardado serializa contenido de inventario/equipo; Inventario expone su estado serializable y un método de restauración. |
| **Facciones y Reputación** (Feature, no diseñado) | Facciones → Guardado | Guardado serializa valores de reputación por facción. |
| **Máquina de Estado Narrativo** (Feature, no diseñado) | MEN → Guardado | Guardado serializa flags narrativos y estado de progresión de la historia. |
| **Relaciones de Personaje** (Feature, no diseñado) | Relaciones → Guardado | Guardado serializa valores de afinidad por personaje. |
| **Bestias del Vínculo** (Core, no diseñado) | Bestias → Guardado | Guardado serializa qué vínculo(s) de bestia están activos. |
| **Diálogos** (Feature, no diseñado) | Diálogos → Guardado | Guardado serializa flags de diálogo ya visto/elegido. |
| **Gestión de Escenas/Niveles** (Core, no diseñado) | Escenas ↔ Guardado | Guardado registra zona/escena actual; al cargar, Escenas transiciona a la zona guardada antes de aplicar el resto del estado. |
| **`CombatEventBus`** (ADR-0001, implementado) | Guardado → Combat | Al cargar, Guardado invoca el reseteo del estado estático de eventos antes de aplicar datos (evita suscriptores residuales). |

**Contrato transversal (patrón `ISaveable`)**: cada sistema con estado persistente expone su estado serializable y un método de restauración a través de una interfaz común (a definir su forma exacta en el ADR de serialización). Guardado orquesta la recolección y la restauración; **no** conoce la estructura interna de cada sistema — solo invoca el contrato. Esto marca el límite diseño/implementación: el GDD fija *qué* sistemas participan y *qué* datos aportan; el ADR fijará *cómo* se define y serializa el contrato.

## Formulas

> **Nota:** este sistema es infraestructura de persistencia — no contiene fórmulas de balance de gameplay. Los parámetros de abajo son presupuestos operativos (tiempo, conteo, ventanas temporales) que rigen *cómo* y *cuándo* se ejecuta la I/O de guardado, no matemática de diseño. Los valores concretos son *tuning knobs* (ver sección correspondiente); aquí se fija su forma y rango seguro.

### F1 — Debounce / coalescencia de autoguardado

Cuando varios disparadores de autoguardado ocurren dentro de una ventana corta, el sistema **no** escribe a disco por cada uno. Colapsa (coalesce) los disparadores en una sola escritura y respeta una ventana mínima entre escrituras reales.

```
Δt = t_disparador − t_ultimo_guardado_real

SI Δt ≥ W_debounce:
    ejecutar guardado inmediatamente; t_ultimo_guardado_real = t_disparador
SI NO (Δt < W_debounce):
    marcar guardado_pendiente = true
    (disparadores adicionales dentro de la ventana NO añaden trabajo:
     el estado se serializa una sola vez en el flush — "last-write-wins")
    programar flush en t = t_ultimo_guardado_real + W_debounce

FLUSH (en t_ultimo_guardado_real + W_debounce) SI guardado_pendiente:
    ejecutar guardado con el estado ACTUAL
    guardado_pendiente = false; t_ultimo_guardado_real = t_flush
```

**Excepciones que ignoran el debounce (flush forzado inmediato):**
- Disparador (c) **muerte/revivir** — la penalización debe grabarse sí o sí, sin coalescencia.
- Disparador (e) **quit limpio** — al cerrar no hay "después" donde hacer flush; se fuerza y se espera a que complete.

| Símbolo | Tipo | Rango | Descripción |
|---|---|---|---|
| `W_debounce` | float (s) | 3.0 – 10.0 (default **5.0**) | Ventana mínima entre dos escrituras reales a disco. |
| `Δt` | float (s) | ≥ 0 | Tiempo transcurrido desde la última escritura real. |
| `t_disparador` | float (s) | tiempo de juego | Instante en que entra un disparador de autoguardado. |
| `guardado_pendiente` | bool | {true, false} | Hay ≥1 disparador coalescido esperando flush. |
| `es_critico` | bool | {true, false} | El disparador es (c) o (e); ignora la ventana. |

**Output range:** frecuencia máxima de escritura real = `1 / W_debounce` → con default 5.0 s, **máx. 1 escritura cada 5 s** (excepto flushes críticos). Escrituras ahorradas en una ráfaga de *n* disparadores dentro de la ventana = *n − 1*.

**Ejemplo:** El jugador cruza El Paso en `t=100.0` (Δt grande → guardado inmediato, `t_ultimo=100.0`). A los `t=101.2` dispara un hito narrativo (Δt=1.2 < 5.0 → `guardado_pendiente=true`, flush programado a `t=105.0`). A los `t=103.5` recoge un ítem que dispara otro autoguardado (dentro de la ventana → no añade trabajo). En `t=105.0` se ejecuta **un solo** guardado con el estado de `t=105.0`. Resultado: 3 disparadores → 2 escrituras reales, no 3.

### F2 — Rotación de backup

Antes de sobrescribir el save canónico, el canónico actual se rota a backup. El sistema mantiene **N = 2 niveles** de backup rotativo (`.bak1` = más reciente, `.bak2` = anterior).

```
descartar .bak2                (el más viejo se pierde)
.bak1  → .bak2                 (renombrado)
canónico → .bak1               (renombrado; el canónico "viejo" se preserva)
temp    → canónico             (rename atómico; ver Core Rule 8)
```

Profundidad total de recuperación: **3 estados en disco** (canónico + 2 backups).

| Símbolo | Tipo | Rango | Descripción |
|---|---|---|---|
| `N_backup` | int | 1 – 3 (fijo **2**) | Niveles de backup rotativo mantenidos. |
| `estados_en_disco` | int | `N_backup + 1` = 3 | Total de copias recuperables simultáneas. |

**Justificación del número (contra "un solo slot, la muerte se graba"):**
- **N=0** rechazado: sin backup, una corrupción del canónico = partida perdida (viola Core Rule 2).
- **N=1** cubre el caso común, pero si la corrupción se propaga a `.bak1` en el mismo ciclo defectuoso, no hay red.
- **N=2 (elegido):** protege contra corrupción en cadena de un ciclo (canónico + `.bak1` dañados → aún queda `.bak2`), a coste de disco despreciable (~pocos MB).
- **N≥3 rechazado:** cada nivel adicional es un punto de restauración más viejo que un jugador podría copiar manualmente para hacer *save scumming* (violando Core Rule 5). N=2 mantiene la profundidad histórica corta (segundos-minutos de juego) y los backups **no** se exponen en UI.

**Output range:** el sistema tolera la pérdida/corrupción de hasta 2 archivos consecutivos (canónico + `.bak1`) sin perder la partida. Fallo total solo si los 3 son ilegibles → estado `LoadFailed`.

**Ejemplo:** Guardados sucesivos S1, S2, S3, S4. Tras S4 en disco hay: `canónico=S4`, `.bak1=S3`, `.bak2=S2` (S1 descartado). Si `S4` está corrupto → `CorruptionRecovery` restaura `.bak1=S3`. Si S4 y S3 están corruptos → restaura `.bak2=S2`. Retroceso máximo: 2 guardados, no una sesión completa antes de morir.

### F3 — Presupuesto de tiempo de guardado

Un guardado no debe congelar el frame. A 60 fps el presupuesto es 16.6 ms; la operación debe caber en un sub-presupuesto y, si excede, degradarse a asíncrono en vez de bloquear.

```
Fase 1 — SNAPSHOT (hilo principal, síncrono):
    recolectar estado serializable de cada ISaveable → objeto en memoria
    presupuesto objetivo: T_snapshot ≤ 4 ms  (≤ 25% del frame)

Fase 2 — SERIALIZAR + ESCRIBIR (hilo de fondo, asíncrono):
    convertir snapshot → bytes, escribir a temp, rotar backup, rename atómico
    NO corre en el hilo principal; no cuenta contra el frame budget de render

REGLA DE EXCESO:
    SI T_snapshot medido > T_snapshot_max (8 ms) en un frame:
        registrar advertencia de performance (telemetría de dev)
        el snapshot se completa igualmente (no se aborta a medias)
        marcar para revisión: fragmentar la recolección en el próximo ciclo

    Caso (e) QUIT: se permite bloqueo síncrono total hasta completar Fase 2
        (no hay frames que proteger al cerrar), con timeout T_quit_max.
```

Solo la **Fase 1** toca el hilo principal, porque el snapshot debe ser coherente con el estado del juego en un instante (no puede mutar mientras se lee). La **Fase 2** (el I/O caro) es asíncrona por defecto.

| Símbolo | Tipo | Rango | Descripción |
|---|---|---|---|
| `T_snapshot` | float (ms) | objetivo ≤ **4.0** | Tiempo en hilo principal recolectando el snapshot. |
| `T_snapshot_max` | float (ms) | **8.0** (≈ 50% frame) | Umbral duro; por encima se registra advertencia. |
| `T_frame` | float (ms) | 16.6 (60 fps) | Presupuesto de frame de referencia. |
| `T_quit_max` | float (s) | 2.0 – 5.0 (default **3.0**) | Timeout máximo de bloqueo síncrono al cerrar. |
| `hilo` | enum | {principal, fondo} | Fase 1 = principal; Fase 2 = fondo. |

**Output range:** en operación normal el jugador percibe como máximo **≤ 4 ms** de coste en un único frame (micro-hitch imperceptible, < ¼ de frame), nunca el coste completo de la escritura a disco. Peor caso tolerado sin bug: 8 ms (medio frame). Por encima → advertencia de telemetría, no crash.

**Ejemplo:** Autoguardado en `t=105.0`. Fase 1 recolecta inventario, flags narrativos, nodos de linaje, reputación, etc. en **3.1 ms** (dentro de objetivo) — ese frame renderiza en 13.5 ms restantes, sin caída de fps. El snapshot (≈ pocos cientos de KB, holgado contra el techo de 4 GB) pasa a un hilo de fondo que serializa y escribe en ≈ 12 ms de tiempo de pared repartidos en varios frames. Al hacer **quit**, el juego muestra "Guardando…" y bloquea hasta completar (o hasta `T_quit_max = 3.0 s`).

## Edge Cases

- **Si el jugador cierra el juego (quit) mientras un autoguardado asíncrono (Fase 2) está en curso**: el quit fuerza un flush síncrono (F1 excepción) y espera a que la escritura pendiente complete, hasta `T_quit_max` (3.0s). Si se agota el timeout, se conserva el último save canónico íntegro (garantía atómica) y se descarta la escritura incompleta.
- **Si un autoguardado se dispara mientras otra escritura ya está en curso**: solo se permite una operación de escritura a la vez. El nuevo disparo se marca `guardado_pendiente` y se coalesce vía el debounce de F1 cuando la escritura actual termina — nunca hay dos escrituras solapadas sobre el mismo archivo.
- **Si un corte de energía o crash ocurre a mitad de la escritura del archivo temporal**: el save canónico anterior queda intacto (nunca se tocó — la escritura va a temp y solo se renombra al completarse). El temp huérfano se descarta en el siguiente arranque.
- **Si el save canónico está corrupto o falla la validación de esquema/integridad al cargar** (incluye manipulación externa manual): el sistema entra en `CorruptionRecovery` y ofrece restaurar desde `.bak1`; si `.bak1` también falla, desde `.bak2`. No hay anti-cheat activo (single-player), pero un save malformado nunca debe crashear el juego — se trata exactamente como un archivo dañado.
- **Si ni el canónico ni ambos backups son válidos**: se entra en `LoadFailed`. El sistema notifica la pérdida con un mensaje claro y ofrece iniciar nueva partida — **nunca fabrica un estado falso** ni carga datos parciales.
- **Si el save proviene de una versión de esquema anterior conocida**: se migra al esquema actual durante la carga (el save lleva su número de versión en los metadatos). El mecanismo concreto de migración se define en el ADR de serialización.
- **Si el save proviene de una versión de esquema desconocida o incompatible** (ej. save de una versión más nueva del juego, o versión no migrable): se rechaza con aviso claro en vez de intentar cargar datos que podrían quedar corruptos a medias. Se trata como `LoadFailed` controlado, no como corrupción silenciosa.
- **Si el disco está lleno o sin permisos de escritura durante un guardado**: la operación entra en `SaveError`, el save canónico previo queda intacto (atómico), y se notifica al jugador para que libere espacio. El juego no se bloquea ni pierde el estado en memoria — el jugador puede reintentar.
- **Si dos disparadores críticos (muerte y quit) ocurren en el mismo frame**: ambos fuerzan flush inmediato; se resuelven en orden de llegada, pero como ambos serializan el *estado actual* (last-write-wins), el resultado es un único save coherente con el estado final del frame. No hay conflicto de datos.
- **Si el jugador muere durante una expedición y el autoguardado (c) falla por `SaveError`**: la penalización de muerte ya se aplicó en memoria (equipo perdido, nodo revertido); el sistema reintenta el guardado. Si el jugador cierra sin que el guardado tenga éxito, al recargar vuelve al último estado guardado válido — que puede ser anterior a la muerte. Esto se acepta como comportamiento a favor del jugador ante un fallo de I/O genuino (no es save scumming intencional, es tolerancia a fallo de disco).

## Dependencies

| Sistema | Dirección | Tipo | Interfaz / Nota |
|---------|-----------|------|-------------------|
| — (ninguna) | Upstream | — | Guardado es Foundation, no depende de ningún sistema para existir |
| **Linajes** | Downstream | Duro (para Linajes) | Linajes necesita Guardado para persistir nodos desbloqueados. Guardado expone el contrato; Linajes aporta `GetUnlockedNodes()`/`RestoreUnlockedNodes()` |
| **Muerte y Resurrección** | Downstream | Duro (mutuo) | Muerte dispara el autoguardado (c) y necesita Guardado para que la penalización persista. Guardado necesita el evento de revivir de Muerte para el disparador (c) |
| **Inventario y Equipo** | Downstream | Duro (para Inventario) | Inventario necesita Guardado para persistir contenido/equipo |
| **Facciones y Reputación** | Downstream | Duro (para Facciones) | Reputación por facción persiste vía Guardado |
| **Máquina de Estado Narrativo** | Downstream | Duro (para MEN) | Flags y progresión narrativa persisten vía Guardado |
| **Relaciones de Personaje** | Downstream | Duro (para Relaciones) | Valores de afinidad persisten vía Guardado |
| **Bestias del Vínculo** | Downstream | Duro (para Bestias) | Vínculos activos persisten vía Guardado |
| **Diálogos** | Downstream | Duro (para Diálogos) | Flags de diálogo visto/elegido persisten vía Guardado |
| **Gestión de Escenas/Niveles** | Downstream | Duro (mutuo) | Guardado registra la zona actual; al cargar necesita que Escenas transicione a la zona guardada antes de aplicar el resto del estado |
| **`CombatEventBus`** (ADR-0001) | Downstream | Duro (para la carga) | Al cargar, Guardado debe invocar el reseteo del estado estático de eventos antes de aplicar datos |

**Nota de dureza asimétrica**: desde la perspectiva de *Guardado*, todas sus dependencias son **blandas** — el sistema puede serializar/restaurar con cualquier subconjunto de `ISaveable` presentes; si un sistema aún no existe, simplemente no aporta datos. Desde la perspectiva de *cada sistema con estado*, la dependencia de Guardado es **dura** — sin él, su progreso no persiste. Por eso la columna marca "Duro (para X)".

**Nota de bidireccionalidad**: ninguno de estos sistemas downstream tiene GDD todavía salvo Linajes. Cuando cada uno se diseñe, debe listar "depende de Sistema de Guardado" y exponer su contrato `ISaveable`. El GDD de Linajes debe actualizarse para reflejar esta dependencia (acción pendiente registrada en Open Questions).

## Tuning Knobs

| Knob | Rango seguro | Default | Qué rompe en los extremos |
|------|--------------|---------|----------------------------|
| `W_debounce` (ventana de debounce de autoguardado) | 3.0 – 10.0 s | 5.0 s | Por debajo de 3s: escrituras a disco demasiado frecuentes, riesgo de micro-hitches acumulados y desgaste de disco. Por encima de 10s: ventana de pérdida grande ante un crash (el jugador puede perder hasta 10s de acciones no críticas) |
| `N_backup` (niveles de backup rotativo) | 1 – 3 | 2 | En 1: sin red ante corrupción en cadena de un mismo ciclo. En 3+: habilita save scumming manual copiando backups y aumenta profundidad histórica más allá de lo deseable (ver F2) |
| `T_snapshot_max` (umbral duro de tiempo de snapshot en hilo principal) | 6.0 – 10.0 ms | 8.0 ms | Por debajo de 6ms: se dispararán advertencias de telemetría en snapshots legítimamente grandes (falsos positivos). Por encima de 10ms: un snapshot puede consumir >60% del frame y causar un hitch visible sin alertar |
| `T_quit_max` (timeout de bloqueo síncrono al cerrar) | 2.0 – 5.0 s | 3.0 s | Por debajo de 2s: en discos lentos el guardado de quit puede no completar, arriesgando pérdida del último estado. Por encima de 5s: el juego se siente colgado al cerrar (el jugador cree que se congeló) |
| Disparadores de autoguardado (set activo) | subconjunto de {cruzar El Paso, volver al Oasis, muerte/revivir, hito narrativo, quit} | todos activos | Desactivar "muerte/revivir" rompería el pilar de permanencia de la penalización. Desactivar "quit" arriesga pérdida al cerrar. Son data-driven para poder añadir disparadores nuevos sin código |

**Interacciones entre knobs**: `W_debounce` y el set de disparadores interactúan — más disparadores activos con una ventana muy corta aumentan la frecuencia real de escritura; se tunean juntos. `T_snapshot_max` depende del volumen real de estado serializable, que crece conforme se diseñen más sistemas `ISaveable` — debe re-validarse cuando Inventario, Facciones y la Máquina de Estado Narrativo existan (su estado es el más voluminoso). `N_backup` no interactúa con los demás — es independiente.

Nota: estos knobs son configuración operativa (data-driven, coherente con coding-standards del proyecto). La ruta de archivos y el formato de serialización **no** son knobs de diseño — son decisiones de implementación del ADR.

## Visual/Audio Requirements

> Sistema de infraestructura: superficie audiovisual mínima. No requiere Art Bible ni producción de assets significativa. Basta una nota de dirección a nivel de GDD.

- **Indicador de guardado en curso**: un ícono discreto (esquina de pantalla) que aparece durante una escritura real y desaparece al completarse. No debe ser intrusivo ni interrumpir el juego — el guardado es de fondo (F3). Sugerido: ícono pequeño con animación sutil (no un spinner ansioso).
- **Pantalla de quit "Guardando…"**: al cerrar con un guardado síncrono en curso, mostrar un overlay breve con "Guardando…" hasta que complete (o `T_quit_max`). Es el único momento en que el guardado bloquea visiblemente.
- **Audio**: ninguno para autoguardado normal (debe ser silencioso e invisible). Un sonido sutil de confirmación es aceptable solo para el guardado manual explícito (feedback de que la acción del jugador surtió efecto).
- **Feedback de error/recuperación**: los estados `SaveError`, `CorruptionRecovery` y `LoadFailed` usan diálogos de UI (ver UI Requirements), no VFX. El tono visual debe ser sobrio y claro, nunca alarmista.

## UI Requirements

- **Botón de guardado manual** en el menú de pausa: ejecuta un guardado al slot canónico. Se deshabilita brevemente mientras una escritura está en curso para evitar spam. Etiqueta clara ("Guardar") — no promete un punto de restauración.
- **Diálogo de `CorruptionRecovery`**: al detectar un save canónico corrupto, informar al jugador y ofrecer restaurar desde backup ("Tu partida no se pudo leer. ¿Restaurar desde la copia de seguridad más reciente?"). Sin jerga técnica.
- **Diálogo de `LoadFailed`**: mensaje claro de que no se pudo recuperar ninguna copia y ofrecer iniciar nueva partida. Nunca cargar en silencio un estado parcial.
- **Diálogo de `SaveError`**: informar del fallo de escritura (disco lleno/permisos) con opción de reintentar, aclarando que el progreso en memoria no se ha perdido.
- **Aviso de versión de esquema incompatible**: si un save es de versión no migrable, informar con claridad en vez de fallar en silencio.
- Toda esta UI es responsabilidad de implementación de **Menús y UI** (Presentation, no diseñado); Guardado solo expone los estados y eventos que esta UI consume.

> 📌 **UX Flag — Sistema de Guardado**: Este sistema tiene requisitos de UI reales (botón de guardado manual + diálogos de recuperación/error). En Fase 4 (Pre-Producción), correr `/ux-design` para especificar estos diálogos antes de escribir epics. Las historias que referencien esta UI deben citar `design/ux/[pantalla].md`, no este GDD directamente.

## Acceptance Criteria

### Core Rules

**AC-CR1 — Un único slot canónico**
- GIVEN una partida en curso con autoguardado y guardado manual disponibles
- WHEN se ejecutan varios guardados (auto + manual) a lo largo de la sesión
- THEN existe exactamente **un** archivo de guardado activo por partida, y tanto el autoguardado como el manual escriben sobre ese mismo archivo (no se crean slots ni puntos de restauración adicionales visibles al jugador).

**AC-CR2 — Backup rotativo no expuesto**
- GIVEN un save canónico existente
- WHEN el sistema va a sobrescribirlo con un nuevo guardado
- THEN copia primero el canónico actual a un backup rotativo antes de escribir, y ese backup **no** aparece en ninguna UI como "partida anterior" seleccionable por el jugador.

**AC-CR3 — Autoguardado en los 5 disparadores**
- GIVEN una partida activa
- WHEN ocurre cualquiera de estos eventos: (a) cruzar El Paso, (b) volver al Oasis, (c) morir y revivir en la Cámara de Jano, (d) completar un hito narrativo, (e) cerrar el juego de forma limpia
- THEN el sistema ejecuta un autoguardado sin que el jugador lo invoque, para cada disparador (sujeto a coalescencia F1 salvo los críticos c y e).

**AC-CR4 — Guardado manual = mismo slot**
- GIVEN el menú de pausa abierto
- WHEN el jugador pulsa "Guardar"
- THEN se escribe al mismo save canónico (idéntico destino que el autoguardado) y **no** se crea un slot separado ni un punto de restauración.

**AC-CR5 — La muerte se graba (irreversible)**
- GIVEN que el jugador muere en una expedición (pierde equipo, revierte un nodo de habilidad) y revive en la Cámara de Jano
- WHEN se completa el autoguardado del disparador (c) y luego se recarga la partida
- THEN el estado restaurado es el **posterior** a la muerte, y no existe ninguna opción de UI ni archivo que permita volver a un estado anterior a la muerte.

**AC-CR6 — Carga resetea estado estático**
- GIVEN una sesión de juego previa que dejó suscriptores de eventos activos (incl. `CombatEventBus`)
- WHEN el jugador carga una partida
- THEN el sistema resetea todo el estado estático y los suscriptores de eventos **antes** de aplicar los datos serializados, y ningún suscriptor/estado residual de la sesión anterior permanece activo tras la carga (verificable: nº de suscriptores del bus tras cargar = el que fija el estado cargado, no la suma con los previos).

**AC-CR7 — Alcance de serialización**
- GIVEN una partida con progreso en varios sistemas
- WHEN se guarda y luego se recarga
- THEN el save restaura: nodos de habilidad desbloqueados del linaje activo, inventario y equipo, reputación de facciones, flags narrativos, valores de relación, vínculos de bestia activos, flags de diálogo, posición/zona y metadatos (versión de esquema, timestamp, linaje); y **no** contiene datos de diseño estáticos (stats base de linaje, definiciones de árbol), que se leen de config.

**AC-CR8 — Guardado atómico**
- GIVEN un guardado en curso escribiendo a un archivo temporal
- WHEN el proceso se interrumpe antes de completar (kill del proceso a mitad de escritura del temp)
- THEN el save canónico anterior queda **byte-idéntico** a como estaba antes del guardado, porque el canónico solo se sustituye vía rename atómico tras completarse el temp.

### Fórmulas

**AC-F1a — Debounce coalescente (W_debounce = 5.0 s)**
- GIVEN que se acaba de ejecutar una escritura real en t_último
- WHEN llegan 3 disparadores no críticos dentro de la ventana de 5.0 s (t_último+1.2, +2.0, +3.5)
- THEN se ejecuta **una sola** escritura real en t_último+5.0 s con el estado actual de ese instante (last-write-wins), ahorrando 2 escrituras de los 3 disparadores.

**AC-F1b — Frecuencia máxima de escritura**
- GIVEN autoguardados no críticos disparándose de forma continua
- WHEN se mide el intervalo entre escrituras reales
- THEN nunca hay más de **1 escritura real cada 5.0 s** (frecuencia máx = 1 / W_debounce).

**AC-F1c — Excepciones críticas ignoran el debounce**
- GIVEN una escritura real reciente hace <5.0 s (ventana abierta)
- WHEN llega el disparador (c) muerte/revivir o el (e) quit limpio
- THEN se fuerza un flush inmediato sin esperar a la ventana; en el caso (e) además se **espera** a que la escritura complete antes de cerrar.

**AC-F2a — Rotación de backup N = 2**
- GIVEN guardados sucesivos S1, S2, S3, S4
- WHEN se completa S4
- THEN en disco quedan exactamente 3 estados: canónico=S4, `.bak1`=S3, `.bak2`=S2, y S1 fue descartado.

**AC-F2b — Recuperación tolerante a corrupción en cadena**
- GIVEN canónico=S4, `.bak1`=S3, `.bak2`=S2
- WHEN al cargar el canónico S4 está corrupto (y opcionalmente también `.bak1` S3)
- THEN el sistema restaura `.bak1`=S3; si S3 también es inválido, restaura `.bak2`=S2 (retroceso máximo tolerado: 2 guardados, sin llegar a LoadFailed mientras ≥1 de los 3 sea válido).

**AC-F3a — Snapshot dentro de presupuesto (objetivo 4 ms)**
- GIVEN un autoguardado normal (no quit)
- WHEN se ejecuta la Fase 1 (snapshot en hilo principal)
- THEN el coste en el hilo principal en ese frame es ≤ **4.0 ms** en operación normal (< ¼ de frame a 60 fps), y el frame no cae de 60 fps por el guardado.

**AC-F3b — Umbral duro 8 ms → advertencia, no abort**
- GIVEN un snapshot cuyo coste en el hilo principal supera **8.0 ms** en un frame
- WHEN termina la Fase 1
- THEN se registra una advertencia de performance (telemetría de dev), el snapshot **se completa igualmente** (no se aborta a medias) y no hay crash.

**AC-F3c — Fase 2 asíncrona**
- GIVEN un autoguardado normal en curso
- WHEN se ejecuta la Fase 2 (serializar + escribir + rotar backup + rename)
- THEN esa fase corre en un hilo de fondo y **no** bloquea el hilo principal ni el frame budget de render.

**AC-F3d — Quit síncrono con timeout T_quit_max = 3.0 s**
- GIVEN un quit limpio con un guardado pendiente/en curso
- WHEN el juego cierra
- THEN bloquea de forma síncrona mostrando "Guardando…" hasta completar, o hasta un máximo de **3.0 s**; si se agota el timeout, conserva el último canónico íntegro y descarta la escritura incompleta.

### Máquina de estados (transición hacia cada estado)

**AC-S-Idle** — GIVEN el sistema inicializado sin operación pendiente, WHEN no hay disparador de guardado ni carga, THEN permanece en Idle sin realizar I/O.

**AC-S-Saving** — GIVEN el sistema en Idle, WHEN se dispara un autoguardado (tras debounce) o guardado manual, THEN entra en Saving (serializa, escribe temp, rota backup, rename) y al completar con éxito vuelve a Idle.

**AC-S-Loading** — GIVEN el jugador inicia carga/continuación, WHEN comienza la restauración, THEN entra en Loading, resetea estado estático, deserializa y aplica datos; al completar vuelve a Idle.

**AC-S-CorruptionRecovery** — GIVEN un intento de carga, WHEN el canónico es ilegible o falla validación de esquema/integridad (incl. manipulación externa), THEN entra en CorruptionRecovery y ofrece restaurar desde `.bak1` (y si falla, `.bak2`); no crashea ante un save malformado.

**AC-S-SaveError** — GIVEN un guardado en curso (Saving), WHEN falla la I/O (disco lleno, sin permisos), THEN entra en SaveError, notifica al jugador, conserva el canónico previo intacto y el estado en memoria no se pierde.

**AC-S-LoadFailed** — GIVEN un intento de carga, WHEN ni el canónico ni `.bak1` ni `.bak2` son válidos, THEN entra en LoadFailed, notifica con mensaje claro y ofrece nueva partida; **nunca** fabrica estado falso ni carga datos parciales.

### Edge Cases clave

**AC-EC1 — Crash a mitad de escritura → canónico intacto**
- GIVEN un guardado escribiendo el archivo temporal, WHEN ocurre un corte de energía/crash antes del rename, THEN el canónico anterior queda intacto y en el siguiente arranque el temp huérfano se descarta.

**AC-EC2 — Save corrupto/manipulado → CorruptionRecovery**
- GIVEN un save canónico dañado o editado externamente, WHEN el jugador intenta cargar, THEN el sistema lo trata como archivo dañado, no crashea, entra en CorruptionRecovery y ofrece restaurar desde backup.

**AC-EC3 — Ningún archivo válido → LoadFailed**
- GIVEN canónico, `.bak1` y `.bak2` todos inválidos, WHEN el jugador intenta cargar, THEN se entra en LoadFailed con mensaje claro y oferta de nueva partida, sin cargar estado parcial.

**AC-EC4 — Migración de esquema conocido anterior**
- GIVEN un save con versión de esquema anterior conocida (número en metadatos), WHEN se carga, THEN se migra al esquema actual y la carga procede con los datos migrados correctos.

**AC-EC4b — Esquema desconocido/incompatible → LoadFailed controlado**
- GIVEN un save de versión desconocida o no migrable (ej. de una versión más nueva del juego), WHEN se intenta cargar, THEN se rechaza con aviso claro y se trata como LoadFailed controlado, sin intentar cargar datos que quedarían corruptos a medias.

**AC-EC5 — Disco lleno → SaveError con memoria intacta**
- GIVEN disco lleno o sin permisos durante un guardado, WHEN falla la operación, THEN se entra en SaveError, el canónico previo queda intacto, se notifica al jugador para liberar espacio y el estado en memoria se conserva para reintentar.

## Open Questions

1. ~~**Actualizar el GDD de Linajes con la dependencia de Guardado**~~ — ✅ RESUELTO (2026-07-13): `linajes.md` ya lista "Sistema de Guardado/Persistencia" en su tabla de Dependencies, con el contrato `GetUnlockedNodes()`/`RestoreUnlockedNodes(data)`. Referencia cruzada bidireccional completa.
2. **Formato de serialización, ruta de archivos y mecanismo de migración de esquema** — decisiones de implementación deliberadamente diferidas. *Dueño: engine-programmer. Resolver con `/architecture-decision` (ADR de serialización) antes de implementar.*
3. **Mecanismo concreto del hilo de fondo (Fase 2)** — Job System de Unity vs. Thread vs. Task, y si el snapshot usa doble-buffer. *Dueño: engine-programmer. Resolver en el mismo ADR de serialización.*
4. **Re-validar `T_snapshot_max` cuando existan los sistemas `ISaveable` voluminosos** — el presupuesto de 8ms asume un volumen de estado que aún no se puede medir (Inventario, Facciones, Máquina de Estado Narrativo no diseñados). *Dueño: systems-designer/engine-programmer. Resolver con `/perf-profile` cuando esos sistemas existan.*
5. **Guardado en la nube / multiplataforma** — fuera de alcance MVP (PC-only), pero el diseño de un solo slot canónico podría necesitar revisión si se añade cloud save más adelante. *Dueño: technical-director. Diferido a post-MVP.*
6. **El ADR de serialización debe exponer un modo de inyección de fallo para QA** — varios criterios de aceptación (crash a mitad de escritura, esquema no migrable, corrupción) no son reproducibles de forma determinista sin un hook de test. *Dueño: engine-programmer. Resolver en el ADR de serialización (gap señalado por QA).*
7. **Exponer una superficie de inspección de suscriptores para AC-CR6** — verificar que la carga no arrastra suscriptores residuales requiere un contador/consulta inspeccionable del `CombatEventBus` (o equivalente); hoy solo es verificable vía test de integración con acceso a internals, no por QA de caja negra. *Dueño: engine-programmer. Resolver junto con la implementación de la carga.*
