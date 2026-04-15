#nullable enable

using System;

namespace McpTest.VoxelVillage
{
    [Serializable]
    public sealed class UiTextDatabaseFile
    {
        public string[] languageOrder = Array.Empty<string>();
        public UiTextEntry[] entries = Array.Empty<UiTextEntry>();
    }

    [Serializable]
    public sealed class UiTextEntry
    {
        public string key = string.Empty;
        public LocalizedText translations = new LocalizedText();
    }

    [Serializable]
    public sealed class NpcCatalogFile
    {
        public NpcDefinition[] npcs = Array.Empty<NpcDefinition>();
    }

    [Serializable]
    public sealed class NpcDefinition
    {
        public string npcId = string.Empty;
        public LocalizedText displayName = new LocalizedText();
        public LocalizedText roleName = new LocalizedText();
        public string paletteId = string.Empty;
        public string[] dialogueSetIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class DialogueDatabaseFile
    {
        public DialogueSetDefinition[] dialogueSets = Array.Empty<DialogueSetDefinition>();
    }

    [Serializable]
    public sealed class DialogueSetDefinition
    {
        public string id = string.Empty;
        public float cooldownSeconds;
        public DialogueLineDefinition[] lines = Array.Empty<DialogueLineDefinition>();
    }

    [Serializable]
    public sealed class DialogueLineDefinition
    {
        public string speaker = string.Empty;
        public LocalizedText translations = new LocalizedText();
    }
}
