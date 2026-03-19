using CodexSix.RequestPipeline.Core;

namespace CodexSix.RequestPipeline.Auth
{
    public interface IAuthProvider
    {
        void Apply(Request request);
    }
}

