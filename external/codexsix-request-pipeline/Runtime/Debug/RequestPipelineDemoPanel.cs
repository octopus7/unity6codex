using System.Threading.Tasks;
using CodexSix.RequestPipeline.Auth;
using CodexSix.RequestPipeline.Core;
using CodexSix.RequestPipeline.Serialization;
using CodexSix.RequestPipeline.Transport;
using UnityEngine;
using UnityEngine.UI;

namespace CodexSix.RequestPipeline.Debug
{
    public sealed class RequestPipelineDemoPanel : MonoBehaviour
    {
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _statusLabel;
        [SerializeField] private Button _sendButton;

        private IRequestClient _client;
        private bool _requestInFlight;

        private void Awake()
        {
            _client = new RequestClient(
                new DummyTransport(),
                new NoAuthProvider(),
                new IdentityBodySerializer());

            if (_titleLabel != null)
            {
                _titleLabel.text = "Request Pipeline Package";
            }

            if (_statusLabel != null)
            {
                _statusLabel.text = "Scene loaded. Waiting for dummy request.";
            }

            if (_sendButton != null)
            {
                _sendButton.onClick.AddListener(HandleSendButtonClicked);
            }
        }

        private async void Start()
        {
            await SendDummyRequestAsync("/scene-load");
        }

        private void OnDestroy()
        {
            if (_sendButton != null)
            {
                _sendButton.onClick.RemoveListener(HandleSendButtonClicked);
            }
        }

        private void HandleSendButtonClicked()
        {
            _ = SendDummyRequestAsync("/button-click");
        }

        private async Task SendDummyRequestAsync(string path)
        {
            if (_requestInFlight)
            {
                return;
            }

            _requestInFlight = true;
            SetButtonInteractable(false);
            SetStatus($"Sending dummy request to {path}...");

            var response = await _client.SendAsync(new Request
            {
                Method = "GET",
                Path = path
            });

            var message = $"{response.StatusCode} {response.Body}";
            SetStatus(message);
            UnityEngine.Debug.Log($"[RequestPipelineDemoPanel] {message}");

            SetButtonInteractable(true);
            _requestInFlight = false;
        }

        private void SetStatus(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = message;
            }
        }

        private void SetButtonInteractable(bool interactable)
        {
            if (_sendButton != null)
            {
                _sendButton.interactable = interactable;
            }
        }
    }
}
