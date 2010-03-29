using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ZuneSocialTagger.GUIV2.Models;

namespace ZuneSocialTagger.GUIV2.ViewModels
{
    public class SearchViewModel : ViewModelBase
    {
        public SearchViewModel(IZuneWizardModel model, SearchHeaderViewModel searchHeaderViewModel)
        {
            this.SearchHeader = searchHeaderViewModel;
            model.FoundAlbums = this.SearchHeader.SearchBar.SearchResults;

            this.SearchHeader.AlbumDetails = model.SelectedAlbum.ZuneAlbumMetaData;
            this.SearchHeader.SearchBar.SearchText = model.SearchText;

            //when the user starts searching we want to move to the next page immediately
            this.SearchHeader.SearchBar.StartedSearching +=
                () => Messenger.Default.Send(typeof (SearchResultsViewModel));

            this.MoveBackCommand = new RelayCommand(MoveBack);
            this.SearchCommand = new RelayCommand(Search);
        }

        public RelayCommand MoveBackCommand { get; set; }
        public RelayCommand SearchCommand { get; private set; }
        public SearchHeaderViewModel SearchHeader { get; set; }

        public void Search()
        {
            this.SearchHeader.SearchBar.Search();
        }

        public void MoveBack()
        {
            Messenger.Default.Send(typeof(IFirstPage));
        }
    }
}