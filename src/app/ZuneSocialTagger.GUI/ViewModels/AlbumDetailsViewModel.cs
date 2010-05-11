using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ZuneSocialTagger.Core.IO;
using ZuneSocialTagger.Core.ZuneDatabase;
using ZuneSocialTagger.Core.ZuneWebsite;
using ZuneSocialTagger.GUI.Models;

namespace ZuneSocialTagger.GUI.ViewModels
{
    [Serializable]
    public class AlbumDetailsViewModel : ViewModelBaseExtended
    {
        [NonSerialized]
        private readonly IZuneDatabaseReader _dbReader;

        private DbAlbum _zuneAlbumMetaData;
        private WebAlbum _webAlbumMetaData;
        private LinkStatus _linkStatus;

        [NonSerialized]
        private RelayCommand _refreshCommand;
        [NonSerialized]
        private RelayCommand _delinkCommand;
        [NonSerialized]
        private RelayCommand _linkCommand;

        [field: NonSerialized]
        public event Action AlbumDetailsDownloaded = delegate { };

        public AlbumDetailsViewModel(IZuneDatabaseReader dbReader)
        {
            _dbReader = dbReader;

            this.DelinkCommand = new RelayCommand(DelinkAlbum);
            this.LinkCommand = new RelayCommand(LinkAlbum);
            this.RefreshCommand = new RelayCommand(RefreshAlbum);
        }

        public AlbumDetailsViewModel()
        {
            //used for serialization purposes
        }

        public RelayCommand RefreshCommand
        {
            get { return _refreshCommand; }
            private set { _refreshCommand = value; }
        }

        public RelayCommand LinkCommand
        {
            get { return _linkCommand; }
            private set { _linkCommand = value; }
        }

        public RelayCommand DelinkCommand
        {
            get { return _delinkCommand; }
            private set { _delinkCommand = value; }
        }

        public DbAlbum ZuneAlbumMetaData
        {
            get { return _zuneAlbumMetaData; }
            set
            {
                _zuneAlbumMetaData = value;
                RaisePropertyChanged(() => this.ZuneAlbumMetaData);
            }
        }

        public WebAlbum WebAlbumMetaData
        {
            get { return _webAlbumMetaData; }
            set
            {
                _webAlbumMetaData = value;
                RaisePropertyChanged(() => this.WebAlbumMetaData);
            }
        }

        public LinkStatus LinkStatus
        {
            get { return _linkStatus; }
            set
            {
                _linkStatus = value;
                RaisePropertyChanged(() => this.LinkStatus);
                RaisePropertyChanged(() => this.CanDelink);
                RaisePropertyChanged(() => this.CanLink);
            }
        }

        public bool CanDelink
        {
            get { return _linkStatus != LinkStatus.Unlinked && _linkStatus != LinkStatus.Unknown; }
        }

        public bool CanLink
        {
            get { return true; }
        }

        public void LinkAlbum()
        {
            ApplicationViewModel.SongsFromFile = new List<Song>();
            var tracks = ApplicationViewModel.SongsFromFile;
            //TODO: instead of passing the selected album into the ctor, create it here and using the messaging system to
            //set it in the applicationviewmodel
            var albumDetails = this.ZuneAlbumMetaData;

            DoesAlbumExistInDbAndDisplayError(albumDetails);

            IEnumerable<DbTrack> tracksForAlbum = _dbReader.GetTracksForAlbum(albumDetails.MediaId);

            foreach (DbTrack track in tracksForAlbum)
            {
                var zuneTagContainer = SharedMethods.GetContainer(track.FilePath);

                if (zuneTagContainer != null)
                    tracks.Add(new Song(track.FilePath, zuneTagContainer));
                else
                    return;
            }

            ApplicationViewModel.AlbumDetailsFromFile = SharedMethods.GetAlbumDetailsFrom(albumDetails);

            //tell the application to switch to the search view
            Messenger.Default.Send<Type, ApplicationViewModel>(typeof(SearchViewModel));
            //send the search text to the search view model after it has been constructed
            Messenger.Default.Send<string, SearchViewModel>(albumDetails.Title + " " + albumDetails.Artist);
        }

        public void DelinkAlbum()
        {
            DoesAlbumExistInDbAndDisplayError(this.ZuneAlbumMetaData);

            Mouse.OverrideCursor = Cursors.Wait;

            //TODO: fix bug where application crashes when removing an album that is currently playing

            List<DbTrack> tracksForAlbum = _dbReader.GetTracksForAlbum(this.ZuneAlbumMetaData.MediaId).ToList();

            foreach (var track in tracksForAlbum)
            {
                IZuneTagContainer zuneTagContainer = SharedMethods.GetContainer(track.FilePath);

                if (zuneTagContainer != null)
                {
                    zuneTagContainer.RemoveZuneAttribute("WM/WMContentID");
                    zuneTagContainer.RemoveZuneAttribute("WM/WMCollectionID");
                    zuneTagContainer.RemoveZuneAttribute("WM/WMCollectionGroupID");
                    zuneTagContainer.RemoveZuneAttribute("ZuneCollectionID");
                    zuneTagContainer.RemoveZuneAttribute("WM/UniqueFileIdentifier");
                    zuneTagContainer.RemoveZuneAttribute("ZuneCollectionID");
                    zuneTagContainer.RemoveZuneAttribute("ZuneUserEditedFields");
                    zuneTagContainer.RemoveZuneAttribute(ZuneIds.Album);
                    zuneTagContainer.RemoveZuneAttribute(ZuneIds.Artist);
                    zuneTagContainer.RemoveZuneAttribute(ZuneIds.Track);

                    zuneTagContainer.WriteToFile(track.FilePath);
                }
                else
                {
                    return;
                }
            }

            Mouse.OverrideCursor = null;

            Messenger.Default.Send<ErrorMessage,ApplicationViewModel>(new ErrorMessage(ErrorMode.Warning,
                                                    "Album should now be de-linked. You may need to " +
                                                    "remove then re-add the album for the changes to take effect."));

            //force a refresh on the album to see if the de-link worked
            //this probably wont work because the zunedatabase does not correctly change the albums
            //details when delinking, but does when linking
            RefreshAlbum();
        }

        public void RefreshAlbum()
        {
            DoesAlbumExistInDbAndDisplayError(this.ZuneAlbumMetaData);

            DbAlbum albumMetaData = _dbReader.GetAlbum(this.ZuneAlbumMetaData.MediaId);
            this.ZuneAlbumMetaData = albumMetaData;
            this.LinkStatus = LinkStatus.Unknown;
            this.WebAlbumMetaData = null;
            GetAlbumDetailsFromWebsite();
        }

        public void GetAlbumDetailsFromWebsite()
        {
            Guid albumMediaId = this.ZuneAlbumMetaData.AlbumMediaId;

            if (albumMediaId != Guid.Empty)
            {
                var downloader = new AlbumDetailsDownloader(String.Concat(Urls.Album, albumMediaId));

                downloader.DownloadCompleted += (alb, state) => 
                {
                    if (state == DownloadState.Success)
                        SharedMethods.SetAlbumDetails(alb, this);
                    else
                        this.LinkStatus = LinkStatus.Unavailable;

                    AlbumDetailsDownloaded.Invoke();
                };

                downloader.DownloadAsync();
            }
            else
                this.LinkStatus = LinkStatus.Unlinked;
        }

        private void DoesAlbumExistInDbAndDisplayError(DbAlbum selectedAlbum)
        {
            //if (!SharedMethods.CheckIfZuneSoftwareIsRunning())
            //{
            //    Messenger.Default.Send<ErrorMessage,ApplicationViewModel>(new ErrorMessage(ErrorMode.Warning, 
            //        "Any albums you link / delink will not show their changes until the zune software is running."));
            //}
            if (!_dbReader.DoesAlbumExist(selectedAlbum.MediaId))
            {
                Messenger.Default.Send<ErrorMessage,ApplicationViewModel>(new ErrorMessage(ErrorMode.Error,
                    "Could not find album, you may need to refresh the database."));
            }
        }
    }
}