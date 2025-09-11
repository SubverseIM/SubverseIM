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
        private readonly Engine engine;

        private readonly ConcurrentQueue<SIPMessageBase> messageQueue;
        
        private readonly ConcurrentDictionary<SubversePeerId, IPeerService> peerProxies;

        private readonly WebSocket webSocket;

        public PeerService(WebSocket webSocket, SubversePeerId peerId)
        {
            engine = new Engine();

            messageQueue = new();
            peerProxies = new();

            this.webSocket = webSocket;

            var router = new DefaultTargetSelector();
            router.Register<IPeerService, PeerService>(this);

            var handler = engine.CreateRequestHandler(router);
            new NamedPipeHost(handler).StartListening(peerId.ToString());
        }

        private IPeerService GetPeerProxy(SubversePeerId peerId) 
        {
            if (peerProxies.TryGetValue(peerId, out IPeerService? peer))
            {
                return peer;
            }
            else 
            {
                var transport = new NamedPipeClientTransport(peerId.ToString());
                var proxy = engine.CreateProxy<IPeerService>(transport);
                return peerProxies.GetOrAdd(peerId, proxy);
            }
        }

        private async Task DispatchMessageAsync(byte[] messageBytes)
        {
            SIPMessageBuffer messageBuffer = SIPMessageBuffer.ParseSIPMessage(messageBytes, SIPEndPoint.Empty, SIPEndPoint.Empty);

            SIPMessageBase sipMessage;
            switch (messageBuffer.SIPMessageType)
            {
                case SIPMessageTypesEnum.Request:
                    sipMessage = SIPRequest.ParseSIPRequest(messageBuffer);
                    break;
                case SIPMessageTypesEnum.Response:
                    sipMessage = SIPResponse.ParseSIPResponse(messageBuffer);
                    break;
                default:
                    throw new PeerServiceException("Could not parse unknown message type.");
            }

            SubversePeerId toPeerId = SubversePeerId.FromString(sipMessage.Header.To.ToURI.User);
            IPeerService toPeer = GetPeerProxy(toPeerId);

            await toPeer.ReceiveMessageAsync(messageBytes);
        }

        public async Task ListenSocketAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[4096];
            using MemoryStream memoryStream = new MemoryStream();
            while (!cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int bytesRead = 0;
                memoryStream.Seek(0, SeekOrigin.Begin);

                WebSocketReceiveResult result;
                do
                {
                    result = await webSocket.ReceiveAsync(buffer, cancellationToken);
                    memoryStream.Write(buffer, 0, result.Count);
                    bytesRead += result.Count;
                } while (!result.EndOfMessage);

                memoryStream.SetLength(bytesRead);

                await DispatchMessageAsync(memoryStream.ToArray());
            }
        }

        public Task SendSocketAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task ReceiveMessageAsync(byte[] messageBytes)
        {
            SIPMessageBuffer messageBuffer = SIPMessageBuffer.ParseSIPMessage(messageBytes, SIPEndPoint.Empty, SIPEndPoint.Empty);

            SIPMessageBase sipMessage;
            switch (messageBuffer.SIPMessageType)
            {
                case SIPMessageTypesEnum.Request:
                    sipMessage = SIPRequest.ParseSIPRequest(messageBuffer);
                    break;
                case SIPMessageTypesEnum.Response:
                    sipMessage = SIPResponse.ParseSIPResponse(messageBuffer);
                    break;
                default:
                    throw new PeerServiceException("Could not parse unknown message type.");
            }

            messageQueue.Enqueue(sipMessage);
            return Task.CompletedTask;
        }

        public Task RegisterPeerAsync(SubversePeerId peerId, IPeerService peer)
        {
            peerProxies.AddOrUpdate(peerId, id => peer, (id, x) => peer);
            return Task.CompletedTask;
        }
    }
}
