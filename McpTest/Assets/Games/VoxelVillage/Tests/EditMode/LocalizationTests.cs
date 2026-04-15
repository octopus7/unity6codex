#nullable enable

using NUnit.Framework;

namespace McpTest.VoxelVillage.Tests
{
    public sealed class LocalizationTests
    {
        const string UiJson = @"{
  ""languageOrder"": [""ko"", ""en"", ""ja""],
  ""entries"": [
    {
      ""key"": ""interaction.talk"",
      ""translations"": {
        ""ko"": ""Talk ko"",
        ""en"": ""F Talk"",
        ""ja"": ""Talk ja""
      }
    },
    {
      ""key"": ""hud.controls.show"",
      ""translations"": {
        ""ko"": ""Controls ko"",
        ""en"": ""Controls"",
        ""ja"": ""Controls ja""
      }
    },
    {
      ""key"": ""speaker.player"",
      ""translations"": {
        ""ko"": ""Player ko"",
        ""en"": ""You"",
        ""ja"": ""Player ja""
      }
    }
  ]
}";

        const string NpcJson = @"{
  ""npcs"": [
    {
      ""npcId"": ""villager_mina"",
      ""displayName"": {
        ""ko"": ""Mina ko"",
        ""en"": ""Mina"",
        ""ja"": ""Mina ja""
      },
      ""roleName"": {
        ""ko"": ""Role ko"",
        ""en"": ""Merchant"",
        ""ja"": ""Role ja""
      },
      ""paletteId"": ""npc_red"",
      ""dialogueSetIds"": [""mina_chat""]
    },
    {
      ""npcId"": ""villager_haru"",
      ""displayName"": {
        ""ko"": ""Haru ko"",
        ""en"": ""Haru"",
        ""ja"": ""Haru ja""
      },
      ""roleName"": {
        ""ko"": ""Craft ko"",
        ""en"": ""Carpenter"",
        ""ja"": ""Craft ja""
      },
      ""paletteId"": ""npc_green"",
      ""dialogueSetIds"": [""haru_chat""]
    }
  ]
}";

        const string DialogueJson = @"{
  ""dialogueSets"": [
    {
      ""id"": ""mina_chat"",
      ""cooldownSeconds"": 6,
      ""lines"": [
        {
          ""speaker"": ""npc"",
          ""translations"": {
            ""ko"": ""Mina line ko"",
            ""en"": ""Mina line en"",
            ""ja"": ""Mina line ja""
          }
        },
        {
          ""speaker"": ""player"",
          ""translations"": {
            ""ko"": ""Reply ko"",
            ""en"": ""Reply en"",
            ""ja"": ""Reply ja""
          }
        }
      ]
    },
    {
      ""id"": ""haru_chat"",
      ""cooldownSeconds"": 6,
      ""lines"": [
        {
          ""speaker"": ""npc"",
          ""translations"": {
            ""ko"": ""Haru line ko"",
            ""en"": ""Haru line en"",
            ""ja"": ""Haru line ja""
          }
        }
      ]
    }
  ]
}";

        [Test]
        public void LanguageStateCyclesKoEnJa()
        {
            var state = new LanguageState(LanguageCode.Ko);

            Assert.AreEqual(LanguageCode.Ko, state.Current);

            state.CycleNext();
            Assert.AreEqual(LanguageCode.En, state.Current);

            state.CycleNext();
            Assert.AreEqual(LanguageCode.Ja, state.Current);

            state.CycleNext();
            Assert.AreEqual(LanguageCode.Ko, state.Current);
        }

        [Test]
        public void LocalizationDatabaseReturnsTranslatedUiAndDialogue()
        {
            var database = LocalizationDatabase.FromJson(UiJson, NpcJson, DialogueJson);

            Assert.AreEqual("F Talk", database.GetUiText("interaction.talk", LanguageCode.En));
            Assert.AreEqual("Controls", database.GetUiText("hud.controls.show", LanguageCode.En));
            Assert.AreEqual("Mina ko", database.GetNpcDisplayName("villager_mina", LanguageCode.Ko));
            Assert.AreEqual("Merchant", database.GetNpcRoleName("villager_mina", LanguageCode.En));
            Assert.AreEqual("Mina - Merchant", database.GetNpcHeader("villager_mina", LanguageCode.En));
            Assert.AreEqual("You", database.GetSpeakerDisplayName("player", "villager_mina", LanguageCode.En));
            Assert.AreEqual(2, database.GetDialogueLineCount("villager_mina"));
            Assert.AreEqual("Mina line ja", database.GetDialogueLine("villager_mina", 0)!.translations.Get(LanguageCode.Ja));
        }

        [Test]
        public void LocalizationDatabaseReturnsNpcSpecificDialogue()
        {
            var database = LocalizationDatabase.FromJson(UiJson, NpcJson, DialogueJson);

            Assert.AreEqual(1, database.GetDialogueLineCount("villager_haru"));
            Assert.AreEqual("Haru line en", database.GetDialogueLine("villager_haru", 0)!.translations.Get(LanguageCode.En));
            Assert.AreEqual("Haru", database.GetSpeakerDisplayName("npc", "villager_haru", LanguageCode.En));
        }

        [Test]
        public void LocalizedTextFallsBackWhenRequestedLanguageIsMissing()
        {
            var localizedText = new LocalizedText
            {
                ko = "fallback"
            };

            Assert.AreEqual("fallback", localizedText.Get(LanguageCode.En));
            Assert.AreEqual("fallback", localizedText.Get(LanguageCode.Ja));
        }
    }
}
