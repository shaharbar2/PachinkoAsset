using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Populates SampleScene with all GameObjects needed to play the Plinko game.
/// Run via: -executeMethod PachinkoSceneSetup.Setup
/// Also available from the menu: Tools → Pachinko → Setup Scene
/// </summary>
public static class PachinkoSceneSetup
{
    [MenuItem("Tools/Pachinko/Setup Scene")]
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",
            OpenSceneMode.Single);

        // Remove everything except the Main Camera
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.GetComponent<Camera>() == null)
                Object.DestroyImmediate(go);
        }

        // ── Board ────────────────────────────────────────────────────────────
        var boardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Pachinko/Prefabs/Boards/BoardDefault.prefab");
        if (boardPrefab == null)
        {
            Debug.LogError("[PachinkoSceneSetup] BoardDefault.prefab not found. " +
                           "Run Tools → Pachinko → Create All Prefabs first.");
            return;
        }
        var boardGo = (GameObject)PrefabUtility.InstantiatePrefab(boardPrefab);
        boardGo.name = "Board";
        boardGo.transform.position = Vector3.zero;

        // ── Ball prefab reference (not placed in scene — pooled at runtime) ──
        var ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Pachinko/Prefabs/Ball.prefab");

        // ── Camera ───────────────────────────────────────────────────────────
        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic     = true;
            cam.orthographicSize = 8f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.backgroundColor    = new Color(0.05f, 0.05f, 0.1f);
        }

        // ── Canvas / UI ──────────────────────────────────────────────────────
        var canvasGo = new GameObject("Canvas");
        var canvas   = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        var uiCtrl = canvasGo.AddComponent<UIController>();

        // Score label
        var scoreLabel = MakeTMPLabel(canvasGo, "ScoreLabel", "Score: 0",
            new Vector2(-300f, 180f), 28);

        // Chips label
        var chipsLabel = MakeTMPLabel(canvasGo, "ChipsLabel", "Chips: 5",
            new Vector2(300f, 180f), 28);

        // Round result label (inactive)
        var resultLabel = MakeTMPLabel(canvasGo, "RoundResultLabel", "Round over!",
            new Vector2(0f, 0f), 36);
        resultLabel.gameObject.SetActive(false);

        // Drop button
        var btnGo  = new GameObject("DropButton");
        btnGo.transform.SetParent(canvasGo.transform, false);
        var btnRect = btnGo.AddComponent<RectTransform>();
        btnRect.anchoredPosition = new Vector2(0f, -200f);
        btnRect.sizeDelta        = new Vector2(200f, 60f);
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.6f, 1f);
        var btn = btnGo.AddComponent<Button>();

        var btnTextGo   = new GameObject("Text");
        btnTextGo.transform.SetParent(btnGo.transform, false);
        var btnTextRect = btnTextGo.AddComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;
        var btnTmp = btnTextGo.AddComponent<TextMeshProUGUI>();
        btnTmp.text      = "DROP";
        btnTmp.fontSize  = 24;
        btnTmp.alignment = TextAlignmentOptions.Center;
        btnTmp.color     = Color.white;

        // Wire UIController fields via SerializedObject
        var uiSo = new SerializedObject(uiCtrl);
        uiSo.FindProperty("_scoreLabel").objectReferenceValue       = scoreLabel;
        uiSo.FindProperty("_chipsLabel").objectReferenceValue       = chipsLabel;
        uiSo.FindProperty("_roundResultLabel").objectReferenceValue = resultLabel;
        uiSo.FindProperty("_dropButton").objectReferenceValue       = btn;
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        // ── EventSystem (required for UI input) ──────────────────────────────
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // ── GameBootstrapper ──────────────────────────────────────────────────
        var bootstrapperGo = new GameObject("GameBootstrapper");
        var bootstrapper   = bootstrapperGo.AddComponent<GameBootstrapper>();

        var bsSo = new SerializedObject(bootstrapper);
        bsSo.FindProperty("_boardView").objectReferenceValue  =
            boardGo.GetComponent<BoardView>();
        bsSo.FindProperty("_ui").objectReferenceValue         = uiCtrl;
        bsSo.FindProperty("_ballPrefab").objectReferenceValue =
            ballPrefab != null ? ballPrefab.GetComponent<BallView>() : null;
        bsSo.FindProperty("_boardId").stringValue             = "default";
        bsSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[PachinkoSceneSetup] Scene setup complete. Press Play!");
    }

    private static TextMeshProUGUI MakeTMPLabel(GameObject parent, string name,
        string text, Vector2 anchoredPos, float fontSize)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = new Vector2(400f, 50f);
        var tmp  = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        return tmp;
    }
}
