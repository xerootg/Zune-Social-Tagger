using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using leetreveil.AutoUpdate.Framework;
using ZuneSocialTagger.Core.ZuneDatabase;
using ZuneSocialTagger.Core.ZuneWebsite;
using ZuneSocialTagger.GUI.Models;
using ZuneSocialTagger.GUI.Properties;
using System.Diagnostics;
using ZuneSocialTagger.GUI.ViewsViewModels.About;
using ZuneSocialTagger.GUI.ViewsViewModels.SelectAudioFiles;
using ZuneSocialTagger.GUI.Shared;
using ZuneSocialTagger.GUI.ViewsViewModels.WebAlbumList;
using ProtoBuf;
using System.Windows.Input;
using SortOrder = ZuneSocialTagger.GUI.ViewsViewModels.WebAlbumList.SortOrder;
using ZuneSocialTagger.Core;
using Ninject;

namespace ZuneSocialTagger.GUI.ViewsViewModels.Application
{

    public class ApplicationViewModel : ViewModelBase
    {
        private readonly IZuneDatabaseReader _dbReader;
        private readonly SafeObservableCollection<AlbumDetailsViewModel> _albums;
        private readonly ViewLocator _viewLocator;
        private readonly IKernel _kernel;
        private WebAlbumListViewModel _webAlbumListViewModel;
        private List<MinCache> _cache;

        public ApplicationViewModel(IZuneDatabaseReader dbReader,
                                    SafeObservableCollection<AlbumDetailsViewModel> albums,
                                    ViewLocator locator,
                                    IKernel kernel,
                                    ApplicationView av)
        {
            _dbReader = dbReader;
            _albums = albums;
            _viewLocator = locator;
            _kernel = kernel;

            //register for notification messages
            Messenger.Default.Register<ErrorMessage>(this, Notifications.Add);
            Messenger.Default.Register<UserControl>(this, (view) => { CurrentPage = view; });
        }

        public void ViewHasLoaded()
        {
            new Thread(() =>
            {
                try
                {
                    ReadCache();
                    CheckForUpdates();
                    CheckLocale();
                    Initialize();
                }
                catch (Exception ex)
                {
                    App.DisplayException(ex);
                }

            }).Start();
        }

        #region View Bindings

        private ICommand _showAboutSettingsCommand;
        public ICommand ShowAboutSettingsCommand
        {
            get
            {
                if (_showAboutSettingsCommand == null)
                    _showAboutSettingsCommand = new RelayCommand(ShowAboutSettings);

                return _showAboutSettingsCommand;
            }
        }

        private ICommand _closeAppCommand;
        public ICommand CloseAppCommand
        {
            get
            {
                if (_closeAppCommand == null)
                    _closeAppCommand = new RelayCommand(CloseApp);

                return _closeAppCommand;
            }
        }

        private SafeObservableCollection<ErrorMessage> _notifications;
        public SafeObservableCollection<ErrorMessage> Notifications 
        {
            get 
            {
                if (_notifications == null) {
                    _notifications = new SafeObservableCollection<ErrorMessage>();
                    RaisePropertyChanged(() => Notifications);
                }
                return _notifications; 
            }
        }

        private UserControl _currentPage;
        public UserControl CurrentPage
        {
            get { return _currentPage; }
            private set
            {
                _currentPage = value;
                RaisePropertyChanged(() => CurrentPage);
            }
        }

        #endregion

        private void Initialize()
        {
            if (InitializeDatabase())
            {
                DispatcherHelper.UIDispatcher.Invoke(new Action(() =>
                {
                    _webAlbumListViewModel = _viewLocator.SwitchToView<WebAlbumListView, WebAlbumListViewModel>();
                }), null);
                
                ReadActualDatabase();
            }
            //if we cannot interop with the zune database switch to the old view
            else
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    var selectAudioFilesViewModel = _viewLocator.SwitchToView<SelectAudioFilesView, SelectAudioFilesViewModel>();
                    selectAudioFilesViewModel.CanSwitchToNewMode = false;
                });
            }
        }

        public void AlbumBeenLinked()
        {
            if (CurrentPage.GetType() == typeof(WebAlbumListViewModel))
            {
                if (!Helpers.CheckIfZuneSoftwareIsRunning())
                {
                    Notifications.Add(new ErrorMessage(ErrorMode.Warning,
                                                    "Any albums you link / delink will not show their changes until the zune software is running."));
                }
                else
                {
                    _webAlbumListViewModel.SelectedAlbum.RefreshAlbum();
                }
            }
        }

        private bool InitializeDatabase()
        {
            try
            {
                bool initalized = _dbReader.Initialize();

                if (!initalized)
                {
                    Notifications.Add(new ErrorMessage(ErrorMode.Error, "Error loading Zune database"));
                    return false;
                }

                return true;
            }
            catch (FileNotFoundException ex)
            {
                Notifications.Add(new ErrorMessage(ErrorMode.Error, "Error loading Zune database. Are you sure Zune Social Tagger is installed in the Zune application folder?"));
                return false;
            }
            catch (Exception e)
            {
                Notifications.Add(new ErrorMessage(ErrorMode.Error, "Error loading Zune database"));
                return false;
            }
        }

        private  void ReadCache()
        {
            var filePath = Path.Combine(AppSettings.AppDataFolder, @"zunesoccache233.dat");
            if (File.Exists(filePath))
            {
                using (var file = File.Open(filePath, FileMode.Open))
                {
                    _cache = Serializer.Deserialize<List<MinCache>>(file);
                }
            }
        }

        private void WriteCache()
        {
            using (var file = File.Create(Path.Combine(AppSettings.AppDataFolder, @"zunesoccache233.dat")))
            {
                Serializer.Serialize(file, _albums.Select(x=> new MinCache{AlbumMediaId = x.AlbumMediaId, LinkStatus = x.LinkStatus, Right = x.Right}).ToList());
            }
        }

        public void ReadActualDatabase()
        {
            Core.ZuneDatabase.SortOrder so;
            switch (Settings.Default.SortOrder)
            {
                case SortOrder.DateAdded:
                    so = Core.ZuneDatabase.SortOrder.DateAdded;
                    break;
                case SortOrder.Album:
                    so = Core.ZuneDatabase.SortOrder.Album;
                    break;
                case SortOrder.Artist:
                    so = Core.ZuneDatabase.SortOrder.Artist;
                    break;
                default:
                    so = Core.ZuneDatabase.SortOrder.DateAdded;
                    break;
            }

            _webAlbumListViewModel.Sort();

            foreach (DbAlbum newAlbum in _dbReader.ReadAlbums(so))
            {
                var newalbumDetails = _kernel.Get<AlbumDetailsViewModel>();

                newalbumDetails.LinkStatus = newAlbum.AlbumMediaId.GetLinkStatusFromGuid();
                newalbumDetails.DateAdded = newAlbum.DateAdded;

                newalbumDetails.Left = new AlbumThumbDetails
                {
                    Artist = newAlbum.Artist,
                    ArtworkUrl = newAlbum.ArtworkUrl,
                    Title = newAlbum.Title,
                };

                if (newalbumDetails.LinkStatus == LinkStatus.Unknown || newalbumDetails.LinkStatus == LinkStatus.Linked)
                {
                    if (_cache != null)
                    {
                        DbAlbum album = newAlbum;
                        var cachedObjects = _cache.Where(x=> x.AlbumMediaId == album.AlbumMediaId);

                        if (cachedObjects.Count() > 0) 
                        {
                            var co = cachedObjects.First();
                            if (co.Right != null)
                            {
                                newalbumDetails.Right = co.Right;
                                newalbumDetails.LinkStatus = LinkStatus.Linked;
                            }
                            else
                            {
                                newalbumDetails.LinkStatus = co.LinkStatus;
                            }
                        }

                    }
                }

                newalbumDetails.Init(newAlbum.MediaId, newAlbum.AlbumMediaId);
                _albums.Add(newalbumDetails);
            }
        }

        private void CheckLocale()
        {
            string locale = WindowsLocale.GetLocale();

            //default the marketplace culture to the current computers culture.
            //This should be overidden by whats read from the marketplace
            MarketplaceInfo.MarketplaceCulture = locale;
            MarketplaceInfo.IsMusicMarketplaceEnabled = true;

            LocaleDownloader.IsMarketPlaceEnabledForLocaleAsync(locale, details =>
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    if (details != null)
                    {
                        if (details.MarketplaceStatus == MarketplaceStatus.NotAvailable)
                        {
                            MarketplaceInfo.IsMusicMarketplaceEnabled = false;
                            var msg = String.Format("The Zune Marketplace is not yet available in your region ({0}). You" +
                                " may not get any search results.", locale);
                            Notifications.Add(new ErrorMessage(ErrorMode.Info, msg));
                        }
                        if (details.MarketplaceStatus == MarketplaceStatus.Error)
                        {
                            MarketplaceInfo.IsMusicMarketplaceEnabled = false;
                            var msg = String.Format("Error connecting to the ({0}) marketplace.", locale);
                            Notifications.Add(new ErrorMessage(ErrorMode.Info, msg));
                        }
                        if (!String.IsNullOrEmpty(details.MarketplaceLocale))
                        {
                            MarketplaceInfo.MarketplaceCulture = details.MarketplaceLocale;
                        }
                    }
                    else
                    {
                        MarketplaceInfo.IsMusicMarketplaceEnabled = false;
                        var msg = "Unable to connect to marketplace. You may not be able to get any search results.";
                        Notifications.Add(new ErrorMessage(ErrorMode.Info, msg));
                    }
                });
            });
        }

        private static void CloseApp()
        {
            System.Windows.Application.Current.Shutdown();
        }

        public void ApplicationIsShuttingDown()
        {
            WriteCache();
            Settings.Default.Save();
        }

        private void ShowAboutSettings()
        {
            new AboutView { DataContext = _kernel.Get<AboutViewModel>() }.Show();
        }

        private void CheckForUpdates()
        {
            if (Settings.Default.CheckForUpdates)
            {
                //do update checking stuff here
                UpdateManager updateManager = UpdateManager.Instance;
                updateManager.AppFeedUrl = AppSettings.UpdateFeedUrl;

                ThreadPool.QueueUserWorkItem(state => {
                    try
                    {
                        if (updateManager.CheckForUpdate())
                        {
                            var msg = String.Format("A new update ({0}) is available for Zune Social Tagger. |https://github.com/leetreveil/Zune-Social-Tagger/downloads |Click here to download now.", updateManager.NewUpdate.Version);
                            Notifications.Add(new ErrorMessage(ErrorMode.Info, msg));
                        }
                            
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                    }
                });
            }
        }
    }
}