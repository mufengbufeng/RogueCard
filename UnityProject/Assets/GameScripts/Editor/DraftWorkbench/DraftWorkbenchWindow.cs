#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// 结构框预览区域的纯布局计算结果。
    /// </summary>
    internal struct StructurePreviewLayoutResult
    {
        /// <summary>输入尺寸有效时为 true。</summary>
        public bool IsValid;

        /// <summary>从原始坐标系到预览图像素的缩放比例。</summary>
        public float Scale;

        /// <summary>预览图绘制宽度。</summary>
        public float ImageWidth;

        /// <summary>预览图绘制高度。</summary>
        public float ImageHeight;

        /// <summary>滚动内容区域宽度。</summary>
        public float ContentWidth;

        /// <summary>滚动内容区域高度。</summary>
        public float ContentHeight;
    }

    /// <summary>
    /// 草稿工作台编辑器窗口，提供 AI 驱动的 UI 布局生成、预览、应用及回滚操作。
    /// <para>通过菜单 <c>RogueCard/Draft Workbench</c> 打开。使用 UIToolkit 渲染。</para>
    /// </summary>
    public class DraftWorkbenchWindow : EditorWindow
    {
        // ─────────────────────── 布局常量 ───────────────────────

        /// <summary>
        /// 叠加层适配模式的显示名称数组，与 <see cref="OverlayFitMode"/> 枚举一一对应。
        /// </summary>
        private static readonly string[] FitModeNames =
        {
            "Fill（等比裁剪填满）",
            "FitWidth（等比宽度适配）",
            "FitHeight（等比高度适配）",
            "Stretch（拉伸填满）"
        };

        /// <summary>
        /// 结构框预览着色模式显示名称数组，与 <see cref="StructurePreviewColorMode"/> 枚举一一对应。
        /// </summary>
        private static readonly string[] StructurePreviewColorModeNames =
        {
            "按控件类型",
            "按变更状态"
        };

        /// <summary>
        /// 结构框描边宽度。
        /// </summary>
        private const float StructurePreviewBorderWidth = 2f;

        /// <summary>
        /// 宽度达到该阈值后启用左侧工作流 + 右侧持久预览的双栏布局。
        /// </summary>
        private const float TwoColumnLayoutBreakpoint = 980f;

        /// <summary>
        /// 高 DPI 下用于判定双栏布局的物理像素宽度阈值。
        /// </summary>
        private const float TwoColumnLayoutPixelBreakpoint = 1400f;

        /// <summary>
        /// 右侧持久预览栏最小宽度。
        /// </summary>
        private const float StructurePreviewPaneMinWidth = 340f;

        /// <summary>
        /// 右侧持久预览栏最大宽度。
        /// </summary>
        private const float StructurePreviewPaneMaxWidth = 560f;

        /// <summary>
        /// 右侧持久预览栏占窗口宽度的目标比例。
        /// </summary>
        private const float StructurePreviewPaneWidthRatio = 0.42f;

        /// <summary>
        /// Auto Slice 面板的阶段 1 文案。
        /// </summary>
        private const string AutoSliceRawStageText = "根据 JSON 生成原始切图";

        /// <summary>
        /// Auto Slice 面板的阶段 2 文案。
        /// </summary>
        private const string AutoSliceRefinedStageText = "使用 ComfyUI 生成可用切图";

        /// <summary>
        /// Auto Slice 面板默认说明。
        /// </summary>
        private const string AutoSliceInitialHelpText = "先根据 JSON 生成原始切图，再把选中的 raw PNG 交给 ComfyUI refinement。";

        /// <summary>
        /// 结构框预览最小缩放倍率。
        /// </summary>
        private const float StructurePreviewMinZoom = 0.25f;

        /// <summary>
        /// 结构框预览最大缩放倍率。
        /// </summary>
        private const float StructurePreviewMaxZoom = 2f;

        // ─────────────────────── UIToolkit 元素引用 ───────────────────────

        // 布局容器
        private VisualElement _rootContainer;
        private VisualElement _previewPanel;
        private VisualElement _inlinePreviewContainer;
        private VisualElement _inlineZoomControls;
        private VisualElement _zoomControls;

        // Settings 控件
        private Foldout _settingsFoldout;
        private TextField _endpointField;
        private TextField _apiKeyField;
        private TextField _modelField;
        private Toggle _useJsonModeToggle;
        private Toggle _useResponsesApiToggle;
        private IntegerField _timeoutField;
        private Button _testConnectionBtn;
        private Button _saveConfigBtn;
        private HelpBox _connectionResultHelpBox;

        // Input 控件
        private Foldout _inputFoldout;
        private ObjectField _designImageField;
        private ObjectField _targetPrefabField;
        private IntegerField _canvasWidthField;
        private IntegerField _canvasHeightField;
        private Button _generateBtn;
        private HelpBox _generateErrorHelpBox;
        private Slider _overlayOpacitySlider;
        private PopupField<string> _fitModePopup;
        private Button _showOverlayBtn;
        private Button _hideOverlayBtn;

        // AI Result 控件
        private Foldout _aiResultFoldout;
        private TextField _aiResponseField;
        private Button _parsePreviewBtn;

        // Auto Slice 控件
        private Foldout _autoSliceFoldout;
        private TextField _comfyBaseUrlField;
        private ObjectField _comfyWorkflowField;
        private IntegerField _sliceExpandPixelsField;
        private Slider _sliceExpandPercentSlider;
        private Button _testComfyBtn;
        private Button _generateRegionsBtn;
        private Button _importSpritesBtn;
        private Button _matchSpritesBtn;
        private Button _applySpritesBtn;
        private Label _autoSliceStageLabel;
        private Label _autoSlicePrimaryActionLabel;
        private Label _autoSliceOutputLabel;
        private Label _autoSliceStatusLabel;
        private ListView _regionListView;
        private ListView _assignmentListView;
        private HelpBox _autoSliceHelpBox;

        // Preview 控件
        private Foldout _previewFoldout;
        private Label _nodeSummaryLabel;
        private HelpBox _previewEmptyHelpBox;
        private ListView _nodeListView;
        private Button _applyBtn;
        private Button _revertBtn;
        private Label _inlinePreviewHeader;

        // 预览工具栏（宽布局）
        private PopupField<string> _colorModePopup;
        private Toggle _manualZoomToggle;
        private Slider _zoomSlider;
        private Label _zoomPercentLabel;
        private Button _fitBtn;
        private Label _statsLabel;

        // 预览工具栏（窄布局）
        private PopupField<string> _inlineColorModePopup;
        private Toggle _inlineManualZoomToggle;
        private Slider _inlineZoomSlider;
        private Label _inlineZoomPercentLabel;
        private Button _inlineFitBtn;
        private Label _inlineStatsLabel;

        // 结构框预览
        private ScrollView _structurePreviewScroll;
        private ScrollView _inlinePreviewScroll;
        private VisualElement _previewContent;
        private VisualElement _inlinePreviewContent;
        private VisualElement _previewImage;
        private VisualElement _inlinePreviewImage;

        // Report 控件
        private Foldout _reportFoldout;
        private Label _conversionWarningsLabel;
        private Label _applyReportLabel;
        private Label _bindingReportLabel;
        private HelpBox _reportEmptyHelpBox;

        // ─────────────────────── AI 设置状态 ───────────────────────

        private AiServiceConfig _aiConfig;
        private ComfyUiServiceConfig _comfyConfig;
        private string _endpointFieldValue;
        private string _apiKeyFieldValue;
        private string _modelFieldValue;
        private bool _useJsonMode;
        private bool _useResponsesApiFormat;
        private int _timeoutSecondsField = AiServiceConfig.DefaultTimeoutSeconds;
        private string _testConnectionResult;
        private bool _isTestingConnection;

        // ─────────────────────── 输入状态 ───────────────────────

        private Texture2D _draftImage;
        private GameObject _prefabAsset;
        private GameObject _editableInstance;
        private int _canvasWidth = 1920;
        private int _canvasHeight = 1080;

        // ─────────────────────── AI 生成状态 ───────────────────────

        private bool _isGenerating;
        private bool _isAutoSlicing;
        private AutoSliceStage _autoSliceStage = AutoSliceStage.Idle;
        private string _autoSliceStatus;
        private string _generateError;
        private string _aiResponse;
        private string _lastParsedSourceJson;
        private string _lastParsedSourceJsonHash;
        private string _lastPreviewMaintenanceEntryGuid;

        // ─────────────────────── 预览状态 ───────────────────────

        private List<UguiNodeChange> _previewChanges = new List<UguiNodeChange>();
        private ConversionReport _conversionReport;
        private StructurePreviewColorMode _structurePreviewColorMode = StructurePreviewColorMode.ComponentType;
        private bool _useManualStructurePreviewZoom;
        private float _structurePreviewZoom = 1f;
        private bool _suppressPrefabChangeCallback;

        /// <summary>
        /// 结构框对象池，缓存已创建的 VisualElement 节点以便复用。
        /// </summary>
        private readonly List<VisualElement> _structureBoxPool = new List<VisualElement>();

        /// <summary>
        /// 窄布局的结构框对象池。
        /// </summary>
        private readonly List<VisualElement> _inlineStructureBoxPool = new List<VisualElement>();

        /// <summary>
        /// Auto Slice region overlay 对象池。
        /// </summary>
        private readonly List<VisualElement> _regionOverlayBoxPool = new List<VisualElement>();

        /// <summary>
        /// 窄布局 Auto Slice region overlay 对象池。
        /// </summary>
        private readonly List<VisualElement> _inlineRegionOverlayBoxPool = new List<VisualElement>();

        /// <summary>
        /// 当前选中的 Auto Slice region id。
        /// </summary>
        private string _selectedRegionId;

        // ─────────────────────── 叠加层状态 ───────────────────────

        private DraftWorkbenchMetadata _metadata;
        private DraftOverlaySettings _overlaySettings = new DraftOverlaySettings();
        private int _fitModeIndex = 1; // 默认 FitWidth

        // ─────────────────────── 报告状态 ───────────────────────

        private DraftBindingReport _bindingReport;
        private DraftSegmentationManifest _segmentationManifest;
        private List<DraftGeneratedSprite> _generatedSprites = new List<DraftGeneratedSprite>();
        private List<DraftSpriteAssignment> _spriteAssignments = new List<DraftSpriteAssignment>();

        // ─────────────────────── 菜单入口 ───────────────────────

        /// <summary>
        /// 通过 Unity 菜单打开草稿工作台窗口。
        /// </summary>
        [MenuItem("RogueCard/Draft Workbench")]
        private static void OpenWindow()
        {
            var window = GetWindow<DraftWorkbenchWindow>("Draft Workbench");
            window.minSize = new Vector2(400f, 600f);
        }

        // ─────────────────────── 生命周期 ───────────────────────

        /// <summary>
        /// 窗口启用时加载 AI 配置并恢复上次的 metadata。
        /// </summary>
        private void OnEnable()
        {
            _aiConfig = AiServiceConfig.GetOrCreate();
            _comfyConfig = ComfyUiServiceConfig.GetOrCreate();
            RestoreAiFieldsFromConfig();

            var savedPath = EditorPrefs.GetString("DraftWorkbench_MetadataPath", "");
            if (!string.IsNullOrEmpty(savedPath))
            {
                _metadata = AssetDatabase.LoadAssetAtPath<DraftWorkbenchMetadata>(savedPath);
                if (_metadata != null)
                {
                    _overlaySettings = _metadata.OverlaySettings ?? new DraftOverlaySettings();
                    _fitModeIndex = (int)_overlaySettings.FitMode;

                    if (!string.IsNullOrEmpty(_metadata.SourceImageAssetGuid))
                    {
                        var imagePath = AssetDatabase.GUIDToAssetPath(_metadata.SourceImageAssetGuid);
                        _draftImage = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
                    }
                }
            }
        }

        /// <summary>
        /// 创建 UIToolkit UI：加载 UXML + USS，查询元素，绑定事件。
        /// </summary>
        private void CreateGUI()
        {
            // 加载 UXML
            var scriptDir = GetScriptDirectory();
            var uxmlPath = scriptDir + "/DraftWorkbenchWindow.uxml";
            var uxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (uxmlAsset != null)
            {
                uxmlAsset.CloneTree(rootVisualElement);
            }
            else
            {
                Debug.LogError($"[DraftWorkbench] 找不到 UXML: {uxmlPath}");
                return;
            }

            // 加载 USS
            var ussPath = scriptDir + "/DraftWorkbenchWindow.uss";
            var ussAsset = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            if (ussAsset != null)
            {
                rootVisualElement.styleSheets.Add(ussAsset);
            }

            QueryAndCacheElements();
            BindEvents();
            SyncUiFromState();
        }

        /// <summary>
        /// 窗口禁用时清理临时对象。
        /// </summary>
        private void OnDisable()
        {
            UnloadEditableInstance();
        }

        // ─────────────────────── UXML 路径 ───────────────────────

        /// <summary>
        /// 获取当前脚本所在目录的 Assets 相对路径（始终使用正斜杠）。
        /// </summary>
        private static string GetScriptDirectory()
        {
            // 通过查找本类型所在脚本的 GUID 获取路径
            var scriptGUID = AssetDatabase.FindAssets("t:Script DraftWorkbenchWindow")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.EndsWith("DraftWorkbenchWindow.cs"));
            if (string.IsNullOrEmpty(scriptGUID))
                return "Assets/GameScripts/Editor/DraftWorkbench";
            var dir = System.IO.Path.GetDirectoryName(scriptGUID);
            return dir?.Replace("\\", "/") ?? "Assets/GameScripts/Editor/DraftWorkbench";
        }

        // ─────────────────────── 元素查询与缓存 ───────────────────────

        /// <summary>
        /// 一次性查询并缓存所有 UIToolkit 元素引用。
        /// </summary>
        private void QueryAndCacheElements()
        {
            // 布局容器
            _rootContainer = rootVisualElement.Q<VisualElement>("root-container");
            _previewPanel = rootVisualElement.Q<VisualElement>("preview-panel");
            _inlinePreviewContainer = rootVisualElement.Q<VisualElement>("inline-preview-container");

            // Settings
            _settingsFoldout = rootVisualElement.Q<Foldout>("settings-foldout");
            _endpointField = rootVisualElement.Q<TextField>("endpoint-field");
            _apiKeyField = rootVisualElement.Q<TextField>("api-key-field");
            _modelField = rootVisualElement.Q<TextField>("model-field");
            _useJsonModeToggle = rootVisualElement.Q<Toggle>("use-json-mode-toggle");
            _useResponsesApiToggle = rootVisualElement.Q<Toggle>("use-responses-api-toggle");
            _timeoutField = rootVisualElement.Q<IntegerField>("timeout-field");
            _testConnectionBtn = rootVisualElement.Q<Button>("test-connection-btn");
            _saveConfigBtn = rootVisualElement.Q<Button>("save-config-btn");

            // 动态创建 connection-result HelpBox
            var connResultContainer = rootVisualElement.Q<VisualElement>("connection-result-container");
            _connectionResultHelpBox = new HelpBox();
            _connectionResultHelpBox.style.display = DisplayStyle.None;
            connResultContainer?.Add(_connectionResultHelpBox);

            // Input
            _inputFoldout = rootVisualElement.Q<Foldout>("input-foldout");

            // 动态创建 design-image ObjectField
            var designImageContainer = rootVisualElement.Q<VisualElement>("design-image-container");
            _designImageField = new ObjectField("Design Image");
            _designImageField.objectType = typeof(Texture2D);
            _designImageField.allowSceneObjects = false;
            designImageContainer?.Add(_designImageField);

            // 动态创建 target-prefab ObjectField
            var targetPrefabContainer = rootVisualElement.Q<VisualElement>("target-prefab-container");
            _targetPrefabField = new ObjectField("Target Prefab");
            _targetPrefabField.objectType = typeof(GameObject);
            _targetPrefabField.allowSceneObjects = false;
            targetPrefabContainer?.Add(_targetPrefabField);
            _canvasWidthField = rootVisualElement.Q<IntegerField>("canvas-width-field");
            _canvasHeightField = rootVisualElement.Q<IntegerField>("canvas-height-field");
            _generateBtn = rootVisualElement.Q<Button>("generate-btn");

            // 动态创建 generate-error HelpBox
            var genErrContainer = rootVisualElement.Q<VisualElement>("generate-error-container");
            _generateErrorHelpBox = new HelpBox();
            _generateErrorHelpBox.style.display = DisplayStyle.None;
            genErrContainer?.Add(_generateErrorHelpBox);

            _overlayOpacitySlider = rootVisualElement.Q<Slider>("overlay-opacity-slider");
            _showOverlayBtn = rootVisualElement.Q<Button>("show-overlay-btn");
            _hideOverlayBtn = rootVisualElement.Q<Button>("hide-overlay-btn");

            // 动态创建 FitMode PopupField
            var fitModeContainer = rootVisualElement.Q<VisualElement>("fit-mode-popup-container");
            _fitModePopup = new PopupField<string>(new List<string>(FitModeNames), _fitModeIndex);
            _fitModePopup.label = "适配模式";
            fitModeContainer?.Add(_fitModePopup);

            // AI Result
            _aiResultFoldout = rootVisualElement.Q<Foldout>("ai-result-foldout");
            _aiResponseField = rootVisualElement.Q<TextField>("ai-response-field");
            _parsePreviewBtn = rootVisualElement.Q<Button>("parse-preview-btn");

            // Auto Slice
            _autoSliceFoldout = rootVisualElement.Q<Foldout>("auto-slice-foldout");
            _comfyBaseUrlField = rootVisualElement.Q<TextField>("comfy-base-url-field");
            var comfyWorkflowContainer = rootVisualElement.Q<VisualElement>("comfy-workflow-container");
            _comfyWorkflowField = new ObjectField("Workflow JSON")
            {
                objectType = typeof(TextAsset),
                allowSceneObjects = false
            };
            comfyWorkflowContainer?.Add(_comfyWorkflowField);
            _sliceExpandPixelsField = rootVisualElement.Q<IntegerField>("slice-expand-pixels-field");
            _sliceExpandPercentSlider = rootVisualElement.Q<Slider>("slice-expand-percent-slider");
            _testComfyBtn = rootVisualElement.Q<Button>("test-comfy-btn");
            _generateRegionsBtn = rootVisualElement.Q<Button>("generate-regions-btn");
            _importSpritesBtn = rootVisualElement.Q<Button>("import-sprites-btn");
            _matchSpritesBtn = rootVisualElement.Q<Button>("match-sprites-btn");
            _applySpritesBtn = rootVisualElement.Q<Button>("apply-sprites-btn");
            _autoSliceStageLabel = rootVisualElement.Q<Label>("auto-slice-stage-label");
            _autoSlicePrimaryActionLabel = rootVisualElement.Q<Label>("auto-slice-primary-action-label");
            _autoSliceOutputLabel = rootVisualElement.Q<Label>("auto-slice-output-label");
            _autoSliceStatusLabel = rootVisualElement.Q<Label>("auto-slice-status-label");
            var autoSliceHelpContainer = rootVisualElement.Q<VisualElement>("auto-slice-help-container");
            _autoSliceHelpBox = new HelpBox(AutoSliceInitialHelpText, HelpBoxMessageType.Info);
            autoSliceHelpContainer?.Add(_autoSliceHelpBox);
            var regionListContainer = rootVisualElement.Q<VisualElement>("region-list-container");
            _regionListView = new ListView();
            _regionListView.fixedItemHeight = 72f;
            regionListContainer?.Add(_regionListView);
            var assignmentListContainer = rootVisualElement.Q<VisualElement>("assignment-list-container");
            _assignmentListView = new ListView();
            _assignmentListView.fixedItemHeight = 120f;
            assignmentListContainer?.Add(_assignmentListView);

            // Preview
            _previewFoldout = rootVisualElement.Q<Foldout>("preview-foldout");
            _nodeSummaryLabel = rootVisualElement.Q<Label>("node-summary-label");

            // 动态创建 preview-empty HelpBox
            var previewEmptyContainer = rootVisualElement.Q<VisualElement>("preview-empty-container");
            _previewEmptyHelpBox = new HelpBox("暂无预览数据。请先 [Generate] 或 [Parse & Preview]。", HelpBoxMessageType.Info);
            previewEmptyContainer?.Add(_previewEmptyHelpBox);

            // 动态创建 ListView
            var nodeListContainer = rootVisualElement.Q<VisualElement>("node-list-container");
            _nodeListView = new ListView();
            nodeListContainer?.Add(_nodeListView);

            _applyBtn = rootVisualElement.Q<Button>("apply-btn");
            _revertBtn = rootVisualElement.Q<Button>("revert-btn");
            _inlinePreviewHeader = rootVisualElement.Q<Label>("inline-preview-header");

            // 宽布局预览工具栏
            var colorModeContainer = rootVisualElement.Q<VisualElement>("color-mode-popup-container");
            _colorModePopup = new PopupField<string>(new List<string>(StructurePreviewColorModeNames), 0);
            _colorModePopup.label = "结构框着色";
            colorModeContainer?.Add(_colorModePopup);

            _manualZoomToggle = rootVisualElement.Q<Toggle>("manual-zoom-toggle");
            _zoomSlider = rootVisualElement.Q<Slider>("zoom-slider");
            _zoomPercentLabel = rootVisualElement.Q<Label>("zoom-percent-label");
            _fitBtn = rootVisualElement.Q<Button>("fit-btn");
            _statsLabel = rootVisualElement.Q<Label>("stats-label");
            _zoomControls = rootVisualElement.Q<VisualElement>("zoom-controls");

            // 窄布局预览工具栏
            var inlineColorModeContainer = rootVisualElement.Q<VisualElement>("inline-color-mode-popup-container");
            _inlineColorModePopup = new PopupField<string>(new List<string>(StructurePreviewColorModeNames), 0);
            _inlineColorModePopup.label = "结构框着色";
            inlineColorModeContainer?.Add(_inlineColorModePopup);

            _inlineManualZoomToggle = rootVisualElement.Q<Toggle>("inline-manual-zoom-toggle");
            _inlineZoomSlider = rootVisualElement.Q<Slider>("inline-zoom-slider");
            _inlineZoomPercentLabel = rootVisualElement.Q<Label>("inline-zoom-percent-label");
            _inlineFitBtn = rootVisualElement.Q<Button>("inline-fit-btn");
            _inlineStatsLabel = rootVisualElement.Q<Label>("inline-stats-label");
            _inlineZoomControls = rootVisualElement.Q<VisualElement>("inline-zoom-controls");

            // 结构框预览 ScrollView
            _structurePreviewScroll = rootVisualElement.Q<ScrollView>("structure-preview-scroll");
            _inlinePreviewScroll = rootVisualElement.Q<ScrollView>("inline-preview-scroll");

            // 预览内容容器
            _previewContent = new VisualElement { name = "preview-content" };
            _previewContent.style.position = Position.Absolute;
            _structurePreviewScroll?.Add(_previewContent);

            _inlinePreviewContent = new VisualElement { name = "inline-preview-content" };
            _inlinePreviewContent.style.position = Position.Absolute;
            _inlinePreviewScroll?.Add(_inlinePreviewContent);

            // 预览设计图元素
            _previewImage = new VisualElement { name = "preview-image" };
            _previewImage.style.position = Position.Absolute;
            _previewContent.Add(_previewImage);

            _inlinePreviewImage = new VisualElement { name = "inline-preview-image" };
            _inlinePreviewImage.style.position = Position.Absolute;
            _inlinePreviewContent.Add(_inlinePreviewImage);

            // Report
            _reportFoldout = rootVisualElement.Q<Foldout>("report-foldout");
            _conversionWarningsLabel = rootVisualElement.Q<Label>("conversion-warnings-label");
            _applyReportLabel = rootVisualElement.Q<Label>("apply-report-label");
            _bindingReportLabel = rootVisualElement.Q<Label>("binding-report-label");

            // 动态创建 report-empty HelpBox
            var reportEmptyContainer = rootVisualElement.Q<VisualElement>("report-empty-container");
            _reportEmptyHelpBox = new HelpBox("暂无报告数据。", HelpBoxMessageType.Info);
            reportEmptyContainer?.Add(_reportEmptyHelpBox);
        }

        // ─────────────────────── 事件绑定 ───────────────────────

        /// <summary>
        /// 为所有交互控件注册事件回调。
        /// </summary>
        private void BindEvents()
        {
            // Settings
            _saveConfigBtn.clicked += SaveConfig;
            _testConnectionBtn.clicked += TestConnectionAsync;

            // Input
            _generateBtn.clicked += OnGenerateClicked;
            _showOverlayBtn.clicked += ShowOverlay;
            _hideOverlayBtn.clicked += HideOverlay;

            // AI Result
            _parsePreviewBtn.clicked += ParseAndPreviewFromField;

            // Auto Slice
            if (_testComfyBtn != null) _testComfyBtn.clicked += TestComfyConnectionAsync;
            if (_generateRegionsBtn != null) _generateRegionsBtn.clicked += GenerateRegionsAsync;
            if (_importSpritesBtn != null) _importSpritesBtn.clicked += ImportGeneratedSprites;
            if (_matchSpritesBtn != null) _matchSpritesBtn.clicked += MatchGeneratedSprites;
            if (_applySpritesBtn != null) _applySpritesBtn.clicked += ApplySpriteAssignmentsToPrefab;

            // Preview actions
            _applyBtn.clicked += ApplyToPrefab;
            _revertBtn.clicked += RevertLast;

            // Prefab 切换检测
            _targetPrefabField.RegisterValueChangedCallback(OnPrefabFieldChanged);

            // 叠加层
            _overlayOpacitySlider.RegisterValueChangedCallback(evt =>
            {
                _overlaySettings.Opacity = evt.newValue;
            });
            if (_fitModePopup != null)
            {
                _fitModePopup.RegisterValueChangedCallback(evt =>
                {
                    _fitModeIndex = _fitModePopup.index;
                    _overlaySettings.FitMode = (OverlayFitMode)_fitModeIndex;
                });
            }

            // 预览工具栏（宽布局）
            BindPreviewToolbarEvents(
                _colorModePopup, _manualZoomToggle, _zoomSlider, _zoomPercentLabel, _fitBtn,
                _zoomControls, isWide: true);

            // 预览工具栏（窄布局）
            BindPreviewToolbarEvents(
                _inlineColorModePopup, _inlineManualZoomToggle, _inlineZoomSlider,
                _inlineZoomPercentLabel, _inlineFitBtn,
                _inlineZoomControls, isWide: false);

            // 响应式布局
            _rootContainer.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            // 初始布局刷新
            UpdateLayoutForWidth(_rootContainer.resolvedStyle.width);
        }

        /// <summary>
        /// 绑定预览工具栏事件，宽/窄布局共用逻辑。
        /// </summary>
        private void BindPreviewToolbarEvents(
            PopupField<string> colorPopup, Toggle manualZoomToggle,
            Slider zoomSlider, Label zoomPercentLabel, Button fitBtn,
            VisualElement zoomControls, bool isWide)
        {
            if (colorPopup != null)
            {
                colorPopup.RegisterValueChangedCallback(evt =>
                {
                    _structurePreviewColorMode = (StructurePreviewColorMode)colorPopup.index;
                    RefreshStructurePreview();
                });
            }

            if (manualZoomToggle != null)
            {
                manualZoomToggle.RegisterValueChangedCallback(evt =>
                {
                    _useManualStructurePreviewZoom = evt.newValue;
                    if (!_useManualStructurePreviewZoom)
                    {
                        _structurePreviewZoom = 1f;
                    }
                    SyncToolbarZoomState();
                    RefreshStructurePreview();
                });
            }

            if (zoomSlider != null)
            {
                zoomSlider.RegisterValueChangedCallback(evt =>
                {
                    _structurePreviewZoom = evt.newValue;
                    SyncToolbarZoomState();
                    RefreshStructurePreview();
                });
            }

            if (fitBtn != null)
            {
                fitBtn.clicked += () =>
                {
                    _structurePreviewZoom = 1f;
                    SyncToolbarZoomState();
                    RefreshStructurePreview();
                };
            }
        }

        /// <summary>
        /// 同步宽/窄两套工具栏的状态（zoom slider、percent label、controls 可见性）。
        /// </summary>
        private void SyncToolbarZoomState()
        {
            SyncToolbarState(_zoomSlider, _zoomPercentLabel, _zoomControls,
                _manualZoomToggle?.value ?? false);
            SyncToolbarState(_inlineZoomSlider, _inlineZoomPercentLabel, _inlineZoomControls,
                _inlineManualZoomToggle?.value ?? false);
        }

        private void SyncToolbarState(Slider slider, Label percentLabel, VisualElement controls, bool manualEnabled)
        {
            if (slider != null) slider.value = _structurePreviewZoom;
            if (percentLabel != null) percentLabel.text = $"{Mathf.RoundToInt(_structurePreviewZoom * 100f)}%";
            if (controls != null) controls.style.display = manualEnabled ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ─────────────────────── 响应式布局 ───────────────────────

        /// <summary>
        /// 根据窗口宽度切换双栏/单栏布局。
        /// </summary>
        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            float width = _rootContainer.resolvedStyle.width;
            if (float.IsNaN(width) || width <= 0f) return;
            UpdateLayoutForWidth(width);
        }

        /// <summary>
        /// 根据窗口逻辑/物理宽度调整布局模式。
        /// </summary>
        private void UpdateLayoutForWidth(float logicalWidth)
        {
            float physicalWidth = logicalWidth * Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
            bool useSplit = logicalWidth >= TwoColumnLayoutBreakpoint
                            || physicalWidth >= TwoColumnLayoutPixelBreakpoint;

            // 宽布局：显示右侧面板，隐藏内嵌预览
            _previewPanel.style.display = useSplit ? DisplayStyle.Flex : DisplayStyle.None;
            _inlinePreviewContainer.style.display = useSplit ? DisplayStyle.None : DisplayStyle.Flex;

            // 调整根容器 flex 方向
            _rootContainer.style.flexDirection = useSplit ? FlexDirection.Row : FlexDirection.Column;

            // 更新面板标题
            string foldoutTitle = useSplit ? "结构摘要与应用" : "预览审查";
            if (_previewFoldout != null) _previewFoldout.text = foldoutTitle;

            // 设置右侧面板宽度
            if (useSplit)
            {
                float previewWidth = Mathf.Clamp(
                    logicalWidth * StructurePreviewPaneWidthRatio,
                    StructurePreviewPaneMinWidth,
                    StructurePreviewPaneMaxWidth);
                _previewPanel.style.width = previewWidth;
            }

            RefreshStructurePreview();
        }

        // ─────────────────────── UI 状态同步 ───────────────────────

        /// <summary>
        /// 将 C# 状态同步到 UI 控件初始值。
        /// </summary>
        private void SyncUiFromState()
        {
            // Settings
            _endpointField.value = _endpointFieldValue ?? "";
            _apiKeyField.value = _apiKeyFieldValue ?? "";
            _modelField.value = _modelFieldValue ?? "";
            _useJsonModeToggle.value = _useJsonMode;
            _useResponsesApiToggle.value = _useResponsesApiFormat;
            _timeoutField.value = _timeoutSecondsField;

            // Input
            if (_designImageField != null) _designImageField.value = _draftImage;
            if (_targetPrefabField != null) _targetPrefabField.value = _prefabAsset;
            _canvasWidthField.value = _canvasWidth;
            _canvasHeightField.value = _canvasHeight;

            // Overlay
            _overlayOpacitySlider.value = _overlaySettings.Opacity;
            if (_fitModePopup != null)
            {
                _fitModePopup.index = _fitModeIndex;
                _fitModePopup.value = FitModeNames[_fitModeIndex];
            }

            // Auto Slice
            if (_comfyConfig != null)
            {
                _comfyConfig.Normalize();
                _comfyBaseUrlField.value = _comfyConfig.BaseUrl ?? ComfyUiServiceConfig.DefaultBaseUrl;
                _comfyWorkflowField.value = ResolveComfyWorkflowAssetFromConfig();
                _sliceExpandPixelsField.value = _comfyConfig.RegionExpandPixels;
                _sliceExpandPercentSlider.value = _comfyConfig.RegionExpandPercent;
            }
            RefreshAutoSlicePanel();

            // Preview toolbar
            if (_colorModePopup != null)
            {
                _colorModePopup.index = (int)_structurePreviewColorMode;
                _colorModePopup.value = StructurePreviewColorModeNames[(int)_structurePreviewColorMode];
            }
            if (_inlineColorModePopup != null)
            {
                _inlineColorModePopup.index = (int)_structurePreviewColorMode;
                _inlineColorModePopup.value = StructurePreviewColorModeNames[(int)_structurePreviewColorMode];
            }
            if (_manualZoomToggle != null) _manualZoomToggle.value = _useManualStructurePreviewZoom;
            if (_inlineManualZoomToggle != null) _inlineManualZoomToggle.value = _useManualStructurePreviewZoom;
            SyncToolbarZoomState();

            // Preview buttons
            UpdateActionButtonStates();

            // AI Result
            _aiResponseField.value = _aiResponse ?? "";

            // Refresh dynamic content
            RefreshNodeList();
            RefreshAutoSlicePanel();
            RefreshReport();
            RefreshStructurePreview();
        }

        // ─────────────────────── 1. SETTINGS ───────────────────────

        /// <summary>
        /// 将当前 AI 配置字段写入 ScriptableObject 并保存。
        /// </summary>
        private void SaveConfig()
        {
            if (_aiConfig == null) _aiConfig = AiServiceConfig.GetOrCreate();

            _endpointFieldValue = _endpointField?.value ?? "";
            _apiKeyFieldValue = _apiKeyField?.value ?? "";
            _modelFieldValue = _modelField?.value ?? "";
            _useJsonMode = _useJsonModeToggle?.value ?? false;
            _useResponsesApiFormat = _useResponsesApiToggle?.value ?? false;
            _timeoutSecondsField = AiServiceConfig.NormalizeTimeoutSeconds(_timeoutField?.value ?? 30);

            _aiConfig.Endpoint = _endpointFieldValue;
            _aiConfig.ApiKey = _apiKeyFieldValue;
            _aiConfig.Model = _modelFieldValue;
            _aiConfig.UseJsonMode = _useJsonMode;
            _aiConfig.UseResponsesApiFormat = _useResponsesApiFormat;
            _aiConfig.TimeoutSeconds = _timeoutSecondsField;

            EditorUtility.SetDirty(_aiConfig);
            AssetDatabase.SaveAssets();
            Debug.Log("[DraftWorkbench] AI 配置已保存。");
        }

        /// <summary>
        /// 从 ScriptableObject 恢复 UI 字段值。
        /// </summary>
        private void RestoreAiFieldsFromConfig()
        {
            if (_aiConfig == null) return;
            _endpointFieldValue = _aiConfig.Endpoint ?? "";
            _apiKeyFieldValue = _aiConfig.ApiKey ?? "";
            _modelFieldValue = _aiConfig.Model ?? "";
            _useJsonMode = _aiConfig.UseJsonMode;
            _useResponsesApiFormat = _aiConfig.UseResponsesApiFormat;
            _timeoutSecondsField = AiServiceConfig.NormalizeTimeoutSeconds(_aiConfig.TimeoutSeconds);
        }

        /// <summary>
        /// 测试与 AI 服务的连接（异步包装）。
        /// </summary>
        private async void TestConnectionAsync()
        {
            if (_aiConfig == null) return;

            // 同步字段到 config
            SyncFieldsToConfig();

            _isTestingConnection = true;
            _testConnectionBtn.SetEnabled(false);
            _testConnectionBtn.text = "测试中...";
            _connectionResultHelpBox.text = "正在连接...";
            _connectionResultHelpBox.messageType = HelpBoxMessageType.Info;
            _connectionResultHelpBox.style.display = DisplayStyle.Flex;

            try
            {
                var tcs = new TaskCompletionSource<bool>();
                AiVisionClient.TestConnection(_aiConfig, success => tcs.TrySetResult(success));
                bool success = await tcs.Task;

                _testConnectionResult = success
                    ? "[成功] 连接正常。"
                    : "[失败] 无法连接到 AI 服务，请检查 Endpoint 和 API Key。";
                _connectionResultHelpBox.text = _testConnectionResult;
                _connectionResultHelpBox.messageType = success ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
            }
            catch (Exception ex)
            {
                _connectionResultHelpBox.text = $"[错误] {ex.Message}";
                _connectionResultHelpBox.messageType = HelpBoxMessageType.Error;
            }
            finally
            {
                _isTestingConnection = false;
                _testConnectionBtn.SetEnabled(true);
                _testConnectionBtn.text = "Test Connection";
            }
        }

        /// <summary>
        /// 将 UI 控件值同步到 config 对象。
        /// </summary>
        private void SyncFieldsToConfig()
        {
            if (_aiConfig == null) return;
            _aiConfig.Endpoint = _endpointField?.value ?? "";
            _aiConfig.ApiKey = _apiKeyField?.value ?? "";
            _aiConfig.Model = _modelField?.value ?? "";
            _aiConfig.UseJsonMode = _useJsonModeToggle?.value ?? false;
            _aiConfig.UseResponsesApiFormat = _useResponsesApiToggle?.value ?? false;
            _aiConfig.TimeoutSeconds = AiServiceConfig.NormalizeTimeoutSeconds(_timeoutField?.value ?? 30);
        }

        /// <summary>
        /// 从配置中的对象引用、路径或默认路径解析 workflow TextAsset。
        /// </summary>
        /// <returns>可用的 workflow TextAsset；不存在时返回 null。</returns>
        private TextAsset ResolveComfyWorkflowAssetFromConfig()
        {
            if (_comfyConfig == null)
                return null;

            if (_comfyConfig.WorkflowJsonAsset != null)
                return _comfyConfig.WorkflowJsonAsset;

            string workflowPath = string.IsNullOrWhiteSpace(_comfyConfig.WorkflowJsonAssetPath)
                ? ComfyUiServiceConfig.DefaultWorkflowJsonAssetPath
                : _comfyConfig.WorkflowJsonAssetPath;

            var workflowAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(workflowPath);
            if (workflowAsset != null)
            {
                _comfyConfig.WorkflowJsonAsset = workflowAsset;
                _comfyConfig.WorkflowJsonAssetPath = AssetDatabase.GetAssetPath(workflowAsset);
                EditorUtility.SetDirty(_comfyConfig);
            }

            return workflowAsset;
        }

        /// <summary>
        /// 将自动切图 UI 控件值同步到 ComfyUI 配置。
        /// </summary>
        private void SyncComfyFieldsToConfig()
        {
            if (_comfyConfig == null) _comfyConfig = ComfyUiServiceConfig.GetOrCreate();
            _comfyConfig.BaseUrl = _comfyBaseUrlField?.value ?? ComfyUiServiceConfig.DefaultBaseUrl;

            var selectedWorkflow = _comfyWorkflowField?.value as TextAsset;
            if (selectedWorkflow != null)
            {
                _comfyConfig.WorkflowJsonAsset = selectedWorkflow;
                _comfyConfig.WorkflowJsonAssetPath = AssetDatabase.GetAssetPath(selectedWorkflow);
            }
            else if (_comfyConfig.WorkflowJsonAsset == null && string.IsNullOrWhiteSpace(_comfyConfig.WorkflowJsonAssetPath))
            {
                _comfyConfig.WorkflowJsonAssetPath = ComfyUiServiceConfig.DefaultWorkflowJsonAssetPath;
            }

            _comfyConfig.RegionExpandPixels = _sliceExpandPixelsField?.value ?? _comfyConfig.RegionExpandPixels;
            _comfyConfig.RegionExpandPercent = _sliceExpandPercentSlider?.value ?? _comfyConfig.RegionExpandPercent;
            _comfyConfig.Normalize();
            if (_comfyWorkflowField != null && _comfyWorkflowField.value == null && _comfyConfig.WorkflowJsonAsset != null)
            {
                _comfyWorkflowField.SetValueWithoutNotify(_comfyConfig.WorkflowJsonAsset);
            }
            EditorUtility.SetDirty(_comfyConfig);
        }

        // ─────────────────────── 2. INPUT ───────────────────────

        /// <summary>
        /// Prefab ObjectField 变更时自动加载/卸载可编辑实例。
        /// </summary>
        private void OnPrefabFieldChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            if (_suppressPrefabChangeCallback) return;

            var newPrefab = evt.newValue as GameObject;
            if (newPrefab == _prefabAsset) return;

            UnloadEditableInstance();
            _prefabAsset = newPrefab;

            if (_prefabAsset != null)
            {
                LoadPrefab();
            }
            else
            {
                _metadata = null;
                _previewChanges.Clear();
                _bindingReport = null;
                _conversionReport = null;
                _aiResponse = null;
                SyncUiFromState();
            }
        }

        /// <summary>
        /// 异步调用 AI 视觉模型生成 UI 结构 JSON。
        /// </summary>
        private async void OnGenerateClicked()
        {
            _draftImage = _designImageField?.value as Texture2D;
            _canvasWidth = _canvasWidthField?.value ?? 1920;
            _canvasHeight = _canvasHeightField?.value ?? 1080;

            if (_draftImage == null)
            {
                _generateError = "请先选择 Design Image。";
                ShowGenerateError();
                return;
            }

            if (_aiConfig == null)
            {
                _generateError = "AI 配置未加载。";
                ShowGenerateError();
                return;
            }

            SyncFieldsToConfig();

            // 使用图片实际分辨率作为 AI 分析的画布尺寸，确保 AI 标注位置与图片像素对齐。
            // 用户设置的 Canvas Width/Height 仅用于最终 UGUI Canvas 参考分辨率，
            // AI 始终在图片原始分辨率坐标系中进行标注。
            int imageWidth = _draftImage.width;
            int imageHeight = _draftImage.height;
            Debug.Log($"[DraftWorkbench] 图片分辨率={imageWidth}x{imageHeight}，用户画布={_canvasWidth}x{_canvasHeight}");

            _isGenerating = true;
            _generateError = null;
            _aiResponse = null;
            _previewChanges.Clear();
            _conversionReport = null;
            _bindingReport = null;

            _generateBtn.SetEnabled(false);
            _generateBtn.text = "生成中...";
            _generateErrorHelpBox.style.display = DisplayStyle.None;

            Debug.Log($"[DraftWorkbench] 开始生成：Endpoint={_aiConfig.Endpoint}, Model={_aiConfig.Model}, ImageSize={imageWidth}x{imageHeight}");

            try
            {
                var tcs = new TaskCompletionSource<string>();
                var requestContext = BuildUiStructureRequestContext();
                AiVisionClient.GenerateUiStructure(
                    _draftImage, _aiConfig, imageWidth, imageHeight, requestContext,
                    onSuccess: response => tcs.TrySetResult(response),
                    onError: ex => tcs.TrySetException(ex));

                string response = await tcs.Task;
                Debug.Log($"[DraftWorkbench] AI 响应成功，长度={response?.Length ?? 0}");
                _aiResponse = response;

                // 将画布尺寸同步为图片实际分辨率，确保叠加层和结构预览与 AI 标注坐标系一致
                _canvasWidth = imageWidth;
                _canvasHeight = imageHeight;

                try
                {
                    ParseAndPreview();
                }
                catch (Exception e)
                {
                    _generateError = $"解析失败: {e.Message}";
                    Debug.LogError($"[DraftWorkbench] 解析失败: {e}");
                    ShowGenerateError();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DraftWorkbench] AI 生成失败: {ex.Message}");
                _generateError = $"AI 生成失败: {ex.Message}";
                ShowGenerateError();
            }
            finally
            {
                _isGenerating = false;
                _generateBtn.SetEnabled(true);
                _generateBtn.text = "[Generate]";
                SyncUiFromState();
            }
        }

        private void ShowGenerateError()
        {
            _generateErrorHelpBox.text = _generateError;
            _generateErrorHelpBox.messageType = HelpBoxMessageType.Error;
            _generateErrorHelpBox.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// 显示草稿叠加层。
        /// </summary>
        private void ShowOverlay()
        {
            if (_editableInstance == null)
            {
                Debug.LogWarning("[DraftWorkbench] 请先选择 Target Prefab。");
                return;
            }

            if (_draftImage == null)
            {
                Debug.LogWarning("[DraftWorkbench] 请先选择 Design Image。");
                return;
            }

            _overlaySettings.CanvasReferenceResolution = new Vector2(_canvasWidth, _canvasHeight);
            DraftOverlayService.CreateOverlay(_editableInstance, _draftImage, _overlaySettings);
            Debug.Log("[DraftWorkbench] 叠加层已显示。");
        }

        /// <summary>
        /// 隐藏草稿叠加层。
        /// </summary>
        private void HideOverlay()
        {
            if (_editableInstance == null) return;
            DraftOverlayService.RemoveOverlay(_editableInstance);
            Debug.Log("[DraftWorkbench] 叠加层已隐藏。");
        }

        // ─────────────────────── 3. AUTO SLICE ───────────────────────

        /// <summary>
        /// 测试本机 ComfyUI 服务连接。
        /// </summary>
        private async void TestComfyConnectionAsync()
        {
            if (_comfyConfig == null) _comfyConfig = ComfyUiServiceConfig.GetOrCreate();
            SyncComfyFieldsToConfig();
            _isAutoSlicing = true;
            _autoSliceStatus = "正在测试 ComfyUI 连接...";
            RefreshAutoSlicePanel();

            try
            {
                string response = await ComfyUiClient.TestConnectionAsync(_comfyConfig);
                _autoSliceStatus = "ComfyUI 连接正常。";
                if (_autoSliceHelpBox != null)
                {
                    string summary = response.Length > 160 ? response.Substring(0, 160) + "..." : response;
                    _autoSliceHelpBox.text = "ComfyUI 连接正常。原始切图阶段仍然只依赖 JSON。\n" + summary;
                    _autoSliceHelpBox.messageType = HelpBoxMessageType.Info;
                }
            }
            catch (Exception ex)
            {
                _autoSliceStatus = "ComfyUI 连接失败，原始切图不受影响。";
                if (_autoSliceHelpBox != null)
                {
                    _autoSliceHelpBox.text = "ComfyUI 连接失败：" + ex.Message + "\n原始切图阶段不受影响。";
                    _autoSliceHelpBox.messageType = HelpBoxMessageType.Error;
                }
            }
            finally
            {
                _isAutoSlicing = false;
                RefreshAutoSlicePanel();
            }
        }

        /// <summary>
        /// 根据 JSON / 预览节点生成原始切图。
        /// </summary>
        private void GenerateRegionsAsync()
        {
            _draftImage = _designImageField?.value as Texture2D;
            if (_draftImage == null)
            {
                SetAutoSliceError("请先选择 Design Image。");
                return;
            }

            string sourceAssetPath = AssetDatabase.GetAssetPath(_draftImage);
            if (string.IsNullOrEmpty(sourceAssetPath))
            {
                SetAutoSliceError("Design Image 必须是项目中的 Texture2D 资源。");
                return;
            }

            _isAutoSlicing = true;
            _autoSliceStage = AutoSliceStage.Detecting;
            _autoSliceStatus = "阶段 1：正在根据 JSON 生成原始切图...";
            RefreshAutoSlicePanel();

            try
            {
                string sourceHash = DraftImageSegmentationService.ComputeSourceHash(_draftImage);
                DraftJsonRegionExtractionResult extractionResult;
                if (_previewChanges != null && _previewChanges.Count > 0)
                {
                    extractionResult = DraftJsonRegionExtractionService.ExtractFromPreviewChanges(
                        _previewChanges,
                        sourceHash,
                        sourceAssetPath,
                        _draftImage.width,
                        _draftImage.height);
                }
                else
                {
                    string jsonText = _lastParsedSourceJson;
                    if (string.IsNullOrWhiteSpace(jsonText) && _aiResponseField != null)
                    {
                        jsonText = _aiResponseField.value;
                    }

                    if (string.IsNullOrWhiteSpace(jsonText))
                    {
                        SetAutoSliceError("请先 Parse & Preview 或提供可编辑的 ui_structure.json。");
                        return;
                    }

                    extractionResult = DraftJsonRegionExtractionService.ExtractFromJson(
                        jsonText,
                        sourceHash,
                        sourceAssetPath,
                        _draftImage.width,
                        _draftImage.height);
                }

                if (extractionResult == null || extractionResult.HasError)
                {
                    SetAutoSliceError(extractionResult != null ? extractionResult.ErrorMessage : "原始切图提取失败。");
                    return;
                }

                _segmentationManifest = extractionResult.Manifest;
                DraftJsonRawCropService.GenerateRawCrops(_draftImage, _segmentationManifest, _comfyConfig);
                _generatedSprites.Clear();
                _spriteAssignments.Clear();
                _autoSliceStage = AutoSliceStage.ReviewingRegions;
                _autoSliceStatus = $"阶段 1 完成：原始切图 {_segmentationManifest.regions.Count} 个。";
                if (_autoSliceHelpBox != null)
                {
                    _autoSliceHelpBox.text = "请检查原始切图列表，确认 include / ignore 与 marker 后，再点击使用 ComfyUI 生成可用切图。";
                    _autoSliceHelpBox.messageType = HelpBoxMessageType.Info;
                }
            }
            catch (Exception ex)
            {
                SetAutoSliceError($"生成原始切图失败：{ex.Message}");
            }
            finally
            {
                _isAutoSlicing = false;
                RefreshAutoSlicePanel();
            }
        }

        /// <summary>
        /// 使用 ComfyUI refinement 生成可用切图，并导入为 Unity Sprite。
        /// </summary>
        private async void ImportGeneratedSprites()
        {
            _draftImage = _designImageField?.value as Texture2D;
            if (_draftImage == null)
            {
                SetAutoSliceError("请先选择 Design Image。");
                return;
            }

            if (_segmentationManifest == null || _segmentationManifest.regions == null || _segmentationManifest.regions.Count == 0)
            {
                SetAutoSliceError("请先根据 JSON 生成原始切图。");
                return;
            }

            if (_comfyConfig == null) _comfyConfig = ComfyUiServiceConfig.GetOrCreate();
            SyncComfyFieldsToConfig();
            _isAutoSlicing = true;
            _autoSliceStage = AutoSliceStage.GeneratingSprites;
            _autoSliceStatus = "阶段 2：正在使用 ComfyUI 生成可用切图（refinement）...";
            RefreshAutoSlicePanel();

            try
            {
                await DraftComfyCropRefinementService.RefineSelectedRawCropsAsync(_segmentationManifest, _comfyConfig);

                string prefabName = _prefabAsset != null ? _prefabAsset.name : null;
                DraftSpriteImportReport report = DraftRefinedSpriteImportService.ImportRefinedSprites(_segmentationManifest, _comfyConfig, prefabName);
                _generatedSprites = report.GeneratedSprites;
                _spriteAssignments.Clear();
                _autoSliceStage = AutoSliceStage.ReviewingMatches;
                _autoSliceStatus = $"阶段 2 完成：可用切图 {_generatedSprites.Count} 个。";
                if (_autoSliceHelpBox != null)
                {
                    _autoSliceHelpBox.text = report.HasTargetPrefab
                        ? "请检查可用切图与预览节点的匹配结果，确认后点击匹配 Prefab 节点。"
                        : "已生成可用切图；未修改 prefab。请检查 Sprite 资产后继续匹配或应用。";
                    _autoSliceHelpBox.messageType = HelpBoxMessageType.Info;
                }
                RefreshAutoSlicePanel();
            }
            catch (Exception ex)
            {
                SetAutoSliceError($"ComfyUI refinement 失败：{ex.Message}");
            }
            finally
            {
                _isAutoSlicing = false;
                RefreshAutoSlicePanel();
            }
        }

        /// <summary>
        /// 将生成的 Sprite 与当前预览节点匹配。
        /// </summary>
        private void MatchGeneratedSprites()
        {
            if (_generatedSprites == null || _generatedSprites.Count == 0)
            {
                SetAutoSliceError("请先使用 ComfyUI 生成可用切图。");
                return;
            }

            if (_previewChanges == null || _previewChanges.Count == 0)
            {
                SetAutoSliceError("请先 Generate 或 Parse & Preview，生成 UI 节点预览。");
                return;
            }

            if (_comfyConfig == null) _comfyConfig = ComfyUiServiceConfig.GetOrCreate();
            SyncComfyFieldsToConfig();
            _spriteAssignments = DraftSpriteAssignmentService.BuildAssignments(
                _editableInstance,
                _previewChanges,
                _generatedSprites,
                _comfyConfig);
            DraftSpriteAssignmentService.ApplyAssignmentsToDescriptors(_previewChanges, _spriteAssignments);
            _autoSliceStatus = $"Sprite 匹配完成：{_spriteAssignments.Count} 条候选。";
            RefreshAutoSlicePanel();
            RefreshNodeList();
        }

        /// <summary>
        /// 将已批准的 Sprite assignment 应用到 prefab。
        /// </summary>
        private void ApplySpriteAssignmentsToPrefab()
        {
            if (_editableInstance == null)
            {
                SetAutoSliceError("请先选择 Target Prefab。");
                return;
            }

            if (_spriteAssignments == null || _spriteAssignments.Count == 0)
            {
                SetAutoSliceError("没有可应用的 Sprite assignment。");
                return;
            }

            int approvedCount = _spriteAssignments.Count(a => a != null && a.Approved);
            int skippedCount = _spriteAssignments.Count - approvedCount;
            int overwriteCount = _spriteAssignments.Count(a => a != null && a.Approved && a.HasExistingSprite);
            if (!EditorUtility.DisplayDialog(
                    "应用 Auto Slice Sprite",
                    $"将应用 {approvedCount} 个已批准 Sprite assignment，跳过 {skippedCount} 个未批准 assignment。\n覆盖已有 Sprite 的 assignment：{overwriteCount} 个。\n生成的 Sprite 资产会保留，不会在回滚时删除。\n\n是否继续？",
                    "应用",
                    "取消"))
            {
                _autoSliceStatus = "已取消 Sprite 应用。";
                RefreshAutoSlicePanel();
                return;
            }

            var applied = DraftSpriteApplyService.ApplyAssignments(_editableInstance, _spriteAssignments);
            if (applied.Count == 0)
            {
                SetAutoSliceError("没有 Sprite 被应用，请检查 assignment 是否已批准。");
                return;
            }

            var record = new DraftApplyRecord
            {
                RecordGuid = Guid.NewGuid().ToString(),
                AppliedAt = DateTime.Now,
                CreatedObjectPaths = new List<string>(),
                ModifiedRects = new List<RectChangeRecord>(),
                AddedCollectorKeys = new List<string>(),
                AddedScriptFields = new List<string>(),
                SpriteChanges = applied
            };

            if (_metadata != null)
            {
                _metadata.AddApplyRecord(record);
                EditorUtility.SetDirty(_metadata);
                AssetDatabase.SaveAssets();
            }

            var prefabPath = AssetDatabase.GetAssetPath(_prefabAsset);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                PrefabUtility.SaveAsPrefabAsset(_editableInstance, prefabPath);
                TryAppendAppliedMaintenanceRecord(prefabPath, record, null);
            }

            _autoSliceStatus = $"已应用 Sprite：{applied.Count} 个，跳过 {skippedCount} 个；覆盖已有 Sprite {overwriteCount} 个。生成资产已保留。";
            if (_autoSliceHelpBox != null)
            {
                _autoSliceHelpBox.text = _autoSliceStatus;
                _autoSliceHelpBox.messageType = HelpBoxMessageType.Info;
            }
            RefreshAutoSlicePanel();
            SyncUiFromState();
        }

        /// <summary>
        /// 刷新自动切图面板状态与列表。
        /// </summary>
        private void RefreshAutoSlicePanel()
        {
            int regionCount = _segmentationManifest?.regions?.Count ?? 0;
            int includedRegionCount = _segmentationManifest?.regions?.Count(r => r != null && r.included) ?? 0;
            int rawCount = _segmentationManifest?.regions?.Count(r =>
                r != null
                && !string.IsNullOrWhiteSpace(r.rawPngPath)
                && System.IO.File.Exists(DraftPathUtility.ToAbsolutePath(r.rawPngPath))) ?? 0;
            int refinedCount = _segmentationManifest?.regions?.Count(r =>
                r != null
                && !string.IsNullOrWhiteSpace(r.refinedPngPath)
                && System.IO.File.Exists(DraftPathUtility.ToAbsolutePath(r.refinedPngPath))) ?? 0;
            int spriteCount = _generatedSprites?.Count ?? 0;
            int assignmentCount = _spriteAssignments?.Count ?? 0;
            int approvedAssignmentCount = _spriteAssignments?.Count(a => a != null && a.Approved) ?? 0;

            if (_autoSliceStageLabel != null)
            {
                _autoSliceStageLabel.text = $"阶段：{GetAutoSliceStageDisplay()}";
            }

            if (_autoSlicePrimaryActionLabel != null)
            {
                _autoSlicePrimaryActionLabel.text = "主操作：" + GetAutoSlicePrimaryAction();
            }

            if (_autoSliceOutputLabel != null)
            {
                _autoSliceOutputLabel.text = BuildAutoSliceOutputSummary();
            }

            if (_autoSliceStatusLabel != null)
            {
                string counts = $"Raw {rawCount}/{includedRegionCount} | Refined {refinedCount}/{includedRegionCount} | Sprites {spriteCount} | Assignments {approvedAssignmentCount}/{assignmentCount}";
                _autoSliceStatusLabel.text = string.IsNullOrEmpty(_autoSliceStatus)
                    ? counts
                    : $"{_autoSliceStatus}  |  {counts}";
            }

            if (_testComfyBtn != null) _testComfyBtn.SetEnabled(!_isAutoSlicing);
            if (_generateRegionsBtn != null)
            {
                _generateRegionsBtn.text = _segmentationManifest?.regions?.Count > 0 ? "重新生成原始切图" : "根据 JSON 生成原始切图";
                _generateRegionsBtn.SetEnabled(!_isAutoSlicing && _draftImage != null);
            }
            if (_importSpritesBtn != null)
            {
                _importSpritesBtn.text = _generatedSprites?.Count > 0 ? "重新生成可用切图" : "使用 ComfyUI 生成可用切图";
                _importSpritesBtn.SetEnabled(!_isAutoSlicing && _segmentationManifest?.regions?.Any(r => r != null && r.included) == true);
            }
            if (_matchSpritesBtn != null) _matchSpritesBtn.SetEnabled(!_isAutoSlicing && _generatedSprites?.Count > 0 && _previewChanges?.Count > 0);
            if (_applySpritesBtn != null) _applySpritesBtn.SetEnabled(!_isAutoSlicing && _editableInstance != null && _spriteAssignments?.Any(a => a.Approved) == true);

            RefreshRegionList();
            RefreshAssignmentList();
            RefreshStructurePreview();
        }

        /// <summary>
        /// 获取 Auto Slice 阶段显示文本。
        /// </summary>
        /// <returns>阶段文本。</returns>
        private string GetAutoSliceStageDisplay()
        {
            switch (_autoSliceStage)
            {
                case AutoSliceStage.CheckingConnection: return "检查 ComfyUI 连接（不影响 raw 阶段）";
                case AutoSliceStage.Detecting: return "阶段 1：根据 JSON 生成原始切图";
                case AutoSliceStage.ReviewingRegions: return "阶段 1：审查原始切图";
                case AutoSliceStage.GeneratingSprites: return "阶段 2：使用 ComfyUI 生成可用切图";
                case AutoSliceStage.ReviewingMatches: return "阶段 2：审查 Sprite 匹配";
                case AutoSliceStage.Applying: return "应用选中修改";
                case AutoSliceStage.Applied: return "已应用";
                case AutoSliceStage.Failed: return "失败";
                default: return "阶段 1：根据 JSON 生成原始切图 -> 阶段 2：使用 ComfyUI 生成可用切图";
            }
        }

        /// <summary>
        /// 根据当前数据计算下一步主操作。
        /// </summary>
        /// <returns>下一步操作文本。</returns>
        private string GetAutoSlicePrimaryAction()
        {
            if (_autoSliceStage == AutoSliceStage.Failed)
            {
                if (!string.IsNullOrEmpty(_autoSliceStatus))
                {
                    if (_autoSliceStatus.IndexOf("ComfyUI", StringComparison.OrdinalIgnoreCase) >= 0
                        || _autoSliceStatus.IndexOf("refinement", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return "检查 ComfyUI refinement 后重试";
                    }

                    if (_autoSliceStatus.IndexOf("原始切图", StringComparison.OrdinalIgnoreCase) >= 0
                        || _autoSliceStatus.IndexOf("JSON", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return "检查 JSON / raw crop 后重试";
                    }
                }

                return "检查错误后重试";
            }

            if (_autoSliceStage == AutoSliceStage.Applied) return "可使用 Revert Last 回滚 Prefab 引用";
            if (_draftImage == null) return "先选择 Design Image，再执行阶段 1";
            if (_segmentationManifest?.regions == null || _segmentationManifest.regions.Count == 0) return AutoSliceRawStageText;
            if (_generatedSprites == null || _generatedSprites.Count == 0) return AutoSliceRefinedStageText;
            if (_previewChanges == null || _previewChanges.Count == 0) return "先 Parse & Preview，再匹配 Prefab";
            if (_spriteAssignments == null || _spriteAssignments.Count == 0) return "匹配 Prefab Image 节点";
            if (_spriteAssignments.Any(a => a != null && a.Approved)) return "应用选中修改";
            return "批准至少一个 assignment";
        }

        /// <summary>
        /// 构建当前 Auto Slice 输出路径提示。
        /// </summary>
        /// <returns>输出摘要。</returns>
        private string BuildAutoSliceOutputSummary()
        {
            string rawPath = BuildCurrentRawCropOutputPath();
            string refinedPath = BuildCurrentRefinedCachePath();
            string spritePath = BuildCurrentSpriteOutputPath();

            if (string.IsNullOrEmpty(rawPath))
                rawPath = DraftJsonRawCropService.DefaultRawCropRoot + "/<SourceHash>";
            if (string.IsNullOrEmpty(refinedPath))
                refinedPath = ComfyUiServiceConfig.DefaultCacheRoot + "/<SourceHash>/refined";
            if (string.IsNullOrEmpty(spritePath))
                spritePath = ComfyUiServiceConfig.DefaultGeneratedSpriteOutputRoot + "/<PrefabName>/<SourceHash>";

            return $"输出：raw PNG -> {rawPath} | refined PNG -> {refinedPath} | Sprite -> {spritePath}";
        }

        /// <summary>
        /// 构建当前 raw crop 缓存目录提示。
        /// </summary>
        /// <returns>raw crop 缓存目录。</returns>
        private string BuildCurrentRawCropOutputPath()
        {
            string sourceHash = BuildCurrentSourceHash();
            if (string.IsNullOrEmpty(sourceHash))
                return string.Empty;

            return (DraftJsonRawCropService.DefaultRawCropRoot.TrimEnd('/') + "/" + sourceHash).Replace('\\', '/');
        }

        /// <summary>
        /// 构建当前 refined PNG 缓存目录提示。
        /// </summary>
        /// <returns>refined PNG 缓存目录。</returns>
        private string BuildCurrentRefinedCachePath()
        {
            string sourceHash = BuildCurrentSourceHash();
            if (string.IsNullOrEmpty(sourceHash))
                return string.Empty;

            string cacheRoot = _comfyConfig != null && !string.IsNullOrWhiteSpace(_comfyConfig.CacheRoot)
                ? _comfyConfig.CacheRoot
                : ComfyUiServiceConfig.DefaultCacheRoot;
            return (cacheRoot.TrimEnd('/') + "/" + sourceHash + "/refined").Replace('\\', '/');
        }

        /// <summary>
        /// 构建当前 final Sprite 输出目录提示。
        /// </summary>
        /// <returns>final Sprite 输出目录。</returns>
        private string BuildCurrentSpriteOutputPath()
        {
            string sourceHash = BuildCurrentSourceHash();
            if (string.IsNullOrEmpty(sourceHash))
                return string.Empty;

            string spriteRoot = _comfyConfig != null && !string.IsNullOrWhiteSpace(_comfyConfig.GeneratedSpriteOutputRoot)
                ? _comfyConfig.GeneratedSpriteOutputRoot
                : ComfyUiServiceConfig.DefaultGeneratedSpriteOutputRoot;
            string prefabName = _prefabAsset != null ? _prefabAsset.name : "Standalone";
            return (spriteRoot.TrimEnd('/') + "/" + DraftImageSegmentationService.SanitizeFileName(prefabName) + "/" + sourceHash).Replace('\\', '/');
        }

        /// <summary>
        /// 构建当前源图 hash。
        /// </summary>
        /// <returns>源图 hash。</returns>
        private string BuildCurrentSourceHash()
        {
            if (_segmentationManifest != null && !string.IsNullOrWhiteSpace(_segmentationManifest.sourceHash))
                return DraftImageSegmentationService.SanitizeFileName(_segmentationManifest.sourceHash);

            if (_draftImage != null)
                return DraftImageSegmentationService.ComputeSourceHash(_draftImage);

            return string.Empty;
        }

        /// <summary>
        /// 刷新 region 列表。
        /// </summary>
        private void RefreshRegionList()
        {
            if (_regionListView == null) return;
            var regions = _segmentationManifest?.regions ?? new List<DraftSegmentRegion>();
            _regionListView.itemsSource = regions;
            _regionListView.selectionChanged -= OnRegionSelectionChanged;
            _regionListView.selectionChanged += OnRegionSelectionChanged;
            _regionListView.makeItem = CreateRegionRow;
            _regionListView.bindItem = (element, index) => BindRegionRow(element, index, regions);
            _regionListView.RefreshItems();
        }

        /// <summary>
        /// 创建单条 region 审核行。
        /// </summary>
        /// <returns>region 行元素。</returns>
        private VisualElement CreateRegionRow()
        {
            var row = new VisualElement();
            row.AddToClassList("region-row");

            var includeToggle = new Toggle { name = "region-include-toggle" };
            includeToggle.RegisterValueChangedCallback(evt =>
            {
                if (includeToggle.userData is DraftSegmentRegion region)
                {
                    region.included = evt.newValue;
                    region.reviewState = evt.newValue ? DraftRegionReviewState.Included : DraftRegionReviewState.Ignored;
                    region.reviewReason = evt.newValue ? string.Empty : "用户忽略该 region。";
                    RefreshAutoSlicePanel();
                }
            });
            row.Add(includeToggle);

            var markerField = new TextField { name = "region-marker-field" };
            markerField.AddToClassList("region-marker-field");
            markerField.RegisterValueChangedCallback(evt =>
            {
                if (markerField.userData is DraftSegmentRegion region)
                {
                    region.marker = DraftImageSegmentationService.SanitizeMarker(evt.newValue, region.label, region.id);
                    _generatedSprites.Clear();
                    _spriteAssignments.Clear();
                    RefreshAutoSlicePanel();
                }
            });
            row.Add(markerField);

            var detail = new Label { name = "region-detail-label" };
            detail.AddToClassList("region-detail");
            row.Add(detail);
            return row;
        }

        /// <summary>
        /// 绑定单条 region 审核行。
        /// </summary>
        private void BindRegionRow(VisualElement element, int index, IReadOnlyList<DraftSegmentRegion> regions)
        {
            if (index < 0 || index >= regions.Count) return;
            var region = regions[index];
            var includeToggle = element.Q<Toggle>("region-include-toggle");
            var markerField = element.Q<TextField>("region-marker-field");
            var detail = element.Q<Label>("region-detail-label");
            if (includeToggle != null)
            {
                includeToggle.userData = region;
                includeToggle.SetValueWithoutNotify(region.included);
                includeToggle.tooltip = region.included ? "参与 raw / refined / Sprite 生成" : "已忽略";
            }

            if (markerField != null)
            {
                markerField.userData = region;
                markerField.SetValueWithoutNotify(region.marker ?? string.Empty);
                markerField.tooltip = "修改 marker 会同步影响 raw、refined 和 Sprite 文件名。";
            }

            if (detail == null) return;
            string stateLabel = GetRegionDisplayStateLabel(region);
            string rawStatus = GetRegionPipelineStatusLabel(region.rawPngPath, region.generationStatus, "已生成", "待生成");
            string refinedStatus = GetRegionPipelineStatusLabel(region.refinedPngPath, region.generationStatus, "已生成", "待 refinement");
            string spriteStatus = GetRegionPipelineStatusLabel(region.spriteAssetPath, region.generationStatus, "已导入", "待导入");
            string reasonSummary = BuildRegionReasonSummary(region);
            string workflowLabel = GetRegionRefinementWorkflowLabel(region);
            string alphaLabel = GetRegionAlphaValidationLabel(region);
            detail.text = $"{region.id} | {stateLabel} | {region.sourceKind} | {region.assetKind}/{region.alphaMode} | bbox {region.bbox.width}x{region.bbox.height} @ {region.bbox.x},{region.bbox.y} | conf {region.confidence:0.00}\nraw: {rawStatus} | refined: {refinedStatus} ({workflowLabel}) | sprite: {spriteStatus} | alpha: {alphaLabel}\n原因: {reasonSummary}";
            detail.tooltip = BuildRegionTooltip(region, stateLabel, rawStatus, refinedStatus, spriteStatus, reasonSummary);
            detail.style.color = GetRegionStyleColor(region);
        }

        /// <summary>
        /// 处理 region 列表选中变化。
        /// </summary>
        private void OnRegionSelectionChanged(IEnumerable<object> selectedItems)
        {
            var region = selectedItems?.OfType<DraftSegmentRegion>().FirstOrDefault();
            _selectedRegionId = region?.id;
            _regionListView?.RefreshItems();
            RefreshStructurePreview();
        }

        /// <summary>
        /// 刷新 Sprite assignment 列表。
        /// </summary>
        private void RefreshAssignmentList()
        {
            if (_assignmentListView == null) return;
            _assignmentListView.itemsSource = _spriteAssignments;
            _assignmentListView.makeItem = CreateAssignmentRow;
            _assignmentListView.bindItem = BindAssignmentRow;
            _assignmentListView.RefreshItems();
        }

        /// <summary>
        /// 创建单条 assignment 审核行。
        /// </summary>
        /// <returns>assignment 行元素。</returns>
        private VisualElement CreateAssignmentRow()
        {
            var row = new VisualElement();
            row.AddToClassList("assignment-row");

            var toggle = new Toggle("批准") { name = "assignment-approved-toggle" };
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (toggle.userData is DraftSpriteAssignment assignment)
                {
                    assignment.Approved = evt.newValue;
                    assignment.ReviewState = evt.newValue
                        ? DraftRegionReviewState.Included
                        : assignment.RequiresReview ? DraftRegionReviewState.NeedsReview : DraftRegionReviewState.Pending;
                    DraftSpriteAssignmentService.ApplyAssignmentsToDescriptors(_previewChanges, _spriteAssignments);
                    RefreshNodeList();
                    RefreshAutoSlicePanel();
                }
            });
            row.Add(toggle);

            var detail = new Label { name = "assignment-detail-label" };
            detail.AddToClassList("assignment-detail");
            row.Add(detail);
            return row;
        }

        /// <summary>
        /// 绑定单条 assignment 审核行。
        /// </summary>
        /// <param name="element">行元素。</param>
        /// <param name="index">assignment 索引。</param>
        private void BindAssignmentRow(VisualElement element, int index)
        {
            if (index < 0 || index >= _spriteAssignments.Count) return;
            var assignment = _spriteAssignments[index];
            var toggle = element.Q<Toggle>("assignment-approved-toggle");
            var label = element.Q<Label>("assignment-detail-label");
            if (toggle != null)
            {
                toggle.userData = assignment;
                toggle.SetValueWithoutNotify(assignment.Approved);
                toggle.tooltip = assignment.RequiresReview ? assignment.ReviewReason : "自动批准，可手动取消。";
            }

            if (label == null) return;
            string review = assignment.RequiresReview ? "需复核" : "自动";
            string approved = assignment.Approved ? "✓ 已批准" : "未批准";
            string oldSprite = string.IsNullOrEmpty(assignment.ExistingSpriteAssetPath) ? "<none>" : assignment.ExistingSpriteAssetPath;
            string newSprite = string.IsNullOrEmpty(assignment.SpriteAssetPath) ? "<missing>" : assignment.SpriteAssetPath;
            string reviewReason = string.IsNullOrEmpty(assignment.ReviewReason) ? assignment.MatchReason : assignment.ReviewReason;
            label.text = $"{approved} / {review} | {assignment.NodePath}\n旧: {oldSprite}\n新: {newSprite}\n{assignment.Marker} ({assignment.Confidence:0.00}) | {assignment.MatchReason}\n{reviewReason}";
            label.style.color = assignment.RequiresReview && !assignment.Approved
                ? new StyleColor(new Color(1f, 0.75f, 0.25f))
                : new StyleColor(Color.green);
        }

        /// <summary>
        /// 设置自动切图错误提示。
        /// </summary>
        private void SetAutoSliceError(string message)
        {
            _autoSliceStage = AutoSliceStage.Failed;
            _autoSliceStatus = message;
            if (_autoSliceHelpBox != null)
            {
                _autoSliceHelpBox.text = message;
                _autoSliceHelpBox.messageType = HelpBoxMessageType.Error;
            }
            RefreshAutoSlicePanel();
        }

        // ─────────────────────── 4. AI RESULT ───────────────────────

        /// <summary>
        /// 从 UI 字段同步并解析 AI 响应 JSON（用户手动编辑后点击按钮时调用）。
        /// </summary>
        private void ParseAndPreviewFromField()
        {
            _aiResponse = _aiResponseField?.value;
            ParseAndPreview();
        }

        /// <summary>
        /// 解析 <see cref="_aiResponse"/> JSON 字符串为节点变更预览列表。
        /// <para>直接使用 _aiResponse 字段，不依赖 UI 元素值。</para>
        /// </summary>
        private void ParseAndPreview()
        {
            if (string.IsNullOrEmpty(_aiResponse))
            {
                Debug.LogWarning("[DraftWorkbench] AI 响应为空，无法解析。");
                return;
            }

            string json = AiVisionClient.ExtractJson(_aiResponse);
            if (string.IsNullOrEmpty(json))
            {
                _generateError = "无法从 AI 响应中提取 JSON。";
                ShowGenerateError();
                return;
            }

            _lastParsedSourceJson = json;
            _lastParsedSourceJsonHash = DraftMaintenanceRecordStore.ComputeSourceJsonHash(json);

            UiStructure structure;
            try
            {
                structure = JsonUtility.FromJson<UiStructure>(json);
            }
            catch (Exception e)
            {
                _generateError = $"JSON 反序列化失败: {e.Message}";
                ShowGenerateError();
                return;
            }

            if (structure?.root == null)
            {
                _generateError = "解析结果为空（UiStructure.root 为 null）。";
                ShowGenerateError();
                return;
            }

            if (structure.root.size == null)
            {
                _generateError = "JSON 解析后 root.size 为 null，可能是 JSON 字段格式不兼容。";
                ShowGenerateError();
                return;
            }

            if (structure.root.children != null && structure.root.children.Any(child => child != null && child.size == null))
            {
                _generateError = "JSON 解析后存在缺少 size 的一级节点，可能是 JSON 字段格式不兼容。";
                ShowGenerateError();
                return;
            }

            ApplyUiStructureHandoffDefaults(structure);
            TryApplyUnityBuildOptions(structure);

            var converter = new UiStructureConverter();
            var convertedNodes = converter.Convert(structure, UiStructureConversionOptions.DraftWorkbenchDefault);
            _conversionReport = converter.Report;

            var descriptors = convertedNodes.Select(n => n.Descriptor).ToList();
            foreach (var node in convertedNodes)
            {
                if (node.Descriptor != null)
                {
                    node.Descriptor.Visuals = node.Visuals;
                    node.Descriptor.LayoutInfo = node.LayoutInfo;
                }
            }

            if (_editableInstance != null)
            {
                _previewChanges = PrefabDraftBuilder.BuildPreview(_editableInstance, descriptors);
            }
            else
            {
                _previewChanges = new List<UguiNodeChange>();
                foreach (var desc in descriptors)
                {
                    _previewChanges.Add(new UguiNodeChange
                    {
                        Descriptor = desc,
                        IsNew = true,
                        HasConflict = false
                    });
                }
            }

            TryAppendPreviewMaintenanceRecord();

            Debug.Log($"[DraftWorkbench] 解析完成：{_previewChanges.Count} 个节点。");
            SyncUiFromState();
        }

        /// <summary>
        /// 应用 Image-To-UI 顶层 unity 配置：在未手动选择 Target Prefab 时按 outputPrefabPath 选择或创建目标 Prefab。
        /// </summary>
        private void TryApplyUnityBuildOptions(UiStructure structure)
        {
            string outputPrefabPath = NormalizeUnityAssetPath(structure?.unity?.outputPrefabPath);
            if (string.IsNullOrEmpty(outputPrefabPath))
                return;

            if (_prefabAsset != null)
            {
                string selectedPath = NormalizeUnityAssetPath(AssetDatabase.GetAssetPath(_prefabAsset));
                if (!string.Equals(selectedPath, outputPrefabPath, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning($"[DraftWorkbench] ui_structure.json 指定 outputPrefabPath={outputPrefabPath}，当前已选择 Target Prefab={selectedPath}，将继续使用当前选择。");
                }
                return;
            }

            if (!IsWritablePrefabPath(outputPrefabPath))
            {
                Debug.LogWarning($"[DraftWorkbench] ui_structure.json 的 outputPrefabPath 无效或不在 Assets 下: {outputPrefabPath}");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(outputPrefabPath);
            if (prefab == null)
            {
                prefab = CreateCanvasPrefabAtPath(outputPrefabPath, structure);
            }

            if (prefab != null)
            {
                SelectTargetPrefab(prefab);
            }
        }

        /// <summary>
        /// 构造发送给 AI 的 UI 结构请求上下文。
        /// </summary>
        private UiStructureRequestContext BuildUiStructureRequestContext()
        {
            string prefabPath = _prefabAsset != null ? AssetDatabase.GetAssetPath(_prefabAsset) : string.Empty;
            string outputPrefabPath = !string.IsNullOrWhiteSpace(prefabPath)
                ? NormalizeUnityAssetPath(prefabPath)
                : string.Empty;

            return new UiStructureRequestContext
            {
                CanvasName = _prefabAsset != null ? _prefabAsset.name : string.Empty,
                OutputPrefabPath = outputPrefabPath,
                SpriteRootFolder = _comfyConfig != null ? _comfyConfig.GeneratedSpriteOutputRoot : string.Empty
            };
        }

        /// <summary>
        /// 解析结果缺少顶层 unity 配置时，按底层默认值补齐，避免后续导出路径和资源根目录丢失。
        /// </summary>
        private void ApplyUiStructureHandoffDefaults(UiStructure structure)
        {
            if (structure == null)
                return;

            if (structure.unity == null)
            {
                structure.unity = new UiUnityBuildOptions();
            }

            if (string.IsNullOrWhiteSpace(structure.unity.outputPrefabPath))
            {
                string canvasName = !string.IsNullOrWhiteSpace(structure.canvas != null ? structure.canvas.name : string.Empty)
                    ? structure.canvas.name
                    : (_prefabAsset != null ? _prefabAsset.name : "ImageToUI");
                structure.unity.outputPrefabPath = AiVisionClient.GetDefaultOutputPrefabPath(canvasName);
            }

            if (string.IsNullOrWhiteSpace(structure.unity.spriteRootFolder))
            {
                string generatedRoot = _comfyConfig != null ? _comfyConfig.GeneratedSpriteOutputRoot : string.Empty;
                structure.unity.spriteRootFolder = NormalizeUnityAssetPath(generatedRoot);
            }
        }

        /// <summary>
        /// 将指定 Prefab 设为 Target Prefab，并加载可编辑实例。
        /// </summary>
        private void SelectTargetPrefab(GameObject prefab)
        {
            if (prefab == null || prefab == _prefabAsset)
                return;

            UnloadEditableInstance();
            _prefabAsset = prefab;
            if (_targetPrefabField != null)
            {
                _suppressPrefabChangeCallback = true;
                _targetPrefabField.SetValueWithoutNotify(_prefabAsset);
                _suppressPrefabChangeCallback = false;
            }

            LoadPrefab();
        }

        /// <summary>
        /// 为 Image-To-UI outputPrefabPath 创建一个最小 Canvas Prefab，后续仍由 Workbench 预览/应用节点。
        /// </summary>
        private static GameObject CreateCanvasPrefabAtPath(string prefabPath, UiStructure structure)
        {
            if (!EnsureAssetFolder(prefabPath))
                return null;

            string rootName = !string.IsNullOrWhiteSpace(structure?.canvas?.name)
                ? structure.canvas.name
                : Path.GetFileNameWithoutExtension(prefabPath);
            var root = new GameObject(SanitizeGameObjectName(rootName), typeof(RectTransform));
            try
            {
                var rect = root.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.zero;

                var canvas = root.AddComponent<UnityEngine.Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.pixelPerfect = false;

                var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(
                    Mathf.Max(1, structure?.canvas?.width ?? 1920),
                    Mathf.Max(1, structure?.canvas?.height ?? 1080));

                root.AddComponent<UnityEngine.UI.GraphicRaycaster>();

                bool success;
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out success);
                if (!success)
                {
                    Debug.LogWarning($"[DraftWorkbench] 无法创建 outputPrefabPath 指定的 Prefab: {prefabPath}");
                    return null;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) ?? saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 确保资源路径的父目录存在。
        /// </summary>
        private static bool EnsureAssetFolder(string assetPath)
        {
            string normalized = NormalizeUnityAssetPath(assetPath);
            string directory = Path.GetDirectoryName(normalized)?.Replace("\\", "/");
            if (string.IsNullOrEmpty(directory) || directory == "Assets" || AssetDatabase.IsValidFolder(directory))
                return !string.IsNullOrEmpty(directory);

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return false;

            Directory.CreateDirectory(Path.Combine(projectRoot, directory));
            AssetDatabase.Refresh();
            return AssetDatabase.IsValidFolder(directory);
        }

        /// <summary>
        /// 判断路径是否是可由 Draft Workbench 创建或写入的 Assets 下 Prefab。
        /// </summary>
        private static bool IsWritablePrefabPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            return assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                   && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 统一 Unity 资源路径分隔符。
        /// </summary>
        private static string NormalizeUnityAssetPath(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath)
                ? string.Empty
                : assetPath.Replace("\\", "/").Trim();
        }

        /// <summary>
        /// 清理 GameObject 名称中 Unity 层级不适合使用的字符。
        /// </summary>
        private static string SanitizeGameObjectName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Canvas";

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name.Trim();
        }

        // ─────────────────────── 4. PREVIEW ───────────────────────

        /// <summary>
        /// 尝试追加 preview 阶段维护记录。
        /// </summary>
        private void TryAppendPreviewMaintenanceRecord()
        {
            string prefabPath = _prefabAsset != null ? AssetDatabase.GetAssetPath(_prefabAsset) : string.Empty;
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogWarning("[DraftWorkbench] 未选择 Target Prefab，跳过 preview 维护记录保存。");
                return;
            }

            var entry = DraftMaintenanceRecordStore.AppendPreviewRecord(
                prefabPath,
                _draftImage,
                _metadata,
                _lastParsedSourceJson,
                _previewChanges,
                _conversionReport,
                _canvasWidth,
                _canvasHeight,
                _editableInstance);
            _lastPreviewMaintenanceEntryGuid = entry?.EntryGuid;
        }

        /// <summary>
        /// 尝试追加 applied 阶段维护记录。
        /// </summary>
        private void TryAppendAppliedMaintenanceRecord(string prefabPath, DraftApplyRecord record, DraftBindingReport bindingReport)
        {
            if (string.IsNullOrEmpty(prefabPath))
                return;

            DraftMaintenanceRecordStore.AppendAppliedRecord(
                prefabPath,
                _draftImage,
                _metadata,
                _lastParsedSourceJson,
                _previewChanges,
                _conversionReport,
                record,
                bindingReport,
                _canvasWidth,
                _canvasHeight,
                _editableInstance);
        }

        /// <summary>
        /// 尝试追加 reverted 阶段维护记录。
        /// </summary>
        private void TryAppendRevertedMaintenanceRecord(string prefabPath, DraftApplyRecord revertedRecord)
        {
            if (string.IsNullOrEmpty(prefabPath))
                return;

            DraftMaintenanceRecordStore.AppendRevertedRecord(
                prefabPath,
                _draftImage,
                _metadata,
                revertedRecord,
                _lastParsedSourceJsonHash,
                true);
        }

        /// <summary>
        /// 刷新节点摘要标签。
        /// </summary>
        private void RefreshNodeList()
        {
            if (_nodeSummaryLabel == null) return;

            if (_previewChanges == null || _previewChanges.Count == 0)
            {
                _nodeSummaryLabel.style.display = DisplayStyle.None;
                _previewEmptyHelpBox.style.display = DisplayStyle.Flex;
            }
            else
            {
                int newCount = _previewChanges.Count(c => c.IsNew);
                int updateCount = _previewChanges.Count(c => !c.IsNew && !c.HasConflict);
                int conflictCount = _previewChanges.Count(c => c.HasConflict);
                _nodeSummaryLabel.text = $"节点摘要：新建 {newCount}  |  更新 {updateCount}  |  冲突 {conflictCount}";
                _nodeSummaryLabel.style.display = DisplayStyle.Flex;
                _previewEmptyHelpBox.style.display = DisplayStyle.None;
            }

            // 更新 ListView
            if (_nodeListView != null)
            {
                if (_previewChanges != null && _previewChanges.Count > 0)
                {
                    _nodeListView.itemsSource = _previewChanges;
                    _nodeListView.makeItem = () => new Label();
                    _nodeListView.bindItem = (element, index) =>
                    {
                        var label = element as Label;
                        if (label == null || index >= _previewChanges.Count) return;
                        var change = _previewChanges[index];
                        if (change?.Descriptor == null) return;

                        string status = change.HasConflict
                            ? "[冲突]"
                            : change.IsNew ? "[新建]" : "[已存在]";
                        label.text = $"  {status}  {change.Descriptor.Name}";
                        label.style.color = change.HasConflict
                            ? new StyleColor(new Color(1f, 0.5f, 0f))
                            : change.IsNew
                                ? new StyleColor(Color.green)
                                : new StyleColor(Color.white);
                    };
                    _nodeListView.RefreshItems();
                }
                else
                {
                    _nodeListView.itemsSource = null;
                    _nodeListView.RefreshItems();
                }
            }

            UpdateActionButtonStates();
        }

        /// <summary>
        /// 更新 Apply/Revert 按钮的可用状态。
        /// </summary>
        private void UpdateActionButtonStates()
        {
            bool canApply = _editableInstance != null && _previewChanges != null && _previewChanges.Count > 0;
            bool canRevert = _editableInstance != null && _metadata != null && _metadata.GetLatestApplyRecord() != null;

            if (_applyBtn != null) _applyBtn.SetEnabled(canApply);
            if (_revertBtn != null) _revertBtn.SetEnabled(canRevert);
        }

        // ─────────────────────── 结构框预览 ───────────────────────

        /// <summary>
        /// 刷新结构框预览（包括设计图和结构框）。
        /// </summary>
        private void RefreshStructurePreview()
        {
            bool isWide = _previewPanel.style.display != DisplayStyle.None;
            RefreshStructurePreviewPane(
                isWide ? _previewImage : _inlinePreviewImage,
                isWide ? _previewContent : _inlinePreviewContent,
                isWide ? _structurePreviewScroll : _inlinePreviewScroll,
                isWide ? _structureBoxPool : _inlineStructureBoxPool,
                isWide ? _regionOverlayBoxPool : _inlineRegionOverlayBoxPool,
                isWide);
        }

        /// <summary>
        /// 刷新单个预览 pane 的设计图和结构框。
        /// </summary>
        private void RefreshStructurePreviewPane(
            VisualElement imageElement, VisualElement contentElement,
            ScrollView scrollView, List<VisualElement> boxPool, List<VisualElement> regionBoxPool, bool isWide)
        {
            if (imageElement == null || contentElement == null || scrollView == null) return;

            // 清除旧结构框
            foreach (var box in boxPool)
            {
                if (box.parent != null) box.RemoveFromHierarchy();
            }

            foreach (var box in regionBoxPool)
            {
                if (box.parent != null) box.RemoveFromHierarchy();
            }

            var drawableChanges = GetDrawableStructurePreviewChanges();
            bool hasRegionOverlays = _segmentationManifest?.regions != null && _segmentationManifest.regions.Count > 0;
            if ((drawableChanges.Count == 0 && !hasRegionOverlays) || _draftImage == null)
            {
                imageElement.style.backgroundImage = StyleKeyword.None;
                return;
            }

            // 设置设计图背景
            imageElement.style.backgroundImage = new StyleBackground(_draftImage);
            var bgPos = BackgroundPropertyHelper.ConvertScaleModeToBackgroundPosition(ScaleMode.StretchToFill);
            var bgRep = BackgroundPropertyHelper.ConvertScaleModeToBackgroundRepeat(ScaleMode.StretchToFill);
            var bgSize = BackgroundPropertyHelper.ConvertScaleModeToBackgroundSize(ScaleMode.StretchToFill);
            imageElement.style.backgroundPositionX = bgPos;
            imageElement.style.backgroundPositionY = bgPos;
            imageElement.style.backgroundRepeat = bgRep;
            imageElement.style.backgroundSize = bgSize;

            // 计算布局
            Vector2 sourceSize = GetStructurePreviewSourceSize(drawableChanges);
            float paneWidth = scrollView.resolvedStyle.width;
            float paneHeight = scrollView.resolvedStyle.height;
            if (float.IsNaN(paneWidth) || paneWidth <= 0f) paneWidth = 400f;
            if (float.IsNaN(paneHeight) || paneHeight <= 0f) paneHeight = 300f;

            float zoomFactor = _useManualStructurePreviewZoom ? _structurePreviewZoom : 1f;
            StructurePreviewLayoutResult layout = StructurePreviewLayoutUtility.CalculateLayout(
                new Vector2(paneWidth - 16f, paneHeight - 16f),
                sourceSize,
                zoomFactor);

            if (!layout.IsValid) return;

            // 设置内容容器尺寸
            contentElement.style.width = layout.ContentWidth;
            contentElement.style.height = layout.ContentHeight;

            // 设置设计图元素
            float imageLeft = Mathf.Max(0f, (layout.ContentWidth - layout.ImageWidth) * 0.5f);
            float imageTop = Mathf.Max(0f, (layout.ContentHeight - layout.ImageHeight) * 0.5f);
            imageElement.style.left = imageLeft;
            imageElement.style.top = imageTop;
            imageElement.style.width = layout.ImageWidth;
            imageElement.style.height = layout.ImageHeight;
            imageElement.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.18f));

            // 更新结构框
            int neededBoxes = drawableChanges.Count;
            while (boxPool.Count < neededBoxes)
            {
                var box = new VisualElement();
                box.AddToClassList("structure-box");
                box.style.position = Position.Absolute;
                boxPool.Add(box);
            }

            for (int i = 0; i < neededBoxes; i++)
            {
                var box = boxPool[i];
                var change = drawableChanges[i];

                Rect imageRect = new Rect(imageLeft, imageTop, layout.ImageWidth, layout.ImageHeight);
                if (TryGetStructurePreviewRect(change.Descriptor, sourceSize, imageRect, out Rect boxRect))
                {
                    box.style.left = boxRect.x;
                    box.style.top = boxRect.y;
                    box.style.width = boxRect.width;
                    box.style.height = boxRect.height;
                    box.style.display = DisplayStyle.Flex;

                    Color color = StructurePreviewColorUtility.GetStructurePreviewColor(
                        _structurePreviewColorMode, change);
                    var styleColor = new StyleColor(color);
                    box.style.borderLeftColor = styleColor;
                    box.style.borderRightColor = styleColor;
                    box.style.borderTopColor = styleColor;
                    box.style.borderBottomColor = styleColor;
                }
                else
                {
                    box.style.display = DisplayStyle.None;
                }

                if (box.parent == null)
                {
                    contentElement.Add(box);
                }
            }

            // 隐藏多余的结构框
            for (int i = neededBoxes; i < boxPool.Count; i++)
            {
                if (boxPool[i].parent != null)
                    boxPool[i].RemoveFromHierarchy();
            }

            RefreshRegionOverlays(contentElement, regionBoxPool, sourceSize, new Rect(imageLeft, imageTop, layout.ImageWidth, layout.ImageHeight));

            // 更新统计标签
            UpdateStatsLabel(isWide, drawableChanges);
        }

        /// <summary>
        /// 刷新 Auto Slice region overlay。
        /// </summary>
        private void RefreshRegionOverlays(VisualElement contentElement, List<VisualElement> regionBoxPool, Vector2 sourceSize, Rect imageRect)
        {
            var regions = _segmentationManifest?.regions ?? new List<DraftSegmentRegion>();
            int neededBoxes = regions.Count;
            while (regionBoxPool.Count < neededBoxes)
            {
                var box = new VisualElement();
                box.AddToClassList("structure-box");
                box.AddToClassList("region-overlay-box");
                box.style.position = Position.Absolute;
                regionBoxPool.Add(box);
            }

            for (int i = 0; i < neededBoxes; i++)
            {
                var region = regions[i];
                var box = regionBoxPool[i];
                if (region?.bbox == null || !TryGetRegionPreviewRect(region, sourceSize, imageRect, out Rect regionRect))
                {
                    box.style.display = DisplayStyle.None;
                    continue;
                }

                box.style.left = regionRect.x;
                box.style.top = regionRect.y;
                box.style.width = regionRect.width;
                box.style.height = regionRect.height;
                box.style.display = DisplayStyle.Flex;
                Color color = GetRegionOverlayColor(region);
                var styleColor = new StyleColor(color);
                box.style.borderLeftColor = styleColor;
                box.style.borderRightColor = styleColor;
                box.style.borderTopColor = styleColor;
                box.style.borderBottomColor = styleColor;
                box.style.backgroundColor = new StyleColor(new Color(color.r, color.g, color.b, 0.08f));
                box.tooltip = BuildRegionTooltip(
                    region,
                    GetRegionDisplayStateLabel(region),
                    GetRegionPipelineStatusLabel(region.rawPngPath, region.generationStatus, "已生成", "待生成"),
                    GetRegionPipelineStatusLabel(region.refinedPngPath, region.generationStatus, "已生成", "待 refinement"),
                    GetRegionPipelineStatusLabel(region.spriteAssetPath, region.generationStatus, "已导入", "待导入"),
                    BuildRegionReasonSummary(region));
                if (box.parent == null)
                {
                    contentElement.Add(box);
                }
            }

            for (int i = neededBoxes; i < regionBoxPool.Count; i++)
            {
                if (regionBoxPool[i].parent != null)
                    regionBoxPool[i].RemoveFromHierarchy();
            }
        }

        /// <summary>
        /// 更新预览工具栏的统计标签。
        /// </summary>
        private void UpdateStatsLabel(bool isWide, List<UguiNodeChange> drawableChanges)
        {
            var statsLabel = isWide ? _statsLabel : _inlineStatsLabel;
            if (statsLabel == null) return;

            int newCount = _previewChanges.Count(c => c.IsNew);
            int updateCount = _previewChanges.Count(c => !c.IsNew && !c.HasConflict);
            int conflictCount = _previewChanges.Count(c => c.HasConflict);

            string colorLegend = _structurePreviewColorMode == StructurePreviewColorMode.ComponentType
                ? "Button 粉 / Image 青 / Text 黄 / Layout 紫 / 其它 灰"
                : "新建 绿 / 更新 蓝 / 冲突 橙";

            statsLabel.text = $"新建 {newCount}  |  更新 {updateCount}  |  冲突 {conflictCount}  |  {colorLegend}";
        }

        /// <summary>
        /// 获取当前可绘制 source bounds 的节点列表。
        /// </summary>
        private List<UguiNodeChange> GetDrawableStructurePreviewChanges()
        {
            return _previewChanges
                .Where(c => c?.Descriptor != null && c.Descriptor.HasSourceBounds)
                .ToList();
        }

        /// <summary>
        /// 获取结构框预览使用的原始坐标系尺寸。
        /// </summary>
        private Vector2 GetStructurePreviewSourceSize(List<UguiNodeChange> drawableChanges)
        {
            Vector2 draftImageSize = _draftImage != null && _draftImage.width > 0 && _draftImage.height > 0
                ? new Vector2(_draftImage.width, _draftImage.height)
                : Vector2.zero;
            return StructurePreviewLayoutUtility.ResolveSourceSize(
                drawableChanges, draftImageSize, new Vector2(_canvasWidth, _canvasHeight));
        }

        /// <summary>
        /// 将 region bbox 转换为预览区域坐标。
        /// </summary>
        private static bool TryGetRegionPreviewRect(DraftSegmentRegion region, Vector2 sourceSize, Rect imageRect, out Rect boxRect)
        {
            boxRect = Rect.zero;
            if (region?.bbox == null || sourceSize.x <= 0f || sourceSize.y <= 0f)
                return false;

            var sourceRect = region.bbox.ToRect();
            float x = imageRect.x + sourceRect.x / sourceSize.x * imageRect.width;
            float y = imageRect.y + sourceRect.y / sourceSize.y * imageRect.height;
            float width = sourceRect.width / sourceSize.x * imageRect.width;
            float height = sourceRect.height / sourceSize.y * imageRect.height;
            boxRect = new Rect(x, y, width, height);
            return width > 0f && height > 0f;
        }

        /// <summary>
        /// 根据 region 状态返回列表文字颜色。
        /// </summary>
        private StyleColor GetRegionStyleColor(DraftSegmentRegion region)
        {
            return new StyleColor(GetRegionOverlayColor(region));
        }

        /// <summary>
        /// 根据 region 状态返回 overlay 颜色。
        /// </summary>
        private Color GetRegionOverlayColor(DraftSegmentRegion region)
        {
            if (region == null)
                return Color.gray;
            if (!string.IsNullOrEmpty(_selectedRegionId) && string.Equals(region.id, _selectedRegionId, StringComparison.Ordinal))
                return new Color(0.35f, 0.7f, 1f);
            if (!region.included || region.reviewState == DraftRegionReviewState.Ignored)
                return new Color(0.55f, 0.55f, 0.55f);
            if (region.generationStatus == DraftRegionGenerationStatus.Failed)
                return new Color(1f, 0.35f, 0.35f);
            if (region.requiresReview || region.reviewState == DraftRegionReviewState.NeedsReview)
                return new Color(1f, 0.7f, 0.2f);
            if (region.generationStatus == DraftRegionGenerationStatus.Skipped)
                return new Color(0.65f, 0.65f, 0.65f);
            if (region.confidence >= 0.75f)
                return new Color(0.2f, 1f, 0.45f);
            return new Color(1f, 0.9f, 0.25f);
        }

        /// <summary>
        /// 将 region 的人工审查和生成状态组合成一条短标签。
        /// </summary>
        private string GetRegionDisplayStateLabel(DraftSegmentRegion region)
        {
            if (region == null)
                return "未知";

            var states = new List<string>();
            if (!string.IsNullOrEmpty(_selectedRegionId) && string.Equals(region.id, _selectedRegionId, StringComparison.Ordinal))
            {
                states.Add("已选中");
            }

            if (!region.included || region.reviewState == DraftRegionReviewState.Ignored)
            {
                states.Add("已忽略");
            }
            else if (region.generationStatus == DraftRegionGenerationStatus.Failed)
            {
                states.Add("失败");
            }
            else if (region.generationStatus == DraftRegionGenerationStatus.Skipped)
            {
                states.Add("已跳过");
            }
            else if (region.requiresReview || region.reviewState == DraftRegionReviewState.NeedsReview)
            {
                states.Add("需复核");
            }
            else if (region.generationStatus == DraftRegionGenerationStatus.Generated)
            {
                states.Add("已生成");
            }
            else
            {
                states.Add("已包含");
            }

            return string.Join(" / ", states);
        }

        /// <summary>
        /// 根据输出路径和生成状态拼接短状态文本。
        /// </summary>
        private static string GetRegionPipelineStatusLabel(string path, DraftRegionGenerationStatus status, string generatedLabel, string pendingLabel)
        {
            if (!string.IsNullOrWhiteSpace(path))
                return generatedLabel;

            switch (status)
            {
                case DraftRegionGenerationStatus.Failed:
                    return "失败";
                case DraftRegionGenerationStatus.Skipped:
                    return "已跳过";
                default:
                    return pendingLabel;
            }
        }

        /// <summary>
        /// 获取 region 最近一次 refinement 工作流标签。
        /// </summary>
        private static string GetRegionRefinementWorkflowLabel(DraftSegmentRegion region)
        {
            if (region?.refinementAttempts != null && region.refinementAttempts.Count > 0)
            {
                var last = region.refinementAttempts[region.refinementAttempts.Count - 1];
                if (!string.IsNullOrWhiteSpace(last.WorkflowName))
                    return last.WorkflowName;

                return last.WorkflowKind.ToString();
            }

            if (region != null && region.alphaMode == DraftAlphaMode.TransparentForeground)
                return "BiRefNet/RMBG";

            return "Passthrough";
        }

        /// <summary>
        /// 获取 region alpha 校验状态标签。
        /// </summary>
        private static string GetRegionAlphaValidationLabel(DraftSegmentRegion region)
        {
            if (region == null)
                return "未知";

            if (region.alphaValidation == null || region.alphaValidation.Status == DraftAlphaValidationStatus.NotRequired)
            {
                return region.requiresTransparentAlpha || region.alphaMode == DraftAlphaMode.TransparentForeground
                    ? "待校验"
                    : "不需要";
            }

            return region.alphaValidation.Status.ToString();
        }

        /// <summary>
        /// 汇总 region 的原因说明。
        /// </summary>
        private static string BuildRegionReasonSummary(DraftSegmentRegion region)
        {
            if (region == null)
                return "无";

            var reasons = new List<string>();
            if (!string.IsNullOrWhiteSpace(region.reviewReason))
            {
                reasons.Add(region.reviewReason);
            }

            bool shouldShowGenerationMessage = region.generationStatus == DraftRegionGenerationStatus.Failed
                || region.generationStatus == DraftRegionGenerationStatus.Skipped
                || region.requiresReview
                || region.reviewState == DraftRegionReviewState.NeedsReview
                || region.reviewState == DraftRegionReviewState.Ignored;
            if (shouldShowGenerationMessage
                && !string.IsNullOrWhiteSpace(region.generationMessage)
                && !string.Equals(region.generationMessage, region.reviewReason, StringComparison.Ordinal))
            {
                reasons.Add(region.generationMessage);
            }

            return reasons.Count > 0 ? string.Join(" | ", reasons) : "无";
        }

        /// <summary>
        /// 构建 region 详细提示。
        /// </summary>
        private static string BuildRegionTooltip(
            DraftSegmentRegion region,
            string stateLabel,
            string rawStatus,
            string refinedStatus,
            string spriteStatus,
            string reasonSummary)
        {
            if (region == null)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("State: " + stateLabel);
            sb.AppendLine("SourceKind: " + region.sourceKind);
            sb.AppendLine("AssetKind: " + region.assetKind);
            sb.AppendLine("AlphaMode: " + region.alphaMode);
            sb.AppendLine("Workflow: " + GetRegionRefinementWorkflowLabel(region));
            sb.AppendLine("Alpha: " + GetRegionAlphaValidationLabel(region));
            if (region.alphaValidation != null && !string.IsNullOrWhiteSpace(region.alphaValidation.Message))
            {
                sb.AppendLine("AlphaReason: " + region.alphaValidation.Message);
                sb.AppendLine("AlphaStats: transparent=" + region.alphaValidation.TransparentRatio.ToString("0.###")
                              + ", borderOpaque=" + region.alphaValidation.BorderOpaqueRatio.ToString("0.###")
                              + ", nonOpaque=" + region.alphaValidation.NonOpaquePixelRatio.ToString("0.###"));
            }
            sb.AppendLine("NodePath: " + (string.IsNullOrWhiteSpace(region.nodePath) ? "<none>" : region.nodePath));
            sb.AppendLine("Raw: " + rawStatus + " | " + (string.IsNullOrWhiteSpace(region.rawPngPath) ? "<none>" : region.rawPngPath));
            sb.AppendLine("Refined: " + refinedStatus + " | " + (string.IsNullOrWhiteSpace(region.refinedPngPath) ? "<none>" : region.refinedPngPath));
            sb.AppendLine("Sprite: " + spriteStatus + " | " + (string.IsNullOrWhiteSpace(region.spriteAssetPath) ? "<none>" : region.spriteAssetPath));
            sb.AppendLine("Reason: " + reasonSummary);
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 将原始设计图坐标系中的识别框换算为预览区域坐标。
        /// </summary>
        private static bool TryGetStructurePreviewRect(
            UguiNodeDescriptor descriptor, Vector2 sourceSize,
            Rect imageRect, out Rect boxRect)
        {
            Rect sourceRect = descriptor.SourceBounds;
            if (sourceRect.width <= 0f || sourceRect.height <= 0f)
            {
                boxRect = Rect.zero;
                return false;
            }

            float x = imageRect.x + sourceRect.x / sourceSize.x * imageRect.width;
            float y = imageRect.y + sourceRect.y / sourceSize.y * imageRect.height;
            float width = sourceRect.width / sourceSize.x * imageRect.width;
            float height = sourceRect.height / sourceSize.y * imageRect.height;
            var rawRect = new Rect(x, y, width, height);

            float xMin = Mathf.Clamp(rawRect.xMin, imageRect.xMin, imageRect.xMax);
            float xMax = Mathf.Clamp(rawRect.xMax, imageRect.xMin, imageRect.xMax);
            float yMin = Mathf.Clamp(rawRect.yMin, imageRect.yMin, imageRect.yMax);
            float yMax = Mathf.Clamp(rawRect.yMax, imageRect.yMin, imageRect.yMax);

            if (xMax <= xMin || yMax <= yMin)
            {
                boxRect = Rect.zero;
                return false;
            }

            boxRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        // ─────────────────────── 5. REPORT ───────────────────────

        /// <summary>
        /// 刷新报告区域。
        /// </summary>
        private void RefreshReport()
        {
            bool hasContent = (_conversionReport != null && _conversionReport.Warnings.Count > 0)
                             || (_previewChanges != null && _previewChanges.Count > 0)
                             || _bindingReport != null;

            if (_reportFoldout != null)
            {
                _reportFoldout.text = hasContent ? "报告 ▸ 有数据" : "报告";
            }

            // 转换警告
            if (_conversionReport != null && _conversionReport.Warnings.Count > 0)
            {
                _conversionWarningsLabel.text = "## 转换警告\n" + string.Join("\n",
                    _conversionReport.Warnings.Select(w => $"  ⚠ {w}"));
                _conversionWarningsLabel.style.display = DisplayStyle.Flex;
            }
            else if (_conversionReport != null && _conversionReport.RenamedElements.Count > 0)
            {
                _conversionWarningsLabel.text = "## 元素重命名\n" + string.Join("\n",
                    _conversionReport.RenamedElements.Select(r => $"  {r}"));
                _conversionWarningsLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _conversionWarningsLabel.style.display = DisplayStyle.None;
            }

            // 应用报告
            if (_previewChanges != null && _previewChanges.Count > 0)
            {
                int created = _previewChanges.Count(c => c.IsNew && !c.HasConflict);
                int modified = _previewChanges.Count(c => !c.IsNew && !c.HasConflict);
                int conflicts = _previewChanges.Count(c => c.HasConflict);
                _applyReportLabel.text = $"## 应用报告\n  创建: {created}  |  修改: {modified}  |  冲突: {conflicts}";
                _applyReportLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _applyReportLabel.style.display = DisplayStyle.None;
            }

            // 绑定报告
            if (_bindingReport != null)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("## 绑定报告");
                sb.AppendLine($"  新增绑定: {_bindingReport.AddedEntries.Count}");
                sb.AppendLine($"  跳过: {_bindingReport.SkippedEntries.Count}");
                sb.AppendLine($"  冲突: {_bindingReport.ConflictEntries.Count}");
                if (_bindingReport.MissingComponentWarnings.Count > 0)
                {
                    sb.AppendLine("  缺少组件警告:");
                    foreach (var w in _bindingReport.MissingComponentWarnings)
                        sb.AppendLine($"    - {w}");
                }
                _bindingReportLabel.text = sb.ToString();
                _bindingReportLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _bindingReportLabel.style.display = DisplayStyle.None;
            }

            // 空状态
            if (_reportEmptyHelpBox != null)
            {
                _reportEmptyHelpBox.style.display = hasContent ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        // ─────────────────────── 功能方法 ───────────────────────

        /// <summary>
        /// 加载 prefab 到可编辑实例，并创建或加载对应的 metadata。
        /// </summary>
        private void LoadPrefab()
        {
            if (_prefabAsset == null) return;

            var prefabPath = AssetDatabase.GetAssetPath(_prefabAsset);
            if (string.IsNullOrEmpty(prefabPath)) return;

            UnloadEditableInstance();
            _editableInstance = PrefabUtility.LoadPrefabContents(prefabPath);
            AutoReadCanvasSize();

            _metadata = DraftWorkbenchMetadata.CreateOrLoad(prefabPath);
            if (_metadata != null)
            {
                _overlaySettings = _metadata.OverlaySettings ?? new DraftOverlaySettings();
                _fitModeIndex = (int)_overlaySettings.FitMode;

                var metaPath = _metadata.GetMetadataAssetPath();
                if (!string.IsNullOrEmpty(metaPath))
                {
                    EditorPrefs.SetString("DraftWorkbench_MetadataPath", metaPath);
                }

                if (!string.IsNullOrEmpty(_metadata.SourceImageAssetGuid) && _draftImage == null)
                {
                    var imagePath = AssetDatabase.GUIDToAssetPath(_metadata.SourceImageAssetGuid);
                    _draftImage = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
                }
            }

            _suppressPrefabChangeCallback = true;
            _canvasWidthField.value = _canvasWidth;
            _canvasHeightField.value = _canvasHeight;
            _overlayOpacitySlider.value = _overlaySettings.Opacity;
            if (_fitModePopup != null)
            {
                _fitModePopup.index = _fitModeIndex;
                _fitModePopup.value = FitModeNames[_fitModeIndex];
            }
            if (_designImageField != null) _designImageField.value = _draftImage;
            _suppressPrefabChangeCallback = false;

            SyncUiFromState();
        }

        /// <summary>
        /// 自动从可编辑实例中读取 CanvasScaler 的参考分辨率。
        /// </summary>
        private void AutoReadCanvasSize()
        {
            if (_editableInstance == null) return;
            var scaler = _editableInstance.GetComponentInChildren<UnityEngine.UI.CanvasScaler>();
            if (scaler != null)
            {
                var res = scaler.referenceResolution;
                if (res.x > 0 && res.y > 0)
                {
                    _canvasWidth = Mathf.RoundToInt(res.x);
                    _canvasHeight = Mathf.RoundToInt(res.y);
                }
            }
        }

        /// <summary>
        /// 释放当前可编辑 prefab 实例。
        /// </summary>
        private void UnloadEditableInstance()
        {
            if (_editableInstance != null)
            {
                DraftOverlayService.RemoveOverlay(_editableInstance);
                PrefabUtility.UnloadPrefabContents(_editableInstance);
                _editableInstance = null;
            }
        }

        /// <summary>
        /// 应用全部变更到 Prefab。
        /// </summary>
        private void ApplyToPrefab()
        {
            if (_editableInstance == null)
            {
                Debug.LogWarning("[DraftWorkbench] 请先选择 Target Prefab。");
                return;
            }

            if (_previewChanges == null || _previewChanges.Count == 0)
            {
                Debug.Log("[DraftWorkbench] 没有需要应用的变更。");
                return;
            }

            DraftOverlayService.RemoveOverlay(_editableInstance);

            var record = new DraftApplyRecord
            {
                RecordGuid = Guid.NewGuid().ToString(),
                AppliedAt = DateTime.Now,
                CreatedObjectPaths = new List<string>(),
                ModifiedRects = new List<RectChangeRecord>(),
                AddedCollectorKeys = new List<string>(),
                AddedScriptFields = new List<string>(),
            };

            foreach (var change in _previewChanges)
            {
                if (change.Descriptor == null || change.HasConflict) continue;
                if (change.IsNew) continue;

                string objectPath = PrefabDraftBuilder.GetResolvedDescriptorPath(_editableInstance, change.Descriptor);
                if (string.IsNullOrEmpty(objectPath)) continue;

                var existing = _editableInstance.transform.Find(objectPath) as RectTransform;
                if (existing == null) continue;

                record.ModifiedRects.Add(new RectChangeRecord
                {
                    ObjectPath = objectPath,
                    OldAnchoredPosition = existing.anchoredPosition,
                    NewAnchoredPosition = change.Descriptor.AnchoredPosition,
                    OldSizeDelta = existing.sizeDelta,
                    NewSizeDelta = change.Descriptor.SizeDelta,
                    OldAnchorMin = existing.anchorMin,
                    NewAnchorMin = change.Descriptor.AnchorMin,
                    OldAnchorMax = existing.anchorMax,
                    NewAnchorMax = change.Descriptor.AnchorMax,
                    HasPivot = true,
                    OldPivot = existing.pivot,
                    NewPivot = new Vector2(0.5f, 0.5f),
                });
            }

            record.SpriteChanges = DraftSpriteApplyService.BuildSpriteChangeRecords(_editableInstance, _previewChanges);

            var appliedPaths = PrefabDraftBuilder.ApplyChanges(_editableInstance, _previewChanges);
            record.CreatedObjectPaths = appliedPaths
                .Where(path => _previewChanges.Any(change =>
                    change.IsNew && !change.HasConflict && change.Descriptor != null &&
                    PrefabDraftBuilder.GetResolvedDescriptorPath(_editableInstance, change.Descriptor) == path))
                .ToList();

            try
            {
                var collector = _editableInstance.GetComponent<ReferenceCollector>();
                if (collector != null)
                {
                    var nodes = new List<GameObject>();
                    foreach (var path in appliedPaths)
                    {
                        var t = _editableInstance.transform.Find(path);
                        if (t != null) nodes.Add(t.gameObject);
                    }

                    _bindingReport = DraftBindingService.BuildBindingPlan(collector, nodes);
                    DraftBindingService.ApplyBindings(collector, _bindingReport, nodes);

                    record.AddedCollectorKeys = _bindingReport.AddedEntries.Select(e => e.Key).ToList();
                    record.AddedScriptFields = _bindingReport.AddedScriptFields;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DraftWorkbench] 绑定过程出错: {ex.Message}");
            }

            if (_metadata != null)
            {
                _metadata.AddApplyRecord(record);
                _metadata.LastBindingReport = _bindingReport;
                EditorUtility.SetDirty(_metadata);
                AssetDatabase.SaveAssets();
            }

            var prefabPath = AssetDatabase.GetAssetPath(_prefabAsset);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                PrefabUtility.SaveAsPrefabAsset(_editableInstance, prefabPath);
                TryAppendAppliedMaintenanceRecord(prefabPath, record, _bindingReport);
                Debug.Log($"[DraftWorkbench] Prefab 已保存: {prefabPath}");
            }

            Debug.Log($"[DraftWorkbench] 应用完成：{appliedPaths.Count} 个节点变更。");
            SyncUiFromState();
        }

        /// <summary>
        /// 回滚上一次应用操作。
        /// </summary>
        private void RevertLast()
        {
            if (_editableInstance == null)
            {
                Debug.LogWarning("[DraftWorkbench] 请先选择 Target Prefab。");
                return;
            }

            if (_metadata == null)
            {
                Debug.LogWarning("[DraftWorkbench] 没有可回滚的记录。");
                return;
            }

            var record = _metadata.GetLatestApplyRecord();
            if (record == null)
            {
                Debug.LogWarning("[DraftWorkbench] 没有可回滚的记录。");
                return;
            }

            bool success = PrefabDraftBuilder.RevertChanges(_editableInstance, record);
            if (success)
            {
                _metadata.ClearLatestApplyRecord();
                EditorUtility.SetDirty(_metadata);
                AssetDatabase.SaveAssets();

                var prefabPath = AssetDatabase.GetAssetPath(_prefabAsset);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    PrefabUtility.SaveAsPrefabAsset(_editableInstance, prefabPath);
                    TryAppendRevertedMaintenanceRecord(prefabPath, record);
                }

                _previewChanges.Clear();
                _bindingReport = null;
                Debug.Log("[DraftWorkbench] 回滚成功。");
            }
            else
            {
                Debug.LogError("[DraftWorkbench] 回滚失败，存在冲突。请检查 Console 中的警告信息。");
            }

            SyncUiFromState();
        }
    }
}
#endif
