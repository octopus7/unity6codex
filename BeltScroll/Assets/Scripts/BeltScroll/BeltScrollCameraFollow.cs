using UnityEngine;

namespace BeltScroll
{
    [RequireComponent(typeof(Camera))]
    public sealed class BeltScrollCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 xBounds = new Vector2(-50f, 50f);
        [SerializeField] private float fixedY;
        [SerializeField] private float followSharpness = 18f;

        private Camera targetCamera;

        public void Configure(Transform followTarget, Vector2 horizontalBounds, float cameraY)
        {
            target = followTarget;
            xBounds = horizontalBounds;
            fixedY = cameraY;
        }

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            var halfWidth = targetCamera.orthographicSize * targetCamera.aspect;
            var minX = xBounds.x + halfWidth;
            var maxX = xBounds.y - halfWidth;
            var desiredX = minX <= maxX
                ? Mathf.Clamp(target.position.x, minX, maxX)
                : (xBounds.x + xBounds.y) * 0.5f;

            var current = transform.position;
            var t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            current.x = Mathf.Lerp(current.x, desiredX, t);
            current.y = fixedY;
            transform.position = current;
        }
    }
}
