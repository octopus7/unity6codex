using UnityEngine;

namespace Labs.WaterShorelineLab
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ShorelineDepthCameraMode : MonoBehaviour
    {
        private void OnEnable()
        {
            ApplyDepthMode();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyDepthMode();
        }
#endif

        private void ApplyDepthMode()
        {
            var targetCamera = GetComponent<Camera>();
            if (targetCamera == null)
            {
                return;
            }

            targetCamera.depthTextureMode |= DepthTextureMode.Depth;
        }
    }
}
