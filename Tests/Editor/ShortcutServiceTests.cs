using NUnit.Framework;
using UnityEngine.InputSystem;

namespace CodexSix.UiKit.Runtime.Tests.Editor
{
    public sealed class ShortcutServiceTests
    {
        [Test]
        public void TextInputFocus_BlocksGlobalShortcut()
        {
            var service = new ShortcutService(includeDefaultActionKeys: false);
            var called = false;

            service.Register(
                new ShortcutBinding("global.escape", "close", Key.Escape, ShortcutScope.Global, ShortcutTrigger.PressedThisFrame),
                () => called = true);

            var context = new InputContext(
                focusState: UiFocusState.TextInput,
                isModalOpen: false,
                pressedThisFrame: new[] { Key.Escape });

            service.Process(context);
            Assert.IsFalse(called);
        }

        [Test]
        public void ModalOpen_BlocksGameplayShortcut()
        {
            var service = new ShortcutService(includeDefaultActionKeys: false);
            var called = false;

            service.Register(
                new ShortcutBinding("game.jump", "jump", Key.Space, ShortcutScope.Gameplay, ShortcutTrigger.PressedThisFrame),
                () => called = true);

            var context = new InputContext(
                focusState: UiFocusState.None,
                isModalOpen: true,
                pressedThisFrame: new[] { Key.Space });

            service.Process(context);
            Assert.IsFalse(called);
        }

        [Test]
        public void PressedThisFrame_InvokesHandler()
        {
            var service = new ShortcutService(includeDefaultActionKeys: false);
            var callCount = 0;

            service.Register(
                new ShortcutBinding("ui.inventory", "inventory", Key.I, ShortcutScope.Ui, ShortcutTrigger.PressedThisFrame),
                () => callCount++);

            var context = new InputContext(
                focusState: UiFocusState.None,
                isModalOpen: false,
                pressedThisFrame: new[] { Key.I });

            var handled = service.Process(context);

            Assert.IsTrue(handled);
            Assert.AreEqual(1, callCount);
        }
    }
}