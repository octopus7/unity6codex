using System;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class RespawnBurstEffectInstance : MonoBehaviour
    {
        [SerializeField]
        private ParticleSystem[] _particleSystems = Array.Empty<ParticleSystem>();

        public float SuggestedDurationSeconds { get; private set; } = 1f;

        private void Awake()
        {
            CacheIfNeeded();
        }

        public void CacheIfNeeded()
        {
            if (_particleSystems == null || _particleSystems.Length == 0)
            {
                _particleSystems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            }

            SuggestedDurationSeconds = ComputeSuggestedDuration(_particleSystems);
        }

        public void PlayAt(Vector3 worldPosition)
        {
            CacheIfNeeded();

            transform.position = worldPosition;
            gameObject.SetActive(true);

            for (var i = 0; i < _particleSystems.Length; i++)
            {
                var particleSystem = _particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Play(withChildren: true);
            }
        }

        public void StopAndHide()
        {
            for (var i = 0; i < _particleSystems.Length; i++)
            {
                var particleSystem = _particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            gameObject.SetActive(false);
        }

        private static float ComputeSuggestedDuration(ParticleSystem[] systems)
        {
            var maxDuration = 0.25f;
            if (systems == null)
            {
                return maxDuration;
            }

            for (var i = 0; i < systems.Length; i++)
            {
                var particleSystem = systems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                var main = particleSystem.main;
                var duration = main.duration + GetMax(main.startDelay) + GetMax(main.startLifetime);
                if (duration > maxDuration)
                {
                    maxDuration = duration;
                }
            }

            return Mathf.Max(0.25f, maxDuration);
        }

        private static float GetMax(ParticleSystem.MinMaxCurve curve)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return Mathf.Max(0f, curve.constant);
                case ParticleSystemCurveMode.TwoConstants:
                    return Mathf.Max(0f, curve.constantMax);
                case ParticleSystemCurveMode.Curve:
                    return Mathf.Max(0f, curve.curveMultiplier);
                case ParticleSystemCurveMode.TwoCurves:
                    return Mathf.Max(0f, curve.curveMultiplier);
                default:
                    return Mathf.Max(0f, curve.constantMax);
            }
        }
    }
}
