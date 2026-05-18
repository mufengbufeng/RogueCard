using System.Collections.Generic;
using GameConfig.card;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// UGUI PlayerStatusView 行为测试。
    /// </summary>
    [TestFixture]
    public sealed class PlayerStatusViewTests
    {
        private GameObject _root;
        private FakePlayerStatusContext _context;

        [SetUp]
        public void SetUp()
        {
            _root = UguiTestFactory.CreateRectObject("PlayerStatusRoot");
            _context = new FakePlayerStatusContext();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        /// <summary>
        /// 玩家状态变化会刷新阶段、HP、能量和护甲文本。
        /// </summary>
        [Test]
        public void 状态变化_刷新文本和进度条()
        {
            PlayerStatusBindings bindings = CreateBindings();
            using var view = new PlayerStatusView(bindings, _context);

            _context.Phase.Value = BattlePhase.PlayerTurn;
            _context.PlayerMaxHp.Value = 100;
            _context.PlayerHp.Value = 30;
            _context.PlayerArmor.Value = 5;
            _context.MaxEnergy.Value = 3;
            _context.Energy.Value = 2;

            Assert.AreEqual("你的回合", bindings.InfoText.text);
            Assert.AreEqual("30/100", bindings.HpText.text);
            Assert.AreEqual(0.3f, bindings.HpFill.fillAmount, 0.001f);
            Assert.AreEqual("5", bindings.ArmorText.text);
            Assert.AreEqual("2/3", bindings.EnergyText.text);
            Assert.AreEqual(2f / 3f, bindings.EnergyFill.fillAmount, 0.001f);
        }

        /// <summary>
        /// 关卡完成和玩家死亡按优先级覆盖阶段文本。
        /// </summary>
        [Test]
        public void 阶段文本_关卡完成优先于玩家死亡()
        {
            PlayerStatusBindings bindings = CreateBindings();
            using var view = new PlayerStatusView(bindings, _context);

            _context.Phase.Value = BattlePhase.MonsterTurn;
            _context.IsPlayerDead.Value = true;
            _context.IsLevelComplete.Value = true;

            Assert.AreEqual("关卡完成！", bindings.InfoText.text);
        }

        /// <summary>
        /// 玩家 Buff 使用模板渲染。
        /// </summary>
        [Test]
        public void 玩家Buff_使用模板渲染()
        {
            PlayerStatusBindings bindings = CreateBindings();
            using var view = new PlayerStatusView(bindings, _context);

            _context.PlayerBuffs.Value = new[]
            {
                new BuffRuntime { Kind = EffectKind.DamageDot, Value = 4, RemainingTurns = 2 },
            };

            Assert.AreEqual(1, RuntimeChildCount(bindings.PlayerBuffBar, bindings.BuffIconTemplate));
            TextMeshProUGUI text = bindings.PlayerBuffBar.GetComponentInChildren<TextMeshProUGUI>(false);
            Assert.AreEqual("4×2", text.text);
        }

        /// <summary>
        /// Dispose 后不再响应上下文变化。
        /// </summary>
        [Test]
        public void Dispose后_不再刷新()
        {
            PlayerStatusBindings bindings = CreateBindings();
            var view = new PlayerStatusView(bindings, _context);
            view.Dispose();

            _context.PlayerMaxHp.Value = 100;
            _context.PlayerHp.Value = 10;

            Assert.AreNotEqual("10/100", bindings.HpText.text);
            Assert.DoesNotThrow(() => view.Dispose());
        }

        private PlayerStatusBindings CreateBindings()
        {
            RectTransform buffBar = UguiTestFactory.CreateRectObject("PlayerBuffBar", _root.transform).GetComponent<RectTransform>();
            return new PlayerStatusBindings
            {
                InfoText = UguiTestFactory.CreateText("InfoText", _root.transform),
                HpFill = UguiTestFactory.CreateImage("PlayerHpFill", _root.transform),
                HpText = UguiTestFactory.CreateText("PlayerHpText", _root.transform),
                ArmorText = UguiTestFactory.CreateText("PlayerArmorText", _root.transform),
                EnergyFill = UguiTestFactory.CreateImage("PlayerEnergyFill", _root.transform),
                EnergyText = UguiTestFactory.CreateText("PlayerEnergyText", _root.transform),
                PlayerBuffBar = buffBar,
                BuffIconTemplate = UguiTestFactory.CreateIconTemplate("BuffIconTemplate", buffBar),
            };
        }

        private static int RuntimeChildCount(RectTransform parent, GameObject template)
        {
            int count = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).gameObject != template)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
