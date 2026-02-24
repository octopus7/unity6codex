using System.Threading.Tasks;
using NUnit.Framework;

namespace CodexSix.UiKit.Runtime.Tests.Editor
{
    public sealed class PopupServiceTests
    {
        [Test]
        public async Task ShowWithTimeoutAsync_ReturnsTimeout_WhenDelayWins()
        {
            var modalService = new UiModalService();
            var popupService = new PopupService(modalService, (milliseconds, token) => Task.CompletedTask);
            var request = new PopupRequest("timeout", "Timeout", "Body", "OK", "Cancel");

            var result = await popupService.ShowWithTimeoutAsync(request, timeoutSeconds: 1f);

            Assert.AreEqual(PopupResultKind.Timeout, result.Kind);
            Assert.AreEqual(request.Id, result.PopupId);
            Assert.AreEqual(0, modalService.ModalDepth);
        }
    }
}