using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Data;
using Suivi_Elec_Maison.Database;

namespace Suivi_Elec_Maison
{
    public partial class MeasurementsWindow : Window
    {
        public MeasurementsWindow()
        {
            InitializeComponent();
            Loaded += MeasurementsWindow_Loaded;
            BtnRefresh.Click += async (s, e) => await LoadMeasuresAsync();
        }

        private async void MeasurementsWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            await LoadMeasuresAsync();
        }

        private async Task LoadMeasuresAsync()
        {
            try
            {
                BtnRefresh.IsEnabled = false;
                var dt = await DatabaseHelper.GetMeasuresAsync(1000);
                DataGridMeasures.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible de charger les mesures : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnRefresh.IsEnabled = true;
            }
        }
    }
}