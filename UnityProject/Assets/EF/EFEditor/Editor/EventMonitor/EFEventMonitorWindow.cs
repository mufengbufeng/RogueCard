using EF.Event;
using UnityEditor;
using UnityEngine;

namespace EF.Editor.EventMonitor
{
    /// <summary>
    /// EF 事件系统监控面板，实时展示所有事件 Channel 的订阅和分发状态。
    /// </summary>
    public class EFEventMonitorWindow : EditorWindow
    {
        private const string MenuPath = "Window/EF/Event Monitor";
        private const float RefreshInterval = 0.5f;

        private Vector2 _scrollPosition;
        private int _expandedChannel = -1;
        private double _lastRefreshTime;
        private IEventChannelInfo[] _channelInfos;

        /// <summary>
        /// 通过菜单打开事件监控窗口。
        /// </summary>
        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<EFEventMonitorWindow>("EF Event Monitor");
            window.minSize = new Vector2(400, 200);
        }

        private void Update()
        {
            if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup - _lastRefreshTime > RefreshInterval)
            {
                _lastRefreshTime = EditorApplication.timeSinceStartup;
                RefreshData();
                Repaint();
            }
        }

        private void RefreshData()
        {
            _channelInfos = null;

            if (!EditorApplication.isPlaying)
                return;

            var entryType = System.Type.GetType("GameLogic.GameLogicEntry, GameLogic");
            if (entryType == null)
                return;

            var eventProp = entryType.GetProperty("Event", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (eventProp == null)
                return;

            var hubInstance = eventProp.GetValue(null);
            if (hubInstance == null)
                return;

            var method = hubInstance.GetType().GetMethod("GetAllChannelInfos");
            if (method == null)
                return;

            _channelInfos = method.Invoke(hubInstance, null) as IEventChannelInfo[];
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("事件系统在运行时初始化，请进入 Play Mode 查看。", MessageType.Info);
                return;
            }

            if (_channelInfos == null || _channelInfos.Length == 0)
            {
                EditorGUILayout.HelpBox("未找到已注册的事件 Channel。", MessageType.Warning);
                return;
            }

            DrawHeader();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            for (int i = 0; i < _channelInfos.Length; i++)
            {
                DrawChannelRow(_channelInfos[i], i);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("事件类型", EditorStyles.toolbarButton, GUILayout.MinWidth(180));
            GUILayout.Label("Handlers", EditorStyles.toolbarButton, GUILayout.Width(70));
            GUILayout.Label("Pending", EditorStyles.toolbarButton, GUILayout.Width(70));
            GUILayout.Label("状态", EditorStyles.toolbarButton, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawChannelRow(IEventChannelInfo info, int index)
        {
            bool isExpanded = _expandedChannel == index;
            string statusText = GetStatusText(info.State);
            Color statusColor = GetStatusColor(info.State);

            var style = new GUIStyle(EditorStyles.helpBox);
            if (isExpanded)
            {
                style.normal.background = EditorGUIUtility.isProSkin
                    ? MakeTex(2, 2, new Color(0.3f, 0.3f, 0.3f))
                    : MakeTex(2, 2, new Color(0.85f, 0.85f, 0.85f));
            }

            EditorGUILayout.BeginVertical(style);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(info.EventName, EditorStyles.label, GUILayout.MinWidth(180)))
            {
                _expandedChannel = isExpanded ? -1 : index;
            }

            GUILayout.Label(info.HandlerCount.ToString(), GUILayout.Width(70));
            GUILayout.Label(info.PendingCount.ToString(), GUILayout.Width(70));

            var coloredStyle = new GUIStyle(EditorStyles.label);
            coloredStyle.normal.textColor = statusColor;
            GUILayout.Label(statusText, coloredStyle, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            if (isExpanded)
            {
                DrawHandlerDetails(info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawHandlerDetails(IEventChannelInfo info)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            EditorGUILayout.BeginVertical();

            if (info.State == EventChannelLifecycleState.Uninitialized)
            {
                GUILayout.Label("  尚未创建实例", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                return;
            }

            var names = info.GetHandlerNames();
            if (names != null && names.Length > 0)
            {
                foreach (var name in names)
                {
                    GUILayout.Label($"  {name}", EditorStyles.miniLabel);
                }
            }
            else
            {
                GUILayout.Label("  无订阅者", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private static string GetStatusText(EventChannelLifecycleState state)
        {
            switch (state)
            {
                case EventChannelLifecycleState.Uninitialized:
                    return "未创建";
                case EventChannelLifecycleState.Active:
                    return "活跃";
                default:
                    return "空闲";
            }
        }

        private static Color GetStatusColor(EventChannelLifecycleState state)
        {
            switch (state)
            {
                case EventChannelLifecycleState.Uninitialized:
                    return Color.gray;
                case EventChannelLifecycleState.Active:
                    return Color.green;
                default:
                    return EditorGUIUtility.isProSkin
                        ? new Color(0.8f, 0.8f, 0.8f)
                        : new Color(0.2f, 0.2f, 0.2f);
            }
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            var result = new Texture2D(w, h);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
