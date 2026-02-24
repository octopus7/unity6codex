using NUnit.Framework;

namespace CodexSix.UiKit.Runtime.Tests.Editor
{
    public sealed class UiSignalBusTests
    {
        [Test]
        public void Publish_DeliversOnlyMatchingSignalType()
        {
            var bus = new UiSignalBus();
            var intCount = 0;
            var stringCount = 0;

            using var intSub = bus.Subscribe<int>(_ => intCount++);
            using var stringSub = bus.Subscribe<string>(_ => stringCount++);

            bus.Publish(7);
            bus.Publish("hello");

            Assert.AreEqual(1, intCount);
            Assert.AreEqual(1, stringCount);
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            var bus = new UiSignalBus();
            var count = 0;

            var sub = bus.Subscribe<int>(_ => count++);
            bus.Publish(1);
            sub.Dispose();
            bus.Publish(2);

            Assert.AreEqual(1, count);
        }
    }
}