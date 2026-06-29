using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ImageToUI.PrefabBuilder
{
    [InitializeOnLoad]
    internal static class ImageToUISkillInstaller
    {
        private const string PackageName = "me.xw.image-to-ui";
        private const string SkillName = "image-to-ui";
        private const string SkillFolder = "Skill~";
        private const string StampFile = ".image-to-ui-skill-install-stamp";

        static ImageToUISkillInstaller()
        {
            EditorApplication.delayCall += AutoInstall;
        }

        [MenuItem("Tools/Image To UI/Install Codex Skill")]
        public static void InstallFromMenu()
        {
            InstallSkill(true, true);
        }

        private static void AutoInstall()
        {
            InstallSkill(false, false);
        }

        private static void InstallSkill(bool force, bool logWhenUnchanged)
        {
            try
            {
                var source = GetSourceSkillPath();
                if (string.IsNullOrEmpty(source) || !Directory.Exists(source))
                {
                    Debug.LogWarning("Image To UI skill source not found in package: " + source);
                    return;
                }

                var skillsRoot = Path.Combine(GetProjectRoot(), ".codex", "skills");
                var target = Path.Combine(skillsRoot, SkillName);
                var stampPath = Path.Combine(skillsRoot, StampFile);
                var sourceStamp = ComputeDirectoryStamp(source);

                if (!force &&
                    File.Exists(Path.Combine(target, "SKILL.md")) &&
                    File.Exists(stampPath) &&
                    File.ReadAllText(stampPath).Trim() == sourceStamp)
                {
                    if (logWhenUnchanged)
                    {
                        Debug.Log("Image To UI Codex skill is already installed: " + target);
                    }

                    return;
                }

                Directory.CreateDirectory(skillsRoot);

                if (Directory.Exists(target) && (force || File.Exists(stampPath)))
                {
                    Directory.Delete(target, true);
                }

                CopyDirectory(source, target);
                File.WriteAllText(stampPath, sourceStamp);

                Debug.Log("Installed Image To UI Codex skill to: " + target);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Failed to install Image To UI Codex skill: " + ex.Message);
            }
        }

        private static string GetSourceSkillPath()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(ImageToUISkillInstaller).Assembly
            );
            if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
            {
                var packageSkill = Path.Combine(packageInfo.resolvedPath, SkillFolder, SkillName);
                if (Directory.Exists(packageSkill))
                {
                    return packageSkill;
                }
            }

            return Path.Combine(GetProjectRoot(), "Packages", PackageName, SkillFolder, SkillName);
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);

            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                if (ShouldSkipPath(directory))
                {
                    continue;
                }

                var relative = GetRelativePath(source, directory);
                Directory.CreateDirectory(Path.Combine(target, relative));
            }

            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                if (ShouldSkipPath(file))
                {
                    continue;
                }

                var relative = GetRelativePath(source, file);
                var destination = Path.Combine(target, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, true);
            }
        }

        private static string ComputeDirectoryStamp(string source)
        {
            var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories)
                .Where(file => !ShouldSkipPath(file))
                .OrderBy(file => GetRelativePath(source, file), StringComparer.Ordinal)
                .ToArray();

            using (var stream = new MemoryStream())
            {
                foreach (var file in files)
                {
                    WriteBytes(stream, GetRelativePath(source, file).Replace('\\', '/'));
                    WriteBytes(stream, "\n");

                    var bytes = File.ReadAllBytes(file);
                    stream.Write(bytes, 0, bytes.Length);
                    WriteBytes(stream, "\n");
                }

                using (var sha = SHA256.Create())
                {
                    return ToHex(sha.ComputeHash(stream.ToArray()));
                }
            }
        }

        private static bool ShouldSkipPath(string path)
        {
            var name = Path.GetFileName(path);
            if (string.Equals(name, "__pycache__", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, ".DS_Store", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Thumbs.db", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var extension = Path.GetExtension(path);
            return string.Equals(extension, ".meta", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".pyc", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".tmp", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".bak", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRelativePath(string root, string path)
        {
            var rootUri = new Uri(AppendDirectorySeparator(root));
            var pathUri = new Uri(path);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static void WriteBytes(Stream stream, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
