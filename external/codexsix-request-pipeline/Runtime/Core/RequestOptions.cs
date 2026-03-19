using System;

namespace CodexSix.RequestPipeline.Core
{
    [Serializable]
    public sealed class RequestOptions
    {
        public float TimeoutSeconds = 10f;
        public bool UseAuth = true;
    }
}

