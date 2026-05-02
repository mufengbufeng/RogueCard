namespace EF.Feature
{
    /// <summary>
    /// 强类型数据特性基类，提供类型安全的 OnSetup 转发。
    /// </summary>
    /// <typeparam name="TData">配置数据类型。</typeparam>
    public abstract class FeatureBase<TData> : FeatureBase where TData : class
    {
        /// <summary>
        /// 将 object 类型数据转发到强类型 OnSetup。
        /// </summary>
        public sealed override void OnSetup(object data)
        {
            if (data is TData typed)
            {
                OnSetup(typed);
            }
        }

        /// <summary>
        /// 接收强类型配置数据。子类重写此方法以使用类型安全的数据。
        /// </summary>
        /// <param name="data">配置数据。</param>
        protected virtual void OnSetup(TData data)
        {
        }
    }
}
