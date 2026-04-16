using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using Suivi_Elec_Maison.Database;

namespace Suivi_Elec_Maison
{
    public partial class PrixElecWindow : Window
    {
        public PrixElecWindow()
        {
            InitializeComponent();
            Loaded += PrixElecWindow_Loaded;
            BtnRefresh.Click += async (s, e) => await LoadPrixElecAsync();
        }

        private async void PrixElecWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            await LoadPrixElecAsync();
        }

        private async Task LoadPrixElecAsync()
        {
            try
            {
                BtnRefresh.IsEnabled = false;
                var dt = await DatabaseHelper.GetPrixElecAsync();
                DataGridPrixElec.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible de charger les prix : {ex.Message}",
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnRefresh.IsEnabled = true;
            }
        }
    }
}