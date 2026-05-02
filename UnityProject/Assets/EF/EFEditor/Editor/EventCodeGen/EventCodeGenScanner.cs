using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EF.Event;
using UnityEditor;
using UnityEngine;

namespace EF.Editor.EventCodeGen
{
    /// <summary>
    /// 扫描所有程序集中被 [EventArgs] 标记的 readonly struct 类型。
    /// </summary>
    public static class EventCodeGenScanner
    {
        /// <summary>
        /// 扫描所有已加载程序集，返回所有标记 [EventArgs] 的 readonly struct 类型。
        /// </summary>
        /// <returns>符合条件的类型列表。</returns>
        public static List<Type> ScanAllAssemblies()
        {
            var results = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!type.IsValueType || type.IsEnum || type.IsAbstract)
                            continue;

                        if (!type.IsDefined(typeof(EventArgsAttribute), false))
                            continue;

                        if (type.IsClass)
                        {
                            Debug.LogWarning($"[EventCodeGen] 类型 {type.FullName} 标记了 [EventArgs] 但不是 struct，已跳过。");
                            continue;
                        }

                        if (type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            .Any(f => !f.IsInitOnly && !f.IsLiteral))
                        {
                            Debug.LogWarning($"[EventCodeGen] 类型 {type.FullName} 标记了 [EventArgs] 但不是 readonly struct，建议改为 readonly struct。");
                        }

                        results.Add(type);
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // 跳过无法加载的程序集
                }
            }

            return results;
        }
    }
}
