using System.Collections.Generic;
using GalaSoft.MvvmLight.Command;
using ZuneSocialTagger.Core;
using ZuneSocialTagger.Core.ZuneWebsite;
using ZuneSocialTagger.GUI.Models;
using ZuneSocialTagger.GUI.Properties;
using System.Linq;
using ZuneSocialTagger.GUI.ViewsViewModels.Search;
using ZuneSocialTagger.GUI.ViewsViewModels.Shared;
using ZuneSocialTagger.Core.IO;
using System;

namespace ZuneSocialTagger.GUI.ViewsViewModels.Details
{
    public class FileAttribute : Attribute{}
    public class WebAttribute : Attribute {}

    public class DetailsViewModel : ViewModelBase
    {
        private readonly IViewModelLocator _locator;
        private readonly IEnumerable<IZuneTagContainer> _fileTracks;
        private readonly WebAlbum _webAlbum;

        public DetailsViewModel(IViewModelLocator locator, 
            [File]ExpandedAlbumDetailsViewModel albumdetailsFromFile,
            [Web]ExpandedAlbumDetailsViewModel albumDetailsFromWeb)
        {
            _locator = locator;
            //_fileTracks = sharedModel.SongsFromFile;
           // _webAlbum = sharedModel.WebAlbum;

            this.AlbumDetailsFromFile = albumdetailsFromFile;
            this.AlbumDetailsFromWebsite = albumDetailsFromWeb;

            this.MoveBackCommand = new RelayCommand(MoveBack);
           // this.SaveCommand = new RelayCommand(Save);

            this.Rows = new List<object>();

            PopulateRows();
        }

        public ExpandedAlbumDetailsViewModel AlbumDetailsFromWebsite { get; set; }
        public ExpandedAlbumDetailsViewModel AlbumDetailsFromFile { get; set; }
        public RelayCommand MoveBackCommand { get; private set; }
        public RelayCommand SaveCommand { get; private set; }
        public List<object> Rows { get; private set; }

        public bool UpdateAlbumInfo
        {
            get { return Settings.Default.UpdateAlbumInfo; }
            set
            {
                if (value != UpdateAlbumInfo)
                {
                    Settings.Default.UpdateAlbumInfo = value;
                }
            }
        }

        public void PopulateRows()
        {
            this.Rows.Clear();

            //get lists of tracks by discNumer
            var tracksByDiscNumber =
                _fileTracks.Select(x => x.MetaData.DiscNumber).Distinct().Select(
                    number => _fileTracks.Where(x => x.MetaData.DiscNumber == number));

            //if the disc count is just one then dont add any headers
            if (tracksByDiscNumber.Count() == 1)
                tracksByDiscNumber.First().ForEach(AddTrackRow);
            else
            {
                foreach (IEnumerable<IZuneTagContainer> discTracks in tracksByDiscNumber)
                {
                    AddHeaderRow(discTracks.First()); //add header for each disc before adding the tracks
                    discTracks.ForEach(AddTrackRow);
                }
            }
        }

        private void AddHeaderRow(IZuneTagContainer track)
        {
            this.Rows.Add(new DiscHeader
            {
                DiscNumber = "Disc " +
                             SharedMethods.DiscNumberConverter(track.MetaData.DiscNumber)
            });
        }

        private void AddTrackRow(IZuneTagContainer track)
        {
            var detailRow = new DetailRow();

            detailRow.SongDetails = new TrackWithTrackNum
            {
                TrackNumber = SharedMethods.TrackNumberConverter(track.MetaData.TrackNumber),
                TrackTitle = track.MetaData.Title
            };

            foreach (WebTrack webTrack in _webAlbum.Tracks)
            {
                detailRow.AvailableZuneTracks.Add(new TrackWithTrackNum
                {
                    TrackNumber = webTrack.TrackNumber,
                    TrackTitle = webTrack.Title
                });
            }

            detailRow.MatchTheSelectedSongToTheAvailableSongs();

            this.Rows.Add(detailRow);
        }

        //private void Save()
        //{
        //    Mouse.OverrideCursor = Cursors.Wait;

        //    var uaeExceptions = new List<UnauthorizedAccessException>();

        //    foreach (var row in _avm.SongsFromFile)
        //    {
        //        try
        //        {
        //            //if (row.SelectedSong != null)
        //            //{
        //            //    var container = row.Container;

        //            //    if (row.SelectedSong.HasAllZuneIds)
        //            //    {
        //            //        container.RemoveZuneAttribute("WM/WMContentID");
        //            //        container.RemoveZuneAttribute("WM/WMCollectionID");
        //            //        container.RemoveZuneAttribute("WM/WMCollectionGroupID");
        //            //        container.RemoveZuneAttribute("ZuneCollectionID");
        //            //        container.RemoveZuneAttribute("WM/UniqueFileIdentifier");

        //            //        container.AddZuneAttribute(new ZuneAttribute(ZuneIds.Album, row.SelectedSong.AlbumMediaId));
        //            //        container.AddZuneAttribute(new ZuneAttribute(ZuneIds.Artist, row.SelectedSong.ArtistMediaId));
        //            //        container.AddZuneAttribute(new ZuneAttribute(ZuneIds.Track, row.SelectedSong.MediaId));

        //            //        //if (Settings.Default.UpdateAlbumInfo)
        //            //        //container.AddMetaData(row.SelectedSong.MetaData);

        //            //        container.WriteToFile(row.FilePath);
        //            //    }

        //            //    //TODO: run a verifier over whats been written to ensure that the tags have actually been written to file
        //            //}
        //        }
        //        catch (UnauthorizedAccessException uae)
        //        {
        //            uaeExceptions.Add(uae);
        //            //TODO: better error handling
        //        }
        //    }

        //    if (uaeExceptions.Count > 0)
        //        //usually occurs when a file is readonly
        //        _avm.DisplayMessage(new ErrorMessage(ErrorMode.Error,
        //                             "One or more files could not be written to. Have you checked the files are not marked read-only?"));

        //    else
        //    {
        //        _locator.SwitchToViewModel<SuccessViewModel>();
        //    }

        //    Mouse.OverrideCursor = null;
        //}

        private void MoveBack()
        {
            _locator.SwitchToViewModel<SearchViewModel>();
        }
    }
}