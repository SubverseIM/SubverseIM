using SIPSorcery.SIP;
using SubverseIM.Core;
using SubverseIM.Models;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace SubverseIM.Services.Implementation
{
    public class RelayService : IRelayService, IInjectable, IDisposableService
    {
        private readonly ConcurrentBag<TaskCompletionSource<SIPMessageBase>>
            recvMessageBag,
            sendMessageBag;

        private WebSocket? webSocket;

        private bool disposedValue;

        public RelayService()
        {
            recvMessageBag = new();
            sendMessageBag = new();
        }

        public Task<SIPMessageBase> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            if (!recvMessageBag.TryPeek(out TaskCompletionSource<SIPMessageBase>? tcs))
            {
                recvMessageBag.Add(tcs = new());
            }

            return tcs.Task.WaitAsync(cancellationToken);
        }

        public Task QueueMessageAsync(SIPMessageBase sipMessage)
        {
            if (!sendMessageBag.TryTake(out TaskCompletionSource<SIPMessageBase>? tcs) || !tcs.TrySetResult(sipMessage))
            {
                sendMessageBag.Add(tcs = new());
                tcs.SetResult(sipMessage);
            }

            return Task.CompletedTask;
        }

        public async Task SendMessageAsync(CancellationToken cancellationToken = default)
        {
            if (!sendMessageBag.TryPeek(out TaskCompletionSource<SIPMessageBase>? tcs))
            {
                sendMessageBag.Add(tcs = new());
            }

            SIPMessageBase sipMessage = await tcs.Task.WaitAsync(cancellationToken);

            byte[]? sipMessageBuffer = sipMessage switch
            {
                SIPRequest sipRequest => sipRequest.GetBytes(),
                SIPResponse sipResponse => sipResponse.GetBytes(),
                _ => null,
            };

            if (sipMessageBuffer is not null && webSocket?.IsAlive == true)
            {
                try
                {
                    webSocket.Send(sipMessageBuffer);
                    return;
                }
                catch { }
            }

            cancellationToken.ThrowIfCancellationRequested();
            await QueueMessageAsync(sipMessage);
        }

        public async Task InjectAsync(IServiceManager serviceManager)
        {
            IBootstrapperService bootstrapperService = await serviceManager.GetWithAwaitAsync<IBootstrapperService>();
            SubversePeerId peerId = await bootstrapperService.GetPeerIdAsync();

            IConfigurationService configurationService = await serviceManager.GetWithAwaitAsync<IConfigurationService>();
            SubverseConfig config = await configurationService.GetConfigAsync();
            Uri bootstrapperUri = new(
                config.BootstrapperUriList?.FirstOrDefault() ??
                IBootstrapperService.DEFAULT_BOOTSTRAPPER_ROOT
                );

            Uri relayUri = new UriBuilder(bootstrapperUri)
            {
                Scheme = Uri.UriSchemeWss,
                Path = "relay",
                Query = $"?p={peerId}",
            }.Uri;
            webSocket = new WebSocket(relayUri.AbsoluteUri);
            webSocket.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12;

            webSocket.OnMessage += OnSocketMessage;
            webSocket.Connect();
        }

        private void OnSocketMessage(object? sender, MessageEventArgs e)
        {
            byte[] rawMessageBuffer = new byte[e.RawData.Length];
            Array.Copy(e.RawData, rawMessageBuffer, rawMessageBuffer.Length);

            SIPMessageBuffer sipMessageBuffer = SIPMessageBuffer.ParseSIPMessage(rawMessageBuffer, SIPEndPoint.Empty, SIPEndPoint.Empty);

            SIPMessageBase sipMessage;
            switch (sipMessageBuffer.SIPMessageType)
            {
                case SIPMessageTypesEnum.Request:
                    sipMessage = SIPRequest.ParseSIPRequest(sipMessageBuffer);
                    break;
                case SIPMessageTypesEnum.Response:
                    sipMessage = SIPResponse.ParseSIPResponse(sipMessageBuffer);
                    break;
                default:
                    return;
            }

            if (!recvMessageBag.TryTake(out TaskCompletionSource<SIPMessageBase>? tcs) || !tcs.TrySetResult(sipMessage))
            {
                recvMessageBag.Add(tcs = new());
                tcs.SetResult(sipMessage);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    webSocket?.Close();
                }

                webSocket = null;
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
