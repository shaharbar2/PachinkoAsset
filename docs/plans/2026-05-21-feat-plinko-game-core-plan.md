---
title: "feat: Plinko Game Core — JSON-driven board, physics ball, seeded drops, basic UI"
type: feat
status: active
date: 2026-05-21
deepened: 2026-05-21
origin: docs/brainstorms/2026-05-21-plinko-game-brainstorm.md
---

# feat: Plinko Game Core

## Enhancement Summary

**Deepened on:** 2026-05-21
**Research agents used:** Unity Physics, JSON/Config, Game Design, Architecture, Performance, Simplicity, Patterns, Security (7 parallel agents)

### Key Improvements Discovered
1. **Unity 6 API change**: `velocity` → `linearVelocity` on Rigidbody2D throughout all code
2. **Critical bug in original plan**: `GameController.Start` subscribes bucket events twice — double-score on every physics landing
3. **Shared PhysicsMaterial2D**: Original plan allocates one per peg (68 heap objects) — use a single shared static instance
4. **Missing `Loading` state**: GameManager had no boot state, allowing drops before config is ready
5. **Static ConfigLoader blocked future goals**: Replace with `IConfigProvider` interface for testability and remote-config readiness
6. **IScoreModifier pipeline**: Add before any power-up arrives — zero code changes when Midas Peg is built
7. **Seeded drop timing parity**: Physics drops vary in duration; kinematic animation must feel gravity-authentic using `hopDuration = baseDuration / (1 + rowIndex * 0.08f)`
8. **Security**: BoardConfig.Validate(), path traversal guard on boardId, targetBucketIndex double bounds-check

### New Considerations Discovered
- `Bounce Threshold` in Physics 2D settings (default 1.0) silently suppresses low-speed bounces — lower to 0.5
- `SimulateLanding()` on ScoreBucketView is a leaky abstraction; remove it, fire `OnPathComplete` from SeededPathAnimator instead
- Layer-based collision matrix (disable Peg-vs-Peg) is more performant than tag-based `CompareTag`
- DOTween is the right tool for per-arc bezier animation of seeded drops (not a custom lerp coroutine)
- `SeededDrop` should be its own file, not nested inside `BoardConfig`
- Rename `GameBootstrapper` (MonoBehaviour) vs `GameController` (pure C#) to eliminate the name collision

---

## Overview

Build the complete playable core of a Plinko/pachinko arcade game in Unity 6 (URP 2D). A ball drops from the top-center of a procedurally-generated peg board, bounces through a staggered grid, and lands in a scored bucket at the bottom. The board, bucket values, chip count, and physics parameters are all driven by a local JSON config file — zero hardcoded tuning constants. A seeded-drop system allows specific drops to be pre-scripted to land in a designated bucket (via bezier path animation, not physics), enabling tutorials, events, and controlled outcomes.

Scope: JSON config layer → procedural board → ball physics → scoring → basic UI → seeded drop animator.

---

## Problem Statement

The project has no game logic yet — only an empty `Pachinko.cs` MonoBehaviour stub. We need to go from blank canvas to a fully playable Plinko round.

---

## Proposed Solution

**JSON-driven, procedural board generation** (see brainstorm: `docs/brainstorms/2026-05-21-plinko-game-brainstorm.md`).

Single source of truth: `StreamingAssets/boards/default.json`. `IConfigProvider` loads it at startup. `PegBoardLayout` computes staggered peg positions from the config. `BoardView` procedurally spawns pegs, walls, and buckets from a pre-warmed `PegPool`. `GameBootstrapper` (MonoBehaviour) wires pure C# systems to scene views. `BallView` handles both physics drops and seeded bezier-path drops via DOTween. Scoring fires identically in both modes through domain events.

No hardcoded constants — every tunable lives in JSON.

---

## Technical Approach

### File Structure

```
Assets/_Pachinko/Scripts/
├── Config/
│   ├── BoardConfig.cs          — [Serializable] JSON model + Validate()
│   ├── SeededDrop.cs           — own file (public [Serializable] type)
│   ├── IConfigProvider.cs      — interface: T Get<T>(string key)
│   └── LocalJsonConfigProvider.cs — reads StreamingAssets, caches
├── Core/
│   ├── GameController.cs       — pure C# state machine + domain events
│   ├── ScoreManager.cs         — score + IScoreModifier pipeline
│   ├── PegBoardLayout.cs       — staggered grid position calculator
│   └── SeededPathAnimator.cs   — bezier waypoint generator
├── Views/
│   ├── GameBootstrapper.cs     — MonoBehaviour root orchestrator (replaces old "GameController" MB)
│   ├── BoardView.cs            — MonoBehaviour procedural spawner
│   ├── PegPool.cs              — MonoBehaviour object pool for pegs
│   ├── BallView.cs             — MonoBehaviour Rigidbody2D + DOTween seeded mode
│   ├── PegView.cs              — MonoBehaviour collider (shared PhysicsMaterial2D)
│   ├── ScoreBucketView.cs      — MonoBehaviour trigger zone + visual feedback
│   └── UIController.cs         — MonoBehaviour HUD (event-driven, no GameBootstrapper ref)
└── Pachinko.cs                 — DELETE (empty stub)

Assets/StreamingAssets/boards/
└── default.json

Assets/_Pachinko/Prefabs/       — created manually in Unity Editor
├── Ball.prefab
├── Peg.prefab
└── ScoreBucket.prefab

Assets/_Pachinko/Physics/
└── PegBounce.physicsmaterial2d — bounciness 0.55, friction 0, Bounce Combine: Maximum
```

**Package to add:**
```json
// Packages/manifest.json — add Newtonsoft.Json via jillejr registry (IL2CPP-safe)
"jillejr.newtonsoft.json-for-unity": "13.0.102"
```

---

### Data Models (`Config/`)

#### BoardConfig.cs

```csharp
// Assets/_Pachinko/Scripts/Config/BoardConfig.cs
using System;
using Newtonsoft.Json;

[Serializable]
public class BoardConfig
{
    public string boardId        = "default";
    public int    rows           = 8;
    public int    cols           = 9;
    public float  pegSpacing     = 1.2f;
    public float  pegRadius      = 0.15f;
    public int[]  bucketValues   = { 100, 500, 1000, 10000, 1000, 500, 100 };
    public int    chipsPerRound  = 5;
    public float  ballMass       = 1.0f;
    public float  gravityScale   = 2.0f;
    public float  pegRestitution = 0.55f;
    public float  randomImpulseRange = 2.5f;
    public float  resolveDelay   = 0.6f;   // seconds between landing and next drop
    public SeededDrop[] seededDrops = Array.Empty<SeededDrop>();

    // Returns null on success, error string on failure.
    // Call immediately after deserialization.
    public string Validate()
    {
        if (pegSpacing < 0.05f || pegSpacing > 10f)
            return $"pegSpacing {pegSpacing} out of range [0.05, 10]";
        if (pegRadius < 0.01f || pegRadius > 2f)
            return $"pegRadius {pegRadius} out of range [0.01, 2]";
        if (pegRadius >= pegSpacing * 0.5f)
            return $"pegRadius {pegRadius} must be < pegSpacing/2 ({pegSpacing * 0.5f})";
        if (rows < 1 || rows > 50)
            return $"rows {rows} out of range [1, 50]";
        if (cols < 1 || cols > 50)
            return $"cols {cols} out of range [1, 50]";
        if (ballMass < 0.01f || ballMass > 10f)
            return $"ballMass {ballMass} out of range [0.01, 10]";
        if (gravityScale < 0.1f || gravityScale > 5f)
            return $"gravityScale {gravityScale} out of range [0.1, 5]";
        if (randomImpulseRange < 0 || randomImpulseRange > 20f)
            return $"randomImpulseRange {randomImpulseRange} out of range [0, 20]";
        if (bucketValues == null || bucketValues.Length == 0)
            return "bucketValues is null or empty";
        if (seededDrops != null)
        {
            for (int i = 0; i < seededDrops.Length; i++)
            {
                if (seededDrops[i].targetBucketIndex < 0 ||
                    seededDrops[i].targetBucketIndex >= bucketValues.Length)
                    return $"seededDrops[{i}].targetBucketIndex {seededDrops[i].targetBucketIndex} " +
                           $"out of bucketValues bounds ({bucketValues.Length})";
            }
        }
        return null;
    }
}
```

#### SeededDrop.cs (own file — public [Serializable] types get their own file)

```csharp
// Assets/_Pachinko/Scripts/Config/SeededDrop.cs
using System;

[Serializable]
public class SeededDrop
{
    public int chipIndex;          // which drop number (0-based) is scripted
    public int targetBucketIndex;  // which bucket (0 = leftmost) to land in
}
```

#### IConfigProvider.cs + LocalJsonConfigProvider.cs

```csharp
// Assets/_Pachinko/Scripts/Config/IConfigProvider.cs
public interface IConfigProvider
{
    BoardConfig GetBoard(string boardId);
}

// Assets/_Pachinko/Scripts/Config/LocalJsonConfigProvider.cs
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

public class LocalJsonConfigProvider : IConfigProvider
{
    private static readonly Regex SafeId =
        new Regex(@"^[a-zA-Z0-9_\-]{1,64}$", RegexOptions.Compiled);

    private readonly Dictionary<string, BoardConfig> _cache = new();
    private readonly string _boardsRoot;

    public LocalJsonConfigProvider()
    {
        _boardsRoot = Path.GetFullPath(
            Path.Combine(Application.streamingAssetsPath, "boards"));
    }

    public BoardConfig GetBoard(string boardId)
    {
        if (_cache.TryGetValue(boardId, out var cached)) return cached;

        // Path traversal guard
        if (!SafeId.IsMatch(boardId))
            throw new System.ArgumentException($"boardId '{boardId}' contains illegal characters.");

        string path     = Path.Combine(_boardsRoot, boardId + ".json");
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(_boardsRoot, System.StringComparison.OrdinalIgnoreCase))
            throw new System.ArgumentException($"Path traversal detected for boardId '{boardId}'.");

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Board config not found: {fullPath}");

        string raw = File.ReadAllText(fullPath);
        var cfg = new BoardConfig();
        JsonConvert.PopulateObject(raw, cfg,
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore });

        string error = cfg.Validate();
        if (error != null)
            throw new System.InvalidOperationException($"Invalid board config '{boardId}': {error}");

        _cache[boardId] = cfg;
        return cfg;
    }
}
```

`JsonConvert.PopulateObject` writes only the keys present in JSON — missing keys keep their C# default values. This is safer than `DeserializeObject<T>` which returns null-field objects on partial JSON.

> **Android/WebGL note**: `File.ReadAllText` does not work on Android or WebGL (StreamingAssets is inside an APK/served over HTTP). For those platforms, replace the file read with a `UnityWebRequest` coroutine in `LocalJsonConfigProvider`, keeping the same `IConfigProvider` interface. The `GameBootstrapper` calls `GetBoard()` as a coroutine on those platforms.

---

### Pure C# Core (`Core/`)

#### GameController.cs (pure C# — state machine + domain events)

```csharp
// Assets/_Pachinko/Scripts/Core/GameController.cs
using System;

public enum GameState { Loading, Idle, Dropping, Resolving, RoundEnd }

public class GameController
{
    public GameState State { get; private set; } = GameState.Loading;
    public int ChipsLeft  { get; private set; }

    // Domain events — views subscribe, GameBootstrapper wires them
    public event Action<GameState> OnStateChanged;
    public event Action<int>       OnDropStarted;   // (dropIndex)
    public event Action<int>       OnBallLanded;    // (bucketValue)
    public event Action<int>       OnRoundEnded;    // (roundTotal)

    // Explicit valid transitions — illegal transitions are no-ops with a log
    private static readonly System.Collections.Generic.Dictionary<GameState, GameState[]> ValidNext = new()
    {
        { GameState.Loading,   new[] { GameState.Idle } },
        { GameState.Idle,      new[] { GameState.Dropping } },
        { GameState.Dropping,  new[] { GameState.Resolving } },
        { GameState.Resolving, new[] { GameState.Idle, GameState.RoundEnd } },
        { GameState.RoundEnd,  new[] { GameState.Loading } },
    };

    public void ConfigLoaded(int chips)
    {
        ChipsLeft = chips;
        Transition(GameState.Idle);
    }

    public bool CanDrop() => State == GameState.Idle && ChipsLeft > 0;

    public void BeginDrop(int dropIndex)
    {
        ChipsLeft--;
        Transition(GameState.Dropping);
        OnDropStarted?.Invoke(dropIndex);
    }

    public void BallLanded(int bucketValue)
    {
        Transition(GameState.Resolving);
        OnBallLanded?.Invoke(bucketValue);
    }

    public void ResolveComplete(int roundTotal)
    {
        if (ChipsLeft > 0)
            Transition(GameState.Idle);
        else
        {
            Transition(GameState.RoundEnd);
            OnRoundEnded?.Invoke(roundTotal);
        }
    }

    private void Transition(GameState next)
    {
        if (System.Array.IndexOf(ValidNext[State], next) < 0)
        {
            UnityEngine.Debug.LogWarning($"[GameController] Invalid transition {State} → {next}. Ignored.");
            return;
        }
        State = next;
        OnStateChanged?.Invoke(next);
    }
}
```

#### ScoreManager.cs (with IScoreModifier pipeline)

```csharp
// Assets/_Pachinko/Scripts/Core/ScoreManager.cs
using System;
using System.Collections.Generic;

public interface IScoreModifier
{
    int Modify(int baseScore);
}

public class ScoreManager
{
    public int RoundTotal { get; private set; }

    public event Action<int, int> OnScoreChanged; // (bucketValue, roundTotal)

    private readonly List<IScoreModifier> _modifiers = new();

    public void Reset() => RoundTotal = 0;

    public void AddModifier(IScoreModifier mod)    => _modifiers.Add(mod);
    public void RemoveModifier(IScoreModifier mod) => _modifiers.Remove(mod);

    public void AddScore(int rawValue)
    {
        int value = rawValue;
        foreach (var mod in _modifiers)
            value = mod.Modify(value);

        RoundTotal += value;
        OnScoreChanged?.Invoke(value, RoundTotal);
    }
}
```

Power-ups (Midas Peg = 2× multiplier) register an `IScoreModifier` when activated and remove it when deactivated. `ScoreManager` code never changes.

#### PegBoardLayout.cs

```csharp
// Assets/_Pachinko/Scripts/Core/PegBoardLayout.cs
using System.Collections.Generic;
using UnityEngine;

public class PegBoardLayout
{
    public IReadOnlyList<Vector2> PegPositions   => _pegs;
    public IReadOnlyList<Vector2> BucketPositions => _buckets;
    public float BucketWidth  { get; private set; }
    public float BoardWidth   { get; private set; }
    public float BoardHeight  { get; private set; }
    public float TopY         { get; private set; }
    public float BottomY      { get; private set; }

    private readonly List<Vector2> _pegs    = new();
    private readonly List<Vector2> _buckets = new();

    public void Build(BoardConfig cfg)
    {
        _pegs.Clear();
        _buckets.Clear();

        // pegSpacingY ≈ 0.866 × pegSpacingX gives equilateral triangle spacing
        // (natural quincunx/staggered-grid proportions)
        float spacingY = cfg.pegSpacing * 0.866f;

        BoardWidth  = (cfg.cols - 1) * cfg.pegSpacing;
        BoardHeight = (cfg.rows - 1) * spacingY;
        TopY        = BoardHeight / 2f;
        BottomY     = -BoardHeight / 2f - spacingY; // one row below last peg row

        // Even rows: cols pegs.  Odd rows: cols-1 pegs, offset right by pegSpacing/2.
        for (int row = 0; row < cfg.rows; row++)
        {
            bool isEven = (row % 2 == 0);
            int  count  = isEven ? cfg.cols : cfg.cols - 1;
            float rowY  = TopY - row * spacingY;
            float startX = isEven
                ? -BoardWidth / 2f
                : -BoardWidth / 2f + cfg.pegSpacing / 2f;

            for (int col = 0; col < count; col++)
                _pegs.Add(new Vector2(startX + col * cfg.pegSpacing, rowY));
        }

        // Buckets: evenly span board width (number of gaps = bucketValues.Length)
        int   bucketCount = cfg.bucketValues.Length;
        float bucketStep  = BoardWidth / (bucketCount - 1);
        BucketWidth       = bucketStep;

        for (int i = 0; i < bucketCount; i++)
            _buckets.Add(new Vector2(-BoardWidth / 2f + i * bucketStep, BottomY - 0.3f));
    }
}
```

#### SeededPathAnimator.cs (waypoints only — no dead FindNearPegs code)

```csharp
// Assets/_Pachinko/Scripts/Core/SeededPathAnimator.cs
using System.Collections.Generic;
using UnityEngine;

public class SeededPathAnimator
{
    // Gravity-authentic hop durations: faster at lower rows, like a real falling ball.
    // baseDuration and speedFactor are tuned in config.
    public const float BaseDuration  = 0.35f;
    public const float SpeedFactor   = 0.08f;
    public const float MinHopSeconds = 0.10f;

    public float HopDuration(int rowIndex) =>
        Mathf.Max(MinHopSeconds, BaseDuration / (1f + rowIndex * SpeedFactor));

    // Generates waypoints from dropPos → targetPos, passing through peg rows with
    // natural-looking lateral drift that converges on targetBucketX.
    public Vector3[] GeneratePath(Vector3 dropPos, Vector3 targetPos,
                                  BoardConfig cfg, PegBoardLayout layout)
    {
        var waypoints = new List<Vector3> { dropPos };
        int steps     = cfg.rows + 1;
        float totalDx = targetPos.x - dropPos.x;
        float totalDy = dropPos.y - targetPos.y;
        float currentX = dropPos.x;
        float jitterDebt = 0f;

        for (int step = 1; step <= steps; step++)
        {
            float progress = (float)step / steps;
            float targetX  = dropPos.x + totalDx * progress;
            float y        = dropPos.y - totalDy * progress;

            float jitter     = Random.Range(-cfg.pegSpacing * 0.35f, cfg.pegSpacing * 0.35f);
            float cancelForce = -jitterDebt * 0.3f;
            float dx         = (targetX - currentX) + jitter + cancelForce;
            currentX = Mathf.Clamp(currentX + dx, -layout.BoardWidth / 2f, layout.BoardWidth / 2f);
            jitterDebt += jitter;

            waypoints.Add(new Vector3(currentX, y, 0f));
        }

        // Final waypoint is exactly the target bucket center
        waypoints[waypoints.Count - 1] = targetPos;
        return waypoints.ToArray();
    }
}
```

`FindNearPegs` and `HandleUtility_DistancePointToSegment` removed — dead code in Phase 1–5. Add them back when the first peg-hit visual/audio feature is built.

---

### MonoBehaviour Views (`Views/`)

#### PegView.cs (shared PhysicsMaterial2D — no per-peg allocation)

```csharp
// Assets/_Pachinko/Scripts/Views/PegView.cs
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D), typeof(SpriteRenderer))]
public class PegView : MonoBehaviour
{
    // One shared instance — never allocate per peg
    private static PhysicsMaterial2D _sharedMaterial;

    public void Setup(float radius, float restitution)
    {
        if (_sharedMaterial == null)
            _sharedMaterial = new PhysicsMaterial2D("PegShared")
                { bounciness = restitution, friction = 0f };

        var col = GetComponent<CircleCollider2D>();
        col.radius         = radius;
        col.sharedMaterial = _sharedMaterial;

        transform.localScale = Vector3.one * radius * 2f;
    }
}
```

> Prefer assigning a `PhysicsMaterial2D` asset in the prefab Inspector over creating it at runtime — zero allocation. The static fallback above is a safeguard only.

#### PegPool.cs

```csharp
// Assets/_Pachinko/Scripts/Views/PegPool.cs
using System.Collections.Generic;
using UnityEngine;

public class PegPool : MonoBehaviour
{
    [SerializeField] private GameObject _pegPrefab;
    [SerializeField] private int _initialSize = 80;

    private readonly List<GameObject> _pool = new();

    private void Awake()
    {
        for (int i = 0; i < _initialSize; i++)
        {
            var go = Instantiate(_pegPrefab, transform);
            go.SetActive(false);
            _pool.Add(go);
        }
    }

    public GameObject Rent()
    {
        foreach (var go in _pool)
            if (!go.activeSelf) { go.SetActive(true); return go; }

        Debug.LogWarning("[PegPool] Pool exhausted — expanding.");
        var extra = Instantiate(_pegPrefab, transform);
        _pool.Add(extra);
        return extra;
    }

    public void ReturnAll()
    {
        foreach (var go in _pool)
            go.SetActive(false);
    }
}
```

Pegs are disabled (not destroyed) between board configurations — no GC, no Instantiate hitch on board transitions.

#### ScoreBucketView.cs (no SimulateLanding — visual feedback only)

```csharp
// Assets/_Pachinko/Scripts/Views/ScoreBucketView.cs
using System;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class ScoreBucketView : MonoBehaviour
{
    [SerializeField] private TextMeshPro _valueLabel;

    public int BucketIndex { get; private set; }
    public int Value       { get; private set; }

    public event Action<int, int> OnBallLanded; // (bucketIndex, value) — physics drops only

    public void Setup(int index, int value, float width)
    {
        BucketIndex = index;
        Value       = value;

        var col     = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size      = new Vector2(width * 0.88f, 1f);
        col.offset    = new Vector2(0f, 0.5f);

        if (_valueLabel != null)
            _valueLabel.text = value.ToString("N0");
    }

    public void PlayLandingEffect()
    {
        // TODO: DOTween scale pulse + glow bloom when FX are added
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ball"))
            OnBallLanded?.Invoke(BucketIndex, Value);
    }
}
```

`SimulateLanding()` is removed. Seeded drops fire `OnBallLanded` through `GameBootstrapper` directly after `SeededPathAnimator.OnPathComplete` — the view only handles visual feedback.

#### BallView.cs (Unity 6 API, DOTween for seeded mode)

```csharp
// Assets/_Pachinko/Scripts/Views/BallView.cs
using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
public class BallView : MonoBehaviour
{
    private Rigidbody2D _rb;

    public event Action OnSeededPathComplete;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        tag = "Ball";
    }

    private void OnEnable()
    {
        // Reset physics state when rented from pool
        _rb.linearVelocity = Vector2.zero;  // Unity 6 API (not velocity)
        _rb.angularVelocity = 0f;
    }

    public void SetupPhysics(BoardConfig cfg)
    {
        _rb.mass                  = cfg.ballMass;
        _rb.gravityScale          = cfg.gravityScale;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.interpolation          = RigidbodyInterpolation2D.Interpolate;
        _rb.sleepMode              = RigidbodySleepMode2D.NeverSleep;
        _rb.linearDamping          = 0f;
        _rb.angularDamping         = 0f;
        _rb.isKinematic            = false;
        GetComponent<CircleCollider2D>().radius = cfg.pegRadius * 0.8f;
    }

    // Normal physics drop: random sideways impulse only (ball falls under gravity)
    public void LaunchPhysics(float impulseRange)
    {
        float x = UnityEngine.Random.Range(-impulseRange, impulseRange);
        _rb.AddForce(new Vector2(x, 0f), ForceMode2D.Impulse);
    }

    // Seeded drop: DOTween per-arc bezier with gravity-authentic timing
    public void LaunchSeeded(Vector3[] waypoints, int rows)
    {
        _rb.isKinematic    = true;
        _rb.linearVelocity = Vector2.zero;

        DOTween.Kill(transform);
        StartCoroutine(AnimateArcs(waypoints, rows));
    }

    private IEnumerator AnimateArcs(Vector3[] waypoints, int rows)
    {
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            float duration = SeededPathAnimator.BaseDuration /
                             (1f + i * SeededPathAnimator.SpeedFactor);
            duration = Mathf.Max(SeededPathAnimator.MinHopSeconds, duration);

            var tween = transform.DOMove(waypoints[i + 1], duration)
                .SetEase(Ease.InQuad);
            yield return tween.WaitForCompletion();
        }

        OnSeededPathComplete?.Invoke();
    }

    private void OnDisable()
    {
        DOTween.Kill(transform);
        OnSeededPathComplete = null;
    }
}
```

**Why DOTween?** DOTween's `Ease.InQuad` per arc gives gravity-authentic acceleration within each hop. The `hopDuration` formula ensures the ball speeds up as rows increase — identical to real Plinko physics feel. Install DOTween via Asset Store or `asmdef` reference.

#### BoardView.cs

```csharp
// Assets/_Pachinko/Scripts/Views/BoardView.cs
using System.Collections.Generic;
using UnityEngine;

public class BoardView : MonoBehaviour
{
    [SerializeField] private PegPool          _pegPool;
    [SerializeField] private GameObject       _bucketPrefab;
    [SerializeField] private GameObject       _wallPrefab;

    private readonly List<ScoreBucketView> _buckets = new();
    private readonly List<GameObject>      _walls   = new();

    public IReadOnlyList<ScoreBucketView> Buckets => _buckets;

    public void Build(BoardConfig cfg, PegBoardLayout layout)
    {
        // Return pooled pegs and destroy transient objects
        _pegPool.ReturnAll();
        foreach (var w in _walls)  Destroy(w);
        foreach (var b in _buckets) Destroy(b.gameObject);
        _walls.Clear();
        _buckets.Clear();

        // Spawn pegs from pool
        foreach (var pos in layout.PegPositions)
        {
            var go = _pegPool.Rent();
            go.transform.position = pos;
            go.GetComponent<PegView>().Setup(cfg.pegRadius, cfg.pegRestitution);
        }

        // Spawn buckets (transient — different configs may have different counts)
        for (int i = 0; i < cfg.bucketValues.Length; i++)
        {
            var go   = Instantiate(_bucketPrefab, layout.BucketPositions[i], Quaternion.identity, transform);
            var view = go.GetComponent<ScoreBucketView>();
            view.Setup(i, cfg.bucketValues[i], layout.BucketWidth);
            _buckets.Add(view);
        }

        // Walls
        SpawnWall(new Vector2(-layout.BoardWidth / 2f - 0.3f, 0f),
                  new Vector2(0.6f, layout.BoardHeight + 3f));
        SpawnWall(new Vector2(layout.BoardWidth / 2f + 0.3f, 0f),
                  new Vector2(0.6f, layout.BoardHeight + 3f));
    }

    private void SpawnWall(Vector2 pos, Vector2 size)
    {
        var wall = Instantiate(_wallPrefab, pos, Quaternion.identity, transform);
        wall.GetComponent<BoxCollider2D>().size = size;
        _walls.Add(wall);
    }
}
```

#### UIController.cs (no reference to GameBootstrapper — event-driven only)

```csharp
// Assets/_Pachinko/Scripts/Views/UIController.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _scoreLabel;
    [SerializeField] private TextMeshProUGUI _chipsLabel;
    [SerializeField] private TextMeshProUGUI _roundResultLabel;
    [SerializeField] private Button          _dropButton;

    public event Action OnDropPressed;

    private void Awake()
    {
        _dropButton.onClick.AddListener(() => OnDropPressed?.Invoke());
        _roundResultLabel.gameObject.SetActive(false);
    }

    public void UpdateHUD(int score, int chipsLeft, bool dropEnabled)
    {
        _scoreLabel.text          = $"Score: {score:N0}";
        _chipsLabel.text          = $"Chips: {chipsLeft}";
        _dropButton.interactable  = dropEnabled;
    }

    public void ShowRoundResult(int total)
    {
        _roundResultLabel.text = $"Round over!  Total: {total:N0}";
        _roundResultLabel.gameObject.SetActive(true);
        _dropButton.interactable = false;
    }

    public void HideRoundResult() => _roundResultLabel.gameObject.SetActive(false);
}
```

`UIController` raises events (OnDropPressed) and exposes update methods. It holds no reference to `GameBootstrapper` — dependency flows one way.

#### GameBootstrapper.cs (MonoBehaviour wiring — replaces old "GameController" MB)

```csharp
// Assets/_Pachinko/Scripts/Views/GameBootstrapper.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

public class GameBootstrapper : MonoBehaviour
{
    [SerializeField] private BoardView   _boardView;
    [SerializeField] private UIController _ui;
    [SerializeField] private BallView    _ballPrefab;
    [SerializeField] private string      _boardId = "default";

    private GameController      _game;
    private ScoreManager        _score;
    private PegBoardLayout      _layout;
    private SeededPathAnimator  _seededAnimator;
    private BoardConfig         _cfg;
    private ObjectPool<BallView> _ballPool;
    private BallView            _activeBall;
    private int                 _dropIndex;
    private Coroutine           _resolveCoroutine;

    private IEnumerator Start()
    {
        // Load config (sync for desktop; swap to UWR coroutine for Android/WebGL)
        IConfigProvider provider = new LocalJsonConfigProvider();
        _cfg = provider.GetBoard(_boardId);

        _layout         = new PegBoardLayout();
        _layout.Build(_cfg);
        _game           = new GameController();
        _score          = new ScoreManager();
        _seededAnimator = new SeededPathAnimator();

        // Ball pool
        _ballPool = new ObjectPool<BallView>(
            createFunc:      () => Instantiate(_ballPrefab),
            actionOnGet:     b  => b.gameObject.SetActive(true),
            actionOnRelease: b  => b.gameObject.SetActive(false),
            actionOnDestroy: b  => Destroy(b.gameObject),
            defaultCapacity: 3
        );

        // Wire domain events (graph of subscriptions, not a linear chain)
        _ui.OnDropPressed              += HandleDropPressed;
        _game.OnStateChanged           += HandleStateChanged;
        _game.OnBallLanded             += HandleBallLandedEvent;
        _game.OnRoundEnded             += total => _ui.ShowRoundResult(total);
        _score.OnScoreChanged          += (_, total) => _ui.UpdateHUD(total, _game.ChipsLeft, _game.CanDrop());

        foreach (var bucket in _boardView.Buckets)
            bucket.OnBallLanded += HandlePhysicsBucketLanded;

        // Build board and subscribe bucket events
        _boardView.Build(_cfg, _layout);
        foreach (var bucket in _boardView.Buckets)  // subscribe AFTER Build
            bucket.OnBallLanded += HandlePhysicsBucketLanded;

        // Start round
        _dropIndex = 0;
        _game.ConfigLoaded(_cfg.chipsPerRound);
        _score.Reset();
        _ui.UpdateHUD(0, _cfg.chipsPerRound, true);
        FitCamera();
        yield return null;
    }

    private void HandleDropPressed()
    {
        if (!_game.CanDrop()) return;

        _activeBall = _ballPool.Get();
        var dropPos = new Vector3(0f, _layout.TopY + 0.5f, 0f);
        _activeBall.transform.position = dropPos;
        _activeBall.SetupPhysics(_cfg);

        var seeded = GetSeededDrop(_dropIndex);
        _game.BeginDrop(_dropIndex);
        _dropIndex++;

        if (seeded != null)
        {
            // Bounds-check against live Buckets list (separate from bucketValues.Length)
            int idx = seeded.targetBucketIndex;
            if (idx < 0 || idx >= _boardView.Buckets.Count)
            {
                Debug.LogError($"[GameBootstrapper] seededDrop targetBucketIndex {idx} " +
                               $"out of Buckets range ({_boardView.Buckets.Count}). Falling back to physics.");
                _activeBall.LaunchPhysics(_cfg.randomImpulseRange);
                return;
            }

            float targetX  = _boardView.Buckets[idx].transform.position.x;
            var   targetPos = new Vector3(targetX, _layout.BottomY, 0f);
            var   path      = _seededAnimator.GeneratePath(dropPos, targetPos, _cfg, _layout);

            _activeBall.OnSeededPathComplete += () =>
                HandlePhysicsBucketLanded(idx, _cfg.bucketValues[idx]);

            _activeBall.LaunchSeeded(path, _cfg.rows);
        }
        else
        {
            _activeBall.LaunchPhysics(_cfg.randomImpulseRange);
        }
    }

    // Physics landing: fired by ScoreBucketView.OnTriggerEnter2D
    private void HandlePhysicsBucketLanded(int bucketIndex, int value)
    {
        if (_game.State != GameState.Dropping) return; // double-score guard (second line of defense)
        _game.BallLanded(value);
    }

    // GameController domain event: both physics and seeded drops end here
    private void HandleBallLandedEvent(int value)
    {
        _score.AddScore(value);
        if (_activeBall != null)
        {
            var ball = _activeBall;
            _activeBall = null;
            if (_resolveCoroutine != null) StopCoroutine(_resolveCoroutine);
            _resolveCoroutine = StartCoroutine(ReturnBallAfterDelay(ball, 0.4f));
        }
        if (_resolveCoroutine != null) StopCoroutine(_resolveCoroutine);
        _resolveCoroutine = StartCoroutine(ResolveAfterDelay(_cfg.resolveDelay));
    }

    private IEnumerator ReturnBallAfterDelay(BallView ball, float delay)
    {
        yield return new WaitForSeconds(delay);
        _ballPool.Release(ball);
    }

    private IEnumerator ResolveAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        _game.ResolveComplete(_score.RoundTotal);
    }

    private void HandleStateChanged(GameState state)
    {
        _ui.UpdateHUD(_score.RoundTotal, _game.ChipsLeft, _game.CanDrop());
    }

    private SeededDrop GetSeededDrop(int dropIndex)
    {
        if (_cfg.seededDrops == null) return null;
        foreach (var s in _cfg.seededDrops)
            if (s.chipIndex == dropIndex) return s;
        return null;
    }

    private void FitCamera()
    {
        Camera.main.orthographicSize = (_layout.BoardHeight + 4f) / 2f;
        Camera.main.transform.position = new Vector3(0f, 0f, -10f);
    }

    private void OnDestroy()
    {
        _ballPool?.Dispose();
    }
}
```

**Key fixes from research:**
- Bucket events subscribed **only after** `_boardView.Build()` — original plan subscribed twice (before + after), causing double-score on every physics landing
- `Invoke()` replaced with a tracked coroutine + `StopCoroutine` for clean cancellation
- `resolveDelay` comes from config, not a magic number
- `ObjectPool<BallView>` (Unity built-in) replaces `Destroy(go, 0.5f)`

---

### JSON Config (`StreamingAssets/boards/default.json`)

```json
{
  "boardId": "default",
  "rows": 8,
  "cols": 9,
  "pegSpacing": 1.2,
  "pegRadius": 0.15,
  "bucketValues": [100, 500, 1000, 10000, 1000, 500, 100],
  "chipsPerRound": 5,
  "ballMass": 1.0,
  "gravityScale": 2.0,
  "pegRestitution": 0.55,
  "randomImpulseRange": 2.5,
  "resolveDelay": 0.6,
  "seededDrops": []
}
```

---

## Physics Configuration (Editor settings — not in code)

### Project Settings → Physics 2D
| Setting | Value | Reason |
|---|---|---|
| Velocity Threshold (Bounce) | **0.5** | Default 1.0 silently suppresses low-speed bounces |
| Default Solver Iterations | 6 | Default; increase to 8 if jitter at rest |
| Fixed Timestep | 0.02 | Default; drop to 0.01 only if tunneling persists |

### Layers & Collision Matrix
| Layer | Objects |
|---|---|
| `Ball` | Ball prefab |
| `Peg` | Peg prefab |
| `Bucket` | ScoreBucket prefab |
| `Wall` | Wall prefab |

Disable **Peg vs Peg** in the Physics 2D collision matrix — pegs never collide with each other. This eliminates unnecessary broad-phase checks for 68 objects.

### PhysicsMaterial2D (PegBounce.physicsmaterial2d)
| Property | Value |
|---|---|
| Bounciness | 0.55 |
| Friction | 0.0 |
| Bounce Combine | **Maximum** |
| Friction Combine | Minimum |

Assign to Peg prefab's CircleCollider2D in the Inspector (not created at runtime).

### Rigidbody2D (Ball prefab)
| Property | Value |
|---|---|
| Collision Detection | **Continuous** (prevents tunneling) |
| Interpolation | **Interpolate** (smooth between physics ticks) |
| Sleep Mode | **Never Sleep** |
| Linear Damping | 0 |
| Angular Damping | 0 |

---

## Implementation Phases

### Phase 1 — Packages, Config & Pure C# Core
**Files:** `SeededDrop.cs`, `BoardConfig.cs`, `IConfigProvider.cs`, `LocalJsonConfigProvider.cs`, `GameController.cs`, `ScoreManager.cs`, `PegBoardLayout.cs`, `SeededPathAnimator.cs`, `default.json`

Tasks:
- [ ] Add Newtonsoft.Json (jillejr package) to `Packages/manifest.json`
- [ ] Add DOTween to project (Asset Store free version or package)
- [ ] Create `Assets/_Pachinko/Scripts/Config/` and `Core/` and `Views/` folders
- [ ] Write `SeededDrop.cs` (own file)
- [ ] Write `BoardConfig.cs` with `Validate()` method
- [ ] Write `IConfigProvider.cs` interface
- [ ] Write `LocalJsonConfigProvider.cs` with path-traversal guard
- [ ] Write `GameController.cs` state machine with `Loading` state and valid-transition guard
- [ ] Write `ScoreManager.cs` with `IScoreModifier` pipeline
- [ ] Write `PegBoardLayout.cs` (quincunx spacing: `spacingY = pegSpacing * 0.866f`)
- [ ] Write `SeededPathAnimator.cs` (no FindNearPegs — Phase 1 dead code)
- [ ] Create `Assets/StreamingAssets/boards/default.json`
- [ ] Delete `Assets/_Pachinko/Scripts/Pachinko.cs`

**Exit criteria:** `LocalJsonConfigProvider.GetBoard("default")` returns a valid `BoardConfig`. `PegBoardLayout.Build(cfg)` produces correct peg count: `rows × cols - floor(rows/2)` for default 8×9 = 68 pegs.

---

### Phase 2 — Views & Procedural Board
**Files:** `PegView.cs`, `PegPool.cs`, `ScoreBucketView.cs`, `BoardView.cs`

Tasks:
- [ ] Write `PegView.cs` (shared PhysicsMaterial2D — NOT `new` per peg)
- [ ] Write `PegPool.cs` (pre-warm 80 pegs in Awake)
- [ ] Write `ScoreBucketView.cs` (no `SimulateLanding` — `PlayLandingEffect` for visuals only)
- [ ] Write `BoardView.cs` (PegPool.ReturnAll on rebuild, not Destroy)
- [ ] **In Unity Editor:** Create `Peg` prefab (white circle sprite + PegView + CircleCollider2D)
- [ ] **In Unity Editor:** Assign `PegBounce.physicsmaterial2d` to Peg prefab's CircleCollider2D
- [ ] **In Unity Editor:** Set Peg prefab layer to `Peg`
- [ ] **In Unity Editor:** Create `ScoreBucket` prefab (rectangle sprite + ScoreBucketView + BoxCollider2D + TMP label)
- [ ] **In Unity Editor:** Set ScoreBucket prefab layer to `Bucket`
- [ ] **In Unity Editor:** Create `Wall` prefab (BoxCollider2D only, no renderer)
- [ ] **In Unity Editor:** Create `PegPool` GameObject, wire peg prefab, set initial size 80
- [ ] **In Unity Editor:** Create `BoardView` GameObject, wire PegPool + bucket/wall prefabs
- [ ] **In Project Settings → Physics 2D:** Lower Velocity Threshold to 0.5
- [ ] **In Project Settings → Physics 2D → Layer Collision Matrix:** Disable Peg vs Peg

**Exit criteria:** `boardView.Build(cfg, layout)` spawns 68 pegs at correct staggered positions and 7 buckets at the bottom.

---

### Phase 3 — Ball Physics & Seeded Mode
**Files:** `BallView.cs`

Tasks:
- [ ] Write `BallView.cs` (Unity 6 `linearVelocity`, Continuous CCD, DOTween arcs, OnEnable reset)
- [ ] **In Unity Editor:** Create `Ball` prefab (white circle sprite + BallView + Rigidbody2D + CircleCollider2D)
- [ ] **In Unity Editor:** Set Ball prefab tag to `Ball`, layer to `Ball`
- [ ] **In Unity Editor:** Set Rigidbody2D: Collision Detection = Continuous, Interpolation = Interpolate, Sleep Mode = Never Sleep
- [ ] **In Project Settings → Physics 2D → Layer Collision Matrix:** Confirm Ball collides with Peg and Wall; disable Ball vs Bucket solid (bucket uses trigger)

**Exit criteria:** Ball spawned at top falls under gravity, bounces off pegs, enters bucket trigger. `LaunchSeeded(path, rows)` animates ball to target bucket with accelerating hops.

---

### Phase 4 — UI & Orchestration
**Files:** `UIController.cs`, `GameBootstrapper.cs`

Tasks:
- [ ] Write `UIController.cs` (no GameBootstrapper reference — events only)
- [ ] Write `GameBootstrapper.cs` (IConfigProvider injection, ObjectPool<BallView>, tracked resolveCoroutine, bucket events subscribed **after** Build)
- [ ] **In Unity Editor:** Set up Canvas: Score TMP label, Chips TMP label, Round Result TMP label (inactive), Drop Button
- [ ] **In Unity Editor:** Add UIController to Canvas, wire labels and button
- [ ] **In Unity Editor:** Add GameBootstrapper to scene root, wire _boardView, _ui, _ballPrefab
- [ ] **In Unity Editor:** Set _boardId = "default" in Inspector

**Exit criteria:** Full round: 5 chips drop, each scores the landing bucket value, UI updates each drop, Round Result appears, Drop button disabled during ball flight.

---

### Phase 5 — Seeded Drop Verification
Tasks:
- [ ] Edit `default.json`: add `"seededDrops": [{ "chipIndex": 0, "targetBucketIndex": 3 }]`
- [ ] Play in Editor — first drop animates to bucket index 3 (the 10000 bucket)
- [ ] Verify score = 10000
- [ ] Test `chipIndex` beyond `chipsPerRound-1` is gracefully ignored
- [ ] Test `targetBucketIndex` out of range logs error and falls back to physics
- [ ] Revert `seededDrops` to `[]`, verify chip 0 is random again

---

## System-Wide Impact

### Interaction Graph (event subscriptions, not a linear chain)
```
UIController.OnDropPressed
  → GameBootstrapper.HandleDropPressed
      → GameController.BeginDrop         (Idle → Dropping, fires OnDropStarted)
      → BallView.LaunchPhysics/LaunchSeeded

ScoreBucketView.OnTriggerEnter2D (physics only)
  → GameBootstrapper.HandlePhysicsBucketLanded
      → GameController.BallLanded(value)  (Dropping → Resolving, fires OnBallLanded)

BallView.OnSeededPathComplete (seeded only)
  → GameBootstrapper.HandlePhysicsBucketLanded (same handler — uniform path)
      → GameController.BallLanded(value)

GameController.OnBallLanded
  → GameBootstrapper.HandleBallLandedEvent
      → ScoreManager.AddScore(value)

ScoreManager.OnScoreChanged
  → UIController.UpdateHUD

GameController.OnRoundEnded
  → UIController.ShowRoundResult

ResolveAfterDelay coroutine
  → GameController.ResolveComplete        (Resolving → Idle or RoundEnd)
```

### Error & Failure Propagation
- `LocalJsonConfigProvider.GetBoard` throws `FileNotFoundException` / `InvalidOperationException` — caught in `GameBootstrapper.Start` (wrap in try/catch, disable gameplay with error message)
- `targetBucketIndex` out of Buckets range — logged, falls back to physics drop (no crash)
- Ball exits board without hitting bucket — `BallView` can self-destruct after `maxFallTime` with a `GameBootstrapper.HandleBallLanded(0)` call (add in a future polish pass)
- Double trigger fire — `GameController.State != Dropping` guard in `HandlePhysicsBucketLanded` (state machine as second line of defense; `BallView` as first)

### State Lifecycle Risks
- `resolveCoroutine` cancelled with `StopCoroutine` before new one starts — no stale `Invoke` callbacks
- `_activeBall` nulled before `ReturnBallAfterDelay` completes — local copy `var ball = _activeBall` captures the reference safely
- Bucket events subscribed twice (original bug) — fixed: subscribe only after `Build()`
- `ObjectPool` released ball re-activated with stale velocity — `BallView.OnEnable` resets `linearVelocity` and `angularVelocity`

---

## Acceptance Criteria

### Functional
- [ ] `default.json` loads without error in Play Mode on Windows
- [ ] Board spawns 68 pegs in staggered layout (8 rows × 9/8 alternating cols)
- [ ] 7 score buckets appear at the bottom with correct values
- [ ] Ball spawns at top-center and falls under physics, bouncing off pegs
- [ ] Ball enters bucket trigger and fires score event with correct value
- [ ] Seeded drop: ball animates with accelerating hops to target bucket index
- [ ] Seeded drop scores identically to physics drop
- [ ] UI shows score total and chips-left, updating after each drop
- [ ] Drop button disabled during ball flight; re-enabled after resolve delay
- [ ] Round ends after 5 drops; Round Result shows correct total
- [ ] `seededDrops: []` = fully random round; no seeded behavior
- [ ] Changing `rows`, `cols`, `pegSpacing` in JSON regenerates board on Play

### Non-Functional
- [ ] No hardcoded numbers in any `.cs` file — all tunables from `BoardConfig`
- [ ] `GameController`, `ScoreManager`, `PegBoardLayout`, `SeededPathAnimator` have zero `MonoBehaviour` dependencies
- [ ] No `FindObjectOfType` calls — all refs via Inspector `[SerializeField]`
- [ ] Bucket events subscribed exactly once (after `Build()`)
- [ ] `new PhysicsMaterial2D()` called at most once (shared static instance on `PegView`)
- [ ] `Invoke()` not used anywhere — only tracked coroutines
- [ ] Ball pooled via `ObjectPool<BallView>` — no `Instantiate/Destroy` per drop
- [ ] Physics 2D Velocity Threshold = 0.5 in Project Settings

---

## Dependencies & Prerequisites

| Item | Status | Notes |
|---|---|---|
| Unity 6 (6000.4.7f1) | ✅ Installed | |
| URP 2D Renderer | ✅ Configured | |
| Newtonsoft.Json (jillejr) | Must add to manifest.json | IL2CPP-safe |
| DOTween | Must install | Asset Store free; required for seeded arcs |
| TextMeshPro | Must import TMP Essential Resources | `Window → TMP → Import TMP Essential Resources` |
| Kenney Shape Characters | ⏳ Not yet | Placeholder sprites for now |

---

## Manual Unity Editor Setup (After Writing Scripts)

1. **Import TMP**: `Window → TextMeshPro → Import TMP Essential Resources`
2. **Add Newtonsoft.Json**: edit `Packages/manifest.json` (see Phase 1)
3. **Install DOTween**: from Asset Store or `.unitypackage`
4. **Create Physics 2D Settings**:
   - Lower Velocity Threshold from 1.0 → 0.5
   - Add layers: `Ball`, `Peg`, `Bucket`, `Wall`
   - Disable Peg vs Peg in collision matrix
5. **Create `PegBounce.physicsmaterial2d`**: bounciness 0.55, friction 0, Bounce Combine = Maximum
6. **Peg prefab**: circle sprite + CircleCollider2D (assign PegBounce material) + PegView, layer = Peg
7. **Ball prefab**: circle sprite + Rigidbody2D (Continuous, Interpolate, Never Sleep) + CircleCollider2D + BallView, tag = Ball, layer = Ball
8. **ScoreBucket prefab**: rectangle sprite + BoxCollider2D (isTrigger) + ScoreBucketView + TMP child, layer = Bucket
9. **Wall prefab**: BoxCollider2D only (no renderer), layer = Wall
10. **Scene setup**: GameBootstrapper on root → wire all refs. Canvas → UIController → wire labels/button. PegPool on its own GO.

---

## Risk Analysis

| Risk | Likelihood | Mitigation |
|---|---|---|
| Ball tunnels through peg at high speed | Medium | Continuous CCD on Rigidbody2D; max ball speed bounded by gravityScale config |
| Velocity Threshold not set → bounces suppressed | High (easy to forget) | Add to Phase 2 acceptance criteria |
| Double bucket trigger fire | Medium | State guard in HandlePhysicsBucketLanded; subscribe after Build() |
| Seeded path looks artificial | Medium | Gravity-accelerating hopDuration + InQuad ease; tune BaseDuration in config |
| DOTween not imported → BallView compile error | Medium | Document as prerequisite; fail-fast at compile time |
| JsonUtility used instead of Newtonsoft → power-up config breaks | High | Use Newtonsoft from day one |
| boardId path traversal | Low | Regex allowlist guard in LocalJsonConfigProvider |

---

## Future Considerations

- **Power-ups**: `IScoreModifier` pipeline ready. Add `PowerUpManager` that registers modifiers on activation.
- **Remote config**: implement `RemoteConfigProvider : IConfigProvider`, inject instead of `LocalJsonConfigProvider` in `GameBootstrapper`. Zero other changes.
- **Android/WebGL**: wrap `LocalJsonConfigProvider` with `UnityWebRequest` coroutine behind `#if UNITY_ANDROID || UNITY_WEBGL`.
- **Peg hit FX**: `PegView.OnCollisionEnter2D` fires — add DOPunchScale + audio. Re-add `SeededPathAnimator.FindNearPegs()` for seeded cosmetic hits.
- **Multiple boards**: `_boardId` is Inspector-configurable; load `hard.json`, `easy.json`, etc.
- **Kenney Shape Characters**: swap circle sprites in prefabs — no code changes.
- **Neon FX**: add URP 2D Light to Peg/Bucket prefabs; `ScoreBucketView.PlayLandingEffect()` already exists as an extension point.

---

## Sources & References

### Origin
- **Brainstorm:** [docs/brainstorms/2026-05-21-plinko-game-brainstorm.md](../brainstorms/2026-05-21-plinko-game-brainstorm.md)
  - Key decisions: JSON-only config, procedural board, seeded bezier animation

### Research Findings Applied
- Unity 6 `linearVelocity` API, Continuous CCD, Velocity Threshold — Physics research agent
- Newtonsoft.Json `PopulateObject` pattern, cross-platform StreamingAssets — Config research agent
- Per-arc Bezier + gravity-authentic `hopDuration` formula — Game design research agent
- `IConfigProvider` interface, `IScoreModifier` pipeline, `Loading` state — Architecture agent
- Shared `PhysicsMaterial2D`, `PegPool`, tracked coroutine — Performance agent
- Removed `FindNearPegs`, `GetBucketWorldX`, `LastBucketValue`, double-subscription — Simplicity agent
- `GameBootstrapper` rename, `OnEnable/OnDisable` subscription, `SeededDrop.cs` own file — Patterns agent
- `BoardConfig.Validate()`, path traversal guard, double bounds-check — Security agent

### Unity Documentation
- [Rigidbody2D.linearVelocity (Unity 6)](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody2D-linearVelocity.html)
- [CollisionDetectionMode2D.Continuous](https://docs.unity3d.com/6000.2/Documentation/ScriptReference/CollisionDetectionMode2D.Continuous.html)
- [Physics 2D Velocity Threshold / Bounce](https://docs.unity3d.com/6000.2/Documentation/Manual/collider-surface-bounce.html)
- [ObjectPool<T> Unity built-in](https://docs.unity3d.com/ScriptReference/Pool.ObjectPool_1.html)
- [DOTween Documentation](https://dotween.demigiant.com/documentation.php)
- [Newtonsoft.Json-for-Unity (jillejr)](https://github.com/applejag/Newtonsoft.Json-for-Unity)
