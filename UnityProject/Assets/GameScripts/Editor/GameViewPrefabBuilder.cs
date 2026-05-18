using System.Collections.Generic;
using System.IO;
using EF.UI;
using GameLogic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameView UGUI Prefab 构建工具，用于把局内战斗 UI 结构和 ReferenceCollector key 固化到资源。
/// </summary>
public static class GameViewPrefabBuilder
{
    private const string PrefabPath = "Assets/AssetRaw/UI/Game/GameView.prefab";
    private const string DefaultFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/MiSans-Normal SDF.asset";
    private const string DefaultFontGuid = "c53490db3bfccab40a7fccbe7fb34482";

    /// <summary>
    /// 重建 GameView UGUI Prefab。
    /// </summary>
    [MenuItem("Tools/RogueCard/UI/Rebuild GameView UGUI Prefab")]
    public static void Rebuild()
    {
        GameObject root = CreateRoot();
        var references = new Dictionary<string, Object>();

        var background = CreatePanel("BgImage", root.transform, Vector2.zero, Vector2.zero, StretchAll);
        background.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.13f, 1f);
        references["BgImage"] = background.GetComponent<Image>();

        var battlePanel = CreatePanel("BattlePanel", root.transform, Vector2.zero, Vector2.zero, StretchAll);
        battlePanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        references["BattlePanel"] = battlePanel;

        var playerStatus = CreatePanel("PlayerStatusPanel", battlePanel.transform, new Vector2(0f, -10f), new Vector2(1160f, 118f), TopStretch);
        playerStatus.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.18f, 0.88f);
        references["PlayerStatusPanel"] = playerStatus.GetComponent<RectTransform>();

        var infoText = CreateText("InfoText", playerStatus.transform, new Vector2(0f, -12f), new Vector2(420f, 36f), TopCenter, "等待中", 28f);
        references["InfoText"] = infoText;

        var hpFill = CreateBar(playerStatus.transform, "PlayerHp", new Vector2(-350f, -68f), new Vector2(260f, 22f), new Color(0.75f, 0.17f, 0.18f, 1f), out TextMeshProUGUI hpText);
        references["PlayerHpFill"] = hpFill;
        references["PlayerHpText"] = hpText;

        var armorText = CreateText("PlayerArmorText", playerStatus.transform, new Vector2(-95f, -68f), new Vector2(120f, 28f), TopCenter, "0", 22f);
        references["PlayerArmorText"] = armorText;

        var energyFill = CreateBar(playerStatus.transform, "PlayerEnergy", new Vector2(180f, -68f), new Vector2(220f, 22f), new Color(0.19f, 0.56f, 0.88f, 1f), out TextMeshProUGUI energyText);
        references["PlayerEnergyFill"] = energyFill;
        references["PlayerEnergyText"] = energyText;

        var buffBar = CreateRect("PlayerBuffBar", playerStatus.transform, new Vector2(470f, -70f), new Vector2(220f, 32f), TopCenter);
        references["PlayerBuffBar"] = buffBar;

        var monsterRect = CreatePanel("MonsterRect", battlePanel.transform, new Vector2(0f, -210f), new Vector2(1080f, 280f), TopCenter);
        monsterRect.GetComponent<Image>().color = new Color(0.16f, 0.17f, 0.18f, 0.55f);
        monsterRect.gameObject.AddComponent<HorizontalLayoutGroup>().spacing = 18f;
        references["MonsterRect"] = monsterRect;

        var dropZone = CreatePanel("DropZone", battlePanel.transform, new Vector2(0f, -510f), new Vector2(520f, 112f), TopCenter);
        dropZone.GetComponent<Image>().color = new Color(0.22f, 0.28f, 0.34f, 0.28f);
        dropZone.gameObject.AddComponent<Button>();
        CreateText("DropZoneText", dropZone.transform, Vector2.zero, Vector2.zero, StretchAll, "出牌区域", 28f);
        references["DropZone"] = dropZone;

        var handContainer = CreatePanel("CardSc", battlePanel.transform, new Vector2(0f, 20f), new Vector2(1120f, 300f), BottomCenter);
        handContainer.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.10f, 0.3f);
        references["CardSc"] = handContainer;

        var previewLayer = CreateRect("PreviewLayer", battlePanel.transform, Vector2.zero, Vector2.zero, StretchAll);
        references["PreviewLayer"] = previewLayer;

        var endButton = CreateButton("EndBtn", battlePanel.transform, new Vector2(-28f, 34f), new Vector2(180f, 56f), BottomRight, "结束回合");
        references["EndBtn"] = endButton;

        var failToast = CreateText("FailToast", battlePanel.transform, new Vector2(0f, 116f), new Vector2(360f, 48f), BottomCenter, string.Empty, 24f);
        failToast.color = new Color(1f, 0.72f, 0.18f, 1f);
        failToast.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
        references["FailToast"] = failToast;

        var rewardPanel = CreatePanel("RewardPanel", root.transform, Vector2.zero, Vector2.zero, StretchAll);
        rewardPanel.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.10f, 0.94f);
        rewardPanel.SetActive(false);
        references["RewardPanel"] = rewardPanel;
        CreateText("RewardTitleText", rewardPanel.transform, new Vector2(0f, -170f), new Vector2(600f, 70f), TopCenter, "关卡完成", 42f);
        var rewardButton = CreateButton("RewardConfirmBtn", rewardPanel.transform, new Vector2(0f, 140f), new Vector2(220f, 64f), MiddleCenter, "确认奖励");
        references["RewardConfirmBtn"] = rewardButton;

        var handTemplate = CreateHandCardTemplate(handContainer.transform);
        references["HandCardTemplate"] = handTemplate;

        var monsterTemplate = CreateMonsterTemplate(monsterRect.transform);
        references["MonsterItemTemplate"] = monsterTemplate;

        var buffTemplate = CreateIconTemplate("BuffIconTemplate", playerStatus.transform);
        references["BuffIconTemplate"] = buffTemplate;

        var intentTemplate = CreateIconTemplate("IntentIconTemplate", playerStatus.transform);
        references["IntentIconTemplate"] = intentTemplate;

        var collector = root.GetComponent<ReferenceCollector>();
        collector.data.Clear();
        foreach (var pair in references)
        {
            collector.data.Add(new ReferenceCollectorData { key = pair.Key, gameObject = pair.Value });
        }
        collector.data.Sort(new ReferenceCollectorDataComparer());

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        RepairSerializedFontReferences();
        Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        Debug.Log($"[GameViewPrefabBuilder] 已重建 {PrefabPath}");
    }

    private static GameObject CreateRoot()
    {
        var root = CreateRect("GameView", null, Vector2.zero, Vector2.zero, StretchAll).gameObject;
        root.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        root.AddComponent<ReferenceCollector>();
        root.AddComponent<GameView>();
        return root;
    }

    private static GameObject CreateHandCardTemplate(Transform parent)
    {
        var card = CreatePanel("HandCardTemplate", parent, Vector2.zero, new Vector2(150f, 230f), MiddleCenter);
        card.GetComponent<Image>().color = new Color(0.87f, 0.83f, 0.72f, 1f);
        card.AddComponent<CanvasGroup>();
        CreateText("CardCostText", card.transform, new Vector2(18f, -18f), new Vector2(34f, 34f), TopLeft, "1", 22f).color = Color.black;
        CreateText("CardNameText", card.transform, new Vector2(0f, -60f), new Vector2(132f, 42f), TopCenter, "卡牌", 22f).color = Color.black;
        card.SetActive(false);
        return card;
    }

    private static GameObject CreateMonsterTemplate(Transform parent)
    {
        var item = CreatePanel("MonsterItemTemplate", parent, Vector2.zero, new Vector2(230f, 230f), MiddleCenter);
        item.GetComponent<Image>().color = new Color(0.24f, 0.20f, 0.18f, 0.92f);
        item.AddComponent<Button>();
        CreateText("NameText", item.transform, new Vector2(0f, -16f), new Vector2(190f, 34f), TopCenter, "怪物", 22f);
        CreateText("HpText", item.transform, new Vector2(0f, -58f), new Vector2(180f, 28f), TopCenter, "10/10", 18f);
        var hpFill = CreatePanel("HpFill", item.transform, new Vector2(0f, -90f), new Vector2(160f, 14f), TopCenter);
        hpFill.GetComponent<Image>().type = Image.Type.Filled;
        hpFill.GetComponent<Image>().color = new Color(0.75f, 0.17f, 0.18f, 1f);
        CreateRect("IntentBar", item.transform, new Vector2(0f, -126f), new Vector2(180f, 32f), TopCenter);
        CreateRect("BuffBar", item.transform, new Vector2(0f, -166f), new Vector2(180f, 32f), TopCenter);
        var highlight = CreatePanel("TargetHighlight", item.transform, Vector2.zero, Vector2.zero, StretchAll);
        highlight.GetComponent<Image>().color = new Color(1f, 0.82f, 0.20f, 0.25f);
        highlight.SetActive(false);
        item.SetActive(false);
        return item;
    }

    private static GameObject CreateIconTemplate(string name, Transform parent)
    {
        var icon = CreatePanel(name, parent, Vector2.zero, new Vector2(32f, 32f), MiddleCenter);
        icon.GetComponent<Image>().color = new Color(0.18f, 0.24f, 0.32f, 1f);
        CreateText("Text", icon.transform, Vector2.zero, Vector2.zero, StretchAll, string.Empty, 16f);
        icon.SetActive(false);
        return icon;
    }

    private static Image CreateBar(Transform parent, string prefix, Vector2 pos, Vector2 size, Color color, out TextMeshProUGUI valueText)
    {
        var bg = CreatePanel(prefix + "Bar", parent, pos, size, TopCenter);
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
        var fill = CreatePanel(prefix + "Fill", bg.transform, Vector2.zero, Vector2.zero, StretchAll);
        var image = fill.GetComponent<Image>();
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.color = color;
        valueText = CreateText(prefix + "Text", bg.transform, Vector2.zero, Vector2.zero, StretchAll, "0/0", 16f);
        return image;
    }

    private static Button CreateButton(string name, Transform parent, Vector2 pos, Vector2 size, AnchorPreset anchor, string text)
    {
        var go = CreatePanel(name, parent, pos, size, anchor);
        go.GetComponent<Image>().color = new Color(0.86f, 0.80f, 0.68f, 1f);
        var button = go.AddComponent<Button>();
        CreateText("Text", go.transform, Vector2.zero, Vector2.zero, StretchAll, text, 24f).color = new Color(0.13f, 0.12f, 0.10f, 1f);
        return button;
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 pos, Vector2 size, AnchorPreset anchor)
    {
        var rect = CreateRect(name, parent, pos, size, anchor);
        rect.gameObject.AddComponent<CanvasRenderer>();
        rect.gameObject.AddComponent<Image>();
        return rect.gameObject;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 pos, Vector2 size, AnchorPreset anchor, string text, float fontSize)
    {
        var rect = CreateRect(name, parent, pos, size, anchor);
        rect.gameObject.AddComponent<CanvasRenderer>();
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        AssignDefaultFont(label);
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private static TMP_FontAsset LoadDefaultFont()
    {
        string guidPath = AssetDatabase.GUIDToAssetPath(DefaultFontGuid);
        TMP_FontAsset font = LoadFontAtPath(guidPath);
        if (font != null)
        {
            return font;
        }

        font = LoadFontAtPath(DefaultFontPath);
        if (font != null)
        {
            return font;
        }

        foreach (string guid in AssetDatabase.FindAssets("MiSans-Normal SDF"))
        {
            font = LoadFontAtPath(AssetDatabase.GUIDToAssetPath(guid));
            if (font != null)
            {
                return font;
            }
        }

        return null;
    }

    private static TMP_FontAsset LoadFontAtPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font != null)
        {
            return font;
        }

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is TMP_FontAsset fontAsset)
            {
                return fontAsset;
            }
        }

        return null;
    }

    private static void AssignDefaultFont(TextMeshProUGUI label)
    {
        TMP_FontAsset font = LoadDefaultFont();
        if (font == null)
        {
            return;
        }

        label.font = font;
        var serializedObject = new SerializedObject(label);
        SerializedProperty fontProperty = serializedObject.FindProperty("m_fontAsset");
        if (fontProperty != null)
        {
            fontProperty.objectReferenceValue = font;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void RepairSerializedFontReferences()
    {
        if (!File.Exists(PrefabPath))
        {
            return;
        }

        const string emptyFontRef = "m_fontAsset: {fileID: 0}";
        const string defaultFontRef = "m_fontAsset: {fileID: 11400000, guid: c53490db3bfccab40a7fccbe7fb34482, type: 2}";
        string text = File.ReadAllText(PrefabPath);
        string updated = text.Replace(emptyFontRef, defaultFontRef);
        if (!ReferenceEquals(text, updated) && text != updated)
        {
            File.WriteAllText(PrefabPath, updated);
        }
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 pos, Vector2 size, AnchorPreset anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        if (parent != null)
        {
            rect.SetParent(parent, false);
        }
        ApplyAnchor(rect, anchor);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return rect;
    }

    private static void ApplyAnchor(RectTransform rect, AnchorPreset anchor)
    {
        rect.anchorMin = anchor.Min;
        rect.anchorMax = anchor.Max;
        rect.pivot = anchor.Pivot;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private readonly struct AnchorPreset
    {
        public readonly Vector2 Min;
        public readonly Vector2 Max;
        public readonly Vector2 Pivot;

        public AnchorPreset(Vector2 min, Vector2 max, Vector2 pivot)
        {
            Min = min;
            Max = max;
            Pivot = pivot;
        }
    }

    private static readonly AnchorPreset StretchAll = new(new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
    private static readonly AnchorPreset TopStretch = new(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
    private static readonly AnchorPreset TopCenter = new(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
    private static readonly AnchorPreset TopLeft = new(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
    private static readonly AnchorPreset MiddleCenter = new(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
    private static readonly AnchorPreset BottomCenter = new(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
    private static readonly AnchorPreset BottomRight = new(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
}
