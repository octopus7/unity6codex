#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace McpTest.VoxelVillage
{
    [DisallowMultipleComponent]
    public sealed class AmbientSpiderWalkerController : MonoBehaviour
    {
        const float MoveSpeed = 2.05f;
        const float SqueezedMoveSpeed = 1.72f;
        const float TurnSpeed = 5.2f;
        const float BodyBobAmplitude = 0.08f;
        const float BodyBobSpeed = 6.2f;
        const float StepDuration = 0.22f;
        const float StepArcHeight = 0.62f;
        const float StepThreshold = 0.42f;
        const float ForwardStrideDistance = 1.44f;
        const float SqueezedForwardStrideDistance = 1.2f;
        const float SqueezedWidthScale = 0.78f;
        const float SqueezedStrideScale = 0.84f;
        const float EyePulseSpeed = 3.4f;
        const float AvoidanceRadius = 2.2f;
        const float BellySpinnerDegreesPerSecond = 360f;
        const int FootTrajectorySamples = 16;

        static readonly int EmissionColorShaderId = Shader.PropertyToID("_EmissionColor");
        static readonly Color GroupAColor = new Color(0.22f, 0.9f, 1f, 0.95f);
        static readonly Color GroupBColor = new Color(1f, 0.67f, 0.22f, 0.95f);
        static readonly Color PlantedColor = new Color(0.2f, 1f, 0.35f, 0.95f);
        static readonly Color DesiredColor = new Color(1f, 0.35f, 0.25f, 0.95f);
        static readonly Color HintColor = new Color(0.75f, 0.3f, 1f, 0.65f);
        static readonly Color VisualTipColor = new Color(0.28f, 0.95f, 1f, 0.9f);

        [SerializeField] bool _drawFootTrajectoryGizmos = true;
        [SerializeField] float _gizmoPointRadius = 0.08f;

        Transform _locomotionRoot = null!;
        Transform _bodyPivot = null!;
        Transform _bodyShell = null!;
        Transform? _bellySpinner;
        Transform _eyeCluster = null!;
        Renderer[] _eyeRenderers = Array.Empty<Renderer>();
        MaterialPropertyBlock[] _eyeBlocks = Array.Empty<MaterialPropertyBlock>();
        SpiderLegState[] _legs = Array.Empty<SpiderLegState>();

        VillageGrid _grid = null!;
        VillageThreatAnchor[] _anchors = Array.Empty<VillageThreatAnchor>();
        Transform[] _avoidanceTargets = Array.Empty<Transform>();
        readonly List<Vector2Int> _path = new List<Vector2Int>();
        readonly List<Vector2Int> _candidateCells = new List<Vector2Int>();
        System.Random _random = new System.Random();

        Vector3 _bodyBaseLocalPosition;
        Quaternion _bodyBaseLocalRotation;
        Vector3 _bodyShellBaseLocalScale;
        Quaternion _bellySpinnerBaseLocalRotation = Quaternion.identity;
        Vector3 _eyeClusterBaseLocalScale;
        Vector3 _currentVelocity;
        Vector2Int _currentCell;
        float _worldCellSize;
        float _townFootprint;
        float _clock;
        float _waitUntilTime;
        float _currentWidthScale = 1f;
        int _pathIndex;
        int _leadGaitGroup;
        int _nextStepGroup = 1;
        bool _navigationBound;

        public bool IsRigBound { get; private set; }

        public MovementFootprint CurrentFootprint { get; private set; } = MovementFootprint.Spider2x2;

        void OnDrawGizmosSelected()
        {
            if (!_drawFootTrajectoryGizmos || !IsRigBound || _legs.Length == 0)
            {
                return;
            }

            DrawFootTrajectoryGizmos();
        }

        public void BindRig(
            Transform locomotionRoot,
            Transform bodyPivot,
            Transform bodyShell,
            Transform bellySpinner,
            Transform eyeCluster,
            Renderer[] eyeRenderers,
            SpiderLegState[] legs)
        {
            _locomotionRoot = locomotionRoot;
            _bodyPivot = bodyPivot;
            _bodyShell = bodyShell;
            _bellySpinner = bellySpinner;
            _eyeCluster = eyeCluster;
            _eyeRenderers = eyeRenderers;
            _legs = legs;
            _bodyBaseLocalPosition = bodyPivot.localPosition;
            _bodyBaseLocalRotation = bodyPivot.localRotation;
            _bodyShellBaseLocalScale = bodyShell.localScale;
            _bellySpinnerBaseLocalRotation = bellySpinner.localRotation;
            _eyeClusterBaseLocalScale = eyeCluster.localScale;
            _eyeBlocks = new MaterialPropertyBlock[eyeRenderers.Length];
            for (var index = 0; index < eyeRenderers.Length; index++)
            {
                _eyeBlocks[index] = new MaterialPropertyBlock();
            }

            IsRigBound = true;
            SnapFeetToRestPose();
            UpdateEyeEmission(1f);
        }

        public void BindNavigation(
            VillageGrid grid,
            VillageThreatAnchor[] anchors,
            Vector2Int spawnCell,
            float worldCellSize,
            float townFootprint,
            Transform[] avoidanceTargets)
        {
            _grid = grid;
            _anchors = anchors ?? Array.Empty<VillageThreatAnchor>();
            _avoidanceTargets = avoidanceTargets ?? Array.Empty<Transform>();
            _worldCellSize = worldCellSize;
            _townFootprint = townFootprint;
            _currentCell = spawnCell;
            _random = new System.Random((spawnCell.x * 397) ^ (spawnCell.y * 7919) ^ GetInstanceID());
            _path.Clear();
            _pathIndex = 0;
            _leadGaitGroup = 0;
            _nextStepGroup = 1;
            _waitUntilTime = Time.time + Range(0.3f, 0.75f);
            CurrentFootprint = _grid.IsWalkable(spawnCell, false, MovementFootprint.Spider2x2)
                ? MovementFootprint.Spider2x2
                : MovementFootprint.SqueezedSpider1x1;
            transform.position = CellToWorld(spawnCell);
            _navigationBound = true;
            SnapFeetToRestPose();
        }

        void Update()
        {
            if (!IsRigBound)
            {
                return;
            }

            _clock += Time.deltaTime;

            if (_navigationBound)
            {
                UpdateNavigation();
            }
            else
            {
                _currentVelocity = Vector3.zero;
            }

            UpdateBodyPose();
            UpdateBellySpinner();
            UpdateLegs();
            UpdateEyeEmission(0.92f + (Mathf.Sin(_clock * EyePulseSpeed) * 0.08f));
        }

        void UpdateNavigation()
        {
            _currentCell = WorldToCell(transform.position);

            if (_pathIndex >= _path.Count)
            {
                _path.Clear();
                _pathIndex = 0;
                _currentVelocity = Vector3.Lerp(_currentVelocity, Vector3.zero, Time.deltaTime * 6f);
                if (Time.time >= _waitUntilTime)
                {
                    TryAssignPath();
                }

                return;
            }

            var nextCell = _path[_pathIndex];
            if (!_grid.IsWalkable(nextCell, false, CurrentFootprint))
            {
                ResetPath(0.35f, 0.8f);
                return;
            }

            var targetPosition = CellToWorld(nextCell);
            var toTarget = targetPosition - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= 0.01f)
            {
                transform.position = targetPosition;
                _pathIndex++;
                if (_pathIndex >= _path.Count)
                {
                    ResetPath(0.8f, 1.65f);
                }

                return;
            }

            var desiredDirection = toTarget.normalized;
            var avoidance = ComputeSoftAvoidance();
            var blendedDirection = desiredDirection + (avoidance * 0.72f);
            if (blendedDirection.sqrMagnitude <= 0.0001f)
            {
                blendedDirection = desiredDirection;
            }

            blendedDirection.Normalize();

            var speed = CurrentFootprint == MovementFootprint.SqueezedSpider1x1 ? SqueezedMoveSpeed : MoveSpeed;
            var candidatePosition = transform.position + (blendedDirection * (speed * Time.deltaTime));
            candidatePosition.y = 0f;

            if (!_grid.IsWalkable(WorldToCell(candidatePosition), false, CurrentFootprint))
            {
                candidatePosition = transform.position + (desiredDirection * (speed * Time.deltaTime));
                candidatePosition.y = 0f;
                if (!_grid.IsWalkable(WorldToCell(candidatePosition), false, CurrentFootprint))
                {
                    _currentVelocity = Vector3.Lerp(_currentVelocity, Vector3.zero, Time.deltaTime * 8f);
                    return;
                }
            }

            candidatePosition = ClampWorldPosition(candidatePosition);
            _currentVelocity = (candidatePosition - transform.position) / Mathf.Max(Time.deltaTime, 0.0001f);
            transform.position = candidatePosition;

            var facing = _currentVelocity;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
            {
                var desiredRotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, TurnSpeed * Time.deltaTime);
            }
        }

        void TryAssignPath()
        {
            if (_anchors.Length == 0)
            {
                ResetPath(0.6f, 1.2f);
                return;
            }

            var start = WorldToCell(transform.position);
            for (var attempt = 0; attempt < Mathf.Max(8, _anchors.Length * 2); attempt++)
            {
                var anchor = _anchors[_random.Next(0, _anchors.Length)];

                if (TryAssignPathToAnchor(start, anchor, MovementFootprint.Spider2x2))
                {
                    return;
                }

                if (TryAssignPathToAnchor(start, anchor, MovementFootprint.SqueezedSpider1x1))
                {
                    return;
                }
            }

            ResetPath(0.7f, 1.3f);
        }

        bool TryAssignPathToAnchor(Vector2Int start, VillageThreatAnchor anchor, MovementFootprint footprint)
        {
            var destination = ResolveAnchorDestination(anchor, footprint);
            if (!_grid.IsWalkable(destination, false, footprint))
            {
                return false;
            }

            if (!_grid.TryFindPath(start, destination, _path, false, footprint) || _path.Count <= 1)
            {
                return false;
            }

            _pathIndex = _path[0] == start ? 1 : 0;
            CurrentFootprint = footprint;
            return true;
        }

        Vector2Int ResolveAnchorDestination(VillageThreatAnchor anchor, MovementFootprint footprint)
        {
            _grid.CollectReachableCells(anchor.cell, Mathf.Clamp(anchor.patrolRadius, 4, 12), _candidateCells, false, footprint);
            if (_candidateCells.Count == 0)
            {
                return anchor.cell;
            }

            return _candidateCells[_random.Next(0, _candidateCells.Count)];
        }

        void ResetPath(float minDelay, float maxDelay)
        {
            _path.Clear();
            _pathIndex = 0;
            _waitUntilTime = Time.time + Range(minDelay, maxDelay);
        }

        Vector3 ComputeSoftAvoidance()
        {
            var offset = Vector3.zero;
            for (var index = 0; index < _avoidanceTargets.Length; index++)
            {
                var target = _avoidanceTargets[index];
                if (target == null || target == transform)
                {
                    continue;
                }

                var toSelf = transform.position - target.position;
                toSelf.y = 0f;
                var distance = toSelf.magnitude;
                if (distance <= 0.0001f || distance >= AvoidanceRadius)
                {
                    continue;
                }

                offset += toSelf.normalized * ((AvoidanceRadius - distance) / AvoidanceRadius);
            }

            return offset;
        }

        void UpdateBodyPose()
        {
            var planarSpeed = new Vector2(_currentVelocity.x, _currentVelocity.z).magnitude;
            var moveRatio = Mathf.Clamp01(planarSpeed / MoveSpeed);
            var desiredWidthScale = CurrentFootprint == MovementFootprint.SqueezedSpider1x1 ? SqueezedWidthScale : 1f;
            _currentWidthScale = Mathf.Lerp(_currentWidthScale, desiredWidthScale, Time.deltaTime * 5.5f);

            var bodyBob = Mathf.Sin(_clock * (BodyBobSpeed + (moveRatio * 3f))) * (BodyBobAmplitude * moveRatio);
            var leanForward = -moveRatio * 10f;
            var roll = Mathf.Sin(_clock * 0.5f + (moveRatio * 3.4f)) * (3.4f + (moveRatio * 2.2f));

            _bodyPivot.localPosition = _bodyBaseLocalPosition + new Vector3(0f, bodyBob, 0f);
            _bodyPivot.localRotation = _bodyBaseLocalRotation * Quaternion.Euler(leanForward, 0f, roll);
            _bodyShell.localScale = new Vector3(
                _bodyShellBaseLocalScale.x * _currentWidthScale,
                _bodyShellBaseLocalScale.y * (1f + (moveRatio * 0.03f)),
                _bodyShellBaseLocalScale.z);
            _eyeCluster.localScale = new Vector3(
                _eyeClusterBaseLocalScale.x * _currentWidthScale,
                _eyeClusterBaseLocalScale.y,
                _eyeClusterBaseLocalScale.z);
        }

        void UpdateBellySpinner()
        {
            if (_bellySpinner == null)
            {
                return;
            }

            _bellySpinner.localRotation =
                _bellySpinnerBaseLocalRotation *
                Quaternion.AngleAxis(_clock * BellySpinnerDegreesPerSecond, Vector3.up);
        }

        void UpdateLegs()
        {
            for (var index = 0; index < _legs.Length; index++)
            {
                var leg = _legs[index];
                leg.LegRoot.localPosition = new Vector3(
                    leg.LegRootBaseLocalPosition.x * _currentWidthScale,
                    leg.LegRootBaseLocalPosition.y,
                    leg.LegRootBaseLocalPosition.z);
            }

            if (!IsAnyLegStepping() && DoesGroupNeedStep(_nextStepGroup))
            {
                StartGroupStep(_nextStepGroup);
            }

            for (var index = 0; index < _legs.Length; index++)
            {
                var leg = _legs[index];
                if (leg.IsStepping)
                {
                    leg.StepProgress = Mathf.Clamp01(leg.StepProgress + (Time.deltaTime / StepDuration));
                    var eased = Mathf.SmoothStep(0f, 1f, leg.StepProgress);
                    var position = Vector3.Lerp(leg.StepStartWorldPosition, leg.StepTargetWorldPosition, eased);
                    position.y += Mathf.Sin(eased * Mathf.PI) * GetStepArcHeight();
                    leg.PlantedWorldPosition = position;

                    if (leg.StepProgress >= 1f)
                    {
                        leg.IsStepping = false;
                        leg.PlantedWorldPosition = leg.StepTargetWorldPosition;
                    }
                }
                else
                {
                    leg.DesiredWorldPosition = ComputeDesiredFootPosition(leg);
                }

                leg.FootTarget.position = leg.PlantedWorldPosition;
                var kneeHintWorldPosition = ComputeKneeHintWorldPosition(leg);
                TwoBoneLegIkSolver.ApplyToTransformsWithHint(
                    leg.Hip,
                    leg.Knee,
                    leg.PlantedWorldPosition,
                    kneeHintWorldPosition,
                    leg.UpperLength,
                    leg.LowerLength);
            }
        }

        bool IsGroupStepping(int gaitGroup)
        {
            for (var index = 0; index < _legs.Length; index++)
            {
                if (_legs[index].GaitGroup == gaitGroup && _legs[index].IsStepping)
                {
                    return true;
                }
            }

            return false;
        }

        bool IsAnyLegStepping()
        {
            for (var index = 0; index < _legs.Length; index++)
            {
                if (_legs[index].IsStepping)
                {
                    return true;
                }
            }

            return false;
        }

        bool DoesGroupNeedStep(int gaitGroup)
        {
            for (var index = 0; index < _legs.Length; index++)
            {
                var leg = _legs[index];
                if (leg.GaitGroup != gaitGroup)
                {
                    continue;
                }

                leg.DesiredWorldPosition = ComputeDesiredFootPosition(leg);
                if (Vector3.Distance(leg.PlantedWorldPosition, leg.DesiredWorldPosition) >= StepThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        void StartGroupStep(int gaitGroup)
        {
            _leadGaitGroup = gaitGroup;
            _nextStepGroup = 1 - gaitGroup;

            for (var index = 0; index < _legs.Length; index++)
            {
                var leg = _legs[index];
                if (leg.GaitGroup != gaitGroup)
                {
                    continue;
                }

                leg.DesiredWorldPosition = ComputeDesiredFootPosition(leg);
                leg.StepStartWorldPosition = leg.PlantedWorldPosition;
                leg.StepTargetWorldPosition = leg.DesiredWorldPosition;
                leg.StepProgress = 0f;
                leg.IsStepping = true;
            }
        }

        Vector3 ComputeDesiredFootPosition(SpiderLegState leg)
        {
            var localTarget = leg.RestLocalTarget;
            localTarget.x *= _currentWidthScale;

            var strideScale = CurrentFootprint == MovementFootprint.SqueezedSpider1x1 ? SqueezedStrideScale : 1f;
            localTarget.z *= strideScale;

            if (ShouldApplyStrideBias())
            {
                localTarget.z += GetForwardStrideBias(leg);
            }

            var world = _locomotionRoot.TransformPoint(localTarget);
            world.y = 0f;
            return world;
        }

        bool ShouldApplyStrideBias()
        {
            var velocity = _currentVelocity;
            velocity.y = 0f;
            return velocity.sqrMagnitude > 0.0001f || (_navigationBound && _pathIndex < _path.Count);
        }

        Vector3 ComputeKneeHintWorldPosition(SpiderLegState leg)
        {
            var localHintOffset = leg.KneeHintLocal - leg.LegRootBaseLocalPosition;
            var strideScale = CurrentFootprint == MovementFootprint.SqueezedSpider1x1 ? SqueezedStrideScale : 1f;

            // Force the pole vector to stay outside the body and above the hip,
            // so the leg keeps the intended M silhouette even while the body narrows.
            var outwardOffset = _locomotionRoot.right * (leg.SideSign * Mathf.Abs(localHintOffset.x) * _currentWidthScale);
            var upwardOffset = _locomotionRoot.up * Mathf.Max(0.24f, localHintOffset.y);
            var foreOffset = _locomotionRoot.forward * (leg.ForeSign * Mathf.Abs(localHintOffset.z) * strideScale);
            return leg.Hip.position + outwardOffset + upwardOffset + foreOffset;
        }

        float GetStepArcHeight()
        {
            return CurrentFootprint == MovementFootprint.SqueezedSpider1x1
                ? StepArcHeight * 0.8f
                : StepArcHeight;
        }

        float GetForwardStrideDistance()
        {
            return CurrentFootprint == MovementFootprint.SqueezedSpider1x1
                ? SqueezedForwardStrideDistance
                : ForwardStrideDistance;
        }

        float GetForwardStrideBias(SpiderLegState leg)
        {
            // One gait group stays forward while the opposite group trails behind.
            // When the trailing group begins a step, it becomes the new lead group.
            var phaseSign = leg.GaitGroup == _leadGaitGroup ? 0.5f : -0.5f;
            return GetForwardStrideDistance() * phaseSign;
        }

        void SnapFeetToRestPose()
        {
            if (!IsRigBound)
            {
                return;
            }

            for (var index = 0; index < _legs.Length; index++)
            {
                var leg = _legs[index];
                var planted = ComputeDesiredFootPosition(leg);
                leg.PlantedWorldPosition = planted;
                leg.DesiredWorldPosition = planted;
                leg.IsStepping = false;
                leg.StepProgress = 1f;
                leg.FootTarget.position = planted;

                var kneeHintWorldPosition = ComputeKneeHintWorldPosition(leg);
                TwoBoneLegIkSolver.ApplyToTransformsWithHint(
                    leg.Hip,
                    leg.Knee,
                    planted,
                    kneeHintWorldPosition,
                    leg.UpperLength,
                    leg.LowerLength);
            }
        }

        void UpdateEyeEmission(float intensityScale)
        {
            for (var index = 0; index < _eyeRenderers.Length; index++)
            {
                var renderer = _eyeRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                var block = _eyeBlocks[index];
                block.Clear();
                block.SetColor(EmissionColorShaderId, new Color(14f, 0.9f, 0.32f) * intensityScale);
                renderer.SetPropertyBlock(block);
            }
        }

        Vector3 ClampWorldPosition(Vector3 position)
        {
            var halfExtent = (_townFootprint * 0.5f) - (_worldCellSize * 0.5f);
            position.x = Mathf.Clamp(position.x, -halfExtent, halfExtent);
            position.z = Mathf.Clamp(position.z, -halfExtent, halfExtent);
            return position;
        }

        Vector3 CellToWorld(Vector2Int cell)
        {
            return new Vector3(
                (cell.x + 0.5f) * _worldCellSize - (_townFootprint * 0.5f),
                0f,
                (cell.y + 0.5f) * _worldCellSize - (_townFootprint * 0.5f));
        }

        Vector2Int WorldToCell(Vector3 position)
        {
            return new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt((position.x + (_townFootprint * 0.5f)) / _worldCellSize), 0, _grid.Width - 1),
                Mathf.Clamp(Mathf.FloorToInt((position.z + (_townFootprint * 0.5f)) / _worldCellSize), 0, _grid.Height - 1));
        }

        float Range(float minInclusive, float maxInclusive)
        {
            return Mathf.Lerp(minInclusive, maxInclusive, (float)_random.NextDouble());
        }

        void DrawFootTrajectoryGizmos()
        {
            for (var index = 0; index < _legs.Length; index++)
            {
                var leg = _legs[index];
                var baseColor = leg.GaitGroup == 0 ? GroupAColor : GroupBColor;
                var start = leg.IsStepping ? leg.StepStartWorldPosition : leg.PlantedWorldPosition;
                var end = leg.IsStepping ? leg.StepTargetWorldPosition : leg.DesiredWorldPosition;
                var kneeHint = ComputeKneeHintWorldPosition(leg);

                Gizmos.color = new Color(HintColor.r, HintColor.g, HintColor.b, HintColor.a);
                Gizmos.DrawLine(leg.Hip.position, kneeHint);
                Gizmos.DrawWireSphere(kneeHint, _gizmoPointRadius * 0.8f);

                Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.85f);
                DrawTrajectoryArc(start, end, GetStepArcHeight());

                Gizmos.color = PlantedColor;
                Gizmos.DrawSphere(leg.PlantedWorldPosition, _gizmoPointRadius);

                Gizmos.color = DesiredColor;
                Gizmos.DrawWireSphere(leg.DesiredWorldPosition, _gizmoPointRadius * 1.25f);

                Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.5f);
                Gizmos.DrawLine(leg.Hip.position, leg.PlantedWorldPosition);

                if (TryGetFootVisualTipWorldPosition(leg, out var visualTip))
                {
                    Gizmos.color = VisualTipColor;
                    Gizmos.DrawWireSphere(visualTip, _gizmoPointRadius);
                    Gizmos.DrawLine(visualTip, leg.PlantedWorldPosition);
                }

                if (leg.IsStepping)
                {
                    Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f);
                    Gizmos.DrawWireSphere(leg.StepStartWorldPosition, _gizmoPointRadius * 0.9f);
                    Gizmos.DrawWireSphere(leg.StepTargetWorldPosition, _gizmoPointRadius * 0.9f);
                }
            }
        }

        void DrawTrajectoryArc(Vector3 start, Vector3 end, float arcHeight)
        {
            var previous = start;
            for (var sampleIndex = 1; sampleIndex <= FootTrajectorySamples; sampleIndex++)
            {
                var t = sampleIndex / (float)FootTrajectorySamples;
                var point = Vector3.Lerp(start, end, t);
                point.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }

        static bool TryGetFootVisualTipWorldPosition(SpiderLegState leg, out Vector3 worldPosition)
        {
            var lowerVisual = leg.Knee.Find("LowerVisual");
            if (lowerVisual == null)
            {
                worldPosition = leg.Knee.position;
                return false;
            }

            var meshFilter = lowerVisual.GetComponentInChildren<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                worldPosition = lowerVisual.position;
                return false;
            }

            var tipLocalPosition = new Vector3(0f, meshFilter.sharedMesh.bounds.size.y * meshFilter.transform.localScale.y, 0f);
            worldPosition = lowerVisual.TransformPoint(tipLocalPosition);
            return true;
        }
    }
}
