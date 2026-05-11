using System.Collections.Generic;
using GameConfig.card;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace GameLogic.Tests
{
    /// <summary>
    /// PlayerStatusView 单元测试：通过 FakePlayerStatusContext 修改 ReactiveProperty.Value 触发刷新，
    /// 验证 info-text / hp-bar-fill / hp-text / armor-text / energy-* / player-buff-bar 元素的可观察行为。
    /// 不依赖 Unity PlayerLoop 与配置表。
    /// </summary>
    [TestFixture]
    public class PlayerStatusViewTests
    {
        private VisualElement _root;
        private FakePlayerStatusContext _ctx;

        [SetUp]
        public void SetUp()
        {
            _root = BuildRoot();
            _ctx = new FakePlayerStatusContext();
        }

        /// <summary>
        /// 构造一棵只含 PlayerStatusView 关心的 7 个元素的最小 VisualElement 树，
        /// 模拟 GameUxml 加载后的根节点结构。
        /// </summary>
        private static VisualElement BuildRoot()
        {
            var root = new VisualElement();
            root.Add(new Label { name = "info-text" });
            root.Add(new VisualElement { name = "hp-bar-fill" });
            root.Add(new Label { name = "hp-text" });
            root.Add(new Label { name = "armor-text" });
            root.Add(new VisualElement { name = "energy-bar-fill" });
            root.Add(new Label { name = "energy-text" });
            root.Add(new VisualElement { name = "player-buff-bar" });
            return root;
        }

        [Test]
        public void HP百分比_PlayerHp变化时进度条按比例更新()
        {
            _ctx.PlayerMaxHp.Value = 100;
            _ctx.PlayerHp.Value = 40;

            var view = new PlayerStatusView(_root, _ctx);

            _ctx.PlayerHp.Value = 30;

            var fill = _root.Q("hp-bar-fill");
            Assert.AreEqual(LengthUnit.Percent, fill.style.width.value.unit);
            Assert.AreEqual(30f, fill.style.width.value.value, 0.01f);

            var hpText = _root.Q<Label>("hp-text");
            Assert.AreEqual("30/100", hpText.text);
            view.Dispose();
        }

        [Test]
        public void 阶段文本_PlayerTurn映射为你的回合()
        {
            var view = new PlayerStatusView(_root, _ctx);
            _ctx.Phase.Value = BattlePhase.PlayerTurn;

            Assert.AreEqual("你的回合", _root.Q<Label>("info-text").text);
            view.Dispose();
        }

        [Test]
        public void 阶段文本_关卡完成优先于Phase()
        {
            var view = new PlayerStatusView(_root, _ctx);
            _ctx.Phase.Value = BattlePhase.MonsterTurn;
            _ctx.IsLevelComplete.Value = true;

            Assert.AreEqual("关卡完成！", _root.Q<Label>("info-text").text);
            view.Dispose();
        }

        [Test]
        public void 阶段文本_玩家死亡次优()
        {
            var view = new PlayerStatusView(_root, _ctx);
            _ctx.Phase.Value = BattlePhase.PlayerTurn;
            _ctx.IsPlayerDead.Value = true;

            Assert.AreEqual("玩家死亡", _root.Q<Label>("info-text").text);
            view.Dispose();
        }

        [Test]
        public void 护甲文本_零时显示零()
        {
            var view = new PlayerStatusView(_root, _ctx);
            _ctx.PlayerArmor.Value = 0;

            // 触发 RefreshInfo —— 护甲字段需要主动变化（已在 SetUp 时为 0，需要先设非零再回零）
            _ctx.PlayerArmor.Value = 5;
            _ctx.PlayerArmor.Value = 0;
            Assert.AreEqual("0", _root.Q<Label>("armor-text").text);
            view.Dispose();
        }

        [Test]
        public void 护甲文本_大于零时显示数值()
        {
            var view = new PlayerStatusView(_root, _ctx);
            _ctx.PlayerArmor.Value = 7;

            Assert.AreEqual("7", _root.Q<Label>("armor-text").text);
            view.Dispose();
        }

        [Test]
        public void 空Buff列表_清空容器()
        {
            // 预填一个 child，验证刷新会清空
            _root.Q("player-buff-bar").Add(new Label("stale"));

            var view = new PlayerStatusView(_root, _ctx);
            _ctx.PlayerBuffs.Value = new List<BuffRuntime>();

            Assert.AreEqual(0, _root.Q("player-buff-bar").childCount);
            view.Dispose();
        }

        [Test]
        public void DoT_Buff渲染_含dot类与文本()
        {
            var view = new PlayerStatusView(_root, _ctx);
            var buff = new BuffRuntime { Kind = EffectKind.DamageDot, Value = 4, RemainingTurns = 2 };
            _ctx.PlayerBuffs.Value = new List<BuffRuntime> { buff };

            var bar = _root.Q("player-buff-bar");
            Assert.AreEqual(1, bar.childCount);
            var icon = bar[0] as Label;
            Assert.NotNull(icon);
            Assert.IsTrue(icon.ClassListContains("buff-icon"));
            Assert.IsTrue(icon.ClassListContains("buff-icon-dot"));
            Assert.AreEqual("4×2", icon.text);
            view.Dispose();
        }

        [Test]
        public void Dispose后_PlayerHp变化不再触发刷新()
        {
            _ctx.PlayerMaxHp.Value = 100;
            var view = new PlayerStatusView(_root, _ctx);
            // 触发首次同步：当前 HP=0/100
            _ctx.PlayerHp.Value = 50;
            string before = _root.Q<Label>("hp-text").text;

            view.Dispose();
            _ctx.PlayerHp.Value = 25;

            Assert.AreEqual(before, _root.Q<Label>("hp-text").text);
        }

        [Test]
        public void 重复Dispose_安全()
        {
            var view = new PlayerStatusView(_root, _ctx);
            view.Dispose();
            Assert.DoesNotThrow(() => view.Dispose());
        }
    }
}
