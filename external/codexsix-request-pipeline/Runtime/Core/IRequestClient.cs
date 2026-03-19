using System.Threading.Tasks;

namespace CodexSix.RequestPipeline.Core
{
    public interface IRequestClient
    {
        Task<Response> SendAsync(Request request, RequestOptions options = null);
    }
}

