# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity 6 (6000.4.7f1) 2D project named **PachinkoAsset**, using the Universal Render Pipeline (URP 17.4.0). The project is in early stage — no custom C# scripts exist yet; game logic should be added under `Assets/`.

## Key Packages

- **Render pipeline**: `com.unity.render-pipelines.universal` 17.4.0 (URP, 2D renderer configured in `Assets/Settings/Renderer2D.asset`)
- **Input**: `com.unity.inputsystem` 1.19.0 — project-wide actions defined in `Assets/InputSystem_Actions.inputactions`
- **2D toolset**: 2D Animation, 2D Aseprite, 2D PSD Importer, 2D SpriteShape, 2D Tilemap + Extras
- **Visual Scripting**: `com.unity.visualscripting` 1.9.11
- **Timeline**: `com.unity.timeline` 1.8.12

## Unity CLI Commands

Unity executable: `"C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe"`
Project path: `"C:\Users\User\PachinkoAsset"`

```powershell
# Open project in Unity Editor
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" -projectPath "C:\Users\User\PachinkoAsset"

# Batch-mode build (example: Windows standalone)
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" `
  -batchmode -quit -projectPath "C:\Users\User\PachinkoAsset" `
  -buildTarget StandaloneWindows64 -executeMethod BuildScript.Build -logFile build.log

# Run Edit Mode / Play Mode tests via CLI
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" `
  -batchmode -projectPath "C:\Users\User\PachinkoAsset" `
  -testPlatform editmode -runTests -testResults results.xml -logFile test.log
```

## Creating Assets & Prefabs (Edit Mode)

**Claude must execute Editor scripts autonomously** — never ask the user to click a menu item or run a script manually.

### Workflow for creating prefabs/scene objects

1. Write a static Editor method (under `Assets/_Pachinko/Editor/`) that uses `UnityEditor` APIs (`PrefabUtility`, `AssetDatabase`, etc.) to build the asset.
2. The method **must** be callable via `-executeMethod` (i.e. `public static void MethodName()`).
3. Run it immediately via batch mode and tail the log to confirm success:

```powershell
# Execute an Editor method in batch mode
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" `
  -batchmode -quit -projectPath "C:\Users\User\PachinkoAsset" `
  -executeMethod YourEditorClass.YourMethod -logFile "C:\Users\User\PachinkoAsset\editor_run.log"

# Check result
Get-Content "C:\Users\User\PachinkoAsset\editor_run.log" -Tail 30
```

### Rules
- All Editor scripts go in `Assets/_Pachinko/Editor/` (stripped from builds automatically).
- Never write `.unity`, `.prefab`, or `.meta` YAML by hand — always let Unity serialize via `AssetDatabase`.
- After running, check `editor_run.log` for errors before reporting success.
- If the Unity Editor is already open with the project, close it first (batch mode and interactive mode cannot share a project lock).

## Architecture Notes

- All game scripts belong under `Assets/_Pachinko/Scripts`
- All Sprites are under `Assets/_Pachinko/Sprites`
- All other assets in relevant folders
- URP 2D renderer is the active renderer; do not add 3D-only URP features.
- Use the new **Input System** (`UnityEngine.InputSystem`) — the legacy Input Manager is not the primary input path.
- The default scene is `Assets/Scenes/SampleScene.unity`.
- `Assets/Settings/` holds URP pipeline assets; edit these (not GraphicsSettings directly) to change render features.

## Prefab & Scene Conventions

**No procedural GameObject creation at runtime.** All GameObjects must originate from prefab assets:
- `new GameObject()` and `gameObject.AddComponent<T>()` at runtime are forbidden.
- `Instantiate(prefab)` is allowed only when spawning from a Designer-authored prefab asset.

**Board layouts are pre-built prefabs**, not procedurally generated:
- Each board variant (e.g. `default`, `hard`) has a corresponding prefab under `Assets/_Pachinko/Prefabs/Boards/` with pegs and buckets pre-placed in the Editor.
- Selecting a board at runtime = `Instantiate(boardPrefab)` where `boardPrefab` is assigned in the Inspector or loaded by `boardId`.
- JSON config (`StreamingAssets/boards/<id>.json`) drives **properties** only: bucket values, physics parameters (restitution, ball mass, gravity), chip count. It does **not** drive layout (peg positions are baked into the prefab).
- `PegView` and `ScoreBucketView` components on pre-placed pegs/buckets are configured from JSON at runtime via their `Setup()` methods.

**Ball** is the one exception: it is spawned from a `Ball.prefab` at drop time and pooled via `ObjectPool<BallView>`.

**Scenes vs prefabs:**
- `Assets/Scenes/SampleScene.unity` is the main game scene. It contains the persistent GameObjects (GameBootstrapper, UIController, Camera) but not the board layout.
- Board prefabs live in `Assets/_Pachinko/Prefabs/Boards/`.
- Switching board configs at runtime = destroy current board instance, instantiate new board prefab.

## Class Design Conventions

**Default to pure C#** (no `MonoBehaviour`) for all logic and systems:
- Game managers (e.g. `GameManager`, `ScoreManager`)
- Object pooling
- State machines, rules, data models
- Any class that doesn't need Unity scene lifecycle hooks

**Use `MonoBehaviour`** only for classes that are attached to GameObjects and need Unity callbacks (`Awake`, `Start`, `Update`, `OnCollision`, etc.):
- Visual/screen components (e.g. `PachinkoScreenView`, `BallView`)
- Physics responders
- UI controllers tied to scene objects

Pure C# classes are instantiated and owned by other systems; `MonoBehaviour` components wire them into the scene.
