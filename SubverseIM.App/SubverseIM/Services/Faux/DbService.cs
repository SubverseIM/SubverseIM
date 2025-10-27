using LiteDB;
using MonoTorrent;
using SubverseIM.Core;
using SubverseIM.Core.Storage.Messages;
using SubverseIM.Models;
using SubverseIM.Serializers;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Faux
{
    public class DbService : IDbService
    {
        private readonly SubverseConfig config = new();

        private readonly Dictionary<SubversePeerId, SubverseContact> contacts = new();

        private readonly Dictionary<MessageId, SubverseMessage> messages = new();

        private readonly Dictionary<InfoHash, SubverseTorrent> torrents = new();

        private readonly Dictionary<string, Stream> fileStreams = new();

        public Task<SubverseConfig?> GetConfigAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<SubverseConfig?>(cancellationToken);
            }
            else
            {
                lock (config)
                {
                    return Task.FromResult(config.Id is null ? null : config);
                }
            }
        }

        public Task<bool> UpdateConfigAsync(SubverseConfig config, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<bool>(cancellationToken);
            }
            else
            {
                lock (config)
                {
                    bool flag = this.config.Id is not null;
                    this.config.Id ??= ObjectId.NewObjectId();
                    this.config.BootstrapperUriList = config.BootstrapperUriList;
                    return Task.FromResult(flag);
                }
            }
        }

        private IEnumerable<SubverseContact> GetContactsCore()
        {
            lock (contacts)
            {
                foreach (SubverseContact contact in contacts.Values
                    .OrderByDescending(x => x.DateLastChattedWith))
                {
                    yield return contact;
                }
            }
        }

        public Task<IEnumerable<SubverseContact>> GetContactsAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<IEnumerable<SubverseContact>>(cancellationToken);
            }
            else
            {
                return Task.FromResult(GetContactsCore());
            }
        }

        public Task<SubverseContact?> GetContactAsync(SubversePeerId otherPeer, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<SubverseContact?>(cancellationToken);
            }
            else
            {
                lock (contacts)
                {
                    contacts.TryGetValue(otherPeer, out SubverseContact? contact);
                    return Task.FromResult(contact);
                }
            }
        }

        public Task<IReadOnlyDictionary<string, IEnumerable<SubversePeerId>>> GetAllMessageTopicsAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<IReadOnlyDictionary<string, IEnumerable<SubversePeerId>>>(cancellationToken);
            }
            else
            {
                lock (messages)
                {
                    return Task.FromResult<IReadOnlyDictionary<string, IEnumerable<SubversePeerId>>>
                        (messages.Values
                        .OrderByDescending(x => x.DateSignedOn)
                        .Where(x => !string.IsNullOrEmpty(x.TopicName) && x.TopicName != "#system")
                        .GroupBy(x => x.TopicName!)
                        .ToFrozenDictionary(g => g.Key, g => g
                            .SelectMany(x => x.Recipients)
                            .Distinct()));
                }
            }
        }

        private IEnumerable<SubverseMessage> GetMessagesWithPeersOnTopicCore(HashSet<SubversePeerId> otherPeers, string? topicName, bool orderFlag)
        {
            lock (messages)
            {
                IEnumerable<SubverseMessage> topicMessages = otherPeers
                    .SelectMany(otherPeer => messages.Values
                    .Where(x => x.WasDecrypted ?? true)
                    .Where(x => otherPeer == x.Sender || x.Recipients.Contains(otherPeer))
                    .Where(x => string.IsNullOrEmpty(topicName) || x.TopicName == topicName))
                    .DistinctBy(x => x.MessageId);
                topicMessages = orderFlag ? topicMessages.OrderBy(x => x.DateSignedOn) :
                    topicMessages.OrderByDescending(x => x.DateSignedOn);
                foreach (SubverseMessage message in topicMessages)
                {
                    yield return message;
                }
            }
        }

        public Task<IEnumerable<SubverseMessage>> GetMessagesWithPeersOnTopicAsync(HashSet<SubversePeerId> otherPeers, string? topicName, bool orderFlag, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<IEnumerable<SubverseMessage>>(cancellationToken);
            }
            else
            {
                return Task.FromResult(GetMessagesWithPeersOnTopicCore(otherPeers, topicName, orderFlag));
            }
        }

        private IEnumerable<SubverseMessage> GetAllUndeliveredMessagesCore()
        {
            lock (messages)
            {
                foreach (SubverseMessage message in messages.Values
                    .Where(x => x.WasDecrypted != false && x.WasDelivered == false)
                    .OrderBy(x => x.DateSignedOn))
                {
                    yield return message;
                }
            }
        }

        public Task<IEnumerable<SubverseMessage>> GetAllUndeliveredMessagesAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) 
            { 
                return Task.FromCanceled<IEnumerable<SubverseMessage>>(cancellationToken); 
            }
            else
            {
                return Task.FromResult(GetAllUndeliveredMessagesCore());
            }
        }

        public Task<SubverseMessage?> GetMessageByIdAsync(MessageId messageId, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) 
            { 
                return Task.FromCanceled<SubverseMessage?>(cancellationToken); 
            }
            else
            {
                lock (messages)
                {
                    messages.TryGetValue(messageId, out SubverseMessage? message);
                    return Task.FromResult(message);
                }
            }
        }

        public Task<SubverseTorrent?> GetTorrentAsync(InfoHash infoHash, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) 
            {
                return Task.FromCanceled<SubverseTorrent?>(cancellationToken); 
            }
            else
            {
                lock (torrents)
                {
                    torrents.TryGetValue(infoHash, out SubverseTorrent? torrent);
                    return Task.FromResult(torrent);
                }
            }
        }

        private IEnumerable<SubverseTorrent> GetTorrentsCore()
        {
            lock (torrents)
            {
                foreach (SubverseTorrent torrent in torrents.Values
                    .OrderByDescending(x => x.DateLastUpdatedOn))
                {
                    yield return torrent;
                }
            }
        }

        public Task<IEnumerable<SubverseTorrent>> GetTorrentsAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) 
            { 
                return Task.FromCanceled<IEnumerable<SubverseTorrent>>(cancellationToken); 
            }
            else
            {
                return Task.FromResult(GetTorrentsCore());
            }
        }

        public Task<bool> InsertOrUpdateItemAsync(SubverseContact newItem, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) 
            { 
                return Task.FromCanceled<bool>(cancellationToken); 
            }
            else
            {
                lock (contacts)
                {
                    if (contacts.TryGetValue(newItem.OtherPeer, out SubverseContact? oldItem))
                    {
                        oldItem.ChatColorCode = newItem.ChatColorCode;
                        oldItem.DateLastChattedWith = newItem.DateLastChattedWith;
                        oldItem.DisplayName = newItem.DisplayName;
                        oldItem.ImagePath = newItem.ImagePath;
                        oldItem.UserNote = newItem.UserNote;
                        return Task.FromResult(true);
                    }
                    else
                    {
                        newItem.Id ??= ObjectId.NewObjectId();
                        contacts.Add(newItem.OtherPeer, newItem);
                        return Task.FromResult(false);
                    }
                }
            }
        }

        public Task<bool> InsertOrUpdateItemAsync(SubverseTorrent newItem, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<bool>(cancellationToken);
            }
            else
            {
                lock (torrents)
                {
                    if (torrents.TryGetValue(newItem.InfoHash, out SubverseTorrent? oldItem))
                    {
                        oldItem.DateLastUpdatedOn = newItem.DateLastUpdatedOn;
                        oldItem.TorrentBytes = newItem.TorrentBytes;
                        return Task.FromResult(true);
                    }
                    else
                    {
                        newItem.Id ??= ObjectId.NewObjectId();
                        torrents.Add(newItem.InfoHash, newItem);
                        return Task.FromResult(false);
                    }
                }
            }
        }

        public Task<bool> InsertOrUpdateItemAsync(SubverseMessage newItem, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<bool>(cancellationToken);
            }
            else
            {
                lock (messages)
                {
                    if (newItem.MessageId is not null && messages
                        .TryGetValue(newItem.MessageId, out SubverseMessage? oldItem))
                    {
                        oldItem.Content = newItem.Content;
                        oldItem.DateSignedOn = newItem.DateSignedOn;
                        oldItem.RecipientNames = newItem.RecipientNames;
                        oldItem.Recipients = newItem.Recipients;
                        oldItem.Sender = newItem.Sender;
                        oldItem.SenderName = newItem.SenderName;
                        oldItem.TopicName = newItem.TopicName;
                        oldItem.WasDecrypted = newItem.WasDecrypted;
                        oldItem.WasDelivered = newItem.WasDelivered;
                        return Task.FromResult(true);
                    }
                    else if (newItem.MessageId is not null)
                    {
                        newItem.Id ??= ObjectId.NewObjectId();
                        messages.Add(newItem.MessageId, newItem);
                        return Task.FromResult(false);
                    }
                    else
                    {
                        throw new ArgumentNullException(nameof(newItem.MessageId));
                    }
                }
            }
        }

        public Task DeleteAllMessagesOfTopicAsync(string topicName, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }
            else
            {
                lock (messages)
                {
                    foreach (SubverseMessage message in messages.Values
                        .Where(x => x.TopicName == topicName)
                        .Where(x => x.MessageId is not null))
                    {
                        messages.Remove(message.MessageId!);
                    }
                }

                return Task.CompletedTask;
            }
        }

        public Task WriteAllMessagesOfTopicAsync(ISerializer<SubverseMessage> serializer, string topicName, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }
            else
            {
                foreach (SubverseMessage message in messages.Values
                        .Where(x => x.TopicName == topicName)
                        .Where(x => x.MessageId is not null)
                        .OrderByDescending(x => x.DateSignedOn))
                {
                    serializer.Serialize(message);
                }

                return Task.CompletedTask;
            }
        }

        public Task<bool> DeleteItemByIdAsync<T>(BsonValue id, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<bool>(cancellationToken);
            }
            else
            {
                bool removedFlag = false;
                switch (typeof(T).FullName)
                {
                    case "SubverseIM.Models.SubverseContact":
                        lock (contacts)
                        {
                            foreach ((SubversePeerId otherPeer, SubverseContact _) in contacts
                                .Where(x => x.Value.Id == id.AsObjectId).ToHashSet())
                            {
                                removedFlag |= contacts.Remove(otherPeer);
                            }
                            break;
                        }
                    case "SubverseIM.Models.SubverseMessage":
                        lock (messages)
                        {
                            foreach ((MessageId messageId, SubverseMessage _) in messages
                                .Where(x => x.Value.Id == id.AsObjectId).ToHashSet())
                            {
                                removedFlag |= messages.Remove(messageId);
                            }
                            break;
                        }
                    case "SubverseIM.Models.SubverseTorrent":
                        lock (torrents)
                        {
                            foreach ((InfoHash infoHash, SubverseTorrent _) in torrents
                                .Where(x => x.Value.Id == id.AsObjectId).ToHashSet())
                            {
                                removedFlag |= torrents.Remove(infoHash);
                            }
                            break;
                        }
                    default:
                        throw new ArgumentException($"{nameof(DbService)} does not manage a collection of type: \"{typeof(T).FullName}\"", nameof(T));
                }
                return Task.FromResult(removedFlag);
            }
        }

        public Task<Stream?> GetReadStreamAsync(string path, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<Stream?>(cancellationToken);
            }
            else
            {
                lock (fileStreams)
                {
                    if (fileStreams.TryGetValue(path, out Stream? stream))
                    {
                        try
                        {
                            stream.Position = 0;
                        }
                        catch (ObjectDisposedException)
                        {
                            stream = new MemoryStream();
                        }

                        return Task.FromResult<Stream?>(stream);
                    }
                    else
                    {
                        return Task.FromResult<Stream?>(null);
                    }
                }
            }
        }

        public Task<Stream> CreateWriteStreamAsync(string path, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<Stream>(cancellationToken);
            }
            else
            {
                lock (fileStreams)
                {
                    if (fileStreams.TryGetValue(path, out Stream? fileStream))
                    {
                        try
                        {
                            fileStream.Position = 0;
                            fileStream.SetLength(0);
                            return Task.FromResult(fileStream);
                        }
                        catch (ObjectDisposedException)
                        {
                            return Task.FromResult(fileStream = new MemoryStream());
                        }
                    }
                    else
                    {
                        fileStream = new MemoryStream();
                        fileStreams.Add(path, fileStream);
                        return Task.FromResult(fileStream);
                    }
                }
            }
        }

        public Task InjectAsync(IServiceManager serviceManager)
        {
            return Task.CompletedTask;
        }

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    lock (contacts)
                    {
                        contacts.Clear();
                    }

                    lock (messages)
                    {
                        messages.Clear();
                    }

                    lock (torrents)
                    {
                        torrents.Clear();
                    }

                    lock (fileStreams)
                    {
                        foreach ((string _, Stream s) in fileStreams)
                        {
                            s.Dispose();
                        }
                        fileStreams.Clear();
                    }
                }
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
