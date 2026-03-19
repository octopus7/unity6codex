using UnityEngine;

namespace CodexSix.RequestPipeline.Config
{
    [CreateAssetMenu(
        fileName = "RequestPipelineSettings",
        menuName = "CodexSix/Request Pipeline/Settings")]
    public sealed class RequestPipelineSettings : ScriptableObject
    {
        public string BaseUrl = "https://example.invalid";
        public float DefaultTimeoutSeconds = 10f;
    }
}

