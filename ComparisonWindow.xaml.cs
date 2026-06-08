using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using Suivi_Elec_Maison.Database;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static Suivi_Elec_Maison.Database.DatabaseHelper;

namespace Suivi_Elec_Maison
{
    public partial class ComparisonWindow : Window, System.ComponentModel.INotifyPropertyChanged
    {
        private DataTable? _allData;

        private IEnumerable<ISeries> _lineSeries = [];
        private IEnumerable<Axis> _xAxes = [new Axis()];
        private IEnumerable<Axis> _yAxes = [new Axis()];

        public IEnumerable<ISeries> LineSeries
        {
            get => _lineSeries;
            set { _lineSeries = value; OnPropertyChanged(nameof(LineSeries)); }
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

        public ComparisonWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
            => await LoadDataAsync();

        // ── Chargement ───────────────────────────────────────────────────────
        private async Task LoadDataAsync()
        {
            try
            {
                BtnRefresh.IsEnabled = false;
                var dt = await DatabaseHelper.GetMeasuresAsync(99999);

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

                dt.DefaultView.Sort = "Jour ASC";
                _allData = dt.DefaultView.ToTable();

                // Remplissage des 2 paires de ComboBox
                FillYearCombo(CbYearA);
                FillYearCombo(CbYearB);

                // Désactiver les événements le temps du remplissage initial
                CbYearA.SelectionChanged -= PeriodChanged;
                CbYearB.SelectionChanged -= PeriodChanged;
                CbMonthA.SelectionChanged -= PeriodChanged;
                CbMonthB.SelectionChanged -= PeriodChanged;

                CbYearA.SelectedIndex = 0;
                CbYearB.SelectedIndex = 0;
                RefreshMonthCombo(CbYearA, CbMonthA);
                RefreshMonthCombo(CbYearB, CbMonthB);

                CbYearA.SelectionChanged += PeriodChanged;
                CbYearB.SelectionChanged += PeriodChanged;
                CbMonthA.SelectionChanged += PeriodChanged;
                CbMonthB.SelectionChanged += PeriodChanged;

                await RefreshAllAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur : {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { BtnRefresh.IsEnabled = true; }
        }

        private void FillYearCombo(ComboBox cb)
        {
            cb.Items.Clear();
            cb.Items.Add("Tous");
            var years = new SortedSet<string>();
            if (_allData == null) return;
            foreach (DataRow r in _allData.Rows)
            {
                var v = r["Jour"]?.ToString();
                if (!string.IsNullOrEmpty(v) && v.Length >= 4) years.Add(v[..4]);
            }
            foreach (var y in years) cb.Items.Add(y);
        }

        private void RefreshMonthCombo(ComboBox cbYear, ComboBox cbMonth)
        {
            cbMonth.Items.Clear();
            cbMonth.Items.Add("Tous");
            var selYear = cbYear.SelectedItem as string ?? "Tous";
            var months = new SortedSet<string>();
            if (_allData == null) return;
            foreach (DataRow r in _allData.Rows)
            {
                var v = r["Jour"]?.ToString();
                if (string.IsNullOrEmpty(v) || v.Length < 7) continue;
                if (selYear != "Tous" && !v.StartsWith(selYear)) continue;
                months.Add(v[..7]);
            }
            foreach (var m in months) cbMonth.Items.Add(m);
            cbMonth.SelectedIndex = 0;
        }

        // ── Événements filtres ───────────────────────────────────────────────
        private async void PeriodChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_allData == null) return;

            // Si c'est un ComboBox d'année, reconstruire les mois correspondants
            if (sender == CbYearA)
            {
                CbMonthA.SelectionChanged -= PeriodChanged;
                RefreshMonthCombo(CbYearA, CbMonthA);
                CbMonthA.SelectionChanged += PeriodChanged;
            }
            else if (sender == CbYearB)
            {
                CbMonthB.SelectionChanged -= PeriodChanged;
                RefreshMonthCombo(CbYearB, CbMonthB);
                CbMonthB.SelectionChanged += PeriodChanged;
            }

            await RefreshAllAsync();
        }

        // ── Rafraîchissement global ──────────────────────────────────────────
        private async Task RefreshAllAsync()
        {
            if (_allData == null) return;

            var dvA = BuildDataView(CbYearA, CbMonthA);
            var dvB = BuildDataView(CbYearB, CbMonthB);

            var tarifA = await GetTarifAsync(CbYearA, CbMonthA);
            var tarifB = await GetTarifAsync(CbYearB, CbMonthB);

            RefreshChart(dvA, dvB);
            RefreshSynthese(dvA, dvB);
            RefreshRatios(dvA, dvB);
            RefreshSimulations(dvA, dvB, tarifA, tarifB);
        }

        private DataView BuildDataView(ComboBox cbYear, ComboBox cbMonth)
        {
            var selYear = cbYear.SelectedItem as string ?? "Tous";
            var selMonth = cbMonth.SelectedItem as string ?? "Tous";
            try
            {
                if (selMonth != "Tous")
                    return new DataView(_allData!) { RowFilter = $"Jour LIKE '{selMonth}%'" };
                if (selYear != "Tous")
                    return new DataView(_allData!) { RowFilter = $"Jour LIKE '{selYear}%'" };
                var dv = _allData!.DefaultView;
                dv.RowFilter = string.Empty;
                return dv;
            }
            catch { return _allData!.DefaultView; }
        }

        private async Task<PrixElecTarif?> GetTarifAsync(ComboBox cbYear, ComboBox cbMonth)
        {
            var selYear = cbYear.SelectedItem as string ?? "Tous";
            var selMonth = cbMonth.SelectedItem as string ?? "Tous";
            try
            {
                if (selMonth != "Tous" && selMonth.Length == 7
                    && int.TryParse(selMonth[..4], out int y)
                    && int.TryParse(selMonth[5..], out int m))
                    return await DatabaseHelper.GetTarifForMonthAsync(y, m);

                if (selYear != "Tous" && int.TryParse(selYear, out int yr))
                    return await DatabaseHelper.GetTarifForYearAsync(yr);

                return await DatabaseHelper.GetLatestTarifAsync();
            }
            catch { return null; }
        }

        // ── Graphique ────────────────────────────────────────────────────────
        private void RefreshChart(DataView dvA, DataView dvB)
        {
            double[] GetCol(DataView dv, string col)
            {
                if (_allData?.Columns.Contains(col) != true) return [];
                return dv.Cast<DataRowView>()
                    .Select(r => r.Row[col] == DBNull.Value ? 0d : Convert.ToDouble(r.Row[col]))
                    .ToArray();
            }

            double[] GetVerte(DataView dv)
            {
                if (_allData?.Columns.Contains("Autoconsommation") != true) return [];
                return dv.Cast<DataRowView>().Select(r =>
                    (r.Row["Autoconsommation"] == DBNull.Value ? 0d : Convert.ToDouble(r.Row["Autoconsommation"])) +
                    (r.Row["Conso_Batterie"] == DBNull.Value ? 0d : Convert.ToDouble(r.Row["Conso_Batterie"])))
                    .ToArray();
            }

            // Labels = indices journaliers (1, 2, 3…) sur la période la plus longue
            int maxLen = Math.Max(dvA.Count, dvB.Count);
            var labels = Enumerable.Range(1, maxLen).Select(i => i.ToString()).ToArray();

            LineSeries = new ISeries[]
            {
                MakeLine(GetCol(dvA, "Production"),  "#378ADD", false, "Prod A"),
                MakeLine(GetCol(dvB, "Production"),  "#378ADD", true,  "Prod B"),
                MakeLine(GetVerte(dvA),              "#1D9E75", false, "C. verte A"),
                MakeLine(GetVerte(dvB),              "#1D9E75", true,  "C. verte B"),
                MakeLine(GetCol(dvA, "Conso_Reseau"),"#BA7517", false, "C. réseau A"),
                MakeLine(GetCol(dvB, "Conso_Reseau"),"#BA7517", true,  "C. réseau B"),
            };

            XAxes = [new Axis { Labels = labels, TextSize = 10,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#999999")) }];
            YAxes = [new Axis { TextSize = 10,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#999999")) }];
        }

        private static LineSeries<double> MakeLine(double[] data, string hex, bool dashed, string name)
        {
            var paint = new SolidColorPaint(SKColor.Parse(hex), 2);
            if (dashed) paint.PathEffect = new DashEffect([5f, 3f]);
            return new LineSeries<double>
            {
                Values = data,
                Name = name,
                Stroke = paint,
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.3,
            };
        }

        // ── Synthèse ─────────────────────────────────────────────────────────
        private void RefreshSynthese(DataView dvA, DataView dvB)
        {
            decimal ProdA = Sum(dvA, "Production"), ProdB = Sum(dvB, "Production");
            decimal StockA = Sum(dvA, "Stockage"), StockB = Sum(dvB, "Stockage");
            decimal VerteA = SumVerte(dvA), VerteB = SumVerte(dvB);
            decimal ReseauA = Sum(dvA, "Conso_Reseau"), ReseauB = Sum(dvB, "Conso_Reseau");
            decimal TotaleA = Sum(dvA, "Conso_Totale"), TotaleB = Sum(dvB, "Conso_Totale");
            int cntA = dvA.Count, cntB = dvB.Count;

            DgSynthese.ItemsSource = new List<CmpRow>
            {
                Row("Production (kWh)",    ProdA,   ProdB),
                Row("Stockage (kWh)",      StockA,  StockB),
                Row("Conso verte (kWh)",   VerteA,  VerteB, invert: true),
                Row("Conso réseau (kWh)",  ReseauA, ReseauB, invert: true),
                Row("Conso totale (kWh)",  TotaleA, TotaleB, invert: true),
                Row("Moy./jour (kWh)",
                    cntA > 0 ? TotaleA / cntA : 0,
                    cntB > 0 ? TotaleB / cntB : 0, invert: true),
            };
        }

        // ── Ratios ───────────────────────────────────────────────────────────
        private void RefreshRatios(DataView dvA, DataView dvB)
        {
            decimal ProdA = Sum(dvA, "Production"), ProdB = Sum(dvB, "Production");
            decimal VerteA = SumVerte(dvA), VerteB = SumVerte(dvB);
            decimal TotA = Sum(dvA, "Conso_Totale"), TotB = Sum(dvB, "Conso_Totale");

            DgRatios.ItemsSource = new List<CmpRow>
            {
                RowPct("Verte / Prod",  VerteA, ProdA,  VerteB, ProdB),
                RowPct("Verte / Conso", VerteA, TotA,   VerteB, TotB),
                RowPct("Prod / Conso",  ProdA,  TotA,   ProdB,  TotB),
            };
        }

        // ── Simulations ──────────────────────────────────────────────────────
        private void RefreshSimulations(DataView dvA, DataView dvB,
            PrixElecTarif? tarifA, PrixElecTarif? tarifB)
        {
            decimal SimPrix(DataView dv, PrixElecTarif? t) =>
                t == null ? 0 : Sum(dv, "Conso_Reseau") * 0.75m * t.HP
                               + Sum(dv, "Conso_Reseau") * 0.25m * t.HC;
            decimal SimAuto(DataView dv, PrixElecTarif? t) =>
                t == null ? 0 : Sum(dv, "Autoconsommation") * 1.00m * t.HP;
            decimal SimBat(DataView dv, PrixElecTarif? t) =>
                t == null ? 0 : Sum(dv, "Conso_Batterie") * 0.75m * t.HP
                               + Sum(dv, "Conso_Batterie") * 0.25m * t.HC;

            decimal prixA = SimPrix(dvA, tarifA), prixB = SimPrix(dvB, tarifB);
            decimal autoA = SimAuto(dvA, tarifA), autoB = SimAuto(dvB, tarifB);
            decimal batA = SimBat(dvA, tarifA), batB = SimBat(dvB, tarifB);

            DgSimulations.ItemsSource = new List<CmpRow>
            {
                Row("Sim. Prix",       prixA, prixB, invert: true),
                Row("Sim. Auto conso", autoA, autoB),
                Row("Sim. Batterie",   batA,  batB),
            };

            decimal solaireA = autoA + batA, solaireB = autoB + batB;
            decimal payerA = prixA, payerB = prixB ;

            TxtSolaireA.Text = $"{solaireA:F2} €";
            TxtSolaireB.Text = $"{solaireB:F2} €";
            TxtSolaireDelta.Text = FmtDeltaPct(solaireA, solaireB);
            TxtSolaireDelta.Foreground = DeltaBrush(solaireA, solaireB, invert: false);

            TxtPayerA.Text = $"{payerA:F2} €";
            TxtPayerB.Text = $"{payerB:F2} €";
            TxtPayerDelta.Text = FmtDeltaPct(payerA, payerB);
            TxtPayerDelta.Foreground = DeltaBrush(payerA, payerB, invert: true);
        }

        // ── Helpers calcul ───────────────────────────────────────────────────
        private decimal Sum(DataView dv, string col)
        {
            if (_allData?.Columns.Contains(col) != true) return 0m;
            decimal acc = 0m;
            foreach (DataRowView rv in dv)
                if (rv.Row[col] != DBNull.Value)
                    acc += Convert.ToDecimal(rv.Row[col]);
            return acc;
        }

        private decimal SumVerte(DataView dv) =>
            Sum(dv, "Autoconsommation") + Sum(dv, "Conso_Batterie");

        // Ligne avec valeurs brutes et delta %
        private static CmpRow Row(string lib, decimal a, decimal b, bool invert = false) =>
            new(lib, a.ToString("F2"), b.ToString("F2"),
                FmtDeltaPct(a, b), DeltaBrush(a, b, invert));

        // Ligne avec ratios (a/dena vs b/denb) et delta en points
        private static CmpRow RowPct(string lib, decimal numA, decimal denA, decimal numB, decimal denB)
        {
            string va = denA == 0 ? "–" : (numA / denA * 100m).ToString("F1") + "%";
            string vb = denB == 0 ? "–" : (numB / denB * 100m).ToString("F1") + "%";
            decimal rA = denA == 0 ? 0 : numA / denA * 100m;
            decimal rB = denB == 0 ? 0 : numB / denB * 100m;
            decimal diff = rB - rA;
            string delta = diff == 0 ? "=" : (diff > 0 ? "+" : "") + diff.ToString("F1") + "pt";
            var brush = diff == 0
                ? new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x80))
                : diff > 0
                    ? new SolidColorBrush(Color.FromRgb(0x0F, 0x6E, 0x56))
                    : new SolidColorBrush(Color.FromRgb(0xA3, 0x2D, 0x2D));
            return new(lib, va, vb, delta, brush);
        }

        private static string FmtDeltaPct(decimal a, decimal b)
        {
            if (a == 0) return b == 0 ? "=" : "+∞";
            decimal pct = (b - a) / Math.Abs(a) * 100m;
            return pct == 0 ? "=" : (pct > 0 ? "+" : "") + pct.ToString("F1") + "%";
        }

        private static SolidColorBrush DeltaBrush(decimal a, decimal b, bool invert)
        {
            decimal diff = b - a;
            if (diff == 0) return new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x80));
            bool positive = invert ? diff < 0 : diff > 0;
            return positive
                ? new SolidColorBrush(Color.FromRgb(0x0F, 0x6E, 0x56))
                : new SolidColorBrush(Color.FromRgb(0xA3, 0x2D, 0x2D));
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    internal record CmpRow(string Libelle, string ValA, string ValB,
        string Delta, SolidColorBrush DeltaColor);
}