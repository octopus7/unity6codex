using NUnit.Framework;

namespace CodexSix.UiKit.Runtime.Tests.Editor
{
    public sealed class UiScreenServiceTests
    {
        [Test]
        public void ShowScreen_ChangesCurrentScreen_AndBackRestoresPrevious()
        {
            var service = new UiScreenService();

            service.ShowScreen("hud");
            service.ShowScreen("inventory");

            Assert.AreEqual("inventory", service.CurrentScreenId);
            Assert.IsTrue(service.TryGoBack());
            Assert.AreEqual("hud", service.CurrentScreenId);
        }

        [Test]
        public void TryGoBack_ReturnsFalse_WhenHistoryEmpty()
        {
            var service = new UiScreenService();
            service.ShowScreen("hud");

            Assert.IsFalse(service.TryGoBack());
        }
    }
}