using System;
using GT;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[CustomEditor(typeof(ReferenceCollector))]
public class ReferenceCollectorEditor : Editor
{
    private ReferenceCollector referenceCollector;
    private SerializedProperty dataProperty;
    private VisualElement referenceList;
    private ObjectField searchObjectField;
    private string searchKey = string.Empty;

    /// <summary>
    /// 初始化当前 Inspector 目标。
    /// </summary>
    private void OnEnable()
    {
        referenceCollector = (ReferenceCollector)target;
    }

    /// <summary>
    /// 创建 ReferenceCollector 的 UIElements Inspector。
    /// </summary>
    public override VisualElement CreateInspectorGUI()
    {
        serializedObject.Update();
        dataProperty = serializedObject.FindProperty("data");

        var root = new VisualElement();
        root.style.paddingLeft = 6;
        root.style.paddingRight = 6;
        root.style.paddingTop = 6;
        root.style.paddingBottom = 6;

        BuildMainOperations(root);
        BuildAutoCollectOperations(root);
        BuildRuleSummary(root);
        BuildSearchDelete(root);
        BuildReferenceList(root);

        return root;
    }

    /// <summary>
    /// 构建手动引用操作按钮。
    /// </summary>
    private void BuildMainOperations(VisualElement root)
    {
        var row = CreateRow();
        row.Add(CreateButton("添加引用", () =>
        {
            AddReference(Guid.NewGuid().GetHashCode().ToString(), null);
            RefreshReferenceList();
        }));
        row.Add(CreateButton("全部删除", () =>
        {
            Undo.RecordObject(referenceCollector, "清空 ReferenceCollector 引用");
            referenceCollector.Clear();
            serializedObject.Update();
            RefreshReferenceList();
        }));
        row.Add(CreateButton("删除空引用", () =>
        {
            DeleteNullReferences();
            RefreshReferenceList();
        }));
        row.Add(CreateButton("排序", () =>
        {
            Undo.RecordObject(referenceCollector, "排序 ReferenceCollector 引用");
            referenceCollector.Sort();
            serializedObject.Update();
            RefreshReferenceList();
        }));
        root.Add(row);
    }

    /// <summary>
    /// 构建自动收集操作区。
    /// </summary>
    private void BuildAutoCollectOperations(VisualElement root)
    {
        root.Add(CreateTitle("自动收集（基于项目规则）"));

        var row = CreateRow();
        row.Add(CreateButton("自动收集", () =>
        {
            Undo.RecordObject(referenceCollector, "自动收集 ReferenceCollector 引用");
            referenceCollector.AutoCollectByNamingRules();
            serializedObject.Update();
            RefreshReferenceList();
        }));
        row.Add(CreateButton("清除自动收集", () =>
        {
            if (!EditorUtility.DisplayDialog("确认清除", "确定要清除所有符合项目规则的自动收集引用吗？", "确认", "取消"))
            {
                return;
            }

            Undo.RecordObject(referenceCollector, "清除自动收集 ReferenceCollector 引用");
            referenceCollector.ClearAutoCollected();
            serializedObject.Update();
            RefreshReferenceList();
        }));
        root.Add(row);
    }

    /// <summary>
    /// 构建只读项目规则摘要。
    /// </summary>
    private void BuildRuleSummary(VisualElement root)
    {
        var container = new VisualElement();
        container.style.marginTop = 8;
        container.style.marginBottom = 8;
        container.style.paddingLeft = 6;
        container.style.paddingRight = 6;
        container.style.paddingTop = 6;
        container.style.paddingBottom = 6;
        container.style.borderTopWidth = 1;
        container.style.borderBottomWidth = 1;
        container.style.borderLeftWidth = 1;
        container.style.borderRightWidth = 1;
        container.style.borderTopColor = Color.gray;
        container.style.borderBottomColor = Color.gray;
        container.style.borderLeftColor = Color.gray;
        container.style.borderRightColor = Color.gray;

        container.Add(CreateTitle("项目收集规则（只读）"));
        foreach (var line in ReferenceCollectorRuleService.BuildRuleSummaryLines())
        {
            var label = new Label(line);
            label.style.whiteSpace = WhiteSpace.Normal;
            container.Add(label);
        }

        root.Add(container);
    }

    /// <summary>
    /// 构建按 key 搜索删除区域。
    /// </summary>
    private void BuildSearchDelete(VisualElement root)
    {
        var row = CreateRow();
        var searchField = new TextField();
        searchField.style.flexGrow = 1;
        searchField.RegisterValueChangedCallback(evt =>
        {
            searchKey = evt.newValue ?? string.Empty;
            searchObjectField.value = FindReferenceByKey(searchKey);
        });
        row.Add(searchField);

        searchObjectField = new ObjectField();
        searchObjectField.objectType = typeof(Object);
        searchObjectField.allowSceneObjects = true;
        searchObjectField.SetEnabled(false);
        searchObjectField.style.flexGrow = 1;
        row.Add(searchObjectField);

        row.Add(CreateButton("删除", () =>
        {
            referenceCollector.Remove(searchKey);
            serializedObject.Update();
            searchObjectField.value = null;
            RefreshReferenceList();
        }));
        root.Add(row);
    }

    /// <summary>
    /// 构建引用列表与拖拽区域。
    /// </summary>
    private void BuildReferenceList(VisualElement root)
    {
        root.Add(CreateTitle("引用列表"));
        referenceList = new VisualElement();
        referenceList.style.minHeight = 80;
        referenceList.style.paddingTop = 4;
        referenceList.style.paddingBottom = 4;
        referenceList.style.borderTopWidth = 1;
        referenceList.style.borderBottomWidth = 1;
        referenceList.style.borderLeftWidth = 1;
        referenceList.style.borderRightWidth = 1;
        referenceList.style.borderTopColor = Color.gray;
        referenceList.style.borderBottomColor = Color.gray;
        referenceList.style.borderLeftColor = Color.gray;
        referenceList.style.borderRightColor = Color.gray;
        referenceList.RegisterCallback<DragUpdatedEvent>(HandleDragUpdated);
        referenceList.RegisterCallback<DragPerformEvent>(HandleDragPerform);
        root.Add(referenceList);
        RefreshReferenceList();
    }

    /// <summary>
    /// 刷新引用列表显示。
    /// </summary>
    private void RefreshReferenceList()
    {
        if (referenceList == null)
        {
            return;
        }

        serializedObject.Update();
        dataProperty = serializedObject.FindProperty("data");
        referenceList.Clear();

        for (int i = dataProperty.arraySize - 1; i >= 0; i--)
        {
            AddReferenceRow(i);
        }

        if (dataProperty.arraySize == 0)
        {
            var emptyLabel = new Label("暂无引用，可拖拽对象到此区域添加。");
            emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            emptyLabel.style.marginTop = 16;
            referenceList.Add(emptyLabel);
        }
    }

    /// <summary>
    /// 添加单条引用编辑行。
    /// </summary>
    private void AddReferenceRow(int index)
    {
        var row = CreateRow();
        var element = dataProperty.GetArrayElementAtIndex(index);
        var keyProperty = element.FindPropertyRelative("key");
        var objectProperty = element.FindPropertyRelative("gameObject");

        var keyField = new TextField();
        keyField.value = keyProperty.stringValue;
        keyField.style.width = 160;
        keyField.RegisterValueChangedCallback(evt =>
        {
            serializedObject.Update();
            var property = serializedObject.FindProperty("data").GetArrayElementAtIndex(index).FindPropertyRelative("key");
            property.stringValue = evt.newValue;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(referenceCollector);
        });
        row.Add(keyField);

        var objectField = new ObjectField();
        objectField.objectType = typeof(Object);
        objectField.allowSceneObjects = true;
        objectField.value = objectProperty.objectReferenceValue;
        objectField.style.flexGrow = 1;
        objectField.RegisterValueChangedCallback(evt =>
        {
            serializedObject.Update();
            var property = serializedObject.FindProperty("data").GetArrayElementAtIndex(index).FindPropertyRelative("gameObject");
            property.objectReferenceValue = evt.newValue;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(referenceCollector);
        });
        row.Add(objectField);

        row.Add(CreateButton("X", () =>
        {
            DeleteReferenceAt(index);
            RefreshReferenceList();
        }));
        referenceList.Add(row);
    }

    /// <summary>
    /// 处理拖拽更新事件。
    /// </summary>
    private void HandleDragUpdated(DragUpdatedEvent evt)
    {
        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        evt.StopPropagation();
    }

    /// <summary>
    /// 处理拖拽添加引用事件。
    /// </summary>
    private void HandleDragPerform(DragPerformEvent evt)
    {
        DragAndDrop.AcceptDrag();
        foreach (var draggedObject in DragAndDrop.objectReferences)
        {
            if (draggedObject == null)
            {
                continue;
            }

            AddReference(draggedObject.name, draggedObject);
        }

        RefreshReferenceList();
        evt.StopPropagation();
    }

    /// <summary>
    /// 添加一条引用数据。
    /// </summary>
    private void AddReference(string key, Object obj)
    {
        serializedObject.Update();
        dataProperty = serializedObject.FindProperty("data");
        int index = dataProperty.arraySize;
        dataProperty.InsertArrayElementAtIndex(index);
        var element = dataProperty.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("key").stringValue = key;
        element.FindPropertyRelative("gameObject").objectReferenceValue = obj;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(referenceCollector);
    }

    /// <summary>
    /// 删除指定索引的引用数据。
    /// </summary>
    private void DeleteReferenceAt(int index)
    {
        serializedObject.Update();
        dataProperty = serializedObject.FindProperty("data");
        if (index < 0 || index >= dataProperty.arraySize)
        {
            return;
        }

        dataProperty.DeleteArrayElementAtIndex(index);
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(referenceCollector);
    }

    /// <summary>
    /// 删除所有空引用数据。
    /// </summary>
    private void DeleteNullReferences()
    {
        serializedObject.Update();
        dataProperty = serializedObject.FindProperty("data");
        for (int i = dataProperty.arraySize - 1; i >= 0; i--)
        {
            var gameObjectProperty = dataProperty.GetArrayElementAtIndex(i).FindPropertyRelative("gameObject");
            if (gameObjectProperty.objectReferenceValue != null)
            {
                continue;
            }

            dataProperty.DeleteArrayElementAtIndex(i);
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(referenceCollector);
    }

    /// <summary>
    /// 按 key 查找当前引用对象。
    /// </summary>
    private Object FindReferenceByKey(string key)
    {
        if (string.IsNullOrEmpty(key) || referenceCollector.data == null)
        {
            return null;
        }

        foreach (var item in referenceCollector.data)
        {
            if (item != null && item.key == key)
            {
                return item.gameObject;
            }
        }

        return null;
    }

    /// <summary>
    /// 创建横向布局容器。
    /// </summary>
    private VisualElement CreateRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.marginBottom = 4;
        return row;
    }

    /// <summary>
    /// 创建通用按钮。
    /// </summary>
    private Button CreateButton(string text, Action clicked)
    {
        var button = new Button(clicked)
        {
            text = text
        };
        button.style.marginRight = 4;
        return button;
    }

    /// <summary>
    /// 创建分区标题。
    /// </summary>
    private Label CreateTitle(string text)
    {
        var label = new Label(text);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginTop = 6;
        label.style.marginBottom = 4;
        return label;
    }
}
