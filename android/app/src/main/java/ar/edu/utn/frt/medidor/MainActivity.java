package ar.edu.utn.frt.medidor;

import android.Manifest;
import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.content.res.ColorStateList;
import android.content.res.Configuration;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.location.Location;
import android.location.LocationListener;
import android.location.LocationManager;
import android.net.ConnectivityManager;
import android.net.Network;
import android.net.NetworkCapabilities;
import android.net.wifi.ScanResult;
import android.net.wifi.WifiInfo;
import android.net.wifi.WifiManager;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.telephony.CellIdentityLte;
import android.telephony.CellInfo;
import android.telephony.CellInfoLte;
import android.telephony.CellSignalStrength;
import android.telephony.CellSignalStrengthLte;
import android.telephony.CellSignalStrengthNr;
import android.telephony.SignalStrength;
import android.telephony.TelephonyManager;
import android.text.Editable;
import android.text.InputType;
import android.text.TextWatcher;
import android.view.Gravity;
import android.view.View;
import android.view.inputmethod.EditorInfo;
import android.widget.Button;
import android.widget.CheckBox;
import android.widget.EditText;
import android.widget.HorizontalScrollView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

import org.json.JSONObject;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.text.Normalizer;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Date;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Random;
import java.util.TimeZone;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

/**
 * Medidor de Señal - UTN FRT, Comunicación.
 * Lee automáticamente equipo, red (RSRP, RSRQ, SINR, banda, ancho de canal / WiFi RSSI, canal, estándar),
 * mide velocidad, ubica por GPS, aplica la teoría de la cátedra y sincroniza automáticamente con nuestra planilla de Google Sheets.
 */
public class MainActivity extends Activity {

    // URL fija de nuestra planilla (Google Apps Script propio). Automática y no editable por el usuario.
    static final String SCRIPT_URL_POR_DEFECTO = "https://script.google.com/macros/s/AKfycbw3aFZwnii8ENYhQgDPl2qAefmcTeZ2HE-QDPz2cuogEJQWPQqG6i21825SfMNFxq5ZVw/exec";
    static final String SPEED_HOST = "https://speed.cloudflare.com";
    static final String IP_INFO_URL = "https://ipwho.is/";
    static final String IP_INFO_FALLBACK_1 = "https://ipinfo.io/json";
    static final String IP_INFO_FALLBACK_2 = "https://ipapi.co/json/";
    static final long ISP_RETRY_MS = 15_000L;
    static final String[] PROVEEDORES_RAPIDOS = {
            "Personal", "Claro", "Movistar", "CCC", "Starlink", "Telecentro", "RIU / UTN"
    };

    static final String[] CAMPOS = {
            "id", "fecha", "tipo_dispositivo", "nombre_dispositivo", "marca", "modelo", "so", "version_so", "navegador",
            "conexion", "tecnologia", "tipo_red_detectado", "empresa", "isp_detectado", "asn",
            "bajada_mbps", "subida_mbps", "latencia_ms", "jitter_ms",
            "senal_dbm", "barras", "lugar", "lat", "lon", "precision_m", "ubicacion_fuente", "ciudad", "comentario",
            "sinr_db", "ancho_mhz", "origen"};

    Tema tema;
    EstadisticasPanel estadisticasPanel;
    int tabActual = 0; // 0 = Medidor, 1 = Estadísticas

    final Handler ui = new Handler(Looper.getMainLooper());
    TextView tEquipo, tRed, tIspLabel, tIspDetalle, tVelocidad, tFase, tResultados, tTeoria, tEstado;
    Button bMedir, bEnviar, bCompartir, bRedetectarIsp;
    EditText eProveedor, eLugar, eComentario;
    CheckBox cConsentimiento;
    LinearLayout panelMedidor, panelEstadisticas;
    ScrollView sv;
    final List<Button> chipsProveedor = new ArrayList<>();
    final Map<String, Object> medicion = Collections.synchronizedMap(new HashMap<>());
    Red redActual = new Red();
    boolean midiendo = false;
    SharedPreferences prefs;

    // Estado de detección y corrección de proveedor / ISP
    volatile InfoIsp ispActual = null;
    volatile String ispClaveEnCurso = null;
    volatile long ultimoIntentoIspMs = 0L;
    volatile boolean ispUltimoFallo = false;
    volatile long ispIntentoSeq = 0L;
    String ultimaClaveRedConectada = "";
    volatile String proveedorTexto = "";
    volatile boolean proveedorEditadoManualmente = false;
    boolean actualizandoProveedorUi = false;
    volatile String medicionClaveRed = "";
    boolean tieneResultadoMedicion = false;
    double ultBaj, ultSub, ultLat, ultJit;
    String ultUbic = "";

    // Estado preservado ante cambios de tema
    String txtEquipo = "…", txtRed = "Leyendo…", txtVelocidad = "0.0", txtFase = "Mbps · listo para medir";
    String txtResultados = "—", txtTeoria = "Hacé una medición para ver el cálculo.", txtEstado = "";
    int colorEstado = 0;
    boolean hayEstado = false, enviarHabilitado = false, compartirHabilitado = false;

    // ======================================================================
    // Datos de la red
    // ======================================================================
    static class Red {
        String conexion = "", tecnologia = "", detalle = "", operador = "", claveRed = "";
        Network redActiva;
        Integer dbm, rsrq, barras;
        Double sinr, anchoMHz;
    }

    static class InfoIsp {
        final String claveRed, ispCrudo, empresaNorm, asn, ciudad;
        final Double lat, lon;

        InfoIsp(String claveRed, String ispCrudo, String empresaNorm, String asn, String ciudad, Double lat, Double lon) {
            this.claveRed = claveRed;
            this.ispCrudo = ispCrudo;
            this.empresaNorm = empresaNorm;
            this.asn = asn;
            this.ciudad = ciudad;
            this.lat = lat;
            this.lon = lon;
        }
    }

    @Override
    protected void onCreate(Bundle b) {
        super.onCreate(b);
        prefs = getSharedPreferences("medidor", MODE_PRIVATE);
        tema = Tema.resolver(this, prefs);
        estadisticasPanel = new EstadisticasPanel(this, prefs, ui);
        construirUI();
        pedirPermisos();
        detectarEquipo();
        refrescarRed.run();
    }

    @Override
    public void onConfigurationChanged(Configuration newConfig) {
        super.onConfigurationChanged(newConfig);
        if (Tema.MODO_SISTEMA.equals(Tema.cargarModo(prefs))) {
            aplicarTema(Tema.MODO_SISTEMA);
        }
    }

    void aplicarTema(String modo) {
        Tema.guardarModo(prefs, modo);
        tema = Tema.resolver(this, prefs);
        guardarEstadoVista();
        construirUI();
    }

    void guardarEstadoVista() {
        if (tEquipo != null) txtEquipo = tEquipo.getText().toString();
        if (tRed != null) txtRed = tRed.getText().toString();
        if (tVelocidad != null) txtVelocidad = tVelocidad.getText().toString();
        if (tFase != null) txtFase = tFase.getText().toString();
        if (tResultados != null) txtResultados = tResultados.getText().toString();
        if (tTeoria != null) txtTeoria = tTeoria.getText().toString();
        if (tEstado != null) {
            txtEstado = tEstado.getText().toString();
            hayEstado = tEstado.getVisibility() == View.VISIBLE;
            colorEstado = tEstado.getCurrentTextColor();
        }
        if (bEnviar != null) enviarHabilitado = bEnviar.isEnabled();
        if (bCompartir != null) compartirHabilitado = bCompartir.isEnabled();
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        ui.removeCallbacks(refrescarRed);
    }

    void pedirPermisos() {
        List<String> faltan = new ArrayList<>();
        for (String p : new String[]{Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.READ_PHONE_STATE}) {
            if (checkSelfPermission(p) != PackageManager.PERMISSION_GRANTED) faltan.add(p);
        }
        if (!faltan.isEmpty()) requestPermissions(faltan.toArray(new String[0]), 1);
    }

    @Override
    public void onRequestPermissionsResult(int code, String[] perms, int[] res) {
        refrescarRed.run();
    }

    boolean tiene(String permiso) {
        return checkSelfPermission(permiso) == PackageManager.PERMISSION_GRANTED;
    }

    // Lectura en vivo de la señal cada 2 segundos
    final Runnable refrescarRed = new Runnable() {
        @Override
        public void run() {
            try {
                redActual = leerRed();
                procesarCambioRedYDetectarIsp(redActual);
                mostrarRed(redActual);
            } catch (Exception e) {
                tRed.setText("No se pudo leer la red: " + e.getMessage());
            }
            ui.removeCallbacks(this);
            ui.postDelayed(this, 2000);
        }
    };

    Red leerRed() {
        Red r = new Red();
        ConnectivityManager cm = (ConnectivityManager) getSystemService(Context.CONNECTIVITY_SERVICE);
        Network redActiva = cm != null ? cm.getActiveNetwork() : null;
        NetworkCapabilities nc = (cm != null && redActiva != null) ? cm.getNetworkCapabilities(redActiva) : null;
        TelephonyManager tm = (TelephonyManager) getSystemService(Context.TELEPHONY_SERVICE);
        r.redActiva = redActiva;
        long handle = redActiva != null ? redActiva.getNetworkHandle() : 0L;

        if (nc != null && nc.hasTransport(NetworkCapabilities.TRANSPORT_WIFI)) {
            leerWifi(r);
            r.claveRed = "WiFi:" + handle;
            r.operador = resolverOperadorIsp(r.claveRed);
        } else if (nc != null && nc.hasTransport(NetworkCapabilities.TRANSPORT_CELLULAR)) {
            leerCelular(r, tm);
            r.claveRed = "Datos móviles:" + handle;
            String op = tm != null ? normalizarOperador(tm.getSimOperatorName()) : "";
            if (op.isEmpty() && tm != null) op = normalizarOperador(tm.getNetworkOperatorName());
            if (op.isEmpty()) op = resolverOperadorIsp(r.claveRed);
            r.operador = op;
        } else if (nc != null && nc.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET)) {
            r.conexion = "Cable";
            r.claveRed = "Cable:" + handle;
            r.operador = resolverOperadorIsp(r.claveRed);
        } else {
            r.conexion = "Sin conexión";
            r.claveRed = "Sin conexión";
            r.operador = "";
        }
        return r;
    }

    String resolverOperadorIsp(String claveRed) {
        InfoIsp info = ispActual;
        if (info != null && claveRed.equals(info.claveRed)) {
            return info.empresaNorm;
        }
        return "";
    }

    @SuppressWarnings("deprecation")
    void leerWifi(Red r) {
        r.conexion = "WiFi";
        WifiManager wm = (WifiManager) getApplicationContext().getSystemService(Context.WIFI_SERVICE);
        WifiInfo wi = wm.getConnectionInfo();
        int rssi = wi.getRssi(), f = wi.getFrequency();
        r.dbm = rssi;
        r.barras = WifiManager.calculateSignalLevel(rssi, 5);
        String estandar = "";
        if (Build.VERSION.SDK_INT >= 30) {
            switch (wi.getWifiStandard()) {
                case 4: estandar = "WiFi 4 (802.11n)"; break;
                case 5: estandar = "WiFi 5 (802.11ac)"; break;
                case 6: estandar = "WiFi 6 (802.11ax)"; break;
                case 8: estandar = "WiFi 7 (802.11be)"; break;
                case 1: estandar = "802.11a/b/g"; break;
            }
        }
        r.tecnologia = estandar;
        try {
            String bssid = wi.getBSSID();
            for (ScanResult sr : wm.getScanResults()) {
                if (sr.BSSID != null && sr.BSSID.equalsIgnoreCase(bssid)) {
                    int[] anchos = {20, 40, 80, 160, 160, 320};
                    if (sr.channelWidth >= 0 && sr.channelWidth < anchos.length) r.anchoMHz = (double) anchos[sr.channelWidth];
                }
            }
        } catch (SecurityException ignored) { }
        String banda = f >= 5925 ? "6 GHz" : f >= 4900 ? "5 GHz" : "2,4 GHz";
        StringBuilder d = new StringBuilder();
        if (!estandar.isEmpty()) d.append(estandar).append(" · ");
        d.append(banda).append(" · canal ").append(canalWifi(f)).append(" (").append(f).append(" MHz)");
        if (r.anchoMHz != null) d.append(" · ancho ").append(r.anchoMHz.intValue()).append(" MHz");
        d.append(" · enlace ").append(wi.getLinkSpeed()).append(" Mbps");
        if (Build.VERSION.SDK_INT >= 29) d.append(" (rx ").append(wi.getRxLinkSpeedMbps()).append(" / tx ").append(wi.getTxLinkSpeedMbps()).append(")");
        r.detalle = d.toString();
    }

    static int canalWifi(int f) {
        if (f == 2484) return 14;
        if (f >= 2412 && f <= 2472) return (f - 2407) / 5;
        if (f >= 5925) return (f - 5950) / 5;
        if (f >= 4900) return (f - 5000) / 5;
        return 0;
    }

    void leerCelular(Red r, TelephonyManager tm) {
        r.conexion = "Datos móviles";
        int tipo = TelephonyManager.NETWORK_TYPE_UNKNOWN;
        if (tiene(Manifest.permission.READ_PHONE_STATE)) {
            try { tipo = tm.getDataNetworkType(); } catch (SecurityException ignored) { }
        }
        switch (tipo) {
            case TelephonyManager.NETWORK_TYPE_NR: r.tecnologia = "5G"; break;
            case TelephonyManager.NETWORK_TYPE_LTE: r.tecnologia = "4G"; break;
            case TelephonyManager.NETWORK_TYPE_HSPAP: case TelephonyManager.NETWORK_TYPE_HSPA:
            case TelephonyManager.NETWORK_TYPE_HSDPA: case TelephonyManager.NETWORK_TYPE_HSUPA:
            case TelephonyManager.NETWORK_TYPE_UMTS: case TelephonyManager.NETWORK_TYPE_EVDO_0:
            case TelephonyManager.NETWORK_TYPE_EVDO_A: case TelephonyManager.NETWORK_TYPE_EVDO_B:
                r.tecnologia = "3G"; break;
            case TelephonyManager.NETWORK_TYPE_EDGE: case TelephonyManager.NETWORK_TYPE_GPRS:
            case TelephonyManager.NETWORK_TYPE_GSM: case TelephonyManager.NETWORK_TYPE_CDMA:
            case TelephonyManager.NETWORK_TYPE_1xRTT:
                r.tecnologia = "2G"; break;
        }
        StringBuilder d = new StringBuilder();

        if (Build.VERSION.SDK_INT >= 29) {
            SignalStrength ss = tm.getSignalStrength();
            if (ss != null) {
                r.barras = ss.getLevel();
                for (CellSignalStrength cs : ss.getCellSignalStrengths()) {
                    if (cs instanceof CellSignalStrengthNr) {
                        CellSignalStrengthNr nr = (CellSignalStrengthNr) cs;
                        if (valido(nr.getSsRsrp())) r.dbm = nr.getSsRsrp();
                        if (valido(nr.getSsRsrq())) r.rsrq = nr.getSsRsrq();
                        if (valido(nr.getSsSinr())) r.sinr = (double) nr.getSsSinr();
                        r.tecnologia = "5G";
                        break;
                    } else if (cs instanceof CellSignalStrengthLte) {
                        CellSignalStrengthLte lte = (CellSignalStrengthLte) cs;
                        if (valido(lte.getRsrp())) r.dbm = lte.getRsrp();
                        if (valido(lte.getRsrq())) r.rsrq = lte.getRsrq();
                        if (valido(lte.getRssnr())) {
                            double s = lte.getRssnr();
                            r.sinr = Math.abs(s) > 50 ? s / 10.0 : s;
                        }
                    }
                }
            }
        }

        if (tiene(Manifest.permission.ACCESS_FINE_LOCATION)) {
            try {
                List<CellInfo> celdas = tm.getAllCellInfo();
                if (celdas != null) for (CellInfo ci : celdas) {
                    if (!ci.isRegistered() || !(ci instanceof CellInfoLte)) continue;
                    CellInfoLte l = (CellInfoLte) ci;
                    CellIdentityLte id = l.getCellIdentity();
                    if (r.dbm == null) r.dbm = l.getCellSignalStrength().getRsrp();
                    if (r.rsrq == null && valido(l.getCellSignalStrength().getRsrq())) r.rsrq = l.getCellSignalStrength().getRsrq();
                    if (r.barras == null) r.barras = l.getCellSignalStrength().getLevel();
                    if (Build.VERSION.SDK_INT >= 30 && id.getBands().length > 0) d.append("banda ").append(id.getBands()[0]).append(" · ");
                    if (Build.VERSION.SDK_INT >= 28 && valido(id.getBandwidth())) r.anchoMHz = id.getBandwidth() / 1000.0;
                    d.append("EARFCN ").append(id.getEarfcn()).append(" · PCI ").append(id.getPci()).append(" · ");
                    break;
                }
            } catch (SecurityException ignored) { }
        }
        if (r.anchoMHz != null) d.append("ancho ").append(fmt(r.anchoMHz, 1)).append(" MHz · ");
        if (r.rsrq != null) d.append("RSRQ ").append(r.rsrq).append(" dB · ");
        if (r.sinr != null) d.append("SINR ").append(fmt(r.sinr, 1)).append(" dB");
        r.detalle = d.toString().replaceAll(" · $", "");
    }

    static boolean valido(int v) {
        return v != Integer.MAX_VALUE && v != CellInfo.UNAVAILABLE;
    }

    static String normalizarOperador(String n) {
        if (n == null) return "";
        String limpio = n.trim();
        if (limpio.isEmpty() || "null".equalsIgnoreCase(limpio)) return "";
        String s = sinAcentos(limpio).toLowerCase(Locale.ROOT);
        if (s.contains("personal")) return "Personal";
        if (s.contains("claro")) return "Claro";
        if (s.contains("movistar") || s.contains("tuenti")) return "Movistar";
        return limpio;
    }

    static String sinAcentos(String s) {
        if (s == null) return "";
        return Normalizer.normalize(s, Normalizer.Form.NFD)
                .replaceAll("\\p{InCombiningDiacriticalMarks}+", "");
    }

    static String normalizarIsp(String n) {
        if (n == null) return "";
        String limpio = n.trim();
        if (limpio.isEmpty() || "null".equalsIgnoreCase(limpio)) return "";
        limpio = limpio.replaceFirst("(?i)^AS\\d+\\s+", "").trim();
        if (limpio.isEmpty()) return "";
        String s = sinAcentos(limpio).toLowerCase(Locale.ROOT);
        if (s.contains("claro") || s.contains("amx") || s.contains("techtel")
                || s.contains("telmex") || s.contains("america movil")) {
            return "Claro";
        }
        if (s.contains("movistar") || s.contains("telefonica") || s.contains("tuenti")) {
            return "Movistar";
        }
        if (s.contains("personal") || s.contains("telecom") || s.contains("cablevision")
                || s.contains("fibertel") || s.contains("arnet")) {
            return "Personal";
        }
        if (s.contains("circuitos cerrados") || s.matches(".*\\bccc\\b.*")) {
            return "CCC";
        }
        if (s.contains("starlink") || s.contains("spacex")) {
            return "Starlink";
        }
        if (s.contains("telecentro")) {
            return "Telecentro";
        }
        if (s.contains("supercanal")) {
            return "Supercanal";
        }
        if (s.contains("gigared")) {
            return "Gigared";
        }
        if (s.contains("iplan") || s.contains("nss s.a")) {
            return "IPLAN";
        }
        if (s.contains("metrotel")) {
            return "Metrotel";
        }
        if (s.contains("innova-red") || s.contains("innovared")
                || s.contains("redes de interconexion universitaria")
                || s.matches(".*\\briu\\b.*")) {
            return "RIU / UTN";
        }
        return limpio;
    }

    static String calidad(int dbm, String conexion) {
        int[][] r = "WiFi".equals(conexion) ? new int[][]{{-50}, {-67}, {-75}, {-82}} : new int[][]{{-80}, {-90}, {-100}, {-110}};
        String[] t = {"Excelente", "Buena", "Regular", "Mala"};
        for (int i = 0; i < 4; i++) if (dbm >= r[i][0]) return t[i];
        return "Sin servicio útil";
    }

    String obtenerProveedorEfectivo(Red r) {
        String txt = proveedorTexto == null ? "" : proveedorTexto.trim();
        if (proveedorEditadoManualmente) return txt;
        if (r != null && r.operador != null && !r.operador.trim().isEmpty()) return r.operador.trim();
        return txt;
    }

    void mostrarRed(Red r) {
        if (tRed == null || r == null) return;
        StringBuilder s = new StringBuilder();
        s.append(r.conexion);
        if (!r.tecnologia.isEmpty()) s.append(" · ").append(r.tecnologia);
        String prov = obtenerProveedorEfectivo(r);
        if (!prov.isEmpty()) {
            s.append(" · ").append(prov);
        } else if (("WiFi".equals(r.conexion) || "Cable".equals(r.conexion))
                && r.claveRed != null && r.claveRed.equals(ispClaveEnCurso)) {
            s.append(" · Detectando ISP…");
        }
        if (r.dbm != null) {
            s.append("\n").append(r.dbm).append(" dBm").append("WiFi".equals(r.conexion) ? " (RSSI)" : " (RSRP)")
                    .append(" · ").append(calidad(r.dbm, r.conexion));
            if (r.barras != null) s.append(" · ").append(barritas(r.barras));
        } else if ("Datos móviles".equals(r.conexion) && !tiene(Manifest.permission.READ_PHONE_STATE)) {
            s.append("\nDá permiso de teléfono y ubicación para leer la señal.");
        }
        if (!r.detalle.isEmpty()) s.append("\n").append(r.detalle);
        tRed.setText(s.toString());
    }

    static String barritas(int n) {
        StringBuilder b = new StringBuilder();
        for (int i = 0; i < 4; i++) b.append(i < n ? "▮" : "▯");
        return b.toString();
    }

    // ======================================================================
    // Equipo
    // ======================================================================
    static final Map<String, String> SAMSUNG = new HashMap<>();
    static {
        String[][] m = {{"A032", "Galaxy A03 Core"}, {"A035", "Galaxy A03"}, {"A045", "Galaxy A04"}, {"A047", "Galaxy A04s"},
                {"A055", "Galaxy A05"}, {"A057", "Galaxy A05s"}, {"A135", "Galaxy A13"}, {"A145", "Galaxy A14"},
                {"A146", "Galaxy A14 5G"}, {"A155", "Galaxy A15"}, {"A156", "Galaxy A15 5G"}, {"A245", "Galaxy A24"},
                {"A256", "Galaxy A25 5G"}, {"A325", "Galaxy A32"}, {"A336", "Galaxy A33 5G"}, {"A346", "Galaxy A34 5G"},
                {"A356", "Galaxy A35 5G"}, {"A525", "Galaxy A52"}, {"A536", "Galaxy A53 5G"}, {"A546", "Galaxy A54 5G"},
                {"A556", "Galaxy A55 5G"}, {"G991", "Galaxy S21"}, {"S901", "Galaxy S22"}, {"S911", "Galaxy S23"},
                {"S918", "Galaxy S23 Ultra"}, {"S921", "Galaxy S24"}, {"S928", "Galaxy S24 Ultra"}};
        for (String[] p : m) SAMSUNG.put(p[0], p[1]);
    }

    static String nombreComercial(String modelo) {
        if (modelo != null && modelo.toUpperCase(Locale.ROOT).startsWith("SM-") && modelo.length() >= 7) {
            String n = SAMSUNG.get(modelo.substring(3, 7).toUpperCase(Locale.ROOT));
            if (n != null) return n + " (" + modelo + ")";
        }
        return modelo;
    }

    void detectarEquipo() {
        String nombre = Settings.Global.getString(getContentResolver(), Settings.Global.DEVICE_NAME);
        if (nombre == null || nombre.isEmpty()) nombre = Build.MODEL;
        String marca = Build.MANUFACTURER.isEmpty() ? "" : Build.MANUFACTURER.substring(0, 1).toUpperCase(Locale.ROOT) + Build.MANUFACTURER.substring(1);
        String modelo = nombreComercial(Build.MODEL);
        medicion.put("nombre_dispositivo", nombre);
        medicion.put("marca", marca);
        medicion.put("modelo", modelo);
        medicion.put("so", "Android");
        medicion.put("version_so", Build.VERSION.RELEASE);
        medicion.put("tipo_dispositivo", "Celular");
        medicion.put("navegador", "App Android");
        medicion.put("origen", "android");
        tEquipo.setText(nombre + "\n" + marca + " " + modelo + " · Android " + Build.VERSION.RELEASE);
    }

    // ======================================================================
    // Proveedor / ISP por IP activa y edición manual
    // ======================================================================
    void establecerProveedorUI(String valor, boolean moverCursor) {
        String limpio = valor == null ? "" : valor;
        proveedorTexto = limpio;
        if (eProveedor == null) return;
        actualizandoProveedorUi = true;
        try {
            if (!limpio.equals(eProveedor.getText().toString())) {
                eProveedor.setText(limpio);
            }
            if (moverCursor) {
                eProveedor.setSelection(limpio.length());
            }
        } finally {
            actualizandoProveedorUi = false;
        }
        actualizarEstilosChips();
    }

    void actualizarEstilosChips() {
        if (tema == null) return;
        String actual = proveedorTexto == null ? "" : proveedorTexto.trim();
        for (Button chip : chipsProveedor) {
            boolean activo = !actual.isEmpty() && actual.equalsIgnoreCase(chip.getText().toString());
            chip.setBackground(fondo(activo ? tema.accentSoft : tema.surfaceAlt, activo ? tema.accent : tema.line, 14));
            chip.setTextColor(activo ? tema.accent : tema.fgSecondary);
            chip.setTypeface(Typeface.DEFAULT, activo ? Typeface.BOLD : Typeface.NORMAL);
        }
    }

    void actualizarPanelIspUI(Red r) {
        if (tIspLabel == null || tIspDetalle == null || tema == null) return;
        String conexion = r != null ? r.conexion : "";
        if ("WiFi".equals(conexion)) {
            tIspLabel.setText("PROVEEDOR / ISP WIFI (DETECTADO POR IP · EDITABLE)");
        } else if ("Datos móviles".equals(conexion)) {
            tIspLabel.setText("OPERADORA MÓVIL (SIM · EDITABLE)");
        } else if ("Cable".equals(conexion)) {
            tIspLabel.setText("PROVEEDOR / ISP CABLE (DETECTADO POR IP · EDITABLE)");
        } else {
            tIspLabel.setText("PROVEEDOR / ISP (EDITABLE)");
        }

        String clave = r != null ? r.claveRed : "";
        boolean enCurso = !clave.isEmpty() && clave.equals(ispClaveEnCurso);
        InfoIsp info = (ispActual != null && clave.equals(ispActual.claveRed)) ? ispActual : null;
        String provActual = obtenerProveedorEfectivo(r);

        if (bRedetectarIsp != null) {
            bRedetectarIsp.setEnabled(!enCurso);
            bRedetectarIsp.setAlpha(enCurso ? 0.5f : 1f);
        }

        if ("Sin conexión".equals(conexion) || conexion.isEmpty()) {
            tIspDetalle.setTextColor(tema.muted);
            tIspDetalle.setText("Sin conexión activa. Podés escribir el proveedor manualmente si lo necesitás.");
            return;
        }

        if (proveedorEditadoManualmente) {
            StringBuilder msg = new StringBuilder();
            if (provActual.isEmpty()) {
                msg.append("Campo vacío: escribí o elegí un proveedor antes de registrar.");
                tIspDetalle.setTextColor(tema.amber);
            } else {
                msg.append("Corregido manualmente → ").append(provActual);
                if (info != null && !info.ispCrudo.isEmpty()) {
                    msg.append(" (IP: ").append(info.ispCrudo);
                    if (!info.asn.isEmpty()) msg.append(" · ").append(info.asn);
                    msg.append(")");
                }
                tIspDetalle.setTextColor(tema.accent);
            }
            tIspDetalle.setText(msg.toString());
            return;
        }

        if ("WiFi".equals(conexion) || "Cable".equals(conexion)) {
            if (enCurso && info == null) {
                tIspDetalle.setTextColor(tema.muted);
                tIspDetalle.setText("Consultando ISP real de la red por IP pública…");
            } else if (info != null) {
                StringBuilder msg = new StringBuilder("IP pública: ").append(info.ispCrudo);
                if (!info.asn.isEmpty()) msg.append(" · ").append(info.asn);
                if (!info.ciudad.isEmpty()) msg.append(" · ").append(info.ciudad);
                tIspDetalle.setTextColor(tema.muted);
                tIspDetalle.setText(msg.toString());
            } else if (ispUltimoFallo) {
                tIspDetalle.setTextColor(tema.amber);
                tIspDetalle.setText("No se pudo detectar el ISP automáticamente. Elegilo o escribilo arriba, o tocá ↻.");
            } else {
                tIspDetalle.setTextColor(tema.muted);
                tIspDetalle.setText("Esperando detección automática del ISP…");
            }
        } else {
            StringBuilder msg = new StringBuilder();
            if (r != null && !r.operador.isEmpty()) {
                msg.append("SIM detectada: ").append(r.operador);
            } else {
                msg.append("Operadora SIM no informada");
            }
            if (info != null && !info.ispCrudo.isEmpty()) {
                msg.append(" · IP: ").append(info.ispCrudo);
                if (!info.asn.isEmpty()) msg.append(" (").append(info.asn).append(")");
            }
            tIspDetalle.setTextColor(tema.muted);
            tIspDetalle.setText(msg.toString());
        }
    }

    void procesarCambioRedYDetectarIsp(Red r) {
        if (r == null || r.redActiva == null || "Sin conexión".equals(r.conexion)) {
            actualizarPanelIspUI(r);
            return;
        }
        if (!r.claveRed.equals(ultimaClaveRedConectada)) {
            ultimaClaveRedConectada = r.claveRed;
            ispActual = null;
            ispUltimoFallo = false;
            ultimoIntentoIspMs = 0L;
            proveedorEditadoManualmente = false;
            aplicarIspAMedicion(null);
            establecerProveedorUI(r.operador, false);
            lanzarDeteccionIspAsync(r.claveRed, r.redActiva, true);
        } else {
            if (!proveedorEditadoManualmente && !r.operador.isEmpty() && !r.operador.equals(proveedorTexto)) {
                establecerProveedorUI(r.operador, false);
            }
            boolean sinIsp = ispActual == null || !r.claveRed.equals(ispActual.claveRed);
            boolean enCurso = r.claveRed.equals(ispClaveEnCurso);
            long ahora = System.currentTimeMillis();
            if (sinIsp && !enCurso && (!ispUltimoFallo || ahora - ultimoIntentoIspMs >= ISP_RETRY_MS)) {
                lanzarDeteccionIspAsync(r.claveRed, r.redActiva, false);
            }
        }
        actualizarPanelIspUI(r);
    }

    void redetectarIspManual() {
        proveedorEditadoManualmente = false;
        Red r = leerRed();
        redActual = r;
        if ("WiFi".equals(r.conexion) || "Cable".equals(r.conexion)) {
            ispActual = null;
            ispUltimoFallo = false;
            r.operador = "";
            aplicarIspAMedicion(null);
            establecerProveedorUI("", false);
            mostrarRed(r);
            actualizarPanelIspUI(r);
            lanzarDeteccionIspAsync(r.claveRed, r.redActiva, true);
        } else if ("Datos móviles".equals(r.conexion)) {
            establecerProveedorUI(r.operador, false);
            mostrarRed(r);
            actualizarPanelIspUI(r);
            if (r.redActiva != null) {
                lanzarDeteccionIspAsync(r.claveRed, r.redActiva, true);
            }
        } else {
            establecerProveedorUI("", false);
            mostrarRed(r);
            actualizarPanelIspUI(r);
        }
        if (tieneResultadoMedicion) actualizarTextoResultados();
    }

    void lanzarDeteccionIspAsync(final String claveRed, final Network red, boolean forzar) {
        if (red == null || claveRed == null || claveRed.isEmpty() || "Sin conexión".equals(claveRed)) return;
        if (!forzar && claveRed.equals(ispClaveEnCurso)) return;
        final long seq = ++ispIntentoSeq;
        ispClaveEnCurso = claveRed;
        ultimoIntentoIspMs = System.currentTimeMillis();
        actualizarPanelIspUI(redActual);
        new Thread(() -> {
            InfoIsp encontrado = null;
            try {
                encontrado = detectarISPSincrono(claveRed, red);
            } catch (Exception ignored) { }
            final InfoIsp info = encontrado;
            ui.post(() -> {
                if (seq == ispIntentoSeq && claveRed.equals(ispClaveEnCurso)) {
                    ispClaveEnCurso = null;
                }
                if (seq != ispIntentoSeq || !claveRed.equals(ultimaClaveRedConectada)) {
                    actualizarPanelIspUI(redActual);
                    return;
                }
                if (info != null) {
                    ispActual = info;
                    ispUltimoFallo = false;
                    aplicarIspAMedicion(info);
                    if (redActual != null && claveRed.equals(redActual.claveRed)) {
                        if ("WiFi".equals(redActual.conexion) || "Cable".equals(redActual.conexion) || redActual.operador.isEmpty()) {
                            redActual.operador = info.empresaNorm;
                        }
                        if (!proveedorEditadoManualmente) {
                            String val = ("WiFi".equals(redActual.conexion) || "Cable".equals(redActual.conexion) || redActual.operador.isEmpty())
                                    ? info.empresaNorm : redActual.operador;
                            establecerProveedorUI(val, false);
                        }
                    }
                    if (tieneResultadoMedicion) actualizarTextoResultados();
                } else {
                    ispUltimoFallo = true;
                    ultimoIntentoIspMs = System.currentTimeMillis();
                }
                mostrarRed(redActual);
                actualizarPanelIspUI(redActual);
            });
        }).start();
    }

    void aplicarIspAMedicion(InfoIsp info) {
        synchronized (medicion) {
            if (info == null) {
                medicion.remove("isp_detectado");
                medicion.remove("asn");
                medicion.remove("ciudad");
                medicion.remove("_ipLat");
                medicion.remove("_ipLon");
            } else {
                medicion.put("isp_detectado", info.ispCrudo);
                medicion.put("asn", info.asn);
                medicion.put("ciudad", info.ciudad);
                if (info.lat != null && info.lon != null) {
                    medicion.put("_ipLat", info.lat);
                    medicion.put("_ipLon", info.lon);
                } else {
                    medicion.remove("_ipLat");
                    medicion.remove("_ipLon");
                }
            }
        }
    }

    static String textoValidoJson(JSONObject o, String key) {
        if (o == null || !o.has(key) || o.isNull(key)) return "";
        String v = o.optString(key, "").trim();
        return "null".equalsIgnoreCase(v) ? "" : v;
    }

    InfoIsp detectarISPSincrono(String claveRed, Network red) {
        if (red == null || claveRed == null || claveRed.isEmpty() || "Sin conexión".equals(claveRed)) {
            return null;
        }
        try {
            InfoIsp r1 = consultarIpWhoIs(claveRed, red);
            if (r1 != null) return r1;
        } catch (Exception ignored) { }
        try {
            InfoIsp r2 = consultarIpInfoIo(claveRed, red);
            if (r2 != null) return r2;
        } catch (Exception ignored) { }
        try {
            InfoIsp r3 = consultarIpApiCo(claveRed, red);
            if (r3 != null) return r3;
        } catch (Exception ignored) { }
        return null;
    }

    InfoIsp consultarIpWhoIs(String claveRed, Network red) throws Exception {
        JSONObject d = new JSONObject(leerTexto(IP_INFO_URL, red, 5000, 6000));
        if (!d.optBoolean("success", true)) return null;
        JSONObject c = d.optJSONObject("connection");
        String ispCrudo = "";
        String asn = "";
        if (c != null) {
            ispCrudo = textoValidoJson(c, "isp");
            if (ispCrudo.isEmpty()) ispCrudo = textoValidoJson(c, "org");
            int asnNum = c.optInt("asn", 0);
            if (asnNum > 0) asn = "AS" + asnNum;
        }
        if (ispCrudo.isEmpty()) ispCrudo = textoValidoJson(d, "isp");
        if (ispCrudo.isEmpty()) ispCrudo = textoValidoJson(d, "org");
        if (ispCrudo.isEmpty()) return null;
        String emp = normalizarIsp(ispCrudo);
        String ciudad = textoValidoJson(d, "city");
        Double lat = d.has("latitude") && !d.isNull("latitude") ? d.optDouble("latitude") : null;
        Double lon = d.has("longitude") && !d.isNull("longitude") ? d.optDouble("longitude") : null;
        if (lat != null && Double.isNaN(lat)) lat = null;
        if (lon != null && Double.isNaN(lon)) lon = null;
        return new InfoIsp(claveRed, ispCrudo, emp, asn, ciudad, lat, lon);
    }

    InfoIsp consultarIpInfoIo(String claveRed, Network red) throws Exception {
        JSONObject d = new JSONObject(leerTexto(IP_INFO_FALLBACK_1, red, 5000, 6000));
        if (d.has("error")) return null;
        String org = textoValidoJson(d, "org");
        if (org.isEmpty()) return null;
        String asn = "";
        String ispCrudo = org;
        if (org.toUpperCase(Locale.ROOT).startsWith("AS")) {
            int sp = org.indexOf(' ');
            if (sp > 2) {
                String pref = org.substring(0, sp).trim();
                if (pref.substring(2).matches("\\d+")) {
                    asn = pref.toUpperCase(Locale.ROOT);
                    ispCrudo = org.substring(sp + 1).trim();
                }
            }
        }
        if (ispCrudo.isEmpty()) ispCrudo = org;
        String emp = normalizarIsp(ispCrudo);
        String ciudad = textoValidoJson(d, "city");
        Double lat = null, lon = null;
        String loc = textoValidoJson(d, "loc");
        if (loc.contains(",")) {
            String[] p = loc.split(",");
            if (p.length == 2) {
                try {
                    lat = Double.parseDouble(p[0].trim());
                    lon = Double.parseDouble(p[1].trim());
                } catch (NumberFormatException ignored) { }
            }
        }
        return new InfoIsp(claveRed, ispCrudo, emp, asn, ciudad, lat, lon);
    }

    InfoIsp consultarIpApiCo(String claveRed, Network red) throws Exception {
        JSONObject d = new JSONObject(leerTexto(IP_INFO_FALLBACK_2, red, 5000, 6000));
        if (d.optBoolean("error", false)) return null;
        String ispCrudo = textoValidoJson(d, "org");
        if (ispCrudo.isEmpty()) return null;
        String asn = textoValidoJson(d, "asn").toUpperCase(Locale.ROOT);
        if (!asn.isEmpty() && !asn.startsWith("AS")) asn = "AS" + asn;
        String emp = normalizarIsp(ispCrudo);
        String ciudad = textoValidoJson(d, "city");
        Double lat = d.has("latitude") && !d.isNull("latitude") ? d.optDouble("latitude") : null;
        Double lon = d.has("longitude") && !d.isNull("longitude") ? d.optDouble("longitude") : null;
        if (lat != null && Double.isNaN(lat)) lat = null;
        if (lon != null && Double.isNaN(lon)) lon = null;
        return new InfoIsp(claveRed, ispCrudo, emp, asn, ciudad, lat, lon);
    }

    // ======================================================================
    // Test de velocidad
    // ======================================================================
    interface Progreso { void en(double mbps); }

    static String leerTexto(String url) throws Exception {
        return leerTexto(url, null, 10000, 15000);
    }

    static String leerTexto(String url, Network red, int connectMs, int readMs) throws Exception {
        URL u = new URL(url);
        HttpURLConnection c = (HttpURLConnection) (red != null ? red.openConnection(u) : u.openConnection());
        c.setUseCaches(false);
        c.setInstanceFollowRedirects(true);
        c.setConnectTimeout(connectMs);
        c.setReadTimeout(readMs);
        c.setRequestProperty("User-Agent", "MedidorSenalUTN/2.0");
        c.setRequestProperty("Accept", "application/json, text/plain, */*");
        try {
            int code = c.getResponseCode();
            if (code < 200 || code >= 300) {
                throw new IOException("HTTP " + code);
            }
            try (InputStream in = c.getInputStream()) {
                return new String(leerTodo(in), StandardCharsets.UTF_8);
            }
        } finally {
            c.disconnect();
        }
    }

    static byte[] leerTodo(InputStream in) throws java.io.IOException {
        java.io.ByteArrayOutputStream out = new java.io.ByteArrayOutputStream();
        byte[] buf = new byte[8192];
        int n;
        while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
        return out.toByteArray();
    }

    static long bajarBytes(String url, long t0, Progreso p) throws Exception {
        HttpURLConnection c = (HttpURLConnection) new URL(url).openConnection();
        c.setUseCaches(false); c.setConnectTimeout(10000); c.setReadTimeout(30000);
        long total = 0;
        try (InputStream in = c.getInputStream()) {
            byte[] buf = new byte[65536];
            int n;
            while ((n = in.read(buf)) > 0) {
                total += n;
                double s = (System.nanoTime() - t0) / 1e9;
                if (p != null && s > 0.15) p.en(total * 8 / s / 1e6);
            }
        }
        return total;
    }

    double[] latencia() throws Exception {
        List<Double> t = new ArrayList<>();
        for (int i = 0; i < 11; i++) {
            long t0 = System.nanoTime();
            bajarBytes(SPEED_HOST + "/__down?bytes=0", t0, null);
            double ms = (System.nanoTime() - t0) / 1e6;
            if (i > 0) t.add(ms);
            fase("Latencia · " + Math.round(ms) + " ms");
        }
        double jit = 0;
        for (int i = 1; i < t.size(); i++) jit += Math.abs(t.get(i) - t.get(i - 1));
        Collections.sort(t);
        double mediana = (t.get(4) + t.get(5)) / 2;
        return new double[]{mediana, jit / (t.size() - 1)};
    }

    double bajada() throws Exception {
        double mejor = 0;
        long[] tam = {200_000, 2_000_000, 10_000_000, 25_000_000};
        for (int i = 0; i < tam.length; i++) {
            long t0 = System.nanoTime();
            long bytes = bajarBytes(SPEED_HOST + "/__down?bytes=" + tam[i], t0, v -> velocidad(v, "Mbps · bajada"));
            double s = (System.nanoTime() - t0) / 1e9, v = bytes * 8 / s / 1e6;
            if (i > 0 || tam.length == 1) mejor = Math.max(mejor, v);
            if (s > 2.5) break;
        }
        return mejor;
    }

    double subida() throws Exception {
        double mejor = 0;
        long[] tam = {200_000, 1_000_000, 4_000_000, 10_000_000};
        byte[] datos = new byte[10_000_000];
        new Random().nextBytes(datos);
        for (int i = 0; i < tam.length; i++) {
            long t0 = System.nanoTime();
            HttpURLConnection c = (HttpURLConnection) new URL(SPEED_HOST + "/__up").openConnection();
            c.setDoOutput(true); c.setRequestMethod("POST");
            c.setFixedLengthStreamingMode((int) tam[i]);
            c.setConnectTimeout(10000); c.setReadTimeout(30000);
            try (OutputStream o = c.getOutputStream()) { o.write(datos, 0, (int) tam[i]); }
            try (InputStream in = c.getInputStream()) { leerTodo(in); }
            double s = (System.nanoTime() - t0) / 1e9, v = tam[i] * 8 / s / 1e6;
            velocidad(v, "Mbps · subida");
            if (i > 0) mejor = Math.max(mejor, v);
            if (s > 2.5) break;
        }
        return mejor;
    }

    // ======================================================================
    // Ubicación
    // ======================================================================
    @SuppressWarnings("deprecation")
    Location ubicar() {
        if (!tiene(Manifest.permission.ACCESS_FINE_LOCATION)) return null;
        LocationManager lm = (LocationManager) getSystemService(Context.LOCATION_SERVICE);
        try {
            for (String prov : new String[]{LocationManager.GPS_PROVIDER, LocationManager.NETWORK_PROVIDER}) {
                Location l = lm.getLastKnownLocation(prov);
                if (l != null && System.currentTimeMillis() - l.getTime() < 120_000) return l;
            }
            final Location[] res = {null};
            final CountDownLatch listo = new CountDownLatch(1);
            LocationListener ll = loc -> { res[0] = loc; listo.countDown(); };
            String prov = lm.isProviderEnabled(LocationManager.GPS_PROVIDER) ? LocationManager.GPS_PROVIDER : LocationManager.NETWORK_PROVIDER;
            ui.post(() -> {
                try { lm.requestSingleUpdate(prov, ll, Looper.getMainLooper()); } catch (Exception e) { listo.countDown(); }
            });
            listo.await(12, TimeUnit.SECONDS);
            ui.post(() -> lm.removeUpdates(ll));
            if (res[0] != null) return res[0];
            return lm.getLastKnownLocation(LocationManager.NETWORK_PROVIDER);
        } catch (Exception e) {
            return null;
        }
    }

    // ======================================================================
    // Medición completa
    // ======================================================================
    void actualizarTextoResultados() {
        if (!tieneResultadoMedicion || tResultados == null) return;
        String prov = resolverEmpresaFinal();
        String lineaProv = prov.isEmpty() ? "—" : prov;
        txtResultados = String.format(Locale.ROOT,
                "Bajada   %.1f Mbps\nSubida   %.1f Mbps\nLatencia %.0f ms\nJitter   %.1f ms\nProveedor: %s\nUbicación: %s",
                ultBaj, ultSub, ultLat, ultJit, lineaProv, ultUbic);
        tResultados.setText(txtResultados);
    }

    String resolverEmpresaFinal() {
        String empUi = eProveedor != null ? eProveedor.getText().toString().trim()
                : (proveedorTexto == null ? "" : proveedorTexto.trim());
        String empMed;
        synchronized (medicion) {
            Object o = medicion.get("empresa");
            empMed = o == null ? "" : String.valueOf(o).trim();
        }
        String claveActual = redActual != null ? redActual.claveRed : "";
        if (proveedorEditadoManualmente) return empUi;
        if (medicionClaveRed.equals(claveActual) || empMed.isEmpty()) {
            return !empUi.isEmpty() ? empUi : empMed;
        }
        return empMed;
    }

    void medir() {
        if (midiendo) return;
        midiendo = true;
        tieneResultadoMedicion = false;
        bMedir.setEnabled(false); bMedir.setText("Midiendo…");
        bEnviar.setEnabled(false); bCompartir.setEnabled(false);
        tResultados.setText("—"); tTeoria.setText("");
        new Thread(() -> {
            try {
                final Red r = leerRed();
                if (r.redActiva != null && !"Sin conexión".equals(r.conexion)
                        && (ispActual == null || !r.claveRed.equals(ispActual.claveRed))) {
                    fase("Detectando ISP…");
                    final InfoIsp nuevo = detectarISPSincrono(r.claveRed, r.redActiva);
                    if (nuevo != null) {
                        ispActual = nuevo;
                        ispUltimoFallo = false;
                        if ("WiFi".equals(r.conexion) || "Cable".equals(r.conexion) || r.operador.isEmpty()) {
                            r.operador = nuevo.empresaNorm;
                        }
                        ui.post(() -> {
                            if (r.claveRed.equals(ultimaClaveRedConectada)) {
                                if (redActual != null && r.claveRed.equals(redActual.claveRed)
                                        && ("WiFi".equals(redActual.conexion) || "Cable".equals(redActual.conexion) || redActual.operador.isEmpty())) {
                                    redActual.operador = nuevo.empresaNorm;
                                }
                                if (!proveedorEditadoManualmente) {
                                    String val = ("WiFi".equals(r.conexion) || "Cable".equals(r.conexion) || r.operador.isEmpty())
                                            ? nuevo.empresaNorm : r.operador;
                                    establecerProveedorUI(val, false);
                                }
                                mostrarRed(redActual);
                                actualizarPanelIspUI(redActual);
                            }
                        });
                    } else {
                        ispUltimoFallo = true;
                        ultimoIntentoIspMs = System.currentTimeMillis();
                    }
                }

                double[] lat = latencia();
                double baj = bajada();
                double sub = subida();
                fase("Ubicación…");
                Location loc = ubicar();

                InfoIsp ispValido = (ispActual != null && r.claveRed.equals(ispActual.claveRed)) ? ispActual : null;
                aplicarIspAMedicion(ispValido);
                String empresaInicial = obtenerProveedorEfectivo(r);
                medicionClaveRed = r.claveRed;

                String ubic;
                synchronized (medicion) {
                    medicion.put("id", UUID.randomUUID().toString());
                    SimpleDateFormat iso = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.ROOT);
                    iso.setTimeZone(TimeZone.getTimeZone("UTC"));
                    medicion.put("fecha", iso.format(new Date()));
                    medicion.put("conexion", r.conexion);
                    medicion.put("tecnologia", r.tecnologia);
                    medicion.put("tipo_red_detectado", r.detalle);
                    medicion.put("empresa", empresaInicial);
                    medicion.put("senal_dbm", r.dbm);
                    medicion.put("barras", r.barras);
                    medicion.put("sinr_db", r.sinr == null ? null : Math.round(r.sinr * 10) / 10.0);
                    medicion.put("ancho_mhz", r.anchoMHz);
                    medicion.put("latencia_ms", Math.round(lat[0] * 10) / 10.0);
                    medicion.put("jitter_ms", Math.round(lat[1] * 10) / 10.0);
                    medicion.put("bajada_mbps", Math.round(baj * 100) / 100.0);
                    medicion.put("subida_mbps", Math.round(sub * 100) / 100.0);
                    if (loc != null) {
                        medicion.put("lat", Math.round(loc.getLatitude() * 1e5) / 1e5);
                        medicion.put("lon", Math.round(loc.getLongitude() * 1e5) / 1e5);
                        medicion.put("precision_m", Math.round(loc.getAccuracy()));
                        medicion.put("ubicacion_fuente", "GPS");
                        ubic = "GPS ±" + Math.round(loc.getAccuracy()) + " m";
                    } else if (ispValido != null && ispValido.lat != null && ispValido.lon != null) {
                        medicion.put("lat", ispValido.lat);
                        medicion.put("lon", ispValido.lon);
                        medicion.remove("precision_m");
                        medicion.put("ubicacion_fuente", "IP (aprox.)");
                        ubic = "aproximada por IP";
                    } else {
                        medicion.remove("lat");
                        medicion.remove("lon");
                        medicion.remove("precision_m");
                        medicion.remove("ubicacion_fuente");
                        ubic = "no disponible";
                    }
                }
                final String ubicTxt = ubic;
                final Red rf = r;
                ui.post(() -> {
                    velocidad(baj, "Mbps · resultado de bajada");
                    ultBaj = baj;
                    ultSub = sub;
                    ultLat = lat[0];
                    ultJit = lat[1];
                    ultUbic = ubicTxt;
                    tieneResultadoMedicion = true;
                    actualizarTextoResultados();
                    tTeoria.setText(teoria(rf, baj));
                    bEnviar.setEnabled(true); bCompartir.setEnabled(true);
                });
            } catch (Exception e) {
                ui.post(() -> {
                    fase("Error en la medición");
                    estado("No se pudo completar la medición: " + e.getMessage(), tema.bad);
                });
            } finally {
                ui.post(() -> { midiendo = false; bMedir.setEnabled(true); bMedir.setText("Medir de nuevo"); });
            }
        }).start();
    }

    // ======================================================================
    // Teoría de la cátedra (Unidades 3, 5 y 7)
    // ======================================================================
    static double ruidoTermicoDbm(double anchoMHz, double tempC) {
        return 10 * Math.log10(1.38e-23 * (tempC + 273) * anchoMHz * 1e6 / 1e-3);
    }

    static double shannonMbps(double anchoMHz, double snrDb) {
        return anchoMHz * Math.log(1 + Math.pow(10, snrDb / 10)) / Math.log(2);
    }

    static String teoria(Red r, double bajada) {
        StringBuilder s = new StringBuilder();
        if (r.dbm == null) return "Sin dato de señal: no se puede aplicar el cálculo teórico.";
        double B = r.anchoMHz != null ? r.anchoMHz : 20, T = 25;
        boolean wifi = "WiFi".equals(r.conexion);
        double mW = Math.pow(10, r.dbm / 10.0);
        s.append(r.dbm).append(" dBm = ").append(fmt(mW * 1e12, 2)).append(" fW  →  calidad ").append(calidad(r.dbm, r.conexion)).append("\n\n");
        double S = r.dbm;
        if (!wifi) {
            int sub = 12 * 5 * (int) Math.round(B);
            S = r.dbm + 10 * Math.log10(sub);
            s.append("RSRP es la potencia de UNA subportadora de 15 kHz. Con B = ").append(fmt(B, 1))
                    .append(" MHz hay ").append(sub).append(" subportadoras:\nS = ").append(r.dbm).append(" + 10·log(").append(sub)
                    .append(") = ").append(fmt(S, 1)).append(" dBm\n\n");
        }
        double N = ruidoTermicoDbm(B, T);
        double snrEst = S - N, cEst = shannonMbps(B, snrEst);
        s.append("Ruido térmico N = kTB (").append(fmt(B, 1)).append(" MHz, 25 °C) = ").append(fmt(N, 1)).append(" dBm\n");
        s.append("SNR estimada (solo ruido térmico) = ").append(fmt(snrEst, 1)).append(" dB\n");
        s.append("Shannon C = B·log₂(1+S/N) = ").append(fmt(cEst, 0)).append(" Mbps\n");
        if (r.sinr != null) {
            double cReal = shannonMbps(B, r.sinr);
            s.append("\nSINR REAL medida por el módem = ").append(fmt(r.sinr, 1)).append(" dB\n");
            s.append("→ incluye interferencia de otras celdas, por eso es menor que la estimada.\n");
            s.append("Shannon con SINR real = ").append(fmt(cReal, 0)).append(" Mbps\n");
            s.append("Tu bajada es el ").append(fmt(100 * bajada / cReal, 0)).append(" % de ese límite.\n");
        } else {
            s.append("Tu bajada es el ").append(fmt(100 * bajada / cEst, 0)).append(" % de ese límite.\n");
        }
        Map<String, String> max = new HashMap<>();
        max.put("2G", "EDGE: hasta 473,6 Kbps"); max.put("3G", "UMTS hasta 2 Mbps · HSPA+ hasta 42 Mbps");
        max.put("4G", "LTE: 100 Mbps a 1 Gbps"); max.put("5G", "5G NR: varios Gbps");
        if (!wifi && max.containsKey(r.tecnologia)) s.append("\nMáximo teórico de ").append(r.tecnologia).append(": ").append(max.get(r.tecnologia));
        return s.toString();
    }

    static String fmt(double v, int dec) {
        return String.format(new Locale("es", "AR"), "%." + dec + "f", v);
    }

    // ======================================================================
    // Envío automático a nuestra planilla
    // ======================================================================
    void enviar() {
        if (!cConsentimiento.isChecked()) { estado("Para enviar tenés que aceptar el uso de los datos.", tema.bad); return; }
        String empresaFinal = resolverEmpresaFinal();
        if (empresaFinal.isEmpty()) {
            estado("Ingresá o elegí el proveedor / ISP antes de registrar la medición.", tema.bad);
            if (sv != null && eProveedor != null) {
                sv.post(() -> {
                    sv.smoothScrollTo(0, 0);
                    eProveedor.requestFocus();
                });
            }
            return;
        }
        final String url = SCRIPT_URL_POR_DEFECTO;
        JSONObject j = new JSONObject();
        synchronized (medicion) {
            medicion.put("empresa", empresaFinal);
            medicion.put("lugar", eLugar.getText().toString().trim());
            medicion.put("comentario", eComentario.getText().toString().trim());
            try {
                for (String c : CAMPOS) { Object v = medicion.get(c); j.put(c, v == null ? "" : v); }
            } catch (Exception ignored) { }
        }
        guardarHistorial(j);
        bEnviar.setEnabled(false);
        new Thread(() -> {
            try {
                HttpURLConnection c = (HttpURLConnection) new URL(url).openConnection();
                c.setDoOutput(true); c.setRequestMethod("POST"); c.setInstanceFollowRedirects(true);
                c.setRequestProperty("Content-Type", "text/plain;charset=utf-8");
                c.setConnectTimeout(15000); c.setReadTimeout(20000);
                try (OutputStream o = c.getOutputStream()) { o.write(j.toString().getBytes(StandardCharsets.UTF_8)); }
                int code = c.getResponseCode();
                ui.post(() -> estado(code < 400 ? "¡Listo! Tu medición se envió a la planilla compartida." : "La planilla respondió con error " + code + ".", code < 400 ? tema.good : tema.bad));
            } catch (Exception e) {
                ui.post(() -> { estado("No se pudo enviar (" + e.getMessage() + "). Quedó guardada en el celular.", tema.bad); bEnviar.setEnabled(true); });
            }
        }).start();
    }

    void guardarHistorial(JSONObject j) {
        try {
            org.json.JSONArray a = new org.json.JSONArray(prefs.getString("historial", "[]"));
            a.put(j);
            prefs.edit().putString("historial", a.toString()).apply();
        } catch (Exception ignored) { }
    }

    void compartir() {
        String txt = "Medidor de Señal UTN FRT\n" + tEquipo.getText() + "\n" + tRed.getText() + "\n\n" + tResultados.getText() + "\n\n" + tTeoria.getText();
        Intent i = new Intent(Intent.ACTION_SEND).setType("text/plain").putExtra(Intent.EXTRA_TEXT, txt);
        startActivity(Intent.createChooser(i, "Compartir resultado"));
    }

    // ======================================================================
    // Interfaz (construida en código, sin XML)
    // ======================================================================
    int dp(int v) { return Math.round(v * getResources().getDisplayMetrics().density); }

    void fase(String t) { ui.post(() -> tFase.setText(t)); }

    void velocidad(double v, String f) {
        ui.post(() -> { tVelocidad.setText(v >= 100 ? fmt(v, 0) : fmt(v, 1)); tFase.setText(f); });
    }

    void estado(String t, int color) {
        tEstado.setVisibility(View.VISIBLE); tEstado.setText(t); tEstado.setTextColor(color);
    }

    GradientDrawable fondo(int color, int borde, int radio) {
        GradientDrawable g = new GradientDrawable();
        g.setColor(color); g.setCornerRadius(dp(radio)); g.setStroke(dp(1), borde);
        return g;
    }

    TextView texto(String t, int sp, int color, boolean negrita) {
        TextView v = new TextView(this);
        v.setText(t); v.setTextSize(sp); v.setTextColor(color);
        if (negrita) v.setTypeface(Typeface.DEFAULT_BOLD);
        return v;
    }

    LinearLayout tarjeta(LinearLayout padre, String titulo) {
        LinearLayout c = new LinearLayout(this);
        c.setOrientation(LinearLayout.VERTICAL);
        c.setPadding(dp(16), dp(14), dp(16), dp(16));
        c.setBackground(fondo(tema.surface, tema.line, 10));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(-1, -2);
        lp.topMargin = dp(14);
        padre.addView(c, lp);
        TextView t = texto(titulo.toUpperCase(Locale.ROOT), 11, tema.muted, true);
        t.setTypeface(Typeface.MONOSPACE, Typeface.BOLD); t.setLetterSpacing(0.08f);
        c.addView(t);
        return c;
    }

    Button boton(String t, boolean primario) {
        Button b = new Button(this);
        b.setText(t); b.setAllCaps(false); b.setTextSize(16);
        b.setTypeface(Typeface.DEFAULT_BOLD);
        b.setTextColor(primario ? Color.WHITE : tema.fg);
        b.setBackground(fondo(primario ? tema.accent : tema.surfaceElevated, primario ? tema.accent : tema.lineStrong, 8));
        b.setPadding(dp(12), dp(14), dp(12), dp(14));
        return b;
    }

    EditText campo(String pista) {
        EditText e = new EditText(this);
        e.setHint(pista); e.setTextSize(15); e.setSingleLine(true);
        e.setTextColor(tema.fg);
        e.setHintTextColor(tema.muted);
        e.setBackground(fondo(tema.surfaceElevated, tema.lineStrong, 8));
        e.setPadding(dp(12), dp(10), dp(12), dp(10));
        e.setInputType(InputType.TYPE_CLASS_TEXT);
        return e;
    }

    Button botonSegmento(String t, boolean activo) {
        Button b = new Button(this);
        b.setText(t);
        b.setAllCaps(false);
        b.setTextSize(13);
        b.setTypeface(Typeface.DEFAULT_BOLD);
        b.setTextColor(activo ? Color.WHITE : tema.fgSecondary);
        b.setBackground(fondo(activo ? tema.accent : tema.surfaceAlt, activo ? tema.accent : tema.line, 7));
        b.setPadding(dp(8), dp(8), dp(8), dp(8));
        b.setMinHeight(0);
        b.setMinimumHeight(0);
        return b;
    }

    void aplicarBarrasSistema() {
        getWindow().setStatusBarColor(tema.bg);
        getWindow().setNavigationBarColor(tema.bg);
        int flags = getWindow().getDecorView().getSystemUiVisibility();
        if (!tema.oscuro) {
            flags |= View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
            if (Build.VERSION.SDK_INT >= 26) flags |= View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR;
        } else {
            flags &= ~View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
            if (Build.VERSION.SDK_INT >= 26) flags &= ~View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR;
        }
        getWindow().getDecorView().setSystemUiVisibility(flags);
    }

    void cambiarTab(int nuevoTab) {
        tabActual = nuevoTab;
        if (panelMedidor != null) panelMedidor.setVisibility(tabActual == 0 ? View.VISIBLE : View.GONE);
        if (panelEstadisticas != null) panelEstadisticas.setVisibility(tabActual == 1 ? View.VISIBLE : View.GONE);
        guardarEstadoVista();
        construirUI();
    }

    void construirUI() {
        aplicarBarrasSistema();

        String prevProveedor = eProveedor != null ? eProveedor.getText().toString() : proveedorTexto;
        String prevLugar = eLugar != null ? eLugar.getText().toString() : "";
        String prevCom = eComentario != null ? eComentario.getText().toString() : "";
        boolean prevConsent = cConsentimiento != null && cConsentimiento.isChecked();

        sv = new ScrollView(this);
        sv.setBackgroundColor(tema.bg);
        LinearLayout raiz = new LinearLayout(this);
        raiz.setOrientation(LinearLayout.VERTICAL);
        raiz.setPadding(dp(16), dp(20), dp(16), dp(40));
        raiz.setFocusable(true);
        raiz.setFocusableInTouchMode(true);
        raiz.requestFocus();
        sv.addView(raiz);

        // Cabecera institucional
        TextView eyebrow = texto("UTN FRT · COMUNICACIÓN", 11, tema.accent, true);
        eyebrow.setTypeface(Typeface.MONOSPACE, Typeface.BOLD); eyebrow.setLetterSpacing(0.08f);
        raiz.addView(eyebrow);
        raiz.addView(texto("Medidor de Señal", 28, tema.fg, true));

        // Selector de tema: Sistema | Claro | Oscuro
        LinearLayout filaTema = new LinearLayout(this);
        filaTema.setOrientation(LinearLayout.HORIZONTAL);
        filaTema.setGravity(Gravity.CENTER_VERTICAL);
        LinearLayout.LayoutParams lpTema = new LinearLayout.LayoutParams(-1, -2);
        lpTema.topMargin = dp(10);
        TextView lblTema = texto("TEMA:", 11, tema.muted, true);
        lblTema.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
        LinearLayout.LayoutParams lpLblT = new LinearLayout.LayoutParams(-2, -2);
        lpLblT.rightMargin = dp(8);
        filaTema.addView(lblTema, lpLblT);

        String[][] modos = {{Tema.MODO_SISTEMA, "Sistema"}, {Tema.MODO_CLARO, "Claro"}, {Tema.MODO_OSCURO, "Oscuro"}};
        String modoActual = Tema.cargarModo(prefs);
        for (int i = 0; i < modos.length; i++) {
            final String mKey = modos[i][0];
            Button bt = botonSegmento(modos[i][1], mKey.equals(modoActual));
            LinearLayout.LayoutParams lpt = new LinearLayout.LayoutParams(0, -2, 1f);
            if (i < modos.length - 1) lpt.rightMargin = dp(6);
            bt.setOnClickListener(v -> {
                if (!mKey.equals(Tema.cargarModo(prefs))) aplicarTema(mKey);
            });
            filaTema.addView(bt, lpt);
        }
        raiz.addView(filaTema, lpTema);

        // Selector de pliego: Medidor | Estadísticas
        LinearLayout filaTabs = new LinearLayout(this);
        filaTabs.setOrientation(LinearLayout.HORIZONTAL);
        LinearLayout.LayoutParams lpTabs = new LinearLayout.LayoutParams(-1, -2);
        lpTabs.topMargin = dp(10);
        Button tabMed = botonSegmento("Medidor", tabActual == 0);
        Button tabEst = botonSegmento("Estadísticas", tabActual == 1);
        LinearLayout.LayoutParams lpT1 = new LinearLayout.LayoutParams(0, -2, 1f);
        lpT1.rightMargin = dp(6);
        tabMed.setOnClickListener(v -> { if (tabActual != 0) cambiarTab(0); });
        tabEst.setOnClickListener(v -> { if (tabActual != 1) cambiarTab(1); });
        filaTabs.addView(tabMed, lpT1);
        filaTabs.addView(tabEst, new LinearLayout.LayoutParams(0, -2, 1f));
        raiz.addView(filaTabs, lpTabs);

        // Panel Medidor
        panelMedidor = new LinearLayout(this);
        panelMedidor.setOrientation(LinearLayout.VERTICAL);
        panelMedidor.setVisibility(tabActual == 0 ? View.VISIBLE : View.GONE);
        raiz.addView(panelMedidor);

        LinearLayout cEq = tarjeta(panelMedidor, "Telemetría del dispositivo");
        tEquipo = texto(txtEquipo, 16, tema.fg, false); cEq.addView(tEquipo);

        LinearLayout cRed = tarjeta(panelMedidor, "Enlace de radiofrecuencia en vivo");
        tRed = texto(txtRed, 16, tema.fg, false); tRed.setLineSpacing(0, 1.15f); cRed.addView(tRed);

        View divIsp = new View(this);
        divIsp.setBackgroundColor(tema.line);
        LinearLayout.LayoutParams lpDivIsp = new LinearLayout.LayoutParams(-1, Math.max(1, dp(1)));
        lpDivIsp.topMargin = dp(12);
        lpDivIsp.bottomMargin = dp(10);
        cRed.addView(divIsp, lpDivIsp);

        tIspLabel = texto("PROVEEDOR / ISP (EDITABLE)", 11, tema.muted, true);
        tIspLabel.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
        tIspLabel.setLetterSpacing(0.08f);
        cRed.addView(tIspLabel);

        LinearLayout filaIsp = new LinearLayout(this);
        filaIsp.setOrientation(LinearLayout.HORIZONTAL);
        filaIsp.setGravity(Gravity.CENTER_VERTICAL);
        LinearLayout.LayoutParams lpFilaIsp = new LinearLayout.LayoutParams(-1, -2);
        lpFilaIsp.topMargin = dp(6);

        eProveedor = campo("Escribí o verificá el proveedor / ISP");
        eProveedor.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_CAP_WORDS);
        eProveedor.setImeOptions(EditorInfo.IME_ACTION_DONE);
        establecerProveedorUI(prevProveedor, false);
        eProveedor.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int start, int count, int after) { }
            @Override public void onTextChanged(CharSequence s, int start, int before, int count) { }
            @Override
            public void afterTextChanged(Editable s) {
                if (actualizandoProveedorUi) return;
                proveedorTexto = s.toString();
                proveedorEditadoManualmente = true;
                actualizarEstilosChips();
                mostrarRed(redActual);
                actualizarPanelIspUI(redActual);
                if (tieneResultadoMedicion) actualizarTextoResultados();
            }
        });
        filaIsp.addView(eProveedor, new LinearLayout.LayoutParams(0, -2, 1f));

        bRedetectarIsp = new Button(this);
        bRedetectarIsp.setText("↻");
        bRedetectarIsp.setAllCaps(false);
        bRedetectarIsp.setTextSize(18);
        bRedetectarIsp.setTypeface(Typeface.DEFAULT_BOLD);
        bRedetectarIsp.setTextColor(tema.fg);
        bRedetectarIsp.setBackground(fondo(tema.surfaceAlt, tema.lineStrong, 8));
        bRedetectarIsp.setPadding(0, 0, 0, 0);
        bRedetectarIsp.setMinWidth(0);
        bRedetectarIsp.setMinimumWidth(0);
        bRedetectarIsp.setMinHeight(0);
        bRedetectarIsp.setMinimumHeight(0);
        bRedetectarIsp.setContentDescription("Re-detectar ISP automáticamente");
        bRedetectarIsp.setOnClickListener(v -> redetectarIspManual());
        LinearLayout.LayoutParams lpBtnIsp = new LinearLayout.LayoutParams(dp(44), dp(44));
        lpBtnIsp.leftMargin = dp(8);
        filaIsp.addView(bRedetectarIsp, lpBtnIsp);
        cRed.addView(filaIsp, lpFilaIsp);

        HorizontalScrollView scrollChips = new HorizontalScrollView(this);
        scrollChips.setHorizontalScrollBarEnabled(false);
        LinearLayout.LayoutParams lpScrollChips = new LinearLayout.LayoutParams(-1, -2);
        lpScrollChips.topMargin = dp(8);
        LinearLayout rowChips = new LinearLayout(this);
        rowChips.setOrientation(LinearLayout.HORIZONTAL);
        chipsProveedor.clear();
        for (int i = 0; i < PROVEEDORES_RAPIDOS.length; i++) {
            final String prov = PROVEEDORES_RAPIDOS[i];
            Button chip = new Button(this);
            chip.setText(prov);
            chip.setAllCaps(false);
            chip.setTextSize(12);
            chip.setMinHeight(0);
            chip.setMinimumHeight(0);
            chip.setMinWidth(0);
            chip.setMinimumWidth(0);
            chip.setPadding(dp(12), dp(6), dp(12), dp(6));
            chip.setOnClickListener(v -> {
                proveedorEditadoManualmente = true;
                establecerProveedorUI(prov, true);
                mostrarRed(redActual);
                actualizarPanelIspUI(redActual);
                if (tieneResultadoMedicion) actualizarTextoResultados();
            });
            LinearLayout.LayoutParams lpChip = new LinearLayout.LayoutParams(-2, -2);
            if (i < PROVEEDORES_RAPIDOS.length - 1) lpChip.rightMargin = dp(6);
            rowChips.addView(chip, lpChip);
            chipsProveedor.add(chip);
        }
        scrollChips.addView(rowChips);
        cRed.addView(scrollChips, lpScrollChips);

        tIspDetalle = texto("", 12, tema.muted, false);
        tIspDetalle.setTypeface(Typeface.MONOSPACE);
        LinearLayout.LayoutParams lpIspDet = new LinearLayout.LayoutParams(-1, -2);
        lpIspDet.topMargin = dp(6);
        cRed.addView(tIspDetalle, lpIspDet);

        LinearLayout cMed = tarjeta(panelMedidor, "Instrumento de medición");
        tVelocidad = texto(txtVelocidad, 56, tema.fg, true);
        tVelocidad.setTypeface(Typeface.MONOSPACE, Typeface.BOLD); tVelocidad.setGravity(Gravity.CENTER);
        cMed.addView(tVelocidad, new LinearLayout.LayoutParams(-1, -2));
        tFase = texto(txtFase, 12, tema.muted, true);
        tFase.setTypeface(Typeface.MONOSPACE, Typeface.BOLD); tFase.setGravity(Gravity.CENTER);
        cMed.addView(tFase, new LinearLayout.LayoutParams(-1, -2));
        bMedir = boton(midiendo ? "Midiendo…" : "Iniciar medición", true);
        bMedir.setEnabled(!midiendo);
        LinearLayout.LayoutParams lpb = new LinearLayout.LayoutParams(-1, -2); lpb.topMargin = dp(14);
        cMed.addView(bMedir, lpb);
        bMedir.setOnClickListener(v -> medir());
        cMed.addView(texto("Usa entre 10 y 40 MB de datos.", 13, tema.muted, false));
        tResultados = texto(txtResultados, 16, tema.fg, false);
        tResultados.setTypeface(Typeface.MONOSPACE);
        LinearLayout.LayoutParams lpr = new LinearLayout.LayoutParams(-1, -2); lpr.topMargin = dp(10);
        cMed.addView(tResultados, lpr);

        LinearLayout cTeo = tarjeta(panelMedidor, "Análisis teórico");
        tTeoria = texto(txtTeoria, 14, tema.fg, false);
        tTeoria.setLineSpacing(0, 1.15f);
        cTeo.addView(tTeoria);

        LinearLayout cEnv = tarjeta(panelMedidor, "Parámetros de enlace y envío a registro");
        LinearLayout.LayoutParams lpCampo = new LinearLayout.LayoutParams(-1, -2);
        lpCampo.topMargin = dp(8);
        eLugar = campo("Entorno: Interior, Exterior, En movimiento…");
        eLugar.setText(prevLugar);
        cEnv.addView(eLugar, lpCampo);
        eComentario = campo("Observación o aula (ej.: Aula 3)");
        eComentario.setText(prevCom);
        cEnv.addView(eComentario, lpCampo);
        cConsentimiento = new CheckBox(this);
        cConsentimiento.setText("Acepto registrar esta medición para análisis académico de la cátedra de Comunicación (UTN FRT). No se almacenan números telefónicos ni direcciones IP personales.");
        cConsentimiento.setTextColor(tema.fg); cConsentimiento.setTextSize(13);
        cConsentimiento.setButtonTintList(ColorStateList.valueOf(tema.accent));
        cConsentimiento.setChecked(prevConsent);
        cEnv.addView(cConsentimiento, lpCampo);
        bEnviar = boton("Registrar medición", true); bEnviar.setEnabled(enviarHabilitado);
        cEnv.addView(bEnviar, lpb);
        bEnviar.setOnClickListener(v -> enviar());
        bCompartir = boton("Compartir resultado", false); bCompartir.setEnabled(compartirHabilitado);
        LinearLayout.LayoutParams lpc = new LinearLayout.LayoutParams(-1, -2); lpc.topMargin = dp(8);
        cEnv.addView(bCompartir, lpc);
        bCompartir.setOnClickListener(v -> compartir());
        tEstado = texto(txtEstado, 14, hayEstado ? colorEstado : tema.fg, true);
        tEstado.setVisibility(hayEstado ? View.VISIBLE : View.GONE);
        cEnv.addView(tEstado, lpr);

        // Panel Estadísticas
        panelEstadisticas = new LinearLayout(this);
        panelEstadisticas.setOrientation(LinearLayout.VERTICAL);
        panelEstadisticas.setVisibility(tabActual == 1 ? View.VISIBLE : View.GONE);
        panelEstadisticas.addView(estadisticasPanel.construir(tema));
        raiz.addView(panelEstadisticas);

        actualizarPanelIspUI(redActual);
        actualizarEstilosChips();
        setContentView(sv);
    }
}
