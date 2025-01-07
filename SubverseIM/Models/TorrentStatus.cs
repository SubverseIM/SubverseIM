using MonoTorrent.Client;

namespace SubverseIM.Models;

public record TorrentStatus(bool Complete, double Progress, TorrentState State) { }

