using System;
using EF.Debugger;
using UnityEngine;

namespace EF.Save
{
    /// <summary>
    /// Unity PlayerPrefs 保存策略实现。
    /// </summary>
    public class PlayerPrefsSaveStrategy : ISaveStrategy
    {
        public bool Save<T>(string key, T data)
        {
            try
            {
                string json = JsonUtility.ToJson(data);
                PlayerPrefs.SetString(key, json);
                PlayerPrefs.Save();
                Log.Info($"[PlayerPrefsSaveStrategy] 保存成功: {key}");
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"[PlayerPrefsSaveStrategy] 保存失败: {key}, 错误: {e.Message}");
                return false;
            }
        }

        public T Load<T>(string key, T defaultValue = default)
        {
            try
            {
                if (!PlayerPrefs.HasKey(key))
                {
                    Log.Warning($"[PlayerPrefsSaveStrategy] 键不存在: {key}");
                    return defaultValue;
                }

                string json = PlayerPrefs.GetString(key);
                T data = JsonUtility.FromJson<T>(json);
                Log.Info($"[PlayerPrefsSaveStrategy] 加载成功: {key}");
                return data;
            }
            catch (Exception e)
            {
                Log.Error($"[PlayerPrefsSaveStrategy] 加载失败: {key}, 错误: {e.Message}");
                return defaultValue;
            }
        }

        public bool HasKey(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        public bool Delete(string key)
        {
            try
            {
                if (PlayerPrefs.HasKey(key))
                {
                    PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.Save();
                    Log.Info($"[PlayerPrefsSaveStrategy] 删除成功: {key}");
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                Log.Error($"[PlayerPrefsSaveStrategy] 删除失败: {key}, 错误: {e.Message}");
                return false;
            }
        }

        public void DeleteAll()
        {
            try
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                Log.Info("[PlayerPrefsSaveStrategy] 已删除所有保存数据");
            }
            catch (Exception e)
            {
                Log.Error($"[PlayerPrefsSaveStrategy] 删除所有数据失败, 错误: {e.Message}");
            }
        }
    }
}
