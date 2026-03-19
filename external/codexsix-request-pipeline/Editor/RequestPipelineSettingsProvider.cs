using CodexSix.RequestPipeline.Config;
using UnityEditor;

namespace CodexSix.RequestPipeline.Editor
{
    public static class RequestPipelineSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Project/CodexSix/Request Pipeline", SettingsScope.Project)
            {
                label = "Request Pipeline",
                guiHandler = _ =>
                {
                    EditorGUILayout.HelpBox(
                        "Learning package skeleton. Create a RequestPipelineSettings asset if you want to extend it.",
                        MessageType.Info);
                    EditorGUILayout.LabelField("Runtime Settings Type", typeof(RequestPipelineSettings).Name);
                }
            };
        }
    }
}
