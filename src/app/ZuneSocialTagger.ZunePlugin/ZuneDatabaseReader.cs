using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MicrosoftZuneInterop;
using MicrosoftZuneLibrary;
using ZuneSocialTagger.Core.ZuneDatabase;

namespace ZuneSocialTagger.ZunePlugin
{
    public class ZuneDatabaseReader : IZuneDatabaseReader
    {
        private ZuneLibrary _zuneLibrary;

        public event Action FinishedReadingAlbums = delegate { };
        public event Action<int, int> ProgressChanged = delegate { };

        public bool Initialize()
        {
            try
            {
                //Just copying what the zune software does internally here to initialize the database
                _zuneLibrary = new ZuneLibrary();

                bool dbRebult;

                //anything other than 0 means an error occured reading the database
                int num = _zuneLibrary.Initialize(null, out dbRebult);

                if (num > -1)
                {
                    int phase2;
                    _zuneLibrary.Phase2Initialization(out phase2);
                    _zuneLibrary.CleanupTransientMedia();
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static T GetFieldValue<T>(int mediaId, EListType listType, int atom, T defaultValue)
        {
            int[] columnIndexes = new int[] {atom};
            object[] fieldValues = new object[] {defaultValue};
            ZuneLibrary.GetFieldValues(mediaId, listType, 1, columnIndexes, fieldValues,
                                       new QueryPropertyBag());
            return (T) fieldValues[0];
        }

        public IEnumerable<Album> ReadAlbums()
        {
            //querying all albums, creates a property bag inside this method to query the database
            //thats why we can pass null for the propertybag
            ZuneQueryList albums = _zuneLibrary.QueryDatabase(EQueryType.eQueryTypeAllAlbums, 0,
                                                              EQuerySortType.eQuerySortOrderAscending,
                                                              (uint) SchemaMap.kiIndex_AlbumID, null);
            albums.AddRef();

            object[] uniqueIds = albums.GetUniqueIds().ToArray();

            for (int i = 0; i < uniqueIds.Length; i++)
            {
                object uniqueId = uniqueIds[i];

                yield return GetAlbum((int) uniqueId);

                ProgressChanged.Invoke(i, uniqueIds.Length);
            }

            FinishedReadingAlbums.Invoke();

            albums.Release();
            albums.Dispose();
        }


        public Album GetAlbumByAlbumTitle(string albumTitle)
        {
            throw new NotImplementedException("SearchForString is not working as I expect, needs investigating");

            //querying all albums, creates a property bag inside this method to query the database
            //thats why we can pass null for the propertybag
            ZuneQueryList albums = _zuneLibrary.QueryDatabase(EQueryType.eQueryTypeAllAlbums, 0,
                                                              EQuerySortType.eQuerySortOrderAscending,
                                                              (uint) SchemaMap.kiIndex_AlbumID, null);

            int searchForString = albums.SearchForString((uint) SchemaMap.kiIndex_WMAlbumArtist, false, albumTitle);


            var album = GetAlbum(searchForString);
        }

        public bool DoesAlbumExist(int index)
        {
            try
            {
                var albumMetadata = _zuneLibrary.GetAlbumMetadata(index);

                return albumMetadata != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public Album GetAlbum(int index)
        {
            AlbumMetadata albumMetadata = _zuneLibrary.GetAlbumMetadata(index);

            var albumMediaId = GetFieldValue(index, EListType.eAlbumList,
                                             ZuneQueryList.AtomNameToAtom("ZuneMediaID"), Guid.Empty);

            var dateAdded = GetFieldValue(index, EListType.eAlbumList,
                                          ZuneQueryList.AtomNameToAtom("DateAdded"), new DateTime());

            var album = new Album
                            {
                                AlbumMediaId = albumMediaId,
                                DateAdded = dateAdded,
                                AlbumTitle = albumMetadata.AlbumTitle,
                                AlbumArtist = albumMetadata.AlbumArtist,
                                ArtworkUrl = albumMetadata.CoverUrl,
                                MediaId = albumMetadata.MediaId,
                                ReleaseYear = albumMetadata.ReleaseYear,
                                TrackCount = (int) albumMetadata.TrackCount,
                                Tracks = GetTracksForAlbum(albumMetadata.MediaId).ToList()
                            };

            albumMetadata.Dispose();

            return album;
        }

        public IEnumerable<Track> GetTracksForAlbum(int albumId)
        {
            ZuneQueryList zuneQueryList = _zuneLibrary.GetTracksByAlbum(0, albumId,
                                                                        EQuerySortType.eQuerySortOrderAscending,
                                                                        (uint) SchemaMap.kiIndex_AlbumID);
            for (int i = 0; i < zuneQueryList.Count; i++)
            {
                var track = new ZuneQueryItem(zuneQueryList, i);

                string filePath =
                    (string) track.GetFieldValue(typeof (string), (uint) ZuneQueryList.AtomNameToAtom("SourceURL"));

                Guid mediaId = GetFieldValue(track.ID, EListType.eTrackList, 
                    ZuneQueryList.AtomNameToAtom("ZuneMediaID"), Guid.Empty);

                yield return new Track
                 {
                     FilePath = filePath,
                     MediaId = mediaId
                 };
            }

            zuneQueryList.Dispose();
        }

        public void RemoveAlbumFromDatabase(int albumId)
        {
            ZuneQueryList zuneQueryList = _zuneLibrary.GetTracksByAlbum(0, albumId,
                                                                        EQuerySortType.eQuerySortOrderAscending,
                                                                        (uint) SchemaMap.kiIndex_AlbumID);

            for (int i = 0; i < zuneQueryList.Count; i++)
            {
                var track = new ZuneQueryItem(zuneQueryList, i);

                _zuneLibrary.DeleteMedia(new[] {track.ID}, EMediaTypes.eMediaTypeAudio, false);
            }

            _zuneLibrary.CleanupTransientMedia();

            zuneQueryList.Dispose();
        }

        public void AddTrackToDatabase(string filePath)
        {
            _zuneLibrary.AddMedia(filePath);
        }

        public bool CanInitialize
        {
            get
            {
                //The version that this program was written to support, in future versions methods could change
                //so updates will probably be needed
                var supportedVersion = new Version(4, 2, 202, 0);

                if (!File.Exists("ZuneDBApi.dll"))
                    throw new FileNotFoundException(
                        "Could not find ZuneDBApi.dll. Are you sure Zune Social tagger is running in the Zune application folder?");

                var fileVersionInfo = FileVersionInfo.GetVersionInfo("ZuneDBApi.dll");

                var fileVersion = new Version(fileVersionInfo.ProductVersion);

                if (fileVersion != supportedVersion)
                    throw new NotSupportedException(
                        "Zune Social Tagger was not designed to work with this version of the Zune software, proceed with caution and please wait for an update!");

                return true;
            }
        }
        
        public void Dispose()
        {
            _zuneLibrary.Dispose();
        }
    }
}