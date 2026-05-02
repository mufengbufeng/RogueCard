// using System.Collections.Generic;

// namespace EF.HotFix
// {
//     /// <summary>
//     /// 热更静态配置类
//     /// 如果不想使用ScriptableObject，可以使用这个静态类
//     /// </summary>
//     public static class HotFixStaticConfig
//     {
//         /// <summary>
//         /// 热更新DLL列表
//         /// </summary>
//         public static readonly List<string> HotFixDlls = new List<string>
//         {
//             "GameLogic.dll",
//             "GameProto.dll"
//         };

//         /// <summary>
//         /// AOT元数据DLL列表
//         /// </summary>
//         public static readonly List<string> AotMetaDlls = new List<string>
//         {
//             "mscorlib.dll",
//             "System.dll",
//             "System.Core.dll",
//             "YooAsset.dll",
//             "UniTask.dll",
//             "EF.Runtime.dll",
//             "DoTween.dll"
//         };

//         /// <summary>
//         /// 获取所有需要加载的DLL列表
//         /// </summary>
//         public static List<string> GetAllDlls()
//         {
//             var allDlls = new List<string>();
//             allDlls.AddRange(HotFixDlls);
//             allDlls.AddRange(AotMetaDlls);
//             return allDlls;
//         }
//     }
// }