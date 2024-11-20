using System.Windows;
using HandyControl.Controls;

namespace EasySpecCalc
{
    public partial class InputSmilesDialog : HandyControl.Controls.Window
    {
        public InputSmilesDialog()
        {
            InitializeComponent();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (rbSmiles.IsChecked == true)
            {
                Smiles = txtSmiles.Text;
                SkipSmilesGeneration = false;
            }
            else
            {
                SkipSmilesGeneration = true;
            }
            this.DialogResult = true;
        }

        public string Smiles { get; private set; }
        public bool SkipSmilesGeneration { get; private set; }
    }
}