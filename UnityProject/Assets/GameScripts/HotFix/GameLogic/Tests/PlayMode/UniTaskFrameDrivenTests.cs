using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using EF.Common;
using EF.Timer;
using GameLogic.Tests.PlayMode.Framework;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameLogic.Tests.PlayMode
{
    /// <summary>
    /// 验证 UniTask 与 TimerManager 在 PlayMode PlayerLoop 真实运行时下的帧驱动行为，
    /// 确认它们在 PlayMode 下与 EditMode（同步即时执行）完全不同。
    /// </summary>
    public sealed class UniTaskFrameDrivenTests : PlayModeTestBase
    {
        private TimerManager _timerManager;

        /// <summary>
        /// 注册 TimerManager；每个测试自己负责调 ModuleSystem.Update 推进时钟，
        /// 因为 PlayMode 测试环境里没有 GameEntry MonoBehaviour 自动每帧调度。
        /// </summary>
        protected override UniTask OnSetUpAsync()
        {
            _timerManager = new TimerManager();
            ModuleSystem.Register<ITimerManager>(_timerManager);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// PlayMode 下 await UniTask.Yield(PlayerLoopTiming.Update) 必须真正推进至少一帧。
        /// </summary>
        [UnityTest]
        public IEnumerator UniTaskYield_AdvancesAtLeastOneFrame() => UniTask.ToCoroutine(async () =>
        {
            int beforeFrame = Time.frameCount;
            await UniTask.Yield(PlayerLoopTiming.Update);
            int afterFrame = Time.frameCount;

            Assert.GreaterOrEqual(afterFrame - beforeFrame, 1,
                $"UniTask.Yield 应至少推进 1 帧，实际推进 {afterFrame - beforeFrame}");
        });

        /// <summary>
        /// PlayMode 下 UniTask.Delay 应消耗真实时间（容许 10% 抖动下界）。
        /// </summary>
        [UnityTest]
        public IEnumerator UniTaskDelay_ConsumesRealTime() => UniTask.ToCoroutine(async () =>
        {
            const float requestedSeconds = 0.2f;
            const float minimumExpectedSeconds = requestedSeconds * 0.9f;

            float startTime = Time.realtimeSinceStartup;
            await UniTask.Delay(TimeSpan.FromSeconds(requestedSeconds));
            float elapsed = Time.realtimeSinceStartup - startTime;

            Assert.GreaterOrEqual(elapsed, minimumExpectedSeconds,
                $"UniTask.Delay 应至少消耗 {minimumExpectedSeconds:F3}s，实际 {elapsed:F3}s");
        });

        /// <summary>
        /// TimerManager 在真实帧驱动下，0.1s 后触发的回调，在测试持续推进 ModuleSystem.Update 0.2s 后应已被调用恰好一次。
        /// </summary>
        [UnityTest]
        public IEnumerator TimerManager_ScheduleOnceFiresWithinExpectedWindow() => UniTask.ToCoroutine(async () =>
        {
            const float scheduledDelay = 0.1f;
            const float waitWindow = 0.3f;
            int firedCount = 0;

            int timerId = _timerManager.ScheduleOnce(scheduledDelay, () => firedCount++);
            Assert.IsTrue(_timerManager.Exists(timerId), "Timer 注册后应存在");

            // 持续推进 PlayerLoop 直到达到等待窗口；每帧调 ModuleSystem.Update 模拟生产 GameEntry 的更新。
            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < waitWindow)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                ModuleSystem.Update(Time.deltaTime, Time.unscaledDeltaTime);
            }

            Assert.AreEqual(1, firedCount,
                $"ScheduleOnce 应当在 0.1s~0.3s 之间触发恰好一次，实际触发 {firedCount} 次");
            Assert.IsFalse(_timerManager.Exists(timerId),
                "一次性计时器触发后应自动清除");
        });
    }
}
