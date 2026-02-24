using System;

namespace CodexSix.UiKit.Runtime
{
    public interface IShortcutService
    {
        void Register(ShortcutBinding binding, Action handler);
        void Unregister(string bindingId);
        bool Process(InputContext context);
    }
}