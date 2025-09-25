using CoreRPC;
using CoreRPC.Routing;
using CoreRPC.Transport.NamedPipe;
using SIPSorcery.SIP;
using SubverseIM.Core;
using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace SubverseIM.Bootstrapper.Services
{
    public class PeerService : IPeerService
    {
        private const int MSG_BUFFER_SIZE = 4096;

        private readonly Engine _engine;

        private readonly ConcurrentQueue<SIPMessageBase> _messageQueue;

        private readonly ConcurrentDictionary<SubversePeerId, IPeerService> _peerProxies;

        private readonly WebSocket _webSocket;

        private readonly SubversePeerId _peerId;

        public PeerService(WebSocket webSocket, SubversePeerId peerId)
        {
            _engine = new Engine();

            _messageQueue = new();
            _peerProxies = new();

            _webSocket = webSocket;
            _peerId = peerId;
        }

        private IPeerService GetPeerProxy(SubversePeerId peerId)
        {
            return _peerProxies.GetOrAdd(peerId, x =>
            {
                var transport = new NamedPipeClientTransport($"PEER-{x}");
                var proxy = _engine.CreateProxy<IPeerService>(transport);
                return proxy;
            });
        }

        private Task DispatchMessageAsync(byte[] messageBytes)
        {
            SIPMessageBuffer sipMessageBuffer = SIPMessageBuffer.ParseSIPMessage(messageBytes, SIPEndPoint.Empty, SIPEndPoint.Empty);

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
                    throw new PeerServiceException("Could not parse unknown message type.");
            }

            SubversePeerId toPeerId = SubversePeerId.FromString(sipMessage.Header.To.ToURI.User);
            IPeerService toPeer = GetPeerProxy(toPeerId);

            return toPeer.ReceiveMessageAsync(sipMessageBuffer.RawMessage);
        }

        public async Task ListenSocketAsync(CancellationToken cancellationToken)
        {
            using CancellationTokenSource cts = new();

            var router = new DefaultTargetSelector();
            router.Register<IPeerService, PeerService>(this);

            var handler = _engine.CreateRequestHandler(router);
            new NamedPipeHost(handler).StartListening($"PEER-{_peerId}", cts.Token);

            byte[] buffer = new byte[MSG_BUFFER_SIZE];
            using MemoryStream memoryStream = new MemoryStream();
            while (!cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int bytesRead = 0;
                memoryStream.Seek(0, SeekOrigin.Begin);

                WebSocketReceiveResult result;
                do
                {
                    if (_webSocket.State > WebSocketState.Open) return;

                    result = await _webSocket.ReceiveAsync(buffer, cancellationToken);
                    memoryStream.Write(buffer, 0, result.Count);
                    bytesRead += result.Count;
                } while (!result.EndOfMessage);

                memoryStream.SetLength(bytesRead);

                _ = DispatchMessageAsync(memoryStream.ToArray());
            }
        }

        public async Task SendSocketAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[MSG_BUFFER_SIZE];
            while (!cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_messageQueue.TryDequeue(out SIPMessageBase? sipMessage))
                {
                    await Task.Delay(1500);
                    continue;
                }

                byte[] sipMessageBuffer = sipMessage switch
                {
                    SIPRequest sipRequest => sipRequest.GetBytes(),
                    SIPResponse sipResponse => sipResponse.GetBytes(),
                    _ => throw new PeerServiceException($"Could not create message buffer from instance of type: {sipMessage.GetType()}")
                };

                using (MemoryStream memoryStream = new MemoryStream(sipMessageBuffer))
                {
                    int totalBytes = 0, bytesRead;
                    bool endOfMessage;
                    do
                    {
                        if (_webSocket.State > WebSocketState.Open) return;

                        totalBytes += bytesRead = memoryStream.Read(buffer, 0, buffer.Length);
                        await _webSocket.SendAsync(
                            new(buffer, 0, bytesRead), WebSocketMessageType.Binary,
                            endOfMessage = totalBytes == sipMessageBuffer.Length,
                            cancellationToken);
                    } while (!endOfMessage);
                }
            }
        }

        public Task ReceiveMessageAsync(string rawMessage)
        {
            SIPMessageBuffer sipMessageBuffer = SIPMessageBuffer.ParseSIPMessage(rawMessage, SIPEndPoint.Empty, SIPEndPoint.Empty);

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
                    throw new PeerServiceException("Could not parse unknown message type.");
            }

            _messageQueue.Enqueue(sipMessage);
            return Task.CompletedTask;
        }
    }
}
