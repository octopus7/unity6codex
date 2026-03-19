using System;
using System.Collections.Generic;

namespace CodexSix.RequestPipeline.Core
{
    [Serializable]
    public sealed class Request
    {
        public string Method = "GET";
        public string Path = "/";
        public string Body = string.Empty;
        public Dictionary<string, string> Headers = new();
    }
}

