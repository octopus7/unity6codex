using System.Threading.Tasks;
using CodexSix.RequestPipeline.Core;

namespace CodexSix.RequestPipeline.Transport
{
    public interface IHttpTransport
    {
        Task<Response> SendAsync(Request request, RequestOptions options);
    }
}

