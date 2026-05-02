using System;
using UnityEngine.SceneManagement;

namespace EF.Scene
{
    /// <summary>
    /// 场景信息数据结构
    /// </summary>
    [Serializable]
    public struct SceneInfo
    {
        /// <summary>
        /// 场景名称
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 场景资源位置
        /// </summary>
        public string Location { get; set; }
        
        /// <summary>
        /// 场景加载模式
        /// </summary>
        public LoadSceneMode LoadMode { get; set; }
        
        /// <summary>
        /// 物理模式
        /// </summary>
        public LocalPhysicsMode PhysicsMode { get; set; }
        
        /// <summary>
        /// 加载开始时间
        /// </summary>
        public DateTime LoadStartTime { get; set; }
        
        /// <summary>
        /// 加载结束时间
        /// </summary>
        public DateTime LoadEndTime { get; set; }
        
        /// <summary>
        /// 创建场景信息
        /// </summary>
        /// <param name="name">场景名称</param>
        /// <param name="location">场景位置</param>
        /// <param name="loadMode">加载模式</param>
        /// <param name="physicsMode">物理模式</param>
        public SceneInfo(string name, string location, LoadSceneMode loadMode = LoadSceneMode.Single, LocalPhysicsMode physicsMode = LocalPhysicsMode.None)
        {
            Name = name;
            Location = location;
            LoadMode = loadMode;
            PhysicsMode = physicsMode;
            LoadStartTime = default;
            LoadEndTime = default;
        }
        
        /// <summary>
        /// 是否为有效的场景信息
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(Location);
        
        /// <summary>
        /// 获取加载耗时
        /// </summary>
        public TimeSpan LoadDuration => LoadEndTime - LoadStartTime;
    }
}