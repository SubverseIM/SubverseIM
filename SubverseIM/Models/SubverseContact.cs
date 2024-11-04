﻿using LiteDB;

namespace SubverseIM.Models
{
    public class SubverseContact
    {
        public ObjectId? Id { get; set; }

        public SubversePeerId OtherPeer { get; set; }

        public string? ImagePath { get; set; }

        public string? DisplayName { get; set; }

        public string? UserNote { get; set; }
    }
}
