#nullable enable

using UnityEngine;

namespace McpTest.VoxelVillage
{
    public readonly struct TwoBoneIkPose
    {
        public TwoBoneIkPose(Vector3 kneePosition, Vector3 targetPosition, float clampedDistance)
        {
            KneePosition = kneePosition;
            TargetPosition = targetPosition;
            ClampedDistance = clampedDistance;
        }

        public Vector3 KneePosition { get; }

        public Vector3 TargetPosition { get; }

        public float ClampedDistance { get; }
    }

    public static class TwoBoneLegIkSolver
    {
        const float Epsilon = 0.0001f;

        public static TwoBoneIkPose Solve(
            Vector3 hipPosition,
            Vector3 targetPosition,
            Vector3 bendNormal,
            float upperLength,
            float lowerLength)
        {
            var toTarget = targetPosition - hipPosition;
            var targetDistance = toTarget.magnitude;
            var direction = targetDistance > Epsilon ? toTarget / targetDistance : Vector3.down;

            var planeNormal = GetStablePlaneNormal(direction, bendNormal);
            var bendDirection = Vector3.Cross(planeNormal, direction).normalized;

            var minReach = Mathf.Max(Epsilon, Mathf.Abs(upperLength - lowerLength) + Epsilon);
            var maxReach = Mathf.Max(minReach, (upperLength + lowerLength) - Epsilon);
            var clampedDistance = Mathf.Clamp(targetDistance, minReach, maxReach);
            var clampedTarget = hipPosition + (direction * clampedDistance);

            var along = ((upperLength * upperLength) - (lowerLength * lowerLength) + (clampedDistance * clampedDistance)) / (2f * clampedDistance);
            var heightSquared = Mathf.Max(0f, (upperLength * upperLength) - (along * along));
            var height = Mathf.Sqrt(heightSquared);
            var kneePosition = hipPosition + (direction * along) + (bendDirection * height);

            return new TwoBoneIkPose(kneePosition, clampedTarget, clampedDistance);
        }

        public static TwoBoneIkPose ApplyToTransforms(
            Transform hip,
            Transform knee,
            Vector3 targetPosition,
            Vector3 bendNormal,
            float upperLength,
            float lowerLength)
        {
            var pose = Solve(hip.position, targetPosition, bendNormal, upperLength, lowerLength);
            var planeNormal = GetStablePlaneNormal((pose.TargetPosition - hip.position).normalized, bendNormal);

            var hipDirection = (pose.KneePosition - hip.position).normalized;
            var kneeDirection = (pose.TargetPosition - pose.KneePosition).normalized;

            hip.rotation = CreateSegmentRotation(hipDirection, planeNormal);
            knee.rotation = CreateSegmentRotation(kneeDirection, planeNormal);
            return pose;
        }

        public static TwoBoneIkPose SolveWithHint(
            Vector3 hipPosition,
            Vector3 targetPosition,
            Vector3 kneeHintPosition,
            float upperLength,
            float lowerLength)
        {
            var toTarget = targetPosition - hipPosition;
            var targetDistance = toTarget.magnitude;
            var direction = targetDistance > Epsilon ? toTarget / targetDistance : Vector3.down;

            var minReach = Mathf.Max(Epsilon, Mathf.Abs(upperLength - lowerLength) + Epsilon);
            var maxReach = Mathf.Max(minReach, (upperLength + lowerLength) - Epsilon);
            var clampedDistance = Mathf.Clamp(targetDistance, minReach, maxReach);
            var clampedTarget = hipPosition + (direction * clampedDistance);

            var toHint = kneeHintPosition - hipPosition;
            var bendDirection = Vector3.ProjectOnPlane(toHint, direction);
            if (bendDirection.sqrMagnitude <= Epsilon)
            {
                bendDirection = Vector3.ProjectOnPlane(Vector3.up, direction);
            }

            if (bendDirection.sqrMagnitude <= Epsilon)
            {
                bendDirection = Vector3.ProjectOnPlane(Vector3.right, direction);
            }

            bendDirection.Normalize();

            var along = ((upperLength * upperLength) - (lowerLength * lowerLength) + (clampedDistance * clampedDistance)) / (2f * clampedDistance);
            var heightSquared = Mathf.Max(0f, (upperLength * upperLength) - (along * along));
            var height = Mathf.Sqrt(heightSquared);
            var kneePosition = hipPosition + (direction * along) + (bendDirection * height);

            return new TwoBoneIkPose(kneePosition, clampedTarget, clampedDistance);
        }

        public static TwoBoneIkPose ApplyToTransformsWithHint(
            Transform hip,
            Transform knee,
            Vector3 targetPosition,
            Vector3 kneeHintPosition,
            float upperLength,
            float lowerLength)
        {
            var pose = SolveWithHint(hip.position, targetPosition, kneeHintPosition, upperLength, lowerLength);
            var hipToTarget = (pose.TargetPosition - hip.position).normalized;
            var hipToKnee = (pose.KneePosition - hip.position).normalized;
            var planeNormal = Vector3.Cross(hipToTarget, hipToKnee);
            if (planeNormal.sqrMagnitude <= Epsilon)
            {
                planeNormal = Vector3.Cross(hipToTarget, Vector3.up);
            }

            if (planeNormal.sqrMagnitude <= Epsilon)
            {
                planeNormal = Vector3.forward;
            }

            planeNormal.Normalize();

            var hipDirection = (pose.KneePosition - hip.position).normalized;
            var kneeDirection = (pose.TargetPosition - pose.KneePosition).normalized;
            hip.rotation = CreateSegmentRotation(hipDirection, planeNormal);
            knee.rotation = CreateSegmentRotation(kneeDirection, planeNormal);
            return pose;
        }

        static Quaternion CreateSegmentRotation(Vector3 upDirection, Vector3 planeNormal)
        {
            var safeUp = upDirection.sqrMagnitude > Epsilon ? upDirection.normalized : Vector3.up;
            var forward = Vector3.Cross(planeNormal, safeUp);
            if (forward.sqrMagnitude <= Epsilon)
            {
                forward = Vector3.Cross(safeUp, Mathf.Abs(Vector3.Dot(safeUp, Vector3.right)) > 0.8f ? Vector3.forward : Vector3.right);
            }

            return Quaternion.LookRotation(forward.normalized, safeUp);
        }

        static Vector3 GetStablePlaneNormal(Vector3 direction, Vector3 bendNormal)
        {
            var planeNormal = bendNormal.sqrMagnitude > Epsilon ? bendNormal.normalized : Vector3.forward;
            if (Mathf.Abs(Vector3.Dot(planeNormal, direction)) > 0.98f)
            {
                planeNormal = Mathf.Abs(Vector3.Dot(direction, Vector3.right)) > 0.8f
                    ? Vector3.forward
                    : Vector3.right;
            }

            if (Mathf.Abs(Vector3.Dot(planeNormal, direction)) > 0.98f)
            {
                planeNormal = Vector3.up;
            }

            return planeNormal.normalized;
        }
    }
}
