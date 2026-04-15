#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace McpTest.VoxelVillage
{
    public enum VoxelCharacterAccessoryType
    {
        None,
        MerchantApron,
        GardenerHat,
        CarpenterBelt,
        WatcherScarf,
        LanternCape,
        CourierPack
    }

    public readonly struct VoxelCharacterBuildResult
    {
        public VoxelCharacterBuildResult(GameObject root, float headOffset)
        {
            Root = root;
            HeadOffset = headOffset;
        }

        public GameObject Root { get; }

        public float HeadOffset { get; }
    }

    public static class VoxelCharacterFactory
    {
        const float VoxelSize = 1f / 16f;
        const int PaletteSize = 6;

        const int SkinColor = 0;
        const int PrimaryColor = 1;
        const int SecondaryColor = 2;
        const int DarkColor = 3;
        const int AccentColor = 4;
        const int LightColor = 5;

        static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material[]> MaterialCache = new Dictionary<string, Material[]>();

        public static VoxelCharacterBuildResult CreateCharacter(
            string name,
            Vector3 position,
            Color primaryColor,
            VoxelCharacterAccessoryType accessoryType,
            bool isPlayer,
            float scaleFactor = 1f)
        {
            var clampedScale = Mathf.Max(0.01f, scaleFactor);

            var root = new GameObject(name);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * clampedScale;

            var visual = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(root.transform, false);

            var mesh = GetOrCreateMesh(accessoryType, isPlayer);
            visual.transform.localPosition = -mesh.bounds.center;

            var meshFilter = visual.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = visual.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = GetOrCreateMaterials(primaryColor, isPlayer);
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;

            return new VoxelCharacterBuildResult(root, mesh.bounds.extents.y * clampedScale);
        }

        static Mesh GetOrCreateMesh(VoxelCharacterAccessoryType accessoryType, bool isPlayer)
        {
            var key = (isPlayer ? "player:" : "villager:") + accessoryType;
            if (MeshCache.TryGetValue(key, out var mesh) && mesh != null)
            {
                return mesh;
            }

            var model = CreateCharacterModel(accessoryType, isPlayer);
            mesh = VoxelMeshBuilder.Build(model, VoxelSize, PaletteSize, "VoxelCharacter_" + key);
            MeshCache[key] = mesh;
            return mesh;
        }

        static Material[] GetOrCreateMaterials(Color primaryColor, bool isPlayer)
        {
            var palette = CreatePalette(primaryColor, isPlayer);
            var key =
                (isPlayer ? "player:" : "villager:") +
                ColorUtility.ToHtmlStringRGBA(palette.Skin) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Primary) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Secondary) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Dark) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Accent) + "|" +
                ColorUtility.ToHtmlStringRGBA(palette.Light);

            if (MaterialCache.TryGetValue(key, out var materials) && AreMaterialsAlive(materials))
            {
                return materials;
            }

            materials = new[]
            {
                CreateMaterial(palette.Skin),
                CreateMaterial(palette.Primary),
                CreateMaterial(palette.Secondary),
                CreateMaterial(palette.Dark),
                CreateMaterial(palette.Accent),
                CreateMaterial(palette.Light)
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

        static CharacterPalette CreatePalette(Color primaryColor, bool isPlayer)
        {
            var primary = primaryColor;
            var secondary = Color.Lerp(primaryColor, Color.white, isPlayer ? 0.34f : 0.24f);
            var dark = Color.Lerp(primaryColor, Color.black, 0.52f);
            var accent = isPlayer
                ? new Color(0.91f, 0.96f, 1f)
                : Color.Lerp(primaryColor, new Color(0.95f, 0.86f, 0.48f), 0.5f);
            var light = Color.Lerp(primaryColor, Color.white, 0.84f);
            var skin = isPlayer
                ? new Color(0.93f, 0.8f, 0.67f)
                : new Color(0.91f, 0.77f, 0.64f);

            return new CharacterPalette(skin, primary, secondary, dark, accent, light);
        }

        static VoxelModel32 CreateCharacterModel(VoxelCharacterAccessoryType accessoryType, bool isPlayer)
        {
            var model = new VoxelModel32();

            BuildBaseBody(model, isPlayer);

            if (isPlayer)
            {
                AddPlayerDetails(model);
            }
            else
            {
                AddVillagerAccessory(model, accessoryType);
            }

            return model;
        }

        static void BuildBaseBody(VoxelModel32 model, bool isPlayer)
        {
            model.FillBox(9, 0, 11, 14, 3, 21, DarkColor);
            model.FillBox(18, 0, 11, 23, 3, 21, DarkColor);
            model.FillBox(9, 3, 11, 14, 12, 21, PrimaryColor);
            model.FillBox(18, 3, 11, 23, 12, 21, PrimaryColor);

            model.FillBox(8, 12, 10, 24, 22, 22, PrimaryColor);
            model.FillBox(7, 20, 10, 25, 23, 22, SecondaryColor);

            model.FillBox(4, 12, 11, 8, 22, 20, PrimaryColor);
            model.FillBox(24, 12, 11, 28, 22, 20, PrimaryColor);
            model.FillBox(4, 12, 12, 8, 15, 19, SkinColor);
            model.FillBox(24, 12, 12, 28, 15, 19, SkinColor);

            model.FillBox(14, 22, 13, 18, 24, 19, SkinColor);
            model.FillBox(9, 23, 9, 23, 31, 23, SkinColor);

            model.FillBox(8, 24, 8, 24, 29, 12, DarkColor);
            model.FillBox(8, 24, 9, 10, 29, 23, DarkColor);
            model.FillBox(22, 24, 9, 24, 29, 23, DarkColor);
            model.FillBox(8, 29, 8, 24, 32, 24, DarkColor);
            model.FillBox(9, 27, 18, 23, 30, 24, DarkColor);

            model.FillBox(12, 27, 22, 14, 28, 23, LightColor);
            model.FillBox(18, 27, 22, 20, 28, 23, LightColor);
            model.FillBox(12, 27, 21, 14, 28, 22, DarkColor);
            model.FillBox(18, 27, 21, 20, 28, 22, DarkColor);
            model.FillBox(15, 25, 22, 17, 26, 23, AccentColor);

            if (isPlayer)
            {
                model.FillBox(10, 14, 21, 22, 18, 22, SecondaryColor);
                model.FillBox(11, 18, 21, 21, 20, 22, AccentColor);
            }
        }

        static void AddPlayerDetails(VoxelModel32 model)
        {
            model.FillBox(10, 31, 10, 22, 32, 22, AccentColor);
            model.FillBox(12, 29, 8, 20, 31, 10, SecondaryColor);
            model.FillBox(8, 11, 10, 24, 13, 22, DarkColor);
        }

        static void AddVillagerAccessory(VoxelModel32 model, VoxelCharacterAccessoryType accessoryType)
        {
            switch (accessoryType)
            {
                case VoxelCharacterAccessoryType.MerchantApron:
                    model.FillBox(10, 13, 21, 22, 21, 23, SecondaryColor);
                    model.FillBox(9, 29, 12, 23, 31, 24, AccentColor);
                    model.FillBox(12, 31, 13, 20, 32, 21, AccentColor);
                    break;

                case VoxelCharacterAccessoryType.GardenerHat:
                    model.FillBox(7, 30, 7, 25, 31, 25, AccentColor);
                    model.FillBox(10, 31, 10, 22, 32, 22, AccentColor);
                    model.FillBox(24, 10, 13, 28, 16, 18, DarkColor);
                    model.FillBox(24, 15, 13, 28, 16, 18, SecondaryColor);
                    break;

                case VoxelCharacterAccessoryType.CarpenterBelt:
                    model.FillBox(8, 11, 10, 24, 13, 22, DarkColor);
                    model.FillBox(22, 10, 13, 26, 16, 18, DarkColor);
                    model.FillBox(4, 14, 14, 7, 18, 16, AccentColor);
                    break;

                case VoxelCharacterAccessoryType.WatcherScarf:
                    model.FillBox(8, 21, 10, 24, 23, 23, AccentColor);
                    model.FillBox(14, 16, 20, 18, 21, 23, AccentColor);
                    model.FillBox(24, 12, 13, 28, 18, 18, SecondaryColor);
                    break;

                case VoxelCharacterAccessoryType.LanternCape:
                    model.FillBox(9, 13, 8, 23, 22, 10, DarkColor);
                    model.FillBox(24, 9, 14, 28, 15, 18, AccentColor);
                    model.FillBox(25, 11, 15, 27, 13, 17, LightColor);
                    break;

                case VoxelCharacterAccessoryType.CourierPack:
                    model.FillBox(9, 14, 7, 23, 22, 10, DarkColor);
                    model.FillBox(9, 30, 12, 23, 31, 24, SecondaryColor);
                    model.FillBox(12, 31, 13, 20, 32, 21, SecondaryColor);
                    model.FillBox(4, 10, 13, 8, 17, 18, AccentColor);
                    break;
            }
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

        readonly struct CharacterPalette
        {
            public CharacterPalette(Color skin, Color primary, Color secondary, Color dark, Color accent, Color light)
            {
                Skin = skin;
                Primary = primary;
                Secondary = secondary;
                Dark = dark;
                Accent = accent;
                Light = light;
            }

            public Color Skin { get; }

            public Color Primary { get; }

            public Color Secondary { get; }

            public Color Dark { get; }

            public Color Accent { get; }

            public Color Light { get; }
        }
    }
}
