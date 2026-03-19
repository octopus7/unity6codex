#nullable enable
using UnityEngine;

namespace CodexSix.UguiRuntime
{
    public abstract class UiPopupView : MonoBehaviour
    {
        public UiContext? Context { get; private set; }
        public UiPopupRequest Request { get; private set; }
        public UiModalHandle Handle { get; private set; }

        internal void Attach(UiContext context)
        {
            Context = context;
        }

        public virtual void Bind(UiPopupRequest request, UiModalHandle handle)
        {
            Request = request;
            Handle = handle;
            OnBound();
        }

        protected virtual void OnBound()
        {
        }
    }
}
