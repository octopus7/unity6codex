using NUnit.Framework;

namespace CodexSix.UiKit.Runtime.Tests.Editor
{
    public sealed class UiStateChannelTests
    {
        [Test]
        public void Subscribe_ReplaysLatestValue_WhenRequested()
        {
            var channel = new UiStateChannel<int>();
            channel.Publish(42);

            var observed = -1;
            using var subscription = channel.Subscribe(value => observed = value, replayLatest: true);

            Assert.AreEqual(42, observed);
        }

        [Test]
        public void Dispose_UnsubscribesListener()
        {
            var channel = new UiStateChannel<int>();
            var callCount = 0;

            var subscription = channel.Subscribe(_ => callCount++);
            channel.Publish(1);
            subscription.Dispose();
            channel.Publish(2);

            Assert.AreEqual(1, callCount);
        }
    }
}