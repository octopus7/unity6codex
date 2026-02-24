using UnityEngine;

namespace CodexSix.UiKit.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UiRoot))]
    public sealed class UiDocumentBinder : MonoBehaviour
    {
        [SerializeField] private UiRoot _uiRoot;

        private void OnEnable()
        {
            if (_uiRoot == null)
            {
                _uiRoot = GetComponent<UiRoot>();
            }

            _uiRoot?.EnsureBuilt();
        }
    }
}