using System;
using System.IO;
using System.Windows;
using Npgsql;
using Suivi_Elec_Maison.Database;

namespace Suivi_Elec_Maison
{
    public partial class ConfigWindow : Window
    {
        public ConfigWindow()
        {
            InitializeComponent();
            BtnTest.Click += BtnTest_Click;
            BtnSave.Click += BtnSave_Click;
            Loaded += ConfigWindow_Loaded;
        }

        private void ConfigWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            // Tenter de pré-remplir les champs depuis un fichier de configuration existant
            var plainPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connection.cfg");
            var encPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connection.enc");

            try
            {
                string cs = null;
                if (File.Exists(plainPath))
                {
                    cs = EncryptedConfig.ReadPlainConnectionString(plainPath);
                    RbPlain.IsChecked = true;
                }
                else if (File.Exists(encPath))
                {
                    cs = EncryptedConfig.ReadConnectionString(encPath);
                    RbEncrypted.IsChecked = true;
                }

                if (!string.IsNullOrEmpty(cs))
                {
                    // Parse simple key=value; pairs
                    var parts = cs.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var p in parts)
                    {
                        var kv = p.Split('=', 2);
                        if (kv.Length != 2) continue;
                        var k = kv[0].Trim().ToLowerInvariant();
                        var v = kv[1].Trim();
                        switch (k)
                        {
                            case "host": TxtHost.Text = v; break;
                            case "port": TxtPort.Text = v; break;
                            case "username": TxtUsername.Text = v; break;
                            case "password": TxtPassword.Password = v; break;
                            case "database": TxtDatabase.Text = v; break;
                            case "ssl mode":
                            case "sslmode":
                                // Try to match to one of the ComboBox items
                                try
                                {
                                    foreach (var item in CbSslMode.Items)
                                    {
                                        if (item is System.Windows.Controls.ComboBoxItem cbi && string.Equals((cbi.Content ?? "").ToString(), v, StringComparison.OrdinalIgnoreCase))
                                        {
                                            CbSslMode.SelectedItem = cbi;
                                            break;
                                        }
                                    }
                                }
                                catch { }
                                break;
                            case "trust server certificate":
                            case "trustservercertificate":
                                ChkTrustServerCert.IsChecked = v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1" || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
                                break;
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors lors du chargement initial
            }
        }

        private async void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            var cs = BuildConnectionString();
            try
            {
                using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync();
                MessageBox.Show("Test de connexion réussi.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                await conn.CloseAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Échec du test de connexion : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var cs = BuildConnectionString();
            var defaultEncPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connection.enc");
            var defaultPlainPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connection.cfg");
            try
            {
                if (RbPlain.IsChecked == true)
                {
                    EncryptedConfig.CreatePlainFile(defaultPlainPath, cs);
                    MessageBox.Show($"Fichier en clair enregistré : {defaultPlainPath}", "Enregistré", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    EncryptedConfig.CreateEncryptedFile(defaultEncPath, cs);
                    MessageBox.Show($"Fichier chiffré enregistré : {defaultEncPath}", "Enregistré", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible d'enregistrer le fichier de configuration : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string BuildConnectionString()
        {
            var host = TxtHost.Text.Trim();
            var port = TxtPort.Text.Trim();
            var user = TxtUsername.Text.Trim();
            var pwd = TxtPassword.Password;
            var db = TxtDatabase.Text.Trim();

            if (string.IsNullOrEmpty(host)) host = "localhost";
            if (string.IsNullOrEmpty(port)) port = "5432";

            var sslMode = "Require";
            if (CbSslMode.SelectedItem is System.Windows.Controls.ComboBoxItem sel && sel.Content != null)
            {
                sslMode = sel.Content.ToString() ?? "Require";
            }

            var trust = ChkTrustServerCert.IsChecked == true ? "Trust Server Certificate=true" : "";

            var parts = new System.Collections.Generic.List<string>
            {
                $"Host={host}",
                $"Port={port}",
                $"Username={user}",
                $"Password={pwd}",
                $"Database={db}",
                $"Ssl Mode={sslMode}"
            };
            if (!string.IsNullOrEmpty(trust)) parts.Add(trust);

            return string.Join(";", parts);
        }
    }
}
