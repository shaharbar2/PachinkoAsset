# PachinkoAsset

A Plinko/pachinko arcade game built in Unity 6 (URP 2D). A ball drops from the top-center of a peg board, bounces through a staggered grid, and lands in a scored bucket. Everything — board size, bucket values, chip count, physics parameters — is driven by local JSON config files.

Built as part of the **Vibe Coding** course by **SBS Games**.

---

## Features

- **JSON-driven board config** — swap `StreamingAssets/boards/default.json` to change any parameter without recompiling
- **Prefab-based board** — pegs and buckets are pre-placed in the Editor; no runtime GameObject spawning
- **Seeded drops** — script specific chip drops to land in a designated bucket via smooth bezier path animation
- **Physics** — Rigidbody2D with Continuous CCD, shared PhysicsMaterial2D, Unity 6 Physics 2D
- **Clean architecture** — pure C# for all logic (no MonoBehaviour), MonoBehaviour only for scene views
- **IScoreModifier pipeline** — ready for power-ups (Midas Peg, multipliers) with zero core code changes

---

## Getting Started

### Requirements

- Unity 6 (6000.4.7f1)
- Universal Render Pipeline (URP 17.4.0)

### Setup

1. Clone the repo and open the project in Unity Hub
2. Let Unity import packages (`com.unity.nuget.newtonsoft-json` installs automatically)
3. Import TMP Essential Resources: **Window → TextMeshPro → Import TMP Essential Resources**
4. Open `Assets/Scenes/SampleScene.unity`
5. Run **Tools → Pachinko → Setup Scene** to wire all GameObjects
6. Press **Play**

---

## Project Structure

```
Assets/_Pachinko/
├── Scripts/
│   ├── Config/          — BoardConfig, IConfigProvider, LocalJsonConfigProvider
│   ├── Core/            — GameController, ScoreManager, SeededPathAnimator
│   └── Views/           — GameBootstrapper, BoardView, BallView, PegView,
│                          ScoreBucketView, UIController
├── Editor/              — PachinkoSetup (prefab creator), PachinkoSceneSetup
└── Prefabs/
    ├── Ball.prefab
    ├── Peg.prefab
    ├── ScoreBucket.prefab
    └── Boards/BoardDefault.prefab  — 68 pre-placed pegs, 7 buckets

Assets/StreamingAssets/boards/
└── default.json         — board config (edit to tune the game)
```

---

## Configuration

Edit `Assets/StreamingAssets/boards/default.json`:

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

To script a specific drop, add to `seededDrops`:

```json
"seededDrops": [
  { "chipIndex": 0, "targetBucketIndex": 3 }
]
```

`chipIndex` is 0-based. `targetBucketIndex` 3 is the center 10000 bucket on the default board.

---

## Credits

Created by **SBS Games**
- Website: [www.sbsgames.dev](https://www.sbsgames.dev)
- Vibe Coding course: [sbsgames.dev/syllabus/vibe-coding](https://sbsgames.dev/syllabus/vibe-coding)

---

## License

MIT License

Copyright (c) 2026 SBS Games

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
