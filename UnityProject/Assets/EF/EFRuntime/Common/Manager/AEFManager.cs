using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EF.Common
{
    public abstract class AEFManager : IUpdate, IEFManager
    {

        /// <summary>
        ///  每帧调用的更新方法
        /// </summary>
        public virtual void Update(float elapseSeconds, float realElapseSeconds) { }

        /// <summary>
        ///  初始化管理器
        /// </summary>
        public abstract void Shutdown();

    }
}