using System.Collections.Generic;
using UnityEngine;

namespace EF.HotFix
{
    [CreateAssetMenu(fileName = "HotFixConfig", menuName = "EasyFramework/HotFixConfig")]
    public class HotFixConfig : ScriptableObject
    {
        [Header("热更新DLL配置")]
        [Tooltip("需要加载的热更新DLL列表")]
        public List<string> hotFixDlls = new List<string>
        {
            "GameLogic.dll",
            "GameProto.dll"
        };

        [Header("AOT元数据DLL配置")]
        [Tooltip("需要加载AOT元数据的DLL列表")]
        public List<string> aotMetaDlls = new List<string>
        {
            "mscorlib.dll",
            "System.dll",
            "System.Core.dll",
            "YooAsset.dll",
            "UniTask.dll",
            "EF.Runtime.dll",
            "DoTween.dll"
        };

        /// <summary>
        /// 获取所有需要加载的DLL列表
        /// </summary>
        public List<string> GetAllDlls()
        {
            var allDlls = new List<string>();
            allDlls.AddRange(hotFixDlls);
            allDlls.AddRange(aotMetaDlls);
            return allDlls;
        }
    }
}