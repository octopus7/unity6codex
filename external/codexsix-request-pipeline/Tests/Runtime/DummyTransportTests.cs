using System.Threading.Tasks;
using CodexSix.RequestPipeline.Core;
using CodexSix.RequestPipeline.Transport;
using NUnit.Framework;

namespace CodexSix.RequestPipeline.Tests.Runtime
{
    public sealed class DummyTransportTests
    {
        [Test]
        public async Task SendAsync_ReturnsSuccessResponse()
        {
            var transport = new DummyTransport();
            var response = await transport.SendAsync(new Request(), new RequestOptions());

            Assert.That(response.IsSuccess, Is.True);
        }
    }
}

