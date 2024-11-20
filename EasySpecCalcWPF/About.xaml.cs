using System.Windows;
using System.Diagnostics;
using HandyControl.Controls;

namespace EasySpecCalc
{
    public partial class AboutDialog : HandyControl.Controls.Window
    {
        public AboutDialog()
        {
            InitializeComponent();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}