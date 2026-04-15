#nullable enable

using UnityEngine;

namespace McpTest.VoxelVillage
{
    [DisallowMultipleComponent]
    public sealed class VoxelVillageCoinPickup : MonoBehaviour
    {
        [SerializeField] float _rotationSpeed = 180f;
        [SerializeField] float _bobAmplitude = 0.14f;
        [SerializeField] float _bobFrequency = 3.25f;
        [SerializeField] float _pickupRadius = 0.95f;

        float _baseY;
        float _phaseOffset;
        bool _hasBaseHeight;

        public float PickupRadius => Mathf.Max(0.1f, _pickupRadius);

        void Awake()
        {
            EnsureBaseHeight();
        }

        void OnEnable()
        {
            EnsureBaseHeight();
        }

        public void SetBaseHeight(float baseHeight)
        {
            _baseY = baseHeight;
            _phaseOffset = ComputePhaseOffset(transform.position);
            _hasBaseHeight = true;
        }

        void Update()
        {
            EnsureBaseHeight();

            transform.Rotate(0f, _rotationSpeed * Time.deltaTime, 0f, Space.World);

            var position = transform.position;
            position.y = _baseY + (Mathf.Sin((Time.time * _bobFrequency) + _phaseOffset) * _bobAmplitude);
            transform.position = position;
        }

        void EnsureBaseHeight()
        {
            if (_hasBaseHeight)
            {
                return;
            }

            _baseY = transform.position.y;
            _phaseOffset = ComputePhaseOffset(transform.position);
            _hasBaseHeight = true;
        }

        static float ComputePhaseOffset(Vector3 position)
        {
            return Mathf.Repeat((position.x * 0.37f) + (position.z * 0.19f), Mathf.PI * 2f);
        }
    }
}
