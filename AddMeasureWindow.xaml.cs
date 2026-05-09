using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Suivi_Elec_Maison.Database;

namespace Suivi_Elec_Maison
{
    public partial class AddMeasureWindow : Window
    {
        private int? _idPrixElec = null;
        private int? _idMesure = null;   // null = INSERT, valeur = UPDATE
        private bool _isEditMode = false;

        public AddMeasureWindow()
        {
            InitializeComponent();
            // Le bouton Enregistrer est désactivé tant qu'on n'a pas cliqué Rechercher
            BtnSave.IsEnabled = false;
        }

        // ── Recherche ──────────────────────────────────────────────────────────
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
                // 1. Résolution du tarif Prix_Elec
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
                    TxtPrixElecInfo.Text = "Aucun tarif trouvé pour cette date";
                    TxtPrixElecInfo.Foreground = Brushes.OrangeRed;
                }

                // 2. Vérification doublon
                var existing = await DatabaseHelper.GetMesureByDateAsync(date);
                if (existing is not null)
                {
                    // Mode modification
                    _isEditMode = true;
                    _idMesure = existing.IdMesure;
                    TxtIdMesure.Text = existing.IdMesure.ToString();

                    // Pré-remplissage des champs
                    TxtProduction.Text = existing.Production.ToString(CultureInfo.InvariantCulture);
                    TxtStockage.Text = existing.Stockage.ToString(CultureInfo.InvariantCulture);
                    TxtAutoconsommation.Text = existing.Autoconsommation.ToString(CultureInfo.InvariantCulture);
                    TxtConsoBatterie.Text = existing.ConsoBatterie.ToString(CultureInfo.InvariantCulture);
                    TxtConsoReseau.Text = existing.ConsoReseau.ToString(CultureInfo.InvariantCulture);
                    TxtConsoTotale.Text = existing.ConsoTotale.ToString(CultureInfo.InvariantCulture);

                    // Alerte + onglet
                    ShowAlert($"Une mesure existe déjà pour le {date:dd/MM/yyyy} (ID {existing.IdMesure}) — mode modification activé.");
                    TabEdit.IsEnabled = true;
                    TabMain.SelectedItem = TabEdit;
                    BtnSave.Content = "Mettre à jour";
                }
                else
                {
                    // Mode création
                    _isEditMode = false;
                    _idMesure = null;
                    HideAlert();
                    TabEdit.IsEnabled = false;
                    TabMain.SelectedItem = TabNew;
                    BtnSave.Content = "Enregistrer";
                }

                BtnSave.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la recherche : {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSearch.IsEnabled = true;
            }
        }

        // ── Enregistrement / Mise à jour ───────────────────────────────────────
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

            if (!TryParseDecimal(TxtProduction.Text, out decimal production)) { ShowFieldError("Production"); return; }
            if (!TryParseDecimal(TxtStockage.Text, out decimal stockage)) { ShowFieldError("Stockage"); return; }
            if (!TryParseDecimal(TxtAutoconsommation.Text, out decimal autoconso)) { ShowFieldError("Autoconsommation"); return; }
            if (!TryParseDecimal(TxtConsoBatterie.Text, out decimal consoBatterie)) { ShowFieldError("Conso Batterie"); return; }
            if (!TryParseDecimal(TxtConsoReseau.Text, out decimal consoReseau)) { ShowFieldError("Conso Réseau"); return; }
            if (!TryParseDecimal(TxtConsoTotale.Text, out decimal consoTotale)) { ShowFieldError("Conso Totale"); return; }

            try
            {
                BtnSave.IsEnabled = false;

                if (_isEditMode && _idMesure.HasValue)
                {
                    await DatabaseHelper.UpdateMesureAsync(
                        _idMesure.Value, jour, _idPrixElec,
                        production, stockage, autoconso,
                        consoBatterie, consoReseau, consoTotale);

                    MessageBox.Show("Mesure mise à jour avec succès.", "Succès",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    await DatabaseHelper.InsertMesureAsync(
                        jour, _idPrixElec,
                        production, stockage, autoconso,
                        consoBatterie, consoReseau, consoTotale);

                    MessageBox.Show("Mesure enregistrée avec succès.", "Succès",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

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

        // Empêche de changer d'onglet manuellement si TabEdit est désactivé
        private void TabMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabMain.SelectedItem == TabEdit && !TabEdit.IsEnabled)
                TabMain.SelectedItem = TabNew;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
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
            _idPrixElec = null;
            _idMesure = null;
            _isEditMode = false;
        }

        private void ShowAlert(string message)
        {
            TxtAlert.Text = message;
            PanelAlert.Visibility = Visibility.Visible;
        }

        private void HideAlert() =>
            PanelAlert.Visibility = Visibility.Collapsed;

        private static bool TryParseDecimal(string input, out decimal result) =>
            decimal.TryParse(input.Replace(",", "."), NumberStyles.Any,
                CultureInfo.InvariantCulture, out result);

        private static void ShowFieldError(string fieldName) =>
            MessageBox.Show($"Valeur invalide pour le champ \"{fieldName}\".", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}