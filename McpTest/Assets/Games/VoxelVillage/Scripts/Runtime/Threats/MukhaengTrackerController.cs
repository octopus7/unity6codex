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
        const float LegStepThreshold = 0.46f;
        const float LegGroundProbeHeight = 5.5f;
        const float LegGroundProbeDistance = 12f;
        const float LegGroundOffset = 0.02f;
        const float LegMinimumReachPadding = 0.01f;
        const int GroundHitCapacity = 16;

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
        Vector3 _previousRootPosition;
        float _clock;
        bool _initialized;
        readonly RaycastHit[] _groundHits = new RaycastHit[GroundHitCapacity];

        public MukhaengTrackerPoseState PoseState { get; private set; } = MukhaengTrackerPoseState.Search;

        public Transform? Target { get; private set; }

        public Transform ThreatCenter => _threatCenter ?? transform;

        void Awake()
        {
            _homePosition = transform.position;
            _previousRootPosition = transform.position;
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
            _previousRootPosition = transform.position;

            _legs = new LegRigState[legs.Length];
            for (var index = 0; index < legs.Length; index++)
            {
                var plantedFootWorld = ResolveGroundContact(legs[index].FootTarget.position, _homePosition.y);
                _legs[index] = new LegRigState(legs[index], transform, plantedFootWorld);
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
            _previousRootPosition = transform.position;
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
            var deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            var worldVelocity = (transform.position - _previousRootPosition) / deltaTime;
            var localVelocity = transform.InverseTransformDirection(worldVelocity);
            var stepHeight = 0.24f + (pursuitBias * 0.1f) + (threatBias * 0.06f);

            for (var index = 0; index < _legs.Length; index++)
            {
                var leg = _legs[index];
                var phase = (_clock * stepSpeed) + leg.PhaseOffset;
                var desiredPlantWorld = ComputeDesiredFootPlantWorld(leg, localVelocity, pursuitBias, threatBias, dormantBias);

                if (leg.IsStepping)
                {
                    AdvanceLegStep(leg, stepHeight);
                }
                else
                {
                    var stepWindow = Mathf.Sin(phase) > 0.18f;
                    var horizontalDistance = Vector2.Distance(
                        new Vector2(leg.PlantedFootWorldPosition.x, leg.PlantedFootWorldPosition.z),
                        new Vector2(desiredPlantWorld.x, desiredPlantWorld.z));
                    var verticalDistance = Mathf.Abs(leg.PlantedFootWorldPosition.y - desiredPlantWorld.y);
                    var stepThreshold = LegStepThreshold + (pursuitBias * 0.12f) + (threatBias * 0.08f) - (dormantBias * 0.12f);

                    if (stepWindow && (horizontalDistance > stepThreshold || verticalDistance > 0.14f))
                    {
                        BeginLegStep(leg, desiredPlantWorld, localVelocity, pursuitBias, threatBias);
                    }
                    else
                    {
                        leg.CurrentFootWorldPosition = leg.PlantedFootWorldPosition;
                    }
                }

                SolveLegPose(leg);
            }

            _previousRootPosition = transform.position;
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

        Vector3 ComputeDesiredFootPlantWorld(
            LegRigState leg,
            Vector3 localVelocity,
            float pursuitBias,
            float threatBias,
            float dormantBias)
        {
            var strideVelocity = new Vector3(
                Mathf.Clamp(localVelocity.x, -1.4f, 1.4f) * 0.12f,
                0f,
                Mathf.Clamp(localVelocity.z, -1.6f, 1.6f) * 0.18f);
            var stanceOffset = new Vector3(
                leg.SideSign * ((threatBias * 0.22f) + (strideVelocity.x * 0.35f)),
                0f,
                (leg.ForeAftBias * (0.08f + (pursuitBias * 0.14f) + (threatBias * 0.05f))) +
                strideVelocity.z -
                (dormantBias * 0.05f));
            var idealFootWorld = transform.TransformPoint(leg.RestFootLocalPosition + stanceOffset);
            return ResolveGroundContact(idealFootWorld, leg.PlantedFootWorldPosition.y);
        }

        void BeginLegStep(
            LegRigState leg,
            Vector3 desiredPlantWorld,
            Vector3 localVelocity,
            float pursuitBias,
            float threatBias)
        {
            leg.IsStepping = true;
            leg.StepElapsed = 0f;
            leg.StepDuration = Mathf.Lerp(0.28f, 0.17f, Mathf.Clamp01(pursuitBias + (threatBias * 0.35f)));
            leg.StepStartWorldPosition = leg.CurrentFootWorldPosition;

            var leadWorldOffset = transform.TransformDirection(new Vector3(
                Mathf.Clamp(localVelocity.x, -1.2f, 1.2f) * 0.08f,
                0f,
                Mathf.Clamp(localVelocity.z, -1.4f, 1.4f) * (0.12f + (pursuitBias * 0.05f))));
            var spreadWorldOffset = transform.right * (leg.SideSign * threatBias * 0.08f);
            leg.StepEndWorldPosition = ResolveGroundContact(desiredPlantWorld + leadWorldOffset + spreadWorldOffset, desiredPlantWorld.y);
        }

        void AdvanceLegStep(LegRigState leg, float stepHeight)
        {
            leg.StepElapsed += Time.deltaTime;
            var normalizedTime = Mathf.Clamp01(leg.StepElapsed / Mathf.Max(leg.StepDuration, 0.0001f));
            var easedTime = normalizedTime * normalizedTime * (3f - (2f * normalizedTime));
            var steppedWorldPosition = Vector3.Lerp(leg.StepStartWorldPosition, leg.StepEndWorldPosition, easedTime);
            steppedWorldPosition.y += Mathf.Sin(normalizedTime * Mathf.PI) * stepHeight;
            leg.CurrentFootWorldPosition = steppedWorldPosition;

            if (normalizedTime < 1f)
            {
                return;
            }

            leg.IsStepping = false;
            leg.PlantedFootWorldPosition = leg.StepEndWorldPosition;
            leg.CurrentFootWorldPosition = leg.PlantedFootWorldPosition;
        }

        void SolveLegPose(LegRigState leg)
        {
            var hipPosition = leg.Hip.position;
            var desiredFootPosition = leg.CurrentFootWorldPosition;
            var toFoot = desiredFootPosition - hipPosition;
            if (toFoot.sqrMagnitude <= 0.0001f)
            {
                toFoot = (transform.right * leg.SideSign * 0.25f) + Vector3.down;
            }

            var upperLength = leg.UpperLength;
            var lowerCombinedLength = leg.LowerCombinedLength;
            var distanceToFoot = toFoot.magnitude;
            var clampedDistance = Mathf.Clamp(
                distanceToFoot,
                Mathf.Abs(upperLength - lowerCombinedLength) + LegMinimumReachPadding,
                upperLength + lowerCombinedLength - LegMinimumReachPadding);
            var clampedFootPosition = hipPosition + (toFoot.normalized * clampedDistance);
            var kneePosition = SolveKneePosition(
                hipPosition,
                clampedFootPosition,
                upperLength,
                lowerCombinedLength,
                ComputePoleWorldPosition(leg));
            var kneeToFoot = clampedFootPosition - kneePosition;
            if (kneeToFoot.sqrMagnitude <= 0.0001f)
            {
                kneeToFoot = (clampedFootPosition - hipPosition).normalized;
            }

            var anklePosition = kneePosition + (kneeToFoot.normalized * leg.LowerLength);

            ApplyJointDirection(leg.Hip, leg.Hip.parent, kneePosition - hipPosition);
            ApplyJointDirection(leg.Knee, leg.Knee.parent, anklePosition - kneePosition);
            ApplyJointDirection(leg.Ankle, leg.Ankle.parent, clampedFootPosition - anklePosition);
        }

        Vector3 ComputePoleWorldPosition(LegRigState leg)
        {
            return leg.Hip.position +
                (transform.right * leg.SideSign * 3.2f) +
                (transform.forward * leg.ForeAftBias * 0.6f) +
                (Vector3.up * 1.2f);
        }

        Vector3 ResolveGroundContact(Vector3 desiredFootWorld, float fallbackY)
        {
            var rayOrigin = desiredFootWorld + (Vector3.up * LegGroundProbeHeight);
            var hitCount = Physics.RaycastNonAlloc(
                rayOrigin,
                Vector3.down,
                _groundHits,
                LegGroundProbeDistance,
                ~0,
                QueryTriggerInteraction.Ignore);
            var bestY = float.NegativeInfinity;
            var found = false;

            for (var index = 0; index < hitCount; index++)
            {
                var collider = _groundHits[index].collider;
                if (collider == null || collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (_groundHits[index].point.y <= bestY)
                {
                    continue;
                }

                bestY = _groundHits[index].point.y;
                found = true;
            }

            desiredFootWorld.y = (found ? bestY : fallbackY) + LegGroundOffset;
            return desiredFootWorld;
        }

        static Vector3 SolveKneePosition(
            Vector3 hipPosition,
            Vector3 footPosition,
            float upperLength,
            float lowerCombinedLength,
            Vector3 poleWorldPosition)
        {
            var toFoot = footPosition - hipPosition;
            var distance = Mathf.Max(toFoot.magnitude, 0.0001f);
            var forward = toFoot / distance;
            var projectedPole = Vector3.ProjectOnPlane(poleWorldPosition - hipPosition, forward);
            if (projectedPole.sqrMagnitude <= 0.0001f)
            {
                projectedPole = Vector3.Cross(forward, Vector3.right);
                if (projectedPole.sqrMagnitude <= 0.0001f)
                {
                    projectedPole = Vector3.Cross(forward, Vector3.forward);
                }
            }

            var bendDirection = projectedPole.normalized;
            var x = ((upperLength * upperLength) - (lowerCombinedLength * lowerCombinedLength) + (distance * distance)) / (2f * distance);
            var y = Mathf.Sqrt(Mathf.Max((upperLength * upperLength) - (x * x), 0f));
            return hipPosition + (forward * x) + (bendDirection * y);
        }

        static void ApplyJointDirection(Transform joint, Transform? parent, Vector3 worldDirection)
        {
            if (parent == null || worldDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var localDirection = parent.InverseTransformDirection(worldDirection.normalized);
            joint.localRotation = Quaternion.FromToRotation(Vector3.up, localDirection);
        }

        sealed class LegRigState
        {
            public LegRigState(MukhaengTrackerLegRig rig, Transform root, Vector3 plantedFootWorldPosition)
            {
                Hip = rig.Hip;
                Knee = rig.Knee;
                Ankle = rig.Ankle;
                FootTarget = rig.FootTarget;
                PhaseOffset = rig.PhaseOffset;
                SideSign = rig.SideSign;
                ForeAftBias = rig.ForeAftBias;
                UpperLength = Vector3.Distance(rig.Hip.position, rig.Knee.position);
                LowerLength = Vector3.Distance(rig.Knee.position, rig.Ankle.position);
                TipLength = Vector3.Distance(rig.Ankle.position, rig.FootTarget.position);
                RestFootLocalPosition = root.InverseTransformPoint(rig.FootTarget.position);
                PlantedFootWorldPosition = plantedFootWorldPosition;
                CurrentFootWorldPosition = plantedFootWorldPosition;
                StepStartWorldPosition = plantedFootWorldPosition;
                StepEndWorldPosition = plantedFootWorldPosition;
            }

            public Transform Hip { get; }

            public Transform Knee { get; }

            public Transform Ankle { get; }

            public Transform FootTarget { get; }

            public float PhaseOffset { get; }

            public float SideSign { get; }

            public float ForeAftBias { get; }

            public float UpperLength { get; }

            public float LowerLength { get; }

            public float TipLength { get; }

            public float LowerCombinedLength => LowerLength + TipLength;

            public Vector3 RestFootLocalPosition { get; }

            public Vector3 PlantedFootWorldPosition { get; set; }

            public Vector3 CurrentFootWorldPosition { get; set; }

            public Vector3 StepStartWorldPosition { get; set; }

            public Vector3 StepEndWorldPosition { get; set; }

            public float StepElapsed { get; set; }

            public float StepDuration { get; set; }

            public bool IsStepping { get; set; }
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
