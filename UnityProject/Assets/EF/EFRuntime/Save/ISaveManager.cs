using EF.Common;

namespace EF.Save
{
    /// <summary>
    /// 本地保存管理器接口，提供数据持久化能力。
    /// </summary>
    public interface ISaveManager : IEFManager
    {
        /// <summary>
        /// 当前使用的保存策略类型。
        /// </summary>
        SaveStrategyType CurrentStrategyType { get; }

        /// <summary>
        /// 切换保存策略。
        /// </summary>
        /// <param name="strategyType">目标策略类型。</param>
        void SetSaveStrategy(SaveStrategyType strategyType);

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
        /// <param name="defaultValue">默认值，当数据不存在时返回。</param>
        /// <returns>加载的数据或默认值。</returns>
        T Load<T>(string key, T defaultValue = default);

        /// <summary>
        /// 检查指定键是否存在数据。
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
        /// 删除所有保存的数据。
        /// </summary>
        void DeleteAll();
    }

    /// <summary>
    /// 保存策略类型。
    /// </summary>
    public enum SaveStrategyType
    {
        /// <summary>
        /// Json 文件保存。
        /// </summary>
        Json,

        /// <summary>
        /// Unity PlayerPrefs 保存。
        /// </summary>
        PlayerPrefs
    }
}
