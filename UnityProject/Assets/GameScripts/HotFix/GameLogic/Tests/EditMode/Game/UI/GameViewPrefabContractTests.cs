using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// GameView UGUI Prefab 与运行时代码静态契约测试。
    /// </summary>
    [TestFixture]
    public sealed class GameViewPrefabContractTests
    {
        private const string PrefabPath = "Assets/AssetRaw/UI/Game/GameView.prefab";
        private static readonly string[] RequiredKeys =
        {
            "BgImage",
            "BattlePanel",
            "RewardPanel",
            "PlayerStatusPanel",
            "InfoText",
            "PlayerHpFill",
            "PlayerHpText",
            "PlayerArmorText",
            "PlayerEnergyFill",
            "PlayerEnergyText",
            "PlayerBuffBar",
            "MonsterRect",
            "CardSc",
            "DropZone",
            "PreviewLayer",
            "EndBtn",
            "FailToast",
            "RewardConfirmBtn",
            "HandCardTemplate",
            "MonsterItemTemplate",
            "BuffIconTemplate",
            "IntentIconTemplate",
        };

        /// <summary>
        /// GameView Prefab 根对象必须声明完整 UGUI 组件和 ReferenceCollector key。
        /// </summary>
        [Test]
        public void GameViewPrefab_声明完整UGUI绑定()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.NotNull(prefab, $"未找到 Prefab：{PrefabPath}");
            Assert.NotNull(prefab.GetComponent<GameView>(), "GameView.prefab 根对象必须挂载 GameView。");
            Assert.NotNull(prefab.GetComponent<Canvas>(), "GameView.prefab 根对象必须挂载 Canvas。");
            Assert.NotNull(prefab.GetComponent<CanvasScaler>(), "GameView.prefab 根对象必须挂载 CanvasScaler。");
            Assert.NotNull(prefab.GetComponent<GraphicRaycaster>(), "GameView.prefab 根对象必须挂载 GraphicRaycaster。");

            ReferenceCollector collector = prefab.GetComponent<ReferenceCollector>();
            Assert.NotNull(collector, "GameView.prefab 根对象必须挂载 ReferenceCollector。");
            var keys = new HashSet<string>(collector.data.Select(x => x.key));
            foreach (string key in RequiredKeys)
            {
                Assert.IsTrue(keys.Contains(key), $"ReferenceCollector 缺少 key：{key}");
                Assert.NotNull(collector.data.First(x => x.key == key).gameObject, $"ReferenceCollector key {key} 引用为空。");
            }
        }

        /// <summary>
        /// 正式 GameView 路径不得继续创建迁移期临时摘要 UI。
        /// </summary>
        [Test]
        public void GameView源码_不再包含临时摘要UI()
        {
            string source = File.ReadAllText("Assets/GameScripts/HotFix/GameLogic/UI/Game/GameView.cs");
            Assert.That(source, Does.Not.Contain("PhaseTextRuntime"));
            Assert.That(source, Does.Not.Contain("HandCommandPanelRuntime"));
            Assert.That(source, Does.Not.Contain("MonsterCommandPanelRuntime"));
            Assert.That(source, Does.Not.Contain("CreateRuntimeText"));
            Assert.That(source, Does.Not.Contain("CreateRuntimeButton"));
        }

        /// <summary>
        /// 运行时代码不能重新引入 UI Toolkit。
        /// </summary>
        [Test]
        public void 运行时代码_不依赖UITK()
        {
            string[] files = Directory.GetFiles("Assets/GameScripts", "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets/EF/EFRuntime", "*.cs", SearchOption.AllDirectories))
                .Where(path => !path.Replace('\\', '/').Contains("/Editor/"))
                .ToArray();

            string[] forbidden =
            {
                "UnityEngine." + "UIElements",
                "Visual" + "Element",
                "UI" + "Document",
                "Visual" + "TreeAsset",
                "Style" + "Sheet",
                "UX" + "ML",
                "US" + "S",
                "Ux" + "ml",
                "Us" + "s",
            };

            var matches = new List<string>();
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                foreach (string token in forbidden)
                {
                    if (text.Contains(token))
                    {
                        matches.Add($"{file}: {token}");
                    }
                }
            }

            Assert.IsEmpty(matches, string.Join("\n", matches));
        }
    }
}
