using Suivi_Elec_Maison.Database;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static Suivi_Elec_Maison.Database.DatabaseHelper;

namespace Suivi_Elec_Maison
{
    public partial class AddMeasureWindow : Window
    {
        private int? _idPrixElec = null;

        public AddMeasureWindow()
        {
            InitializeComponent();
            //DpJour.SelectedDate = DateTime.Today;
            var ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            ci.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd";
            ci.DateTimeFormat.DateSeparator = "-";
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;
        }

        private async void DpJour_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DpJour.SelectedDate is not DateTime date)
            {
                _idPrixElec = null;
                TxtIdPrixElec.Text = string.Empty;
                TxtPrixElecInfo.Text = string.Empty;
                return;
            }

            try
            {
                PrixElecResult result = await GetIdPrixElecForDateAsync(date);
                if (result != null)
                {
                    _idPrixElec = result.Id;
                    TxtIdPrixElec.Text = result.Id.ToString();
                    //TxtPrixElecInfo.Text = result.Label;
                    TxtPrixElecInfo.Foreground = System.Windows.Media.Brushes.Gray;
                }
                else
                {
                    _idPrixElec = null;
                    TxtIdPrixElec.Text = string.Empty;
                    TxtPrixElecInfo.Text = "Aucun tarif trouvé pour cette date";
                    TxtPrixElecInfo.Foreground = System.Windows.Media.Brushes.OrangeRed;
                }
            }
            catch (Exception ex)
            {
                _idPrixElec = null;
                TxtIdPrixElec.Text = string.Empty;
                TxtPrixElecInfo.Text = $"Erreur : {ex.Message}";
                TxtPrixElecInfo.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (DpJour.SelectedDate is not DateTime jour)
            {
                MessageBox.Show("Veuillez saisir une date.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_idPrixElec is null)
            {
                var res = MessageBox.Show(
                    "Aucun tarif électricité trouvé pour cette date. Continuer quand même ?",
                    "Tarif manquant", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.No) return;
            }

            if (!TryParseDecimal(TxtProduction.Text, out decimal production))
            { ShowFieldError("Production"); return; }

            if (!TryParseDecimal(TxtStockage.Text, out decimal stockage))
            { ShowFieldError("Stockage"); return; }

            if (!TryParseDecimal(TxtAutoconsommation.Text, out decimal autoconso))
            { ShowFieldError("Autoconsommation"); return; }

            if (!TryParseDecimal(TxtConsoBatterie.Text, out decimal consoBatterie))
            { ShowFieldError("Conso Batterie"); return; }

            if (!TryParseDecimal(TxtConsoReseau.Text, out decimal consoReseau))
            { ShowFieldError("Conso Réseau"); return; }

            if (!TryParseDecimal(TxtConsoTotale.Text, out decimal consoTotale))
            { ShowFieldError("Conso Totale"); return; }

            try
            {
                BtnSave.IsEnabled = false;
                await DatabaseHelper.InsertMesureAsync(
                    jour, _idPrixElec,
                    production, stockage, autoconso,
                    consoBatterie, consoReseau, consoTotale);

                MessageBox.Show("Mesure enregistrée avec succès.", "Succès",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de l'enregistrement : {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSave.IsEnabled = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Accepte virgule ou point comme séparateur décimal
        private static bool TryParseDecimal(string input, out decimal result) =>
            decimal.TryParse(input.Replace(",", "."), NumberStyles.Any,
                CultureInfo.InvariantCulture, out result);

        private static void ShowFieldError(string fieldName) =>
            MessageBox.Show($"Valeur invalide pour le champ \"{fieldName}\".", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}