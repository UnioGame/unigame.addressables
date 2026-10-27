namespace UniGame.AddressableTools.Tests
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using NUnit.Framework;
    using UniGame.AddressableTools.Runtime;
    using UniGame.Runtime.DataFlow;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.ResourceManagement;
    using UnityEngine.ResourceManagement.AsyncOperations;
    using UnityEngine.TestTools;

    /// <summary>ADDR-03: cancel/retry, late results and release counts of <c>LoadReferenceAsync</c>, with an operation the test completes by hand</summary>
    public class AddressableLoadReferenceTests
    {
        private const string Key = "addr03/test-asset";

        private sealed class ControlledOp : AsyncOperationBase<Object>
        {
            public int DestroyCount;
            public readonly Texture2D Asset = new Texture2D(1, 1);

            protected override void Execute() { }

            protected override void Destroy() => DestroyCount++;

            public void Succeed() => Complete(Asset, true, string.Empty);

            public void Fail()
            {
                Complete(null, false, "forced failure");
                PumpResourceManager();
            }
        }

        /// <summary>
        /// A failed operation reports through the ResourceManager's deferred callbacks, which only its play-mode
        /// MonoBehaviour hook drives; edit mode pumps them by hand (ResourceManager.Update is internal).
        /// </summary>
        private static void PumpResourceManager()
        {
            var update = typeof(ResourceManager).GetMethod("Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            update?.Invoke(Addressables.ResourceManager, new object[] { 0f });
        }

        private readonly List<ControlledOp> _ops = new List<ControlledOp>();
        private readonly List<LifeTime> _lifeTimes = new List<LifeTime>();

        [SetUp]
        public void SetUp()
        {
            AddressableExtensions.ResetAddressableData();
            _ops.Clear();
            AddressableLoadStarter<Object>.Start = key =>
            {
                var op = new ControlledOp();
                _ops.Add(op);
                return Addressables.ResourceManager.StartOperation(op, default);
            };
        }

        [TearDown]
        public void TearDown()
        {
            AddressableLoadStarter<Object>.Reset();
            foreach (var lifeTime in _lifeTimes) lifeTime.Terminate();
            _lifeTimes.Clear();
            foreach (var op in _ops) Object.DestroyImmediate(op.Asset);
            AddressableExtensions.ResetAddressableData();
        }

        private LifeTime NewLifeTime()
        {
            var lifeTime = new LifeTime();
            _lifeTimes.Add(lifeTime);
            return lifeTime;
        }

        private static UniTask Frames(int count) => UniTask.DelayFrame(count);

        [UnityTest]
        public IEnumerator Load_Succeeds_And_LifetimeEnd_ReleasesOnce() => UniTask.ToCoroutine(async () =>
        {
            var life = NewLifeTime();
            var task = Key.LoadReferenceAsync<Object>(life, life.Token).AsTask();
            await Frames(2);
            Assert.AreEqual(1, _ops.Count);

            _ops[0].Succeed();
            var result = await task;

            Assert.IsTrue(result.Success);
            Assert.AreSame(_ops[0].Asset, result.Result);
            Assert.AreEqual(0, _ops[0].DestroyCount, "alive while the lifetime is alive");

            life.Terminate();
            await Frames(2);
            Assert.AreEqual(1, _ops[0].DestroyCount, "released exactly once with the lifetime");
        });

        [UnityTest]
        public IEnumerator CancelThenRetry_SameAddress_Succeeds() => UniTask.ToCoroutine(async () =>
        {
            var life1 = NewLifeTime();
            var cts = new CancellationTokenSource();
            var first = Key.LoadReferenceAsync<Object>(life1, cts.Token).AsTask();
            await Frames(2);
            cts.Cancel();
            var firstResult = await first;

            Assert.IsFalse(firstResult.Success);
            Assert.AreEqual(AddressableLoadResult.CancelledMessage, firstResult.Error);

            var life2 = NewLifeTime();
            var second = Key.LoadReferenceAsync<Object>(life2, life2.Token).AsTask();
            await Frames(2);
            Assert.AreEqual(2, _ops.Count, "the retry starts a fresh load instead of reusing the cancelled one");

            _ops[1].Succeed();
            var secondResult = await second;

            Assert.IsTrue(secondResult.Success, secondResult.Error);
            Assert.AreSame(_ops[1].Asset, secondResult.Result);
            //Addressables destroys a released, still running operation when it completes
            _ops[0].Succeed();
            await Frames(2);
            Assert.AreEqual(1, _ops[0].DestroyCount, "the cancelled handle was released once");
            Assert.AreEqual(0, _ops[1].DestroyCount);
        });

        [UnityTest]
        public IEnumerator LateResult_AfterRequesterTerminated_IsReleasedOnceAndNotDelivered() => UniTask.ToCoroutine(async () =>
        {
            var life = NewLifeTime();
            var task = Key.LoadReferenceAsync<Object>(life, CancellationToken.None).AsTask();
            await Frames(2);

            life.Terminate();
            _ops[0].Succeed();
            var result = await task;
            await Frames(2);

            Assert.IsFalse(result.Success, "a result that arrives after its requester ended is not delivered");
            Assert.AreEqual(AddressableLoadResult.CancelledMessage, result.Error);
            Assert.AreEqual(1, _ops[0].DestroyCount, "released exactly once (no leak, no double release)");

            var retryLife = NewLifeTime();
            var retry = Key.LoadReferenceAsync<Object>(retryLife, retryLife.Token).AsTask();
            await Frames(2);
            _ops[1].Succeed();
            Assert.IsTrue((await retry).Success, "a late result does not poison the address");
        });

        [UnityTest]
        public IEnumerator LateResult_AfterTokenCancelled_IsReleasedOnceAndNotDelivered() => UniTask.ToCoroutine(async () =>
        {
            var life = NewLifeTime();
            var cts = new CancellationTokenSource();
            var task = Key.LoadReferenceAsync<Object>(life, cts.Token).AsTask();
            await Frames(2);

            cts.Cancel();
            _ops[0].Succeed();
            var result = await task;
            await Frames(2);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, _ops[0].DestroyCount, "released exactly once");
        });

        [UnityTest]
        public IEnumerator OneWaiterCancelled_OtherWaiterStillLoads() => UniTask.ToCoroutine(async () =>
        {
            var lifeA = NewLifeTime();
            var lifeB = NewLifeTime();
            var ctsA = new CancellationTokenSource();

            var a = Key.LoadReferenceAsync<Object>(lifeA, ctsA.Token).AsTask();
            await Frames(2);
            var b = Key.LoadReferenceAsync<Object>(lifeB, lifeB.Token).AsTask();
            await Frames(2);

            ctsA.Cancel();
            var resultA = await a;
            await Frames(3);
            Assert.IsFalse(resultA.Success);
            Assert.AreEqual(2, _ops.Count, "waiter B took the load over");

            _ops[1].Succeed();
            var resultB = await b;

            Assert.IsTrue(resultB.Success, resultB.Error);
            Assert.AreEqual(0, _ops[1].DestroyCount);
        });

        [UnityTest]
        public IEnumerator FailedLoad_ReleasesHandle_AndRetryStartsFresh() => UniTask.ToCoroutine(async () =>
        {
            var life = NewLifeTime();
            var task = Key.LoadReferenceAsync<Object>(life, life.Token).AsTask();
            await Frames(2);

            LogAssert.ignoreFailingMessages = true;
            _ops[0].Fail();
            var result = await task;
            LogAssert.ignoreFailingMessages = false;
            await Frames(2);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, _ops[0].DestroyCount, "the failed handle is released (no leak)");

            var retry = Key.LoadReferenceAsync<Object>(life, life.Token).AsTask();
            await Frames(2);
            Assert.AreEqual(2, _ops.Count, "no stale cache entry: the retry loads again");
            _ops[1].Succeed();
            Assert.IsTrue((await retry).Success);
        });

        [UnityTest]
        public IEnumerator SecondRequester_ReusesLoadedHandle_AndBothReleaseToZero() => UniTask.ToCoroutine(async () =>
        {
            var lifeA = NewLifeTime();
            var lifeB = NewLifeTime();
            var a = Key.LoadReferenceAsync<Object>(lifeA, lifeA.Token).AsTask();
            await Frames(2);
            _ops[0].Succeed();
            Assert.IsTrue((await a).Success);

            var resultB = await Key.LoadReferenceAsync<Object>(lifeB, lifeB.Token);
            Assert.IsTrue(resultB.Success);
            Assert.AreEqual(1, _ops.Count, "one load serves both requesters");

            lifeA.Terminate();
            await Frames(2);
            Assert.AreEqual(0, _ops[0].DestroyCount, "still held by the second requester");
            lifeB.Terminate();
            await Frames(2);
            Assert.AreEqual(1, _ops[0].DestroyCount);
        });
    }
}
