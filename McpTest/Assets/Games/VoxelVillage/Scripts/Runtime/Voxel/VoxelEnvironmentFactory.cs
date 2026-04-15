#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace McpTest.VoxelVillage
{
    public enum VoxelStructureType
    {
        House,
        Door,
        Tree,
        Shrub,
        Flower,
        Fountain
    }

    public readonly struct VoxelStructureBuildResult
    {
        public VoxelStructureBuildResult(GameObject root, Vector3 localSize)
        {
            Root = root;
            LocalSize = localSize;
        }

        public GameObject Root { get; }

        public Vector3 LocalSize { get; }
    }

    public static class VoxelEnvironmentFactory
    {
        const int PaletteSize = 6;
        const float BaseVoxelSize = 0.25f;

        const int PrimaryColor = 0;
        const int SecondaryColor = 1;
        const int TrimColor = 2;
        const int DarkColor = 3;
        const int AccentColor = 4;
        const int LightColor = 5;

        static readonly Dictionary<VoxelStructureType, Mesh> MeshCache = new Dictionary<VoxelStructureType, Mesh>();
        static readonly Dictionary<string, Material[]> MaterialCache = new Dictionary<string, Material[]>();

        public static VoxelStructureBuildResult CreateHouse(
            string name,
            Vector3 position,
            Vector3 targetSize,
            float yaw,
            Color wallColor,
            Color roofColor,
            Color trimColor)
        {
            return CreateStructure(
                name,
                VoxelStructureType.House,
                position,
                yaw,
                targetSize,
                CreatePalette(
                    wallColor,
                    roofColor,
                    trimColor,
                    Color.Lerp(trimColor, Color.black, 0.35f),
                    new Color(0.64f, 0.87f, 0.95f),
                    new Color(0.96f, 0.94f, 0.83f)));
        }

        public static VoxelStructureBuildResult CreateDoor(
            string name,
            Vector3 position,
            Vector3 targetSize,
            float yaw,
            Color woodColor)
        {
            return CreateStructure(
                name,
                VoxelStructureType.Door,
                position,
                yaw,
                targetSize,
                CreatePalette(
                    woodColor,
                    Color.Lerp(woodColor, Color.white, 0.2f),
                    Color.Lerp(woodColor, Color.black, 0.25f),
                    Color.Lerp(woodColor, Color.black, 0.45f),
                    new Color(0.9f, 0.8f, 0.48f),
                    new Color(0.98f, 0.94f, 0.86f)));
        }

        public static VoxelStructureBuildResult CreateTree(
            string name,
            Vector3 position,
            Vector3 targetSize,
            float yaw,
            Color trunkColor,
            Color foliageColor)
        {
            return CreateStructure(
                name,
                VoxelStructureType.Tree,
                position,
                yaw,
                targetSize,
                CreatePalette(
                    trunkColor,
                    foliageColor,
                    Color.Lerp(foliageColor, Color.white, 0.18f),
                    Color.Lerp(trunkColor, Color.black, 0.36f),
                    Color.Lerp(foliageColor, new Color(0.94f, 0.9f, 0.42f), 0.2f),
                    Color.Lerp(foliageColor, Color.white, 0.32f)));
        }

        public static VoxelStructureBuildResult CreateShrub(
            string name,
            Vector3 position,
            Vector3 targetSize,
            float yaw,
            Color shrubColor)
        {
            return CreateStructure(
                name,
                VoxelStructureType.Shrub,
                position,
                yaw,
                targetSize,
                CreatePalette(
                    shrubColor,
                    Color.Lerp(shrubColor, Color.white, 0.12f),
                    Color.Lerp(shrubColor, Color.white, 0.24f),
                    Color.Lerp(shrubColor, Color.black, 0.36f),
                    new Color(0.92f, 0.78f, 0.32f),
                    Color.Lerp(shrubColor, Color.white, 0.42f)));
        }

        public static VoxelStructureBuildResult CreateFlower(
            string name,
            Vector3 position,
            Vector3 targetSize,
            float yaw,
            Color petalColor)
        {
            return CreateStructure(
                name,
                VoxelStructureType.Flower,
                position,
                yaw,
                targetSize,
                CreatePalette(
                    new Color(0.32f, 0.62f, 0.3f),
                    petalColor,
                    Color.Lerp(petalColor, Color.white, 0.25f),
                    Color.Lerp(petalColor, Color.black, 0.32f),
                    new Color(0.95f, 0.79f, 0.23f),
                    Color.Lerp(petalColor, Color.white, 0.54f)));
        }

        public static VoxelStructureBuildResult CreateFountain(
            string name,
            Vector3 position,
            Vector3 targetSize,
            float yaw,
            Color stoneColor,
            Color waterColor)
        {
            return CreateStructure(
                name,
                VoxelStructureType.Fountain,
                position,
                yaw,
                targetSize,
                CreatePalette(
                    stoneColor,
                    Color.Lerp(stoneColor, Color.white, 0.12f),
                    Color.Lerp(stoneColor, Color.black, 0.2f),
                    Color.Lerp(stoneColor, Color.black, 0.4f),
                    waterColor,
                    Color.Lerp(waterColor, Color.white, 0.48f)));
        }

        static VoxelStructureBuildResult CreateStructure(
            string name,
            VoxelStructureType structureType,
            Vector3 position,
            float yaw,
            Vector3 targetSize,
            StructurePalette palette)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var visual = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(root.transform, false);

            var mesh = GetOrCreateMesh(structureType);
            visual.transform.localPosition = -mesh.bounds.center;

            var meshFilter = visual.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = visual.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = GetOrCreateMaterials(palette);
            meshRenderer.shadowCastingMode = ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;

            var meshSize = mesh.bounds.size;
            root.transform.localScale = new Vector3(
                meshSize.x <= 0.001f ? 1f : targetSize.x / meshSize.x,
                meshSize.y <= 0.001f ? 1f : targetSize.y / meshSize.y,
                meshSize.z <= 0.001f ? 1f : targetSize.z / meshSize.z);

            return new VoxelStructureBuildResult(root, targetSize);
        }

        static Mesh GetOrCreateMesh(VoxelStructureType structureType)
        {
            if (MeshCache.TryGetValue(structureType, out var mesh))
            {
                return mesh;
            }

            mesh = VoxelMeshBuilder.Build(CreateModel(structureType), BaseVoxelSize, PaletteSize, "VoxelStructure_" + structureType);
            MeshCache[structureType] = mesh;
            return mesh;
        }

        static Material[] GetOrCreateMaterials(StructurePalette palette)
        {
            var key =
                ColorUtility.ToHtmlStringRGBA(palette.Primary) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Secondary) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Trim) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Dark) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Accent) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Light);

            if (MaterialCache.TryGetValue(key, out var materials))
            {
                return materials;
            }

            materials = new[]
            {
                CreateMaterial(palette.Primary),
                CreateMaterial(palette.Secondary),
                CreateMaterial(palette.Trim),
                CreateMaterial(palette.Dark),
                CreateMaterial(palette.Accent),
                CreateMaterial(palette.Light)
            };

            MaterialCache[key] = materials;
            return materials;
        }

        static VoxelModel32 CreateModel(VoxelStructureType structureType)
        {
            var model = new VoxelModel32();
            switch (structureType)
            {
                case VoxelStructureType.House:
                    BuildHouseModel(model);
                    break;
                case VoxelStructureType.Door:
                    BuildDoorModel(model);
                    break;
                case VoxelStructureType.Tree:
                    BuildTreeModel(model);
                    break;
                case VoxelStructureType.Shrub:
                    BuildShrubModel(model);
                    break;
                case VoxelStructureType.Flower:
                    BuildFlowerModel(model);
                    break;
                case VoxelStructureType.Fountain:
                    BuildFountainModel(model);
                    break;
            }

            return model;
        }

        static void BuildHouseModel(VoxelModel32 model)
        {
            model.FillBox(3, 0, 4, 29, 16, 28, PrimaryColor);
            model.FillBox(5, 2, 5, 27, 15, 27, LightColor);
            model.FillBox(2, 15, 3, 30, 17, 29, TrimColor);
            model.FillBox(4, 16, 5, 28, 18, 27, SecondaryColor);
            model.FillBox(6, 18, 7, 26, 20, 25, SecondaryColor);
            model.FillBox(8, 20, 9, 24, 22, 23, SecondaryColor);
            model.FillBox(11, 0, 27, 21, 11, 29, TrimColor);
            model.FillBox(12, 1, 27, 20, 10, 28, DarkColor);
            model.FillBox(7, 6, 27, 11, 10, 29, AccentColor);
            model.FillBox(21, 6, 27, 25, 10, 29, AccentColor);
            model.FillBox(7, 6, 28, 11, 10, 29, LightColor);
            model.FillBox(21, 6, 28, 25, 10, 29, LightColor);
            model.FillBox(23, 17, 10, 26, 24, 13, DarkColor);
        }

        static void BuildDoorModel(VoxelModel32 model)
        {
            model.FillBox(2, 0, 10, 30, 31, 14, PrimaryColor);
            model.FillBox(4, 2, 11, 28, 29, 13, SecondaryColor);
            model.FillBox(6, 4, 12, 14, 12, 13, TrimColor);
            model.FillBox(18, 4, 12, 26, 12, 13, TrimColor);
            model.FillBox(25, 15, 14, 28, 18, 16, AccentColor);
        }

        static void BuildTreeModel(VoxelModel32 model)
        {
            model.FillBox(13, 0, 13, 19, 15, 19, PrimaryColor);
            model.FillBox(8, 12, 8, 24, 24, 24, SecondaryColor);
            model.FillBox(6, 17, 10, 26, 28, 22, SecondaryColor);
            model.FillBox(10, 20, 6, 22, 30, 26, LightColor);
        }

        static void BuildShrubModel(VoxelModel32 model)
        {
            model.FillBox(6, 0, 8, 26, 12, 24, PrimaryColor);
            model.FillBox(4, 6, 6, 28, 16, 26, SecondaryColor);
            model.FillBox(9, 12, 10, 23, 20, 22, LightColor);
        }

        static void BuildFlowerModel(VoxelModel32 model)
        {
            model.FillBox(14, 0, 14, 18, 20, 18, PrimaryColor);
            model.FillBox(8, 18, 12, 24, 24, 20, SecondaryColor);
            model.FillBox(12, 18, 8, 20, 24, 24, TrimColor);
            model.FillBox(13, 20, 13, 19, 26, 19, AccentColor);
            model.FillBox(14, 24, 14, 18, 30, 18, LightColor);
        }

        static void BuildFountainModel(VoxelModel32 model)
        {
            model.FillBox(4, 0, 4, 28, 5, 28, PrimaryColor);
            model.FillBox(7, 2, 7, 25, 4, 25, DarkColor);
            model.FillBox(12, 4, 12, 20, 14, 20, SecondaryColor);
            model.FillBox(10, 13, 10, 22, 15, 22, TrimColor);
            model.FillBox(8, 4, 8, 24, 7, 24, AccentColor);
            model.FillBox(14, 14, 14, 18, 24, 18, LightColor);
        }

        static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader)
            {
                color = color,
                enableInstancing = true
            };

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0f);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0f);
            }

            return material;
        }

        static StructurePalette CreatePalette(
            Color primary,
            Color secondary,
            Color trim,
            Color dark,
            Color accent,
            Color light)
        {
            return new StructurePalette(primary, secondary, trim, dark, accent, light);
        }

        readonly struct StructurePalette
        {
            public StructurePalette(Color primary, Color secondary, Color trim, Color dark, Color accent, Color light)
            {
                Primary = primary;
                Secondary = secondary;
                Trim = trim;
                Dark = dark;
                Accent = accent;
                Light = light;
            }

            public Color Primary { get; }

            public Color Secondary { get; }

            public Color Trim { get; }

            public Color Dark { get; }

            public Color Accent { get; }

            public Color Light { get; }
        }
    }
}
