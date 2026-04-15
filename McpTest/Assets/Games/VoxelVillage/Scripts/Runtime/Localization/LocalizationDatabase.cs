#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace McpTest.VoxelVillage
{
    public sealed class LocalizationDatabase
    {
        readonly Dictionary<string, UiTextEntry> _uiEntries;
        readonly Dictionary<string, NpcDefinition> _npcs;
        readonly Dictionary<string, DialogueSetDefinition> _dialogueSets;

        LocalizationDatabase(
            Dictionary<string, UiTextEntry> uiEntries,
            Dictionary<string, NpcDefinition> npcs,
            Dictionary<string, DialogueSetDefinition> dialogueSets)
        {
            _uiEntries = uiEntries;
            _npcs = npcs;
            _dialogueSets = dialogueSets;
        }

        public static LocalizationDatabase LoadFromResources()
        {
            return FromJson(
                LoadRequiredJson("VoxelVillage/Localization/UiTextDatabase"),
                LoadRequiredJson("VoxelVillage/Npcs/NpcCatalog"),
                LoadRequiredJson("VoxelVillage/Dialogue/DialogueDatabase"));
        }

        public static LocalizationDatabase FromJson(string uiJson, string npcJson, string dialogueJson)
        {
            var uiFile = JsonUtility.FromJson<UiTextDatabaseFile>(uiJson);
            var npcFile = JsonUtility.FromJson<NpcCatalogFile>(npcJson);
            var dialogueFile = JsonUtility.FromJson<DialogueDatabaseFile>(dialogueJson);

            if (uiFile == null || npcFile == null || dialogueFile == null)
            {
                throw new InvalidOperationException("Failed to parse one or more localization JSON files.");
            }

            var uiEntries = new Dictionary<string, UiTextEntry>(StringComparer.Ordinal);
            for (var index = 0; index < uiFile.entries.Length; index++)
            {
                var entry = uiFile.entries[index];
                if (string.IsNullOrWhiteSpace(entry.key))
                {
                    continue;
                }

                uiEntries[entry.key] = entry;
            }

            var npcs = new Dictionary<string, NpcDefinition>(StringComparer.Ordinal);
            for (var index = 0; index < npcFile.npcs.Length; index++)
            {
                var npc = npcFile.npcs[index];
                if (string.IsNullOrWhiteSpace(npc.npcId))
                {
                    continue;
                }

                npcs[npc.npcId] = npc;
            }

            var dialogueSets = new Dictionary<string, DialogueSetDefinition>(StringComparer.Ordinal);
            for (var index = 0; index < dialogueFile.dialogueSets.Length; index++)
            {
                var dialogueSet = dialogueFile.dialogueSets[index];
                if (string.IsNullOrWhiteSpace(dialogueSet.id))
                {
                    continue;
                }

                dialogueSets[dialogueSet.id] = dialogueSet;
            }

            return new LocalizationDatabase(uiEntries, npcs, dialogueSets);
        }

        public string GetUiText(string key, LanguageCode language)
        {
            if (_uiEntries.TryGetValue(key, out var entry))
            {
                return entry.translations.Get(language);
            }

            return "[" + key + "]";
        }

        public string GetNpcDisplayName(string npcId, LanguageCode language)
        {
            if (_npcs.TryGetValue(npcId, out var npc))
            {
                var text = npc.displayName.Get(language);
                return string.IsNullOrWhiteSpace(text) ? npcId : text;
            }

            return npcId;
        }

        public string GetNpcRoleName(string npcId, LanguageCode language)
        {
            if (_npcs.TryGetValue(npcId, out var npc))
            {
                return npc.roleName.Get(language);
            }

            return string.Empty;
        }

        public string GetNpcHeader(string npcId, LanguageCode language)
        {
            var name = GetNpcDisplayName(npcId, language);
            var role = GetNpcRoleName(npcId, language);
            return string.IsNullOrWhiteSpace(role) ? name : name + " · " + role;
        }

        public DialogueSetDefinition? GetFirstDialogueSet(string npcId)
        {
            if (!_npcs.TryGetValue(npcId, out var npc))
            {
                return null;
            }

            for (var index = 0; index < npc.dialogueSetIds.Length; index++)
            {
                var dialogueSetId = npc.dialogueSetIds[index];
                if (_dialogueSets.TryGetValue(dialogueSetId, out var dialogueSet))
                {
                    return dialogueSet;
                }
            }

            return null;
        }

        public int GetDialogueLineCount(string npcId)
        {
            return GetFirstDialogueSet(npcId)?.lines.Length ?? 0;
        }

        public DialogueLineDefinition? GetDialogueLine(string npcId, int lineIndex)
        {
            var dialogueSet = GetFirstDialogueSet(npcId);
            if (dialogueSet == null || lineIndex < 0 || lineIndex >= dialogueSet.lines.Length)
            {
                return null;
            }

            return dialogueSet.lines[lineIndex];
        }

        public string GetSpeakerDisplayName(string speaker, string npcId, LanguageCode language)
        {
            if (string.Equals(speaker, "npc", StringComparison.OrdinalIgnoreCase))
            {
                return GetNpcDisplayName(npcId, language);
            }

            if (string.Equals(speaker, "player", StringComparison.OrdinalIgnoreCase))
            {
                return GetUiText("speaker.player", language);
            }

            return speaker;
        }

        static string LoadRequiredJson(string resourcePath)
        {
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null)
            {
                throw new InvalidOperationException("Missing localization resource at Resources/" + resourcePath + ".json");
            }

            return textAsset.text;
        }
    }
}
