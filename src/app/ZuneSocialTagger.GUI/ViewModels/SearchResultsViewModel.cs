using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using ZuneSocialTagger.Core.ZuneWebsite;

namespace ZuneSocialTagger.GUI.ViewModels
{
    public class SearchResultsViewModel : ViewModelBaseExtended
    {
        private IEnumerable<WebAlbum> _albums;
        private IEnumerable<Artist> _artists;
        private bool _isLoading;
        private SearchResultsDetailViewModel _searchResultsDetailViewModel;
        private string _albumCount;
        private bool _isAlbumsEnabled;
        private string _artistCount;

        public SearchResultsViewModel()
        {
            this.SearchResultsDetailViewModel = new SearchResultsDetailViewModel();
            this.SearchResults = new ObservableCollection<object>();
            this.SearchResults.CollectionChanged += SearchResults_CollectionChanged;
  
            this.MoveNextCommand = new RelayCommand(MoveNext);
            this.MoveBackCommand = new RelayCommand(MoveBack);
            this.ArtistCommand = new RelayCommand(DisplayArtists);
            this.AlbumCommand = new RelayCommand(DisplayAlbums);
            this.ResultClickedCommand = new RelayCommand<object>(ResultClicked);

            this.ArtistCount = "";
            this.AlbumCount = "";
        }

        #region Bindings

        public ObservableCollection<object> SearchResults { get; set; }
        public RelayCommand MoveNextCommand { get; private set; }
        public RelayCommand MoveBackCommand { get; private set; }
        public RelayCommand ArtistCommand { get; private set; }
        public RelayCommand AlbumCommand { get; private set; }
        public RelayCommand<object>ResultClickedCommand { get; private set; }

        public SearchResultsDetailViewModel SearchResultsDetailViewModel
        {
            get { return _searchResultsDetailViewModel; }
            set
            {
                _searchResultsDetailViewModel = value;
                RaisePropertyChanged(() => this.SearchResultsDetailViewModel);
            }
        }

        public bool HasResults
        {
            get { return this.SearchResults.Count > 0; }
        }

        public string AlbumCount
        {
            get { return _albumCount; }
            set
            {
                _albumCount = value;
                RaisePropertyChanged(() => this.AlbumCount);
            }
        }

        public string ArtistCount
        {
            get { return _artistCount; }
            set
            {
                _artistCount = value;
                RaisePropertyChanged(() => this.ArtistCount);
            }
        }

        public bool IsAlbumsEnabled
        {
            get { return _isAlbumsEnabled; }
            set
            {
                _isAlbumsEnabled = value;

                if (_isAlbumsEnabled)
                    DisplayAlbums();
                else
                {
                    DisplayArtists();
                    this.SearchResultsDetailViewModel = null;
                }


                RaisePropertyChanged(() => this.IsAlbumsEnabled);
            }
        }

        public bool IsLoading
        {
            get { return _isLoading; }
            set
            {
                _isLoading = value;
                RaisePropertyChanged(() => this.IsLoading);
            }
        }

        #endregion

        public void LoadAlbum(WebAlbum album)
        {
            this.IsLoading = true;

            if (album == null) return;

            string fullUrlToAlbumXmlDetails = String.Concat(Urls.Album, album.AlbumMediaId);

            var reader = new AlbumDetailsDownloader(fullUrlToAlbumXmlDetails);

            reader.DownloadCompleted += (details, state) => {
                if (state == DownloadState.Success)
                {
                    ApplicationViewModel.AlbumDetailsFromWeb = SetAlbumDetails(details);
                    ApplicationViewModel.SongsFromWebsite = details.Tracks.ToList();

                    UpdateDetail(details);
                    this.IsLoading = false;
                }
                else
                {
                    this.SearchResultsDetailViewModel.SelectedAlbumTitle =
                        "Could not get album details";

                    this.IsLoading = false;
                }
            };
            
            reader.DownloadAsync();
        }

        public void LoadAlbumsForArtist(Artist artist)
        {
            ThreadPool.QueueUserWorkItem(_ => {
                IEnumerable<WebAlbum> albums = AlbumSearch.GetAlbumsFromArtistGuid(artist.Id).ToList();

                _albums = albums;

                DispatcherHelper.CheckBeginInvokeOnUI(() => {
                    this.SearchResults.Clear();

                    foreach (var album in _albums)
                        this.SearchResults.Add(album);

                    this.AlbumCount = String.Format("ALBUMS ({0})", albums.Count());

                    this.IsAlbumsEnabled = true;
                });
            });
        }

        private void DisplayArtists()
        {
            this.SearchResults.Clear();

            foreach (var artist in _artists)
                this.SearchResults.Add(artist);
        }

        private void DisplayAlbums()
        {
            this.SearchResults.Clear();

            foreach (var album in _albums)
                this.SearchResults.Add(album);

            RaisePropertyChanged(() => this.HasResults);
        }

        public void LoadArtists(IEnumerable<Artist> artists)
        {
            _artists = artists;
            this.ArtistCount = String.Format("ARTISTS ({0})", artists.Count());
        }

        public void LoadAlbums(IEnumerable<WebAlbum> albums)
        {
            _albums = albums;
            this.AlbumCount = String.Format("ALBUMS ({0})", albums.Count());

            foreach (WebAlbum album in albums)
                this.SearchResults.Add(album);

            this.IsAlbumsEnabled = true;
        }

        private static void MoveBack()
        {
            Messenger.Default.Send<Type, ApplicationViewModel>(typeof(SearchViewModel));
        }

        private static void MoveNext()
        {
            Messenger.Default.Send<Type, ApplicationViewModel>(typeof(DetailsViewModel));
        }


        private static ExpandedAlbumDetailsViewModel SetAlbumDetails(WebAlbum albumMetaData)
        {
            return new ExpandedAlbumDetailsViewModel
                 {
                     Title = albumMetaData.Title,
                     Artist = albumMetaData.Artist,
                     ArtworkUrl = albumMetaData.ArtworkUrl,
                     Year = albumMetaData.ReleaseYear,
                     SongCount = albumMetaData.Tracks.Count().ToString()
                 };
        }

        private void UpdateDetail(WebAlbum albumMetaData)
        {
             this.SearchResultsDetailViewModel = new SearchResultsDetailViewModel
                                                    {
                                                        SelectedAlbumTitle = albumMetaData.Title
                                                    };

            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                foreach (var track in albumMetaData.Tracks)
                    this.SearchResultsDetailViewModel.SelectedAlbumSongs.Add(track);
            });
        }

        private void SearchResults_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewStartingIndex == 0 && e.NewItems[0].GetType() == typeof(WebAlbum))
            {
                LoadAlbum((WebAlbum)e.NewItems[0]);
            }
        }

        private void ResultClicked(object item)
        {
            if (item.GetType() == typeof(Artist))
                LoadAlbumsForArtist(item as Artist);

            if (item.GetType() == typeof(WebAlbum))
                LoadAlbum(item as WebAlbum);
        }
    }
}