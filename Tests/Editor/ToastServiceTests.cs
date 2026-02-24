using System.Collections.Generic;
using NUnit.Framework;

namespace CodexSix.UiKit.Runtime.Tests.Editor
{
    public sealed class ToastServiceTests
    {
        [Test]
        public void Enqueue_DuplicateKey_MergesEntry()
        {
            double now = 0;
            var service = new ToastService(new ToastServiceOptions(maxVisible: 2, queueTtlSeconds: 10f), () => now);

            var first = service.Enqueue(new ToastRequest("coins", "Coins: 10", 2f, ToastPriority.Normal));
            var second = service.Enqueue(new ToastRequest("coins", "Coins: 15", 2f, ToastPriority.Normal));

            var active = service.SnapshotActiveToasts();
            Assert.AreEqual(first, second);
            Assert.AreEqual(1, active.Count);
            Assert.AreEqual("Coins: 15", active[0].Message);
        }

        [Test]
        public void QueueTtl_ExpiresPendingToast()
        {
            double now = 0;
            var service = new ToastService(new ToastServiceOptions(maxVisible: 1, queueTtlSeconds: 1f), () => now);

            service.Enqueue(new ToastRequest("one", "One", 10f, ToastPriority.Normal));
            service.Enqueue(new ToastRequest("two", "Two", 2f, ToastPriority.Normal));

            now = 2.1;
            service.Tick();

            var active = service.SnapshotActiveToasts();
            Assert.AreEqual(1, active.Count);
            Assert.AreEqual("One", active[0].Message);

            now = 10.5;
            service.Tick();
            var afterExpire = service.SnapshotActiveToasts();
            Assert.AreEqual(0, afterExpire.Count);
        }

        [Test]
        public void Dismiss_RemovesToast()
        {
            double now = 0;
            var service = new ToastService(new ToastServiceOptions(maxVisible: 2, queueTtlSeconds: 10f), () => now);
            var handle = service.Enqueue(new ToastRequest("dismiss", "Dismiss me", 5f, ToastPriority.Low));

            Assert.IsTrue(service.Dismiss(handle));
            Assert.AreEqual(0, service.SnapshotActiveToasts().Count);
        }
    }
}