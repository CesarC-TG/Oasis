# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Engine & Language

- **Engine**: Unity 6000.4.7f1
- **Language**: C#
- **Rendering**: Universal Render Pipeline (URP)
- **Physics**: Unity Physics (3D Rigidbody + CharacterController)

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: PC (Windows primary)
- **Input Methods**: Keyboard/Mouse (primary), Gamepad (partial support)
- **Primary Input**: Keyboard/Mouse
- **Gamepad Support**: Partial
- **Touch Support**: None
- **Platform Notes**: PC-only initially. Single player + co-op multiplayer planned.

## Naming Conventions

- **Classes**: PascalCase (e.g., `PlayerController`)
- **Public fields/properties**: PascalCase (e.g., `MoveSpeed`)
- **Private fields**: _camelCase (e.g., `_moveSpeed`)
- **Methods**: PascalCase (e.g., `TakeDamage()`)
- **Files**: PascalCase matching class (e.g., `PlayerController.cs`)
- **Scenes/Prefabs**: PascalCase (e.g., `PlayerCharacter.prefab`)
- **Constants**: PascalCase or UPPER_SNAKE_CASE

## Performance Budgets

- **Target Framerate**: 60fps
- **Frame Budget**: 16.6ms
- **Draw Calls**: 500 max (URP batching enabled)
- **Memory Ceiling**: 4GB RAM target

## Testing

- **Framework**: NUnit (Unity Test Framework)
- **Minimum Coverage**: Core gameplay systems (PlayerStats, inventory, death mechanic)
- **Required Tests**: Balance formulas, stamina/HP systems, networking (Unity Netcode — future)

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
- [None configured yet — add as architectural decisions are made]

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here -->
- [None configured yet — add as dependencies are approved]

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- [No ADRs yet — use /architecture-decision to create one]

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist (C# review — primary covers it)
- **Shader Specialist**: unity-shader-specialist (Shader Graph, HLSL, URP/HDRP materials)
- **UI Specialist**: unity-ui-specialist (UI Toolkit UXML/USS, UGUI Canvas, runtime UI)
- **Additional Specialists**: unity-dots-specialist (ECS, Jobs, Burst), unity-addressables-specialist (asset loading, memory)
- **Routing Notes**: Primary para arquitectura y C# general. DOTS para ECS/Jobs. Shader para VFX y rendering. UI para interfaces. Addressables para gestión de assets.

### File Extension Routing

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs files) | unity-specialist |
| Shader / material files (.shader, .shadergraph, .mat) | unity-shader-specialist |
| UI / screen files (.uxml, .uss, Canvas prefabs) | unity-ui-specialist |
| Scene / prefab / level files (.unity, .prefab) | unity-specialist |
| Native extension / plugin files (.dll, native plugins) | unity-specialist |
| General architecture review | unity-specialist |
