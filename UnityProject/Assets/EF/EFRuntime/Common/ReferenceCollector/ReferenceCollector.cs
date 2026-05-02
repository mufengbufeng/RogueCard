using System;
using System.Collections.Generic;
using UnityEngine;
//Object并非C#基础中的Object，而是 UnityEngine.Object
using Object = UnityEngine.Object;

//使其能在Inspector面板显示，并且可以被赋予相应值
[Serializable]
public class ReferenceCollectorData
{
	public string key;
	//Object并非C#基础中的Object，而是 UnityEngine.Object
	public Object gameObject;
}
//继承IComparer对比器，Ordinal会使用序号排序规则比较字符串，因为是byte级别的比较，所以准确性和性能都不错
public class ReferenceCollectorDataComparer : IComparer<ReferenceCollectorData>
{
	public int Compare(ReferenceCollectorData x, ReferenceCollectorData y)
	{
		return string.Compare(x.key, y.key, StringComparison.Ordinal);
	}
}

//继承ISerializationCallbackReceiver后会增加OnAfterDeserialize和OnBeforeSerialize两个回调函数，如果有需要可以在对需要序列化的东西进行操作
//ET在这里主要是在OnAfterDeserialize回调函数中将data中存储的ReferenceCollectorData转换为dict中的Object，方便之后的使用
//注意UNITY_EDITOR宏定义，在编译以后，部分编辑器相关函数并不存在
public class ReferenceCollector : MonoBehaviour, ISerializationCallbackReceiver
{
	//用于序列化的List
	public List<ReferenceCollectorData> data = new List<ReferenceCollectorData>();
	//Object并非C#基础中的Object，而是 UnityEngine.Object
	private readonly Dictionary<string, Object> dict = new Dictionary<string, Object>();

#if UNITY_EDITOR
	//添加新的元素
	public void Add(string key, Object obj)
	{
		UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(this);
		//根据PropertyPath读取数据
		//如果不知道具体的格式，可以右键用文本编辑器打开一个prefab文件（如Bundles/UI目录中的几个）
		//因为这几个prefab挂载了ReferenceCollector，所以搜索data就能找到存储的数据
		UnityEditor.SerializedProperty dataProperty = serializedObject.FindProperty("data");
		int i;
		//遍历data，看添加的数据是否存在相同key
		for (i = 0; i < data.Count; i++)
		{
			if (data[i].key == key)
			{
				break;
			}
		}
		//不等于data.Count意为已经存在于data List中，直接赋值即可
		if (i != data.Count)
		{
			//根据i的值获取dataProperty，也就是data中的对应ReferenceCollectorData，不过在这里，是对Property进行的读取，有点类似json或者xml的节点
			UnityEditor.SerializedProperty element = dataProperty.GetArrayElementAtIndex(i);
			//对对应节点进行赋值，值为gameobject相对应的fileID
			//fileID独一无二，单对单关系，其他挂载在这个gameobject上的script或组件会保存相对应的fileID
			element.FindPropertyRelative("gameObject").objectReferenceValue = obj;
		}
		else
		{
			//等于则说明key在data中无对应元素，所以得向其插入新的元素
			dataProperty.InsertArrayElementAtIndex(i);
			UnityEditor.SerializedProperty element = dataProperty.GetArrayElementAtIndex(i);
			element.FindPropertyRelative("key").stringValue = key;
			element.FindPropertyRelative("gameObject").objectReferenceValue = obj;
		}
		//应用与更新
		UnityEditor.EditorUtility.SetDirty(this);
		serializedObject.ApplyModifiedProperties();
		serializedObject.UpdateIfRequiredOrScript();
	}

	/// <summary>
	/// 基于命名规范自动收集组件
	/// 遵循 UHub 命名规范：Button 后缀为 "Btn"，Text 后缀为 "Text" 等
	/// </summary>
	public int AutoCollectByNamingRules()
	{
		int collectedCount = 0;
		var allChildren = GetComponentsInChildren<Transform>(true);

		foreach (var childTransform in allChildren)
		{
			if (childTransform == this.transform) continue; // 跳过自己

			var childName = childTransform.name;
			if (string.IsNullOrEmpty(childName)) continue;

			// 检查是否符合命名规范
			if (ShouldCollectByName(childName))
			{
				var targetComponent = GetTargetComponent(childTransform);
				if (targetComponent != null)
				{
					// 检查是否已存在
					bool exists = false;
					foreach (var existingData in data)
					{
						if (existingData.key == childName)
						{
							exists = true;
							break;
						}
					}

					if (!exists)
					{
						Add(childName, targetComponent);
						collectedCount++;
						UnityEngine.Debug.Log($"[ReferenceCollector] 自动收集: {childName} -> {targetComponent.GetType().Name}");
					}
				}
			}
		}

		if (collectedCount > 0)
		{
			Sort(); // 自动排序
			UnityEngine.Debug.Log($"[ReferenceCollector] 自动收集完成，共收集 {collectedCount} 个组件");
		}
		else
		{
			UnityEngine.Debug.Log("[ReferenceCollector] 未找到符合命名规范的组件");
		}

		return collectedCount;
	}

	/// <summary>
	/// 判断是否应该根据名称收集此组件
	/// 基于 UHub 命名规范的后缀匹配
	/// </summary>
	private bool ShouldCollectByName(string objectName)
	{
		if (string.IsNullOrEmpty(objectName)) return false;

		// 定义 UHub 支持的后缀
		string[] supportedSuffixes =
		{
			"Btn", "Button",           // Button 组件
			"Text", "Label",           // Text 组件  
			"Img",            			// Image 组件
			"Slider",                  // Slider 组件
			"Toggle",                  // Toggle 组件
			"Input", "InputField",     // InputField 组件
			"Dropdown",                // Dropdown 组件
			"Go", "Obj", // GameObject
			"SpriteRenderer"    // SpriteRenderer 组件
		};

		foreach (var suffix in supportedSuffixes)
		{
			if (objectName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// 根据命名获取目标组件
	/// 优先获取具体组件，如果没有则返回 GameObject
	/// </summary>
	private Object GetTargetComponent(Transform transform)
	{
		var objectName = transform.name;

		// 根据后缀推断组件类型
		if (objectName.EndsWith("Btn", StringComparison.OrdinalIgnoreCase) ||
			objectName.EndsWith("Button", StringComparison.OrdinalIgnoreCase))
		{
			var button = transform.GetComponent<UnityEngine.UI.Button>();
			if (button != null) return button;
		}
		else if (objectName.EndsWith("Text", StringComparison.OrdinalIgnoreCase) ||
				 objectName.EndsWith("Label", StringComparison.OrdinalIgnoreCase))
		{
			var text = transform.GetComponent<UnityEngine.UI.Text>();
			if (text != null) return text;
		}
		else if (objectName.EndsWith("Img", StringComparison.OrdinalIgnoreCase) ||
				 objectName.EndsWith("Image", StringComparison.OrdinalIgnoreCase))
		{
			var image = transform.GetComponent<UnityEngine.UI.Image>();
			if (image != null) return image;
		}
		else if (objectName.EndsWith("Slider", StringComparison.OrdinalIgnoreCase))
		{
			var slider = transform.GetComponent<UnityEngine.UI.Slider>();
			if (slider != null) return slider;
		}
		else if (objectName.EndsWith("Toggle", StringComparison.OrdinalIgnoreCase))
		{
			var toggle = transform.GetComponent<UnityEngine.UI.Toggle>();
			if (toggle != null) return toggle;
		}
		else if (objectName.EndsWith("Input", StringComparison.OrdinalIgnoreCase) ||
				 objectName.EndsWith("InputField", StringComparison.OrdinalIgnoreCase))
		{
			var inputField = transform.GetComponent<UnityEngine.UI.InputField>();
			if (inputField != null) return inputField;
		}
		else if (objectName.EndsWith("Dropdown", StringComparison.OrdinalIgnoreCase))
		{
			var dropdown = transform.GetComponent<UnityEngine.UI.Dropdown>();
			if (dropdown != null) return dropdown;
		}else if (objectName.EndsWith("SpriteRenderer", StringComparison.OrdinalIgnoreCase))
		{
			var spriteRenderer = transform.GetComponent<SpriteRenderer>();
			if (spriteRenderer != null) return spriteRenderer;
		}

		// 如果没有找到特定组件，返回 GameObject
		return transform.gameObject;
	}

	/// <summary>
	/// 清除所有自动收集的组件（保留手动添加的）
	/// 基于命名规范判断是否为自动收集的组件
	/// </summary>
	public int ClearAutoCollected()
	{
		var toRemove = new List<string>();

		foreach (var item in data)
		{
			if (ShouldCollectByName(item.key))
			{
				toRemove.Add(item.key);
			}
		}

		foreach (var key in toRemove)
		{
			Remove(key);
		}

		if (toRemove.Count > 0)
		{
			UnityEngine.Debug.Log($"[ReferenceCollector] 清除自动收集的组件，共清除 {toRemove.Count} 个");
		}

		return toRemove.Count;
	}
	//删除元素，知识点与上面的添加相似
	public void Remove(string key)
	{
		UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(this);
		UnityEditor.SerializedProperty dataProperty = serializedObject.FindProperty("data");
		int i;
		for (i = 0; i < data.Count; i++)
		{
			if (data[i].key == key)
			{
				break;
			}
		}
		if (i != data.Count)
		{
			dataProperty.DeleteArrayElementAtIndex(i);
		}
		UnityEditor.EditorUtility.SetDirty(this);
		serializedObject.ApplyModifiedProperties();
		serializedObject.UpdateIfRequiredOrScript();
	}

	public void Clear()
	{
		UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(this);
		//根据PropertyPath读取prefab文件中的数据
		//如果不知道具体的格式，可以直接右键用文本编辑器打开，搜索data就能找到
		var dataProperty = serializedObject.FindProperty("data");
		dataProperty.ClearArray();
		UnityEditor.EditorUtility.SetDirty(this);
		serializedObject.ApplyModifiedProperties();
		serializedObject.UpdateIfRequiredOrScript();
	}

	public void Sort()
	{
		UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(this);
		data.Sort(new ReferenceCollectorDataComparer());
		UnityEditor.EditorUtility.SetDirty(this);
		serializedObject.ApplyModifiedProperties();
		serializedObject.UpdateIfRequiredOrScript();
	}
#endif
	//使用泛型返回对应key的gameobject
	public T Get<T>(string key) where T : class
	{
		Object dictGo;
		if (!dict.TryGetValue(key, out dictGo))
		{
			return null;
		}
		if (typeof(T) == typeof(GameObject))
		{
			return dictGo as T;
		}
		else
		{
			//如果T不是GameObject，则尝试将其转换为T类型
			if (dictGo is GameObject go)
			{
				return go.GetComponent<T>();
			}
			else if (dictGo is Component component)
			{
				return component as T;
			}
		}

		return dictGo as T;
	}

	public Object GetObject(string key)
	{
		Object dictGo;
		if (!dict.TryGetValue(key, out dictGo))
		{
			return null;
		}
		return dictGo;
	}

	public void OnBeforeSerialize()
	{
	}
	//在反序列化后运行
	public void OnAfterDeserialize()
	{
		dict.Clear();
		foreach (ReferenceCollectorData referenceCollectorData in data)
		{
			if (!dict.ContainsKey(referenceCollectorData.key))
			{
				dict.Add(referenceCollectorData.key, referenceCollectorData.gameObject);
			}
		}
	}
}
