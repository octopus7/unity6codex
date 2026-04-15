#nullable enable

namespace McpTest.VoxelVillage
{
    public enum LanguageCode
    {
        Ko,
        En,
        Ja
    }

    public static class LanguageCodeExtensions
    {
        public static string ToCode(this LanguageCode language)
        {
            return language switch
            {
                LanguageCode.Ko => "ko",
                LanguageCode.En => "en",
                LanguageCode.Ja => "ja",
                _ => "ko"
            };
        }

        public static LanguageCode Next(this LanguageCode language)
        {
            return language switch
            {
                LanguageCode.Ko => LanguageCode.En,
                LanguageCode.En => LanguageCode.Ja,
                LanguageCode.Ja => LanguageCode.Ko,
                _ => LanguageCode.Ko
            };
        }
    }
}
