﻿using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Ninject;
using ZuneSocialTagger.Core.ZuneDatabase;
using GalaSoft.MvvmLight.Threading;
using ZuneSocialTagger.GUI.Controls;
using ZuneSocialTagger.GUI.Properties;
using ZuneSocialTagger.GUI.ViewsViewModels.Application;
using ZuneSocialTagger.GUI.ViewsViewModels.Details;
using ZuneSocialTagger.GUI.ViewsViewModels.Search;
using ZuneSocialTagger.GUI.ViewsViewModels.WebAlbumList;
using ZuneSocialTagger.Core.IO;
using ZuneSocialTagger.GUI.ViewsViewModels.SelectAudioFiles;
using ZuneSocialTagger.GUI.Shared;
using ZuneSocialTagger.GUI.Models;
using System.Windows.Controls;

namespace ZuneSocialTagger.GUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private static ApplicationView _appView;

        public App()
        {
            WpfSingleInstanceByEventWaitHandle.WpfSingleInstance.Make();
            DispatcherHelper.Initialize();
            this.Startup += App_Startup;
        }

        void App_Startup(object sender, StartupEventArgs e)
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            //improved perceived application startup by allowing the main
            //application view to load before anything else, be it code, dll's etc.
            _appView = new ApplicationView();
            _appView.ContentRendered += new EventHandler(_appView_ContentRendered);
            _appView.Show();
            
        }

        void _appView_ContentRendered(object sender, EventArgs e)
        {
            Settings.Default.AppDataFolder = GetUserDataPath();

            var container = new StandardKernel();
            SetupBindings(container);

            var appViewModel = container.Get<ApplicationViewModel>();
            _appView.DataContext = appViewModel;
            Current.Exit += delegate { appViewModel.ApplicationIsShuttingDown(); };

            appViewModel.ViewHasLoaded();
        }

        private static void SetupBindings(StandardKernel container)
        {
#if DEBUG
            container.Bind<IZuneDatabaseReader>().To<TestZuneDatabaseReader>().InSingletonScope();
#else
            container.Bind<IZuneDatabaseReader>().To<ZuneDatabaseReader>().InSingletonScope();
#endif
            //Container.Bind<IApplicationViewModel>().To<ApplicationViewModel>();
            container.Bind<ViewLocator>().ToSelf().InSingletonScope();

            //we need the web view model to be a singleton because we want to be able to continue
            //downloading data while linking etc
            container.Bind<SharedModel>().ToSelf().InSingletonScope();
            container.Bind<SelectAudioFilesViewModel>().ToSelf();
            container.Bind<WebAlbumListViewModel>().ToSelf().InSingletonScope();
            container.Bind<SearchViewModel>().ToSelf().InSingletonScope();
            container.Bind<DetailsViewModel>().ToSelf();
            container.Bind<SafeObservableCollection<AlbumDetailsViewModel>>().ToSelf().InSingletonScope();
            container.Bind<ApplicationViewModel>().ToSelf().InSingletonScope();

            //set some views to remember their state (we need to do this so we can remember the selected row etc)
            container.Bind<WebAlbumListView>().ToSelf().InSingletonScope();
        }

        private static string GetUserDataPath()
        {
            string pathToZuneSocAppDataFolder = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), "Zune Social Tagger");

            if (!Directory.Exists(pathToZuneSocAppDataFolder))
                Directory.CreateDirectory(pathToZuneSocAppDataFolder);

            return pathToZuneSocAppDataFolder;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            DisplayException(e.Exception);
            e.Handled = true;
        }

        public static void DisplayException(Exception ex) 
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() 
                => { ErrorReportDialog.Show(ExceptionLogger.LogException(ex), null); });
        }
    }
}
