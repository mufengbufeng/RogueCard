using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RogueCard.Editor
{
    /// <summary>
    /// 提供项目 Luban 配置表相关的 Unity Editor 菜单入口。
    /// </summary>
    public static class LubanEditorTools
    {
        private const string OpenDataPathMenuName = "Luban/OpenToDataPath";
        private const string BuildDataMenuName = "Luban/BuildData";
        private const string ConfigsDirectoryName = "Configs";
        private const string GameConfigDirectoryName = "GameConfig";
        private const string DatasDirectoryName = "Datas";
        private const string ExportBatchFileName = "gen_code_bin_to_project.bat";

        /// <summary>
        /// 打开 Luban 表格数据目录。
        /// </summary>
        [MenuItem(OpenDataPathMenuName)]
        public static void OpenToDataPath()
        {
            string dataDirectoryPath = GetDataDirectoryPath();
            if (!Directory.Exists(dataDirectoryPath))
            {
                Debug.LogError($"Luban 表格目录不存在：{dataDirectoryPath}");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = dataDirectoryPath,
                UseShellExecute = true
            });
        }

        /// <summary>
        /// 执行 Luban 配置导出批处理并刷新资源数据库。
        /// </summary>
        [MenuItem(BuildDataMenuName)]
        public static void BuildData()
        {
            string exportBatchPath = GetExportBatchPath();
            if (!File.Exists(exportBatchPath))
            {
                Debug.LogError($"Luban 导出脚本不存在：{exportBatchPath}");
                return;
            }

            string workingDirectory = Path.GetDirectoryName(exportBatchPath);
            if (string.IsNullOrEmpty(workingDirectory))
            {
                Debug.LogError($"Luban 导出脚本目录无效：{exportBatchPath}");
                return;
            }

            bool hasErrorOutput;
            int exitCode = ExecuteExportBatch(workingDirectory, out hasErrorOutput);
            AssetDatabase.Refresh();

            if (exitCode != 0)
            {
                Debug.LogError($"Luban 导出进程退出码非 0：{exitCode}");
                return;
            }

            if (!hasErrorOutput)
            {
                Debug.Log("Luban 导出完成，已刷新资源数据库。");
            }
        }

        /// <summary>
        /// 获取 Luban 表格数据目录路径。
        /// </summary>
        private static string GetDataDirectoryPath()
        {
            return Path.Combine(GetGameConfigDirectoryPath(), DatasDirectoryName);
        }

        /// <summary>
        /// 获取 Luban 导出批处理路径。
        /// </summary>
        private static string GetExportBatchPath()
        {
            return Path.Combine(GetGameConfigDirectoryPath(), ExportBatchFileName);
        }

        /// <summary>
        /// 获取 Luban 配置根目录路径。
        /// </summary>
        private static string GetGameConfigDirectoryPath()
        {
            return Path.Combine(GetRogueCardRootPath(), ConfigsDirectoryName, GameConfigDirectoryName);
        }

        /// <summary>
        /// 从 Unity Assets 目录推导 RogueCard 仓库根目录路径。
        /// </summary>
        private static string GetRogueCardRootPath()
        {
            DirectoryInfo assetsDirectory = new DirectoryInfo(Application.dataPath);
            DirectoryInfo unityProjectDirectory = assetsDirectory.Parent;
            DirectoryInfo rogueCardRootDirectory = unityProjectDirectory?.Parent;

            if (rogueCardRootDirectory == null)
            {
                throw new InvalidOperationException($"无法从 Unity 项目路径推导 RogueCard 根目录：{Application.dataPath}");
            }

            return rogueCardRootDirectory.FullName;
        }

        /// <summary>
        /// 执行 Luban 导出批处理并输出进程日志。
        /// </summary>
        private static int ExecuteExportBatch(string workingDirectory, out bool hasErrorOutput)
        {
            List<string> stdoutLines = new List<string>();
            List<string> stderrLines = new List<string>();

            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/d /s /c \"\"{ExportBatchFileName}\" < nul\"",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = new Process())
            {
                process.StartInfo = processStartInfo;
                process.OutputDataReceived += (_, args) => AddProcessLine(stdoutLines, args.Data);
                process.ErrorDataReceived += (_, args) => AddProcessLine(stderrLines, args.Data);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                process.WaitForExit();

                hasErrorOutput = LogProcessOutput(stdoutLines, stderrLines);
                return process.ExitCode;
            }
        }

        /// <summary>
        /// 收集进程输出行。
        /// </summary>
        private static void AddProcessLine(List<string> lines, string line)
        {
            if (line == null)
            {
                return;
            }

            lock (lines)
            {
                lines.Add(line);
            }
        }

        /// <summary>
        /// 将进程输出写入 Unity Console。
        /// </summary>
        private static bool LogProcessOutput(List<string> stdoutLines, List<string> stderrLines)
        {
            bool hasErrorOutput = false;

            foreach (string stdoutLine in stdoutLines)
            {
                if (ContainsErrorText(stdoutLine))
                {
                    Debug.LogError(stdoutLine);
                    hasErrorOutput = true;
                    continue;
                }

                Debug.Log(stdoutLine);
            }

            foreach (string stderrLine in stderrLines)
            {
                Debug.LogError(stderrLine);
                hasErrorOutput = true;
            }

            return hasErrorOutput;
        }

        /// <summary>
        /// 判断输出行是否包含错误文本。
        /// </summary>
        private static bool ContainsErrorText(string line)
        {
            return line.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
