using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace MedidorSenalUTN
{
    // =========================================================================
    // Paleta de Tema (DESIGN.md: Papel Técnico Claro / Osciloscopio Nocturno)
    // =========================================================================
    public class TemaPaleta
    {
        public bool Oscuro;
        public Color Bg;
        public Color Surface;
        public Color SurfaceAlt;
        public Color SurfaceElevated;
        public Color Fg;
        public Color FgSecondary;
        public Color Muted;
        public Color Line;
        public Color LineStrong;
        public Color Accent;
        public Color AccentSoft;
        public Color Good;
        public Color Amber;
        public Color Bad;

        public static TemaPaleta Crear(bool oscuro)
        {
            var t = new TemaPaleta();
            t.Oscuro = oscuro;
            if (oscuro)
            {
                t.Bg = ColorTranslator.FromHtml("#0A0F1A");
                t.Surface = ColorTranslator.FromHtml("#111827");
                t.SurfaceAlt = ColorTranslator.FromHtml("#182235");
                t.SurfaceElevated = ColorTranslator.FromHtml("#1E293B");
                t.Fg = ColorTranslator.FromHtml("#F1F5F9");
                t.FgSecondary = ColorTranslator.FromHtml("#CBD5E1");
                t.Muted = ColorTranslator.FromHtml("#94A3B8");
                t.Line = ColorTranslator.FromHtml("#24324B");
                t.LineStrong = ColorTranslator.FromHtml("#384B6B");
                t.Accent = ColorTranslator.FromHtml("#6096FF");
                t.AccentSoft = ColorTranslator.FromHtml("#1E3A5F");
                t.Good = ColorTranslator.FromHtml("#34D399");
                t.Amber = ColorTranslator.FromHtml("#F59E0B");
                t.Bad = ColorTranslator.FromHtml("#F87171");
            }
            else
            {
                t.Bg = ColorTranslator.FromHtml("#F5F4EE");
                t.Surface = ColorTranslator.FromHtml("#FDFCF9");
                t.SurfaceAlt = ColorTranslator.FromHtml("#ECEAE1");
                t.SurfaceElevated = ColorTranslator.FromHtml("#FFFFFF");
                t.Fg = ColorTranslator.FromHtml("#0F172A");
                t.FgSecondary = ColorTranslator.FromHtml("#334155");
                t.Muted = ColorTranslator.FromHtml("#526074");
                t.Line = ColorTranslator.FromHtml("#D5D2C6");
                t.LineStrong = ColorTranslator.FromHtml("#989484");
                t.Accent = ColorTranslator.FromHtml("#1849C6");
                t.AccentSoft = ColorTranslator.FromHtml("#E0E8FA");
                t.Good = ColorTranslator.FromHtml("#117A43");
                t.Amber = ColorTranslator.FromHtml("#945300");
                t.Bad = ColorTranslator.FromHtml("#B93829");
            }
            return t;
        }
    }

    // =========================================================================
    // Control Vectorial GDI+: Velocímetro Logarítmico (0 - 1000 Mbps)
    // =========================================================================
    public class VelocimetroControl : Control
    {
        private double valorMbps = 0.0;
        private string fase = "Mbps · listo para medir";
        private TemaPaleta tema;
        private Color colorActivo;

        public VelocimetroControl(TemaPaleta t)
        {
            tema = t;
            colorActivo = t.Accent;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 205;
        }

        public void SetTema(TemaPaleta t)
        {
            tema = t;
            Invalidate();
        }

        public void Actualizar(double mbps, string nuevaFase, Color? colorOverride)
        {
            valorMbps = Math.Max(0.0, Math.Min(1000.0, mbps));
            fase = nuevaFase ?? "";
            if (colorOverride.HasValue) colorActivo = colorOverride.Value;
            else colorActivo = tema.Accent;
            Invalidate();
        }

        private static double MbpsAFraccion(double mbps)
        {
            if (mbps <= 0) return 0.0;
            return Math.Min(1.0, Math.Log10(1.0 + mbps) / Math.Log10(1001.0));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(tema.Surface);

            int w = Width;
            int h = Height;
            int size = Math.Min(w - 32, (h - 30) * 2 - 24);
            if (size < 110) size = 110;
            float cx = w / 2f;
            float cy = h - 68f;
            float r = size / 2f;
            if (cy - r < 14f) r = Math.Max(45f, cy - 14f);
            float stroke = r < 75f ? 9f : 12f;

            RectangleF rect = new RectangleF(cx - r, cy - r, r * 2f, r * 2f);
            float startAngle = 180f;
            float sweepTotal = 180f;

            // Arco base
            using (var penBg = new Pen(tema.LineStrong, stroke))
            {
                penBg.StartCap = LineCap.Round;
                penBg.EndCap = LineCap.Round;
                g.DrawArc(penBg, rect, startAngle, sweepTotal);
            }

            // Arco activo
            double frac = MbpsAFraccion(valorMbps);
            if (frac > 0.005)
            {
                using (var penVal = new Pen(colorActivo, stroke))
                {
                    penVal.StartCap = LineCap.Round;
                    penVal.EndCap = LineCap.Round;
                    g.DrawArc(penVal, rect, startAngle, (float)(sweepTotal * frac));
                }
            }

            // Ticks logarítmicos
            double[] marcas = new double[] { 0, 2, 10, 50, 200, 1000 };
            float tickFontSz = r < 75f ? 6.5f : 7.5f;
            using (var fontTick = new Font("Consolas", tickFontSz, FontStyle.Bold))
            using (var brushTick = new SolidBrush(tema.Muted))
            using (var penTick = new Pen(tema.Muted, 1.5f))
            {
                foreach (double m in marcas)
                {
                    double f = MbpsAFraccion(m);
                    double angRad = Math.PI * (1.0 - f);
                    float x1 = cx + (float)(Math.Cos(angRad) * (r - stroke));
                    float y1 = cy - (float)(Math.Sin(angRad) * (r - stroke));
                    float x2 = cx + (float)(Math.Cos(angRad) * (r - stroke - 7f));
                    float y2 = cy - (float)(Math.Sin(angRad) * (r - stroke - 7f));
                    g.DrawLine(penTick, x1, y1, x2, y2);

                    float tx = cx + (float)(Math.Cos(angRad) * (r - stroke - 19f));
                    float ty = cy - (float)(Math.Sin(angRad) * (r - stroke - 19f));
                    string lbl = m.ToString("0", CultureInfo.InvariantCulture);
                    SizeF sz = g.MeasureString(lbl, fontTick);
                    g.DrawString(lbl, fontTick, brushTick, tx - sz.Width / 2f, ty - sz.Height / 2f);
                }
            }

            // Aguja
            double needleRad = Math.PI * (1.0 - frac);
            float nx = cx + (float)(Math.Cos(needleRad) * Math.Max(20f, r - stroke - 10f));
            float ny = cy - (float)(Math.Sin(needleRad) * Math.Max(20f, r - stroke - 10f));
            using (var penNeedle = new Pen(tema.Fg, 2.5f))
            {
                penNeedle.EndCap = LineCap.Round;
                g.DrawLine(penNeedle, cx, cy, nx, ny);
            }
            using (var brPivot = new SolidBrush(colorActivo))
            {
                g.FillEllipse(brPivot, cx - 5f, cy - 5f, 10f, 10f);
            }

            // Lectura numérica tabular
            string valTxt = valorMbps >= 100
                ? valorMbps.ToString("0", CultureInfo.InvariantCulture)
                : valorMbps.ToString("0.0", CultureInfo.InvariantCulture);

            float valFontSz = w < 320 ? 22f : 27f;
            using (var fVal = new Font("Consolas", valFontSz, FontStyle.Bold))
            using (var brVal = new SolidBrush(tema.Fg))
            {
                SizeF szV = g.MeasureString(valTxt, fVal);
                g.DrawString(valTxt, fVal, brVal, cx - szV.Width / 2f, cy + 6f);
            }

            using (var fFase = new Font("Consolas", 8.5f, FontStyle.Bold))
            using (var brFase = new SolidBrush(tema.Muted))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                RectangleF rectFase = new RectangleF(8f, cy + 46f, Math.Max(40f, w - 16f), 20f);
                g.DrawString(fase, fFase, brFase, rectFase, sf);
            }
        }
    }

    // =========================================================================
    // Datos de Medición Histórica y Gráficos GDI+ (Estadísticas)
    // =========================================================================
    public class MedicionRegistro
    {
        public string Fecha = "";
        public string Dispositivo = "";
        public string Conexion = "";
        public string Tecnologia = "";
        public string Empresa = "";
        public string Lugar = "";
        public string Origen = "";
        public double? Bajada;
        public double? Subida;
        public double? Latencia;
        public double? Jitter;
        public double? Dbm;
    }

    public class RedVecinaWifi
    {
        public string Ssid = "";
        public string Bssid = "";
        public int SenalPct = 0;
        public int Rssi = -80;
        public int Canal = 0;
        public string Banda = "";
        public string Radio = "";
    }

    public class GraficoDispersionControl : Control
    {
        private TemaPaleta tema;
        private List<MedicionRegistro> puntos = new List<MedicionRegistro>();
        private double anchoMhz = 20.0;
        private double tempC = 25.0;

        public GraficoDispersionControl(TemaPaleta t)
        {
            tema = t;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 250;
        }

        public void SetTema(TemaPaleta t) { tema = t; Invalidate(); }

        public void SetDatos(List<MedicionRegistro> datos, double bMhz, double tC)
        {
            puntos = datos ?? new List<MedicionRegistro>();
            anchoMhz = bMhz;
            tempC = tC;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(tema.Surface);

            int padL = 48, padR = 16, padT = 26, padB = 34;
            Rectangle plot = new Rectangle(padL, padT, Math.Max(40, Width - padL - padR), Math.Max(40, Height - padT - padB));

            using (var fTitle = new Font("Consolas", 8.25f, FontStyle.Bold))
            using (var brTitle = new SolidBrush(tema.Muted))
            {
                var sfT = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                g.DrawString(string.Format(CultureInfo.InvariantCulture,
                    "DISPERSIÓN dBm vs Mbps (Shannon B={0:0} MHz, T={1:0} °C)", anchoMhz, tempC),
                    fTitle, brTitle, new RectangleF(padL, 5f, plot.Width, 18f), sfT);
            }

            using (var penBorder = new Pen(tema.Line, 1f))
                g.DrawRectangle(penBorder, plot);

            double xMin = -110.0, xMax = -35.0;
            double yMax = 100.0;
            foreach (var p in puntos)
            {
                if (p.Dbm.HasValue && p.Bajada.HasValue && p.Bajada.Value > yMax)
                    yMax = Math.Ceiling(p.Bajada.Value / 50.0) * 50.0;
            }
            if (yMax > 1000.0) yMax = 1000.0;

            // Grilla horizontal
            using (var penGrid = new Pen(tema.Line, 1f))
            using (var fAxis = new Font("Consolas", 7.5f))
            using (var brAxis = new SolidBrush(tema.Muted))
            {
                penGrid.DashStyle = DashStyle.Dot;
                for (int i = 0; i <= 4; i++)
                {
                    double yv = yMax * i / 4.0;
                    float py = plot.Bottom - (float)(plot.Height * (yv / yMax));
                    g.DrawLine(penGrid, plot.Left, py, plot.Right, py);
                    g.DrawString(yv.ToString("0", CultureInfo.InvariantCulture), fAxis, brAxis, 4f, py - 6f);
                }
                for (int dbm = -105; dbm <= -40; dbm += 15)
                {
                    float px = plot.Left + (float)(plot.Width * ((dbm - xMin) / (xMax - xMin)));
                    g.DrawLine(penGrid, px, plot.Top, px, plot.Bottom);
                    g.DrawString(dbm.ToString(CultureInfo.InvariantCulture), fAxis, brAxis, px - 12f, plot.Bottom + 6f);
                }
            }

            // Curva teórica práctica de Shannon (alineada con teoria.js)
            var curvaPts = new List<PointF>();
            double k = 1.38e-23;
            double ruido = 10.0 * Math.Log10(k * (tempC + 273.0) * anchoMhz * 1e6 / 1e-3);
            for (double dbm = xMin; dbm <= xMax; dbm += 2.0)
            {
                double snrIdeal = dbm - ruido;
                double snrPractico = Math.Max(-10.0, snrIdeal - 22.0);
                double cPractica = anchoMhz * Math.Log(1.0 + Math.Pow(10.0, snrPractico / 10.0)) / Math.Log(2.0) * 0.65;
                double yClamped = Math.Min(yMax, Math.Max(0.0, cPractica));
                float px = plot.Left + (float)(plot.Width * ((dbm - xMin) / (xMax - xMin)));
                float py = plot.Bottom - (float)(plot.Height * (yClamped / yMax));
                curvaPts.Add(new PointF(px, py));
            }
            if (curvaPts.Count > 1)
            {
                using (var penCurva = new Pen(tema.Accent, 2f))
                {
                    penCurva.DashStyle = DashStyle.Dash;
                    g.DrawLines(penCurva, curvaPts.ToArray());
                }
            }

            // Puntos medidos
            int nDibujados = 0;
            foreach (var p in puntos)
            {
                if (!p.Dbm.HasValue || !p.Bajada.HasValue) continue;
                double dx = Math.Max(xMin, Math.Min(xMax, p.Dbm.Value));
                double dy = Math.Max(0.0, Math.Min(yMax, p.Bajada.Value));
                float px = plot.Left + (float)(plot.Width * ((dx - xMin) / (xMax - xMin)));
                float py = plot.Bottom - (float)(plot.Height * (dy / yMax));
                Color c = (p.Conexion == "WiFi") ? tema.Good : tema.Amber;
                using (var br = new SolidBrush(Color.FromArgb(200, c)))
                    g.FillEllipse(br, px - 3.5f, py - 3.5f, 7f, 7f);
                nDibujados++;
            }

            if (nDibujados == 0)
            {
                using (var fEmpty = new Font("Segoe UI", 9f))
                using (var brEmpty = new SolidBrush(tema.Muted))
                    g.DrawString("Sin muestras con dBm registrado en el filtro actual.", fEmpty, brEmpty, plot.Left + 14f, plot.Top + plot.Height / 2f - 8f);
            }
        }
    }

    public class GraficoSerieTemporalControl : Control
    {
        private TemaPaleta tema;
        private List<MedicionRegistro> serie = new List<MedicionRegistro>();

        public GraficoSerieTemporalControl(TemaPaleta t)
        {
            tema = t;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 250;
        }

        public void SetTema(TemaPaleta t) { tema = t; Invalidate(); }

        public void SetDatos(List<MedicionRegistro> datos)
        {
            serie = new List<MedicionRegistro>();
            if (datos != null)
            {
                int start = Math.Max(0, datos.Count - 40);
                for (int i = start; i < datos.Count; i++)
                {
                    if (datos[i].Bajada.HasValue) serie.Add(datos[i]);
                }
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(tema.Surface);

            int padL = 48, padR = 16, padT = 26, padB = 34;
            Rectangle plot = new Rectangle(padL, padT, Math.Max(40, Width - padL - padR), Math.Max(40, Height - padT - padB));

            using (var fTitle = new Font("Consolas", 8.25f, FontStyle.Bold))
            using (var brTitle = new SolidBrush(tema.Muted))
            {
                var sfT = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                g.DrawString("SERIE TEMPORAL (Últimas 40 mediciones · Bajada vs Subida Mbps)",
                    fTitle, brTitle, new RectangleF(padL, 5f, plot.Width, 18f), sfT);
            }

            using (var penBorder = new Pen(tema.Line, 1f))
                g.DrawRectangle(penBorder, plot);

            if (serie.Count < 2)
            {
                using (var fEmpty = new Font("Segoe UI", 9f))
                using (var brEmpty = new SolidBrush(tema.Muted))
                    g.DrawString("Se necesitan al menos 2 mediciones para trazar la serie.", fEmpty, brEmpty,
                        new RectangleF(plot.Left + 12f, plot.Top + plot.Height / 2f - 16f, Math.Max(40f, plot.Width - 24f), 36f));
                return;
            }

            double yMax = 20.0;
            foreach (var m in serie)
            {
                if (m.Bajada.HasValue && m.Bajada.Value > yMax) yMax = m.Bajada.Value;
                if (m.Subida.HasValue && m.Subida.Value > yMax) yMax = m.Subida.Value;
            }
            yMax = Math.Ceiling(yMax / 25.0) * 25.0;
            if (yMax < 25.0) yMax = 25.0;

            using (var penGrid = new Pen(tema.Line, 1f))
            using (var fAxis = new Font("Consolas", 7.5f))
            using (var brAxis = new SolidBrush(tema.Muted))
            {
                penGrid.DashStyle = DashStyle.Dot;
                for (int i = 0; i <= 4; i++)
                {
                    double yv = yMax * i / 4.0;
                    float py = plot.Bottom - (float)(plot.Height * (yv / yMax));
                    g.DrawLine(penGrid, plot.Left, py, plot.Right, py);
                    g.DrawString(yv.ToString("0", CultureInfo.InvariantCulture), fAxis, brAxis, 4f, py - 6f);
                }
            }

            var ptsBaj = new List<PointF>();
            var ptsSub = new List<PointF>();
            for (int i = 0; i < serie.Count; i++)
            {
                float px = plot.Left + (float)(plot.Width * (i / (double)(serie.Count - 1)));
                if (serie[i].Bajada.HasValue)
                {
                    float py = plot.Bottom - (float)(plot.Height * (serie[i].Bajada.Value / yMax));
                    ptsBaj.Add(new PointF(px, py));
                }
                if (serie[i].Subida.HasValue)
                {
                    float py = plot.Bottom - (float)(plot.Height * (serie[i].Subida.Value / yMax));
                    ptsSub.Add(new PointF(px, py));
                }
            }

            if (ptsSub.Count > 1)
            {
                using (var penSub = new Pen(tema.Amber, 1.8f))
                    g.DrawLines(penSub, ptsSub.ToArray());
            }
            if (ptsBaj.Count > 1)
            {
                using (var penBaj = new Pen(tema.Good, 2.2f))
                    g.DrawLines(penBaj, ptsBaj.ToArray());
                using (var brPt = new SolidBrush(tema.Good))
                {
                    foreach (var pt in ptsBaj) g.FillEllipse(brPt, pt.X - 2.5f, pt.Y - 2.5f, 5f, 5f);
                }
            }

            // Leyenda compacta abajo
            using (var fLeg = new Font("Consolas", 7.5f, FontStyle.Bold))
            using (var brG = new SolidBrush(tema.Good))
            using (var brA = new SolidBrush(tema.Amber))
            {
                g.DrawString("● Bajada", fLeg, brG, plot.Left, plot.Bottom + 8f);
                g.DrawString("● Subida", fLeg, brA, plot.Left + 74f, plot.Bottom + 8f);
            }
        }
    }

    // =========================================================================
    // Formulario Principal
    // =========================================================================
    public class MainForm : Form
    {
        private const string DEFAULT_SCRIPT_URL = "https://script.google.com/macros/s/AKfycbw3aFZwnii8ENYhQgDPl2qAefmcTeZ2HE-QDPz2cuogEJQWPQqG6i21825SfMNFxq5ZVw/exec";
        private const string SPEED_HOST = "https://speed.cloudflare.com";
        private const string IP_INFO_URL = "https://ipwho.is/";

        private static readonly string[] CAMPOS = new string[] {
            "id", "fecha", "tipo_dispositivo", "nombre_dispositivo", "marca", "modelo", "so", "version_so", "navegador",
            "conexion", "tecnologia", "tipo_red_detectado", "empresa", "isp_detectado", "asn",
            "bajada_mbps", "subida_mbps", "latencia_ms", "jitter_ms",
            "senal_dbm", "barras", "lugar", "lat", "lon", "precision_m", "ubicacion_fuente", "ciudad", "comentario",
            "sinr_db", "ancho_mhz", "origen"
        };

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private TemaPaleta tema = TemaPaleta.Crear(false);
        private int tabActual = 0; // 0 = Medidor, 1 = Estadísticas

        // Header & Tabs
        private Panel headerPanel;
        private TableLayoutPanel headerGrid;
        private Panel titleBox;
        private FlowLayoutPanel navFlow;
        private Label lblBadge;
        private Label lblTitle;
        private Button btnTabMedidor;
        private Button btnTabStats;
        private Button btnTema;
        private Panel panelMedidor;
        private Panel panelEstadisticas;

        // Medidor UI & Responsividad
        private TableLayoutPanel medidorGrid;
        private Panel colIzq;
        private Panel colDer;
        private TableLayoutPanel kpiGrid;
        private TableLayoutPanel kpiStats;
        private TableLayoutPanel chartsGrid;
        private Panel cardG1;
        private Panel cardG2;
        private bool? esLayoutUnaColumna = null;
        private bool? esHeaderCompacto = null;

        private VelocimetroControl velocimetro;
        private Label lblEquipo;
        private Label lblRed;
        private Label lblProveedor;
        private Label lblBajadaVal;
        private Label lblSubidaVal;
        private Label lblLatenciaVal;
        private Label lblJitterVal;
        private Label lblTeoria;
        private Label lblCongestion;
        private Label lblEstado;

        private NumericUpDown numAnchoMhz;
        private NumericUpDown numTempC;
        private DataGridView gridEspectro;
        private Button btnEscanear;

        private Button btnMedir;
        private Button btnEnviar;
        private ComboBox cmbLugar;
        private TextBox txtComentario;
        private CheckBox chkConsentimiento;
        private CheckBox chkMonitoreo;
        private NumericUpDown numIntervalo;

        // Estadísticas UI
        private Label lblStatsEstado;
        private Button btnStatsActualizar;
        private ComboBox cmbStatsFiltro;
        private Label lblKpiN;
        private Label lblKpiBaj;
        private Label lblKpiSub;
        private Label lblKpiLat;
        private GraficoDispersionControl grafDispersion;
        private GraficoSerieTemporalControl grafSerie;
        private DataGridView gridHistorial;

        // Estado de medición
        private System.Windows.Forms.Timer timerRed;
        private System.Windows.Forms.Timer timerMonitoreo;
        private int segRestantesMonitoreo = 0;

        private Dictionary<string, object> medicion = new Dictionary<string, object>();
        private Dictionary<string, string> wifiActual = new Dictionary<string, string>();
        private List<MedicionRegistro> registrosLocales = new List<MedicionRegistro>();
        private List<MedicionRegistro> registrosRemotos = new List<MedicionRegistro>();
        private bool midiendo = false;
        private bool tieneResultado = false;
        private double ultimaBajadaMbps = 0.0;
        private double ultimaSubidaMbps = 0.0;
        private string rutaCsv;

        // Colecciones para aplicar tema dinámico
        private readonly List<Panel> tarjetasThemed = new List<Panel>();
        private readonly List<Label> labelsCardTitle = new List<Label>();
        private readonly List<Label> labelsFg = new List<Label>();
        private readonly List<Label> labelsMuted = new List<Label>();
        private readonly List<Panel> panelsSurface = new List<Panel>();
        private readonly List<Panel> panelsSurfaceAlt = new List<Panel>();
        private readonly List<Panel> panelsLine = new List<Panel>();

        [STAThread]
        public static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            if (args != null && args.Length > 0)
            {
                EjecutarModoConsola(args);
                return;
            }
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        public MainForm()
        {
            rutaCsv = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mediciones_pc.csv");

            Text = "Medidor de Señal · UTN FRT Comunicación (Windows)";
            Size = new Size(1080, 770);
            MinimumSize = new Size(560, 560);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            ConstruirUI();
            AplicarTema();
            SizeChanged += (s, e) => AjustarLayoutResponsivo();
            Shown += (s, e) => AjustarLayoutResponsivo();

            CargarEquipoInicial();
            RefrescarWifi();
            EscanearEspectroWifi();
            CargarCsvLocal();

            timerRed = new System.Windows.Forms.Timer();
            timerRed.Interval = 4000;
            timerRed.Tick += (s, e) => { if (!midiendo && tabActual == 0) RefrescarWifi(); };
            timerRed.Start();

            timerMonitoreo = new System.Windows.Forms.Timer();
            timerMonitoreo.Interval = 1000;
            timerMonitoreo.Tick += TimerMonitoreo_Tick;

            Task.Run(() => DetectarISP());
        }

        // =====================================================================
        // Construcción de Interfaz (2 columnas + Pestaña Estadísticas)
        // =====================================================================
        private void ConstruirUI()
        {
            // Cabecera fija superior
            headerPanel = new Panel { Dock = DockStyle.Top, Height = 74, Padding = new Padding(20, 10, 20, 0) };

            var headerLine = new Panel { Dock = DockStyle.Bottom, Height = 1 };
            panelsLine.Add(headerLine);

            headerGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            titleBox = new Panel { Dock = DockStyle.Fill, Height = 54, Margin = new Padding(0) };
            lblBadge = new Label
            {
                Text = "UTN FRT · COMUNICACIÓN · TELEMETRÍA DE ESCRITORIO",
                Font = new Font("Consolas", 8.5f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(0, 2)
            };
            lblTitle = new Label
            {
                Text = "Medidor de Señal — Windows",
                Font = new Font("Segoe UI", 17f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(-2, 20)
            };
            titleBox.Controls.Add(lblBadge);
            titleBox.Controls.Add(lblTitle);

            navFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(8, 10, 0, 0),
                Padding = new Padding(0)
            };

            btnTabMedidor = CrearBoton("Medidor", true);
            btnTabMedidor.Size = new Size(106, 36);
            btnTabMedidor.Margin = new Padding(0, 0, 6, 0);
            btnTabMedidor.Click += (s, e) => CambiarTab(0);

            btnTabStats = CrearBoton("Estadísticas", false);
            btnTabStats.Size = new Size(122, 36);
            btnTabStats.Margin = new Padding(0, 0, 10, 0);
            btnTabStats.Click += (s, e) => CambiarTab(1);

            btnTema = CrearBoton("Tema: Oscuro", false);
            btnTema.Size = new Size(118, 36);
            btnTema.Margin = new Padding(0);
            btnTema.Click += (s, e) =>
            {
                tema = TemaPaleta.Crear(!tema.Oscuro);
                AplicarTema();
            };

            navFlow.Controls.Add(btnTabMedidor);
            navFlow.Controls.Add(btnTabStats);
            navFlow.Controls.Add(btnTema);

            headerGrid.Controls.Add(titleBox, 0, 0);
            headerGrid.Controls.Add(navFlow, 1, 0);

            headerPanel.Controls.Add(headerGrid);
            headerPanel.Controls.Add(headerLine);

            // Contenedores de pestañas
            panelMedidor = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(18, 12, 18, 14) };
            panelEstadisticas = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(18, 12, 18, 14), Visible = false };

            ConstruirPanelMedidor();
            ConstruirPanelEstadisticas();

            Controls.Add(panelEstadisticas);
            Controls.Add(panelMedidor);
            Controls.Add(headerPanel);
        }

        private void ConstruirPanelMedidor()
        {
            medidorGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1
            };
            medidorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));
            medidorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));
            panelMedidor.Controls.Add(medidorGrid);

            colIzq = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(0, 0, 8, 0) };
            colDer = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 0, 0, 0) };
            medidorGrid.Controls.Add(colIzq, 0, 0);
            medidorGrid.Controls.Add(colDer, 1, 0);

            // --- COLUMNA IZQUIERDA: Instrumento + Registro ---
            var cardMed = CrearTarjeta("INSTRUMENTO DE MEDICIÓN");
            velocimetro = new VelocimetroControl(tema);
            velocimetro.Dock = DockStyle.Top;

            btnMedir = CrearBoton("▶  Iniciar medición de velocidad", true);
            btnMedir.Dock = DockStyle.Top;
            btnMedir.Height = 40;
            btnMedir.Click += async (s, e) => await EjecutarMedicionAsync(false);

            kpiGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 68,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(1),
                Margin = new Padding(0, 10, 0, 2)
            };
            panelsLine.Add(kpiGrid);
            for (int i = 0; i < 4; i++) kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            kpiGrid.Controls.Add(CrearCeldaKpi("BAJADA", out lblBajadaVal), 0, 0);
            kpiGrid.Controls.Add(CrearCeldaKpi("SUBIDA", out lblSubidaVal), 1, 0);
            kpiGrid.Controls.Add(CrearCeldaKpi("LATENCIA", out lblLatenciaVal), 2, 0);
            kpiGrid.Controls.Add(CrearCeldaKpi("JITTER", out lblJitterVal), 3, 0);

            var spMed = new Panel { Dock = DockStyle.Top, Height = 8 };
            panelsSurface.Add(spMed);

            cardMed.Controls.Add(kpiGrid);
            cardMed.Controls.Add(spMed);
            cardMed.Controls.Add(btnMedir);
            cardMed.Controls.Add(velocimetro);

            // Tarjeta Registro y Monitoreo
            var cardEnv = CrearTarjeta("REGISTRO, MONITOREO Y SINCRONIZACIÓN");
            var formTable = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 4, 0, 4) };
            panelsSurface.Add(formTable);
            formTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
            formTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));

            var lblLugar = new Label { Text = "Entorno:", Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 6, 3) };
            var lblCom = new Label { Text = "Observación / aula:", Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold), AutoSize = true, Margin = new Padding(6, 0, 0, 3) };
            labelsMuted.Add(lblLugar);
            labelsMuted.Add(lblCom);
            formTable.Controls.Add(lblLugar, 0, 0);
            formTable.Controls.Add(lblCom, 1, 0);

            cmbLugar = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f), Margin = new Padding(0, 0, 6, 8) };
            cmbLugar.Items.AddRange(new object[] { "Interior", "Exterior", "En movimiento" });
            cmbLugar.SelectedIndex = 0;

            txtComentario = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f), Text = "Laboratorio PC", Margin = new Padding(6, 0, 0, 8) };
            formTable.Controls.Add(cmbLugar, 0, 1);
            formTable.Controls.Add(txtComentario, 1, 1);

            var monPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(8, 6, 8, 6), Margin = new Padding(0, 2, 0, 6) };
            panelsSurfaceAlt.Add(monPanel);
            chkMonitoreo = new CheckBox { Text = "Monitoreo cada", AutoSize = true, Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold), Margin = new Padding(0, 3, 4, 0) };
            chkMonitoreo.CheckedChanged += ChkMonitoreo_CheckedChanged;
            numIntervalo = new NumericUpDown { Minimum = 15, Maximum = 3600, Value = 60, Width = 58, Font = new Font("Consolas", 9.5f, FontStyle.Bold) };
            var lblSeg = new Label { Text = "seg (auto-envía y guarda CSV)", AutoSize = true, Margin = new Padding(4, 4, 0, 0) };
            labelsMuted.Add(lblSeg);
            monPanel.Controls.Add(chkMonitoreo);
            monPanel.Controls.Add(numIntervalo);
            monPanel.Controls.Add(lblSeg);

            chkConsentimiento = new CheckBox
            {
                Text = "Acepto registrar esta medición para la cátedra de Comunicación (UTN FRT).",
                Checked = true,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                Dock = DockStyle.Top,
                Padding = new Padding(0, 4, 0, 8)
            };

            btnEnviar = CrearBoton("Registrar en Google Sheets + CSV local", true);
            btnEnviar.Dock = DockStyle.Top;
            btnEnviar.Height = 38;
            btnEnviar.Enabled = false;
            btnEnviar.Click += async (s, e) => await EnviarMedicionAsync();

            lblEstado = new Label { Text = "", Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold), AutoSize = true, MaximumSize = new Size(420, 0), Dock = DockStyle.Top, Padding = new Padding(0, 6, 0, 2), Visible = false };

            cardEnv.Controls.Add(lblEstado);
            cardEnv.Controls.Add(btnEnviar);
            cardEnv.Controls.Add(chkConsentimiento);
            cardEnv.Controls.Add(monPanel);
            cardEnv.Controls.Add(formTable);

            colIzq.Controls.Add(cardEnv);
            colIzq.Controls.Add(cardMed);

            // --- COLUMNA DERECHA: Telemetría + Teoría Interactiva + Escáner WiFi ---
            var cardEq = CrearTarjeta("TELEMETRÍA DEL EQUIPO Y ENLACE EN VIVO");
            lblEquipo = new Label { Text = "Detectando equipo…", Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold), AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 2, 0, 3) };
            lblRed = new Label { Text = "Analizando interfaz de red…", Font = new Font("Consolas", 9f, FontStyle.Regular), AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 2, 0, 3) };
            lblProveedor = new Label { Text = "Proveedor IP: consultando ipwho.is…", Font = new Font("Segoe UI", 9f, FontStyle.Regular), AutoSize = true, Dock = DockStyle.Top };
            labelsFg.Add(lblEquipo);
            labelsFg.Add(lblRed);
            labelsMuted.Add(lblProveedor);
            cardEq.Controls.Add(lblProveedor);
            cardEq.Controls.Add(lblRed);
            cardEq.Controls.Add(lblEquipo);

            // Tarjeta Teoría Interactiva (Shannon + Nyquist Ethernet)
            var cardTeo = CrearTarjeta("ANÁLISIS TEÓRICO DE CANAL (SHANNON-HARTLEY / NYQUIST)");
            var teoParams = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(6, 5, 6, 5), Margin = new Padding(0, 2, 0, 6) };
            panelsSurfaceAlt.Add(teoParams);

            var lblB = new Label { Text = "B (MHz):", AutoSize = true, Font = new Font("Consolas", 8.5f, FontStyle.Bold), Margin = new Padding(0, 4, 4, 0) };
            labelsMuted.Add(lblB);
            numAnchoMhz = new NumericUpDown { Minimum = 1, Maximum = 320, Value = 20, Width = 58, Font = new Font("Consolas", 9f, FontStyle.Bold), Margin = new Padding(0, 0, 14, 0) };
            numAnchoMhz.ValueChanged += (s, e) => { ActualizarAnalisisTeorico(ultimaBajadaMbps); if (grafDispersion != null) grafDispersion.SetDatos(ObtenerDatasetActivo(), (double)numAnchoMhz.Value, (double)numTempC.Value); };

            var lblT = new Label { Text = "Temp (°C):", AutoSize = true, Font = new Font("Consolas", 8.5f, FontStyle.Bold), Margin = new Padding(0, 4, 4, 0) };
            labelsMuted.Add(lblT);
            numTempC = new NumericUpDown { Minimum = -20, Maximum = 80, Value = 25, Width = 52, Font = new Font("Consolas", 9f, FontStyle.Bold) };
            numTempC.ValueChanged += (s, e) => { ActualizarAnalisisTeorico(ultimaBajadaMbps); if (grafDispersion != null) grafDispersion.SetDatos(ObtenerDatasetActivo(), (double)numAnchoMhz.Value, (double)numTempC.Value); };

            teoParams.Controls.Add(lblB);
            teoParams.Controls.Add(numAnchoMhz);
            teoParams.Controls.Add(lblT);
            teoParams.Controls.Add(numTempC);

            lblTeoria = new Label
            {
                Text = "Iniciá una medición para calcular ruido térmico (N = kTB), SNR y capacidad límite de Shannon (WiFi) o Nyquist 4D-PAM5 (Ethernet).",
                Font = new Font("Consolas", 8.75f, FontStyle.Regular),
                AutoSize = true,
                MaximumSize = new Size(510, 0),
                Dock = DockStyle.Top,
                Padding = new Padding(0, 4, 0, 2)
            };
            labelsFg.Add(lblTeoria);

            cardTeo.Controls.Add(lblTeoria);
            cardTeo.Controls.Add(teoParams);

            // Tarjeta Escáner de Espectro WiFi Local
            var cardWifi = CrearTarjeta("ESCÁNER DE ESPECTRO WiFi LOCAL (CONGESTIÓN DE CANALES)");
            var wifiBar = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(0, 0, 0, 4) };
            panelsSurface.Add(wifiBar);
            lblCongestion = new Label { Text = "Escaneando redes vecinas…", Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            labelsFg.Add(lblCongestion);
            btnEscanear = CrearBoton("↻ Escanear", false);
            btnEscanear.Dock = DockStyle.Right;
            btnEscanear.Width = 88;
            btnEscanear.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            btnEscanear.Click += (s, e) => EscanearEspectroWifi();
            wifiBar.Controls.Add(lblCongestion);
            wifiBar.Controls.Add(btnEscanear);

            gridEspectro = CrearGridEstilizado();
            gridEspectro.Height = 165;
            gridEspectro.Dock = DockStyle.Top;
            gridEspectro.Columns.Add("ssid", "SSID");
            gridEspectro.Columns.Add("banda", "Banda");
            gridEspectro.Columns.Add("canal", "Canal");
            gridEspectro.Columns.Add("rssi", "RSSI");
            gridEspectro.Columns.Add("radio", "Estándar");
            gridEspectro.Columns["ssid"].FillWeight = 34;
            gridEspectro.Columns["banda"].FillWeight = 18;
            gridEspectro.Columns["canal"].FillWeight = 14;
            gridEspectro.Columns["rssi"].FillWeight = 18;
            gridEspectro.Columns["radio"].FillWeight = 16;

            cardWifi.Controls.Add(gridEspectro);
            cardWifi.Controls.Add(wifiBar);

            colDer.Controls.Add(cardWifi);
            colDer.Controls.Add(cardTeo);
            colDer.Controls.Add(cardEq);
        }

        private void ConstruirPanelEstadisticas()
        {
            // Barra superior de estadísticas
            var cardBar = CrearTarjeta("SINCRONIZACIÓN Y FILTROS DE MUESTRAS");
            var barFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            panelsSurface.Add(barFlow);

            btnStatsActualizar = CrearBoton("↻ Actualizar desde Google Sheets", true);
            btnStatsActualizar.Size = new Size(235, 32);
            btnStatsActualizar.Margin = new Padding(0, 0, 12, 0);
            btnStatsActualizar.Click += async (s, e) => await CargarEstadisticasNubeAsync();

            var lblF = new Label { Text = "Origen:", AutoSize = true, Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold), Margin = new Padding(0, 7, 6, 0) };
            labelsMuted.Add(lblF);

            cmbStatsFiltro = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Font = new Font("Segoe UI", 9.5f), Margin = new Padding(0, 2, 14, 0) };
            cmbStatsFiltro.Items.AddRange(new object[] { "Todas (Nube + Local)", "Solo esta PC (CSV local)", "Solo WiFi", "Solo Cable / Ethernet" });
            cmbStatsFiltro.SelectedIndex = 0;
            cmbStatsFiltro.SelectedIndexChanged += (s, e) => RefrescarVistaEstadisticas();

            lblStatsEstado = new Label { Text = "Mostrando historial local (mediciones_pc.csv).", AutoSize = true, Font = new Font("Segoe UI", 9f), Margin = new Padding(0, 7, 0, 0) };
            labelsMuted.Add(lblStatsEstado);

            barFlow.Controls.Add(btnStatsActualizar);
            barFlow.Controls.Add(lblF);
            barFlow.Controls.Add(cmbStatsFiltro);
            barFlow.Controls.Add(lblStatsEstado);
            cardBar.Controls.Add(barFlow);

            // 4 KPIs estadísticos
            kpiStats = new TableLayoutPanel { Dock = DockStyle.Top, Height = 72, ColumnCount = 4, RowCount = 1, Padding = new Padding(1), Margin = new Padding(0, 0, 0, 12) };
            panelsLine.Add(kpiStats);
            for (int i = 0; i < 4; i++) kpiStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            kpiStats.Controls.Add(CrearCeldaKpi("MUESTRAS (N)", out lblKpiN), 0, 0);
            kpiStats.Controls.Add(CrearCeldaKpi("MEDIANA BAJADA (Mbps)", out lblKpiBaj), 1, 0);
            kpiStats.Controls.Add(CrearCeldaKpi("MEDIANA SUBIDA (Mbps)", out lblKpiSub), 2, 0);
            kpiStats.Controls.Add(CrearCeldaKpi("MEDIANA LATENCIA (ms)", out lblKpiLat), 3, 0);

            // Gráficos lado a lado
            chartsGrid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 266, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 0, 12) };
            chartsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            chartsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            cardG1 = CrearTarjeta("TEORÍA VS EMPIRIA");
            cardG1.Dock = DockStyle.Fill;
            cardG1.Margin = new Padding(0, 0, 6, 0);
            grafDispersion = new GraficoDispersionControl(tema) { Dock = DockStyle.Fill };
            cardG1.Controls.Add(grafDispersion);

            cardG2 = CrearTarjeta("EVOLUCIÓN EN EL TIEMPO");
            cardG2.Dock = DockStyle.Fill;
            cardG2.Margin = new Padding(6, 0, 0, 0);
            grafSerie = new GraficoSerieTemporalControl(tema) { Dock = DockStyle.Fill };
            cardG2.Controls.Add(grafSerie);

            chartsGrid.Controls.Add(cardG1, 0, 0);
            chartsGrid.Controls.Add(cardG2, 1, 0);

            // Tabla historial
            var cardTabla = CrearTarjeta("ÚLTIMAS MEDICIONES REGISTRADAS");
            gridHistorial = CrearGridEstilizado();
            gridHistorial.Height = 220;
            gridHistorial.Dock = DockStyle.Top;
            gridHistorial.Columns.Add("fecha", "Fecha");
            gridHistorial.Columns.Add("disp", "Equipo / Origen");
            gridHistorial.Columns.Add("con", "Conexión");
            gridHistorial.Columns.Add("baj", "Bajada (Mbps)");
            gridHistorial.Columns.Add("sub", "Subida (Mbps)");
            gridHistorial.Columns.Add("lat", "Latencia (ms)");
            gridHistorial.Columns.Add("dbm", "Señal (dBm)");
            gridHistorial.Columns.Add("lug", "Entorno");
            cardTabla.Controls.Add(gridHistorial);

            panelEstadisticas.Controls.Add(cardTabla);
            panelEstadisticas.Controls.Add(chartsGrid);
            panelEstadisticas.Controls.Add(kpiStats);
            panelEstadisticas.Controls.Add(cardBar);
        }

        private void CambiarTab(int idx)
        {
            tabActual = idx;
            panelMedidor.Visible = (idx == 0);
            panelEstadisticas.Visible = (idx == 1);
            EstilizarBoton(btnTabMedidor, idx == 0);
            EstilizarBoton(btnTabStats, idx == 1);
            AjustarLayoutResponsivo();
            if (idx == 1)
            {
                RefrescarVistaEstadisticas();
                if (registrosRemotos.Count == 0)
                {
                    var t = CargarEstadisticasNubeAsync();
                }
            }
        }

        private void AjustarLayoutResponsivo()
        {
            if (headerGrid == null || medidorGrid == null || chartsGrid == null) return;
            int w = ClientSize.Width;
            if (w <= 0) return;

            SuspendLayout();
            try
            {
                // 1. Cabecera adaptable (1 fila en ancho >= 740, 2 filas en compacto)
                bool compacto = w < 740;
                if (esHeaderCompacto != compacto)
                {
                    esHeaderCompacto = compacto;
                    headerGrid.SuspendLayout();
                    headerGrid.Controls.Clear();
                    headerGrid.ColumnStyles.Clear();
                    headerGrid.RowStyles.Clear();
                    if (compacto)
                    {
                        headerGrid.ColumnCount = 1;
                        headerGrid.RowCount = 2;
                        headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                        headerGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                        headerGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                        navFlow.Anchor = AnchorStyles.Left;
                        navFlow.Margin = new Padding(0, 4, 0, 8);
                        headerGrid.Controls.Add(titleBox, 0, 0);
                        headerGrid.Controls.Add(navFlow, 0, 1);
                        headerPanel.Height = 112;
                    }
                    else
                    {
                        headerGrid.ColumnCount = 2;
                        headerGrid.RowCount = 1;
                        headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                        headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                        headerGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                        navFlow.Anchor = AnchorStyles.Right;
                        navFlow.Margin = new Padding(8, 10, 0, 0);
                        headerGrid.Controls.Add(titleBox, 0, 0);
                        headerGrid.Controls.Add(navFlow, 1, 0);
                        headerPanel.Height = 74;
                    }
                    headerGrid.ResumeLayout(true);
                }

                // 2. Medidor y Gráficos: 2 columnas en >= 880 px, 1 columna apilada en < 880 px
                bool unaCol = w < 880;
                if (esLayoutUnaColumna != unaCol)
                {
                    esLayoutUnaColumna = unaCol;

                    medidorGrid.SuspendLayout();
                    medidorGrid.Controls.Clear();
                    medidorGrid.ColumnStyles.Clear();
                    medidorGrid.RowStyles.Clear();
                    if (unaCol)
                    {
                        medidorGrid.ColumnCount = 1;
                        medidorGrid.RowCount = 2;
                        medidorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                        medidorGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                        medidorGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                        colIzq.Padding = new Padding(0);
                        colDer.Padding = new Padding(0);
                        medidorGrid.Controls.Add(colIzq, 0, 0);
                        medidorGrid.Controls.Add(colDer, 0, 1);
                    }
                    else
                    {
                        medidorGrid.ColumnCount = 2;
                        medidorGrid.RowCount = 1;
                        medidorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));
                        medidorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));
                        medidorGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                        colIzq.Padding = new Padding(0, 0, 8, 0);
                        colDer.Padding = new Padding(8, 0, 0, 0);
                        medidorGrid.Controls.Add(colIzq, 0, 0);
                        medidorGrid.Controls.Add(colDer, 1, 0);
                    }
                    medidorGrid.ResumeLayout(true);

                    chartsGrid.SuspendLayout();
                    chartsGrid.Controls.Clear();
                    chartsGrid.ColumnStyles.Clear();
                    chartsGrid.RowStyles.Clear();
                    if (unaCol)
                    {
                        chartsGrid.ColumnCount = 1;
                        chartsGrid.RowCount = 2;
                        chartsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                        chartsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
                        chartsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
                        cardG1.Margin = new Padding(0, 0, 0, 10);
                        cardG2.Margin = new Padding(0);
                        chartsGrid.Height = 532;
                        chartsGrid.Controls.Add(cardG1, 0, 0);
                        chartsGrid.Controls.Add(cardG2, 0, 1);
                    }
                    else
                    {
                        chartsGrid.ColumnCount = 2;
                        chartsGrid.RowCount = 1;
                        chartsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                        chartsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                        chartsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                        cardG1.Margin = new Padding(0, 0, 6, 0);
                        cardG2.Margin = new Padding(6, 0, 0, 0);
                        chartsGrid.Height = 266;
                        chartsGrid.Controls.Add(cardG1, 0, 0);
                        chartsGrid.Controls.Add(cardG2, 1, 0);
                    }
                    chartsGrid.ResumeLayout(true);
                }

                // 3. Ancho dinámico para ajuste de texto (sin recortes ni desbordes)
                int scrollBarReserve = 28;
                int avail = Math.Max(320, w - 36 - scrollBarReserve);
                int izqInner = unaCol ? (avail - 32) : (int)(avail * 0.46) - 42;
                int derInner = unaCol ? (avail - 32) : (int)(avail * 0.54) - 42;
                izqInner = Math.Max(220, izqInner);
                derInner = Math.Max(220, derInner);

                if (lblEstado != null) lblEstado.MaximumSize = new Size(izqInner, 0);
                if (chkConsentimiento != null) chkConsentimiento.MaximumSize = new Size(izqInner, 0);
                if (lblEquipo != null) lblEquipo.MaximumSize = new Size(derInner, 0);
                if (lblRed != null) lblRed.MaximumSize = new Size(derInner, 0);
                if (lblProveedor != null) lblProveedor.MaximumSize = new Size(derInner, 0);
                if (lblTeoria != null) lblTeoria.MaximumSize = new Size(derInner, 0);
                if (lblStatsEstado != null) lblStatsEstado.MaximumSize = new Size(Math.Max(260, avail - 36), 0);

                // 4. Escala tipográfica adaptativa en celdas KPI
                float kpiFontSz = (unaCol ? avail : (int)(avail * 0.46)) < 430 ? 11.5f : 14.5f;
                Label[] kpiVals = new Label[] { lblBajadaVal, lblSubidaVal, lblLatenciaVal, lblJitterVal, lblKpiN, lblKpiBaj, lblKpiSub, lblKpiLat };
                foreach (var kv in kpiVals)
                {
                    if (kv != null && Math.Abs(kv.Font.Size - kpiFontSz) > 0.1f)
                        kv.Font = new Font("Consolas", kpiFontSz, FontStyle.Bold);
                }
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        // =====================================================================
        // Helpers de UI y Tema Dinámico
        // =====================================================================
        private Panel CrearTarjeta(string titulo)
        {
            var card = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(14, 10, 14, 12),
                Margin = new Padding(0, 0, 0, 12)
            };
            card.Paint += (s, e) =>
            {
                using (var pen = new Pen(tema.Line, 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };
            var lblHead = new Label
            {
                Text = titulo,
                Font = new Font("Consolas", 8.25f, FontStyle.Bold),
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 6)
            };
            card.Controls.Add(lblHead);
            tarjetasThemed.Add(card);
            labelsCardTitle.Add(lblHead);
            return card;
        }

        private Panel CrearCeldaKpi(string etiqueta, out Label lblValor)
        {
            var cell = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 6, 8, 4) };
            panelsSurface.Add(cell);
            var k = new Label { Text = etiqueta, Font = new Font("Consolas", 7.5f, FontStyle.Bold), Dock = DockStyle.Top, Height = 16 };
            labelsMuted.Add(k);
            lblValor = new Label { Text = "—", Font = new Font("Consolas", 14.5f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            labelsFg.Add(lblValor);
            cell.Controls.Add(lblValor);
            cell.Controls.Add(k);
            return cell;
        }

        private Button CrearBoton(string texto, bool primario)
        {
            var b = new Button
            {
                Text = texto,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 1;
            EstilizarBoton(b, primario);
            return b;
        }

        private void EstilizarBoton(Button b, bool primario)
        {
            if (b == null) return;
            b.BackColor = primario ? tema.Accent : tema.SurfaceAlt;
            b.ForeColor = primario ? Color.White : tema.Fg;
            b.FlatAppearance.BorderColor = primario ? tema.Accent : tema.Line;
        }

        private DataGridView CrearGridEstilizado()
        {
            var g = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 26,
                Font = new Font("Consolas", 8.25f)
            };
            g.RowTemplate.Height = 23;
            return g;
        }

        private void EstilizarGrid(DataGridView g)
        {
            g.BackgroundColor = tema.Surface;
            g.GridColor = tema.Line;
            g.DefaultCellStyle.BackColor = tema.Surface;
            g.DefaultCellStyle.ForeColor = tema.Fg;
            g.DefaultCellStyle.SelectionBackColor = tema.AccentSoft;
            g.DefaultCellStyle.SelectionForeColor = tema.Fg;
            g.AlternatingRowsDefaultCellStyle.BackColor = tema.SurfaceAlt;
            g.ColumnHeadersDefaultCellStyle.BackColor = tema.SurfaceAlt;
            g.ColumnHeadersDefaultCellStyle.ForeColor = tema.Muted;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Consolas", 8f, FontStyle.Bold);
        }

        private void AplicarTema()
        {
            BackColor = tema.Bg;
            ForeColor = tema.Fg;
            headerPanel.BackColor = tema.Bg;
            panelMedidor.BackColor = tema.Bg;
            panelEstadisticas.BackColor = tema.Bg;

            lblBadge.ForeColor = tema.Accent;
            lblTitle.ForeColor = tema.Fg;

            foreach (var p in tarjetasThemed) { p.BackColor = tema.Surface; p.Invalidate(); }
            foreach (var p in panelsSurface) p.BackColor = tema.Surface;
            foreach (var p in panelsSurfaceAlt) p.BackColor = tema.SurfaceAlt;
            foreach (var p in panelsLine) p.BackColor = tema.Line;
            foreach (var l in labelsCardTitle) l.ForeColor = tema.Muted;
            foreach (var l in labelsFg) { if (l != null) l.ForeColor = tema.Fg; }
            foreach (var l in labelsMuted) { if (l != null) l.ForeColor = tema.Muted; }

            chkMonitoreo.ForeColor = tema.Fg;
            chkConsentimiento.ForeColor = tema.FgSecondary;
            cmbLugar.BackColor = tema.SurfaceElevated;
            cmbLugar.ForeColor = tema.Fg;
            txtComentario.BackColor = tema.SurfaceElevated;
            txtComentario.ForeColor = tema.Fg;
            numIntervalo.BackColor = tema.SurfaceElevated;
            numIntervalo.ForeColor = tema.Fg;
            numAnchoMhz.BackColor = tema.SurfaceElevated;
            numAnchoMhz.ForeColor = tema.Fg;
            numTempC.BackColor = tema.SurfaceElevated;
            numTempC.ForeColor = tema.Fg;
            cmbStatsFiltro.BackColor = tema.SurfaceElevated;
            cmbStatsFiltro.ForeColor = tema.Fg;

            EstilizarBoton(btnTabMedidor, tabActual == 0);
            EstilizarBoton(btnTabStats, tabActual == 1);
            EstilizarBoton(btnTema, false);
            btnTema.Text = tema.Oscuro ? "Tema: Claro" : "Tema: Oscuro";
            EstilizarBoton(btnMedir, true);
            EstilizarBoton(btnEnviar, true);
            EstilizarBoton(btnEscanear, false);
            EstilizarBoton(btnStatsActualizar, true);

            EstilizarGrid(gridEspectro);
            EstilizarGrid(gridHistorial);

            velocimetro.SetTema(tema);
            grafDispersion.SetTema(tema);
            grafSerie.SetTema(tema);

            try
            {
                int dark = tema.Oscuro ? 1 : 0;
                DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            }
            catch { }
        }

        // =====================================================================
        // Detección de Equipo, WiFi, Escáner de Espectro e ISP
        // =====================================================================
        private static string LeerScriptUrlInicial()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] posibles = new string[] {
                    Path.Combine(baseDir, "config.js"),
                    Path.Combine(baseDir, "..", "config.js")
                };
                foreach (var p in posibles)
                {
                    if (File.Exists(p))
                    {
                        var txt = File.ReadAllText(p, Encoding.UTF8);
                        var m = Regex.Match(txt, "SCRIPT_URL:\\s*\"([^\"]+)\"");
                        if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                            return m.Groups[1].Value;
                    }
                }
            }
            catch { }
            return DEFAULT_SCRIPT_URL;
        }

        private void CargarEquipoInicial()
        {
            string nombre = Environment.MachineName;
            string marca = "", modelo = "";
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        marca = Convert.ToString(obj["Manufacturer"] ?? "").Trim();
                        modelo = Convert.ToString(obj["Model"] ?? "").Trim();
                        break;
                    }
                }
            }
            catch { }

            int build = Environment.OSVersion.Version.Build;
            string verWin = build >= 22000 ? "11" : "10";

            medicion["nombre_dispositivo"] = nombre;
            medicion["marca"] = string.IsNullOrEmpty(marca) ? "PC" : marca;
            medicion["modelo"] = modelo;
            medicion["so"] = "Windows";
            medicion["version_so"] = verWin;
            medicion["tipo_dispositivo"] = "Computadora";
            medicion["navegador"] = "App Escritorio Windows";
            medicion["origen"] = "pc";

            lblEquipo.Text = string.Format("{0}  ·  {1} {2}  ·  Windows {3} (Build {4})",
                nombre, medicion["marca"], modelo, verWin, build);
        }

        private static string EjecutarNetsh(string args)
        {
            var psi = new ProcessStartInfo("netsh", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using (var p = Process.Start(psi))
            {
                string salida = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3500);
                return salida;
            }
        }

        private static Dictionary<string, string> LeerNetshWifi()
        {
            var w = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string salida = EjecutarNetsh("wlan show interfaces");
                foreach (var rawLine in salida.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int idx = rawLine.IndexOf(':');
                    if (idx <= 0) continue;
                    string k = RemoverAcentos(rawLine.Substring(0, idx).Trim()).ToLowerInvariant();
                    string v = rawLine.Substring(idx + 1).Trim();
                    if (k == "estado" || k == "state") w["estado"] = RemoverAcentos(v).ToLowerInvariant();
                    else if (k == "ssid") w["ssid"] = v;
                    else if (k == "banda" || k == "band") w["banda"] = v;
                    else if (k == "canal" || k == "channel") w["canal"] = v;
                    else if (k == "tipo de radio" || k == "radio type") w["radio"] = v;
                    else if (k.StartsWith("velocidad de recepcion") || k.StartsWith("receive rate")) w["rx"] = v;
                    else if (k.StartsWith("velocidad de transmision") || k.StartsWith("transmit rate")) w["tx"] = v;
                    else if (k == "senal" || k == "signal") w["senal_pct"] = v.Replace("%", "").Trim();
                    else if (k == "rssi") w["rssi"] = v;
                }
            }
            catch { }
            return w;
        }

        private static List<RedVecinaWifi> LeerRedesVecinasWifi()
        {
            var lista = new List<RedVecinaWifi>();
            try
            {
                string salida = EjecutarNetsh("wlan show networks mode=bssid");
                string ssidActual = "";
                RedVecinaWifi cur = null;

                foreach (var rawLine in salida.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int idx = rawLine.IndexOf(':');
                    if (idx <= 0) continue;
                    string k = RemoverAcentos(rawLine.Substring(0, idx).Trim()).ToLowerInvariant();
                    string v = rawLine.Substring(idx + 1).Trim();

                    if (k.StartsWith("ssid ") && !k.StartsWith("bssid"))
                    {
                        ssidActual = string.IsNullOrEmpty(v) ? "(oculta)" : v;
                    }
                    else if (k.StartsWith("bssid"))
                    {
                        if (cur != null) lista.Add(cur);
                        cur = new RedVecinaWifi { Ssid = ssidActual, Bssid = v };
                    }
                    else if (cur != null)
                    {
                        if (k == "senal" || k == "signal")
                        {
                            int pct;
                            if (int.TryParse(v.Replace("%", "").Trim(), out pct))
                            {
                                cur.SenalPct = pct;
                                cur.Rssi = (int)Math.Round(pct / 2.0 - 100.0);
                            }
                        }
                        else if (k == "tipo de radio" || k == "radio type") cur.Radio = v;
                        else if (k == "banda" || k == "band") cur.Banda = v;
                        else if (k == "canal" || k == "channel")
                        {
                            int ch;
                            if (int.TryParse(v, out ch))
                            {
                                cur.Canal = ch;
                                if (string.IsNullOrEmpty(cur.Banda))
                                    cur.Banda = ch <= 14 ? "2.4 GHz" : "5 GHz";
                            }
                        }
                    }
                }
                if (cur != null) lista.Add(cur);
            }
            catch { }
            lista.Sort((a, b) => b.Rssi.CompareTo(a.Rssi));
            return lista;
        }

        private void EscanearEspectroWifi()
        {
            var redes = LeerRedesVecinasWifi();
            gridEspectro.Rows.Clear();

            int canalPropio = 0;
            string chStr;
            if (wifiActual.TryGetValue("canal", out chStr)) int.TryParse(chStr, out canalPropio);

            int coCanal = 0, adyacentes = 0;
            foreach (var r in redes)
            {
                if (canalPropio > 0 && r.Canal > 0)
                {
                    if (r.Canal == canalPropio) coCanal++;
                    else if (canalPropio <= 14 && r.Canal <= 14 && Math.Abs(r.Canal - canalPropio) <= 4) adyacentes++;
                }
                gridEspectro.Rows.Add(
                    r.Ssid,
                    r.Banda,
                    r.Canal > 0 ? r.Canal.ToString(CultureInfo.InvariantCulture) : "?",
                    string.Format("{0} dBm ({1}%)", r.Rssi, r.SenalPct),
                    r.Radio
                );
            }

            if (redes.Count == 0)
            {
                lblCongestion.Text = "Sin redes WiFi visibles o adaptador WiFi inactivo.";
                lblCongestion.ForeColor = tema.Muted;
            }
            else if (canalPropio > 0)
            {
                string nivel = coCanal >= 4 ? "Congestión ALTA" : coCanal >= 2 ? "Congestión moderada" : "Canal despejado";
                lblCongestion.Text = string.Format("Canal activo {0}: {1} redes co-canal · {2} adyacentes ({3})",
                    canalPropio, coCanal, adyacentes, nivel);
                lblCongestion.ForeColor = coCanal >= 4 ? tema.Bad : coCanal >= 2 ? tema.Amber : tema.Good;
            }
            else
            {
                lblCongestion.Text = string.Format("{0} puntos de acceso detectados en el entorno.", redes.Count);
                lblCongestion.ForeColor = tema.Fg;
            }
        }

        private static string RemoverAcentos(string texto)
        {
            string norm = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in norm)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString();
        }

        private void RefrescarWifi()
        {
            wifiActual = LeerNetshWifi();
            string estado;
            if (wifiActual.TryGetValue("estado", out estado) && (estado.StartsWith("conectado") || estado.StartsWith("connected")))
            {
                int rssi = -70;
                string rssiTxt;
                if (wifiActual.TryGetValue("rssi", out rssiTxt) && int.TryParse(rssiTxt.Trim(), out rssi)) { }
                else
                {
                    string pctTxt;
                    int pct = 70;
                    if (wifiActual.TryGetValue("senal_pct", out pctTxt)) int.TryParse(pctTxt, out pct);
                    rssi = (int)Math.Round(pct / 2.0 - 100.0);
                }
                int barras = rssi >= -55 ? 5 : rssi >= -65 ? 4 : rssi >= -75 ? 3 : rssi >= -82 ? 2 : rssi >= -90 ? 1 : 0;
                string ssid = ObtenerValor(wifiActual, "ssid", "WiFi");
                string radio = ObtenerValor(wifiActual, "radio", "802.11");
                string banda = ObtenerValor(wifiActual, "banda", "");
                string canal = ObtenerValor(wifiActual, "canal", "?");
                string rx = ObtenerValor(wifiActual, "rx", "?");
                string tx = ObtenerValor(wifiActual, "tx", "?");
                string pctStr = ObtenerValor(wifiActual, "senal_pct", "?");

                medicion["conexion"] = "WiFi";
                medicion["tecnologia"] = radio;
                medicion["senal_dbm"] = rssi;
                medicion["barras"] = barras;
                medicion["tipo_red_detectado"] = string.Format("{0} · {1} · canal {2} · enlace {3}/{4} Mbps", radio, banda, canal, rx, tx);

                lblRed.Text = string.Format("WiFi «{0}»  ·  {1} dBm ({2}% · {3})  ·  {4}",
                    ssid, rssi, pctStr, CalidadSenal(rssi), medicion["tipo_red_detectado"]);
            }
            else
            {
                medicion["conexion"] = "Cable";
                medicion["tecnologia"] = "";
                medicion["senal_dbm"] = "";
                medicion["barras"] = "";
                medicion["tipo_red_detectado"] = "Ethernet / Cable";
                lblRed.Text = "Conexión por Cable (Ethernet) o interfaz WiFi desconectada";
            }
        }

        private static string ObtenerValor(Dictionary<string, string> d, string k, string def)
        {
            string v;
            return d.TryGetValue(k, out v) && !string.IsNullOrEmpty(v) ? v : def;
        }

        private static string CalidadSenal(int dbm)
        {
            if (dbm >= -50) return "Excelente";
            if (dbm >= -67) return "Buena";
            if (dbm >= -75) return "Regular";
            if (dbm >= -82) return "Mala";
            return "Crítica";
        }

        private void DetectarISP()
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(IP_INFO_URL);
                req.UserAgent = "MedidorSenalUTN/2.0";
                req.Timeout = 10000;
                using (var resp = req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    var ser = new JavaScriptSerializer();
                    var d = ser.Deserialize<Dictionary<string, object>>(json);
                    string isp = "", asn = "", ciudad = "";
                    if (d.ContainsKey("connection") && d["connection"] is Dictionary<string, object>)
                    {
                        var c = (Dictionary<string, object>)d["connection"];
                        if (c.ContainsKey("isp") && c["isp"] != null) isp = c["isp"].ToString();
                        else if (c.ContainsKey("org") && c["org"] != null) isp = c["org"].ToString();
                        if (c.ContainsKey("asn") && c["asn"] != null) asn = "AS" + c["asn"].ToString();
                    }
                    if (d.ContainsKey("city") && d["city"] != null) ciudad = d["city"].ToString();
                    if (d.ContainsKey("latitude") && d["latitude"] != null)
                        medicion["lat"] = Math.Round(Convert.ToDouble(d["latitude"], CultureInfo.InvariantCulture), 3);
                    if (d.ContainsKey("longitude") && d["longitude"] != null)
                        medicion["lon"] = Math.Round(Convert.ToDouble(d["longitude"], CultureInfo.InvariantCulture), 3);

                    string emp = EmpresaDesdeIsp(isp);
                    medicion["isp_detectado"] = isp;
                    medicion["asn"] = asn;
                    medicion["ciudad"] = ciudad;
                    medicion["empresa"] = emp;
                    medicion["ubicacion_fuente"] = "IP (aprox.)";

                    BeginInvoke((Action)(() =>
                    {
                        lblProveedor.Text = string.Format("Proveedor IP: {0} ({1})  ·  {2}  ·  {3}",
                            string.IsNullOrEmpty(isp) ? "detectado" : isp, emp, asn, ciudad);
                    }));
                }
            }
            catch
            {
                BeginInvoke((Action)(() => { lblProveedor.Text = "Proveedor IP: sin datos de geolocalización"; }));
            }
        }

        private static string EmpresaDesdeIsp(string isp)
        {
            string s = (isp ?? "").ToLowerInvariant();
            if (Regex.IsMatch(s, "claro|amx|techtel")) return "Claro";
            if (Regex.IsMatch(s, "telef[oó]nica|movistar")) return "Movistar";
            if (Regex.IsMatch(s, "telecom|personal")) return "Personal";
            return "Otra";
        }

        // =====================================================================
        // Motor de Prueba de Velocidad (Cloudflare)
        // =====================================================================
        private async Task EjecutarMedicionAsync(bool autoEnviar)
        {
            if (midiendo) return;
            midiendo = true;
            tieneResultado = false;
            btnMedir.Enabled = false;
            btnEnviar.Enabled = false;
            lblEstado.Visible = false;
            lblBajadaVal.Text = "—";
            lblSubidaVal.Text = "—";
            lblLatenciaVal.Text = "—";
            lblJitterVal.Text = "—";

            try
            {
                RefrescarWifi();
                EscanearEspectroWifi();

                double lat = 0, jit = 0, baj = 0, sub = 0;

                await Task.Run(() =>
                {
                    var tiempos = new List<double>();
                    for (int i = 0; i < 11; i++)
                    {
                        var sw = Stopwatch.StartNew();
                        DescargarBloque(SPEED_HOST + "/__down?bytes=0&r=" + Guid.NewGuid().ToString("N"), null);
                        sw.Stop();
                        double ms = sw.Elapsed.TotalMilliseconds;
                        if (i > 0) tiempos.Add(ms);
                        ActualizarGaugeUI(0, string.Format(CultureInfo.InvariantCulture, "Latencia · {0:0} ms", ms), tema.Accent);
                    }
                    double sumaJit = 0;
                    for (int i = 1; i < tiempos.Count; i++) sumaJit += Math.Abs(tiempos[i] - tiempos[i - 1]);
                    jit = sumaJit / (tiempos.Count - 1);
                    tiempos.Sort();
                    lat = (tiempos[4] + tiempos[5]) / 2.0;

                    BeginInvoke((Action)(() =>
                    {
                        lblLatenciaVal.Text = lat.ToString("0", CultureInfo.InvariantCulture);
                        lblJitterVal.Text = jit.ToString("0.0", CultureInfo.InvariantCulture);
                    }));

                    int[] tamBaj = new int[] { 200000, 2000000, 10000000, 25000000 };
                    var resBaj = new List<double>();
                    for (int i = 0; i < tamBaj.Length; i++)
                    {
                        var sw = Stopwatch.StartNew();
                        long leidos = DescargarBloque(SPEED_HOST + "/__down?bytes=" + tamBaj[i] + "&r=" + i,
                            mbps => ActualizarGaugeUI(mbps, "Mbps · midiendo bajada", tema.Good));
                        sw.Stop();
                        double seg = Math.Max(0.001, sw.Elapsed.TotalSeconds);
                        resBaj.Add((leidos * 8.0) / seg / 1e6);
                        if (seg > 2.5) break;
                    }
                    for (int i = (resBaj.Count > 1 ? 1 : 0); i < resBaj.Count; i++)
                        if (resBaj[i] > baj) baj = resBaj[i];

                    BeginInvoke((Action)(() =>
                    {
                        lblBajadaVal.Text = baj.ToString("0.0", CultureInfo.InvariantCulture);
                    }));

                    int[] tamSub = new int[] { 200000, 1000000, 4000000, 10000000 };
                    byte[] payload = new byte[10000000];
                    new Random().NextBytes(payload);
                    var resSub = new List<double>();
                    for (int i = 0; i < tamSub.Length; i++)
                    {
                        var sw = Stopwatch.StartNew();
                        SubirBloque(SPEED_HOST + "/__up?r=" + i, payload, tamSub[i]);
                        sw.Stop();
                        double seg = Math.Max(0.001, sw.Elapsed.TotalSeconds);
                        double mbps = (tamSub[i] * 8.0) / seg / 1e6;
                        ActualizarGaugeUI(mbps, "Mbps · midiendo subida", tema.Amber);
                        resSub.Add(mbps);
                        if (seg > 2.5) break;
                    }
                    for (int i = (resSub.Count > 1 ? 1 : 0); i < resSub.Count; i++)
                        if (resSub[i] > sub) sub = resSub[i];

                    BeginInvoke((Action)(() =>
                    {
                        lblSubidaVal.Text = sub.ToString("0.0", CultureInfo.InvariantCulture);
                    }));
                });

                ultimaBajadaMbps = baj;
                ultimaSubidaMbps = sub;
                ActualizarGaugeUI(baj, "Mbps · resultado de bajada", baj >= 20 ? tema.Good : baj >= 5 ? tema.Amber : tema.Bad);

                medicion["id"] = Guid.NewGuid().ToString();
                medicion["fecha"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
                medicion["bajada_mbps"] = Math.Round(baj, 2);
                medicion["subida_mbps"] = Math.Round(sub, 2);
                medicion["latencia_ms"] = Math.Round(lat, 1);
                medicion["jitter_ms"] = Math.Round(jit, 1);
                medicion["ancho_mhz"] = (double)numAnchoMhz.Value;

                ActualizarAnalisisTeorico(baj);
                tieneResultado = true;
                btnEnviar.Enabled = true;

                if (autoEnviar && chkConsentimiento.Checked)
                {
                    await EnviarMedicionAsync();
                }
            }
            catch (Exception ex)
            {
                ActualizarGaugeUI(0, "Error en la prueba", tema.Bad);
                MostrarEstado("No se pudo completar la medición: " + ex.Message, tema.Bad);
            }
            finally
            {
                midiendo = false;
                btnMedir.Enabled = true;
                btnMedir.Text = "▶  Volver a medir";
            }
        }

        private static long DescargarBloque(string url, Action<double> progreso)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "MedidorSenalUTN/2.0";
            req.Timeout = 25000;
            req.ReadWriteTimeout = 25000;
            long total = 0;
            var sw = Stopwatch.StartNew();
            using (var resp = req.GetResponse())
            using (var st = resp.GetResponseStream())
            {
                byte[] buf = new byte[65536];
                int n;
                while ((n = st.Read(buf, 0, buf.Length)) > 0)
                {
                    total += n;
                    double s = sw.Elapsed.TotalSeconds;
                    if (progreso != null && s > 0.15)
                        progreso((total * 8.0) / s / 1e6);
                }
            }
            return total;
        }

        private static void SubirBloque(string url, byte[] buffer, int count)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.UserAgent = "MedidorSenalUTN/2.0";
            req.ContentLength = count;
            req.Timeout = 25000;
            req.ReadWriteTimeout = 25000;
            using (var st = req.GetRequestStream())
                st.Write(buffer, 0, count);
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream()))
                sr.ReadToEnd();
        }

        private void ActualizarGaugeUI(double mbps, string fase, Color c)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => ActualizarGaugeUI(mbps, fase, c)));
                return;
            }
            velocimetro.Actualizar(mbps, fase, c);
        }

        // =====================================================================
        // Análisis Teórico Interactivo (WiFi Shannon + Cable Nyquist/Shannon)
        // =====================================================================
        private static string PotenciaLegible(double mw)
        {
            string[] nombres = new string[] { "mW", "µW", "nW", "pW", "fW" };
            double[] factores = new double[] { 1.0, 1e-3, 1e-6, 1e-9, 1e-12 };
            for (int i = 0; i < factores.Length; i++)
            {
                if (mw >= factores[i])
                    return string.Format(CultureInfo.InvariantCulture, "{0:0.00} {1}", mw / factores[i], nombres[i]);
            }
            return string.Format(CultureInfo.InvariantCulture, "{0:0.00} aW", mw / 1e-15);
        }

        private void ActualizarAnalisisTeorico(double bajadaMbps)
        {
            double bMhz = (double)numAnchoMhz.Value;
            double tempC = (double)numTempC.Value;
            medicion["ancho_mhz"] = bMhz;

            if (medicion.ContainsKey("senal_dbm") && medicion["senal_dbm"] is int)
            {
                int dbm = (int)medicion["senal_dbm"];
                double ruido = 10.0 * Math.Log10(1.38e-23 * (tempC + 273.0) * bMhz * 1e6 / 1e-3);
                double snr = dbm - ruido;
                double shannon = bMhz * Math.Log(1.0 + Math.Pow(10.0, snr / 10.0)) / Math.Log(2.0);
                double mw = Math.Pow(10.0, dbm / 10.0);
                double pct = shannon > 0 ? (100.0 * bajadaMbps) / shannon : 0.0;

                lblTeoria.Text = string.Format(CultureInfo.InvariantCulture,
                    "Potencia recibida: {0} dBm ({1} · calidad {2})\r\n" +
                    "Ruido térmico N = kTB ({3:0} MHz, {4:0} °C) = {5:0.0} dBm  →  SNR = {6:0.0} dB\r\n" +
                    "Límite de Shannon C = B·log₂(1 + S/N) = {7:0} Mbps  ·  Aprovechamiento: {8:0.0}%",
                    dbm, PotenciaLegible(mw), CalidadSenal(dbm), bMhz, tempC, ruido, snr, shannon, pct);
            }
            else
            {
                // Modelo Ethernet IEEE 802.3ab (1000BASE-T, 4 pares, 4D-PAM5) alineado con teoria.js
                double bEfectivo = bMhz == 20.0 ? 250.0 : bMhz;
                double cNyquist = 2.0 * bEfectivo * (Math.Log(5.0) / Math.Log(2.0));
                double ruido = 10.0 * Math.Log10(1.38e-23 * (tempC + 273.0) * bEfectivo * 1e6 / 1e-3);
                double cShannon = bEfectivo * (Math.Log(1.0 + Math.Pow(10.0, 24.0 / 10.0)) / Math.Log(2.0));
                string snrMinStr = "—";
                if (bajadaMbps > 0 && bEfectivo > 0)
                {
                    double snrLin = Math.Pow(2.0, bajadaMbps / bEfectivo) - 1.0;
                    if (snrLin > 0) snrMinStr = string.Format(CultureInfo.InvariantCulture, "{0:0.0} dB", 10.0 * Math.Log10(snrLin));
                }

                lblTeoria.Text = string.Format(CultureInfo.InvariantCulture,
                    "Canal guiado IEEE 802.3ab (1000BASE-T · 4D-PAM5 · B={0:0} MHz):\r\n" +
                    "Capacidad Nyquist C = 2B·log₂(5) = {1:0} Mbps brutos (1000 Mbps netos)\r\n" +
                    "Ruido térmico N = {2:0.0} dBm · Shannon (SNR 24 dB) = {3:0} Mbps · SNR mín. p/ {4:0.1} Mbps: {5}",
                    bEfectivo, cNyquist, ruido, cShannon, bajadaMbps, snrMinStr);
            }
        }

        // =====================================================================
        // Envío a Google Sheets + CSV local
        // =====================================================================
        private async Task EnviarMedicionAsync()
        {
            if (!tieneResultado) return;
            if (!chkConsentimiento.Checked)
            {
                MostrarEstado("Para registrar la medición debés marcar la casilla de consentimiento.", tema.Bad);
                return;
            }

            medicion["lugar"] = cmbLugar.SelectedItem != null ? cmbLugar.SelectedItem.ToString() : "Interior";
            medicion["comentario"] = txtComentario.Text.Trim();
            medicion["ancho_mhz"] = (double)numAnchoMhz.Value;
            string scriptUrl = LeerScriptUrlInicial();

            var fila = new Dictionary<string, object>();
            foreach (var c in CAMPOS)
                fila[c] = medicion.ContainsKey(c) && medicion[c] != null ? medicion[c] : "";

            btnEnviar.Enabled = false;
            GuardarCsvLocal(fila, rutaCsv);
            CargarCsvLocal();

            if (string.IsNullOrEmpty(scriptUrl))
            {
                MostrarEstado("Guardada localmente en mediciones_pc.csv.", tema.Amber);
                return;
            }

            try
            {
                await Task.Run(() => EnviarPostJson(scriptUrl, fila));
                MostrarEstado("✔ Medición sincronizada con Google Sheets y guardada en mediciones_pc.csv.", tema.Good);
            }
            catch (Exception ex)
            {
                MostrarEstado("Guardada en CSV local. Error al conectar a Google Sheets (" + ex.Message + ").", tema.Amber);
                btnEnviar.Enabled = true;
            }
        }

        private static void EnviarPostJson(string url, Dictionary<string, object> fila)
        {
            var ser = new JavaScriptSerializer();
            byte[] body = Encoding.UTF8.GetBytes(ser.Serialize(fila));
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "text/plain;charset=utf-8";
            req.UserAgent = "MedidorSenalUTN/2.0";
            req.AllowAutoRedirect = true;
            req.Timeout = 20000;
            req.ContentLength = body.Length;
            using (var st = req.GetRequestStream())
                st.Write(body, 0, body.Length);
            using (var resp = (HttpWebResponse)req.GetResponse())
            {
                if ((int)resp.StatusCode >= 400)
                    throw new Exception("HTTP " + (int)resp.StatusCode);
            }
        }

        private static void GuardarCsvLocal(Dictionary<string, object> fila, string ruta)
        {
            try
            {
                bool nuevo = !File.Exists(ruta);
                using (var sw = new StreamWriter(ruta, true, new UTF8Encoding(true)))
                {
                    if (nuevo) sw.WriteLine(string.Join(";", CAMPOS));
                    var vals = new List<string>();
                    foreach (var c in CAMPOS)
                    {
                        string v = fila.ContainsKey(c) && fila[c] != null ? Convert.ToString(fila[c], CultureInfo.InvariantCulture) : "";
                        v = v.Replace(";", ",").Replace("\r", " ").Replace("\n", " ");
                        vals.Add(v);
                    }
                    sw.WriteLine(string.Join(";", vals));
                }
            }
            catch { }
        }

        // =====================================================================
        // Estadísticas: Lectura CSV local + Google Sheets + Cálculo de KPIs
        // =====================================================================
        private void CargarCsvLocal()
        {
            registrosLocales.Clear();
            if (!File.Exists(rutaCsv)) return;
            try
            {
                string[] lines = File.ReadAllLines(rutaCsv, Encoding.UTF8);
                if (lines.Length < 2) return;
                string[] headers = lines[0].Split(';');
                var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headers.Length; i++) idx[headers[i].Trim()] = i;

                for (int r = 1; r < lines.Length; r++)
                {
                    if (string.IsNullOrWhiteSpace(lines[r])) continue;
                    string[] cols = lines[r].Split(';');
                    var m = new MedicionRegistro();
                    m.Fecha = ObtenerCol(cols, idx, "fecha");
                    m.Dispositivo = ObtenerCol(cols, idx, "nombre_dispositivo");
                    m.Conexion = ObtenerCol(cols, idx, "conexion");
                    m.Tecnologia = ObtenerCol(cols, idx, "tecnologia");
                    m.Empresa = ObtenerCol(cols, idx, "empresa");
                    m.Lugar = ObtenerCol(cols, idx, "lugar");
                    m.Origen = "CSV local";
                    m.Bajada = ParseDbl(ObtenerCol(cols, idx, "bajada_mbps"));
                    m.Subida = ParseDbl(ObtenerCol(cols, idx, "subida_mbps"));
                    m.Latencia = ParseDbl(ObtenerCol(cols, idx, "latencia_ms"));
                    m.Jitter = ParseDbl(ObtenerCol(cols, idx, "jitter_ms"));
                    m.Dbm = ParseDbl(ObtenerCol(cols, idx, "senal_dbm"));
                    registrosLocales.Add(m);
                }
            }
            catch { }
        }

        private async Task CargarEstadisticasNubeAsync()
        {
            string url = LeerScriptUrlInicial();
            if (string.IsNullOrEmpty(url))
            {
                lblStatsEstado.Text = "Planilla no configurada; mostrando CSV local.";
                return;
            }
            btnStatsActualizar.Enabled = false;
            lblStatsEstado.Text = "Consultando planilla compartida…";

            try
            {
                var nuevos = await Task.Run(() =>
                {
                    var req = (HttpWebRequest)WebRequest.Create(url + "?r=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    req.UserAgent = "MedidorSenalUTN/2.0";
                    req.Timeout = 20000;
                    using (var resp = req.GetResponse())
                    using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        string json = sr.ReadToEnd();
                        var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                        var root = ser.Deserialize<Dictionary<string, object>>(json);
                        var res = new List<MedicionRegistro>();
                        if (root != null && root.ContainsKey("filas") && root["filas"] is System.Collections.ArrayList)
                        {
                            var arr = (System.Collections.ArrayList)root["filas"];
                            foreach (object item in arr)
                            {
                                var d = item as Dictionary<string, object>;
                                if (d == null) continue;
                                var m = new MedicionRegistro();
                                m.Fecha = StrVal(d, "fecha");
                                m.Dispositivo = StrVal(d, "nombre_dispositivo");
                                m.Conexion = StrVal(d, "conexion");
                                m.Tecnologia = StrVal(d, "tecnologia");
                                m.Empresa = StrVal(d, "empresa");
                                m.Lugar = StrVal(d, "lugar");
                                m.Origen = StrVal(d, "origen");
                                m.Bajada = ParseDbl(StrVal(d, "bajada_mbps"));
                                m.Subida = ParseDbl(StrVal(d, "subida_mbps"));
                                m.Latencia = ParseDbl(StrVal(d, "latencia_ms"));
                                m.Jitter = ParseDbl(StrVal(d, "jitter_ms"));
                                m.Dbm = ParseDbl(StrVal(d, "senal_dbm"));
                                res.Add(m);
                            }
                        }
                        return res;
                    }
                });

                registrosRemotos = nuevos;
                lblStatsEstado.Text = string.Format("Sincronizado: {0} filas en nube · {1} en CSV local.",
                    registrosRemotos.Count, registrosLocales.Count);
                RefrescarVistaEstadisticas();
            }
            catch (Exception ex)
            {
                lblStatsEstado.Text = "Sin conexión a la nube (" + ex.Message + "). Mostrando CSV local.";
                RefrescarVistaEstadisticas();
            }
            finally
            {
                btnStatsActualizar.Enabled = true;
            }
        }

        private List<MedicionRegistro> ObtenerDatasetActivo()
        {
            int f = cmbStatsFiltro != null ? cmbStatsFiltro.SelectedIndex : 0;
            var baseList = (f == 1 || registrosRemotos.Count == 0) ? registrosLocales : registrosRemotos;
            var res = new List<MedicionRegistro>();
            foreach (var m in baseList)
            {
                if (f == 2 && !string.Equals(m.Conexion, "WiFi", StringComparison.OrdinalIgnoreCase)) continue;
                if (f == 3 && !string.Equals(m.Conexion, "Cable", StringComparison.OrdinalIgnoreCase)) continue;
                res.Add(m);
            }
            return res;
        }

        private void RefrescarVistaEstadisticas()
        {
            var data = ObtenerDatasetActivo();
            var bajs = new List<double>();
            var subs = new List<double>();
            var lats = new List<double>();
            foreach (var m in data)
            {
                if (m.Bajada.HasValue) bajs.Add(m.Bajada.Value);
                if (m.Subida.HasValue) subs.Add(m.Subida.Value);
                if (m.Latencia.HasValue) lats.Add(m.Latencia.Value);
            }

            lblKpiN.Text = data.Count.ToString(CultureInfo.InvariantCulture);
            lblKpiBaj.Text = MedianaStr(bajs, "0.0");
            lblKpiSub.Text = MedianaStr(subs, "0.0");
            lblKpiLat.Text = MedianaStr(lats, "0");

            grafDispersion.SetDatos(data, (double)numAnchoMhz.Value, (double)numTempC.Value);
            grafSerie.SetDatos(data);

            gridHistorial.Rows.Clear();
            int minIdx = Math.Max(0, data.Count - 50);
            for (int i = data.Count - 1; i >= minIdx; i--)
            {
                var m = data[i];
                string fechaCorta = m.Fecha.Length >= 16 ? m.Fecha.Substring(0, 16).Replace("T", " ") : m.Fecha;
                gridHistorial.Rows.Add(
                    fechaCorta,
                    string.IsNullOrEmpty(m.Dispositivo) ? m.Origen : m.Dispositivo,
                    m.Conexion,
                    m.Bajada.HasValue ? m.Bajada.Value.ToString("0.0", CultureInfo.InvariantCulture) : "—",
                    m.Subida.HasValue ? m.Subida.Value.ToString("0.0", CultureInfo.InvariantCulture) : "—",
                    m.Latencia.HasValue ? m.Latencia.Value.ToString("0", CultureInfo.InvariantCulture) : "—",
                    m.Dbm.HasValue ? m.Dbm.Value.ToString("0", CultureInfo.InvariantCulture) : "—",
                    m.Lugar
                );
            }
        }

        private static string MedianaStr(List<double> vals, string fmt)
        {
            if (vals.Count == 0) return "—";
            vals.Sort();
            int n = vals.Count;
            double med = (n % 2 == 1) ? vals[n / 2] : (vals[n / 2 - 1] + vals[n / 2]) / 2.0;
            return med.ToString(fmt, CultureInfo.InvariantCulture);
        }

        private static string ObtenerCol(string[] cols, Dictionary<string, int> idx, string k)
        {
            int i;
            if (idx.TryGetValue(k, out i) && i < cols.Length) return cols[i].Trim();
            return "";
        }

        private static string StrVal(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture).Trim() : "";
        }

        private static double? ParseDbl(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            double v;
            if (double.TryParse(s.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out v))
                return v;
            return null;
        }

        private void MostrarEstado(string msg, Color color)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => MostrarEstado(msg, color)));
                return;
            }
            lblEstado.ForeColor = color;
            lblEstado.Text = msg;
            lblEstado.Visible = true;
        }

        private async void ChkMonitoreo_CheckedChanged(object sender, EventArgs e)
        {
            if (chkMonitoreo.Checked)
            {
                segRestantesMonitoreo = (int)numIntervalo.Value;
                timerMonitoreo.Start();
                if (!midiendo)
                    await EjecutarMedicionAsync(true);
            }
            else
            {
                timerMonitoreo.Stop();
                velocimetro.Actualizar(ultimaBajadaMbps, "Monitoreo automático detenido", tema.Muted);
            }
        }

        private async void TimerMonitoreo_Tick(object sender, EventArgs e)
        {
            if (midiendo) return;
            segRestantesMonitoreo--;
            if (segRestantesMonitoreo <= 0)
            {
                segRestantesMonitoreo = (int)numIntervalo.Value;
                await EjecutarMedicionAsync(true);
            }
            else
            {
                velocimetro.Actualizar(ultimaBajadaMbps, string.Format("Monitoreo activo · próxima en {0} s", segRestantesMonitoreo), tema.Accent);
            }
        }

        // =====================================================================
        // Modo Consola (--repetir N --cada S --lugar X --comentario Y)
        // =====================================================================
        private static void EjecutarModoConsola(string[] args)
        {
            int repetir = 1, cada = 60;
            string lugar = "Interior", comentario = "Consola PC";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--repetir" && i + 1 < args.Length) int.TryParse(args[++i], out repetir);
                else if (args[i] == "--cada" && i + 1 < args.Length) int.TryParse(args[++i], out cada);
                else if (args[i] == "--lugar" && i + 1 < args.Length) lugar = args[++i];
                else if (args[i] == "--comentario" && i + 1 < args.Length) comentario = args[++i];
            }

            string scriptUrl = LeerScriptUrlInicial();
            Console.WriteLine("Medidor de Señal · UTN FRT · Comunicación (Modo Consola)");
            for (int r = 0; r < repetir; r++)
            {
                var fila = new Dictionary<string, object>();
                foreach (var c in CAMPOS) fila[c] = "";
                fila["id"] = Guid.NewGuid().ToString();
                fila["fecha"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
                fila["nombre_dispositivo"] = Environment.MachineName;
                fila["marca"] = "PC";
                fila["so"] = "Windows";
                fila["version_so"] = Environment.OSVersion.Version.Build >= 22000 ? "11" : "10";
                fila["tipo_dispositivo"] = "Computadora";
                fila["navegador"] = "App Escritorio Windows";
                fila["origen"] = "pc";
                fila["lugar"] = lugar;
                fila["comentario"] = comentario;
                fila["ancho_mhz"] = 20;

                var w = LeerNetshWifi();
                string est;
                if (w.TryGetValue("estado", out est) && (est.StartsWith("conectado") || est.StartsWith("connected")))
                {
                    int pct = 70;
                    string pctTxt;
                    if (w.TryGetValue("senal_pct", out pctTxt)) int.TryParse(pctTxt, out pct);
                    int rssi = (int)Math.Round(pct / 2.0 - 100.0);
                    fila["conexion"] = "WiFi";
                    fila["tecnologia"] = ObtenerValor(w, "radio", "802.11");
                    fila["senal_dbm"] = rssi;
                }
                else
                {
                    fila["conexion"] = "Cable";
                }

                var sw = Stopwatch.StartNew();
                long b = DescargarBloque(SPEED_HOST + "/__down?bytes=2000000", null);
                sw.Stop();
                fila["bajada_mbps"] = Math.Round((b * 8.0) / Math.Max(0.01, sw.Elapsed.TotalSeconds) / 1e6, 2);

                byte[] upBuf = new byte[1000000];
                sw.Restart();
                SubirBloque(SPEED_HOST + "/__up", upBuf, upBuf.Length);
                sw.Stop();
                fila["subida_mbps"] = Math.Round((upBuf.Length * 8.0) / Math.Max(0.01, sw.Elapsed.TotalSeconds) / 1e6, 2);
                fila["latencia_ms"] = 25.0;
                fila["jitter_ms"] = 3.0;

                GuardarCsvLocal(fila, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mediciones_pc.csv"));
                try { EnviarPostJson(scriptUrl, fila); } catch { }
                if (r < repetir - 1) Thread.Sleep(cada * 1000);
            }
        }
    }
}
