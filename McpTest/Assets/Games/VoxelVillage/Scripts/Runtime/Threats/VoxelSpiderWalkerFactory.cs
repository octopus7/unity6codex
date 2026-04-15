#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace McpTest.VoxelVillage
{
    public readonly struct SpiderWalkerBuildResult
    {
        public SpiderWalkerBuildResult(GameObject root, AmbientSpiderWalkerController controller)
        {
            Root = root;
            Controller = controller;
        }

        public GameObject Root { get; }

        public AmbientSpiderWalkerController Controller { get; }
    }

    public static class VoxelSpiderWalkerFactory
    {
        const float VoxelSize = 0.08f;
        const int PaletteSize = 1;
        static readonly Vector3 EyeScale = Vector3.one * 0.25f;
        static readonly Vector3 LegThicknessScale = new Vector3(0.25f, 1f, 0.25f);

        static readonly Dictionary<SpiderWalkerModuleType, Mesh> MeshCache = new Dictionary<SpiderWalkerModuleType, Mesh>();
        static Material? SharedBodyMaterial;
        static Material? SharedLegMaterial;
        static Material? SharedEyeMaterial;

        public static SpiderWalkerBuildResult CreateInstance(string name, Vector3 position, float scaleFactor = 1f)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * Mathf.Max(0.01f, scaleFactor);
            return EnsureInstance(root);
        }

        public static SpiderWalkerBuildResult EnsureInstance(GameObject root)
        {
            root.name = "VV_Ambient_SpiderWalker";

            var controller = root.GetComponent<AmbientSpiderWalkerController>();
            if (controller == null)
            {
                controller = root.AddComponent<AmbientSpiderWalkerController>();
            }

            if (controller.IsRigBound)
            {
                return new SpiderWalkerBuildResult(root, controller);
            }

            var locomotionRoot = CreateNode(root.transform, "LocomotionRoot", Vector3.zero);
            var bodyPivot = CreateNode(locomotionRoot, "BodyPivot", new Vector3(0f, 1.34f, 0f));
            var bodyShell = CreateVisualObject(
                bodyPivot,
                "BodyShell",
                GetOrCreateMesh(SpiderWalkerModuleType.BodyShell),
                GetOrCreateBodyMaterial(),
                Vector3.zero,
                new Vector3(1.4f, 1f, 1.4f),
                VisualAlignment.Center);

            var eyeCluster = CreateNode(bodyPivot, "EyeCluster", new Vector3(0f, 0.28f, 0.98f));
            var eyeRenderers = new List<Renderer>(8);
            for (var row = 0; row < 2; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    var eyeIndex = (row * 4) + column;
                    var eye = CreateVisualObject(
                        eyeCluster,
                        $"Eye_{eyeIndex + 1:00}",
                        GetOrCreateMesh(SpiderWalkerModuleType.Eye),
                        GetOrCreateEyeMaterial(),
                        new Vector3(-0.34f + (column * 0.22f), 0.09f - (row * 0.14f), 0f),
                        EyeScale,
                        VisualAlignment.Center);
                    eye.shadowCastingMode = ShadowCastingMode.Off;
                    eye.receiveShadows = false;
                    eyeRenderers.Add(eye);
                }
            }

            var legs = new[]
            {
                CreateLeg(locomotionRoot, "Leg_FL", new Vector3(-0.82f, 0.72f, 0.86f), new Vector3(-1.42f, -1.06f, 0.74f), 0, -1f, 1f),
                CreateLeg(locomotionRoot, "Leg_FR", new Vector3(0.82f, 0.72f, 0.86f), new Vector3(1.42f, -1.06f, 0.74f), 1, 1f, 1f),
                CreateLeg(locomotionRoot, "Leg_BL", new Vector3(-0.84f, 0.68f, -0.82f), new Vector3(-1.46f, -1.04f, -0.78f), 1, -1f, -1f),
                CreateLeg(locomotionRoot, "Leg_BR", new Vector3(0.84f, 0.68f, -0.82f), new Vector3(1.46f, -1.04f, -0.78f), 0, 1f, -1f)
            };

            controller.BindRig(locomotionRoot, bodyPivot, bodyShell.transform, eyeCluster, eyeRenderers.ToArray(), legs);
            return new SpiderWalkerBuildResult(root, controller);
        }

        static SpiderLegState CreateLeg(
            Transform parent,
            string name,
            Vector3 rootLocalPosition,
            Vector3 footLocalPosition,
            int gaitGroup,
            float sideSign,
            float foreSign)
        {
            var legRoot = CreateNode(parent, name, rootLocalPosition);
            var hip = CreateNode(legRoot, "Hip", Vector3.zero);
            var upperVisual = CreateVisualObject(
                hip,
                "UpperVisual",
                GetOrCreateMesh(SpiderWalkerModuleType.LegUpper),
                GetOrCreateLegMaterial(),
                Vector3.zero,
                LegThicknessScale,
                VisualAlignment.CenterBase);

            var knee = CreateNode(
                upperVisual.transform,
                "Knee",
                GetTopCenterLocalPosition(upperVisual.GetComponent<MeshFilter>().sharedMesh!, upperVisual.transform.localScale));

            var lowerVisual = CreateVisualObject(
                knee,
                "LowerVisual",
                GetOrCreateMesh(SpiderWalkerModuleType.LegLower),
                GetOrCreateLegMaterial(),
                Vector3.zero,
                LegThicknessScale,
                VisualAlignment.CenterBase);

            var footTarget = CreateNode(legRoot, "FootTarget", footLocalPosition - rootLocalPosition);

            var upperDirection = new Vector3((footTarget.localPosition.x * 0.68f), footTarget.localPosition.y * 0.52f, footTarget.localPosition.z * 0.64f).normalized;
            var lowerDirection = new Vector3((footTarget.localPosition.x * 0.34f), footTarget.localPosition.y * 0.48f, footTarget.localPosition.z * 0.36f).normalized;
            hip.localRotation = Quaternion.FromToRotation(Vector3.up, upperDirection);
            knee.localRotation = Quaternion.FromToRotation(Vector3.up, lowerDirection);

            return new SpiderLegState(
                name,
                legRoot,
                hip,
                knee,
                footTarget,
                footLocalPosition,
                new Vector3(0f, 0f, foreSign),
                gaitGroup,
                sideSign,
                foreSign);
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
            Material material,
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
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;
            return meshRenderer;
        }

        static Vector3 GetAlignmentOffset(Mesh mesh, Vector3 localScale, VisualAlignment alignment)
        {
            var bounds = mesh.bounds;
            var pivotOffset = alignment == VisualAlignment.Center
                ? -bounds.center
                : new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
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

        static Mesh GetOrCreateMesh(SpiderWalkerModuleType moduleType)
        {
            if (MeshCache.TryGetValue(moduleType, out var mesh) && mesh != null)
            {
                return mesh;
            }

            mesh = VoxelMeshBuilder.Build(CreateModel(moduleType), VoxelSize, PaletteSize, "SpiderWalker_" + moduleType);
            MeshCache[moduleType] = mesh;
            return mesh;
        }

        static Material GetOrCreateBodyMaterial()
        {
            if (SharedBodyMaterial == null)
            {
                SharedBodyMaterial = CreateMaterial(new Color(0.16f, 0.17f, 0.19f), false, 0.08f);
            }

            return SharedBodyMaterial;
        }

        static Material GetOrCreateLegMaterial()
        {
            if (SharedLegMaterial == null)
            {
                SharedLegMaterial = CreateMaterial(new Color(0.2f, 0.23f, 0.26f), false, 0.06f);
            }

            return SharedLegMaterial;
        }

        static Material GetOrCreateEyeMaterial()
        {
            if (SharedEyeMaterial == null)
            {
                SharedEyeMaterial = CreateMaterial(new Color(1f, 0.18f, 0.08f), true, 0.14f);
            }

            return SharedEyeMaterial;
        }

        static Material CreateMaterial(Color color, bool emissive, float smoothness)
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
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", new Color(12f, 0.6f, 0.24f));
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            return material;
        }

        static VoxelModel32 CreateModel(SpiderWalkerModuleType moduleType)
        {
            var model = new VoxelModel32();
            switch (moduleType)
            {
                case SpiderWalkerModuleType.BodyShell:
                    model.FillBox(7, 7, 5, 25, 17, 27, 0);
                    model.FillBox(5, 10, 8, 27, 20, 24, 0);
                    model.FillBox(9, 16, 10, 23, 23, 22, 0);
                    model.FillBox(12, 18, 18, 20, 24, 29, 0);
                    model.FillBox(10, 5, 18, 22, 11, 30, 0);
                    break;
                case SpiderWalkerModuleType.LegUpper:
                    model.FillBox(12, 0, 12, 20, 16, 20, 0);
                    model.FillBox(10, 12, 10, 22, 20, 22, 0);
                    break;
                case SpiderWalkerModuleType.LegLower:
                    model.FillBox(13, 0, 13, 19, 18, 19, 0);
                    model.FillBox(11, 14, 11, 21, 22, 21, 0);
                    model.FillBox(10, 18, 10, 22, 24, 22, 0);
                    break;
                default:
                    model.FillBox(10, 10, 10, 22, 22, 22, 0);
                    break;
            }

            return model;
        }

        enum SpiderWalkerModuleType
        {
            BodyShell,
            LegUpper,
            LegLower,
            Eye
        }

        enum VisualAlignment
        {
            Center,
            CenterBase
        }
    }
}
