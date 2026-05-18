using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace EF.UI
{
    /// <summary>
    /// UI Prefab 加载结果，封装 Prefab 与可释放的资源句柄。
    /// </summary>
    internal readonly struct UIPrefabLoadResult
    {
        /// <summary>
        /// 创建 UI Prefab 加载结果。
        /// </summary>
        public UIPrefabLoadResult(GameObject prefab, HandleBase handle)
        {
            Prefab = prefab;
            Handle = handle;
        }

        /// <summary>
        /// 加载到的 Prefab。
        /// </summary>
        public GameObject Prefab { get; }

        /// <summary>
        /// 资源系统返回的句柄；测试加载器可以为空。
        /// </summary>
        public HandleBase Handle { get; }
    }

    /// <summary>
    /// UIManager 内部使用的 Prefab 加载接口，便于 EditMode 测试隔离 YooAsset 句柄。
    /// </summary>
    internal interface IUIManagerPrefabLoader
    {
        /// <summary>
        /// 异步加载指定地址的 UI Prefab。
        /// </summary>
        UniTask<UIPrefabLoadResult> LoadAsync(string location, CancellationToken cancellationToken);

        /// <summary>
        /// 释放加载结果中的资源句柄。
        /// </summary>
        void Release(UIPrefabLoadResult result);
    }
}
