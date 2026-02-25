using UnityEngine;

namespace CodexSix.TerrainMeshMovementLab
{
    [DisallowMultipleComponent]
    public sealed class TerrainLabCameraFollow : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset = new(0f, 28f, -24f);
        [Min(0f)] public float FollowSharpness = 8f;
        public bool LookAtTarget = true;

        private void LateUpdate()
        {
            if (Target == null)
            {
                return;
            }

            var targetPosition = Target.position + Offset;
            var t = 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);

            if (LookAtTarget)
            {
                var lookPoint = Target.position;
                transform.rotation = Quaternion.LookRotation((lookPoint - transform.position).normalized, Vector3.up);
            }
        }
    }
}
