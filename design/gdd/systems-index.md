# Systems Index: Oasis

> **Status**: Draft
> **Created**: 2026-06-04
> **Last Updated**: 2026-06-04
> **Source Concept**: design/gdd/game-concept.md

---

## Overview

Post-apocalyptic narrative-driven action RPG. 150 years after nuclear war, humanity survives in a protected valley (Oasis). Five "Renacidos" (Reborn) who can die and revive are the only ones who can explore the radiation-ravaged exterior. Gameplay loop: Prepare in Oasis → Cross the Járnviðr wall → Explore/Fight/Collect in the Dead Lands → Die and revive or return → Deliver findings → Repeat.

The game requires **22 interconnected systems** spanning 5 character lineages, 5 bonded beasts, branching narrative with 4 endings, a faction reputation system, and a distinctive death-and-revival core loop — all built on Unity 6000.4.7f1 with URP.

---

## Systems Enumeration

| # | System Name | Category | Priority | Status | Design Doc | Depends On |
|---|-------------|----------|----------|--------|------------|------------|
| 1 | Linajes (Clases) | Foundation | MVP | Designed | design/gdd/linajes.md | — |
| 2 | Metabolismo de Resplandor | Foundation | MVP | Not Started | — | — |
| 3 | Muerte y Resurrección | Foundation | MVP | Not Started | — | — |
| 4 | Movimiento y Exploración | Foundation | MVP | Not Started | — | — |
| 5 | Sistema de Cámara | Foundation | MVP | Designed | design/gdd/sistema-camara.md | Sistema de Input |
| 6 | Sistema de Input | Foundation | MVP | Designed | design/gdd/sistema-input.md | — |
| 7 | Sistema de Guardado/Persistencia | Foundation | MVP | Designed | design/gdd/sistema-guardado.md | — |
| 8 | Combate | Core | MVP | Not Started | — | Linajes, Resplandor, Movimiento, Cámara, Input |
| 9 | Bestias del Vínculo | Core | MVP | Not Started | — | Linajes, Resplandor |
| 10 | Inventario y Equipo | Core | MVP | Not Started | — | Muerte, Movimiento |
| 11 | Progresión de Personaje | Core | Vertical Slice | Not Started | — | Linajes, Combate, Bestias |
| 12 | IA de Enemigos | Core | MVP | Not Started | — | Combate, Bestias, Resplandor, Gestión Escenas |
| 13 | Sistema de Audio | Core | MVP | Not Started | — | Gestión Escenas, Combate, Resplandor |
| 14 | Gestión de Escenas/Niveles | Core | MVP | Not Started | — | Movimiento |
| 15 | Diálogos | Feature | Vertical Slice | Not Started | — | Facciones (contract), Máquina Estado Narrativo (contract), Relaciones (contract) |
| 16 | Facciones y Reputación | Feature | Alpha | Not Started | — | Diálogos, Máquina Estado Narrativo, Guardado |
| 17 | Máquina de Estado Narrativo | Feature | Alpha | Not Started | — | Facciones, Diálogos, Guardado |
| 18 | Relaciones de Personaje | Feature | Alpha | Not Started | — | Máquina Estado Narrativo, Diálogos, Guardado |
| 19 | Economía y Crafting | Feature | Alpha | Not Started | — | Inventario, Facciones |
| 20 | HUD | Presentation | MVP | Not Started | — | Resplandor, Combate, Bestias, Inventario |
| 21 | Mapa y Navegación | Presentation | Vertical Slice | Not Started | — | Movimiento, Resplandor, Gestión Escenas |
| 22 | Menús y UI | Presentation | Alpha | Not Started | — | Linajes, Inventario, Progresión, Facciones, Guardado |

---

## Categories

| Category | Description | Typical Systems |
|----------|-------------|-----------------|
| **Foundation** | Systems with zero dependencies, built first. Define the fundamental rules and technical infrastructure everything else rests on. | Linajes (Clases), Metabolismo de Resplandor, Muerte y Resurrección, Movimiento y Exploración, Sistema de Cámara, Sistema de Input, Sistema de Guardado/Persistencia |
| **Core** | Gameplay systems that depend on Foundation. The moment-to-moment interactive systems the player engages with directly. Form the playable core loop. | Combate, Bestias del Vínculo, Inventario y Equipo, Progresión de Personaje, IA de Enemigos, Sistema de Audio, Gestión de Escenas/Niveles |
| **Feature** | Deep systems depending on Core. Add narrative depth, social dynamics, and systemic complexity on top of the core loop. | Diálogos, Facciones y Reputación, Máquina de Estado Narrativo, Relaciones de Personaje, Economía y Crafting |
| **Presentation** | UI and feedback layer wrapping everything. Displays game state to the player and provides the interface for interaction. | HUD, Mapa y Navegación, Menús y UI |

---

## Priority Tiers

| Tier | Definition | Target Milestone | Design Urgency |
|------|------------|------------------|----------------|
| **MVP** | Required for the core loop to function. Without these, you can't test "is this fun?" | First playable prototype | Design FIRST |
| **Vertical Slice** | Required for one complete, polished area. Demonstrates the full experience. | Vertical slice / demo | Design SECOND |
| **Alpha** | All features present in rough form. Complete mechanical scope, placeholder content OK. | Alpha milestone | Design THIRD |
| **Full Vision** | Polish, edge cases, nice-to-haves, and content-complete features. | Beta / Release | Design as needed |

---

## Dependency Map

Systems sorted by dependency order — design and build from top to bottom. Systems at the top are foundations; systems at the bottom are wrappers.

### Foundation Layer (no dependencies)

1. **Linajes (Clases)** — Define las 5 clases jugables, árboles de habilidades, nodos. Todo el gameplay de personaje depende de esto.
2. **Metabolismo de Resplandor** — Mecánica central del mundo exterior (radiación). Define cómo el Resplandor afecta al jugador, bestias y enemigos.
3. **Muerte y Resurrección** — El loop distintivo del juego. Define qué se pierde al morir, cómo se revive en la Cámara de Jano, y las condiciones de muerte por radiación.
4. **Movimiento y Exploración** — Base para navegar todos los entornos. Locomoción del personaje, interacción con el terreno, traversal.
5. **Sistema de Cámara** — Cámara en tercera persona con modos de combate/exploración, seguimiento de bestia, y detección de colisión de cámara.
6. **Sistema de Input** — New Input System de Unity. Soporte KB/M + gamepad, rebinding, y múltiples contextos de input (exploración, combate, UI, diálogo).
7. **Sistema de Guardado/Persistencia** — Serialización completa del estado: facciones, narrativa, inventario, progresión, vínculos de bestia, flags de diálogo.

### Core Layer (depends on foundation)

1. **Combate** — depends on: Linajes, Resplandor, Movimiento, Cámara, Input
2. **Bestias del Vínculo** — depends on: Linajes, Resplandor
3. **Inventario y Equipo** — depends on: Muerte, Movimiento
4. **Progresión de Personaje** — depends on: Linajes, Combate, Bestias
5. **IA de Enemigos** — depends on: Combate, Bestias, Resplandor, Gestión Escenas
6. **Sistema de Audio** — depends on: Gestión Escenas, Combate, Resplandor
7. **Gestión de Escenas/Niveles** — depends on: Movimiento

### Feature Layer (depends on core)

1. **Diálogos** — depends on: Facciones (contract), Máquina Estado Narrativo (contract), Relaciones (contract)
2. **Facciones y Reputación** — depends on: Diálogos, Máquina Estado Narrativo, Guardado
3. **Máquina de Estado Narrativo** — depends on: Facciones, Diálogos, Guardado
4. **Relaciones de Personaje** — depends on: Máquina Estado Narrativo, Diálogos, Guardado
5. **Economía y Crafting** — depends on: Inventario, Facciones

### Presentation Layer (depends on features)

1. **HUD** — depends on: Resplandor, Combate, Bestias, Inventario
2. **Mapa y Navegación** — depends on: Movimiento, Resplandor, Gestión Escenas
3. **Menús y UI** — depends on: Linajes, Inventario, Progresión, Facciones, Guardado

---

## Key Design Notes

### Linajes ↔ Progresión Boundary

**Linajes** defines _what_ skills exist: skill tree node definitions, individual skill effects and stats, tree topology and connections, and per-lineage identity. **Progresión de Personaje** manages _how_ skills are unlocked: XP thresholds, unlock conditions, resource costs, and level-gating. The two systems share a contract: Linajes exposes a data structure of unlockable nodes; Progresión queries that structure and applies unlock state. This separation prevents circular ownership and allows each system to be balanced independently.

### Muerte y Resurrección — Deferred Dependencies

Muerte y Resurrección has dependencies on systems that are designed later in the order, but it remains in Foundation because it is a core rule of the world from which other systems derive their behavior. The deferred dependencies are:

- **Inventario y Equipo** (S10, Core): What equipment is lost/retained on death
- **Gestión de Escenas/Niveles** (S14, Core): Cámara de Jano scene transition, respawn flow
- **Metabolismo de Resplandor** (S2, Foundation): Radiation death condition thresholds

These dependencies are documented upfront. Muerte y Resurrección defines the _rules_ of death and revival; the dependent systems implement their side of the contract. No circular dependency is introduced.

### Diálogos — Feature Layer Placement

Diálogos is classified as Feature (not Core) because its full implementation requires Facciones, Máquina de Estado Narrativo, and Relaciones — all Alpha-priority systems. However, a **minimal dialogue shell** (basic text display, single-choice responses) may be needed earlier for MVP testing of narrative-critical interactions. This shell is scoped as part of the HUD/Menús presentation layer during MVP, with full dialogue logic deferred to Vertical Slice.

---

## Circular Dependencies

- **Facciones y Reputación ↔ Máquina de Estado Narrativo**: Las facciones afectan qué rutas narrativas se abren, y las decisiones narrativas cambian la reputación.  
  **Resolución**: Diseñar en paralelo definiendo contratos claros. Facciones expone valores numéricos de reputación; Máquina de Estado Narrativo emite eventos de decisión. Ambos sistemas (S16 y S17) se diseñan como Alpha simultáneamente con contratos cruzados.

- **Diálogos ↔ Facciones / Máquina Narrativa / Relaciones**: Diálogos depende de los tres sistemas para funcionar completamente, pero los tres necesitan diálogos para manifestarse ante el jugador.  
  **Resolución**: Diálogos se diseña como sistema de presentación con una interfaz de contrato. Facciones, Máquina Narrativa y Relaciones definen las estructuras de datos y eventos que Diálogos consume. Esto permite que Diálogos (S15, Vertical Slice) se diseñe antes que Facciones (S18, Alpha), Máquina Narrativa (S19, Alpha) y Relaciones (S20, Alpha).

- **Relaciones de Personaje ↔ Máquina de Estado Narrativo**: Las relaciones afectan qué rutas narrativas y diálogos están disponibles; la máquina de estado narrativo modifica los valores de relación.  
  **Resolución**: Relaciones expone un modelo de afinidad numérico por personaje. Máquina de Estado Narrativo consulta umbrales de relación para desbloquear ramas. Contratos definidos durante diseño Alpha simultáneo.

---

## Recommended Design Order

Combining dependency sort, priority tiers, and risk analysis. Design these systems in this order. Each system's GDD should be completed and reviewed before starting the next, though independent systems at the same layer can be designed in parallel.

| Order | System | Priority | Layer | Agent | Est. Effort |
|-------|--------|----------|-------|-------|-------------|
| 1 | Linajes (Clases) | MVP | Foundation | game-designer | M |
| 2 | Sistema de Guardado/Persistencia | MVP | Foundation | engine-programmer | M |
| 3 | Sistema de Input | MVP | Foundation | gameplay-programmer | S |
| 4 | Sistema de Cámara | MVP | Foundation | gameplay-programmer | M |
| 5 | Metabolismo de Resplandor | MVP | Foundation | systems-designer | M |
| 6 | Muerte y Resurrección | MVP | Foundation | game-designer | M |
| 7 | Movimiento y Exploración | MVP | Foundation | game-designer | S |
| 8 | Gestión de Escenas/Niveles | MVP | Core | engine-programmer | M |
| 9 | IA de Enemigos | MVP | Core | ai-programmer | L |
| 10 | Combate | MVP | Core | systems-designer | L |
| 11 | Bestias del Vínculo | MVP | Core | game-designer | M |
| 12 | Inventario y Equipo | MVP | Core | systems-designer | M |
| 13 | Sistema de Audio | MVP | Core | audio-director | M |
| 14 | HUD | MVP | Presentation | ux-designer | S |
| 15 | Progresión de Personaje | Vertical Slice | Core | systems-designer | M |
| 16 | Diálogos | Vertical Slice | Feature | narrative-director | M |
| 17 | Mapa y Navegación | Vertical Slice | Presentation | ux-designer | S |
| 18 | Facciones y Reputación | Alpha | Feature | game-designer | L |
| 19 | Máquina de Estado Narrativo | Alpha | Feature | narrative-director | L |
| 20 | Relaciones de Personaje | Alpha | Feature | narrative-director | M |
| 21 | Economía y Crafting | Alpha | Feature | economy-designer | M |
| 22 | Menús y UI | Alpha | Presentation | ux-designer | S |

Effort estimates: S = 1 session, M = 2-3 sessions, L = 4+ sessions. A "session" is one focused design conversation producing a complete GDD.

---

## High-Risk Systems

| System | Risk Type | Risk Description | Mitigation |
|--------|-----------|-----------------|------------|
| Muerte y Resurrección | Design | Penalización de muerte: si es muy severa frustra, si es muy leve trivializa el riesgo. Bucle único sin referentes claros. | Prototipar temprano con playtest de sensación |
| Máquina de Estado Narrativo | Scope | 4 finales + sistema de espía oculto + 3 relaciones románticas. Riesgo de scope creep narrativo. | MVP: 1 ruta principal + 1 final alterno |
| Bestias del Vínculo | Technical | 5 criaturas con IA distinta ejecutándose simultáneamente (detección, protección, sigilo, combate, rastreo). | POC con 1 bestia (El Cristalino/Kael) como prueba de concepto |
| Metabolismo de Resplandor | Design | Equilibrar la tensión de supervivencia sin hacerla tediosa. Debe sentirse como recurso estratégico, no como barra de hambre. | Referencia: Metro 2033 (filtros de gas), Subnautica (oxígeno) |
| IA de Enemigos | Technical | 5 tipos de criatura hostil + IA autónoma de bestias del vínculo. Complejidad de comportamientos simultáneos. | Separar en módulos de comportamiento por tipo de criatura; perfilar tiempo de frame por tipo |
| Sistema de Guardado | Technical | Serialización completa del estado del juego: facciones, narrativa, inventario, progresión, bestias, diálogos. Riesgo de corrupción de datos con evolución del esquema. | Definir formato de serialización temprano; implementar test de regresión de carga/guardado |

---

## Performance Budgets

The following technical constraints must be factored into every system's design:

| Budget | Target | Scope |
|--------|--------|-------|
| IA de Bestias (frame time) | ≤ 2ms per beast | Per-beast AI update budget. Drives behavior complexity limits. |
| Sistema de Resplandor | Event-driven | Trigger volumes and zone events, no per-frame polling. Prevents constant radiation checks. |
| Stat Update Manager | Single consolidated manager | HP, Stamina, and Radiation updates routed through one update loop. No per-system Update() calls. |
| URP Draw Calls | ≤ 500 max | Total draw call budget for the frame. Affects VFX, beast rendering, and environmental detail. |

---

## Progress Tracker

| Metric | Count |
|--------|-------|
| Total systems identified | 22 |
| MVP systems | 14 |
| Vertical Slice systems | 3 |
| Alpha systems | 5 |
| Design docs started | 4 |
| Design docs reviewed | 0 |
| Design docs approved | 0 |

---

## Next Steps

- [ ] Review and approve this updated systems index
- [ ] Design MVP Foundation systems first: begin with **Linajes (Clases)** — `design/gdd/linajes.md`
- [ ] Run `/design-review` on each completed GDD
- [ ] Create ADR for Combat pipeline before GDD authoring
- [ ] Validate the highest-risk systems (Muerte y Resurrección, Metabolismo de Resplandor) with early prototyping before committing to Production
- [ ] Run `/gate-check pre-production` when MVP systems are designed
