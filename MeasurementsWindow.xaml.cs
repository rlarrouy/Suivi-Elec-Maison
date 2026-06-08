using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Suivi_Elec_Maison.Database;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static Suivi_Elec_Maison.Database.DatabaseHelper;

namespace Suivi_Elec_Maison
{
    public partial class MeasurementsWindow : Window, System.ComponentModel.INotifyPropertyChanged
    {
        private DataTable? _currentData;
        private PrixElecTarif? _currentTarif;
        private bool _detailExpanded = false;

        // ── Propriétés binding graphiques ────────────────────────────────────
        private IEnumerable<ISeries> _lineSeries = [];
        private IEnumerable<ISeries> _pieSeries = [];
        private IEnumerable<Axis> _xAxes = [new Axis()];
        private IEnumerable<Axis> _yAxes = [new Axis()];

        public IEnumerable<ISeries> LineSeries
        {
            get => _lineSeries;
            set { _lineSeries = value; OnPropertyChanged(nameof(LineSeries)); }
        }
        public IEnumerable<ISeries> PieSeries
        {
            get => _pieSeries;
            set { _pieSeries = value; OnPropertyChanged(nameof(PieSeries)); }
        }
        public IEnumerable<Axis> XAxes
        {
            get => _xAxes;
            set { _xAxes = value; OnPropertyChanged(nameof(XAxes)); }
        }
        public IEnumerable<Axis> YAxes
        {
            get => _yAxes;
            set { _yAxes = value; OnPropertyChanged(nameof(YAxes)); }
        }

        public MeasurementsWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += async (s, e) => await LoadMeasuresAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
            => await LoadMeasuresAsync();

        // ── Chargement ───────────────────────────────────────────────────────
        private async Task LoadMeasuresAsync()
        {
            try
            {
                BtnRefresh.IsEnabled = false;
                var dt = await DatabaseHelper.GetMeasuresAsync(5000);

                // Formatage Jour → yyyy/MM/dd
                var jourCol = dt.Columns.Cast<DataColumn>()
                    .FirstOrDefault(c => string.Equals(c.ColumnName, "Jour",
                        StringComparison.OrdinalIgnoreCase))?.ColumnName;

                if (!string.IsNullOrEmpty(jourCol))
                {
                    const string tmp = "Jour_tmp";
                    dt.Columns.Add(tmp, typeof(string));
                    foreach (DataRow r in dt.Rows)
                    {
                        var val = r[jourCol];
                        if (val == DBNull.Value) { r[tmp] = ""; continue; }
                        r[tmp] = val is DateTime dv ? dv.ToString("yyyy/MM/dd")
                            : DateTime.TryParse(val.ToString(), out var p)
                                ? p.ToString("yyyy/MM/dd") : val.ToString();
                    }
                    dt.Columns.Remove(jourCol);
                    dt.Columns[tmp].ColumnName = "Jour";
                }

                // Tri ascendant par Jour
                dt.DefaultView.Sort = "Jour ASC";
                _currentData = dt.DefaultView.ToTable();

                // Remplissage CbYear
                CbYear.SelectionChanged -= CbYear_SelectionChanged;
                CbYear.Items.Clear();
                CbYear.Items.Add("Tous");
                var years = new SortedSet<string>();
                foreach (DataRow r in _currentData.Rows)
                {
                    var v = r["Jour"]?.ToString();
                    if (!string.IsNullOrEmpty(v) && v.Length >= 4) years.Add(v[..4]);
                }
                foreach (var y in years) CbYear.Items.Add(y);
                CbYear.SelectedIndex = 0;
                CbYear.SelectionChanged += CbYear_SelectionChanged;

                RefreshMonthCombo(null);

                var dvInit = BuildDataView("Tous", "Tous");
                await RefreshAllAsync(dvInit, "Tous", "Tous");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur : {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { BtnRefresh.IsEnabled = true; }
        }

        // ── Filtres ──────────────────────────────────────────────────────────
        private async void CbYear_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_currentData == null) return;
            var selYear = CbYear.SelectedItem as string ?? "Tous";
            CbMonth.SelectionChanged -= CbMonth_SelectionChanged;
            RefreshMonthCombo(selYear == "Tous" ? null : selYear);
            CbMonth.SelectionChanged += CbMonth_SelectionChanged;
            var selMonth = CbMonth.SelectedItem as string ?? "Tous";
            var dv = BuildDataView(selYear, selMonth);
            await RefreshAllAsync(dv, selYear, selMonth);
        }

        private async void CbMonth_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_currentData == null) return;
            var selYear = CbYear.SelectedItem as string ?? "Tous";
            var selMonth = CbMonth.SelectedItem as string ?? "Tous";
            var dv = BuildDataView(selYear, selMonth);
            await RefreshAllAsync(dv, selYear, selMonth);
        }

        private void RefreshMonthCombo(string? filterYear)
        {
            CbMonth.Items.Clear();
            CbMonth.Items.Add("Tous");
            var months = new SortedSet<string>();
            foreach (DataRow r in _currentData!.Rows)
            {
                var v = r["Jour"]?.ToString();
                if (string.IsNullOrEmpty(v) || v.Length < 7) continue;
                if (filterYear != null && !v.StartsWith(filterYear)) continue;
                months.Add(v[..7]);
            }
            foreach (var m in months) CbMonth.Items.Add(m);
            CbMonth.SelectedIndex = 0;
        }

        private DataView BuildDataView(string selYear, string selMonth)
        {
            try
            {
                if (selMonth != "Tous")
                    return new DataView(_currentData!) { RowFilter = $"Jour LIKE '{selMonth}%'" };
                if (selYear != "Tous")
                    return new DataView(_currentData!) { RowFilter = $"Jour LIKE '{selYear}%'" };
                var dv = _currentData!.DefaultView;
                dv.RowFilter = string.Empty;
                return dv;
            }
            catch { return _currentData!.DefaultView; }
        }

        // ── Rafraîchissement global ──────────────────────────────────────────
        private async Task RefreshAllAsync(DataView dv, string selYear, string selMonth)
        {
            try
            {
                if (selMonth != "Tous" && selMonth.Length == 7
                    && int.TryParse(selMonth[..4], out int y)
                    && int.TryParse(selMonth[5..], out int m))
                    _currentTarif = await DatabaseHelper.GetTarifForMonthAsync(y, m);
                else if (selYear != "Tous" && int.TryParse(selYear, out int yr))
                    _currentTarif = await DatabaseHelper.GetTarifForYearAsync(yr);
                else
                    _currentTarif = await DatabaseHelper.GetLatestTarifAsync();
            }
            catch { _currentTarif = null; }

            RefreshMetrics(dv);
            RefreshCharts(dv);
            RefreshSynthese(dv);
            RefreshRatios(dv);
            RefreshSimulations(dv);
            RefreshDetailGrid(dv);
        }

        // ── Métriques ────────────────────────────────────────────────────────
        private void RefreshMetrics(DataView dv)
        {
            decimal prod = SumCol(dv, "Production");
            decimal stock = SumCol(dv, "Stockage");
            decimal auto_ = SumCol(dv, "Autoconsommation");
            decimal bat = SumCol(dv, "Conso_Batterie");
            decimal reseau = SumCol(dv, "Conso_Reseau");
            decimal totale = SumCol(dv, "Conso_Totale");
            decimal verte = auto_ + bat;

            TxtMetricProduction.Text = prod.ToString("F0");
            TxtMetricConsoVerte.Text = verte.ToString("F0");
            TxtMetricConsoReseau.Text = reseau.ToString("F0");
            TxtMetricStockage.Text = stock.ToString("F0");
            TxtMetricRatio.Text = totale == 0 ? "–" : (verte / totale * 100).ToString("F0") + "%";
        }

        // ── Graphiques ───────────────────────────────────────────────────────
        private void RefreshCharts(DataView dv)
        {
            // Courbes journalières
            var rows = dv.Cast<DataRowView>().ToList();
            var labels = rows.Select(r => r.Row["Jour"]?.ToString()?[8..] ?? "").ToArray();

            double[] GetSeries(string col) => rows
                .Select(r => r.Row[col] == DBNull.Value ? 0d : Convert.ToDouble(r.Row[col]))
                .ToArray();

            bool Has(string c) => _currentData?.Columns.Contains(c) == true;

            var prod = Has("Production") ? GetSeries("Production") : [];
            var verte = Has("Autoconsommation") && Has("Conso_Batterie")
                ? rows.Select(r =>
                    (r.Row["Autoconsommation"] == DBNull.Value ? 0d : Convert.ToDouble(r.Row["Autoconsommation"])) +
                    (r.Row["Conso_Batterie"] == DBNull.Value ? 0d : Convert.ToDouble(r.Row["Conso_Batterie"]))).ToArray()
                : Array.Empty<double>();
            var reseau = Has("Conso_Reseau") ? GetSeries("Conso_Reseau") : [];
            var stock = Has("Stockage") ? GetSeries("Stockage") : [];

            LineSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = prod, Name = "Production",
                    Stroke = new SolidColorPaint(SKColor.Parse("#1D9E75"), 2),
                    Fill = new SolidColorPaint(SKColor.Parse("#1D9E75").WithAlpha(20)),
                    GeometrySize = 4, GeometryStroke = new SolidColorPaint(SKColor.Parse("#1D9E75"), 1),
                    LineSmoothness = 0.3
                },
                new LineSeries<double>
                {
                    Values = verte, Name = "Conso verte",
                    Stroke = new SolidColorPaint(SKColor.Parse("#378ADD"), 2),
                    Fill = null, GeometrySize = 4,
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#378ADD"), 1),
                    LineSmoothness = 0.3
                },
                new LineSeries<double>
                {
                    Values = reseau, Name = "Conso réseau",
                    Stroke = new SolidColorPaint(SKColor.Parse("#BA7517"), 2),
                    Fill = null, GeometrySize = 4,
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#BA7517"), 1),
                    LineSmoothness = 0.3
                },
                new LineSeries<double>
                {
                    Values = stock, Name = "Stockage",
                    Stroke = new SolidColorPaint(SKColor.Parse("#7F77DD"), 2)
                        { PathEffect = new LiveChartsCore.SkiaSharpView.Painting.Effects.DashEffect([4,3]) },
                    Fill = null, GeometrySize = 4,
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#7F77DD"), 1),
                    LineSmoothness = 0.3
                },
            };

            XAxes = [new Axis { Labels = labels, TextSize = 10,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#999999")) }];
            YAxes = [new Axis { TextSize = 10,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#999999")) }];

            // Donut
            decimal auto_ = SumCol(dv, "Autoconsommation");
            decimal bat = SumCol(dv, "Conso_Batterie");
            decimal reseauD = SumCol(dv, "Conso_Reseau");
            decimal totalD = auto_ + bat + reseauD;

            string Pct(decimal v) => totalD == 0 ? "–" : (v / totalD * 100).ToString("F0") + "%";
            TxtPctAuto.Text = Pct(auto_);
            TxtPctBat.Text = Pct(bat);
            TxtPctReseau.Text = Pct(reseauD);

            PieSeries = new ISeries[]
            {
                new PieSeries<double> { Values = [(double)auto_],   Name = "Autoconso.",
                    Fill = new SolidColorPaint(SKColor.Parse("#1D9E75")), InnerRadius = 0 },
                new PieSeries<double> { Values = [(double)bat],     Name = "Batterie",
                    Fill = new SolidColorPaint(SKColor.Parse("#378ADD")), InnerRadius = 0 },
                new PieSeries<double> { Values = [(double)reseauD], Name = "Réseau",
                    Fill = new SolidColorPaint(SKColor.Parse("#BA7517")), InnerRadius = 0 },
            };

            OnPropertyChanged(nameof(LineSeries));
            OnPropertyChanged(nameof(PieSeries));
            XAxes = [ new Axis { Labels = labels, TextSize = 10,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#999999")) } ];
            YAxes = [ new Axis { TextSize = 10,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#999999")) } ];
        }

        // ── Tableau synthèse ─────────────────────────────────────────────────
        private void RefreshSynthese(DataView dv)
        {
            int count = dv.Count;
            decimal prod = SumCol(dv, "Production");
            decimal stock = SumCol(dv, "Stockage");
            decimal auto_ = SumCol(dv, "Autoconsommation");
            decimal bat = SumCol(dv, "Conso_Batterie");
            decimal reseau = SumCol(dv, "Conso_Reseau");
            decimal totale = SumCol(dv, "Conso_Totale");
            decimal verte = auto_ + bat;

            DataGridSynthese.ItemsSource = new List<SyntheseRow>
            {
                new("Total période", Fmt(prod), Fmt(stock), Fmt(verte), Fmt(reseau), Fmt(totale)),
                new("Moy./jour", FmtMoy(prod,count), FmtMoy(stock,count), FmtMoy(verte,count),
                    FmtMoy(reseau,count), FmtMoy(totale,count)),
            };
        }

        // ── Tableau ratios ───────────────────────────────────────────────────
        private void RefreshRatios(DataView dv)
        {
            decimal prod = SumCol(dv, "Production");
            decimal auto_ = SumCol(dv, "Autoconsommation");
            decimal bat = SumCol(dv, "Conso_Batterie");
            decimal totale = SumCol(dv, "Conso_Totale");
            decimal verte = auto_ + bat;

            DataGridRatios.ItemsSource = new List<RatiosRow>
            {
                new("Total période", FmtPct(verte,prod), FmtPct(verte,totale), FmtPct(prod,totale)),
            };
        }

        // ── Tableau simulations ──────────────────────────────────────────────
        private void RefreshSimulations(DataView dv)
        {
            if (_currentTarif is null)
            {
                DataGridSimulations.ItemsSource = new List<SimulationRow>
                    { new("–", "Tarif non disponible", "–", "–") };
                TxtSimSolaire.Text = "–";
                TxtSimAPayer.Text = "–";
                return;
            }

            decimal hp = _currentTarif.HP;
            decimal hc = _currentTarif.HC;
            decimal reseau = SumCol(dv, "Conso_Reseau");
            decimal auto_ = SumCol(dv, "Autoconsommation");
            decimal bat = SumCol(dv, "Conso_Batterie");

            SimulationRow Calc(string lib, decimal kwh, decimal pHP, decimal pHC)
            {
                decimal partHP = kwh * pHP * hp;
                decimal partHC = kwh * pHC * hc;
                return new(lib, FmtEur(partHP), FmtEur(partHC), FmtEur(partHP + partHC));
            }

            var rowPrix = Calc("Simulation Prix", reseau, 0.75m, 0.25m);
            var rowAuto = Calc("Simulation Auto conso", auto_, 1.00m, 0.00m);
            var rowBat = Calc("Simulation Batterie", bat, 0.75m, 0.25m);

            DataGridSimulations.ItemsSource = new List<SimulationRow> { rowPrix, rowAuto, rowBat };

            decimal solaire = (auto_ * 1.00m * hp) + (bat * 0.75m * hp) + (bat * 0.25m * hc);
            decimal aPayer = (reseau * 0.75m * hp) + (reseau * 0.25m * hc);

            TxtSimSolaire.Text = FmtEur(solaire) + " €";
            TxtSimAPayer.Text = FmtEur(aPayer) + " €";
        }

        // ── Tableau de détail ────────────────────────────────────────────────
        private void RefreshDetailGrid(DataView dv)
        {
            DataGridMeasures.ItemsSource = dv;
            TxtRowCount.Text = $"{dv.Count} ligne{(dv.Count > 1 ? "s" : "")}";

            // Masquer colonnes techniques
            DataGridMeasures.AutoGeneratedColumns += (s, e) =>
            {
                foreach (var c in DataGridMeasures.Columns)
                {
                    var h = c.Header?.ToString() ?? "";
                    if (h is "Id_Mesure" or "Id_PrixElec")
                        c.Visibility = Visibility.Collapsed;
                }
            };
        }

        private void BtnToggleDetail_Click(object sender, RoutedEventArgs e)
        {
            _detailExpanded = !_detailExpanded;
            DetailBorder.MaxHeight = _detailExpanded ? double.PositiveInfinity : 200;
            BtnToggleDetail.Content = _detailExpanded ? "▲  Réduire" : "▼  Tout afficher";
        }

        // ── Helpers calcul ───────────────────────────────────────────────────
        private decimal SumCol(DataView dv, string col)
        {
            if (_currentData == null || !_currentData.Columns.Contains(col)) return 0m;
            decimal acc = 0m;
            foreach (DataRowView rv in dv)
                if (rv.Row[col] != DBNull.Value)
                    acc += Convert.ToDecimal(rv.Row[col]);
            return acc;
        }

        private static string Fmt(decimal v) => v.ToString("F2");
        private static string FmtMoy(decimal v, int count) => count > 0 ? (v / count).ToString("F2") : "–";
        private static string FmtPct(decimal n, decimal d) => d == 0 ? "–" : (n / d * 100m).ToString("F1") + " %";
        private static string FmtEur(decimal v) => v.ToString("F2");

        // INotifyPropertyChanged minimal pour LiveCharts
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    internal record SyntheseRow(string Libelle, string Production, string Stockage,
        string ConsoVerte, string ConsoReseau, string ConsoTotale);
    internal record RatiosRow(string Libelle, string RatioVerteProd,
        string RatioVerteConso, string RatioProdConso);
    internal record SimulationRow(string Libelle, string HP, string HC, string Total);
}