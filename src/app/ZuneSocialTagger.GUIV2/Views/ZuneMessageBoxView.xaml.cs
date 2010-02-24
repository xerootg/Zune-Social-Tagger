using System;
using System.Windows;
using System.Windows.Input;

namespace ZuneSocialTagger.GUIV2.Views
{
    /// <summary>
    /// Interaction logic for ZuneMessageBoxView.xaml
    /// </summary>
    public partial class ZuneMessageBoxView : DraggableWindow
    {
        private readonly string _errorMessage;
        private readonly ErrorMode _mode;

        public ZuneMessageBoxView(string errorMessage, ErrorMode mode)
        {
            InitializeComponent();

            _errorMessage = errorMessage;
            _mode = mode;
            this.DataContext = this;
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
        }

        public string MessageTitle
        {
            get
            {
                switch (_mode)
                {
                    case ErrorMode.Error:
                        return "ERROR";
                    case ErrorMode.Warning:
                        return "WARNING";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public string ImageSourceUrl
        {
            get
            {
                switch (_mode)
                {
                    case ErrorMode.Error:
                        return "pack://application:,,,/Assets/error.png";
                    case ErrorMode.Warning:
                        return "pack://application:,,,/Assets/alert.png";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public enum ErrorMode
    {
        Error,
        Warning,
    }
}
