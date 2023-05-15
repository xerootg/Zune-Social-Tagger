using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Threading;
using System.Xml;
using System.Diagnostics;

namespace ZuneSocialTagger.Core.ZuneWebsite
{
    public class AlbumSearch
    {
        public static void SearchForAlbumAsync(string searchString, Action<IEnumerable<WebAlbum>> callback)
        {
            ThreadPool.QueueUserWorkItem(_ => callback(SearchForAlbum(searchString).ToList()));
        }

        public static IEnumerable<WebAlbum> SearchForAlbum(string searchString)
        {
            string searchUrl = String.Format("{0}?q={1}", Urls.Album, searchString);

            try
            {
                return ReadFromXmlDocument(searchUrl);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return new List<WebAlbum>();
        }

        public static void SearchForAlbumFromArtistGuidAsync(Guid guid, Action<IEnumerable<WebAlbum>> callback)
        {
            ThreadPool.QueueUserWorkItem(_ => callback(SearchForAlbumFromArtistGuid(guid)));
        }

        public static IEnumerable<WebAlbum> SearchForAlbumFromArtistGuid(Guid guid)
        {
            var artistAlbumsUrl = String.Format("{0}{1}/albums", Urls.Artist, guid);

            try
            {
                return ReadFromXmlDocument(artistAlbumsUrl);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return new List<WebAlbum>();
        }

        private static IEnumerable<WebAlbum> ReadFromXmlDocument(string searchUrl)
        {
            var xmlDocument = new XmlDocument();
            xmlDocument.Load(searchUrl);
            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            namespaceManager.AddNamespace("a", "http://www.w3.org/2005/Atom");
            var feed = xmlDocument.SelectNodes("/a:feed/a:entry", namespaceManager);
            if (feed != null)
            {
                foreach (XmlNode item in feed)
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
                        var date = item.SelectSingleNode("a:releaseDate", namespaceManager).InnerText;
                        year = DateTime.Parse(date).Year.ToString();
                    }
                    catch { }
                    yield return new WebAlbum
                         {
                             Title = item.SelectSingleNode("a:title", namespaceManager).InnerText,
                             AlbumMediaId = new Guid(item.SelectSingleNode("a:id", namespaceManager).InnerText),
                             Artist = item.SelectSingleNode("a:primaryArtist/a:name", namespaceManager).InnerText,
                             ArtworkUrl = SyndicationExtensions.GetImageUrlFromElement(item.SelectSingleNode("a:image/a:id", namespaceManager).InnerText),
                             // this is probably wrong
                             ReleaseYear = year,
                             Genre = genre

                    };
                }
            }
        }
    }
}