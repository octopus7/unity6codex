#nullable enable
using CodexSix.UguiRuntime;
using NUnit.Framework;

namespace CodexSix.UguiRuntime.Tests.Editor
{
    public sealed class UiInputDriverTests
    {
        [Test]
        public void Escape_PrefersModalCancel_ThenFallsBackToScreenBack()
        {
            var modal = new FakeModalService { ModalDepthValue = 1, CancelTopResult = true };
            var screens = new FakeScreenService { GoBackResult = true };
            var driver = new UiInputDriver(modal, screens);

            Assert.IsTrue(driver.ProcessEscape());
            Assert.AreEqual(1, modal.CancelCount);
            Assert.AreEqual(0, screens.GoBackCount);

            modal.ModalDepthValue = 0;

            Assert.IsTrue(driver.ProcessEscape());
            Assert.AreEqual(1, screens.GoBackCount);
        }

        [Test]
        public void Confirm_OnlyActsWhenModalExists()
        {
            var modal = new FakeModalService { ModalDepthValue = 0, ConfirmTopResult = true };
            var screens = new FakeScreenService();
            var driver = new UiInputDriver(modal, screens);

            Assert.IsFalse(driver.ProcessConfirm());
            Assert.AreEqual(0, modal.ConfirmCount);

            modal.ModalDepthValue = 1;

            Assert.IsTrue(driver.ProcessConfirm());
            Assert.AreEqual(1, modal.ConfirmCount);
        }

        private sealed class FakeScreenService : IUiScreenService
        {
            public string? CurrentScreenId => "hud";
            public event System.Action<string?>? ScreenChanged;
            public bool GoBackResult { get; set; }
            public int GoBackCount { get; private set; }

            public void Show(string screenId)
            {
                ScreenChanged?.Invoke(screenId);
            }

            public bool TryGoBack()
            {
                GoBackCount++;
                return GoBackResult;
            }
        }

        private sealed class FakeModalService : IUiModalService
        {
            public int ModalDepthValue { get; set; }
            public bool ConfirmTopResult { get; set; }
            public bool CancelTopResult { get; set; }

            public int ConfirmCount { get; private set; }
            public int CancelCount { get; private set; }

            public int ModalDepth => ModalDepthValue;
            public UiPopupRequest? TopRequest => null;
            public event System.Action<int>? ModalDepthChanged;

            public System.Threading.Tasks.Task<UiPopupResult> ShowAsync(UiPopupRequest request, System.Threading.CancellationToken ct = default)
            {
                throw new System.NotSupportedException();
            }

            public bool TryConfirmTop()
            {
                ConfirmCount++;
                return ConfirmTopResult;
            }

            public bool TryCancelTop()
            {
                CancelCount++;
                return CancelTopResult;
            }

            public bool TryDismissTop(UiPopupDismissReason reason = UiPopupDismissReason.Back)
            {
                CancelCount++;
                return CancelTopResult;
            }
        }
    }
}
