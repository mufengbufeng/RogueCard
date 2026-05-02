using System;
using System.IO;
using EF.Debugger;
using UnityEngine;

namespace EF.Save
{
    /// <summary>
    /// Json 文件保存策略实现。
    /// </summary>
    public class JsonSaveStrategy : ISaveStrategy
    {
        private readonly string _savePath;

        public JsonSaveStrategy()
        {
            _savePath = Path.Combine(Application.persistentDataPath, "SaveData");
            if (!Directory.Exists(_savePath))
            {
                Directory.CreateDirectory(_savePath);
            }
        }

        public bool Save<T>(string key, T data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                string filePath = GetFilePath(key);
                File.WriteAllText(filePath, json);
                Log.Info($"[JsonSaveStrategy] 保存成功: {key}");
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"[JsonSaveStrategy] 保存失败: {key}, 错误: {e.Message}");
                return false;
            }
        }

        public T Load<T>(string key, T defaultValue = default)
        {
            try
            {
                string filePath = GetFilePath(key);
                if (!File.Exists(filePath))
                {
                    Log.Warning($"[JsonSaveStrategy] 文件不存在: {key}");
                    return defaultValue;
                }

                string json = File.ReadAllText(filePath);
                T data = JsonUtility.FromJson<T>(json);
                Log.Info($"[JsonSaveStrategy] 加载成功: {key}");
                return data;
            }
            catch (Exception e)
            {
                Log.Error($"[JsonSaveStrategy] 加载失败: {key}, 错误: {e.Message}");
                return defaultValue;
            }
        }

        public bool HasKey(string key)
        {
            string filePath = GetFilePath(key);
            return File.Exists(filePath);
        }

        public bool Delete(string key)
        {
            try
            {
                string filePath = GetFilePath(key);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Log.Info($"[JsonSaveStrategy] 删除成功: {key}");
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                Log.Error($"[JsonSaveStrategy] 删除失败: {key}, 错误: {e.Message}");
                return false;
            }
        }

        public void DeleteAll()
        {
            try
            {
                if (Directory.Exists(_savePath))
                {
                    Directory.Delete(_savePath, true);
                    Directory.CreateDirectory(_savePath);
                    Log.Info("[JsonSaveStrategy] 已删除所有保存数据");
                }
            }
            catch (Exception e)
            {
                Log.Error($"[JsonSaveStrategy] 删除所有数据失败, 错误: {e.Message}");
            }
        }

        private string GetFilePath(string key)
        {
            return Path.Combine(_savePath, $"{key}.json");
        }
    }
}
