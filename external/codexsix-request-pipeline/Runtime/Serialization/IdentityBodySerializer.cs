namespace CodexSix.RequestPipeline.Serialization
{
    public sealed class IdentityBodySerializer : IBodySerializer
    {
        public string Serialize(string body)
        {
            return body ?? string.Empty;
        }
    }
}

