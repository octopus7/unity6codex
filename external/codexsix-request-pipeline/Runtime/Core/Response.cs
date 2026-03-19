using System;

namespace CodexSix.RequestPipeline.Core
{
    [Serializable]
    public sealed class Response
    {
        public int StatusCode = 200;
        public string Body = string.Empty;
        public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
    }
}

