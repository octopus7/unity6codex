#nullable enable

using UnityEngine;

namespace McpTest.Bowling
{
    [DisallowMultipleComponent]
    public sealed class BowlingPin : MonoBehaviour
    {
        Rigidbody _rigidbody = null!;
        float _baseHeight;

        public int PinIndex { get; private set; }

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _baseHeight = transform.position.y;
        }

        public void Configure(int pinIndex)
        {
            PinIndex = pinIndex;
            _baseHeight = transform.position.y;
        }

        public bool IsStanding(float maxTiltDegrees)
        {
            return transform.position.y > _baseHeight * 0.6f &&
                Vector3.Angle(transform.up, Vector3.up) <= maxTiltDegrees;
        }

        public bool IsSettled(float linearThreshold, float angularThreshold)
        {
            return _rigidbody.IsSleeping() ||
                (_rigidbody.linearVelocity.sqrMagnitude <= linearThreshold * linearThreshold &&
                    _rigidbody.angularVelocity.sqrMagnitude <= angularThreshold * angularThreshold);
        }
    }
}
