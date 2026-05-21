# Brainstorm: Plinko/Pachinko Game — Unity 6 2D

**Date:** 2026-05-21  
**Status:** Ready for planning

---

## What We're Building

A Plinko-style arcade game in Unity 6 (URP 2D). A ball drops from the top-center with a random sideways impulse, bounces off a staggered peg grid, and lands in a scored bucket at the bottom. The player has a configurable number of chips (balls) per round. Everything — board size, bucket values, chip count — is driven by local JSON config files (future: remote/MongoDB, no changes needed now).

Visual style: neon arcade (reference image). Assets: Kenney Shape Characters (not yet downloaded; Unity primitives as placeholders in v1).

---

## Why This Approach

**JSON-driven, procedural board generation.**

- `StreamingAssets/BoardConfig.json` is the single source of truth for board rows, columns, peg spacing, bucket values, and chip count.
- Pure C# config models (`BoardConfig`, `BucketConfig`, `GameConfig`) are deserialized at startup by a `ConfigLoader` service.
- The board (pegs + buckets) is generated procedurally at runtime — no manual scene layout.
- MonoBehaviour views are thin: they receive data from pure C# managers and render it.
- Swapping configs (difficulty, board sizes, special rounds) = swap JSON, no recompile.
- Future remote config: replace `ConfigLoader` file-read with HTTP call, nothing else changes.

---

## Key Decisions

| Decision | Choice | Reason |
|---|---|---|
| Config format | Local JSON (StreamingAssets) | Simple now, extensible to remote later |
| Board generation | Procedural at runtime from config | Scalable, no hard-coded layout |
| Ball drop | Center-top, random sideways impulse | Simple mechanic, pure physics variety |
| Bucket values | Configurable in JSON, symmetric by default (100-500-1k-10k-1k-500-100) | Tunable per board |
| Chip count | Configurable in JSON (default 5) | Matches design reference |
| Default board | 8 rows × 9 cols | Standard Plinko feel; tunable |
| Assets | Unity primitives now, Kenney Shape Characters later | Unblocked from art |
| C# architecture | Pure C# for logic, MonoBehaviour for views | Per CLAUDE.md conventions |

---

## Architecture

### Pure C# (no MonoBehaviour)

```
ConfigLoader          — reads/parses JSON from StreamingAssets
BoardConfig           — deserialized JSON model (rows, cols, spacing, restitution, etc.)
GameConfig            — chips per round, gravity scale, ball mass, random impulse range
BucketConfig          — list of bucket values + labels
GameManager           — state machine (Idle → Dropping → Resolving → RoundEnd)
ScoreManager          — tracks score, chip count, round total
PegBoardLayout        — computes staggered peg positions from BoardConfig
```

### MonoBehaviour (scene objects)

```
GameController        — root orchestrator; owns GameManager + ScoreManager; drives views
BallView              — Rigidbody2D + CircleCollider2D; receives launch impulse
PegView               — CircleCollider2D + PhysicsMaterial2D; spawned by GameController
ScoreBucketView       — BoxCollider2D trigger; fires OnBallEntered event with value
BoardView             — spawns pegs and buckets from layout data
UIController          — score label, chips-left label, drop-again button
```

### JSON Config shape (StreamingAssets/boards/default.json)

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
  "pegRestitution": 0.6,
  "randomImpulseRange": 2.5,
  "seededDrops": [
    { "chipIndex": 2, "targetBucketIndex": 3 }
  ]
}
```

`seededDrops` is optional. When present, the specified chip drop (by index) is guaranteed to land in the given bucket index. All other drops are normal physics. An empty or absent array = fully random round.

### Scene structure

```
[GameController]
  ├── [BoardView]
  │     ├── Peg_0_0 ... Peg_N_M   (spawned at runtime)
  │     └── Bucket_0 ... Bucket_8  (spawned at runtime)
  ├── [BallDropper]  (center top, spawns BallView prefab)
  └── [Canvas / UIController]
```

### Seeded Drop Implementation

When `chipIndex` matches the current drop number, `GameController` activates **path-animation mode** instead of physics:

1. Ball is spawned at the top-center (normal).
2. Physics is **disabled** (`isKinematic = true`).
3. `SeededPathAnimator` (pure C#) generates a bezier spline from top-center → target bucket, with randomised intermediate control points that pass near (not through) pegs to look natural.
4. Ball animates along the spline at normal-feeling speed.
5. At each peg the path passes close to, a collision sound/visual fires (pure cosmetic).
6. On arrival at the target bucket, `ScoreBucketView.OnBallEntered` fires as normal — scoring is identical.

**Why not physics-based seeding?** Unity physics is non-deterministic across platforms/frame rates. Pre-computing an exact impulse to hit a bucket is fragile. Path animation is reliable, looks natural at game speed, and is the standard approach in mobile games with outcome seeding.

`SeededPathAnimator` is pure C# (takes target bucket world position, returns `Vector3[]` waypoints). `BallView` consumes the waypoints in `Update`.

---

## Scope (v1 MVP)

**In:**
- JSON config loading
- Procedural peg + bucket generation
- Ball physics (Rigidbody2D, CircleCollider2D on pegs, random impulse)
- Scoring (bucket triggers, score accumulation)
- Basic UI: score total, chips left, "Drop" button
- Round flow: drop chip → wait for landing → update score → repeat until chips gone → show round total

**Out (future):**
- Power-ups (Midas Peg, Charging Chip, Bouncy Pegs)
- Neon shader effects / particle FX
- Kenney Shape Character sprites (slot exists in Inspector)
- Remote config (MongoDB)
- Multiplayer / leaderboard
- Sound effects

---

## Open Questions

_None — all resolved in brainstorm session._

---

## Resolved Questions

| Question | Answer |
|---|---|
| Board size | 8×9 default, fully configurable via JSON |
| Bucket values | Configurable JSON; symmetric default |
| Chip count | Configurable JSON; default 5 |
| Ball aim | Fixed center drop, random sideways impulse |
| Config source | Local JSON (StreamingAssets) |
| Asset state | Primitives first, Kenney later |
| Scope | Core board + physics + score + basic UI |
