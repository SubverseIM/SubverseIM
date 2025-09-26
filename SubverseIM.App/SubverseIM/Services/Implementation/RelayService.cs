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
        private readonly ConcurrentQueue<SIPMessageBase> 
            recvMessageQueue,
            sendMessageQueue;

        private WebSocket? webSocket;

        private bool disposedValue;

        public RelayService()
        {
            recvMessageQueue = new();
            sendMessageQueue = new();
        }

        private async Task ConnectToSocketAsync()
        {
            webSocket?.Close();
            while (webSocket?.IsAlive == false)
            {
                webSocket.Connect();
                if (!webSocket.IsAlive)
                {
                    await Task.Delay(5000);
                }
            }
        }

        public Task<SIPMessageBase?> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            recvMessageQueue.TryDequeue(out SIPMessageBase? sipMessage);
            return Task.FromResult(sipMessage);
        }

        public Task QueueMessageAsync(SIPMessageBase sipMessage)
        {
            sendMessageQueue.Enqueue(sipMessage);
            return Task.CompletedTask;
        }

        public async Task<bool> SendMessageAsync(CancellationToken cancellationToken = default)
        {
            if (!sendMessageQueue.TryDequeue(out SIPMessageBase? sipMessage)) return false;

            byte[]? sipMessageBuffer = sipMessage switch
            {
                SIPRequest sipRequest => sipRequest.GetBytes(),
                SIPResponse sipResponse => sipResponse.GetBytes(),
                _ => null,
            };

            if (sipMessageBuffer is not null && webSocket is not null)
            {
                try
                {
                    webSocket.Send(sipMessageBuffer);
                    return true;
                }
                catch { }
            }

            cancellationToken.ThrowIfCancellationRequested();
            await QueueMessageAsync(sipMessage);

            return false;
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

            webSocket.OnError += OnSocketError;
            webSocket.OnMessage += OnSocketMessage;

            await ConnectToSocketAsync();
        }

        private void OnSocketError(object? sender, ErrorEventArgs e)
        {
            _ = ConnectToSocketAsync();
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

            recvMessageQueue.Enqueue(sipMessage);
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
