using System.Threading.Tasks;
using NUnit.Framework;

namespace CodexSix.UiKit.Runtime.Tests.Editor
{
    public sealed class UiModalServiceTests
    {
        [Test]
        public async Task ShowAsync_ConfirmTop_ReturnsConfirmedResult()
        {
            var service = new UiModalService();
            var request = new PopupRequest("confirm_case", "Title", "Body", "OK", "Cancel");

            var pending = service.ShowAsync(request);
            Assert.AreEqual(1, service.ModalDepth);

            Assert.IsTrue(service.TryConfirmTop());
            var result = await pending;

            Assert.AreEqual(PopupResultKind.Confirmed, result.Kind);
            Assert.AreEqual(request.Id, result.PopupId);
            Assert.AreEqual(0, service.ModalDepth);
        }

        [Test]
        public async Task TryDismissTop_Back_ReturnsCancelled()
        {
            var service = new UiModalService();
            var request = new PopupRequest("cancel_case", "Title", "Body", "OK", "Cancel");

            var pending = service.ShowAsync(request);
            Assert.IsTrue(service.TryDismissTop(PopupDismissReason.Back));
            var result = await pending;

            Assert.AreEqual(PopupResultKind.Cancelled, result.Kind);
        }

        [Test]
        public async Task TryDismiss_ById_ReturnsDismissed()
        {
            var service = new UiModalService();
            var request = new PopupRequest("dismiss_case", "Title", "Body", "OK", "Cancel");

            var pending = service.ShowAsync(request);
            Assert.IsTrue(service.TryDismiss(request.Id, PopupDismissReason.Programmatic));
            var result = await pending;

            Assert.AreEqual(PopupResultKind.Dismissed, result.Kind);
        }
    }
}