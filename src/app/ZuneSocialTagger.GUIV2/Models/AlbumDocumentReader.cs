using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using System.ServiceModel.Syndication;
using System.IO;
using System.Text;

namespace ZuneSocialTagger.GUIV2.Models
{
    public class AlbumDocumentReader
    {
        private XmlReader _reader;
        private SyndicationFeed _feed;
        private WebClient _client;

        public event Action<AlbumMetaData> DownloadCompleted = delegate { };

        public bool Initialize(string url)
        {
            try
            {
                _client = new WebClient();

                _client.DownloadDataCompleted += _client_DownloadDataCompleted;
                _client.DownloadDataAsync(new Uri(url));

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        void _client_DownloadDataCompleted(object sender, DownloadDataCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                _reader = XmlReader.Create(new MemoryStream(e.Result));

                this.DownloadCompleted.Invoke(this.Read());
            }
        }

        private AlbumMetaData Read()
        {
            _feed = SyndicationFeed.Load(_reader);

            return _feed != null ? GetAlbumDetails() : null;
        }

        private AlbumMetaData GetAlbumDetails()
        {
            return new AlbumMetaData
              {
                  AlbumTitle = _feed.Title.Text,
                  AlbumArtist = GetArtist(_feed),
                  ArtworkUrl = GetArtworkUrl(_feed)
              };
        }


        private string GetArtist(SyndicationFeed item)
        {
            XElement primaryArtistElement = GetElement(item, "primaryArtist");

            return primaryArtistElement != null ? primaryArtistElement.Elements().Last().Value : null;
        }

        private string GetArtworkUrl(SyndicationFeed feed)
        {
            XElement imageElement = GetElement(feed, "image");

            //TODO: pull out the string formattings, we should just be returning the artwork guid 
            //and deal with what size image to get later

            //TODO: we shouldnt be getting an image that big anyway

            return imageElement != null
                       ? String.Format("{0}{1}?width=50&height=50", "http://image.catalog.zune.net/v3.0/image/",
                                       ExtractGuidFromUrnUuid(imageElement.Elements().First().Value))
                       : null;
        }

        private XElement GetElement(SyndicationFeed feed, string elementName)
        {
            Collection<XElement> elements =
                feed.ElementExtensions.ReadElementExtensions<XElement>(elementName, "http://schemas.zune.net/catalog/music/2007/10");

            return elements.Count > 0 ? elements.First() : null;
        }

        /// <summary>
        /// urn:uuid:c14c4e00-0300-11db-89ca-0019b92a3933
        /// </summary>
        /// <param name="urn"></param>
        /// <returns>c14c4e00-0300-11db-89ca-0019b92a3933</returns>
        public static Guid ExtractGuidFromUrnUuid(string urn)
        {
            return new Guid(urn.Substring(urn.LastIndexOf(':') + 1));
        }

    }
}


