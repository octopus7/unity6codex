using UnityEngine;

namespace CodexSix.TerrainMeshMovementLab
{
    public static class TerrainLabHeightCodec
    {
        public static Color32 EncodeHeight(float height, float minHeight, float maxHeight)
        {
            var encoded = EncodeToByte(height, minHeight, maxHeight);
            return PackByte(encoded);
        }

        public static float DecodeHeight(Color32 packed, float minHeight, float maxHeight)
        {
            var encoded = UnpackByte(packed);
            var normalized = encoded / 255f;
            return Mathf.Lerp(minHeight, maxHeight, normalized);
        }

        public static byte EncodeToByte(float height, float minHeight, float maxHeight)
        {
            if (Mathf.Approximately(minHeight, maxHeight))
            {
                return 0;
            }

            var normalized = Mathf.InverseLerp(minHeight, maxHeight, height);
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(normalized) * 255f);
        }

        public static Color32 PackByte(byte value)
        {
            return new Color32(value, value, value, 255);
        }

        public static byte UnpackByte(Color32 color)
        {
            return color.r;
        }
    }

    public static class TerrainLabHeightColorRamp
    {
        public static Color32 Evaluate(TerrainLabWorldConfig config, float height)
        {
            if (config == null || Mathf.Approximately(config.HeightMin, config.HeightMax))
            {
                return new Color32(124, 170, 124, 255);
            }

            var waterLevel = config.WaterLevel;
            if (height < waterLevel)
            {
                if (Mathf.Approximately(config.HeightMin, waterLevel))
                {
                    return new Color32(70, 150, 215, 255);
                }

                var waterT = Mathf.InverseLerp(config.HeightMin, waterLevel, height);
                return Color32.Lerp(new Color32(18, 52, 120, 255), new Color32(70, 150, 215, 255), waterT);
            }

            var landT = Mathf.InverseLerp(waterLevel, config.HeightMax, height);
            if (landT < 0.22f)
            {
                return Color32.Lerp(new Color32(194, 184, 125, 255), new Color32(90, 136, 70, 255), landT / 0.22f);
            }

            if (landT < 0.7f)
            {
                return Color32.Lerp(new Color32(90, 136, 70, 255), new Color32(155, 143, 94, 255), (landT - 0.22f) / 0.48f);
            }

            return Color32.Lerp(new Color32(155, 143, 94, 255), new Color32(238, 236, 220, 255), (landT - 0.7f) / 0.3f);
        }
    }
}
