using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading.Tasks;
using Npgsql;
using Suivi_Elec_Maison.Database;

namespace Suivi_Elec_Maison
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            // Liaison du bouton d'ouverture de la configuration
            BtnOpenConfig.Click += (s, e) =>
            {
                var cfg = new ConfigWindow();
                cfg.Owner = this;
                cfg.ShowDialog();
            };
            // Ouvrir la fenêtre des mesures
            BtnOpenMeasures.Click += (s, e) =>
            {
                var mw = new MeasurementsWindow();
                mw.Owner = this;
                mw.Show();
            };
        }

        private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            // Tentative de connexion au serveur Postgre distant au démarrage.
            try
            {
                using var conn = await DatabaseHelper.GetOpenConnectionAsync();
                //MessageBox.Show("Connexion à la base réussie.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                await conn.CloseAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible de se connecter au serveur PostgreSQL:\n{ex.Message}", "Erreur de connexion", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOpenMeasures_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}