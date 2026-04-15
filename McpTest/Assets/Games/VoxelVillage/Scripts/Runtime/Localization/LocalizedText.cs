#nullable enable

using System;

namespace McpTest.VoxelVillage
{
    [Serializable]
    public sealed class LocalizedText
    {
        public string ko = string.Empty;
        public string en = string.Empty;
        public string ja = string.Empty;

        public string Get(LanguageCode language)
        {
            var primary = language switch
            {
                LanguageCode.Ko => ko,
                LanguageCode.En => en,
                LanguageCode.Ja => ja,
                _ => ko
            };

            if (!string.IsNullOrWhiteSpace(primary))
            {
                return primary;
            }

            if (!string.IsNullOrWhiteSpace(ko))
            {
                return ko;
            }

            if (!string.IsNullOrWhiteSpace(en))
            {
                return en;
            }

            if (!string.IsNullOrWhiteSpace(ja))
            {
                return ja;
            }

            return string.Empty;
        }
    }
}
