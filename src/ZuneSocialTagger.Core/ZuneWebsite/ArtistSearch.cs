using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Threading;
using System.Xml;
using System.Diagnostics;
using System.Net.Http;

namespace ZuneSocialTagger.Core.ZuneWebsite
{
    public class ArtistSearch
    {
        public static IEnumerable<WebArtist> SearchFor(string searchString)
        {
            string searchUrl = String.Format("{0}?q={1}", Urls.Artist, searchString);

            try
            {
                return ReadArtistsFromXmlDocument(searchUrl);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return new List<WebArtist>();
        }

        public static void SearchForAsync(string searchString,Action<IEnumerable<WebArtist>> callback)
        {
            ThreadPool.QueueUserWorkItem(_ => callback(SearchFor(searchString).ToList()));
        }

        private static IEnumerable<WebArtist> ReadArtistsFromXmlDocument(string searchUrl)
        {

            var xmlDocument = new XmlDocument();
            xmlDocument.Load(searchUrl);
            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            namespaceManager.AddNamespace("a", "http://www.w3.org/2005/Atom");
            var feed = xmlDocument.SelectNodes("/a:feed/a:entry",namespaceManager);
            if (feed != null)
            {
                foreach (XmlNode item in feed)
                {

                    yield return new WebArtist
                    {
                        Name = item.SelectSingleNode("a:title", namespaceManager).InnerText,
                        Id = new Guid(item.SelectSingleNode("a:id", namespaceManager).InnerText)
                    };
                }
            }
        }
    }
}