using SIPSorcery.SIP;
using SubverseIM.Core;
using SubverseIM.Models;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace SubverseIM.Services.Implementation
{
    public class RelayService : IRelayService, IInjectable, IDisposableService
    {
        private readonly ConcurrentBag<TaskCompletionSource<SIPMessageBase>> messageTcsBag;

        private WebSocket? webSocket;

        private bool disposedValue;

        public RelayService()
        {
            messageTcsBag = new();
        }

        public Task<SIPMessageBase> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            if (!messageTcsBag.TryTake(out TaskCompletionSource<SIPMessageBase>? tcs)) 
            {
                messageTcsBag.Add(tcs = new());
            }

            return tcs.Task.WaitAsync(cancellationToken);
        }

        public Task SendMessageAsync(SIPMessageBase sipMessage, CancellationToken cancellationToken = default)
        {
            byte[]? sipMessageBuffer = sipMessage switch
            {
                SIPRequest sipRequest => sipRequest.GetBytes(),
                SIPResponse sipResponse => sipResponse.GetBytes(),
                _ => null,
            };

            if (sipMessageBuffer is not null)
            {
                webSocket?.Send(sipMessageBuffer);
            }

            return Task.CompletedTask;
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

            webSocket.OnMessage += OnSocketMessage;
            webSocket.Connect();
        }

        private void OnSocketMessage(object? sender, MessageEventArgs e)
        {
            SIPMessageBuffer sipMessageBuffer = SIPMessageBuffer.ParseSIPMessage(e.RawData, SIPEndPoint.Empty, SIPEndPoint.Empty);

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

            if (!messageTcsBag.TryTake(out TaskCompletionSource<SIPMessageBase>? tcs)) 
            {
                messageTcsBag.Add(tcs = new());
            }
            tcs.SetResult(sipMessage);
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
