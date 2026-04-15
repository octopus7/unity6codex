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
        ""ko"": ""F 대화하기"",
        ""en"": ""F Talk"",
        ""ja"": ""F 話す""
      }
    },
    {
      ""key"": ""speaker.player"",
      ""translations"": {
        ""ko"": ""나"",
        ""en"": ""You"",
        ""ja"": ""あなた""
      }
    }
  ]
}";

        const string NpcJson = @"{
  ""npcs"": [
    {
      ""npcId"": ""villager_mina"",
      ""displayName"": {
        ""ko"": ""미나"",
        ""en"": ""Mina"",
        ""ja"": ""ミナ""
      },
      ""paletteId"": ""npc_red"",
      ""dialogueSetIds"": [""greeting_common""]
    }
  ]
}";

        const string DialogueJson = @"{
  ""dialogueSets"": [
    {
      ""id"": ""greeting_common"",
      ""cooldownSeconds"": 6,
      ""lines"": [
        {
          ""speaker"": ""npc"",
          ""translations"": {
            ""ko"": ""오늘은 장터가 꽤 붐비네."",
            ""en"": ""The market is pretty busy today."",
            ""ja"": ""今日は市場がかなりにぎやかだね。""
          }
        },
        {
          ""speaker"": ""player"",
          ""translations"": {
            ""ko"": ""그쪽부터 둘러봐야겠네요."",
            ""en"": ""I should start looking around there."",
            ""ja"": ""まずはあちらから見て回ろうかな。""
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
            Assert.AreEqual("미나", database.GetNpcDisplayName("villager_mina", LanguageCode.Ko));
            Assert.AreEqual("You", database.GetSpeakerDisplayName("player", "villager_mina", LanguageCode.En));
            Assert.AreEqual(2, database.GetDialogueLineCount("villager_mina"));
            Assert.AreEqual(
                "今日は市場がかなりにぎやかだね。",
                database.GetDialogueLine("villager_mina", 0)!.translations.Get(LanguageCode.Ja));
        }

        [Test]
        public void LocalizedTextFallsBackWhenRequestedLanguageIsMissing()
        {
            var localizedText = new LocalizedText
            {
                ko = "기본 텍스트"
            };

            Assert.AreEqual("기본 텍스트", localizedText.Get(LanguageCode.En));
            Assert.AreEqual("기본 텍스트", localizedText.Get(LanguageCode.Ja));
        }
    }
}
