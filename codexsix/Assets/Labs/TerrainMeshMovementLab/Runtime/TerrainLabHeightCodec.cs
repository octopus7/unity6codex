using UnityEngine;

namespace CodexSix.TerrainMeshMovementLab
{
    public static class TerrainLabHeightCodec
    {
        public static Color32 EncodeHeight(float height, float minHeight, float maxHeight)
        {
            var encoded = EncodeToUInt16(height, minHeight, maxHeight);
            return PackUInt16(encoded);
        }

        public static float DecodeHeight(Color32 packed, float minHeight, float maxHeight)
        {
            var encoded = UnpackUInt16(packed);
            var normalized = encoded / 65535f;
            return Mathf.Lerp(minHeight, maxHeight, normalized);
        }

        public static ushort EncodeToUInt16(float height, float minHeight, float maxHeight)
        {
            if (Mathf.Approximately(minHeight, maxHeight))
            {
                return 0;
            }

            var normalized = Mathf.InverseLerp(minHeight, maxHeight, height);
            return (ushort)Mathf.RoundToInt(Mathf.Clamp01(normalized) * 65535f);
        }

        public static Color32 PackUInt16(ushort value)
        {
            return new Color32((byte)(value >> 8), (byte)(value & 0xFF), 0, 255);
        }

        public static ushort UnpackUInt16(Color32 color)
        {
            return (ushort)((color.r << 8) | color.g);
        }
    }
}
