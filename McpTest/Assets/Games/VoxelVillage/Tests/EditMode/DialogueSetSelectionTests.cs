#nullable enable

using NUnit.Framework;

namespace McpTest.VoxelVillage.Tests
{
    public sealed class DialogueSetSelectionTests
    {
        [Test]
        public void DialogueSetOverloadsMatchExistingSingleSetResources()
        {
            var database = LocalizationDatabase.LoadFromResources();

            Assert.AreEqual(1, database.GetDialogueSetCount("villager_mina"));
            Assert.AreEqual(
                database.GetDialogueLineCount("villager_mina"),
                database.GetDialogueLineCount("villager_mina", 0));
            Assert.IsNotNull(database.GetDialogueSet("villager_mina", 0));
            Assert.IsNull(database.GetDialogueSet("villager_mina", 1));
            Assert.IsNotNull(database.GetDialogueLine("villager_mina", 0, 0));
        }
    }
}
