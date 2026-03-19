using System.Threading.Tasks;
using CodexSix.RequestPipeline.Auth;
using CodexSix.RequestPipeline.Serialization;
using CodexSix.RequestPipeline.Transport;

namespace CodexSix.RequestPipeline.Core
{
    public sealed class RequestClient : IRequestClient
    {
        private readonly IHttpTransport _transport;
        private readonly IAuthProvider _authProvider;
        private readonly IBodySerializer _bodySerializer;

        public RequestClient(
            IHttpTransport transport,
            IAuthProvider authProvider,
            IBodySerializer bodySerializer)
        {
            _transport = transport ?? new DummyTransport();
            _authProvider = authProvider ?? new NoAuthProvider();
            _bodySerializer = bodySerializer ?? new IdentityBodySerializer();
        }

        public Task<Response> SendAsync(Request request, RequestOptions options = null)
        {
            options ??= new RequestOptions();
            request ??= new Request();

            if (options.UseAuth)
            {
                _authProvider.Apply(request);
            }

            request.Body = _bodySerializer.Serialize(request.Body);
            return _transport.SendAsync(request, options);
        }
    }
}
