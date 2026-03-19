using System.Threading.Tasks;
using CodexSix.RequestPipeline.Core;

namespace CodexSix.RequestPipeline.Transport
{
    public sealed class DummyTransport : IHttpTransport
    {
        public Task<Response> SendAsync(Request request, RequestOptions options)
        {
            return Task.FromResult(new Response
            {
                StatusCode = 200,
                Body = $"Dummy response for {request.Method} {request.Path}"
            });
        }
    }
}

