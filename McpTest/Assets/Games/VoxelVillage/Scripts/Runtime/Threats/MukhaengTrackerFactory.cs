#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace McpTest.VoxelVillage
{
    public readonly struct MukhaengTrackerBuildResult
    {
        public MukhaengTrackerBuildResult(GameObject root, MukhaengTrackerController controller)
        {
            Root = root;
            Controller = controller;
        }

        public GameObject Root { get; }

        public MukhaengTrackerController Controller { get; }
    }

    public static class MukhaengTrackerFactory
    {
        const float VoxelSize = 0.1f;
        const int PaletteSize = 3;

        const int ShellColor = 0;
        const int UndersideColor = 1;
        const int GlowColor = 2;

        static readonly Dictionary<MukhaengTrackerModuleType, Mesh> MeshCache = new Dictionary<MukhaengTrackerModuleType, Mesh>();
        static Material[]? SharedMaterials;
        static Material[]? SharedEyeMaterials;

        public static MukhaengTrackerBuildResult CreateTracker(
            string name,
            Vector3 position,
            float scaleFactor = 1f)
        {
            var clampedScale = Mathf.Max(0.01f, scaleFactor);
            var materials = GetOrCreateMaterials();
            var glowRenderers = new List<Renderer>(2);
            var legs = new List<MukhaengTrackerLegRig>(6);
            var tentacles = new List<MukhaengTrackerTentacleRig>(2);

            var root = new GameObject(name);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * clampedScale;

            var locomotionRoot = CreateNode(root.transform, "LocomotionRoot", Vector3.zero);
            var bodyPivot = CreateNode(locomotionRoot, "BodyPivot", new Vector3(0f, 2.05f, 0f));
            var mantleRoot = CreateNode(bodyPivot, "MantleRoot", Vector3.zero);
            var legRing = CreateNode(bodyPivot, "LegRing", new Vector3(0f, -0.12f, 0f));
            var attackTentacles = CreateNode(bodyPivot, "AttackTentacles", new Vector3(0f, -0.18f, 1.05f));

            CreateVisualObject(
                mantleRoot,
                "MantleCoreVisual",
                GetOrCreateMesh(MukhaengTrackerModuleType.MantleCore),
                materials,
                Vector3.zero,
                Vector3.one,
                VisualAlignment.CenterBase);

            CreateVisualObject(
                mantleRoot,
                "MantleUndersideVisual",
                GetOrCreateMesh(MukhaengTrackerModuleType.MantleUnderside),
                materials,
                new Vector3(0f, -0.32f, 0.2f),
                Vector3.one,
                VisualAlignment.CenterBase);

            glowRenderers.Add(CreateVisualObject(
                mantleRoot,
                "EyeCluster_L",
                GetOrCreateMesh(MukhaengTrackerModuleType.EyeCluster),
                GetOrCreateEyeMaterials(),
                new Vector3(-0.82f, 0.52f, 1.28f),
                new Vector3(0.82f, 0.82f, 0.82f),
                VisualAlignment.Center));

            glowRenderers.Add(CreateVisualObject(
                mantleRoot,
                "EyeCluster_R",
                GetOrCreateMesh(MukhaengTrackerModuleType.EyeCluster),
                GetOrCreateEyeMaterials(),
                new Vector3(0.82f, 0.52f, 1.28f),
                new Vector3(0.82f, 0.82f, 0.82f),
                VisualAlignment.Center));

            CreateVisualObject(
                mantleRoot,
                "MouthCore",
                GetOrCreateMesh(MukhaengTrackerModuleType.MouthCore),
                materials,
                new Vector3(0f, -0.58f, 1.52f),
                new Vector3(0.8f, 0.8f, 0.8f),
                VisualAlignment.Center);

            var legDefinitions = new[]
            {
                new LegDefinition("Leg_FL", new Vector3(-1.95f, -0.18f, 1.45f), 0f, -1f, 0.75f),
                new LegDefinition("Leg_FR", new Vector3(1.95f, -0.18f, 1.45f), Mathf.PI, 1f, 0.75f),
                new LegDefinition("Leg_ML", new Vector3(-2.28f, -0.3f, 0f), Mathf.PI * 0.5f, -1f, 0f),
                new LegDefinition("Leg_MR", new Vector3(2.28f, -0.3f, 0f), Mathf.PI * 1.5f, 1f, 0f),
                new LegDefinition("Leg_RL", new Vector3(-1.86f, -0.42f, -1.52f), Mathf.PI * 0.25f, -1f, -0.75f),
                new LegDefinition("Leg_RR", new Vector3(1.86f, -0.42f, -1.52f), Mathf.PI * 1.25f, 1f, -0.75f)
            };

            for (var index = 0; index < legDefinitions.Length; index++)
            {
                legs.Add(CreateLeg(legRing, legDefinitions[index], materials));
            }

            tentacles.Add(CreateTentacle(attackTentacles, "Tentacle_Attack_L", new Vector3(-0.58f, -0.14f, 0.54f), 0f, -1f, materials));
            tentacles.Add(CreateTentacle(attackTentacles, "Tentacle_Attack_R", new Vector3(0.58f, -0.14f, 0.54f), Mathf.PI, 1f, materials));

            var sensors = CreateNode(root.transform, "Sensors", Vector3.zero);
            CreateNode(sensors, "VisionOrigin", new Vector3(0f, 2.28f, 1.68f));
            CreateNode(sensors, "TargetOrigin", new Vector3(0f, 1.65f, 0.9f));
            var threatCenter = CreateNode(sensors, "ThreatCenter", new Vector3(0f, 1.82f, 0.35f));
            CreateNode(sensors, "AudioOrigin", new Vector3(0f, 1.52f, 0.1f));
            CreateNode(sensors, "InkCastOrigin", new Vector3(0f, 1.5f, 1.8f));
            CreateNode(sensors, "RetreatAnchor", new Vector3(0f, 0f, -4.2f));

            var gameplay = CreateNode(root.transform, "Gameplay", Vector3.zero);
            CreateBodyBlocker(gameplay);
            CreateCloseThreatCollider(gameplay);
            CreateTentacleThreatCollider(gameplay);
            CreateOccupancyBounds(gameplay);

            var fx = CreateNode(root.transform, "FX", Vector3.zero);
            CreateNode(fx, "EyeGlow_L", new Vector3(-0.82f, 2.55f, 1.28f));
            CreateNode(fx, "EyeGlow_R", new Vector3(0.82f, 2.55f, 1.28f));
            CreateNode(fx, "InkBurstFxOrigin", new Vector3(0f, 1.5f, 1.9f));
            CreateNode(fx, "GroundRippleOrigin", new Vector3(0f, 0.05f, 0.1f));

            var controller = root.AddComponent<MukhaengTrackerController>();
            controller.Initialize(
                locomotionRoot,
                bodyPivot,
                mantleRoot,
                threatCenter,
                legs.ToArray(),
                tentacles.ToArray(),
                glowRenderers.ToArray());

            return new MukhaengTrackerBuildResult(root, controller);
        }

        static MukhaengTrackerLegRig CreateLeg(Transform parent, LegDefinition definition, Material[] materials)
        {
            var legRoot = CreateNode(parent, definition.Name, definition.AnchorLocalPosition);
            var hip = CreateNode(legRoot, definition.Name + "_Hip", Vector3.zero);
            var upperVisual = CreateVisualObject(
                hip,
                definition.Name + "_UpperVisual",
                GetOrCreateMesh(MukhaengTrackerModuleType.LegUpper),
                materials,
                Vector3.zero,
                new Vector3(0.9f, 1f, 0.9f),
                VisualAlignment.CenterBase);

            var upperDirection = new Vector3(
                definition.AnchorLocalPosition.x * 0.74f,
                -1.1f,
                definition.AnchorLocalPosition.z * 0.74f).normalized;
            hip.localRotation = Quaternion.FromToRotation(Vector3.up, upperDirection);

            var knee = CreateNode(
                upperVisual.transform,
                definition.Name + "_Knee",
                GetTopCenterLocalPosition(upperVisual.GetComponent<MeshFilter>().sharedMesh!, upperVisual.transform.localScale));

            var lowerVisual = CreateVisualObject(
                knee,
                definition.Name + "_LowerVisual",
                GetOrCreateMesh(MukhaengTrackerModuleType.LegLower),
                materials,
                Vector3.zero,
                new Vector3(0.84f, 0.95f, 0.84f),
                VisualAlignment.CenterBase);

            var lowerDirection = new Vector3(
                definition.AnchorLocalPosition.x * 0.34f,
                -1.24f,
                definition.AnchorLocalPosition.z * 0.34f).normalized;
            knee.localRotation = Quaternion.FromToRotation(Vector3.up, lowerDirection);

            var ankle = CreateNode(
                lowerVisual.transform,
                definition.Name + "_Ankle",
                GetTopCenterLocalPosition(lowerVisual.GetComponent<MeshFilter>().sharedMesh!, lowerVisual.transform.localScale));

            var tipVisual = CreateVisualObject(
                ankle,
                definition.Name + "_TipVisual",
                GetOrCreateMesh(MukhaengTrackerModuleType.LegTip),
                materials,
                Vector3.zero,
                new Vector3(0.92f, 0.72f, 1.04f),
                VisualAlignment.CenterBase);

            ankle.localRotation = Quaternion.FromToRotation(
                Vector3.up,
                new Vector3(
                    definition.AnchorLocalPosition.x * 0.1f,
                    -1f,
                    definition.AnchorLocalPosition.z * 0.1f).normalized);

            var footTarget = CreateNode(
                tipVisual.transform,
                definition.Name + "_FootTarget",
                GetTopCenterLocalPosition(tipVisual.GetComponent<MeshFilter>().sharedMesh!, tipVisual.transform.localScale) +
                new Vector3(definition.SideSign * 0.1f, 0.05f, definition.ForeAftBias * 0.08f));

            return new MukhaengTrackerLegRig(
                definition.Name,
                hip,
                knee,
                ankle,
                footTarget,
                definition.PhaseOffset,
                definition.SideSign,
                definition.ForeAftBias);
        }

        static MukhaengTrackerTentacleRig CreateTentacle(
            Transform parent,
            string name,
            Vector3 anchorLocalPosition,
            float phaseOffset,
            float sideSign,
            Material[] materials)
        {
            var root = CreateNode(parent, name, anchorLocalPosition);
            var @base = CreateNode(root, name + "_Base", Vector3.zero);
            var baseVisual = CreateVisualObject(
                @base,
                name + "_BaseVisual",
                GetOrCreateMesh(MukhaengTrackerModuleType.TentacleBase),
                materials,
                Vector3.zero,
                new Vector3(0.78f, 1f, 0.78f),
                VisualAlignment.CenterBase);

            @base.localRotation = Quaternion.FromToRotation(
                Vector3.up,
                new Vector3(sideSign * 0.22f, 0.72f, 0.74f).normalized);

            var mid = CreateNode(
                @base,
                name + "_Mid",
                GetTopCenterLocalPosition(baseVisual.GetComponent<MeshFilter>().sharedMesh!, baseVisual.transform.localScale));

            var midVisual = CreateVisualObject(
                mid,
                name + "_MidVisual",
                GetOrCreateMesh(MukhaengTrackerModuleType.TentacleMid),
                materials,
                Vector3.zero,
                new Vector3(0.7f, 0.94f, 0.7f),
                VisualAlignment.CenterBase);

            mid.localRotation = Quaternion.FromToRotation(
                Vector3.up,
                new Vector3(sideSign * 0.14f, 0.62f, 0.78f).normalized);

            var tip = CreateNode(
                mid,
                name + "_Tip",
                GetTopCenterLocalPosition(midVisual.GetComponent<MeshFilter>().sharedMesh!, midVisual.transform.localScale));

            var tipVisual = CreateVisualObject(
                tip,
                name + "_TipVisual",
                GetOrCreateMesh(MukhaengTrackerModuleType.TentacleTip),
                materials,
                Vector3.zero,
                new Vector3(0.62f, 0.86f, 0.62f),
                VisualAlignment.CenterBase);

            tip.localRotation = Quaternion.FromToRotation(
                Vector3.up,
                new Vector3(sideSign * 0.08f, 0.54f, 0.84f).normalized);

            var hitOrigin = CreateNode(
                tip,
                name + "_HitOrigin",
                GetTopCenterLocalPosition(tipVisual.GetComponent<MeshFilter>().sharedMesh!, tipVisual.transform.localScale));

            return new MukhaengTrackerTentacleRig(name, @base, mid, tip, hitOrigin, phaseOffset, sideSign);
        }

        static Transform CreateNode(Transform parent, string name, Vector3 localPosition)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.localPosition = localPosition;
            return node;
        }

        static MeshRenderer CreateVisualObject(
            Transform parent,
            string name,
            Mesh mesh,
            Material[] materials,
            Vector3 anchorLocalPosition,
            Vector3 localScale,
            VisualAlignment alignment)
        {
            var visual = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = localScale;
            visual.transform.localPosition = anchorLocalPosition + GetAlignmentOffset(mesh, localScale, alignment);

            var meshFilter = visual.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = visual.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = materials;
            meshRenderer.shadowCastingMode = ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;
            return meshRenderer;
        }

        static Vector3 GetAlignmentOffset(Mesh mesh, Vector3 localScale, VisualAlignment alignment)
        {
            var bounds = mesh.bounds;
            Vector3 pivotOffset;
            switch (alignment)
            {
                case VisualAlignment.Center:
                    pivotOffset = -bounds.center;
                    break;
                default:
                    pivotOffset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                    break;
            }

            return Vector3.Scale(pivotOffset, localScale);
        }

        static Vector3 GetTopCenterLocalPosition(Mesh mesh, Vector3 localScale)
        {
            var bounds = mesh.bounds;
            return new Vector3(
                bounds.center.x * localScale.x,
                bounds.size.y * localScale.y,
                bounds.center.z * localScale.z);
        }

        static void CreateBodyBlocker(Transform gameplay)
        {
            var blocker = CreateNode(gameplay, "BodyBlocker", new Vector3(0f, 1.55f, 0.2f));
            var collider = blocker.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(3.1f, 1.55f, 2.8f);
        }

        static void CreateCloseThreatCollider(Transform gameplay)
        {
            var node = CreateNode(gameplay, "ThreatCollider_Close", new Vector3(0f, 1.45f, 0.35f));
            var collider = node.gameObject.AddComponent<SphereCollider>();
            collider.radius = 2.9f;
            collider.isTrigger = true;
        }

        static void CreateTentacleThreatCollider(Transform gameplay)
        {
            var node = CreateNode(gameplay, "ThreatCollider_Tentacle", new Vector3(0f, 1.6f, 1.1f));
            var collider = node.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.5f, 1.4f, 4.6f);
            collider.isTrigger = true;
        }

        static void CreateOccupancyBounds(Transform gameplay)
        {
            var node = CreateNode(gameplay, "OccupancyBounds", new Vector3(0f, 0.8f, 0f));
            var collider = node.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(4.25f, 1.6f, 4.25f);
            collider.isTrigger = true;
        }

        static Mesh GetOrCreateMesh(MukhaengTrackerModuleType moduleType)
        {
            if (MeshCache.TryGetValue(moduleType, out var mesh) && mesh != null)
            {
                return mesh;
            }

            mesh = VoxelMeshBuilder.Build(CreateModel(moduleType), VoxelSize, PaletteSize, "MukhaengTracker_" + moduleType);
            MeshCache[moduleType] = mesh;
            return mesh;
        }

        static Material[] GetOrCreateMaterials()
        {
            if (SharedMaterials != null && AreMaterialsAlive(SharedMaterials))
            {
                return SharedMaterials;
            }

            SharedMaterials = new[]
            {
                CreateMaterial(new Color(0.11f, 0.16f, 0.19f), false),
                CreateMaterial(new Color(0.66f, 0.73f, 0.75f), false),
                CreateMaterial(new Color(0.66f, 0.95f, 0.84f), true)
            };

            return SharedMaterials;
        }

        static Material[] GetOrCreateEyeMaterials()
        {
            if (SharedEyeMaterials != null && AreMaterialsAlive(SharedEyeMaterials))
            {
                return SharedEyeMaterials;
            }

            SharedEyeMaterials = new[]
            {
                CreateMaterial(new Color(0.16f, 0.1f, 0.11f), false),
                CreateMaterial(new Color(0.28f, 0.08f, 0.08f), false),
                CreateMaterial(new Color(0.96f, 0.08f, 0.06f), true)
            };

            return SharedEyeMaterials;
        }

        static bool AreMaterialsAlive(Material[] materials)
        {
            if (materials.Length != PaletteSize)
            {
                return false;
            }

            for (var index = 0; index < materials.Length; index++)
            {
                if (materials[index] == null)
                {
                    return false;
                }
            }

            return true;
        }

        static Material CreateMaterial(Color color, bool emissive)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader)
            {
                enableInstancing = true
            };

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", emissive ? 0.12f : 0.04f);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", emissive ? 0.12f : 0.04f);
            }

            if (emissive)
            {
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", color * 10f);
                }

                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            return material;
        }

        static VoxelModel32 CreateModel(MukhaengTrackerModuleType moduleType)
        {
            var model = new VoxelModel32();
            switch (moduleType)
            {
                case MukhaengTrackerModuleType.MantleCore:
                    BuildMantleCoreModel(model);
                    break;
                case MukhaengTrackerModuleType.MantleUnderside:
                    BuildMantleUndersideModel(model);
                    break;
                case MukhaengTrackerModuleType.EyeCluster:
                    BuildEyeClusterModel(model);
                    break;
                case MukhaengTrackerModuleType.MouthCore:
                    BuildMouthCoreModel(model);
                    break;
                case MukhaengTrackerModuleType.LegUpper:
                    BuildLegUpperModel(model);
                    break;
                case MukhaengTrackerModuleType.LegLower:
                    BuildLegLowerModel(model);
                    break;
                case MukhaengTrackerModuleType.LegTip:
                    BuildLegTipModel(model);
                    break;
                case MukhaengTrackerModuleType.TentacleBase:
                    BuildTentacleBaseModel(model);
                    break;
                case MukhaengTrackerModuleType.TentacleMid:
                    BuildTentacleMidModel(model);
                    break;
                default:
                    BuildTentacleTipModel(model);
                    break;
            }

            return model;
        }

        static void BuildMantleCoreModel(VoxelModel32 model)
        {
            model.FillBox(6, 0, 6, 26, 8, 28, ShellColor);
            model.FillBox(4, 4, 8, 28, 12, 24, ShellColor);
            model.FillBox(8, 8, 10, 24, 15, 22, ShellColor);
            model.FillBox(10, 12, 12, 22, 18, 20, ShellColor);
            model.FillBox(9, 2, 22, 23, 8, 31, ShellColor);
            model.FillBox(5, 5, 20, 11, 11, 30, ShellColor);
            model.FillBox(21, 5, 20, 27, 11, 30, ShellColor);
            model.FillBox(9, 1, 9, 23, 3, 14, UndersideColor);
        }

        static void BuildMantleUndersideModel(VoxelModel32 model)
        {
            model.FillBox(8, 0, 7, 24, 4, 24, UndersideColor);
            model.FillBox(10, 2, 20, 22, 6, 31, UndersideColor);
            model.FillBox(12, 1, 23, 20, 4, 31, GlowColor);
        }

        static void BuildEyeClusterModel(VoxelModel32 model)
        {
            model.FillBox(10, 8, 8, 22, 14, 20, ShellColor);
            model.FillBox(12, 9, 18, 20, 14, 24, GlowColor);
            model.FillBox(14, 10, 22, 18, 13, 26, GlowColor);
        }

        static void BuildMouthCoreModel(VoxelModel32 model)
        {
            model.FillBox(12, 0, 10, 20, 6, 20, UndersideColor);
            model.FillBox(14, 0, 12, 18, 4, 18, ShellColor);
            model.FillBox(13, 1, 19, 19, 5, 25, GlowColor);
        }

        static void BuildLegUpperModel(VoxelModel32 model)
        {
            model.FillBox(11, 0, 11, 21, 16, 21, ShellColor);
            model.FillBox(9, 12, 9, 23, 20, 23, UndersideColor);
            model.FillBox(12, 3, 20, 20, 12, 22, GlowColor);
        }

        static void BuildLegLowerModel(VoxelModel32 model)
        {
            model.FillBox(12, 0, 12, 20, 15, 20, ShellColor);
            model.FillBox(10, 10, 10, 22, 18, 22, UndersideColor);
            model.FillBox(13, 2, 19, 19, 10, 21, GlowColor);
        }

        static void BuildLegTipModel(VoxelModel32 model)
        {
            model.FillBox(11, 0, 11, 21, 10, 21, ShellColor);
            model.FillBox(8, 8, 8, 24, 14, 24, UndersideColor);
            model.FillBox(10, 10, 16, 22, 14, 24, GlowColor);
        }

        static void BuildTentacleBaseModel(VoxelModel32 model)
        {
            model.FillBox(11, 0, 11, 21, 18, 21, ShellColor);
            model.FillBox(10, 14, 10, 22, 22, 22, UndersideColor);
            model.FillBox(12, 0, 20, 20, 16, 23, GlowColor);
        }

        static void BuildTentacleMidModel(VoxelModel32 model)
        {
            model.FillBox(12, 0, 12, 20, 18, 20, ShellColor);
            model.FillBox(11, 14, 11, 21, 22, 21, UndersideColor);
            model.FillBox(13, 0, 19, 19, 18, 21, GlowColor);
        }

        static void BuildTentacleTipModel(VoxelModel32 model)
        {
            model.FillBox(13, 0, 13, 19, 16, 19, ShellColor);
            model.FillBox(12, 12, 12, 20, 20, 20, UndersideColor);
            model.FillBox(14, 0, 18, 18, 16, 20, GlowColor);
        }

        enum MukhaengTrackerModuleType
        {
            MantleCore,
            MantleUnderside,
            EyeCluster,
            MouthCore,
            LegUpper,
            LegLower,
            LegTip,
            TentacleBase,
            TentacleMid,
            TentacleTip
        }

        enum VisualAlignment
        {
            Center,
            CenterBase
        }

        readonly struct LegDefinition
        {
            public LegDefinition(string name, Vector3 anchorLocalPosition, float phaseOffset, float sideSign, float foreAftBias)
            {
                Name = name;
                AnchorLocalPosition = anchorLocalPosition;
                PhaseOffset = phaseOffset;
                SideSign = sideSign;
                ForeAftBias = foreAftBias;
            }

            public string Name { get; }

            public Vector3 AnchorLocalPosition { get; }

            public float PhaseOffset { get; }

            public float SideSign { get; }

            public float ForeAftBias { get; }
        }
    }
}
