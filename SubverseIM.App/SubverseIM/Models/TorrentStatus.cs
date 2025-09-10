using MonoTorrent.Client;

namespace SubverseIM.Models;

public record TorrentStatus(bool Complete, bool HasMetadata, double Progress) { }

