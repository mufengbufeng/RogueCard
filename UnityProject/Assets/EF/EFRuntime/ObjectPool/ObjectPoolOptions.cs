namespace EF.ObjectPool
{
    /// <summary>
    /// 对象池的运行配置。
    /// </summary>
    public sealed class ObjectPoolOptions
    {
        private int _capacity = int.MaxValue;
        private float _expireTime = 60f;
        private float _autoReleaseInterval = 5f;

        /// <summary>
        /// 是否允许同一个对象被重复取出（引用计数方式）。
        /// </summary>
        public bool AllowMultiSpawn { get; set; }

        /// <summary>
        /// 是否自动释放长时间未使用的对象。
        /// </summary>
        public bool AutoRelease { get; set; } = true;

        /// <summary>
        /// 对象池容量上限，超过后将优先释放空闲对象。
        /// </summary>
        public int Capacity
        {
            get => _capacity;
            set
            {
                if (value <= 0)
                {
                    throw new System.ArgumentOutOfRangeException(nameof(value), "对象池容量必须为正数");
                }

                _capacity = value;
            }
        }

        /// <summary>
        /// 对象空闲多久后视为过期（秒）。
        /// </summary>
        public float ExpireTime
        {
            get => _expireTime;
            set
            {
                if (value < 0f)
                {
                    throw new System.ArgumentOutOfRangeException(nameof(value), "对象过期时间不能为负数");
                }

                _expireTime = value;
            }
        }

        /// <summary>
        /// 自动释放的检测间隔（秒）。
        /// </summary>
        public float AutoReleaseInterval
        {
            get => _autoReleaseInterval;
            set
            {
                if (value <= 0f)
                {
                    throw new System.ArgumentOutOfRangeException(nameof(value), "自动释放检测间隔必须大于零");
                }

                _autoReleaseInterval = value;
            }
        }

        /// <summary>
        /// 克隆配置，避免外部修改内部状态。
        /// </summary>
        public ObjectPoolOptions Clone()
        {
            return (ObjectPoolOptions)MemberwiseClone();
        }
    }
}
