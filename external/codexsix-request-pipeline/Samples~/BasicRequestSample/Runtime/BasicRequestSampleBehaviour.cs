using CodexSix.RequestPipeline.Auth;
using CodexSix.RequestPipeline.Core;
using CodexSix.RequestPipeline.Serialization;
using CodexSix.RequestPipeline.Transport;
using UnityEngine;

namespace CodexSix.RequestPipeline.Samples.BasicRequestSample
{
    public sealed class BasicRequestSampleBehaviour : MonoBehaviour
    {
        private async void Start()
        {
            var client = new RequestClient(
                new DummyTransport(),
                new NoAuthProvider(),
                new IdentityBodySerializer());

            var response = await client.SendAsync(new Request
            {
                Method = "GET",
                Path = "/sample"
            });

            Debug.Log(response.Body);
        }
    }
}

