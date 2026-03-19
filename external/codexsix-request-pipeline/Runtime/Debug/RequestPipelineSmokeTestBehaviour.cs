using CodexSix.RequestPipeline.Auth;
using CodexSix.RequestPipeline.Core;
using CodexSix.RequestPipeline.Serialization;
using CodexSix.RequestPipeline.Transport;
using UnityEngine;

namespace CodexSix.RequestPipeline.Debug
{
    public sealed class RequestPipelineSmokeTestBehaviour : MonoBehaviour
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
                Path = "/smoke-test"
            });

            UnityEngine.Debug.Log($"[RequestPipelineSmokeTest] {response.StatusCode} {response.Body}");
        }
    }
}

