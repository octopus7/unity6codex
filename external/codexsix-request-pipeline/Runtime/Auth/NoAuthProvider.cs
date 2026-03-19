using CodexSix.RequestPipeline.Core;

namespace CodexSix.RequestPipeline.Auth
{
    public sealed class NoAuthProvider : IAuthProvider
    {
        public void Apply(Request request)
        {
        }
    }
}

