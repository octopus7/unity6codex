using System.Threading.Tasks;
using CodexSix.RequestPipeline.Core;

namespace CodexSix.RequestPipeline.Transport
{
    public sealed class UnityWebRequestTransport : IHttpTransport
    {
        public Task<Response> SendAsync(Request request, RequestOptions options)
        {
            return Task.FromResult(new Response
            {
                StatusCode = 501,
                Body = "UnityWebRequest transport is intentionally left as a stub in this learning package."
            });
        }
    }
}

