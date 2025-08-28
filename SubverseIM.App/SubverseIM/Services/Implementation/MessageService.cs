using MonoTorrent;
using PgpCore;
using SIPSorcery.SIP;
using SubverseIM.Core;
using SubverseIM.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation;

public class MessageService : IMessageService, IDisposableService
{
    private readonly Dictionary<(string callId, SubversePeerId toPeer), SIPRequest> requestMap;

    private readonly ConcurrentBag<TaskCompletionSource<SubverseMessage>> messagesBag;

    private readonly SIPUDPChannel sipChannel;

    private readonly SIPTransport sipTransport;

    private readonly IServiceManager serviceManager;

    public IDictionary<SubversePeerId, SubversePeer> CachedPeers { get; }

    public MessageService(IServiceManager serviceManager)
    {
        requestMap = new();
        messagesBag = new();

        sipChannel = new SIPUDPChannel(IPAddress.Any, IBootstrapperService.DEFAULT_PORT_NUMBER);
        sipTransport = new SIPTransport(stateless: true);

        this.serviceManager = serviceManager;

        sipTransport.SIPTransportRequestReceived += SIPTransportRequestReceived;
        sipTransport.SIPTransportResponseReceived += SIPTransportResponseReceived;
        sipTransport.AddSIPChannel(sipChannel);

        CachedPeers = new Dictionary<SubversePeerId, SubversePeer>();
    }

    private async Task SIPTransportRequestReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
    {
        IBootstrapperService bootstrapperService = await serviceManager.GetWithAwaitAsync<IBootstrapperService>();
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

        SubversePeerId fromPeer = SubversePeerId.FromString(sipRequest.Header.From.FromURI.User);
        string fromName = sipRequest.Header.From.FromName;

        SubversePeerId toPeer = SubversePeerId.FromString(sipRequest.Header.To.ToURI.User);
        string toName = sipRequest.Header.To.ToName;

        string? messageContent;
        bool wasDecrypted = false;
        try
        {
            using (PGP pgp = new PGP(await bootstrapperService.GetPeerKeysAsync(fromPeer)))
            using (MemoryStream encryptedMessageStream = new(sipRequest.BodyBuffer))
            using (MemoryStream decryptedMessageStream = new())
            {
                await pgp.DecryptAndVerifyAsync(encryptedMessageStream, decryptedMessageStream);
                messageContent = Encoding.UTF8.GetString(decryptedMessageStream.ToArray());
                wasDecrypted = true;
            }
        }
        catch
        {
            messageContent = sipRequest.Body;
        }

        IEnumerable<SubversePeerId> recipients = [toPeer, ..sipRequest.Header.Contact
                        .Select(x => SubversePeerId.FromString(x.ContactURI.User))];

        IEnumerable<string?> localRecipientNames = recipients
            .Select(x => dbService.GetContact(x)?.DisplayName);

        IEnumerable<string> remoteRecipientNames =
            [toName, .. sipRequest.Header.Contact.Select(x => x.ContactName)];

        SubverseMessage message = new SubverseMessage
        {
            CallId = sipRequest.Header.CallId,
            Content = messageContent,
            Sender = fromPeer,
            SenderName = fromName,
            Recipients = recipients.ToArray(),
            RecipientNames = localRecipientNames
                .Zip(remoteRecipientNames)
                .Select(x => x.First ?? x.Second)
                .ToArray(),
            DateSignedOn = DateTime.Parse(sipRequest.Header.Date),
            TopicName = sipRequest.URI.Parameters.Get("topic"),
        };

        SubversePeer? peer;
        lock (CachedPeers)
        {
            if (!CachedPeers.TryGetValue(fromPeer, out peer))
            {
                CachedPeers.Add(fromPeer, peer = new() { OtherPeer = fromPeer });
            }
        }

        peer.RemoteEndPoints.Add(remoteEndPoint.GetIPEndPoint());
        foreach (SIPViaHeader viaHeader in sipRequest.Header.Vias.Via) 
        {
            string viaEndPointStr = $"{viaHeader.ReceivedFromIPAddress}:{viaHeader.ReceivedFromPort}";
            if (IPEndPoint.TryParse(viaEndPointStr, out IPEndPoint? viaEndPoint))
            {
                peer.RemoteEndPoints.Add(viaEndPoint);
            }
        }

        bool hasReachedDestination = toPeer == await bootstrapperService.GetPeerIdAsync();
        message.WasDecrypted = (message.WasDelivered = hasReachedDestination) && wasDecrypted;
        if (hasReachedDestination)
        {
            if (!messagesBag.TryTake(out TaskCompletionSource<SubverseMessage>? tcs))
            {
                messagesBag.Add(tcs = new());
            }
            tcs.SetResult(message);

            SIPResponse sipResponse = SIPResponse.GetResponse(
                sipRequest, SIPResponseStatusCodesEnum.Ok, "Message was delivered."
                );
            await sipTransport.SendResponseAsync(remoteEndPoint, sipResponse);
        }
        else
        {
            SIPViaHeader viaHeader = new(remoteEndPoint, CallProperties.CreateBranchId());
            sipRequest.Header.Vias.PushViaHeader(viaHeader);

            dbService.InsertOrUpdateItem(message);
            await SendSIPRequestAsync(sipRequest);

            SIPResponse sipResponse = SIPResponse.GetResponse(
                sipRequest, SIPResponseStatusCodesEnum.Accepted, "Message was forwarded."
                );
            await sipTransport.SendResponseAsync(remoteEndPoint, sipResponse);
        }
    }

    private async Task SIPTransportResponseReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPResponse sipResponse)
    {
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

        string callId;
        SubversePeerId peerId;
        lock (requestMap)
        {
            callId = sipResponse.Header.CallId;
            peerId = SubversePeerId.FromString(sipResponse.Header.To.ToURI.User);
            requestMap.Remove((callId, peerId));
        }

        if (sipResponse.Status == SIPResponseStatusCodesEnum.Ok)
        {
            lock (CachedPeers)
            {
                if (CachedPeers.TryGetValue(peerId, out SubversePeer? peer))
                {
                    peer.RemoteEndPoints.Add(remoteEndPoint.GetIPEndPoint());
                }
            }
        }

        SubverseMessage? message = dbService.GetMessageByCallId(sipResponse.Header.CallId);
        if (message is not null)
        {
            message.WasDelivered = true;
            dbService.InsertOrUpdateItem(message);
        }
    }

    private async Task SendSIPRequestAsync(SIPRequest sipRequest)
    {
        IBootstrapperService bootstrapperService = await serviceManager.GetWithAwaitAsync<IBootstrapperService>();

        SubversePeerId toPeer = SubversePeerId.FromString(sipRequest.URI.User);
        HashSet<IPEndPoint> cachedEndPoints;
        lock (CachedPeers)
        {
            CachedPeers.TryGetValue(toPeer, out SubversePeer? peer);
            cachedEndPoints = peer?.RemoteEndPoints ?? [];
        }

        foreach (IPEndPoint cachedEndPoint in cachedEndPoints) 
        {
            await sipTransport.SendRequestAsync(new(cachedEndPoint), sipRequest);
        }

        IList<PeerInfo> peerInfo = await bootstrapperService.GetPeerInfoAsync(toPeer);
        foreach (Uri peerUri in peerInfo.Select(x => x.ConnectionUri))
        {
            if (!IPAddress.TryParse(peerUri.DnsSafeHost, out IPAddress? ipAddress))
            {
                continue;
            }

            IPEndPoint ipEndPoint = new(ipAddress, peerUri.Port);
            await sipTransport.SendRequestAsync(new(ipEndPoint), sipRequest);
        }
    }

    public async Task<SubverseMessage> ReceiveMessageAsync(CancellationToken cancellationToken)
    {
        if (!messagesBag.TryTake(out TaskCompletionSource<SubverseMessage>? tcs))
        {
            messagesBag.Add(tcs = new());
        }

        return await tcs.Task.WaitAsync(cancellationToken);
    }

    public async Task SendMessageAsync(SubverseMessage message, CancellationToken cancellationToken = default)
    {
        IBootstrapperService bootstrapperService = await serviceManager.GetWithAwaitAsync<IBootstrapperService>();

        List<Task> sendTasks = new();
        foreach ((SubversePeerId recipient, string contactName) in message.Recipients.Zip(message.RecipientNames))
        {
            sendTasks.Add(Task.Run(async Task? () =>
            {
                SIPURI requestUri = SIPURI.ParseSIPURI($"sip:{recipient}@subverse.network");
                if (message.TopicName is not null)
                {
                    requestUri.Parameters.Set("topic", message.TopicName);
                }

                SIPURI toURI = SIPURI.ParseSIPURI($"sip:{recipient}@subverse.network");
                SIPURI fromURI = SIPURI.ParseSIPURI($"sip:{message.Sender}@subverse.network");

                SIPRequest sipRequest = SIPRequest.GetRequest(
                    SIPMethodsEnum.MESSAGE, requestUri,
                    new(contactName, toURI, null),
                    new(message.SenderName, fromURI, null)
                    );

                if (message.CallId is not null)
                {
                    sipRequest.Header.CallId = message.CallId;
                }

                sipRequest.Header.SetDateHeader();

                sipRequest.Header.Contact = new();
                for (int i = 0; i < message.Recipients.Length; i++)
                {
                    if (message.Recipients[i] == recipient) continue;

                    SIPURI contactUri = SIPURI.ParseSIPURI($"sip:{message.Recipients[i]}@subverse.network");
                    sipRequest.Header.Contact.Add(new(message.RecipientNames[i], contactUri));
                }

                if (message.Sender == await bootstrapperService.GetPeerIdAsync())
                {
                    using (PGP pgp = new(await bootstrapperService.GetPeerKeysAsync(recipient, cancellationToken)))
                    {
                        sipRequest.Body = await pgp.EncryptAndSignAsync(message.Content);
                    }
                }
                else
                {
                    sipRequest.Body = message.Content;
                }

                lock (requestMap)
                {
                    if (!requestMap.ContainsKey((sipRequest.Header.CallId, recipient)))
                    {
                        requestMap.Add((sipRequest.Header.CallId, recipient), sipRequest);
                    }
                    else
                    {
                        requestMap[(sipRequest.Header.CallId, recipient)] = sipRequest;
                    }
                }

                bool flag;
                using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(1500));
                do
                {
                    await SendSIPRequestAsync(sipRequest);
                    await timer.WaitForNextTickAsync(cancellationToken);
                    lock (requestMap)
                    {
                        flag = requestMap.ContainsKey((sipRequest.Header.CallId, recipient));
                    }
                } while (flag && !cancellationToken.IsCancellationRequested);
            }));
        }

        await Task.WhenAll(sendTasks);
    }

    private bool disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                sipChannel.Dispose();
                sipTransport.Dispose();
            }
            CachedPeers.Clear();    
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
