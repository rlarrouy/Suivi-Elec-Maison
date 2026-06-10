using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Suivi_Elec_Maison.Database;

namespace Suivi_Elec_Maison
{
    public partial class AddMeasureView : UserControl
    {
        private int? _idPrixElec = null;
        private int? _idMesure = null;
        private bool _isEditMode = false;

        public AddMeasureView()
        {
            InitializeComponent();
            Loaded += AddMeasureView_Loaded;
        }

        private async void AddMeasureView_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                var jour = await DatabaseHelper.GetNearestJourInBaseAsync();
                TxtDernierJour.Text = jour.HasValue
                    ? jour.Value.ToString("dd/MM/yyyy")
                    : "–";
            }
            catch { TxtDernierJour.Text = "–"; }
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            if (DpJour.SelectedDate is not DateTime date)
            {
                MessageBox.Show("Veuillez saisir une date.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnSearch.IsEnabled = false;
            ResetForm();

            try
            {
                var prix = await DatabaseHelper.GetIdPrixElecForDateAsync(date);
                if (prix != null)
                {
                    _idPrixElec = prix.Id;
                    TxtIdPrixElec.Text = prix.Id.ToString();
                    //TxtPrixElecInfo.Text = prix.Label;
                    TxtPrixElecInfo.Foreground = Brushes.Gray;
                }
                else
                {
                    _idPrixElec = null;
                    TxtIdPrixElec.Text = string.Empty;
                    TxtPrixElecInfo.Text = "Aucun tarif trouvé";
                    TxtPrixElecInfo.Foreground = Brushes.OrangeRed;
                }

                var existing = await DatabaseHelper.GetMesureByDateAsync(date);
                if (existing is not null)
                {
                    _isEditMode = true;
                    _idMesure = existing.IdMesure;
                    TxtIdMesure.Text = existing.IdMesure.ToString();

                    TxtProduction.Text = existing.Production.ToString(CultureInfo.InvariantCulture);
                    TxtStockage.Text = existing.Stockage.ToString(CultureInfo.InvariantCulture);
                    TxtAutoconsommation.Text = existing.Autoconsommation.ToString(CultureInfo.InvariantCulture);
                    TxtConsoBatterie.Text = existing.ConsoBatterie.ToString(CultureInfo.InvariantCulture);
                    TxtConsoReseau.Text = existing.ConsoReseau.ToString(CultureInfo.InvariantCulture);
                    TxtConsoTotale.Text = existing.ConsoTotale.ToString(CultureInfo.InvariantCulture);

                    ShowAlert($"Une mesure existe déjà pour le {date:dd/MM/yyyy} (ID {existing.IdMesure}) — mode modification activé.");
                    TabEdit.IsEnabled = true;
                    TabMain.SelectedItem = TabEdit;
                    BtnSave.Content = "Mettre à jour";
                    BtnSaveAndNew.IsEnabled = false;
                }
                else
                {
                    _isEditMode = false;
                    _idMesure = null;
                    HideAlert();
                    TabEdit.IsEnabled = false;
                    TabMain.SelectedItem = TabNew;
                    BtnSave.Content = "Enregistrer";
                    BtnSaveAndNew.IsEnabled = true;
                }

                BtnSave.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur : {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { BtnSearch.IsEnabled = true; }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
            => await SaveAsync(resetAfter: false);

        private async void BtnSaveAndNew_Click(object sender, RoutedEventArgs e)
            => await SaveAsync(resetAfter: true);

        private async Task SaveAsync(bool resetAfter)
        {
            if (DpJour.SelectedDate is not DateTime jour) return;

            if (_idPrixElec is null)
            {
                var res = MessageBox.Show(
                    "Aucun tarif trouvé pour cette date. Continuer ?",
                    "Tarif manquant", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.No) return;
            }

            if (!TryParse(TxtProduction.Text, out decimal production)) { Err("Production"); return; }
            if (!TryParse(TxtStockage.Text, out decimal stockage)) { Err("Stockage"); return; }
            if (!TryParse(TxtAutoconsommation.Text, out decimal autoconso)) { Err("Autoconsommation"); return; }
            if (!TryParse(TxtConsoBatterie.Text, out decimal consoBatterie)) { Err("Conso batterie"); return; }
            if (!TryParse(TxtConsoReseau.Text, out decimal consoReseau)) { Err("Conso réseau"); return; }
            if (!TryParse(TxtConsoTotale.Text, out decimal consoTotale)) { Err("Conso totale"); return; }

            try
            {
                BtnSave.IsEnabled = false;

                if (_isEditMode && _idMesure.HasValue)
                    await DatabaseHelper.UpdateMesureAsync(
                        _idMesure.Value, jour, _idPrixElec,
                        production, stockage, autoconso,
                        consoBatterie, consoReseau, consoTotale);
                else
                    await DatabaseHelper.InsertMesureAsync(
                        jour, _idPrixElec,
                        production, stockage, autoconso,
                        consoBatterie, consoReseau, consoTotale);

                if (resetAfter)
                    FullReset();
                else
                    MessageBox.Show(
                        _isEditMode ? "Mesure mise à jour." : "Mesure enregistrée.",
                        "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur : {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { BtnSave.IsEnabled = true; }
        }

        private void TabMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabMain.SelectedItem == TabEdit && !TabEdit.IsEnabled)
                TabMain.SelectedItem = TabNew;
        }

        private void ResetForm()
        {
            TxtIdPrixElec.Text = string.Empty;
            TxtPrixElecInfo.Text = string.Empty;
            TxtProduction.Text = string.Empty;
            TxtStockage.Text = string.Empty;
            TxtAutoconsommation.Text = string.Empty;
            TxtConsoBatterie.Text = string.Empty;
            TxtConsoReseau.Text = string.Empty;
            TxtConsoTotale.Text = string.Empty;
            TxtIdMesure.Text = string.Empty;
            BtnSave.IsEnabled = false;
            BtnSaveAndNew.IsEnabled = false;
            _idPrixElec = null;
            _idMesure = null;
            _isEditMode = false;
        }

        private void FullReset()
        {
            ResetForm();
            HideAlert();
            TabEdit.IsEnabled = false;
            TabMain.SelectedItem = TabNew;
            DpJour.SelectedDate = DateTime.Today;
            BtnSave.Content = "Enregistrer";
        }

        private void ShowAlert(string msg)
        {
            TxtAlert.Text = msg;
            PanelAlert.Visibility = Visibility.Visible;
        }

        private void HideAlert() =>
            PanelAlert.Visibility = Visibility.Collapsed;

        private static bool TryParse(string s, out decimal r) =>
            decimal.TryParse(s.Replace(",", "."), NumberStyles.Any,
                CultureInfo.InvariantCulture, out r);

        private static void Err(string f) =>
            MessageBox.Show($"Valeur invalide : {f}", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}