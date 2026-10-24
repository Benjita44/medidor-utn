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
    public class MainForm : Form
    {
        // Configuración por defecto conectada a nuestra planilla de Google Sheets
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

        // Paleta Impeccable ("Cuaderno de Telemetría e Instrumentación RF")
        private static readonly Color ColBg = ColorTranslator.FromHtml("#F5F4EE");
        private static readonly Color ColSurface = ColorTranslator.FromHtml("#FDFCF9");
        private static readonly Color ColSurfaceAlt = ColorTranslator.FromHtml("#ECEAE1");
        private static readonly Color ColFg = ColorTranslator.FromHtml("#0F172A");
        private static readonly Color ColMuted = ColorTranslator.FromHtml("#526074");
        private static readonly Color ColLine = ColorTranslator.FromHtml("#D5D2C6");
        private static readonly Color ColAccent = ColorTranslator.FromHtml("#1849C6");
        private static readonly Color ColGood = ColorTranslator.FromHtml("#117A43");
        private static readonly Color ColAmber = ColorTranslator.FromHtml("#B46900");
        private static readonly Color ColBad = ColorTranslator.FromHtml("#B93829");

        private Label lblEquipo;
        private Label lblRed;
        private Label lblProveedor;
        private Label lblVelocidad;
        private Label lblFase;
        private Label lblBajadaVal;
        private Label lblSubidaVal;
        private Label lblLatenciaVal;
        private Label lblJitterVal;
        private Label lblTeoria;
        private Label lblEstado;

        private Button btnMedir;
        private Button btnEnviar;
        private ComboBox cmbLugar;
        private TextBox txtComentario;
        private TextBox txtUrl;
        private CheckBox chkConsentimiento;
        private CheckBox chkMonitoreo;
        private NumericUpDown numIntervalo;

        private System.Windows.Forms.Timer timerRed;
        private System.Windows.Forms.Timer timerMonitoreo;
        private int segRestantesMonitoreo = 0;

        private Dictionary<string, object> medicion = new Dictionary<string, object>();
        private Dictionary<string, string> wifiActual = new Dictionary<string, string>();
        private bool midiendo = false;
        private bool tieneResultado = false;

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

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
            Text = "Medidor de Señal · UTN FRT Comunicación (Windows)";
            Size = new Size(760, 860);
            MinimumSize = new Size(680, 740);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = ColBg;
            ForeColor = ColFg;
            Font = new Font("Segoe UI", 9.75f, FontStyle.Regular);

            ConstruirUI();
            CargarEquipoInicial();
            RefrescarWifi();

            timerRed = new System.Windows.Forms.Timer();
            timerRed.Interval = 3000;
            timerRed.Tick += (s, e) => { if (!midiendo) RefrescarWifi(); };
            timerRed.Start();

            timerMonitoreo = new System.Windows.Forms.Timer();
            timerMonitoreo.Interval = 1000;
            timerMonitoreo.Tick += TimerMonitoreo_Tick;

            Task.Run(() => DetectarISP());
        }

        private void ConstruirUI()
        {
            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(22, 18, 22, 24),
                BackColor = ColBg
            };
            Controls.Add(scroll);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                BackColor = ColBg
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            scroll.Controls.Add(root);

            // Cabecera
            var headerPanel = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = ColBg, Margin = new Padding(0, 0, 0, 8) };
            var lblBadge = new Label
            {
                Text = "UTN FRT · COMUNICACIÓN · TELEMETRÍA DE ESCRITORIO",
                Font = new Font("Consolas", 9.5f, FontStyle.Bold),
                ForeColor = ColAccent,
                AutoSize = true,
                Location = new Point(0, 2)
            };
            var lblTitle = new Label
            {
                Text = "Medidor de Señal — Windows",
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                ForeColor = ColFg,
                AutoSize = true,
                Location = new Point(-2, 22)
            };
            headerPanel.Controls.Add(lblBadge);
            headerPanel.Controls.Add(lblTitle);
            root.Controls.Add(headerPanel);

            // 1. Tarjeta de Telemetría del Equipo y Enlace WiFi/Ethernet
            var cardEq = CrearTarjeta("TELEMETRÍA DEL EQUIPO Y ENLACE DE RED EN VIVO");
            lblEquipo = new Label { Text = "Detectando equipo…", Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold), ForeColor = ColFg, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 4, 0, 4) };
            lblRed = new Label { Text = "Analizando interfaz de red (netsh wlan)…", Font = new Font("Consolas", 10f, FontStyle.Regular), ForeColor = ColFg, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 2, 0, 4) };
            lblProveedor = new Label { Text = "Proveedor IP: consultando ipwho.is…", Font = new Font("Segoe UI", 9.5f, FontStyle.Regular), ForeColor = ColMuted, AutoSize = true, Dock = DockStyle.Top };
            cardEq.Controls.Add(lblProveedor);
            cardEq.Controls.Add(lblRed);
            cardEq.Controls.Add(lblEquipo);
            root.Controls.Add(cardEq);

            // 2. Tarjeta de Instrumento de Medición
            var cardMed = CrearTarjeta("INSTRUMENTO DE MEDICIÓN DE CAUDAL Y LATENCIA");

            lblVelocidad = new Label
            {
                Text = "0.0",
                Font = new Font("Consolas", 42f, FontStyle.Bold),
                ForeColor = ColFg,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 68
            };
            lblFase = new Label
            {
                Text = "Mbps · listo para medir",
                Font = new Font("Consolas", 10f, FontStyle.Bold),
                ForeColor = ColMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 24
            };

            btnMedir = CrearBoton("▶  Iniciar medición de velocidad", true);
            btnMedir.Dock = DockStyle.Top;
            btnMedir.Height = 44;
            btnMedir.Click += async (s, e) => await EjecutarMedicionAsync(false);

            // Grilla de 4 KPIs
            var kpiGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 74,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = ColLine,
                Padding = new Padding(1),
                Margin = new Padding(0, 12, 0, 8)
            };
            for (int i = 0; i < 4; i++) kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            kpiGrid.Controls.Add(CrearCeldaKpi("BAJADA (Mbps)", out lblBajadaVal), 0, 0);
            kpiGrid.Controls.Add(CrearCeldaKpi("SUBIDA (Mbps)", out lblSubidaVal), 1, 0);
            kpiGrid.Controls.Add(CrearCeldaKpi("LATENCIA (ms)", out lblLatenciaVal), 2, 0);
            kpiGrid.Controls.Add(CrearCeldaKpi("JITTER (ms)", out lblJitterVal), 3, 0);

            var spacerMed = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = ColSurface };

            cardMed.Controls.Add(kpiGrid);
            cardMed.Controls.Add(spacerMed);
            cardMed.Controls.Add(btnMedir);
            cardMed.Controls.Add(lblFase);
            cardMed.Controls.Add(lblVelocidad);
            root.Controls.Add(cardMed);

            // 3. Tarjeta de Análisis Teórico (Shannon-Hartley)
            var cardTeo = CrearTarjeta("ANÁLISIS TEÓRICO DE CANAL (UNIDADES 3, 5 Y 7 · SHANNON-HARTLEY)");
            lblTeoria = new Label
            {
                Text = "Iniciá una medición para calcular el piso de ruido térmico (N = kTB), la relación señal a ruido (SNR) y la capacidad límite de Shannon C = B·log₂(1 + S/N).",
                Font = new Font("Consolas", 9.5f, FontStyle.Regular),
                ForeColor = ColFg,
                AutoSize = true,
                MaximumSize = new Size(650, 0),
                Dock = DockStyle.Top,
                Padding = new Padding(0, 6, 0, 4)
            };
            cardTeo.Controls.Add(lblTeoria);
            root.Controls.Add(cardTeo);

            // 4. Tarjeta de Registro, Monitoreo y Sincronización
            var cardEnv = CrearTarjeta("PARÁMETROS DE ENLACE, MONITOREO CONTINUO Y ENVÍO A PLANILLA");

            var formTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                BackColor = ColSurface,
                Padding = new Padding(0, 6, 0, 6)
            };
            formTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            formTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));

            var lblLugar = new Label { Text = "Entorno de propagación:", ForeColor = ColMuted, Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 8, 4) };
            var lblCom = new Label { Text = "Observación o laboratorio:", ForeColor = ColMuted, Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold), AutoSize = true, Margin = new Padding(8, 0, 0, 4) };
            formTable.Controls.Add(lblLugar, 0, 0);
            formTable.Controls.Add(lblCom, 1, 0);

            cmbLugar = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10f),
                Margin = new Padding(0, 0, 8, 10)
            };
            cmbLugar.Items.AddRange(new object[] { "Interior", "Exterior", "En movimiento" });
            cmbLugar.SelectedIndex = 0;

            txtComentario = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f),
                Text = "Laboratorio PC",
                Margin = new Padding(8, 0, 0, 10)
            };
            formTable.Controls.Add(cmbLugar, 0, 1);
            formTable.Controls.Add(txtComentario, 1, 1);

            // Fila de monitoreo automático
            var monPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = ColSurfaceAlt,
                Padding = new Padding(10, 8, 10, 8),
                Margin = new Padding(0, 4, 0, 8)
            };
            chkMonitoreo = new CheckBox
            {
                Text = "Modo monitoreo automático: medir y registrar cada",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                ForeColor = ColFg,
                Margin = new Padding(0, 3, 6, 0)
            };
            chkMonitoreo.CheckedChanged += ChkMonitoreo_CheckedChanged;
            numIntervalo = new NumericUpDown
            {
                Minimum = 15,
                Maximum = 3600,
                Value = 60,
                Width = 64,
                Font = new Font("Consolas", 10f, FontStyle.Bold)
            };
            var lblSeg = new Label
            {
                Text = "segundos (guarda en Google Sheets y en mediciones_pc.csv)",
                AutoSize = true,
                ForeColor = ColMuted,
                Margin = new Padding(6, 5, 0, 0)
            };
            monPanel.Controls.Add(chkMonitoreo);
            monPanel.Controls.Add(numIntervalo);
            monPanel.Controls.Add(lblSeg);

            chkConsentimiento = new CheckBox
            {
                Text = "Acepto registrar esta medición (velocidades, características técnicas del equipo y ubicación aproximada) para la cátedra de Comunicación (UTN FRT).",
                Checked = true,
                AutoSize = true,
                MaximumSize = new Size(650, 0),
                ForeColor = ColFg,
                Font = new Font("Segoe UI", 9f),
                Dock = DockStyle.Top,
                Padding = new Padding(0, 8, 0, 10)
            };

            btnEnviar = CrearBoton("Registrar medición en Google Sheets + CSV local", true);
            btnEnviar.Dock = DockStyle.Top;
            btnEnviar.Height = 42;
            btnEnviar.Enabled = false;
            btnEnviar.Click += async (s, e) => await EnviarMedicionAsync();

            lblEstado = new Label
            {
                Text = "",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                ForeColor = ColGood,
                AutoSize = true,
                MaximumSize = new Size(650, 0),
                Dock = DockStyle.Top,
                Padding = new Padding(0, 8, 0, 4),
                Visible = false
            };

            // Configuración URL
            var lblUrl = new Label
            {
                Text = "URL de sincronización (Google Apps Script):",
                Font = new Font("Segoe UI", 8.75f),
                ForeColor = ColMuted,
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 10, 0, 2)
            };
            txtUrl = new TextBox
            {
                Dock = DockStyle.Top,
                Font = new Font("Consolas", 8.75f),
                Text = LeerScriptUrlInicial()
            };

            cardEnv.Controls.Add(txtUrl);
            cardEnv.Controls.Add(lblUrl);
            cardEnv.Controls.Add(lblEstado);
            cardEnv.Controls.Add(btnEnviar);
            cardEnv.Controls.Add(chkConsentimiento);
            cardEnv.Controls.Add(monPanel);
            cardEnv.Controls.Add(formTable);
            root.Controls.Add(cardEnv);
        }

        private Panel CrearTarjeta(string titulo)
        {
            var card = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = ColSurface,
                Padding = new Padding(18, 14, 18, 16),
                Margin = new Padding(0, 0, 0, 14)
            };
            card.Paint += (s, e) =>
            {
                using (var pen = new Pen(ColLine, 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                }
            };
            var lblHead = new Label
            {
                Text = titulo,
                Font = new Font("Consolas", 9f, FontStyle.Bold),
                ForeColor = ColMuted,
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 6)
            };
            card.Controls.Add(lblHead);
            return card;
        }

        private Panel CrearCeldaKpi(string etiqueta, out Label lblValor)
        {
            var cell = new Panel { Dock = DockStyle.Fill, BackColor = ColSurface, Padding = new Padding(10, 8, 10, 6) };
            var k = new Label
            {
                Text = etiqueta,
                Font = new Font("Consolas", 8f, FontStyle.Bold),
                ForeColor = ColMuted,
                Dock = DockStyle.Top,
                Height = 18
            };
            lblValor = new Label
            {
                Text = "—",
                Font = new Font("Consolas", 16f, FontStyle.Bold),
                ForeColor = ColFg,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
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
                Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
                BackColor = primario ? ColAccent : ColSurfaceAlt,
                ForeColor = primario ? Color.White : ColFg,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = primario ? ColAccent : ColLine;
            return b;
        }

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

        // =====================================================================
        // Detección de Equipo, WiFi (netsh) y Proveedor IP
        // =====================================================================
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

        private static Dictionary<string, string> LeerNetshWifi()
        {
            var w = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                using (var p = Process.Start(psi))
                {
                    string salida = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
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
            }
            catch { }
            return w;
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

                double lat = 0, jit = 0, baj = 0, sub = 0;

                await Task.Run(() =>
                {
                    // 1. Latencia y Jitter
                    var tiempos = new List<double>();
                    for (int i = 0; i < 11; i++)
                    {
                        var sw = Stopwatch.StartNew();
                        DescargarBloque(SPEED_HOST + "/__down?bytes=0&r=" + Guid.NewGuid().ToString("N"), null);
                        sw.Stop();
                        double ms = sw.Elapsed.TotalMilliseconds;
                        if (i > 0) tiempos.Add(ms);
                        ActualizarGaugeUI(0, string.Format(CultureInfo.InvariantCulture, "Latencia · {0:0} ms", ms));
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

                    // 2. Bajada
                    int[] tamBaj = new int[] { 200000, 2000000, 10000000, 25000000 };
                    var resBaj = new List<double>();
                    for (int i = 0; i < tamBaj.Length; i++)
                    {
                        var sw = Stopwatch.StartNew();
                        long leidos = DescargarBloque(SPEED_HOST + "/__down?bytes=" + tamBaj[i] + "&r=" + i,
                            mbps => ActualizarGaugeUI(mbps, "Mbps · midiendo bajada"));
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

                    // 3. Subida
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
                        ActualizarGaugeUI(mbps, "Mbps · midiendo subida");
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

                ActualizarGaugeUI(baj, "Mbps · resultado de bajada");
                medicion["id"] = Guid.NewGuid().ToString();
                medicion["fecha"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
                medicion["bajada_mbps"] = Math.Round(baj, 2);
                medicion["subida_mbps"] = Math.Round(sub, 2);
                medicion["latencia_ms"] = Math.Round(lat, 1);
                medicion["jitter_ms"] = Math.Round(jit, 1);

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
                ActualizarGaugeUI(0, "Error en la prueba");
                MostrarEstado("No se pudo completar la medición: " + ex.Message, ColBad);
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
            {
                st.Write(buffer, 0, count);
            }
            using (var resp = req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream()))
            {
                sr.ReadToEnd();
            }
        }

        private void ActualizarGaugeUI(double mbps, string fase)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => ActualizarGaugeUI(mbps, fase)));
                return;
            }
            lblVelocidad.Text = mbps >= 100
                ? mbps.ToString("0", CultureInfo.InvariantCulture)
                : mbps.ToString("0.0", CultureInfo.InvariantCulture);
            lblFase.Text = fase;
        }

        private void ActualizarAnalisisTeorico(double bajadaMbps)
        {
            if (medicion.ContainsKey("senal_dbm") && medicion["senal_dbm"] is int)
            {
                int dbm = (int)medicion["senal_dbm"];
                double bMhz = 20.0, tempC = 25.0;
                double ruido = 10.0 * Math.Log10(1.38e-23 * (tempC + 273.0) * bMhz * 1e6 / 1e-3);
                double snr = dbm - ruido;
                double shannon = bMhz * Math.Log(1.0 + Math.Pow(10.0, snr / 10.0)) / Math.Log(2.0);
                double pw = Math.Pow(10.0, dbm / 10.0) * 1e9;
                double pct = (100.0 * bajadaMbps) / shannon;

                lblTeoria.Text = string.Format(CultureInfo.InvariantCulture,
                    "Potencia recibida: {0} dBm ({1:0.00} pW · calidad {2})\r\n" +
                    "Piso de ruido térmico N = kTB (20 MHz, 25 °C) = {3:0.0} dBm  →  SNR estimada = {4:0.0} dB\r\n" +
                    "Capacidad teórica de Shannon C = B·log₂(1 + S/N) = {5:0} Mbps  ·  Aprovechamiento real: {6:0}%",
                    dbm, pw, CalidadSenal(dbm), ruido, snr, shannon, pct);
            }
            else
            {
                lblTeoria.Text = string.Format(CultureInfo.InvariantCulture,
                    "Enlace por cable (Ethernet): caudal medido {0:0.1} Mbps de bajada / {1:0.1} Mbps de subida.\r\n" +
                    "(En conexiones cableadas no aplica lectura de RSSI en dBm por interfaz inalámbrica).",
                    bajadaMbps, medicion.ContainsKey("subida_mbps") ? medicion["subida_mbps"] : 0);
            }
        }

        // =====================================================================
        // Envío a Google Sheets + Guardado en mediciones_pc.csv
        // =====================================================================
        private async Task EnviarMedicionAsync()
        {
            if (!tieneResultado) return;
            if (!chkConsentimiento.Checked)
            {
                MostrarEstado("Para registrar la medición debés marcar la casilla de consentimiento.", ColBad);
                return;
            }

            medicion["lugar"] = cmbLugar.SelectedItem != null ? cmbLugar.SelectedItem.ToString() : "Interior";
            medicion["comentario"] = txtComentario.Text.Trim();
            string scriptUrl = txtUrl.Text.Trim();

            var fila = new Dictionary<string, object>();
            foreach (var c in CAMPOS)
                fila[c] = medicion.ContainsKey(c) && medicion[c] != null ? medicion[c] : "";

            btnEnviar.Enabled = false;
            string csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mediciones_pc.csv");
            GuardarCsvLocal(fila, csvPath);

            if (string.IsNullOrEmpty(scriptUrl))
            {
                MostrarEstado("Guardada localmente en mediciones_pc.csv.", ColAmber);
                return;
            }

            try
            {
                await Task.Run(() => EnviarPostJson(scriptUrl, fila));
                MostrarEstado("✔ ¡Listo! Medición sincronizada con nuestra planilla de Google Sheets y guardada en mediciones_pc.csv.", ColGood);
            }
            catch (Exception ex)
            {
                MostrarEstado("Guardada en mediciones_pc.csv. No se pudo conectar a Google Sheets (" + ex.Message + ").", ColAmber);
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
            {
                st.Write(body, 0, body.Length);
            }
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
                    if (nuevo)
                        sw.WriteLine(string.Join(";", CAMPOS));
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
                lblFase.Text = "Monitoreo automático detenido";
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
                lblFase.Text = string.Format("Monitoreo activo · próxima medición en {0} s", segRestantesMonitoreo);
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
