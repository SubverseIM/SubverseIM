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

        private readonly Engine engine;

        private readonly ConcurrentBag<TaskCompletionSource<SIPMessageBase>> messageBag;
        
        private readonly ConcurrentDictionary<SubversePeerId, IPeerService> peerProxies;

        private readonly WebSocket webSocket;

        public PeerService(WebSocket webSocket, SubversePeerId peerId)
        {
            engine = new Engine();

            messageBag = new();
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

        private Task DispatchMessageAsync(byte[] messageBytes)
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

            return toPeer.ReceiveMessageAsync(messageBytes);
        }

        public async Task ListenSocketAsync(CancellationToken cancellationToken)
        {
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
                    result = await webSocket.ReceiveAsync(buffer, cancellationToken);
                    memoryStream.Write(buffer, 0, result.Count);
                    bytesRead += result.Count;
                } while (!result.EndOfMessage);

                memoryStream.SetLength(bytesRead);

                _ = Task.Run(Task? () => DispatchMessageAsync(memoryStream.ToArray()));
            }
        }

        public async Task SendSocketAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[MSG_BUFFER_SIZE];
            while (!cancellationToken.IsCancellationRequested) 
            {
                if (!messageBag.TryTake(out TaskCompletionSource<SIPMessageBase>? tcs)) 
                {
                    messageBag.Add(tcs = new());
                }

                SIPMessageBase sipMessage = await tcs.Task;
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
                        totalBytes += bytesRead = memoryStream.Read(buffer, 0, buffer.Length);
                        await webSocket.SendAsync(
                            new(buffer, 0, bytesRead), WebSocketMessageType.Binary,
                            endOfMessage = totalBytes == sipMessageBuffer.Length, 
                            cancellationToken);
                    } while (!endOfMessage);
                }
            }
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


            if (!messageBag.TryTake(out TaskCompletionSource<SIPMessageBase>? tcs))
            {
                messageBag.Add(tcs = new());
            }
            tcs.SetResult(sipMessage);

            return Task.CompletedTask;
        }

        public Task RegisterPeerAsync(SubversePeerId peerId, IPeerService peer)
        {
            peerProxies.AddOrUpdate(peerId, id => peer, (id, x) => peer);
            return Task.CompletedTask;
        }
    }
}
