using EF.Resource;
using GameConfig;
using Luban;
using UnityEngine;
using YooAsset;

/// <summary>
/// 配置加载器。
/// </summary>
public class ConfigSystem
{
    private readonly IResourceManager _resourceManager;
    private bool _init = false;
    private Tables _tables;

    public Tables Tables
    {
        get
        {
            if (!_init)
            {
                Load();
            }

            return _tables;
        }
    }

    /// <summary>
    /// 构造函数，注入资源管理器。
    /// </summary>
    public ConfigSystem(IResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    /// <summary>
    /// 加载配置。
    /// </summary>
    public void Load()
    {
        _tables = new Tables(LoadByteBuf);
        _init = true;
    }

    /// <summary>
    /// 加载二进制配置。
    /// </summary>
    /// <param name="file">FileName</param>
    /// <returns>ByteBuf</returns>
    private ByteBuf LoadByteBuf(string file)
    {
        // 使用 EF 框架的 ResourceManager 加载配置文件
        AssetHandle handle = _resourceManager.LoadAssetSync<TextAsset>(file);
        TextAsset textAsset = handle.AssetObject as TextAsset;
        if (textAsset != null)
        {
            byte[] bytes = textAsset.bytes;
            return new ByteBuf(bytes);
        }

        Debug.LogError($"加载配置文件失败: {file}");
        return null;
    }

    /// <summary>
    /// 释放配置资源。
    /// </summary>
    public void Shutdown()
    {
        _tables = null;
        _init = false;
    }
}