#nullable enable

using NUnit.Framework;

namespace McpTest.VoxelVillage.Tests
{
    public sealed class VoxelVillageResourceDataTests
    {
        [Test]
        public void LoadFromResources_ReturnsExpandedTwelveVillagerDataset()
        {
            var database = LocalizationDatabase.LoadFromResources();

            Assert.AreEqual("아린", database.GetNpcDisplayName("villager_arin", LanguageCode.Ko));
            Assert.AreEqual("Archivist", database.GetNpcRoleName("villager_sora", LanguageCode.En));
            Assert.AreEqual(2, database.GetDialogueLineCount("villager_toma"));
            Assert.AreEqual("ナリ", database.GetNpcDisplayName("villager_nari", LanguageCode.Ja));
            StringAssert.Contains("12 villagers", database.GetUiText("hud.instructions", LanguageCode.En));
        }
    }
}
