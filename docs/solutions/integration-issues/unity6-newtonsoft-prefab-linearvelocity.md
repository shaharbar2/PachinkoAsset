---
title: "Unity 6 Project Setup — Newtonsoft.Json official package, prefab-first architecture, linearVelocity API"
category: integration-issues
tags: [unity, unity6, newtonsoft-json, prefabs, rigidbody2d, physics2d, upm]
module: PachinkoAsset
symptom: "Scoped registry for Newtonsoft.Json unnecessary; runtime peg spawning violates prefab-first rule; Rigidbody2D.velocity removed in Unity 6"
root_cause: "Unity ships com.unity.nuget.newtonsoft-json officially on packages.unity.com; Unity 6 renamed Rigidbody2D.velocity to linearVelocity and drag to linearDamping"
---

# Unity 6 Project Setup — Three Gotchas

Discovered while setting up the PachinkoAsset Plinko game in Unity 6 (6000.4.7f1).

---

## Solutions

### 1. Newtonsoft.Json: Use Unity's Official Package

**Wrong** — adds a third-party scoped registry that isn't needed:

```json
{
  "scopedRegistries": [
    {
      "name": "jillejr",
      "url": "https://npm.cloudsmith.io/jillejr/newtonsoft-json-for-unity/",
      "scopes": ["jillejr"]
    }
  ],
  "dependencies": {
    "jillejr.newtonsoft.json-for-unity": "13.0.102"
  }
}
```

**Right** — Unity ships its own wrapper directly on `packages.unity.com`:

```json
{
  "dependencies": {
    "com.unity.nuget.newtonsoft-json": "3.2.1"
  }
}
```

No `scopedRegistries` block needed. The `using Newtonsoft.Json;` namespace is identical — zero C# changes required when switching.

---

### 2. Prefab-First Architecture: No Runtime Spawning

**Wrong** — `BoardView.Build(cfg, layout)` calling a pool to spawn pegs at runtime-computed positions:

```csharp
// WRONG: violates no-new-GameObject-at-runtime rule
foreach (var pos in layout.PegPositions)
{
    var go = _pegPool.Rent();          // Instantiate at runtime
    go.transform.position = pos;
    go.GetComponent<PegView>().Setup(cfg.pegRadius, cfg.pegRestitution);
}
```

**Right** — pegs are pre-placed in the prefab by an Editor script; `BoardView.Setup()` just configures existing children:

```csharp
// RIGHT: finds pre-placed children and configures them from JSON
public void Setup(BoardConfig cfg)
{
    _pegs    = GetComponentsInChildren<PegView>(true);
    _buckets = GetComponentsInChildren<ScoreBucketView>(true);

    foreach (var peg in _pegs)
        peg.Setup(cfg.pegRadius, cfg.pegRestitution);

    float bucketWidth = _buckets.Length > 1
        ? Mathf.Abs(_buckets[1].transform.position.x - _buckets[0].transform.position.x)
        : cfg.pegSpacing;

    for (int i = 0; i < _buckets.Length && i < cfg.bucketValues.Length; i++)
        _buckets[i].Setup(i, cfg.bucketValues[i], bucketWidth);
}
```

The layout computation (`PegBoardLayout`) still exists, but lives in `Assets/_Pachinko/Editor/` and runs only inside the `PachinkoSetup.CreateAll` Editor method that bakes positions into the prefab at edit time.

JSON config drives **properties only** — bucket values, restitution, physics params. Never XY positions.

---

### 3. Unity 6 Rigidbody2D API Renames

| Unity 5 / 2022 LTS | Unity 6 |
|---|---|
| `Rigidbody2D.velocity` | `Rigidbody2D.linearVelocity` |
| `Rigidbody2D.drag` | `Rigidbody2D.linearDamping` |

```csharp
// Before (Unity 5 / 2022)
_rb.velocity = Vector2.zero;
_rb.drag = 0f;

// After (Unity 6)
_rb.linearVelocity = Vector2.zero;
_rb.linearDamping = 0f;
```

Unity 6 marks the old names `[Obsolete(error: true)]` so the project will not compile if they are used — `CS0619` / `CS1061` on a physics type = likely API rename.

---

## Prevention

### Newtonsoft.Json

- Before adding any scoped registry for JSON, open **Package Manager → Unity Registry** and search `Newtonsoft`. If it appears, install from there.
- Rule: always prefer the Unity-registry package over a community scoped registry for the same library — Unity's wrapper is version-tested against the editor and avoids assembly name collisions.
- After switching sources, delete `Library/` and reopen if you see assembly conflict errors.

### Prefab-First Architecture Checklist

- [ ] No `new GameObject()` in `Assets/_Pachinko/Scripts/` (grep to verify)
- [ ] No `gameObject.AddComponent<T>()` at runtime
- [ ] All layout objects are pre-placed in prefabs under `Assets/_Pachinko/Prefabs/`
- [ ] Spawning code only uses `Instantiate(prefabReference)` with an Inspector-assigned prefab
- [ ] JSON config files contain only property values (physics, scores) — no positions

### Unity 6 API Renames

- Consult the [Unity 6 Upgrade Guide](https://docs.unity3d.com/6000.0/Documentation/Manual/UpgradeGuide.html) before migrating from any earlier version — the Physics 2D section lists all renames.
- Look up Rigidbody2D/Physics members in the Unity 6 Scripting API docs before writing physics code; don't rely on 2022 muscle memory.
- Enable "Treat warnings as errors" in Project Settings → Player during development.

---

## Related

- Project `CLAUDE.md` → "Prefab & Scene Conventions" — the authoritative rule source for the prefab-first constraint
- `Assets/_Pachinko/Editor/PachinkoSetup.cs` — the Editor script that bakes board layout into prefabs
- `Assets/_Pachinko/Scripts/Views/BoardView.cs` — the runtime consumer that calls `GetComponentsInChildren` instead of spawning
