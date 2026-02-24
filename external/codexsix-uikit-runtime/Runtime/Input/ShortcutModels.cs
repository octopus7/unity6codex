using UnityEngine.InputSystem;

namespace CodexSix.UiKit.Runtime
{
    public enum ShortcutScope
    {
        Global = 0,
        Gameplay = 1,
        Ui = 2
    }

    public enum ShortcutTrigger
    {
        PressedThisFrame = 0,
        Held = 1
    }

    public enum UiFocusState
    {
        None = 0,
        TextInput = 1,
        PointerCapture = 2
    }

    public readonly record struct ShortcutBinding(
        string BindingId,
        string ActionId,
        Key Key,
        ShortcutScope Scope,
        ShortcutTrigger Trigger);
}