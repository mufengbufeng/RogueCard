using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EF.Common
{
    internal interface IUpdate
    {
        /// <summary>
        ///  每帧调用的更新方法
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间，单位秒</param>
        /// <param name="realElapseSeconds">真实流逝时间，单位秒</param>
        public void Update(float elapseSeconds, float realElapseSeconds);
    }
}