using System;
using System.Collections.Generic;

namespace EF.Fsm
{
    /// <summary>
    /// FSM 内部使用的共享数据容器，提供类型安全的访问方式。
    /// </summary>
    internal sealed class FsmDataCollection
    {
        private readonly Dictionary<string, object> _data = new(StringComparer.Ordinal);

        /// <summary>
        /// 当前数据数量。
        /// </summary>
        public int Count => _data.Count;

        /// <summary>
        /// 设置数据，若键已存在则覆盖。
        /// </summary>
        public void SetData<TData>(string name, TData data)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("数据键不能为空。", nameof(name));
            }

            _data[name] = data!;
        }

        /// <summary>
        /// 尝试获取数据并进行类型转换。
        /// </summary>
        public bool TryGetData<TData>(string name, out TData value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("数据键不能为空。", nameof(name));
            }

            if (_data.TryGetValue(name, out object raw) && raw is TData typed)
            {
                value = typed;
                return true;
            }

            value = default!;
            return false;
        }

        /// <summary>
        /// 获取数据，若失败则抛出异常。
        /// </summary>
        public TData GetData<TData>(string name)
        {
            if (TryGetData<TData>(name, out TData value))
            {
                return value;
            }

            throw new KeyNotFoundException($"未找到名为 {name} 的状态机数据，或类型不匹配。");
        }

        /// <summary>
        /// 移除指定键的数据。
        /// </summary>
        public bool RemoveData(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("数据键不能为空。", nameof(name));
            }

            return _data.Remove(name);
        }

        /// <summary>
        /// 清空所有数据。
        /// </summary>
        public void Clear()
        {
            _data.Clear();
        }
    }
}
