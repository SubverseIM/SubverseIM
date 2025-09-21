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
        private readonly ConcurrentBag<TaskCompletionSource<SIPMessageBase>> recvMessageBag;

        private readonly ConcurrentQueue<SIPMessageBase> sendMessageQueue;

        private WebSocket? webSocket;

        private bool disposedValue;

        public RelayService()
        {
            recvMessageBag = new();
            sendMessageQueue = new();
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

            if (sipMessageBuffer is not null && webSocket?.IsAlive == true)
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
