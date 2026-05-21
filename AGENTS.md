# AGENTS.md

## Project

Unity 6 (6000.4.7f1) 2D project — PachinkoAsset. URP 2D renderer (`Assets/Settings/Renderer2D.asset`). .NET Standard 2.1 (`apiCompatibilityLevel: 6`). New Input System active (`activeInputHandler: 1`, `com.unity.inputsystem` 1.19.0). Very early stage — one stub `MonoBehaviour` at `Assets/_Pachinko/Scripts/Pachinko.cs`, only scene is `Assets/Scenes/SampleScene.unity`.

## Key conventions (from existing CLAUDE.md)

- Game scripts: `Assets/_Pachinko/Scripts/`, sprites: `Assets/_Pachinko/Sprites/`
- Pure C# for logic/managers/state machines; `MonoBehaviour` only for scene-attached components needing Unity callbacks
- Use `UnityEngine.InputSystem` (not legacy Input Manager)
- Edit URP via `Assets/Settings/` assets, not GraphicsSettings directly

## Architecture (from existing plan at `docs/plans/2026-05-21-feat-plinko-game-core-plan.md`)

A Plinko game is being built: JSON config → procedural board → physics ball → scoring → seeded bezier-path drops. All tunables in `StreamingAssets/boards/default.json` — zero hardcoded constants. The planned file structure:

```
Assets/_Pachinko/Scripts/
├── Config/BoardConfig.cs       [Serializable] JSON model
├── Config/ConfigLoader.cs      reads StreamingAssets, cached
├── Core/GameManager.cs         state machine (pure C#)
├── Core/ScoreManager.cs        score + chip tracking (pure C#)
├── Core/PegBoardLayout.cs      staggered grid calculator (pure C#)
├── Core/SeededPathAnimator.cs  bezier waypoint generator (pure C#)
├── Views/GameController.cs     MonoBehaviour root orchestrator
├── Views/BoardView.cs          MonoBehaviour procedural spawner
├── Views/BallView.cs           MonoBehaviour Rigidbody2D + physics/seeded modes
├── Views/PegView.cs            MonoBehaviour collider + visual
├── Views/ScoreBucketView.cs    MonoBehaviour trigger zone
└── Views/UIController.cs       MonoBehaviour HUD
```

`GameController` wires everything — no `FindObjectOfType`, all via `[SerializeField]`. `ConfigLoader` is static, uses `File.ReadAllText` (desktop only; Android needs `UnityWebRequest`). `JsonUtility` handles `[Serializable]` classes natively.

## Developer commands

```powershell
# Open project
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -projectPath "C:\Users\User\PachinkoAsset"

# Run Edit Mode tests
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\User\PachinkoAsset" -testPlatform editmode -runTests -testResults results.xml -logFile test.log
```

## Unity Editor manual steps required

The following cannot be scripted — must be done in Unity Editor:
1. Import TextMeshPro: `Window → TextMeshPro → Import TMP Essential Resources`
2. Create Prefabs for Peg, Ball, ScoreBucket (with SpriteRenderer, colliders, physics materials)
3. Create `PhysicsMaterial2D` at `Assets/_Pachinko/Physics/PegBounce.physicsmaterial2d` (bounciness 0.55, friction 0)
4. Create folders: `Assets/_Pachinko/Prefabs/`, `Assets/_Pachinko/Physics/`
5. Wire `[SerializeField]` references on GameController in scene
6. Set camera: Orthographic, size=7, position (0,0,-10)

## Testing quirks

- No tests exist yet. When adding, use `com.unity.test-framework` (1.6.0, already in manifest).
- `ConfigLoader` fails silently on missing JSON — always verify `default.json` exists at `Assets/StreamingAssets/boards/default.json` before testing.

## Dependency notes

- TextMeshPro: must be imported via `Window → TextMeshPro` — not present by default
- No external JSON package — `JsonUtility` is sufficient for `[Serializable]` classes
