# ADR-0001: Pipeline de Combate

## Status

Accepted

## Date

2026-06-04

## Last Verified

2026-06-05

## Decision Makers

User (Creative Director), Technical Director, Game Designer, Systems Designer,
Unity Specialist, AI Programmer

## Summary

Define the damage pipeline, hit detection strategy, and event system for all
combat interactions in Oasis. The combat architecture must support 5 player
lineages with distinct combat styles, 5+ enemy creature types, 5 bonded beasts
as combat companions, and radiation as an environmental modifier — all in Unity
6000.4.7f1 (URP), single-player, first/third-person hybrid.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6000.4.7f1 |
| **Domain** | Core / Physics / Scripting |
| **Knowledge Risk** | MEDIUM — Unity 6 near/post training cutoff, must verify against engine reference |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, existing code in `Assets/Scripts/` |
| **Post-Cutoff APIs Used** | `FindFirstObjectByType<T>()` (confirmed correct Unity 6 API), `Physics.OverlapSphereNonAlloc`, `Physics.SphereCastNonAlloc` |
| **Verification Required** | Layer-based collision matrix, `Physics.SphereCast` performance with 5+ creatures |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None (first ADR) |
| **Enables** | ADR-0002 (Bestias del Vínculo), ADR-0003 (IA de Enemigos) |
| **Blocks** | GDD de Combate, GDD de Bestias, GDD de IA de Enemigos |
| **Ordering Note** | Must be Accepted before any combat-related GDD is authored |

## Context

### Problem Statement

The game has 5 lineages (Kael=tracker/ranged, Sira=medic/support, Davan=engineer/tech,
Vael=warrior/melee, Ren=infiltrator/stealth), 5+ enemy types (Vaciados=group melee,
Fusionado=boss, Garra=apex predator, Enjambre=swarm, Silencio=environmental hazard),
5 beast companions, and radiation as a zone modifier. Without a defined damage pipeline,
each system will implement ad-hoc damage calculations, leading to inconsistent behavior,
untestable balance, and impossible integration.

### Current State

Existing code (`PlayerStats.cs`) implements a flat `TakeDamage(float amount)` call with
no defense calculation, no armor, no crit system, and no damage metadata (source type,
element). HP and Stamina use simple regen with configurable delays. `UnityEvent onDeath`
fires on death with no damage context passed. This is functional for a prototype but
insufficient for the full game's requirements.

### Constraints

- **CharacterController** is already in use (not Rigidbody) — physics queries must work
  with kinematic bodies
- **First-person primary mode** (CameraController.cs defaults to FirstPerson)
- **URP draw call budget**: 500 max — VFX must use VFX Graph (GPU-accelerated), not
  Legacy Shuriken (deprecated in Unity 6). Enable GPU Resident Drawer for automatic batching.
- **Single-player** — no server-authoritative concerns, no lag compensation needed
- **New Input System** in use — combat inputs must work within multiple action maps
  (exploration, combat, dialogue, menu)
- **60fps target** (16.6ms frame budget) — per-beast AI capped at 2ms
- **No GC allocations in hot paths** — all physics queries must use NonAlloc variants
  (`OverlapSphereNonAlloc`, `SphereCastNonAlloc`) to avoid garbage collection spikes
- **Domain Reload** may be disabled for fast editor iteration — static event delegates
  must be cleared on assembly reload

### Requirements

- [FR1] Damage calculation must support: base damage, weapon modifier, skill modifier,
  armor/resistance reduction, radiation zone modifier, beast buff/debuff, and critical hits
- [FR2] Hit detection must work in both first-person and third-person camera modes
- [FR3] All damage events must carry metadata (amount, source, damage type, position,
  isCritical) for VFX, SFX, and UI consumption
- [FR4] Radiation must affect combat through zone-based modifiers (not per-frame polling)
- [FR5] Beast companions must share the same damage pipeline (friendly fire rules defined)
- [FR6] Performance: combat calculations + hit detection < 4ms frame time at peak (10+ actors)

## Decision

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      COMBAT PIPELINE                             │
│                                                                  │
│  ATACANTE                    DEFENSOR                            │
│  ┌──────────┐               ┌──────────────┐                    │
│  │Daño Base │               │ Armor/Def    │                    │
│  │(stats)   │               │ (flat DR)    │                    │
│  └────┬─────┘               └──────┬───────┘                    │
│       │                            │                             │
│  ┌────▼─────┐                      │                             │
│  │Weapon    │                      │                             │
│  │Modifier  │                      │                             │
│  └────┬─────┘                      │                             │
│       │                            │                             │
│  ┌────▼─────┐                      │                             │
│  │Skill     │                      │                             │
│  │Modifier  │                      │                             │
│  └────┬─────┘                      │                             │
│       │                            │                             │
│  ┌────▼─────┐                      │                             │
│  │Radiation │                      │                             │
│  │Modifier  │                      │                             │
│  └────┬─────┘                      │                             │
│       │                            │                             │
│  ┌────▼─────┐                      │                             │
│  │Beast     │                      │                             │
│  │Modifier  │                      │                             │
│  └────┬─────┘                      │                             │
│       │                            │                             │
│  ┌────▼─────┐                      │                             │
│  │Crit?     │                      │                             │
│  │(chance×) │                      │                             │
│  └────┬─────┘                      │                             │
│       │                            │                             │
│       ▼                            ▼                             │
│  ┌──────────────┐    ┌──────────────────────┐                    │
│  │  Daño Bruto  │───▶│  Defensa Reducción   │                    │
│  │  (float)     │    │  (flat DR, min 0)    │                    │
│  └──────────────┘    └──────────┬───────────┘                    │
│                                 │                                │
│                                 ▼                                │
│                          ┌──────────────┐                       │
│                          │  Daño Neto   │                       │
│                          │  (float)     │                       │
│                          └──────┬───────┘                       │
│                                 │                                │
│                ┌────────────────┼────────────────┐              │
│                ▼                ▼                ▼              │
│         ┌──────────┐   ┌──────────────┐  ┌────────────┐       │
│         │Health    │   │DamageEvent   │  │VFX/SFX     │       │
│         │Component │   │(Scriptable)  │  │Pool        │       │
│         │.TakeDmg()│   │published to  │  │.PlayAt()   │       │
│         │          │   │EventBus      │  │            │       │
│         └──────────┘   └──────────────┘  └────────────┘       │
└─────────────────────────────────────────────────────────────────┘
```

### Damage Formula

```
damageRaw = baseDamage × weaponMultiplier × skillMultiplier
          × radiationMultiplier × beastMultiplier

damageRaw = min(damageRaw, maxDamageClamp)

if (Random.value < critChance)
    damageRaw *= critMultiplier

damageNet = max(minDamageClamp, damageRaw - targetDefense)
```

**Variable definitions and ranges:**

| Variable | Source | Range | Notes |
|----------|--------|-------|-------|
| `baseDamage` | Linaje stats + weapon base | 5–200 | Defined per lineage/weapon |
| `weaponMultiplier` | Weapon class + quality | 0.5–2.5 | e.g., dagger 0.8, greatsword 2.0 |
| `skillMultiplier` | Active ability | 0.0–5.0 | 0.0 = utility skill, no damage |
| `radiationMultiplier` | Zone metadata | 0.7–1.5 | Below 1.0 = radiation weakens; above 1.0 = radiation-empowered enemies |
| `beastMultiplier` | Beast bond buff | 0.9–1.3 | Applied if beast is active and in range |
| `critChance` | Linaje + weapon | 0.0–0.4 | Capped at 40% |
| `critMultiplier` | Linaje + weapon | 1.5–2.5 | |
| `maxDamageClamp` | Tuning knob | 500–2000 | Prevents multiplicative stacking from producing extreme one-shots |
| `minDamageClamp` | Tuning knob | 1.0–5.0 | Floor damage to prevent zero-damage stalemates (default 1.0) |
| `targetDefense` | Target armor stat | 0–100 | Flat damage reduction (not percentage) |
| `damageNet` | Pipeline output | 0–∞ | Clamped at minDamageClamp floor |

### Hit Detection Strategy

| Attack Type | Detection Method | Rationale |
|-------------|-----------------|-----------|
| **Melee (Vael, bestias, Vaciados)** | `Physics.OverlapSphereNonAlloc` at weapon/hitbox position | Non-allocating. Works with CharacterController kinematic colliders. Gated by `isHitWindowActive` bool (set via Animation Events). Single-frame per swing |
| **Ranged (Kael, Davan gadgets, Garra spit)** | `Physics.SphereCastNonAlloc` from camera/weapon origin | Non-allocating. Thin raycasts miss small targets; SphereCast gives tolerance |
| **Area/AOE (Sira AoE heal/damage, Enjambre cloud)** | `Physics.OverlapSphereNonAlloc` with radius | Non-allocating area query |
| **Stealth (Ren backstab, invisible attacks)** | `Physics.OverlapSphereNonAlloc` + angle check (dot product) | Non-allocating. "Behind target" check for bonus damage |

Use Unity's **Layer-based collision matrix**:
- `Player` layer — collides with `Enemy`, `Environment`
- `Enemy` layer — collides with `Player`, `Beast`
- `Beast` layer — collides with `Enemy` (NOT `Player` — friendly fire disabled by default)
- `Projectile` layer — collides with `Player`, `Enemy`, `Beast`, `Environment`

### Key Interfaces

```csharp
// === DamageData struct — the "currency" of the pipeline ===
public struct DamageData
{
    public float BaseDamage;
    public float WeaponMultiplier;    // 1.0 if unarmed
    public float SkillMultiplier;     // 1.0 for basic attack
    public float RadiationMultiplier; // 1.0 in safe zones
    public float BeastMultiplier;     // 1.0 if beast inactive
    public float CritChance;          // 0.0–0.4
    public float CritMultiplier;      // 1.5–2.5

    public GameObject Source;         // Who dealt this
    public DamageType Type;           // Physical, Radiation, Fire, Tech, Environmental
    public Vector3 HitPoint;
    public Vector3 HitNormal;
}

public enum DamageType { Physical, Radiation, Fire, Tech, Environmental }

// === IDamageable — anything that can receive damage ===
public interface IDamageable
{
    void ApplyDamage(DamageData data);
    float GetDefense();
    Transform GetTransform();
}

// === CombatEventBus — centralized event system ===
// Replaces ad-hoc UnityEvent chains. Uses ScriptableObject events
// or a static mediator pattern.
public static class CombatEventBus
{
    public static event System.Action<DamageData, float> OnDamageDealt;
    // damageData = full context, float = damageNet actually applied

    public static event System.Action<GameObject> OnEntityKilled;
    public static event System.Action<GameObject, float> OnHealed;
}

// === DamageCalculator — pure function, no MonoBehaviour ===
public static class DamageCalculator
{
    public static float CalculateRaw(DamageData data)
    {
        float raw = data.BaseDamage
                  * data.WeaponMultiplier
                  * data.SkillMultiplier
                  * data.RadiationMultiplier
                  * data.BeastMultiplier;

        raw = Mathf.Min(raw, MaxDamageClamp);

        if (Random.value < data.CritChance)
            raw *= data.CritMultiplier;

        return raw;
    }

    public static float ApplyDefense(float rawDamage, float defense)
        => Mathf.Max(MinDamageClamp, rawDamage - defense);

    public static float MinDamageClamp { get; set; } = 1.0f;
    public static float MaxDamageClamp { get; set; } = 1000f;
}
```

### Implementation Guidelines

1. **DamageCalculator** is a static utility class. No MonoBehaviour, no scene dependency.
   Testable in isolation via NUnit.

2. **IDamageable** is implemented by `PlayerStats`, enemy health components, destructible objects,
   and the Cámara de Jano revival trigger. Every damageable entity exposes `GetDefense()`.

3. **CombatEventBus** uses static events (not a MonoBehaviour singleton). Subscribers
   (`HUDController`, `VFXManager`, `AudioManager`, `QuestTracker`) register in `OnEnable`
   and unregister in `OnDisable`.

4. **Radiation modifier** is NOT queried per-attack. Each zone has a `RadiationZone`
   component that sets `radiationMultiplier` on a shared `EnvironmentState` ScriptableObject.
   `DamageData.radiationMultiplier` is populated from this state at attack time — read-only,
   zero per-frame cost.

5. **Beast modifier** is populated by the active beast's `BeastBond` component when the
   beast is within bond range. If no beast or out of range, multiplier is 1.0.

6. **Hit detection** runs in `FixedUpdate` (physics step) for melee/area; ranged raycasts
   can run in `Update` since they're instantaneous queries.

7. **Existing code compatibility**: `PlayerStats.TakeDamage(float)` is refactored to
   implement `IDamageable.ApplyDamage(DamageData)`. The old signature is deprecated.
   `UnityEvent onDeath` is replaced by `CombatEventBus.OnEntityKilled`.

8. **Hit window for AI melee attacks**: AI enemies use a `HitTracker` component with a
   `HashSet<IDamageable> DamagedThisSwing` reset on attack start. An `isHitWindowActive`
   bool gates the `OverlapSphereNonAlloc` query — set to `true` via Animation Events at
   the swing's impact frame, `false` on swing end. This prevents multi-hit from a single
   attack animation. Behavior trees read `lastDamageReceived` from a blackboard updated
   by `AIController` (the sole subscriber to `CombatEventBus` per enemy). BT nodes do NOT
   subscribe directly to static events.

9. **VFX system**: Uses Unity VFX Graph (GPU-accelerated, recommended for Unity 6).
   Legacy Shuriken particle system is deprecated in Unity 6 and must not be used.
   VFX instances are managed via VFX Graph's built-in instance pooling, NOT per-GameObject
   Instantiate/Destroy. The `VFXManager` component receives `CombatEventBus.OnDamageDealt`
   and calls `VFXEventAttribute` reuse patterns for hit sparks, blood, and impact decals.

10. **Environmental hazards (Silencio)**: Entities that deal damage without being
    combatants implement `IHazard` (not `IDamageable`):
    ```csharp
    public interface IHazard
    {
        DamageData GetHazardDamage();      // damage per tick
        float GetDamageInterval();         // seconds between ticks
        bool IsPlayerInHazard(Collider c); // trigger check
    }
    ```
    `DamageType.Environmental` covers Silencio, radiation zones, and other non-combat
    damage sources. Hazards emit through `CombatEventBus` like any other damage source.

11. **Beast AI positioning**: Since `Beast` ↔ `Player` collision is disabled in the
    physics matrix, beast AI must implement **soft player-avoidance steering** (repulsion
    force when within 1.5m of the player). Without this, enemy knockback can push beasts
    into the player position causing visual clipping.

12. **Domain reload safety**: If Domain Reload is disabled for fast editor iteration,
    `CombatEventBus` static delegates must be explicitly cleared on assembly reload via
    `[RuntimeInitializeOnLoadMethod]` to prevent subscriber accumulation across
    Play → Stop → Play cycles.

13. **`FindFirstObjectByType<T>()`** calls must occur once in `Awake()`/`Start()` and
    be cached in a private field. Never call in `Update()` or other hot paths — it
    performs a scene-wide O(n) search.

## Alternatives Considered

### Alternative 1: Damage as ScriptableObject Data

- **Description**: Each attack defines damage in a ScriptableObject asset. No runtime calculation.
- **Pros**: Designer-friendly, drag-and-drop in editor, trivial to balance
- **Cons**: Static — can't combine modifiers dynamically. Beast buffs, radiation, and crits
  would require separate systems anyway. Doubles the architecture
- **Estimated Effort**: Low
- **Rejection Reason**: Oasis needs dynamic modifier stacking (5 lineages × radiation zones ×
  beast states = too many combinations to pre-bake). ScriptableObject approach works for
  static RPGs, not for systemic survival games

### Alternative 2: Percentage-based Defense (Damage * (1 - reduction%))

- **Description**: Defense reduces damage by a percentage instead of flat subtraction
- **Pros**: Scales better at high levels, common in RPGs, avoids zero-damage problem
- **Cons**: Harder to balance low-level combat. A 50% reduction means a dagger hit (5 dmg)
  becomes 2.5 and a greatsword hit (50 dmg) becomes 25 — same ratio, less tactical variety
- **Estimated Effort**: Same
- **Rejection Reason**: Flat DR creates interesting weapon variety (fast low-damage vs slow
  high-damage matters against armored targets). Percentage defense makes weapon choice
  irrelevant against armor. Flat DR better serves the survival/scavenge tone of Oasis

### Alternative 3: Per-Beast Separate Damage System

- **Description**: Beasts deal damage through their own independent system, not the shared pipeline
- **Pros**: Simpler initial implementation. Beast damage isolated
- **Cons**: Duplicate code for damage application, events, VFX. Two sources of truth for
  "how much damage did this entity take." Multiplicative buffs become impossible
- **Estimated Effort**: High (maintaining two systems)
- **Rejection Reason**: The "vínculo" mechanic is designed as a shared biological system —
  beast and Renacido share the Resplandor metabolism and should share the damage pipeline

### Alternative 4: Rigidbody-based Physics Combat

- **Description**: Use Rigidbody + forces for melee impact, physics-based hit reactions
- **Pros**: "Physics sandbox" feel, emergent moments
- **Cons**: CharacterController is already implemented. Physics-based combat is hard to
  tune, unpredictable, and makes hit confirmation unreliable for melee
- **Estimated Effort**: Very high (rewrite movement system)
- **Rejection Reason**: CharacterController + kinematic approach is already in place and
  works. Physics-based melee is better suited for physics sandbox games (Blade & Sorcery),
  not narrative action RPGs

## Consequences

### Positive

- Single damage pipeline ensures all systems (combat, beasts, radiation, UI, audio) speak
  the same language. A damage number on screen is always correct
- Static `DamageCalculator` is trivially testable in NUnit — balance formulas can be verified
  without loading a scene
- `CombatEventBus` decouples VFX, SFX, and UI from combat logic. Adding a new damage reaction
  (e.g., screen shake) requires no changes to combat code
- Flat defense creates clear weapon identity: fast weapons (Ren, Kael) vs. armor-breaking
  weapons (Vael) — tactical choice
- Layer-based collision eliminates friendly fire bugs architecturally

### Negative

- Flat DR formula means high-defense enemies are immune to low-damage attacks. This is
  by design for boss encounters (Fusionado) but must be communicated to players
- Static event bus (`CombatEventBus`) means all subscribers hear all events. Memory-safe
  as long as subscribers unregister in `OnDisable`, but a missed unregister = leak
- First-person melee hit detection with `OverlapSphereNonAlloc` requires precise weapon transform
  placement, Animation Event-driven hit windows, and `HitTracker` per-swing deduplication —
  more authoring work than a simple raycast
- `Random.value` in `DamageCalculator` for crits makes the static class non-deterministic.
  Acceptable for single-player; would need seeded RNG for replays or multiplayer

### Neutral

- `IDamageable` replaces the existing `PlayerStats.TakeDamage(float)` pattern. Migration
  is straightforward but touches every damage-receiving script
- Radiation modifier moves from "what should it do?" to "where does CombatEventBus read
  it from?" — shifts the design question but doesn't answer it

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| Flat DR makes low-damage weapons useless vs. armored enemies | Medium | Medium | `minDamageClamp` tuning knob (default 1.0, testable 2.0–5.0). Players informed via UI feedback |
| Multiplicative stacking produces extreme damage (one-shots) | Medium | High | `maxDamageClamp` tuning knob (default 1000). Cap verified in NUnit tests |
| Crit investment dominates all other multipliers (homogenizes builds) | Medium | Medium | `critChance` capped at 40%, `critMultiplier` capped at 2.5. Monitored per lineage during tuning |
| CombatEventBus static events cause memory leaks | Low | High | Enforce OnEnable/OnDisable pattern in code review. RuntimeInitializeOnLoadMethod clears delegates on domain reload |
| First-person melee hit detection feels imprecise | Medium | Medium | Prototype with Vael's melee first. Adjust sphere radius + animation timing. HitTracker prevents multi-hit |
| 5 beast companions + enemies overload frame budget | Medium | High | Enforce 2ms per-beast budget. Only 1 beast active at a time (Vertical Slice scope). Profile early |
| Static event delegates accumulate across Play sessions (Domain Reload off) | Medium | Low | `[RuntimeInitializeOnLoadMethod]` explicitly clears all `CombatEventBus` static delegates |
| Zero-damage stalemates at mid-game defense thresholds | Medium | Medium | `minDamageClamp` ensures minimum feedback. Per-tier defense curves validated in GDD tuning phase |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU — Damage calc (per hit) | 0ms (no calc) | ~0.01ms | 0.05ms |
| CPU — Hit detection (melee, OverlapSphereNonAlloc) | 0ms | ~0.1ms | 0.2ms |
| CPU — Hit detection (ranged, SphereCastNonAlloc) | 0ms | ~0.05ms | 0.1ms |
| CPU — Combat total (10 actors) | 0ms | ~2ms | 4ms |
| CPU — Per-beast AI | 0ms | ~1.5ms | 2ms |
| Memory — DamageData struct | 0B | ~64B per hit | N/A (stack) |
| Memory — CombatEventBus | 0B | ~1KB static | 1KB |

All within the 16.6ms frame budget. Peak combat scenario (player + 1 beast + 5 enemies)
estimated at 3ms for combat systems.

## Migration Plan

1. **Phase 1: Add IDamageable interface** — Create `IDamageable.cs`. Implement on
   `PlayerStats` alongside the existing `TakeDamage(float)`. Both signatures coexist.
   Old calls still work. New calls use `ApplyDamage(DamageData)`.

2. **Phase 2: Add DamageData + DamageCalculator** — Create `DamageData.cs` struct and
   `DamageCalculator.cs` static class. Unit test the formula variations.

3. **Phase 3: Add CombatEventBus** — Create `CombatEventBus.cs`. Wire `HUDController`
   to subscribe to `OnDamageDealt` instead of polling `PlayerStats.CurrentHP` in Update.
   Wire `PlayerStats.Die()` to invoke `OnEntityKilled`.

4. **Phase 4: Migrate existing code** — Replace direct `TakeDamage(float)` calls with
   `DamageCalculator` + `ApplyDamage(DamageData)`. Remove old `TakeDamage` signature.

5. **Phase 5: Add radiation and beast modifiers** — When those systems are implemented,
   their components write to the shared `EnvironmentState` and `BeastBond` data, which
   `DamageData` reads at attack time.

**Rollback plan**: Phase 1-3 are additive — existing code continues to work alongside new
interfaces. If pipeline proves wrong, revert to `TakeDamage(float)` and refactor
`CombatEventBus` subscribers to use `UnityEvent` instead. No data loss.

## Validation Criteria

- [ ] `DamageCalculator.CalculateRaw()` returns correct values for all combinations of
  multiplier values (NUnit test with known inputs/outputs)
- [ ] `DamageCalculator.ApplyDefense()` correctly handles 0 defense, partial defense,
  defense > damage (returns `MinDamageClamp`), and defense < 0 (clamped)
- [ ] `maxDamageClamp` correctly caps extreme multiplier stacking (NUnit: all multipliers at max → output ≤ MaxDamageClamp)
- [ ] `minDamageClamp` prevents zero-damage (NUnit: raw 5, defense 100 → output = MinDamageClamp)
- [ ] `IDamageable.ApplyDamage()` reduces health and fires `CombatEventBus.OnDamageDealt`
  with correct `damageNet` value
- [ ] Melee `OverlapSphereNonAlloc` detects enemies within weapon range and does NOT detect allies
  (friendly fire disabled via layer matrix)
- [ ] `HitTracker` prevents multiple hits from single melee swing (simulated test: 10-frame swing → 1 hit registered)
- [ ] Ranged `SphereCastNonAlloc` hits moving targets (simulated in test scene)
- [ ] CombatEventBus subscribers (HUD, VFX, SFX) all respond to `OnDamageDealt` event
- [ ] Peak combat scenario (player + 1 beast + 5 enemies) stays under 4ms CPU
- [ ] `RadiationMultiplier` of 0.7 correctly reduces damage by 30%
- [ ] `IHazard` entities (Silencio) correctly apply periodic damage via `CombatEventBus`

## GDD Requirements Addressed

Foundational — no GDD dependency (this ADR is written before GDDs). Enables:

| GDD | System | What This ADR Enables |
|-----|--------|----------------------|
| `design/gdd/linajes.md` | Linajes (Clases) | Base damage stats, crit chance/multiplier per lineage |
| `design/gdd/combate.md` | Combate | Full damage pipeline, hit detection, event system |
| `design/gdd/bestias.md` | Bestias del Vínculo | Beast multiplier integration, beast combat AI bounds |
| `design/gdd/ia-enemigos.md` | IA de Enemigos | Enemy damage application, defense values, attack patterns |
| `design/gdd/resplandor.md` | Metabolismo de Resplandor | Radiation zone modifier interface |
| `design/gdd/hud.md` | HUD | Event-driven damage number display, health bar updates |

## Related

- `Assets/Scripts/Player/PlayerStats.cs` — Current implementation (to be refactored)
- `Assets/Scripts/Player/PlayerController.cs` — Movement + CharacterController (constrains physics approach)
- `Assets/Scripts/Camera/CameraController.cs` — First/Third person toggle (affects hit detection origin)
- `Assets/Scripts/UI/HUDController.cs` — Health/stamina display (subscriber to CombatEventBus)
