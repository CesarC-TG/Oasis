# Unity — Version Reference

| Field | Value |
|-------|-------|
| **Engine Version** | Unity 6000.4.7f1 (Unity 6.4) |
| **Rendering Pipeline** | Universal Render Pipeline (URP) |
| **Project Pinned** | 2026-05-18 |
| **LLM Knowledge Cutoff** | May 2025 |
| **Risk Level** | MEDIUM — Unity 6.3+ partially beyond training data |
| **Last Docs Verified** | 2026-05-18 |

## Post-Cutoff Version Timeline

| Version | Release | Key Changes |
|---------|---------|-------------|
| Unity 6000.1.x | 2024 Q4 | Unity 6.1 — URP improvements, GPU Resident Drawer |
| Unity 6000.2.x | 2025 Q1 | Unity 6.2 — Multiplayer improvements |
| Unity 6000.3.x | 2025 Q3 | Unity 6.3 LTS — Long-term support branch |
| Unity 6000.4.x | 2026 Q1 | Unity 6.4 — Latest features |

## Key Unity 6 APIs Already in Use (Oasis)

- `FindFirstObjectByType<T>()` ✓ (HUDController.cs — correct Unity 6 API)
- `UnityEngine.InputSystem` ✓ (PlayerController.cs — new Input System)
- `CharacterController` ✓ (PlayerController.cs)
- `Physics.SphereCast` ✓ (ground detection)

## Important API Notes for Unity 6

- `FindObjectOfType<T>()` → DEPRECATED → use `FindFirstObjectByType<T>()`
- Legacy Input System → use New Input System (UnityEngine.InputSystem) ✓
- `Resources.Load()` → prefer Addressables for large assets
- `OnGUI()` → prefer UI Toolkit for new UI systems

## References

- [Unity 6 Release Notes](https://unity.com/releases/unity-6)
- [Unity 6 What's New 6000.4](https://unity.com/releases/editor/whats-new/6000.4.0f1)
- [URP Documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/index.html)
