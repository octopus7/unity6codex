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
        DoorOpen,
        Tree,
        Shrub,
        Flower,
        Fountain,
        GrassPatchA,
        GrassPatchB,
        GrassPatchC,
        GrassPatchD
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
        const string HouseRevealMaterialKeyPrefix = "house|";
        const string HouseRevealTransparentMaterialKeyPrefix = "house-transparent|";
        const string DefaultMaterialKeyPrefix = "default|";

        const int PrimaryColor = 0;
        const int SecondaryColor = 1;
        const int TrimColor = 2;
        const int DarkColor = 3;
        const int AccentColor = 4;
        const int LightColor = 5;
        const int FenceNorth = 1 << 0;
        const int FenceEast = 1 << 1;
        const int FenceSouth = 1 << 2;
        const int FenceWest = 1 << 3;

        static readonly Dictionary<VoxelStructureType, Mesh> MeshCache = new Dictionary<VoxelStructureType, Mesh>();
        static readonly Dictionary<int, Mesh> FenceMeshCache = new Dictionary<int, Mesh>();
        static readonly Dictionary<string, Material[]> MaterialCache = new Dictionary<string, Material[]>();

        public const string HouseRevealShaderName = "McpTest/VoxelVillage/HouseReveal";
        public const string HouseRevealTransparentShaderName = "McpTest/VoxelVillage/HouseRevealTransparent";
        public const float HouseDoorSillNormalizedHeight = 1f / 24f;
        public const float HouseDoorFrontFaceNormalizedDepth = 12f / 26f;
        public const float HouseRoofRevealNormalizedHeight = 12f / 24f;

        public static VoxelStructureBuildResult CreateHouse(
            string name,
            Vector3 position,
            Vector3 targetSize,
            float yaw,
            Color wallColor,
            Color roofColor,
            Color trimColor)
        {
            return CreateHouseStructure(
                name,
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
                false,
                CreatePalette(
                    woodColor,
                    Color.Lerp(woodColor, Color.white, 0.2f),
                    Color.Lerp(woodColor, Color.black, 0.25f),
                    Color.Lerp(woodColor, Color.black, 0.45f),
                    new Color(0.9f, 0.8f, 0.48f),
                    new Color(0.98f, 0.94f, 0.86f)));
        }

        public static VoxelStructureBuildResult CreateFence(
            string name,
            Vector3 position,
            Vector3 targetSize,
            bool connectNorth,
            bool connectEast,
            bool connectSouth,
            bool connectWest,
            Color woodColor)
        {
            var connectionMask = 0;
            if (connectNorth)
            {
                connectionMask |= FenceNorth;
            }

            if (connectEast)
            {
                connectionMask |= FenceEast;
            }

            if (connectSouth)
            {
                connectionMask |= FenceSouth;
            }

            if (connectWest)
            {
                connectionMask |= FenceWest;
            }

            return CreateStructure(
                name,
                GetOrCreateFenceMesh(connectionMask),
                position,
                0f,
                targetSize,
                false,
                CreatePalette(
                    woodColor,
                    Color.Lerp(woodColor, new Color(0.72f, 0.57f, 0.34f), 0.22f),
                    Color.Lerp(woodColor, Color.white, 0.18f),
                    Color.Lerp(woodColor, Color.black, 0.38f),
                    Color.Lerp(woodColor, new Color(0.22f, 0.19f, 0.16f), 0.35f),
                    Color.Lerp(woodColor, Color.white, 0.34f)));
        }

        public static Mesh GetDoorMesh(bool open)
        {
            return GetOrCreateMesh(open ? VoxelStructureType.DoorOpen : VoxelStructureType.Door);
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
                false,
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
                false,
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
                false,
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
                false,
                CreatePalette(
                    stoneColor,
                    Color.Lerp(stoneColor, Color.white, 0.12f),
                    Color.Lerp(stoneColor, Color.black, 0.2f),
                    Color.Lerp(stoneColor, Color.black, 0.4f),
                    waterColor,
                    Color.Lerp(waterColor, Color.white, 0.48f)));
        }

        public static VoxelStructureBuildResult CreateGrass(
            string name,
            Vector3 position,
            Vector3 targetSize,
            float yaw,
            VillageGrassVariant variant,
            Color baseColor,
            Color tipColor)
        {
            return CreateStructure(
                name,
                GetGrassStructureType(variant),
                position,
                yaw,
                targetSize,
                false,
                CreatePalette(
                    baseColor,
                    Color.Lerp(baseColor, tipColor, 0.35f),
                    Color.Lerp(baseColor, tipColor, 0.58f),
                    Color.Lerp(baseColor, Color.black, 0.34f),
                    Color.Lerp(tipColor, Color.white, 0.18f),
                    Color.Lerp(tipColor, Color.white, 0.34f)));
        }

        static VoxelStructureBuildResult CreateHouseStructure(
            string name,
            Vector3 position,
            float yaw,
            Vector3 targetSize,
            StructurePalette palette)
        {
            var mesh = GetOrCreateMesh(VoxelStructureType.House);
            var root = new GameObject(name);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var opaqueVisual = CreateVisual(
                root.transform,
                "OpaqueVisual",
                mesh,
                GetOrCreateMaterials(palette, true),
                ShadowCastingMode.On,
                true);
            opaqueVisual.sortingOrder = 0;

            var revealVisual = CreateVisual(
                root.transform,
                "RevealVisual",
                mesh,
                GetOrCreateHouseRevealTransparentMaterials(palette),
                ShadowCastingMode.Off,
                true);
            revealVisual.sortingOrder = 1;

            var meshSize = mesh.bounds.size;
            root.transform.localScale = new Vector3(
                meshSize.x <= 0.001f ? 1f : targetSize.x / meshSize.x,
                meshSize.y <= 0.001f ? 1f : targetSize.y / meshSize.y,
                meshSize.z <= 0.001f ? 1f : targetSize.z / meshSize.z);

            return new VoxelStructureBuildResult(root, targetSize);
        }

        static VoxelStructureBuildResult CreateStructure(
            string name,
            VoxelStructureType structureType,
            Vector3 position,
            float yaw,
            Vector3 targetSize,
            bool useHouseRevealShader,
            StructurePalette palette)
        {
            return CreateStructure(
                name,
                GetOrCreateMesh(structureType),
                position,
                yaw,
                targetSize,
                useHouseRevealShader,
                palette);
        }

        static VoxelStructureBuildResult CreateStructure(
            string name,
            Mesh mesh,
            Vector3 position,
            float yaw,
            Vector3 targetSize,
            bool useHouseRevealShader,
            StructurePalette palette)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            CreateVisual(
                root.transform,
                "Visual",
                mesh,
                GetOrCreateMaterials(palette, useHouseRevealShader),
                ShadowCastingMode.On,
                true);

            var meshSize = mesh.bounds.size;
            root.transform.localScale = new Vector3(
                meshSize.x <= 0.001f ? 1f : targetSize.x / meshSize.x,
                meshSize.y <= 0.001f ? 1f : targetSize.y / meshSize.y,
                meshSize.z <= 0.001f ? 1f : targetSize.z / meshSize.z);

            return new VoxelStructureBuildResult(root, targetSize);
        }

        static Mesh GetOrCreateMesh(VoxelStructureType structureType)
        {
            if (MeshCache.TryGetValue(structureType, out var mesh) && mesh != null)
            {
                return mesh;
            }

            mesh = VoxelMeshBuilder.Build(CreateModel(structureType), BaseVoxelSize, PaletteSize, "VoxelStructure_" + structureType);
            MeshCache[structureType] = mesh;
            return mesh;
        }

        static MeshRenderer CreateVisual(
            Transform parent,
            string name,
            Mesh mesh,
            Material[] materials,
            ShadowCastingMode shadowCastingMode,
            bool receiveShadows)
        {
            var visual = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = -mesh.bounds.center;

            var meshFilter = visual.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = visual.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = materials;
            meshRenderer.shadowCastingMode = shadowCastingMode;
            meshRenderer.receiveShadows = receiveShadows;
            return meshRenderer;
        }

        static Mesh GetOrCreateFenceMesh(int connectionMask)
        {
            connectionMask &= FenceNorth | FenceEast | FenceSouth | FenceWest;
            if (FenceMeshCache.TryGetValue(connectionMask, out var mesh) && mesh != null)
            {
                return mesh;
            }

            var model = new VoxelModel32();
            BuildFenceModel(model, connectionMask);
            mesh = VoxelMeshBuilder.Build(model, BaseVoxelSize, PaletteSize, "VoxelStructure_Fence_" + connectionMask);
            FenceMeshCache[connectionMask] = mesh;
            return mesh;
        }

        static Material[] GetOrCreateMaterials(StructurePalette palette, bool useHouseRevealShader)
        {
            var key =
                (useHouseRevealShader ? HouseRevealMaterialKeyPrefix : DefaultMaterialKeyPrefix) +
                ColorUtility.ToHtmlStringRGBA(palette.Primary) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Secondary) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Trim) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Dark) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Accent) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Light);

            if (MaterialCache.TryGetValue(key, out var materials) && AreMaterialsAlive(materials))
            {
                return materials;
            }

            materials = new[]
            {
                CreateMaterial(palette.Primary, useHouseRevealShader),
                CreateMaterial(palette.Secondary, useHouseRevealShader),
                CreateMaterial(palette.Trim, useHouseRevealShader),
                CreateMaterial(palette.Dark, useHouseRevealShader),
                CreateMaterial(palette.Accent, useHouseRevealShader),
                CreateMaterial(palette.Light, useHouseRevealShader)
            };

            MaterialCache[key] = materials;
            return materials;
        }

        static Material[] GetOrCreateHouseRevealTransparentMaterials(StructurePalette palette)
        {
            var key =
                HouseRevealTransparentMaterialKeyPrefix +
                ColorUtility.ToHtmlStringRGBA(palette.Primary) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Secondary) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Trim) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Dark) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Accent) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Light);

            if (MaterialCache.TryGetValue(key, out var materials) && AreMaterialsAlive(materials))
            {
                return materials;
            }

            materials = new[]
            {
                CreateMaterial(palette.Primary, HouseRevealTransparentShaderName),
                CreateMaterial(palette.Secondary, HouseRevealTransparentShaderName),
                CreateMaterial(palette.Trim, HouseRevealTransparentShaderName),
                CreateMaterial(palette.Dark, HouseRevealTransparentShaderName),
                CreateMaterial(palette.Accent, HouseRevealTransparentShaderName),
                CreateMaterial(palette.Light, HouseRevealTransparentShaderName)
            };

            MaterialCache[key] = materials;
            return materials;
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
                case VoxelStructureType.DoorOpen:
                    BuildDoorOpenModel(model);
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
                case VoxelStructureType.GrassPatchA:
                    BuildGrassPatchAModel(model);
                    break;
                case VoxelStructureType.GrassPatchB:
                    BuildGrassPatchBModel(model);
                    break;
                case VoxelStructureType.GrassPatchC:
                    BuildGrassPatchCModel(model);
                    break;
                case VoxelStructureType.GrassPatchD:
                    BuildGrassPatchDModel(model);
                    break;
            }

            return model;
        }

        static void BuildHouseModel(VoxelModel32 model)
        {
            // Foundation and floor slab.
            model.FillBox(3, 0, 4, 29, 1, 28, DarkColor);
            model.FillBox(5, 1, 6, 27, 2, 26, LightColor);

            // Hollow wall shell with a centered doorway on the front face.
            model.FillBox(3, 1, 4, 29, 15, 6, PrimaryColor);
            model.FillBox(3, 1, 6, 5, 15, 26, PrimaryColor);
            model.FillBox(27, 1, 6, 29, 15, 26, PrimaryColor);
            model.FillBox(3, 1, 26, 15, 15, 28, PrimaryColor);
            model.FillBox(17, 1, 26, 29, 15, 28, PrimaryColor);
            model.FillBox(15, 11, 26, 17, 15, 28, PrimaryColor);

            // Door trim around the separate door actor.
            model.FillBox(14, 1, 26, 15, 12, 28, TrimColor);
            model.FillBox(17, 1, 26, 18, 12, 28, TrimColor);
            model.FillBox(15, 11, 26, 17, 12, 28, TrimColor);
            model.FillBox(15, 1, 26, 17, 2, 28, AccentColor);

            // Window bands keep the shell readable from outside while the interior stays open.
            model.FillBox(7, 6, 4, 11, 10, 6, AccentColor);
            model.FillBox(7, 6, 5, 11, 10, 6, LightColor);
            model.FillBox(21, 6, 4, 25, 10, 6, AccentColor);
            model.FillBox(21, 6, 5, 25, 10, 6, LightColor);
            model.FillBox(3, 6, 10, 5, 10, 14, AccentColor);
            model.FillBox(4, 6, 10, 5, 10, 14, LightColor);
            model.FillBox(27, 6, 18, 29, 10, 22, AccentColor);
            model.FillBox(27, 6, 18, 28, 10, 22, LightColor);

            // Roof cap and chimney.
            model.FillBox(2, 15, 3, 30, 17, 29, TrimColor);
            model.FillBox(4, 16, 5, 28, 18, 27, SecondaryColor);
            model.FillBox(6, 18, 7, 26, 20, 25, SecondaryColor);
            model.FillBox(8, 20, 9, 24, 22, 23, SecondaryColor);
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

        static void BuildDoorOpenModel(VoxelModel32 model)
        {
            model.FillBox(2, 0, 10, 6, 31, 14, PrimaryColor);
            model.FillBox(26, 0, 10, 30, 31, 14, PrimaryColor);
            model.FillBox(6, 0, 10, 26, 5, 14, SecondaryColor);
            model.FillBox(6, 26, 10, 26, 31, 14, SecondaryColor);
            model.FillBox(6, 14, 10, 26, 18, 14, TrimColor);
            model.FillBox(8, 6, 11, 24, 12, 13, LightColor);
            model.FillBox(8, 19, 11, 24, 25, 13, LightColor);
            model.FillBox(24, 15, 14, 27, 18, 16, AccentColor);
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

        static void BuildGrassPatchAModel(VoxelModel32 model)
        {
            model.FillBox(10, 0, 11, 22, 3, 21, PrimaryColor);
            model.FillBox(11, 2, 13, 15, 16, 17, SecondaryColor);
            model.FillBox(14, 2, 12, 18, 22, 16, TrimColor);
            model.FillBox(17, 2, 15, 21, 18, 19, SecondaryColor);
            model.FillBox(13, 2, 17, 17, 14, 20, AccentColor);
            model.FillBox(14, 18, 13, 18, 28, 17, LightColor);
        }

        static void BuildGrassPatchBModel(VoxelModel32 model)
        {
            model.FillBox(8, 0, 10, 24, 3, 22, PrimaryColor);
            model.FillBox(8, 2, 12, 13, 14, 16, SecondaryColor);
            model.FillBox(12, 2, 10, 17, 12, 14, TrimColor);
            model.FillBox(15, 2, 18, 20, 16, 22, SecondaryColor);
            model.FillBox(19, 2, 14, 24, 15, 18, AccentColor);
            model.FillBox(11, 10, 13, 21, 19, 19, LightColor);
        }

        static void BuildGrassPatchCModel(VoxelModel32 model)
        {
            model.FillBox(11, 0, 11, 21, 3, 21, PrimaryColor);
            model.FillBox(13, 2, 14, 16, 26, 16, SecondaryColor);
            model.FillBox(16, 2, 15, 19, 24, 17, TrimColor);
            model.FillBox(14, 2, 11, 17, 28, 13, AccentColor);
            model.FillBox(12, 8, 17, 15, 18, 20, SecondaryColor);
            model.FillBox(15, 24, 13, 18, 32, 16, LightColor);
        }

        static void BuildGrassPatchDModel(VoxelModel32 model)
        {
            model.FillBox(7, 0, 11, 15, 3, 19, PrimaryColor);
            model.FillBox(17, 0, 13, 25, 3, 21, PrimaryColor);
            model.FillBox(8, 2, 13, 12, 18, 16, SecondaryColor);
            model.FillBox(11, 2, 10, 15, 14, 13, TrimColor);
            model.FillBox(18, 2, 15, 22, 21, 18, SecondaryColor);
            model.FillBox(15, 4, 13, 19, 12, 18, AccentColor);
            model.FillBox(17, 18, 14, 21, 27, 17, LightColor);
        }

        static void BuildFenceModel(VoxelModel32 model, int connectionMask)
        {
            model.FillBox(12, 0, 12, 20, 26, 20, PrimaryColor);
            model.FillBox(11, 26, 11, 21, 29, 21, TrimColor);
            model.FillBox(10, 29, 10, 22, 32, 22, LightColor);
            model.FillBox(11, 0, 11, 21, 2, 21, DarkColor);

            if ((connectionMask & FenceNorth) != 0)
            {
                AddFenceRails(model, 13, 19, 19, 32);
            }

            if ((connectionMask & FenceEast) != 0)
            {
                AddFenceRails(model, 19, 32, 13, 19);
            }

            if ((connectionMask & FenceSouth) != 0)
            {
                AddFenceRails(model, 13, 19, 0, 13);
            }

            if ((connectionMask & FenceWest) != 0)
            {
                AddFenceRails(model, 0, 13, 13, 19);
            }
        }

        static void AddFenceRails(VoxelModel32 model, int xMin, int xMaxExclusive, int zMin, int zMaxExclusive)
        {
            model.FillBox(xMin, 8, zMin, xMaxExclusive, 11, zMaxExclusive, SecondaryColor);
            model.FillBox(xMin, 16, zMin, xMaxExclusive, 19, zMaxExclusive, SecondaryColor);
            model.FillBox(xMin, 11, zMin, xMaxExclusive, 12, zMaxExclusive, TrimColor);
            model.FillBox(xMin, 19, zMin, xMaxExclusive, 20, zMaxExclusive, TrimColor);
        }

        static Material CreateMaterial(Color color, bool useHouseRevealShader)
        {
            return CreateMaterial(color, useHouseRevealShader ? HouseRevealShaderName : null);
        }

        static Material CreateMaterial(Color color, string? preferredShaderName)
        {
            var shader = preferredShaderName == null
                ? null
                : Shader.Find(preferredShaderName);
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

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

        static VoxelStructureType GetGrassStructureType(VillageGrassVariant variant)
        {
            switch (variant)
            {
                case VillageGrassVariant.PatchA:
                    return VoxelStructureType.GrassPatchA;
                case VillageGrassVariant.PatchB:
                    return VoxelStructureType.GrassPatchB;
                case VillageGrassVariant.PatchC:
                    return VoxelStructureType.GrassPatchC;
                default:
                    return VoxelStructureType.GrassPatchD;
            }
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
