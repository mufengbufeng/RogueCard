using System;
using System.Reflection;
using GameConfig.card;
using GameConfig.monster;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// UGUI EditMode 测试对象工厂，统一创建 TMP、Image、Button 和配置表对象。
    /// </summary>
    internal static class UguiTestFactory
    {
        /// <summary>
        /// 创建带 RectTransform 的空对象。
        /// </summary>
        public static GameObject CreateRectObject(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            return go;
        }

        /// <summary>
        /// 创建 TMP 文本。
        /// </summary>
        public static TextMeshProUGUI CreateText(string name, Transform parent = null)
        {
            var go = CreateRectObject(name, parent);
            return go.AddComponent<TextMeshProUGUI>();
        }

        /// <summary>
        /// 创建 Image。
        /// </summary>
        public static Image CreateImage(string name, Transform parent = null)
        {
            var go = CreateRectObject(name, parent);
            return go.AddComponent<Image>();
        }

        /// <summary>
        /// 创建 Button。
        /// </summary>
        public static Button CreateButton(string name, Transform parent = null)
        {
            var go = CreateRectObject(name, parent);
            go.AddComponent<Image>();
            return go.AddComponent<Button>();
        }

        /// <summary>
        /// 创建 Buff 或意图图标模板。
        /// </summary>
        public static GameObject CreateIconTemplate(string name, Transform parent = null)
        {
            var root = CreateRectObject(name, parent);
            root.AddComponent<Image>();
            CreateText("Text", root.transform);
            root.SetActive(false);
            return root;
        }

        /// <summary>
        /// 创建手牌模板。
        /// </summary>
        public static GameObject CreateCardTemplate(string name, Transform parent = null)
        {
            var root = CreateRectObject(name, parent);
            root.AddComponent<Image>();
            root.AddComponent<CanvasGroup>();
            CreateText("CardNameText", root.transform);
            CreateText("CardCostText", root.transform);
            root.SetActive(false);
            return root;
        }

        /// <summary>
        /// 创建怪物项模板。
        /// </summary>
        public static GameObject CreateMonsterTemplate(string name, Transform parent = null)
        {
            var root = CreateRectObject(name, parent);
            root.AddComponent<Image>();
            root.AddComponent<Button>();
            CreateText("NameText", root.transform);
            var hpFill = CreateImage("HpFill", root.transform);
            hpFill.type = Image.Type.Filled;
            CreateText("HpText", root.transform);
            CreateRectObject("IntentBar", root.transform);
            CreateRectObject("BuffBar", root.transform);
            var highlight = CreateRectObject("TargetHighlight", root.transform);
            highlight.SetActive(false);
            root.SetActive(false);
            return root;
        }

        /// <summary>
        /// 创建测试卡牌配置。
        /// </summary>
        public static Card NewCard(int id, string name, int cost, TargetMode targetMode, CardReleaseKind releaseKind = CardReleaseKind.Spell)
        {
            var card = (Card)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Card));
            SetField(card, "Id", id);
            SetField(card, "Name", name);
            SetField(card, "Desc", string.Empty);
            SetField(card, "Cost", cost);
            SetField(card, "OwnerKind", OwnerKind.Player);
            SetField(card, "CardReleaseKind", releaseKind);
            SetField(card, "TargetMode", targetMode);
            SetField(card, "TargetCount", 1);
            SetField(card, "IsBasic", true);
            SetField(card, "AssetId", string.Empty);
            return card;
        }

        /// <summary>
        /// 创建测试卡牌效果配置。
        /// </summary>
        public static CardEffect NewEffect(int cardId, EffectKind kind, int value, int duration = 0)
        {
            var effect = (CardEffect)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(CardEffect));
            SetField(effect, "Id", cardId * 100 + (int)kind);
            SetField(effect, "CardId", cardId);
            SetField(effect, "Kind", kind);
            SetField(effect, "TriggerTiming", EffectTriggerTiming.Immediate);
            SetField(effect, "Value", value);
            SetField(effect, "Duration", duration);
            SetField(effect, "Comment", string.Empty);
            return effect;
        }

        /// <summary>
        /// 创建测试怪物配置。
        /// </summary>
        public static Monster NewMonsterConfig(int id, string name)
        {
            var monster = (Monster)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Monster));
            SetField(monster, "Id", id);
            SetField(monster, "Name", name);
            SetField(monster, "Desc", string.Empty);
            SetField(monster, "MaxHp", 100);
            SetField(monster, "MaxEnergy", 3);
            SetField(monster, "HandLimit", 5);
            SetField(monster, "AssetId", string.Empty);
            return monster;
        }

        /// <summary>
        /// 设置 Luban 只读字段。
        /// </summary>
        public static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            field.SetValue(target, value);
        }
    }
}
