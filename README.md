# Zombie War

Top-down zombie survival shooter — soldier vs. hordes of zombies pushing in from all four sides. Built for a 7-day take-home game test.

Full gameplay design & reasoning: [`Docs/GAMEPLAY_DESIGN.md`](Docs/GAMEPLAY_DESIGN.md).

## Project info

- **Unity version:** 6000.3.10f1
- **Render pipeline:** URP 17.3.0
- **Target platform:** PC / mobile (touch virtual joystick)
- **Input:** Unity Input System (`com.unity.inputsystem`)

## Key packages

| Package | Purpose |
|---|---|
| `com.unity.cinemachine` (3.1.6) | Top-down follow camera + impulse-based camera shake |
| `com.unity.animation.rigging` (1.4.1) | Player IK (feet, weapon aim) |
| `com.unity.ai.navigation` (2.0.10) | Zombie pathing (NavMesh) |
| `com.billtruong.stylized-toon-world-kit` | Toon lit/outline shaders, VFX (dissolve), environment shaders |
| `com.coplaydev.unity-mcp` | Unity Editor automation via MCP (dev tooling only, not shipped in build) |

## Folder structure

```
Assets/
├── _Project/           # all game-specific code/content
│   ├── Scripts/Runtime/{Gameplay,Systems,UI}
│   ├── Scripts/Editor
│   ├── Scripts/Tests/{EditMode,PlayMode}
│   ├── ScriptableObjects/{Configs,Events}
│   ├── Prefabs/
│   ├── Scenes/
│   ├── Art/{Models,Materials,Textures,Animations}
│   ├── Audio/
│   └── Shaders/
├── ThirdParty/          # imported packages not maintained here (BillGameCore, VAT, weapon assets)
├── Plugins/
├── Settings/            # URP pipeline assets (from Unity template, kept as-is)
└── Scenes/              # default template scene (to be repurposed)
```

## Git LFS

Binary assets (models, textures, audio) are tracked via Git LFS — see `.gitattributes`. Run `git lfs install` once per machine before committing new binary assets.
