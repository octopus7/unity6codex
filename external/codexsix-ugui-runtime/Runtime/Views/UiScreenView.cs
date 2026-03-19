#nullable enable
using UnityEngine;

namespace CodexSix.UguiRuntime
{
    public abstract class UiScreenView : MonoBehaviour
    {
        public UiContext? Context { get; private set; }
        public string? ScreenId { get; private set; }

        internal void Attach(UiContext context, string screenId)
        {
            Context = context;
            ScreenId = screenId;
            OnAttached();
        }

        internal void NotifyShow()
        {
            OnShow();
        }

        internal void NotifyHide()
        {
            OnHide();
        }

        internal void NotifyFocus()
        {
            OnFocus();
        }

        internal void NotifyBlur()
        {
            OnBlur();
        }

        protected virtual void OnAttached()
        {
        }

        protected virtual void OnShow()
        {
        }

        protected virtual void OnHide()
        {
        }

        protected virtual void OnFocus()
        {
        }

        protected virtual void OnBlur()
        {
        }
    }
}
