using System;
using System.Collections.Generic;
using EF.Common;

namespace EF.Model
{
    /// <summary>
    /// 全局模型管理器，负责模型的注册、检索与生命周期管理。
    /// </summary>
    public sealed class ModelManager : AEFManager
    {
        private readonly Dictionary<Type, IModelInternal> _models = new();
        private readonly Dictionary<Type, Type> _viewToModel = new();
        private readonly List<IModelInternal> _updateOrder = new();
        private readonly object _syncRoot = new();

        /// <summary>
        /// 当前已注册的模型数量。
        /// </summary>
        public int ModelCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _models.Count;
                }
            }
        }

        /// <summary>
        /// 注册一个模型实例。
        /// </summary>
        /// <typeparam name="TModel">模型类型。</typeparam>
        /// <param name="model">模型实例。</param>
        public TModel Register<TModel>(TModel model)
            where TModel : ModelBase
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            lock (_syncRoot)
            {
                return RegisterInternal(model);
            }
        }

        /// <summary>
        /// 内部注册逻辑，调用者必须持有 _syncRoot 锁。
        /// </summary>
        private TModel RegisterInternal<TModel>(TModel model)
            where TModel : ModelBase
        {
            IModelInternal internalModel = model;

            Type concreteType = model.GetType();
            if (_models.ContainsKey(concreteType))
            {
                throw new InvalidOperationException($"模型 {concreteType.FullName} 已经注册，不能重复注册。");
            }

            Type viewType = internalModel.ViewType ?? throw new InvalidOperationException($"模型 {concreteType.FullName} 未提供有效的视图类型。");
            if (_viewToModel.ContainsKey(viewType))
            {
                throw new InvalidOperationException($"视图类型 {viewType.FullName} 已被模型 {_viewToModel[viewType].FullName} 占用。");
            }

            internalModel.Initialize(this);
            object viewInstance = internalModel.ViewInstance ?? throw new InvalidOperationException($"模型 {concreteType.FullName} 无法创建视图实例。");

            _models.Add(concreteType, internalModel);
            _viewToModel.Add(viewType, concreteType);
            _updateOrder.Add(internalModel);

            return model;
        }

        /// <summary>
        /// 注册一个模型实例并返回对应数据接口。
        /// </summary>
        public TData Register<TModel, TData>(TModel model)
            where TModel : ModelBase<TData>
            where TData : class
        {
            Register(model);
            return (TData)((IModelInternal)model).ViewInstance;
        }

        /// <summary>
        /// 通过类型自动创建并注册模型。
        /// </summary>
        public TModel Register<TModel>()
            where TModel : ModelBase, new()
        {
            return Register(new TModel());
        }

        /// <summary>
        /// 通过类型自动创建并注册模型，并返回对应数据接口。
        /// </summary>
        public TData Register<TModel, TData>()
            where TModel : ModelBase<TData>, new()
            where TData : class
        {
            TModel model = Register(new TModel());
            return (TData)((IModelInternal)model).ViewInstance;
        }

        /// <summary>
        /// 注销某个模型。
        /// </summary>
        public bool Unregister<TModel>() where TModel : ModelBase
        {
            return Unregister(typeof(TModel));
        }

        /// <summary>
        /// 注销指定类型的模型。
        /// </summary>
        public bool Unregister(Type modelType)
        {
            if (modelType == null)
            {
                throw new ArgumentNullException(nameof(modelType));
            }

            IModelInternal removed = null;

            lock (_syncRoot)
            {
                if (!_models.TryGetValue(modelType, out IModelInternal model))
                {
                    return false;
                }

                removed = model;
                _models.Remove(modelType);
                _viewToModel.Remove(model.ViewType);
                _updateOrder.Remove(model);
            }

            removed.Shutdown();
            return true;
        }

        /// <summary>
        /// 根据数据接口类型注销模型。
        /// </summary>
        public bool UnregisterByData<TData>() where TData : class
        {
            return UnregisterByData(typeof(TData));
        }

        /// <summary>
        /// 根据数据接口类型注销模型。
        /// </summary>
        public bool UnregisterByData(Type dataType)
        {
            if (dataType == null)
            {
                throw new ArgumentNullException(nameof(dataType));
            }

            Type modelType;

            lock (_syncRoot)
            {
                if (!_viewToModel.TryGetValue(dataType, out modelType))
                {
                    return false;
                }
            }

            return Unregister(modelType);
        }

        /// <summary>
        /// 获取某个模型实例。
        /// 如果模型未注册将抛出异常。
        /// </summary>
        public TModel GetModel<TModel>() where TModel : ModelBase
        {
            lock (_syncRoot)
            {
                if (_models.TryGetValue(typeof(TModel), out IModelInternal internalModel))
                {
                    return (TModel)internalModel;
                }
            }

            throw new KeyNotFoundException($"模型 {typeof(TModel).FullName} 尚未注册。");
        }



        /// <summary>
        /// 获取只读数据接口。
        /// </summary>
        public TData Get<TData>() where TData : class
        {
            if (TryGet(out TData data))
            {
                return data;
            }

            throw new KeyNotFoundException($"数据接口 {typeof(TData).FullName} 尚未注册。");
        }

        /// <summary>
        /// 尝试获取模型实例，如果未注册则自动创建并注册。
        /// 该方法保证返回非空实例。
        /// </summary>
        /// <typeparam name="TModel">模型类型，必须有无参构造函数。</typeparam>
        /// <returns>模型实例（保证非空）。</returns>
        public TModel TryGetModel<TModel>() where TModel : ModelBase, new()
        {
            lock (_syncRoot)
            {
                // 先尝试获取现有实例
                if (_models.TryGetValue(typeof(TModel), out IModelInternal existing))
                {
                    return (TModel)existing;
                }

                // 不存在则在锁内创建并注册
                return RegisterInternal(new TModel());
            }
        }

        /// <summary>
        /// 尝试获取模型实例，仅执行查找不自动创建。
        /// 如果模型未注册则返回 null。
        /// </summary>
        /// <param name="modelType">模型类型。</param>
        /// <returns>模型实例，如果未注册则返回 null。</returns>
        public ModelBase TryGetModel(Type modelType)
        {
            if (modelType == null)
            {
                throw new ArgumentNullException(nameof(modelType));
            }

            lock (_syncRoot)
            {
                if (_models.TryGetValue(modelType, out IModelInternal internalModel))
                {
                    return (ModelBase)internalModel;
                }
            }

            return null;
        }

        /// <summary>
        /// 尝试获取只读数据接口。
        /// </summary>
        public bool TryGet<TData>(out TData data) where TData : class
        {
            if (TryGet(typeof(TData), out object dataInstance))
            {
                data = (TData)dataInstance;
                return true;
            }

            data = null;
            return false;
        }

        /// <summary>
        /// 尝试获取只读数据接口。
        /// </summary>
        public bool TryGet(Type dataType, out object data)
        {
            if (dataType == null)
            {
                throw new ArgumentNullException(nameof(dataType));
            }

            lock (_syncRoot)
            {
                if (_viewToModel.TryGetValue(dataType, out Type modelType) &&
                    _models.TryGetValue(modelType, out IModelInternal model))
                {
                    data = model.ViewInstance;
                    return true;
                }
            }

            data = null;
            return false;
        }

        /// <summary>
        /// 尝试获取只读数据接口，若未注册则返回 null。
        /// </summary>
        public TData TryGet<TData>() where TData : class
        {
            TryGet(out TData data);
            return data;
        }

        /// <summary>
        /// 尝试获取只读数据接口，若未注册则返回 null。
        /// </summary>
        public object TryGet(Type dataType)
        {
            TryGet(dataType, out object data);
            return data;
        }

        /// <summary>
        /// 尝试通过数据接口类型获取模型实例。
        /// </summary>
        public bool TryGetModelByData<TData>(out ModelBase model) where TData : class
        {
            return TryGetModelByData(typeof(TData), out model);
        }

        /// <summary>
        /// 尝试通过数据接口类型获取模型实例。
        /// </summary>
        public bool TryGetModelByData(Type dataType, out ModelBase model)
        {
            if (dataType == null)
            {
                throw new ArgumentNullException(nameof(dataType));
            }

            lock (_syncRoot)
            {
                if (_viewToModel.TryGetValue(dataType, out Type modelType) &&
                    _models.TryGetValue(modelType, out IModelInternal internalModel))
                {
                    model = (ModelBase)internalModel;
                    return true;
                }
            }

            model = null;
            return false;
        }

        /// <summary>
        /// 判断模型是否已经注册。
        /// </summary>
        public bool HasModel<TModel>() where TModel : ModelBase
        {
            lock (_syncRoot)
            {
                return _models.ContainsKey(typeof(TModel));
            }
        }

        /// <summary>
        /// 清空并关闭所有模型。
        /// </summary>
        public override void Shutdown()
        {
            IModelInternal[] snapshot;

            lock (_syncRoot)
            {
                if (_updateOrder.Count == 0)
                {
                    return;
                }

                snapshot = _updateOrder.ToArray();
                _updateOrder.Clear();
                _models.Clear();
                _viewToModel.Clear();
            }

            for (int index = snapshot.Length - 1; index >= 0; index--)
            {
                snapshot[index].Shutdown();
            }
        }

        /// <summary>
        /// 更新所有已注册模型。
        /// </summary>
        public override void Update(float elapseSeconds, float realElapseSeconds)
        {
            IModelInternal[] snapshot;

            lock (_syncRoot)
            {
                if (_updateOrder.Count == 0)
                {
                    return;
                }

                snapshot = _updateOrder.ToArray();
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                snapshot[i].Update(elapseSeconds, realElapseSeconds);
            }
        }
    }
}
