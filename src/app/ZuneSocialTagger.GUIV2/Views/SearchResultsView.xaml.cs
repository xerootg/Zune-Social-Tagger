﻿using System.Windows.Controls;
using ZuneSocialTagger.Core.ZuneWebsiteScraper;
using ZuneSocialTagger.GUIV2.ViewModels;

namespace ZuneSocialTagger.GUIV2.Views
{
    /// <summary>
    /// Interaction logic for SearchResultsView.xaml
    /// </summary>
    public partial class SearchResultsView : UserControl
    {
        public SearchResultsView()
        {
            this.InitializeComponent();
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var dContext = (SearchResultsViewModel) this.DataContext;

            if (dContext != null && e.AddedItems.Count > 0)
            {
                dContext.LoadAlbum((AlbumSearchResult) e.AddedItems[0]);
            }
        }
    }
}