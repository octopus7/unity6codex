#nullable enable

using System;
using UnityEngine;

namespace McpTest.VoxelVillage
{
    public enum MukhaengTrackerPoseState
    {
        Dormant,
        Search,
        Pursuit,
        Threat,
        Retreat
    }

    public readonly struct MukhaengTrackerLegRig
    {
        public MukhaengTrackerLegRig(
            string id,
            Transform hip,
            Transform knee,
            Transform ankle,
            Transform footTarget,
            float phaseOffset,
            float sideSign,
            float foreAftBias)
        {
            Id = id;
            Hip = hip;
            Knee = knee;
            Ankle = ankle;
            FootTarget = footTarget;
            PhaseOffset = phaseOffset;
            SideSign = sideSign;
            ForeAftBias = foreAftBias;
        }

        public string Id { get; }

        public Transform Hip { get; }

        public Transform Knee { get; }

        public Transform Ankle { get; }

        public Transform FootTarget { get; }

        public float PhaseOffset { get; }

        public float SideSign { get; }

        public float ForeAftBias { get; }
    }

    public readonly struct MukhaengTrackerTentacleRig
    {
        public MukhaengTrackerTentacleRig(
            string id,
            Transform @base,
            Transform mid,
            Transform tip,
            Transform hitOrigin,
            float phaseOffset,
            float sideSign)
        {
            Id = id;
            Base = @base;
            Mid = mid;
            Tip = tip;
            HitOrigin = hitOrigin;
            PhaseOffset = phaseOffset;
            SideSign = sideSign;
        }

        public string Id { get; }

        public Transform Base { get; }

        public Transform Mid { get; }

        public Transform Tip { get; }

        public Transform HitOrigin { get; }

        public float PhaseOffset { get; }

        public float SideSign { get; }
    }

    [DisallowMultipleComponent]
    public sealed class MukhaengTrackerController : MonoBehaviour
    {
        const float SearchInterestRadius = 28f;
        const float ThreatRadius = 10.5f;
        const float MotionLerpSpeed = 1.15f;
        const float RotationLerpSpeed = 2.8f;
        const float SearchLegSpeed = 1.7f;
        const float PursuitLegSpeed = 2.65f;
        const float GlowPulseSpeed = 3.2f;

        static readonly int EmissionColorShaderId = Shader.PropertyToID("_EmissionColor");
        static readonly Color IdleEyeEmissionColor = new Color(0.65f, 0.02f, 0.01f);
        static readonly Color AlertEyeEmissionColor = new Color(18f, 0.18f, 0.08f);

        Transform? _locomotionRoot;
        Transform? _bodyPivot;
        Transform? _mantleRoot;
        Transform? _threatCenter;
        LegRigState[] _legs = Array.Empty<LegRigState>();
        TentacleRigState[] _tentacles = Array.Empty<TentacleRigState>();
        Renderer[] _glowRenderers = Array.Empty<Renderer>();
        MaterialPropertyBlock[] _glowBlocks = Array.Empty<MaterialPropertyBlock>();

        Vector3 _locomotionBaseLocalPosition;
        Vector3 _bodyBaseLocalPosition;
        Quaternion _bodyBaseLocalRotation;
        Quaternion _mantleBaseLocalRotation;
        Vector3 _homePosition;
        float _clock;
        bool _initialized;

        public MukhaengTrackerPoseState PoseState { get; private set; } = MukhaengTrackerPoseState.Search;

        public Transform? Target { get; private set; }

        public Transform ThreatCenter => _threatCenter ?? transform;

        void Awake()
        {
            _homePosition = transform.position;
        }

        public void Initialize(
            Transform locomotionRoot,
            Transform bodyPivot,
            Transform mantleRoot,
            Transform threatCenter,
            MukhaengTrackerLegRig[] legs,
            MukhaengTrackerTentacleRig[] tentacles,
            Renderer[] glowRenderers)
        {
            _locomotionRoot = locomotionRoot;
            _bodyPivot = bodyPivot;
            _mantleRoot = mantleRoot;
            _threatCenter = threatCenter;
            _locomotionBaseLocalPosition = locomotionRoot.localPosition;
            _bodyBaseLocalPosition = bodyPivot.localPosition;
            _bodyBaseLocalRotation = bodyPivot.localRotation;
            _mantleBaseLocalRotation = mantleRoot.localRotation;
            _homePosition = transform.position;

            _legs = new LegRigState[legs.Length];
            for (var index = 0; index < legs.Length; index++)
            {
                _legs[index] = new LegRigState(legs[index]);
            }

            _tentacles = new TentacleRigState[tentacles.Length];
            for (var index = 0; index < tentacles.Length; index++)
            {
                _tentacles[index] = new TentacleRigState(tentacles[index]);
            }

            _glowRenderers = glowRenderers;
            _glowBlocks = new MaterialPropertyBlock[glowRenderers.Length];
            for (var index = 0; index < glowRenderers.Length; index++)
            {
                _glowBlocks[index] = new MaterialPropertyBlock();
            }

            PoseState = MukhaengTrackerPoseState.Search;
            Target = null;
            _initialized = true;
        }

        public void SetTarget(Transform? target)
        {
            Target = target;
        }

        public void SetHomePosition(Vector3 homePosition)
        {
            _homePosition = homePosition;
        }

        void Update()
        {
            if (!_initialized || _locomotionRoot == null || _bodyPivot == null || _mantleRoot == null)
            {
                return;
            }

            _clock += Time.deltaTime;
            UpdatePoseStateFromTarget();

            var desiredPosition = ComputeDesiredWorldPosition();
            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * MotionLerpSpeed);

            UpdateFacing(desiredPosition);
            UpdateBodyPose();
            UpdateLegPose();
            UpdateTentacles();
            UpdateGlow();
        }

        void UpdatePoseStateFromTarget()
        {
            if (Target == null || !Target.gameObject.activeInHierarchy)
            {
                PoseState = MukhaengTrackerPoseState.Search;
                return;
            }

            var offset = Target.position - ThreatCenter.position;
            offset.y = 0f;
            var distance = offset.magnitude;
            if (distance <= ThreatRadius)
            {
                PoseState = MukhaengTrackerPoseState.Threat;
                return;
            }

            PoseState = distance <= SearchInterestRadius
                ? MukhaengTrackerPoseState.Pursuit
                : MukhaengTrackerPoseState.Search;
        }

        Vector3 ComputeDesiredWorldPosition()
        {
            var desired = _homePosition;

            switch (PoseState)
            {
                case MukhaengTrackerPoseState.Dormant:
                    desired += new Vector3(0f, 0f, Mathf.Sin(_clock * 0.31f) * 0.1f);
                    break;

                case MukhaengTrackerPoseState.Search:
                    desired += new Vector3(
                        Mathf.Sin(_clock * 0.43f) * 0.45f,
                        0f,
                        Mathf.Cos(_clock * 0.37f) * 0.32f);
                    break;

                case MukhaengTrackerPoseState.Pursuit:
                case MukhaengTrackerPoseState.Threat:
                    if (Target != null)
                    {
                        var toTarget = Target.position - _homePosition;
                        toTarget.y = 0f;
                        if (toTarget.sqrMagnitude > 0.0001f)
                        {
                            var forward = toTarget.normalized;
                            var side = Vector3.Cross(Vector3.up, forward);
                            var forwardDistance = PoseState == MukhaengTrackerPoseState.Threat ? 1.55f : 2.3f;
                            var lateralDistance = PoseState == MukhaengTrackerPoseState.Threat ? 0.85f : 1.35f;
                            desired += Vector3.ClampMagnitude(
                                (forward * forwardDistance) + (side * Mathf.Sin(_clock * 0.92f) * lateralDistance),
                                2.85f);
                        }
                    }

                    break;

                case MukhaengTrackerPoseState.Retreat:
                    desired += -transform.forward * 1.8f;
                    break;
            }

            desired.y = _homePosition.y;
            return desired;
        }

        void UpdateFacing(Vector3 desiredPosition)
        {
            var flatDirection = Vector3.zero;

            if (Target != null)
            {
                flatDirection = Target.position - ThreatCenter.position;
                flatDirection.y = 0f;
            }

            if (flatDirection.sqrMagnitude <= 0.0001f)
            {
                flatDirection = desiredPosition - transform.position;
                flatDirection.y = 0f;
            }

            if (flatDirection.sqrMagnitude <= 0.0001f)
            {
                flatDirection = transform.forward;
                flatDirection.y = 0f;
            }

            if (flatDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var desiredRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * RotationLerpSpeed);
        }

        void UpdateBodyPose()
        {
            if (_locomotionRoot == null || _bodyPivot == null || _mantleRoot == null)
            {
                return;
            }

            var pursuitBias = PoseState == MukhaengTrackerPoseState.Pursuit ? 1f : 0f;
            var threatBias = PoseState == MukhaengTrackerPoseState.Threat ? 1f : 0f;
            var dormantBias = PoseState == MukhaengTrackerPoseState.Dormant ? 1f : 0f;
            var stepSpeed = GetStepSpeed();
            var bodyBob = Mathf.Sin(_clock * stepSpeed) * (0.07f + pursuitBias * 0.05f + threatBias * 0.03f);
            var mantleSway = Mathf.Sin(_clock * 0.86f) * (2.6f + pursuitBias * 2.4f);
            var mantlePitch = Mathf.Sin(_clock * 1.47f) * (1.6f + pursuitBias * 1.1f) + (threatBias * 7.5f);

            _locomotionRoot.localPosition = _locomotionBaseLocalPosition + new Vector3(0f, bodyBob, 0f);
            _bodyPivot.localPosition = _bodyBaseLocalPosition + new Vector3(0f, (-pursuitBias * 0.26f) + (threatBias * 0.82f) - (dormantBias * 0.18f), 0f);
            _bodyPivot.localRotation = _bodyBaseLocalRotation * Quaternion.Euler(
                (-threatBias * 5.5f) + (Mathf.Sin(_clock * 0.71f) * 2.2f),
                0f,
                mantleSway * 0.55f);

            _mantleRoot.localRotation = _mantleBaseLocalRotation * Quaternion.Euler(
                mantlePitch,
                Mathf.Sin(_clock * 0.58f) * 2f,
                mantleSway);
        }

        void UpdateLegPose()
        {
            var pursuitBias = PoseState == MukhaengTrackerPoseState.Pursuit ? 1f : 0f;
            var threatBias = PoseState == MukhaengTrackerPoseState.Threat ? 1f : 0f;
            var dormantBias = PoseState == MukhaengTrackerPoseState.Dormant ? 1f : 0f;
            var stepSpeed = GetStepSpeed();

            for (var index = 0; index < _legs.Length; index++)
            {
                var leg = _legs[index];
                var phase = (_clock * stepSpeed) + leg.PhaseOffset;
                var lift = Mathf.Max(0f, Mathf.Sin(phase)) * (0.14f + (pursuitBias * 0.08f) + (threatBias * 0.04f));
                var sweep = Mathf.Cos(phase) * (8f + (pursuitBias * 5f));
                var brace = threatBias * 9f;

                leg.Hip.localRotation = leg.HipBaseLocalRotation * Quaternion.Euler(
                    sweep - (threatBias * 4.5f) - (dormantBias * 2f),
                    0f,
                    leg.SideSign * (brace + (Mathf.Sin(phase + 0.6f) * 3.4f)));

                leg.Knee.localRotation = leg.KneeBaseLocalRotation * Quaternion.Euler(
                    20f + (lift * 88f) + (threatBias * 10f),
                    0f,
                    0f);

                leg.Ankle.localRotation = leg.AnkleBaseLocalRotation * Quaternion.Euler(
                    -15f - (lift * 38f) - (threatBias * 6f),
                    0f,
                    0f);

                leg.FootTarget.localPosition = leg.FootTargetBaseLocalPosition + new Vector3(
                    leg.SideSign * (0.1f + (threatBias * 0.16f)),
                    lift,
                    (leg.ForeAftBias * 0.08f) + (Mathf.Cos(phase) * (0.1f + (pursuitBias * 0.05f))));
            }
        }

        void UpdateTentacles()
        {
            var pursuitBias = PoseState == MukhaengTrackerPoseState.Pursuit ? 1f : 0f;
            var threatBias = PoseState == MukhaengTrackerPoseState.Threat ? 1f : 0f;
            var dormantBias = PoseState == MukhaengTrackerPoseState.Dormant ? 1f : 0f;

            for (var index = 0; index < _tentacles.Length; index++)
            {
                var tentacle = _tentacles[index];
                var phase = (_clock * 1.82f) + tentacle.PhaseOffset;
                var liftAngle = (threatBias * 44f) + (pursuitBias * 12f) - (dormantBias * 10f);
                var curl = 12f + (threatBias * 18f);

                tentacle.Base.localRotation = tentacle.BaseLocalRotation * Quaternion.Euler(
                    liftAngle + (Mathf.Sin(phase) * 6f),
                    0f,
                    tentacle.SideSign * (8f + (threatBias * 14f)));

                tentacle.Mid.localRotation = tentacle.MidLocalRotation * Quaternion.Euler(
                    curl + (Mathf.Sin(phase + 0.5f) * 6f),
                    0f,
                    tentacle.SideSign * (4f + (threatBias * 8f)));

                tentacle.Tip.localRotation = tentacle.TipLocalRotation * Quaternion.Euler(
                    (curl * 0.8f) + (Mathf.Sin(phase + 1f) * 8f),
                    0f,
                    tentacle.SideSign * (2f + (threatBias * 4f)));
            }
        }

        void UpdateGlow()
        {
            if (_glowRenderers.Length == 0)
            {
                return;
            }

            float targetGlow;
            switch (PoseState)
            {
                case MukhaengTrackerPoseState.Dormant:
                    targetGlow = 0.1f;
                    break;
                case MukhaengTrackerPoseState.Pursuit:
                    targetGlow = 0.65f;
                    break;
                case MukhaengTrackerPoseState.Threat:
                    targetGlow = 1f;
                    break;
                case MukhaengTrackerPoseState.Retreat:
                    targetGlow = 0.35f;
                    break;
                default:
                    targetGlow = 0.28f;
                    break;
            }

            var pulse = 0.9f + (Mathf.Sin(_clock * GlowPulseSpeed) * 0.2f);
            var glow = Mathf.Clamp01(targetGlow) * pulse;
            var emission = Color.Lerp(IdleEyeEmissionColor, AlertEyeEmissionColor, glow);

            for (var index = 0; index < _glowRenderers.Length; index++)
            {
                var renderer = _glowRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                var block = _glowBlocks[index];
                block.Clear();
                block.SetColor(EmissionColorShaderId, emission);
                renderer.SetPropertyBlock(block);
            }
        }

        float GetStepSpeed()
        {
            switch (PoseState)
            {
                case MukhaengTrackerPoseState.Dormant:
                    return 0.9f;
                case MukhaengTrackerPoseState.Pursuit:
                    return PursuitLegSpeed;
                case MukhaengTrackerPoseState.Threat:
                    return SearchLegSpeed * 0.78f;
                case MukhaengTrackerPoseState.Retreat:
                    return PursuitLegSpeed * 0.92f;
                default:
                    return SearchLegSpeed;
            }
        }

        sealed class LegRigState
        {
            public LegRigState(MukhaengTrackerLegRig rig)
            {
                Hip = rig.Hip;
                Knee = rig.Knee;
                Ankle = rig.Ankle;
                FootTarget = rig.FootTarget;
                PhaseOffset = rig.PhaseOffset;
                SideSign = rig.SideSign;
                ForeAftBias = rig.ForeAftBias;
                HipBaseLocalRotation = rig.Hip.localRotation;
                KneeBaseLocalRotation = rig.Knee.localRotation;
                AnkleBaseLocalRotation = rig.Ankle.localRotation;
                FootTargetBaseLocalPosition = rig.FootTarget.localPosition;
            }

            public Transform Hip { get; }

            public Transform Knee { get; }

            public Transform Ankle { get; }

            public Transform FootTarget { get; }

            public float PhaseOffset { get; }

            public float SideSign { get; }

            public float ForeAftBias { get; }

            public Quaternion HipBaseLocalRotation { get; }

            public Quaternion KneeBaseLocalRotation { get; }

            public Quaternion AnkleBaseLocalRotation { get; }

            public Vector3 FootTargetBaseLocalPosition { get; }
        }

        sealed class TentacleRigState
        {
            public TentacleRigState(MukhaengTrackerTentacleRig rig)
            {
                Base = rig.Base;
                Mid = rig.Mid;
                Tip = rig.Tip;
                HitOrigin = rig.HitOrigin;
                PhaseOffset = rig.PhaseOffset;
                SideSign = rig.SideSign;
                BaseLocalRotation = rig.Base.localRotation;
                MidLocalRotation = rig.Mid.localRotation;
                TipLocalRotation = rig.Tip.localRotation;
            }

            public Transform Base { get; }

            public Transform Mid { get; }

            public Transform Tip { get; }

            public Transform HitOrigin { get; }

            public float PhaseOffset { get; }

            public float SideSign { get; }

            public Quaternion BaseLocalRotation { get; }

            public Quaternion MidLocalRotation { get; }

            public Quaternion TipLocalRotation { get; }
        }
    }
}
