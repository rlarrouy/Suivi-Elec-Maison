using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Suivi_Elec_Maison
{
    public partial class MainWindow : Window
    {
        // Dictionnaire tag → bouton nav pour gérer l'état actif
        private Dictionary<string, Button> _navButtons = [];

        // Vues instanciées en lazy (une seule instance par vue)
        private MeasurementsView? _viewMesures;
        private AddMeasureView? _viewSaisie;
        private PrixElecView? _viewPrixElec;
        private ComparisonView? _viewComparaison;
        private ConfigView? _viewConfig;
        private DashboardView? _viewDashboard;

        public MainWindow()
        {
            InitializeComponent();

            _navButtons = new()
            {
                { "Dashboard",   BtnNavDashboard   },
                { "Mesures",     BtnNavMesures     },
                { "Comparaison", BtnNavComparaison },
                { "Saisie",      BtnNavSaisie      },
                { "PrixElec",    BtnNavPrixElec    },
                { "Config",      BtnNavConfig      },
            };

            // Afficher le tableau de bord au démarrage
            NavigateTo("Dashboard");
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
                NavigateTo(tag);
        }

        private void NavigateTo(string tag)
        {
            // Mise à jour des styles nav
            foreach (var (key, btn) in _navButtons)
                btn.Style = key == tag
                    ? (Style)FindResource("NavItemActive")
                    : (Style)FindResource("NavItem");

            // Changement du titre topbar
            TxtPageTitle.Text = tag switch
            {
                "Dashboard" => "Tableau de bord",
                "Mesures" => "Mesures",
                "Comparaison" => "Comparaison de périodes",
                "Saisie" => "Saisir une mesure",
                "PrixElec" => "Prix électricité",
                "Config" => "Configuration",
                _ => tag
            };

            // Chargement lazy de la vue
            MainContent.Content = tag switch
            {
                "Dashboard" => _viewDashboard ??= new DashboardView(),
                "Mesures" => _viewMesures ??= new MeasurementsView(),
                "Comparaison" => _viewComparaison ??= new ComparisonView(),
                "Saisie" => _viewSaisie ??= new AddMeasureView(),
                "PrixElec" => _viewPrixElec ??= new PrixElecView(),
                "Config" => _viewConfig ??= new ConfigView(),
                _ => null
            };
        }
    }
}