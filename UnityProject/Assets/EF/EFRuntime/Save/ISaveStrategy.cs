namespace EF.Save
{
    /// <summary>
    /// 保存策略接口，定义数据保存和加载的具体实现方式。
    /// </summary>
    public interface ISaveStrategy
    {
        /// <summary>
        /// 保存数据。
        /// </summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="key">保存键值。</param>
        /// <param name="data">要保存的数据。</param>
        /// <returns>保存是否成功。</returns>
        bool Save<T>(string key, T data);

        /// <summary>
        /// 加载数据。
        /// </summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="key">保存键值。</param>
        /// <param name="defaultValue">默认值。</param>
        /// <returns>加载的数据或默认值。</returns>
        T Load<T>(string key, T defaultValue = default);

        /// <summary>
        /// 检查指定键是否存在。
        /// </summary>
        /// <param name="key">保存键值。</param>
        /// <returns>数据是否存在。</returns>
        bool HasKey(string key);

        /// <summary>
        /// 删除指定键的数据。
        /// </summary>
        /// <param name="key">保存键值。</param>
        /// <returns>删除是否成功。</returns>
        bool Delete(string key);

        /// <summary>
        /// 删除所有数据。
        /// </summary>
        void DeleteAll();
    }
}
