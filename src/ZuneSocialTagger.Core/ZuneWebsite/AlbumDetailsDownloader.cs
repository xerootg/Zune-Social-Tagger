using Microsoft.Zune.Service;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Xml;
using System.Xml.Linq;
using TagLib.Ape;

namespace ZuneSocialTagger.Core.ZuneWebsite
{
    /// <summary>
    /// Downloads the album details from the zune album's xml document
    /// </summary>
    public static class AlbumDetailsDownloader
    {
        private static List<WebRequest> _currentRequests = new List<WebRequest>();

        public static bool Aborted = false;

        public static void DownloadAsync(string url, Action<WebException, WebAlbum> callback)
        {
            var request = WebRequest.Create(new Uri(url));
            _currentRequests.Add(request);

            request.BeginGetResponse(ar => 
            {
                var currrentRequests = (List<WebRequest>)ar.AsyncState;

                try
                {
                    using (var response = request.EndGetResponse(ar))
                    {
                        var doc = new XmlDocument();
                        doc.Load(response.GetResponseStream());
                        callback.Invoke(null, GetAlbumDetails(doc));
                    }
                }
                catch (WebException ex)
                {
                    callback.Invoke(ex, null);
                }
                catch (IOException ex)
                {
                    //callback.Invoke(ex, null);
                    //TODO: log web response fail (usually after abort)
                }
                finally
                {
                    currrentRequests.Remove(request);
                }
            }, _currentRequests);
        }

        public static void AbortAllCurrentRequests()
        {
            foreach (var request in _currentRequests.ToList())
            {
                request.Abort();
            }

            Aborted = true;
        }

        private static WebAlbum GetAlbumDetails(XmlDocument xmlDocument)
        {
            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            namespaceManager.AddNamespace("a", "http://www.w3.org/2005/Atom");
            var item = xmlDocument.SelectSingleNode("/feed", namespaceManager);

            if (item != null)
            {
                var genre = string.Empty;
                var year = string.Empty;
                try
                {
                    genre = item.SelectSingleNode("a:primaryGenre", namespaceManager).InnerText;
                }
                catch { }

                try
                {
                    var date = item.SelectSingleNode("releaseDate", namespaceManager).InnerText;
                    year = DateTime.Parse(date).Year.ToString();
                }
                catch { }
                var albumId = new Guid(item.SelectSingleNode("a:id", namespaceManager).InnerText);
                return new WebAlbum
                {
                    Title = item.SelectSingleNode("a:title", namespaceManager).InnerText,
                    Artist = item.SelectSingleNode("primaryArtist/name", namespaceManager).InnerText,
                    ArtworkUrl = SyndicationExtensions.GetImageUrlFromElement(item.SelectSingleNode("image/id", namespaceManager).InnerText),
                    ReleaseYear = year,
                    // Tracks are the elements within /feed/entry
                    Tracks = GetTracks(xmlDocument.SelectNodes("feed/entry", namespaceManager), namespaceManager, albumId).ToList(),
                    Genre = genre,
                    AlbumMediaId = albumId
                };
            }

            return null;
        }

        private static List<WebTrack> GetTracks(XmlNodeList feed, XmlNamespaceManager namespaceManager, Guid albumId)
        {
            var trackList = new List<WebTrack>();

            foreach(XmlNode item in feed) 
            {

                var trackArtistId = new Guid();

                try
                {
                    // each track should have a primary artist ID, else failover to album
                    trackArtistId = new Guid(item.SelectSingleNode("primaryArtist/id").InnerText);

                }
                catch { }

                var contributingArtists = new List<string>();

                try
                {
                    foreach(XmlNode artist in item.SelectNodes("artists/artist/name")) 
                    {
                        contributingArtists.Add(artist.InnerText);
                    }
                }
                catch { }


                trackList.Add( new WebTrack
                {
                    MediaId = new Guid(item.SelectSingleNode("a:id", namespaceManager).InnerText),
                    ArtistMediaId = trackArtistId,
                    AlbumMediaId = albumId,
                    Title = item.SelectSingleNode("a:title", namespaceManager).InnerText,
                    DiscNumber = item.SelectSingleNode("discNumber").InnerText,
                    TrackNumber = item.SelectSingleNode("trackNumber").InnerText,
                    Genre = string.Empty, // Our API does not return this value :(
                    //Genre = item.GetGenre(),
                    ContributingArtists = contributingArtists,
                    Artist = item.SelectSingleNode("primaryArtist/name", namespaceManager).InnerText
                });
            }

            return trackList;
        }
    }
}


