using UnityEngine;

namespace CodexSix.UiKit.Runtime
{
    internal static class UiPersistentChannels
    {
        private static ToastService _persistentToasts;

        public static ToastService PersistentToasts =>
            _persistentToasts ??= new ToastService(
                new ToastServiceOptions(maxVisible: 3, queueTtlSeconds: 15f),
                () => Time.unscaledTimeAsDouble);
    }
}