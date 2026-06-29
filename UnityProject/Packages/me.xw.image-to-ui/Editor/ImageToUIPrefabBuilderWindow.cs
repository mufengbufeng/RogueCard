using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ImageToUI.PrefabBuilder
{
    public sealed class ImageToUIPrefabBuilderWindow : EditorWindow
    {
        private TextAsset structureJson;
        private string jsonPath = "";

        [MenuItem("Tools/Image To UI/Generate Prefab")]
        public static void ShowWindow()
        {
            var window = GetWindow<ImageToUIPrefabBuilderWindow>();
            window.titleContent = new GUIContent("Image To UI");
            window.minSize = new Vector2(500f, 220f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Image To UI Prefab Builder", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var newJson = (TextAsset)EditorGUILayout.ObjectField(
                    "ui_structure.json",
                    structureJson,
                    typeof(TextAsset),
                    false
                );
                if (newJson != structureJson)
                {
                    structureJson = newJson;
                    jsonPath = structureJson != null ? AssetDatabase.GetAssetPath(structureJson) : "";
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    jsonPath = EditorGUILayout.TextField("JSON Path", jsonPath);
                    if (GUILayout.Button("Browse...", GUILayout.Width(90f)))
                    {
                        var selected = EditorUtility.OpenFilePanel(
                            "Select ui_structure.json",
                            Application.dataPath,
                            "json"
                        );
                        if (!string.IsNullOrEmpty(selected))
                        {
                            structureJson = null;
                            jsonPath = selected;
                        }
                    }
                }

                DrawPreview();
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(jsonPath)))
            {
                if (GUILayout.Button("Generate Prefab", GUILayout.Height(32f)))
                {
                    Generate();
                }
            }
        }

        private void DrawPreview()
        {
            if (string.IsNullOrEmpty(jsonPath))
            {
                return;
            }
            if (!File.Exists(jsonPath))
            {
                EditorGUILayout.HelpBox("ui_structure.json not found.", MessageType.Warning);
                return;
            }

            try
            {
                var document = UiStructureDocument.FromJson(File.ReadAllText(jsonPath));
                var output = !string.IsNullOrEmpty(document.Unity.OutputPrefabPath)
                    ? document.Unity.OutputPrefabPath
                    : ImageToUIPrefabBuilder.GetDefaultOutputPrefabPath(document.CanvasName);
                var spriteRoot = !string.IsNullOrEmpty(document.Unity.SpriteRootFolder)
                    ? document.Unity.SpriteRootFolder
                    : "(empty)";

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Output Prefab", output);
                EditorGUILayout.LabelField("Sprite Root Folder", spriteRoot);
                EditorGUILayout.LabelField("Canvas", document.CanvasWidth + " x " + document.CanvasHeight);
            }
            catch (System.Exception ex)
            {
                EditorGUILayout.HelpBox("Could not parse JSON: " + ex.Message, MessageType.Error);
            }
        }

        private void Generate()
        {
            var report = ImageToUIPrefabBuilder.GeneratePrefabFromJson(jsonPath);
            LogReport(report);
            ShowReportDialog(report);
            if (report.HasErrors)
            {
                return;
            }

            if (!string.IsNullOrEmpty(report.OutputPrefabPath))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(report.OutputPrefabPath);
            }
        }

        private static void LogReport(PrefabBuildReport report)
        {
            foreach (var error in report.Errors)
            {
                Debug.LogError("Image To UI: " + error);
            }
            foreach (var warning in report.Warnings)
            {
                Debug.LogWarning("Image To UI: " + warning);
            }
            if (!report.HasErrors)
            {
                Debug.Log(
                    "Image To UI prefab generated: "
                    + report.OutputPrefabPath
                    + ". Nodes: "
                    + report.NodesCreated
                    + ", images: "
                    + report.ImagesCreated
                    + ", texts: "
                    + report.TextsCreated
                    + ", buttons: "
                    + report.ButtonsCreated
                    + ", warnings: "
                    + report.Warnings.Count
                );
            }
        }

        private static void ShowReportDialog(PrefabBuildReport report)
        {
            var title = report.HasErrors
                ? "Prefab Generation Failed"
                : report.Warnings.Count > 0
                    ? "Prefab Generated With Warnings"
                    : "Prefab Generated";

            var message = new StringBuilder();
            if (!string.IsNullOrEmpty(report.OutputPrefabPath))
            {
                message.AppendLine("Path: " + report.OutputPrefabPath);
                message.AppendLine();
            }

            message.AppendLine("Nodes: " + report.NodesCreated);
            message.AppendLine("Images: " + report.ImagesCreated);
            message.AppendLine("Texts: " + report.TextsCreated);
            message.AppendLine("Buttons: " + report.ButtonsCreated);
            message.AppendLine("Errors: " + report.Errors.Count);
            message.AppendLine("Warnings: " + report.Warnings.Count);

            if (report.Errors.Count > 0 || report.Warnings.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("See Console for details.");
            }

            EditorUtility.DisplayDialog(title, message.ToString(), "OK");
        }
    }
}
