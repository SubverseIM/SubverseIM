using SIPSorcery.SIP;
using SubverseIM.Core;
using SubverseIM.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WebSocketSharp;

namespace SubverseIM.Services.Implementation
{
    public class RelayService : IRelayService, IInjectable, IDisposableService
    {
        private readonly Channel<SIPMessageBase>
            recvMessageQueue,
            sendMessageQueue;

        private WebSocket? webSocket;

        private Task? connectTask;

        private bool disposedValue;

        public RelayService()
        {
            recvMessageQueue = Channel.CreateUnbounded<SIPMessageBase>();
            sendMessageQueue = Channel.CreateUnbounded<SIPMessageBase>();
        }

        private Task ConnectWithRetryAsync(bool initial = true)
        {
            return connectTask ??= Task.Run(async Task? () =>
            {
                Debug.Assert(webSocket is not null);

                if (!initial)
                {
                    await Task.Delay(5000);
                }

                while (!webSocket.IsAlive)
                {
                    try
                    {
                        webSocket.Connect();
                    }
                    catch
                    { 
                        webSocket.Close();
                        continue;
                    }

                    if (webSocket.IsAlive)
                    {
                        connectTask = null;
                        break;
                    }
                    else
                    {
                        await Task.Delay(5000);
                    }
                }
            });
        }

        public async Task<SIPMessageBase> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            return await recvMessageQueue.Reader.ReadAsync(cancellationToken);
        }

        public async Task QueueMessageAsync(SIPMessageBase sipMessage, CancellationToken cancellationToken)
        {
            await sendMessageQueue.Writer.WriteAsync(sipMessage, cancellationToken);
        }

        public async Task<bool> SendMessageAsync(CancellationToken cancellationToken)
        {
            SIPMessageBase sipMessage = await sendMessageQueue.Reader.ReadAsync(cancellationToken);
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
            await QueueMessageAsync(sipMessage, cancellationToken);

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

            webSocket.OnClose += OnSocketClose;
            webSocket.OnMessage += OnSocketMessage;

            await ConnectWithRetryAsync(initial: true);
        }

        private void OnSocketClose(object? sender, CloseEventArgs e)
        {
            _ = ConnectWithRetryAsync(initial: false);
        }

        private async void OnSocketMessage(object? sender, MessageEventArgs e)
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

            await recvMessageQueue.Writer.WriteAsync(sipMessage);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    ((IDisposable?)webSocket)?.Dispose();
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
