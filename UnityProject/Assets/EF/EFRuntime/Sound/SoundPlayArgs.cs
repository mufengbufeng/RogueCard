using UnityEngine;

namespace EF.Sound
{
    /// <summary>
    /// 音频播放参数，封装播放时的配置选项。
    /// </summary>
    public sealed class SoundPlayArgs
    {
        /// <summary>
        /// 音频资源路径或标识。
        /// </summary>
        public string AssetName { get; set; }

        /// <summary>
        /// 音频类型。
        /// </summary>
        public SoundType SoundType { get; set; }

        /// <summary>
        /// 音量（0.0 ~ 1.0）。
        /// </summary>
        public float Volume { get; set; } = 1f;

        /// <summary>
        /// 是否循环播放。
        /// </summary>
        public bool Loop { get; set; }

        /// <summary>
        /// 音频优先级（0 ~ 256，数值越低优先级越高）。
        /// </summary>
        public int Priority { get; set; } = 128;

        /// <summary>
        /// 淡入时长（秒）。
        /// </summary>
        public float FadeInDuration { get; set; }

        /// <summary>
        /// 音频空间混合（0 = 2D，1 = 3D）。
        /// </summary>
        public float SpatialBlend { get; set; }

        /// <summary>
        /// 3D 音频位置（仅当 SpatialBlend > 0 时有效）。
        /// </summary>
        public Vector3? Position { get; set; }

        /// <summary>
        /// 关联的 Transform（用于跟随移动物体）。
        /// </summary>
        public Transform AttachedTransform { get; set; }

        /// <summary>
        /// 播放完成时的回调。
        /// </summary>
        public System.Action<int> OnComplete { get; set; }

        /// <summary>
        /// 用户自定义数据。
        /// </summary>
        public object UserData { get; set; }
    }
}
