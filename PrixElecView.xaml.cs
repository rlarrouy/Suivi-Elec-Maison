using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Suivi_Elec_Maison.Database;

namespace Suivi_Elec_Maison
{
    public partial class PrixElecView : UserControl
    {
        public PrixElecView()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadAsync();
            BtnRefresh.Click += async (s, e) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                BtnRefresh.IsEnabled = false;
                var dt = await DatabaseHelper.GetPrixElecAsync();
                DataGridPrixElec.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur : {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { BtnRefresh.IsEnabled = true; }
        }
    }
}