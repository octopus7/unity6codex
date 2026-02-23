using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class TopDownCameraFollow : MonoBehaviour
    {
        public NetworkGameClient Client;
        public Vector3 Offset = new(0f, 18f, -10f);
        public float PositionSmooth = 12f;
        public Vector3 FixedEuler = new(60f, 0f, 0f);

        private void LateUpdate()
        {
            transform.rotation = Quaternion.Euler(FixedEuler);

            if (Client == null || !Client.TryGetLocalPlayerPosition(out var playerPosition))
            {
                return;
            }

            var desired = playerPosition + Offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-PositionSmooth * Time.deltaTime));
        }
    }
}
