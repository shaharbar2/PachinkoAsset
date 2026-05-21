using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates all Pachinko prefabs and the default board prefab with pre-placed pegs/buckets.
/// Run via: -executeMethod PachinkoSetup.CreateAll
/// </summary>
public static class PachinkoSetup
{
    private const string PrefabRoot  = "Assets/_Pachinko/Prefabs";
    private const string BoardsRoot  = "Assets/_Pachinko/Prefabs/Boards";

    public static void CreateAll()
    {
        EnsureFolders();
        EnsureLayers();

        var pegPrefab    = CreatePegPrefab();
        var ballPrefab   = CreateBallPrefab();
        var bucketPrefab = CreateBucketPrefab();
        CreateBoardDefaultPrefab(pegPrefab, bucketPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PachinkoSetup] All prefabs created successfully.");
    }

    // ── Peg ──────────────────────────────────────────────────────────────────

    private static GameObject CreatePegPrefab()
    {
        string path = $"{PrefabRoot}/Peg.prefab";

        var go = new GameObject("Peg");

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color  = Color.white;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.15f;

        // Shared physics material
        var mat = new PhysicsMaterial2D("PegBounce")
            { bounciness = 0.55f, friction = 0f };
        AssetDatabase.CreateAsset(mat, "Assets/_Pachinko/Prefabs/PegBounce.physicsmaterial2d");
        col.sharedMaterial = mat;

        go.AddComponent<PegView>();

        SetLayer(go, "Peg");

        var prefab = SavePrefab(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ── Ball ─────────────────────────────────────────────────────────────────

    private static GameObject CreateBallPrefab()
    {
        string path = $"{PrefabRoot}/Ball.prefab";

        var go = new GameObject("Ball");
        go.tag = "Ball";

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color  = new Color(1f, 0.9f, 0.2f); // yellow-gold

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.12f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation           = RigidbodyInterpolation2D.Interpolate;
        rb.sleepMode               = RigidbodySleepMode2D.NeverSleep;
        rb.linearDamping           = 0f;
        rb.angularDamping          = 0f;

        go.AddComponent<BallView>();

        SetLayer(go, "Ball");

        var prefab = SavePrefab(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ── Score Bucket ─────────────────────────────────────────────────────────

    private static GameObject CreateBucketPrefab()
    {
        string path = $"{PrefabRoot}/ScoreBucket.prefab";

        var go = new GameObject("ScoreBucket");

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();
        sr.color  = new Color(0.2f, 0.4f, 1f, 0.6f);

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size      = new Vector2(1f, 1f);
        col.offset    = new Vector2(0f, 0.5f);

        go.AddComponent<ScoreBucketView>();

        SetLayer(go, "Bucket");

        var prefab = SavePrefab(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ── Board Default (pre-placed pegs + buckets) ─────────────────────────────

    private static void CreateBoardDefaultPrefab(GameObject pegPrefab, GameObject bucketPrefab)
    {
        string path = $"{BoardsRoot}/BoardDefault.prefab";

        // Default config values (must match default.json)
        const int   rows        = 8;
        const int   cols        = 9;
        const float pegSpacing  = 1.2f;
        const float spacingY    = pegSpacing * 0.866f; // equilateral triangle rows
        int[]       bucketVals  = { 100, 500, 1000, 10000, 1000, 500, 100 };

        float boardWidth  = (cols - 1) * pegSpacing;
        float boardHeight = (rows - 1) * spacingY;
        float topY        = boardHeight / 2f;
        float bottomY     = -boardHeight / 2f - spacingY;

        var root = new GameObject("BoardDefault");
        root.AddComponent<BoardView>();

        // Drop point (empty child — marks where ball spawns)
        var dropPt = new GameObject("DropPoint");
        dropPt.transform.SetParent(root.transform, false);
        dropPt.transform.localPosition = new Vector3(0f, topY + 0.8f, 0f);

        // Wire DropPoint to BoardView via SerializedObject after creation
        // (done programmatically below after saving to disk)

        // Pegs
        var pegsParent = new GameObject("Pegs");
        pegsParent.transform.SetParent(root.transform, false);

        for (int row = 0; row < rows; row++)
        {
            bool  isEven  = (row % 2 == 0);
            int   count   = isEven ? cols : cols - 1;
            float rowY    = topY - row * spacingY;
            float startX  = isEven
                ? -boardWidth / 2f
                : -boardWidth / 2f + pegSpacing / 2f;

            for (int col = 0; col < count; col++)
            {
                var peg = (GameObject)PrefabUtility.InstantiatePrefab(pegPrefab);
                peg.name = $"Peg_R{row}_C{col}";
                peg.transform.SetParent(pegsParent.transform, false);
                peg.transform.localPosition = new Vector3(startX + col * pegSpacing, rowY, 0f);
            }
        }

        // Buckets
        var bucketsParent = new GameObject("Buckets");
        bucketsParent.transform.SetParent(root.transform, false);

        float bucketStep = boardWidth / (bucketVals.Length - 1);
        for (int i = 0; i < bucketVals.Length; i++)
        {
            var bucket = (GameObject)PrefabUtility.InstantiatePrefab(bucketPrefab);
            bucket.name = $"Bucket_{i}";
            bucket.transform.SetParent(bucketsParent.transform, false);
            bucket.transform.localPosition = new Vector3(
                -boardWidth / 2f + i * bucketStep, bottomY - 0.3f, 0f);
        }

        // Walls (simple BoxCollider2D, no renderer)
        CreateWall(root, "WallLeft",
            new Vector3(-boardWidth / 2f - 0.4f, 0f, 0f),
            new Vector2(0.6f, boardHeight + 3f));
        CreateWall(root, "WallRight",
            new Vector3(boardWidth / 2f + 0.4f, 0f, 0f),
            new Vector2(0.6f, boardHeight + 3f));

        // Save to disk
        bool success;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out success);
        Object.DestroyImmediate(root);

        if (!success)
        {
            Debug.LogError($"[PachinkoSetup] Failed to save BoardDefault prefab at {path}");
            return;
        }

        // Wire _dropPoint on the saved prefab via SerializedObject
        using (var so = new SerializedObject(prefab.GetComponent<BoardView>()))
        {
            var dropProp = so.FindProperty("_dropPoint");
            if (dropProp != null)
            {
                var savedDrop = prefab.transform.Find("DropPoint");
                if (savedDrop != null)
                    dropProp.objectReferenceValue = savedDrop;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        PrefabUtility.SavePrefabAsset(prefab);
        Debug.Log($"[PachinkoSetup] BoardDefault prefab created at {path}");
    }

    private static void CreateWall(GameObject parent, string name, Vector3 localPos, Vector2 size)
    {
        var wall = new GameObject(name);
        wall.transform.SetParent(parent.transform, false);
        wall.transform.localPosition = localPos;

        var col = wall.AddComponent<BoxCollider2D>();
        col.size = size;
        SetLayer(wall, "Wall");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GameObject SavePrefab(GameObject go, string path)
    {
        return PrefabUtility.SaveAsPrefabAsset(go, path);
    }

    private static void EnsureFolders()
    {
        foreach (var folder in new[] { PrefabRoot, BoardsRoot })
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(
                    Path.GetDirectoryName(folder).Replace('\\', '/'),
                    Path.GetFileName(folder));
    }

    private static void EnsureLayers()
    {
        AddLayerIfMissing("Ball");
        AddLayerIfMissing("Peg");
        AddLayerIfMissing("Bucket");
        AddLayerIfMissing("Wall");
    }

    private static void AddLayerIfMissing(string layerName)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));
        var layers = tagManager.FindProperty("layers");

        for (int i = 0; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return;

        // Find first empty user layer slot (slots 0–7 are built-in)
        for (int i = 8; i < layers.arraySize; i++)
        {
            var element = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(element.stringValue))
            {
                element.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[PachinkoSetup] Added layer '{layerName}' at index {i}.");
                return;
            }
        }
        Debug.LogWarning($"[PachinkoSetup] No empty layer slot for '{layerName}'.");
    }

    private static void SetLayer(GameObject go, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0) go.layer = layer;
    }

    // Generates a white circle sprite from a Texture2D (8×8 circle mask)
    private static Sprite CreateCircleSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float r      = size / 2f - 1f;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float a  = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateSquareSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
