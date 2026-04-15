#nullable enable

using UnityEngine;

namespace McpTest.VoxelVillage
{
    public sealed class SpiderLegState
    {
        public SpiderLegState(
            string id,
            Transform legRoot,
            Transform hip,
            Transform knee,
            Transform footTarget,
            Vector3 restLocalTarget,
            Vector3 bendNormalLocal,
            int gaitGroup,
            float sideSign,
            float foreSign)
        {
            Id = id;
            LegRoot = legRoot;
            Hip = hip;
            Knee = knee;
            FootTarget = footTarget;
            LegRootBaseLocalPosition = legRoot.localPosition;
            RestLocalTarget = restLocalTarget;
            BendNormalLocal = bendNormalLocal.normalized;
            GaitGroup = gaitGroup;
            SideSign = sideSign;
            ForeSign = foreSign;
            UpperLength = Vector3.Distance(hip.position, knee.position);
            LowerLength = Vector3.Distance(knee.position, footTarget.position);
            PlantedWorldPosition = footTarget.position;
            DesiredWorldPosition = footTarget.position;
        }

        public string Id { get; }

        public Transform LegRoot { get; }

        public Transform Hip { get; }

        public Transform Knee { get; }

        public Transform FootTarget { get; }

        public Vector3 LegRootBaseLocalPosition { get; }

        public Vector3 RestLocalTarget { get; }

        public Vector3 BendNormalLocal { get; }

        public int GaitGroup { get; }

        public float SideSign { get; }

        public float ForeSign { get; }

        public float UpperLength { get; }

        public float LowerLength { get; }

        public Vector3 PlantedWorldPosition { get; set; }

        public Vector3 DesiredWorldPosition { get; set; }

        public Vector3 StepStartWorldPosition { get; set; }

        public Vector3 StepTargetWorldPosition { get; set; }

        public float StepProgress { get; set; }

        public bool IsStepping { get; set; }
    }
}
