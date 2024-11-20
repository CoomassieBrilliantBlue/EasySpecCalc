using System.Windows;
using System.Windows.Controls;

namespace EasySpecCalc
{
    public partial class ORCAOutputWindow : HandyControl.Controls.Window
    {
        public ORCAOutputWindow()
        {
            InitializeComponent();
        }

        public void AppendOutput(string output)
        {
            OutputTextBox.AppendText(output + "\n");
            OutputScrollViewer.ScrollToBottom();
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            OutputTextBox.Clear();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}