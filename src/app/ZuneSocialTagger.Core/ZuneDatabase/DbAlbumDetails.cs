﻿using System;
using System.Collections.Generic;

namespace ZuneSocialTagger.Core.ZuneDatabase
{
    public class DbAlbumDetails
    {
        public string AlbumTitle { get; set; }
        public string AlbumArtist { get; set; }
        public string ArtworkUrl { get; set; }
        public DateTime DateAdded { get; set; }
        public string AlbumMediaId { get; set; }
        public int MediaId { get; set; }
        public int ReleaseYear { get; set; }
        public int TrackCount { get; set; }
        public List<DbTrack> Tracks { get; set; }
    }
}