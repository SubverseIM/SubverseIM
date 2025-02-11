using LiteDB;
using SubverseIM.Core;
using SubverseIM.Models;
using SubverseIM.Serializers;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace SubverseIM.Services.Faux
{
    public class DbService : IDbService
    {
        private readonly SubverseConfig config = new();

        private readonly Dictionary<SubversePeerId, SubverseContact> contacts = new();

        private readonly Dictionary<string, SubverseMessage> messages = new();

        private readonly Dictionary<string, SubverseTorrent> torrents = new();

        private readonly Dictionary<string, Stream> fileStreams = new();

        public SubverseConfig? GetConfig()
        {
            lock (config)
            {
                return config.Id is null ? null : config;
            }
        }

        public bool UpdateConfig(SubverseConfig config)
        {
            lock (config)
            {
                bool flag = this.config.Id is not null;
                this.config.Id ??= ObjectId.NewObjectId();
                this.config.BootstrapperUriList = config.BootstrapperUriList;
                return flag;
            }
        }

        public IEnumerable<SubverseContact> GetContacts()
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

        public SubverseContact? GetContact(SubversePeerId otherPeer)
        {
            lock (contacts)
            {
                contacts.TryGetValue(otherPeer, out SubverseContact? contact);
                return contact;
            }
        }

        public IReadOnlyDictionary<string, IEnumerable<SubversePeerId>> GetAllMessageTopics()
        {
            lock (messages)
            {
                return messages.Values
                    .OrderByDescending(x => x.DateSignedOn)
                    .Where(x => !string.IsNullOrEmpty(x.TopicName) && x.TopicName != "#system")
                    .GroupBy(x => x.TopicName!)
                    .ToFrozenDictionary(g => g.Key, g => g
                        .SelectMany(x => x.Recipients)
                        .Distinct());
            }
        }

        public IEnumerable<SubverseMessage> GetMessagesWithPeersOnTopic(HashSet<SubversePeerId> otherPeers, string? topicName)
        {
            lock (messages)
            {
                IEnumerable<SubverseMessage> topicMessages = otherPeers
                    .SelectMany(otherPeer => messages.Values
                    .Where(x => x.WasDecrypted ?? true)
                    .Where(x => otherPeer == x.Sender || x.Recipients.Contains(otherPeer))
                    .Where(x => string.IsNullOrEmpty(topicName) || x.TopicName == topicName))
                    .DistinctBy(x => x.CallId)
                    .OrderByDescending(x => x.DateSignedOn);

                foreach (SubverseMessage message in topicMessages)
                {
                    yield return message;
                }
            }
        }

        public IEnumerable<SubverseMessage> GetAllUndeliveredMessages()
        {
            lock (messages)
            {
                foreach (SubverseMessage message in messages.Values
                    .Where(x => !x.WasDelivered))
                {
                    yield return message;
                }
            }
        }

        public SubverseMessage? GetMessageByCallId(string callId)
        {
            lock (messages)
            {
                messages.TryGetValue(callId, out SubverseMessage? message);
                return message;
            }
        }

        public SubverseTorrent? GetTorrent(string magnetUri)
        {
            lock (torrents)
            {
                torrents.TryGetValue(magnetUri, out SubverseTorrent? torrent);
                return torrent;
            }
        }

        public IEnumerable<SubverseTorrent> GetTorrents()
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

        public bool InsertOrUpdateItem(SubverseContact newItem)
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
                    return true;
                }
                else
                {
                    newItem.Id ??= ObjectId.NewObjectId();
                    contacts.Add(newItem.OtherPeer, newItem);
                    return false;
                }
            }
        }

        public bool InsertOrUpdateItem(SubverseTorrent newItem)
        {
            lock (torrents)
            {
                if (torrents.TryGetValue(newItem.MagnetUri, out SubverseTorrent? oldItem))
                {
                    oldItem.DateLastUpdatedOn = newItem.DateLastUpdatedOn;
                    oldItem.TorrentBytes = newItem.TorrentBytes;
                    return true;
                }
                else
                {
                    newItem.Id ??= ObjectId.NewObjectId();
                    torrents.Add(newItem.MagnetUri, newItem);
                    return false;
                }
            }
        }

        public bool InsertOrUpdateItem(SubverseMessage newItem)
        {
            lock (messages)
            {
                if (newItem.CallId is not null && messages
                    .TryGetValue(newItem.CallId, out SubverseMessage? oldItem))
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
                    return true;
                }
                else if (newItem.CallId is not null)
                {
                    newItem.Id ??= ObjectId.NewObjectId();
                    messages.Add(newItem.CallId, newItem);
                    return false;
                }
                else
                {
                    throw new ArgumentNullException(nameof(newItem.CallId));
                }
            }
        }

        public void DeleteAllMessagesOfTopic(string topicName)
        {
            lock (messages)
            {
                foreach (SubverseMessage message in messages.Values
                    .Where(x => x.TopicName == topicName)
                    .Where(x => x.CallId is not null))
                {
                    messages.Remove(message.CallId!);
                }
            }
        }

        public void WriteAllMessagesOfTopic(ISerializer<SubverseMessage> serializer, string topicName)
        {
            foreach (SubverseMessage message in messages.Values
                    .Where(x => x.TopicName == topicName)
                    .Where(x => x.CallId is not null)
                    .OrderByDescending(x => x.DateSignedOn))
            {
                serializer.Serialize(message);
            }
        }

        public bool DeleteItemById<T>(BsonValue id)
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
                        foreach ((string callId, SubverseMessage _) in messages
                            .Where(x => x.Value.Id == id.AsObjectId).ToHashSet())
                        {
                            removedFlag |= messages.Remove(callId);
                        }
                        break;
                    }
                case "SubverseIM.Models.SubverseTorrent":
                    lock (torrents)
                    {
                        foreach ((string magnetUri, SubverseTorrent _) in torrents
                            .Where(x => x.Value.Id == id.AsObjectId).ToHashSet())
                        {
                            removedFlag |= torrents.Remove(magnetUri);
                        }
                        break;
                    }
                default:
                    throw new ArgumentException($"{nameof(DbService)} does not manage a collection of type: \"{typeof(T).FullName}\"", nameof(T));
            }
            return removedFlag;
        }

        public bool TryGetReadStream(string path, [NotNullWhen(true)] out Stream? stream)
        {
            lock (fileStreams)
            {
                if (fileStreams.TryGetValue(path, out stream))
                {
                    try
                    {
                        stream.Position = 0;
                        return true;
                    }
                    catch (ObjectDisposedException) 
                    {
                        stream = new MemoryStream();
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        public Stream CreateWriteStream(string path)
        {
            lock (fileStreams)
            {
                if (fileStreams.TryGetValue(path, out Stream? fileStream))
                {
                    try
                    {
                        fileStream.Position = 0;
                        fileStream.SetLength(0);
                        return fileStream;
                    }
                    catch (ObjectDisposedException) 
                    {
                        return fileStream = new MemoryStream();
                    }
                }
                else
                {
                    fileStream = new MemoryStream();
                    fileStreams.Add(path, fileStream);
                    return fileStream;
                }
            }
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
