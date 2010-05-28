using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.WindowsAPICodePack.Dialogs;
using ZuneSocialTagger.Core.IO;
using ZuneSocialTagger.GUI.Models;
using ZuneSocialTagger.GUI.ViewsViewModels.Application;
using ZuneSocialTagger.GUI.ViewsViewModels.Details;
using ZuneSocialTagger.GUI.ViewsViewModels.Search;
using ZuneSocialTagger.GUI.ViewsViewModels.Shared;
using ZuneSocialTagger.GUI.ViewsViewModels.WebAlbumList;

namespace ZuneSocialTagger.GUI.ViewsViewModels.SelectAudioFiles
{
    public class SelectAudioFilesViewModel : ViewModelBase
    {
        private readonly IViewModelLocator _locator;
        private readonly ExpandedAlbumDetailsViewModel _albumDetailsFromFile;
        private readonly IZuneAudioFileRetriever _fileRetriever;

        public SelectAudioFilesViewModel(IViewModelLocator locator,
                                         [File]ExpandedAlbumDetailsViewModel albumDetailsFromFile,
                                         IZuneAudioFileRetriever fileRetriever)
        {
            _locator = locator;
            _albumDetailsFromFile = albumDetailsFromFile;
            _fileRetriever = fileRetriever;
            this.CanSwitchToNewMode = true;
            this.SelectFilesCommand = new RelayCommand(SelectFiles);
            this.SwitchToNewModeCommand = new RelayCommand(SwitchToNewMode);    
        }

        public bool CanSwitchToNewMode { get; set; }
        public RelayCommand SelectFilesCommand { get; private set; }
        public RelayCommand SwitchToNewModeCommand { get; private set; }

        public void SwitchToNewMode()
        {
            _locator.SwitchToViewModel<WebAlbumListViewModel>();
        }

        public void SelectFiles()
        {
            if (CommonFileDialog.IsPlatformSupported)
            {
                var commonOpenFileDialog = new CommonOpenFileDialog("Select the audio files that you want to link to the zune social");

                commonOpenFileDialog.Multiselect = true;
                commonOpenFileDialog.EnsureFileExists = true;
                commonOpenFileDialog.Filters.Add(new CommonFileDialogFilter("Audio Files", "*.mp3;*.wma"));
                commonOpenFileDialog.Filters.Add(new CommonFileDialogFilter("MP3 Files", "*.mp3"));
                commonOpenFileDialog.Filters.Add(new CommonFileDialogFilter("WMA Files", "*.wma"));

                if (commonOpenFileDialog.ShowDialog() == CommonFileDialogResult.OK)
                    ReadFiles(commonOpenFileDialog.FileNames);
            }
            else
            {
                var ofd = new OpenFileDialog { Multiselect = true, Filter = "Audio files .mp3,.wma |*.mp3;*.wma" };
                ofd.AutoUpgradeEnabled = true;
                ofd.Title = "Select the audio files that you want to link to the zune social";
                if (ofd.ShowDialog() == DialogResult.OK)
                    ReadFiles(ofd.FileNames);
            }
        }

        private void ReadFiles(IEnumerable<string> files)
        {
            try
            {
                //get the files and sort by trackNumber
                _fileRetriever.GetContainers(files);
                _fileRetriever.SortByTrackNumber();

                //get the first tracks metadata which is used to set some details
                MetaData firstTrackMetaData = _fileRetriever.Containers.First().MetaData;

                //set the album details that is used throughout the app
                SharedMethods.SetAlbumDetails(_albumDetailsFromFile, firstTrackMetaData, _fileRetriever.Containers.Count);

                //as soon as the view has switched start searching
                var searchVm = _locator.SwitchToViewModel<SearchViewModel>();
                searchVm.Search(firstTrackMetaData.AlbumName, firstTrackMetaData.AlbumArtist);
            }
            catch (Exception ex)
            {
                Messenger.Default.Send<ErrorMessage, ApplicationViewModel>(new ErrorMessage(ErrorMode.Error, ex.Message));
                return;  //if we hit an error on any track in the albums then just fail and dont read anymore
            }
        }
    }
}