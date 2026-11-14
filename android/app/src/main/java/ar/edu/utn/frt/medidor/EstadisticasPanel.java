package ar.edu.utn.frt.medidor;

import android.app.Activity;
import android.content.Context;
import android.content.SharedPreferences;
import android.graphics.Canvas;
import android.graphics.DashPathEffect;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Handler;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.Spinner;
import android.widget.TextView;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;

/**
 * Panel nativo de Estadísticas para Android.
 * Consulta la planilla compartida (con respaldo en caché local e historial del dispositivo),
 * calcula KPIs, medidas por grupo, histogramas, dispersión vs. límite de Shannon y últimas mediciones.
 */
public class EstadisticasPanel {

    static class Medicion {
        String id = "", fecha = "", nombre = "", marca = "", modelo = "", so = "";
        String conexion = "", tecnologia = "", empresa = "", lugar = "", ciudad = "", origen = "";
        Double bajada, subida, latencia, jitter, dbm;
    }

    static class Resumen {
        int n;
        double media, mediana, desvio, min, max;
    }

    private final Activity ctx;
    private final SharedPreferences prefs;
    private final Handler ui;

    private Tema tema;
    private final List<Medicion> datos = new ArrayList<>();
    private boolean yaCargoRed = false;
    private boolean cargando = false;
    private String estadoTexto = "Cargando mediciones…";
    private boolean estadoEsError = false;

    // Filtros persistentes entre cambios de tema
    private String fConexion = "";
    private String fEmpresa = "";
    private String fVariable = "conexion";
    private String fMetrica = "bajada_mbps";

    // Vistas dinámicas
    private TextView tEstado, kN, kBajada, kSubida, kLat, kDatos;
    private Button bActualizar;
    private Spinner sConexion, sEmpresa, sVariable, sMetrica;
    private LinearLayout boxResumen, boxGrafGrupo, boxGrafHist, boxGrafSO, boxGrafMarca, boxScatter, boxRecientes;
    private boolean ignorarEventosSpinner = false;

    public EstadisticasPanel(Activity ctx, SharedPreferences prefs, Handler ui) {
        this.ctx = ctx;
        this.prefs = prefs;
        this.ui = ui;
        cargarLocales();
    }

    public View construir(Tema t) {
        this.tema = t;
        LinearLayout cont = new LinearLayout(ctx);
        cont.setOrientation(LinearLayout.VERTICAL);

        // Barra de estado y actualización
        LinearLayout cEstado = tarjeta(cont, "Sincronización de datos");
        tEstado = texto(estadoTexto, 13, estadoEsError ? t.bad : t.muted, false);
        cEstado.addView(tEstado);

        bActualizar = new Button(ctx);
        bActualizar.setText(cargando ? "Actualizando…" : "Actualizar estadísticas");
        bActualizar.setEnabled(!cargando);
        bActualizar.setAllCaps(false);
        bActualizar.setTextSize(14);
        bActualizar.setTypeface(Typeface.DEFAULT_BOLD);
        bActualizar.setTextColor(t.fg);
        bActualizar.setBackground(fondo(t.surfaceAlt, t.lineStrong, 8));
        bActualizar.setPadding(dp(12), dp(10), dp(12), dp(10));
        LinearLayout.LayoutParams lpb = new LinearLayout.LayoutParams(-1, -2);
        lpb.topMargin = dp(10);
        cEstado.addView(bActualizar, lpb);
        bActualizar.setOnClickListener(v -> refrescarDesdeRed());

        // Filtros
        LinearLayout cFiltros = tarjeta(cont, "Filtros y variables de corte");
        sConexion = agregarSpinner(cFiltros, "Conexión", new String[]{"Todas", "WiFi", "Datos móviles", "Cable"});
        sEmpresa = agregarSpinner(cFiltros, "Empresa", new String[]{"Todas"});
        sVariable = agregarSpinner(cFiltros, "Agrupar por", new String[]{
                "Conexión", "Tecnología", "Empresa", "Sistema operativo", "Marca", "Origen"
        });
        sMetrica = agregarSpinner(cFiltros, "Métrica", new String[]{
                "Bajada (Mbps)", "Subida (Mbps)", "Latencia (ms)", "Jitter (ms)", "Señal (dBm)"
        });
        sincronizarSpinners();
        configurarListenersSpinners();

        // KPIs
        LinearLayout cKpi = tarjeta(cont, "Resumen general");
        LinearLayout fila1 = new LinearLayout(ctx);
        fila1.setOrientation(LinearLayout.HORIZONTAL);
        kN = crearKpiTile(fila1, "Mediciones", true);
        kBajada = crearKpiTile(fila1, "Bajada media (Mbps)", false);
        cKpi.addView(fila1);

        LinearLayout fila2 = new LinearLayout(ctx);
        fila2.setOrientation(LinearLayout.HORIZONTAL);
        LinearLayout.LayoutParams lpF2 = new LinearLayout.LayoutParams(-1, -2);
        lpF2.topMargin = dp(8);
        kSubida = crearKpiTile(fila2, "Subida media (Mbps)", true);
        kLat = crearKpiTile(fila2, "Latencia media (ms)", false);
        cKpi.addView(fila2, lpF2);

        LinearLayout fila3 = new LinearLayout(ctx);
        fila3.setOrientation(LinearLayout.HORIZONTAL);
        kDatos = crearKpiTile(fila3, "Uso de datos móviles (%)", false);
        cKpi.addView(fila3, lpF2);

        // Medidas por grupo
        boxResumen = tarjeta(cont, "Medidas estadísticas por grupo");
        // Gráficos
        boxGrafGrupo = tarjeta(cont, "Promedio por grupo");
        boxGrafHist = tarjeta(cont, "Distribución de frecuencias");
        boxScatter = tarjeta(cont, "Dispersión: Potencia vs. Bajada y Límite de Shannon");
        boxGrafSO = tarjeta(cont, "Sistemas operativos");
        boxGrafMarca = tarjeta(cont, "Marcas de dispositivos");
        boxRecientes = tarjeta(cont, "Últimas mediciones");

        render();

        if (!yaCargoRed && !cargando) {
            refrescarDesdeRed();
        }
        return cont;
    }

    public void refrescarDesdeRed() {
        if (cargando) return;
        cargando = true;
        if (bActualizar != null) {
            bActualizar.setEnabled(false);
            bActualizar.setText("Actualizando…");
        }
        if (tEstado != null) {
            tEstado.setText("Consultando planilla compartida…");
            tEstado.setTextColor(tema.muted);
        }
        new Thread(() -> {
            try {
                String url = MainActivity.SCRIPT_URL_POR_DEFECTO + "?r=" + System.currentTimeMillis();
                String resp = MainActivity.leerTexto(url);
                JSONObject j = new JSONObject(resp);
                JSONArray arr = j.optJSONArray("filas");
                if (arr == null) arr = new JSONArray();
                prefs.edit().putString("stats_cache", arr.toString()).apply();
                List<Medicion> nuevas = parsearArray(arr);
                combinarConHistorialLocal(nuevas);
                ui.post(() -> {
                    datos.clear();
                    datos.addAll(nuevas);
                    yaCargoRed = true;
                    cargando = false;
                    estadoEsError = false;
                    estadoTexto = "Planilla sincronizada · " + datos.size() + " mediciones registradas.";
                    actualizarEstadoYRender();
                });
            } catch (Exception e) {
                ui.post(() -> {
                    cargarLocales();
                    cargando = false;
                    estadoEsError = true;
                    estadoTexto = "Sin conexión al servidor. Mostrando " + datos.size() + " mediciones locales/en caché.";
                    actualizarEstadoYRender();
                });
            }
        }).start();
    }

    private void actualizarEstadoYRender() {
        if (bActualizar != null) {
            bActualizar.setEnabled(true);
            bActualizar.setText("Actualizar estadísticas");
        }
        if (tEstado != null && tema != null) {
            tEstado.setText(estadoTexto);
            tEstado.setTextColor(estadoEsError ? tema.bad : tema.good);
        }
        sincronizarSpinners();
        render();
    }

    private void cargarLocales() {
        List<Medicion> lista = new ArrayList<>();
        try {
            String cache = prefs.getString("stats_cache", "");
            if (!cache.isEmpty()) {
                lista.addAll(parsearArray(new JSONArray(cache)));
            }
        } catch (Exception ignored) { }
        combinarConHistorialLocal(lista);
        datos.clear();
        datos.addAll(lista);
        if (datos.isEmpty()) {
            estadoTexto = "Todavía no hay mediciones en caché. Tocá «Actualizar estadísticas».";
        } else {
            estadoTexto = "Mostrando " + datos.size() + " mediciones en caché local.";
        }
    }

    private void combinarConHistorialLocal(List<Medicion> lista) {
        try {
            Set<String> ids = new HashSet<>();
            for (Medicion m : lista) if (!m.id.isEmpty()) ids.add(m.id);
            JSONArray hist = new JSONArray(prefs.getString("historial", "[]"));
            for (Medicion m : parsearArray(hist)) {
                if (m.id.isEmpty() || !ids.contains(m.id)) {
                    lista.add(m);
                    if (!m.id.isEmpty()) ids.add(m.id);
                }
            }
        } catch (Exception ignored) { }
    }

    private static List<Medicion> parsearArray(JSONArray arr) {
        List<Medicion> out = new ArrayList<>();
        for (int i = 0; i < arr.length(); i++) {
            JSONObject o = arr.optJSONObject(i);
            if (o == null) continue;
            Medicion m = new Medicion();
            m.id = o.optString("id", "");
            m.fecha = o.optString("fecha", "");
            m.nombre = o.optString("nombre_dispositivo", "");
            m.marca = o.optString("marca", "");
            m.modelo = o.optString("modelo", "");
            m.so = o.optString("so", "");
            m.conexion = o.optString("conexion", "");
            m.tecnologia = o.optString("tecnologia", "");
            m.empresa = o.optString("empresa", "");
            m.lugar = o.optString("lugar", "");
            m.ciudad = o.optString("ciudad", "");
            m.origen = o.optString("origen", "");
            m.bajada = num(o, "bajada_mbps");
            m.subida = num(o, "subida_mbps");
            m.latencia = num(o, "latencia_ms");
            m.jitter = num(o, "jitter_ms");
            m.dbm = num(o, "senal_dbm");
            out.add(m);
        }
        return out;
    }

    private static Double num(JSONObject o, String k) {
        if (!o.has(k) || o.isNull(k)) return null;
        Object v = o.opt(k);
        if (v instanceof Number) {
            double d = ((Number) v).doubleValue();
            return Double.isNaN(d) ? null : d;
        }
        String s = String.valueOf(v).trim();
        if (s.isEmpty()) return null;
        try {
            double d = Double.parseDouble(s.replace(',', '.'));
            return Double.isNaN(d) ? null : d;
        } catch (Exception e) {
            return null;
        }
    }

    // ======================================================================
    // Lógica estadística
    // ======================================================================
    private List<Medicion> filtrados() {
        List<Medicion> res = new ArrayList<>();
        for (Medicion m : datos) {
            if (!fConexion.isEmpty() && !fConexion.equals(m.conexion)) continue;
            if (!fEmpresa.isEmpty() && !fEmpresa.equals(m.empresa)) continue;
            res.add(m);
        }
        return res;
    }

    private static Double valorMetrica(Medicion m, String metrica) {
        switch (metrica) {
            case "subida_mbps": return m.subida;
            case "latencia_ms": return m.latencia;
            case "jitter_ms": return m.jitter;
            case "senal_dbm": return m.dbm;
            default: return m.bajada;
        }
    }

    private static String valorGrupo(Medicion m, String campo) {
        String v;
        switch (campo) {
            case "tecnologia": v = m.tecnologia; break;
            case "empresa": v = m.empresa; break;
            case "so": v = m.so; break;
            case "marca": v = m.marca; break;
            case "origen": v = m.origen; break;
            default: v = m.conexion; break;
        }
        return (v == null || v.trim().isEmpty()) ? "Sin dato" : v.trim();
    }

    private static Resumen calcularResumen(List<Double> vals) {
        List<Double> v = new ArrayList<>();
        for (Double x : vals) if (x != null && !x.isNaN()) v.add(x);
        Collections.sort(v);
        Resumen r = new Resumen();
        r.n = v.size();
        if (r.n == 0) return r;
        double sum = 0;
        for (double x : v) sum += x;
        r.media = sum / r.n;
        r.mediana = (r.n % 2 == 1) ? v.get((r.n - 1) / 2) : (v.get(r.n / 2 - 1) + v.get(r.n / 2)) / 2.0;
        if (r.n > 1) {
            double sq = 0;
            for (double x : v) sq += Math.pow(x - r.media, 2);
            r.desvio = Math.sqrt(sq / (r.n - 1));
        }
        r.min = v.get(0);
        r.max = v.get(r.n - 1);
        return r;
    }

    private static Map<String, List<Medicion>> agrupar(List<Medicion> lista, String campo) {
        Map<String, List<Medicion>> g = new LinkedHashMap<>();
        for (Medicion m : lista) {
            String k = valorGrupo(m, campo);
            List<Medicion> bucket = g.get(k);
            if (bucket == null) {
                bucket = new ArrayList<>();
                g.put(k, bucket);
            }
            bucket.add(m);
        }
        return g;
    }

    // ======================================================================
    // Renderizado de UI
    // ======================================================================
    private void render() {
        if (tema == null || kN == null) return;
        List<Medicion> lista = filtrados();

        // KPIs
        List<Double> bajadas = new ArrayList<>(), subidas = new ArrayList<>(), lats = new ArrayList<>();
        int movil = 0;
        for (Medicion m : lista) {
            bajadas.add(m.bajada);
            subidas.add(m.subida);
            lats.add(m.latencia);
            if ("Datos móviles".equals(m.conexion)) movil++;
        }
        Resumen rBaj = calcularResumen(bajadas);
        Resumen rSub = calcularResumen(subidas);
        Resumen rLat = calcularResumen(lats);

        kN.setText(String.valueOf(lista.size()));
        kBajada.setText(rBaj.n > 0 ? MainActivity.fmt(rBaj.media, 1) : "—");
        kSubida.setText(rSub.n > 0 ? MainActivity.fmt(rSub.media, 1) : "—");
        kLat.setText(rLat.n > 0 ? MainActivity.fmt(rLat.media, 0) : "—");
        kDatos.setText(lista.isEmpty() ? "—" : MainActivity.fmt(100.0 * movil / lista.size(), 0) + "%");

        // Medidas por grupo
        limpiarContenidoTarjeta(boxResumen);
        Map<String, List<Medicion>> grupos = agrupar(lista, fVariable);
        List<Map.Entry<String, Resumen>> filas = new ArrayList<>();
        for (Map.Entry<String, List<Medicion>> e : grupos.entrySet()) {
            List<Double> vals = new ArrayList<>();
            for (Medicion m : e.getValue()) vals.add(valorMetrica(m, fMetrica));
            filas.add(new HashMap.SimpleEntry<>(e.getKey(), calcularResumen(vals)));
        }
        filas.sort((a, b) -> {
            double ma = a.getValue().n > 0 ? a.getValue().media : -1e9;
            double mb = b.getValue().n > 0 ? b.getValue().media : -1e9;
            return Double.compare(mb, ma);
        });

        if (filas.isEmpty()) {
            boxResumen.addView(texto("Sin mediciones para los filtros seleccionados.", 13, tema.muted, false));
        } else {
            for (Map.Entry<String, Resumen> e : filas) {
                Resumen r = e.getValue();
                LinearLayout item = new LinearLayout(ctx);
                item.setOrientation(LinearLayout.VERTICAL);
                item.setPadding(dp(10), dp(8), dp(10), dp(8));
                item.setBackground(fondo(tema.surfaceAlt, tema.line, 8));
                LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(-1, -2);
                lp.topMargin = dp(8);
                boxResumen.addView(item, lp);

                LinearLayout top = new LinearLayout(ctx);
                top.setOrientation(LinearLayout.HORIZONTAL);
                TextView nom = texto(e.getKey(), 14, tema.fg, true);
                TextView cnt = texto("n=" + r.n, 12, tema.accent, true);
                cnt.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
                top.addView(nom, new LinearLayout.LayoutParams(0, -2, 1f));
                top.addView(cnt);
                item.addView(top);

                String statsTxt = r.n == 0 ? "Sin datos numéricos" : String.format(
                        Locale.ROOT,
                        "Media %s · Mediana %s · Desvío %s\nMín %s · Máx %s",
                        MainActivity.fmt(r.media, 1), MainActivity.fmt(r.mediana, 1),
                        MainActivity.fmt(r.desvio, 1), MainActivity.fmt(r.min, 1), MainActivity.fmt(r.max, 1));
                TextView det = texto(statsTxt, 12, tema.fgSecondary, false);
                det.setTypeface(Typeface.MONOSPACE);
                LinearLayout.LayoutParams lpd = new LinearLayout.LayoutParams(-1, -2);
                lpd.topMargin = dp(4);
                item.addView(det, lpd);
            }
        }

        // Gráfico: Promedio por grupo
        limpiarContenidoTarjeta(boxGrafGrupo);
        List<String> gLabels = new ArrayList<>();
        List<Double> gVals = new ArrayList<>();
        for (Map.Entry<String, Resumen> e : filas) {
            if (e.getValue().n > 0) {
                gLabels.add(e.getKey());
                gVals.add(Math.abs(e.getValue().media));
            }
        }
        List<Double> gValsReales = new ArrayList<>();
        for (Map.Entry<String, Resumen> e : filas) {
            if (e.getValue().n > 0) gValsReales.add(e.getValue().media);
        }
        dibujarBarrasHorizontales(boxGrafGrupo, gLabels, gValsReales, true);

        // Gráfico: Histograma
        limpiarContenidoTarjeta(boxGrafHist);
        List<Double> metricVals = new ArrayList<>();
        for (Medicion m : lista) {
            Double v = valorMetrica(m, fMetrica);
            if (v != null) metricVals.add(v);
        }
        dibujarHistograma(boxGrafHist, metricVals);

        // Gráfico: Dispersión Shannon
        limpiarContenidoTarjeta(boxScatter);
        if (lista.isEmpty()) {
            boxScatter.addView(texto("Sin mediciones para graficar.", 13, tema.muted, false));
        } else {
            ShannonScatterView scatter = new ShannonScatterView(ctx, lista, tema);
            LinearLayout.LayoutParams lps = new LinearLayout.LayoutParams(-1, dp(230));
            lps.topMargin = dp(8);
            boxScatter.addView(scatter, lps);

            TextView ley = texto("● WiFi   ● Datos móviles   --- Cota Shannon (20 MHz, 25 °C)", 11, tema.muted, false);
            ley.setTypeface(Typeface.MONOSPACE);
            LinearLayout.LayoutParams lpl = new LinearLayout.LayoutParams(-1, -2);
            lpl.topMargin = dp(6);
            boxScatter.addView(ley, lpl);
        }

        // Gráficos: SO y Marcas
        renderConteo(boxGrafSO, lista, "so");
        renderConteo(boxGrafMarca, lista, "marca");

        // Últimas mediciones
        limpiarContenidoTarjeta(boxRecientes);
        if (lista.isEmpty()) {
            boxRecientes.addView(texto("No hay mediciones registradas.", 13, tema.muted, false));
        } else {
            int maxItems = Math.min(15, lista.size());
            for (int i = 0; i < maxItems; i++) {
                Medicion m = lista.get(lista.size() - 1 - i);
                LinearLayout row = new LinearLayout(ctx);
                row.setOrientation(LinearLayout.VERTICAL);
                row.setPadding(0, dp(8), 0, dp(8));
                String eq = !m.nombre.isEmpty() ? m.nombre : (m.marca + " " + m.modelo).trim();
                if (eq.isEmpty()) eq = "Dispositivo";
                String head = eq + (m.empresa.isEmpty() ? "" : " · " + m.empresa);
                String red = m.conexion + (m.tecnologia.isEmpty() ? "" : " " + m.tecnologia)
                        + (m.dbm != null ? " · " + MainActivity.fmt(m.dbm, 0) + " dBm" : "");
                String vel = String.format(Locale.ROOT, "↓ %s Mbps  ↑ %s Mbps  · %s ms",
                        m.bajada != null ? MainActivity.fmt(m.bajada, 1) : "—",
                        m.subida != null ? MainActivity.fmt(m.subida, 1) : "—",
                        m.latencia != null ? MainActivity.fmt(m.latencia, 0) : "—");
                row.addView(texto(head, 13, tema.fg, true));
                TextView tv2 = texto(red, 12, tema.muted, false);
                row.addView(tv2);
                TextView tv3 = texto(vel, 12, tema.accent, true);
                tv3.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
                row.addView(tv3);
                boxRecientes.addView(row);

                if (i < maxItems - 1) {
                    View div = new View(ctx);
                    div.setBackgroundColor(tema.line);
                    boxRecientes.addView(div, new LinearLayout.LayoutParams(-1, Math.max(1, dp(1))));
                }
            }
        }
    }

    private void renderConteo(LinearLayout box, List<Medicion> lista, String campo) {
        limpiarContenidoTarjeta(box);
        Map<String, List<Medicion>> g = agrupar(lista, campo);
        List<Map.Entry<String, Integer>> pares = new ArrayList<>();
        for (Map.Entry<String, List<Medicion>> e : g.entrySet()) {
            pares.add(new HashMap.SimpleEntry<>(e.getKey(), e.getValue().size()));
        }
        pares.sort((a, b) -> Integer.compare(b.getValue(), a.getValue()));
        List<String> labels = new ArrayList<>();
        List<Double> vals = new ArrayList<>();
        int lim = Math.min(8, pares.size());
        for (int i = 0; i < lim; i++) {
            labels.add(pares.get(i).getKey());
            vals.add((double) pares.get(i).getValue());
        }
        dibujarBarrasHorizontales(box, labels, vals, false);
    }

    private void dibujarHistograma(LinearLayout box, List<Double> vals) {
        if (vals.isEmpty()) {
            box.addView(texto("Sin datos para el histograma.", 13, tema.muted, false));
            return;
        }
        double min = Collections.min(vals), max = Collections.max(vals);
        if (Math.abs(max - min) < 1e-6) {
            List<String> l = Collections.singletonList(MainActivity.fmt(min, 1));
            List<Double> v = Collections.singletonList((double) vals.size());
            dibujarBarrasHorizontales(box, l, v, false);
            return;
        }
        int bins = 6;
        double paso = (max - min) / bins;
        int[] cuentas = new int[bins];
        for (double x : vals) {
            int idx = (int) Math.floor((x - min) / paso);
            if (idx >= bins) idx = bins - 1;
            if (idx < 0) idx = 0;
            cuentas[idx]++;
        }
        List<String> labels = new ArrayList<>();
        List<Double> cVals = new ArrayList<>();
        for (int i = 0; i < bins; i++) {
            double a = min + i * paso, b = a + paso;
            labels.add(MainActivity.fmt(a, 0) + " – " + MainActivity.fmt(b, 0));
            cVals.add((double) cuentas[i]);
        }
        dibujarBarrasHorizontales(box, labels, cVals, false);
    }

    private void dibujarBarrasHorizontales(LinearLayout box, List<String> labels, List<Double> vals, boolean decimales) {
        if (labels.isEmpty()) {
            box.addView(texto("Sin datos disponibles.", 13, tema.muted, false));
            return;
        }
        double maxAbs = 0;
        for (double v : vals) maxAbs = Math.max(maxAbs, Math.abs(v));
        if (maxAbs <= 0) maxAbs = 1;

        int[] paleta = {tema.accent, tema.good, tema.amber, tema.violet, tema.bad};
        for (int i = 0; i < labels.size(); i++) {
            double v = vals.get(i);
            LinearLayout row = new LinearLayout(ctx);
            row.setOrientation(LinearLayout.VERTICAL);
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(-1, -2);
            lp.topMargin = dp(8);

            LinearLayout top = new LinearLayout(ctx);
            top.setOrientation(LinearLayout.HORIZONTAL);
            TextView lbl = texto(labels.get(i), 12, tema.fgSecondary, true);
            TextView numTxt = texto(decimales ? MainActivity.fmt(v, 1) : MainActivity.fmt(v, 0), 12, tema.fg, true);
            numTxt.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
            top.addView(lbl, new LinearLayout.LayoutParams(0, -2, 1f));
            top.addView(numTxt);
            row.addView(top);

            LinearLayout track = new LinearLayout(ctx);
            track.setBackground(fondo(tema.surfaceAlt, tema.line, 4));
            LinearLayout.LayoutParams lpTrack = new LinearLayout.LayoutParams(-1, dp(10));
            lpTrack.topMargin = dp(3);

            float frac = (float) Math.max(0.03, Math.min(1.0, Math.abs(v) / maxAbs));
            View bar = new View(ctx);
            GradientDrawable bgBar = new GradientDrawable();
            bgBar.setColor(paleta[i % paleta.length]);
            bgBar.setCornerRadius(dp(4));
            bar.setBackground(bgBar);
            track.addView(bar, new LinearLayout.LayoutParams(0, -1, frac));
            if (frac < 1f) {
                track.addView(new View(ctx), new LinearLayout.LayoutParams(0, -1, 1f - frac));
            }
            row.addView(track, lpTrack);
            box.addView(row, lp);
        }
    }

    // ======================================================================
    // Gráfico de dispersión nativo con curva de Shannon
    // ======================================================================
    static class ShannonScatterView extends View {
        private final List<Medicion> lista;
        private final Tema t;
        private final Paint pAxis = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint pGrid = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint pCurve = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint pDot = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint pText = new Paint(Paint.ANTI_ALIAS_FLAG);

        ShannonScatterView(Context ctx, List<Medicion> lista, Tema t) {
            super(ctx);
            this.lista = lista;
            this.t = t;
            float d = ctx.getResources().getDisplayMetrics().density;

            pAxis.setColor(t.lineStrong);
            pAxis.setStrokeWidth(1.5f * d);
            pGrid.setColor(t.line);
            pGrid.setStrokeWidth(1f * d);

            pCurve.setColor(t.accent);
            pCurve.setStyle(Paint.Style.STROKE);
            pCurve.setStrokeWidth(2.5f * d);
            pCurve.setPathEffect(new DashPathEffect(new float[]{6f * d, 4f * d}, 0));

            pText.setColor(t.muted);
            pText.setTextSize(10f * d);
            pText.setTypeface(Typeface.MONOSPACE);
        }

        @Override
        protected void onDraw(Canvas c) {
            super.onDraw(c);
            float d = getResources().getDisplayMetrics().density;
            float padL = 36f * d, padR = 12f * d, padT = 12f * d, padB = 26f * d;
            float w = getWidth() - padL - padR;
            float h = getHeight() - padT - padB;
            if (w <= 0 || h <= 0) return;

            double minX = -120, maxX = -35;
            double maxY = 50;
            for (Medicion m : lista) {
                if (m.bajada != null && m.bajada > maxY) maxY = m.bajada;
            }
            maxY = Math.ceil(maxY / 25.0) * 25.0;

            // Grilla horizontal
            for (int i = 0; i <= 4; i++) {
                float y = padT + h - (h * i / 4f);
                c.drawLine(padL, y, padL + w, y, pGrid);
                String lbl = String.valueOf(Math.round(maxY * i / 4.0));
                c.drawText(lbl, 2f * d, y + 3f * d, pText);
            }
            // Grilla vertical
            for (int dbm = -120; dbm <= -40; dbm += 20) {
                float x = (float) (padL + w * (dbm - minX) / (maxX - minX));
                c.drawLine(x, padT, x, padT + h, pGrid);
                c.drawText(dbm + "", x - 12f * d, padT + h + 16f * d, pText);
            }
            // Ejes
            c.drawLine(padL, padT, padL, padT + h, pAxis);
            c.drawLine(padL, padT + h, padL + w, padT + h, pAxis);

            // Curva práctica de Shannon (20 MHz, 25 °C)
            Path path = new Path();
            boolean first = true;
            double B = 20.0, T = 25.0;
            double nDbm = MainActivity.ruidoTermicoDbm(B, T);
            int sub = 12 * 5 * (int) Math.round(B);
            for (int dbm = -118; dbm <= -40; dbm += 2) {
                double sDbm = dbm + 10.0 * Math.log10(sub);
                double snrIdeal = sDbm - nDbm;
                double snrPractico = Math.max(-10.0, snrIdeal - 22.0);
                double cap = MainActivity.shannonMbps(B, snrPractico) * 0.65;
                float x = (float) (padL + w * (dbm - minX) / (maxX - minX));
                float y = (float) (padT + h - h * Math.min(1.0, cap / maxY));
                if (first) { path.moveTo(x, y); first = false; }
                else path.lineTo(x, y);
            }
            c.drawPath(path, pCurve);

            // Puntos empíricos
            for (Medicion m : lista) {
                if (m.bajada == null) continue;
                double dbm = m.dbm != null ? m.dbm : ("WiFi".equals(m.conexion) ? -70 : "Datos móviles".equals(m.conexion) ? -95 : -999);
                if (dbm < -150) continue;
                dbm = Math.max(minX, Math.min(maxX, dbm));
                float x = (float) (padL + w * (dbm - minX) / (maxX - minX));
                float y = (float) (padT + h - h * Math.min(1.0, m.bajada / maxY));
                int col = "WiFi".equals(m.conexion) ? t.good : ("Cable".equals(m.conexion) ? t.violet : t.amber);
                pDot.setColor(col);
                c.drawCircle(x, y, 4.5f * d, pDot);
            }
        }
    }

    // ======================================================================
    // Spinners y helpers UI
    // ======================================================================
    private Spinner agregarSpinner(LinearLayout padre, String etiqueta, String[] items) {
        TextView lbl = texto(etiqueta, 12, tema.muted, true);
        LinearLayout.LayoutParams lpl = new LinearLayout.LayoutParams(-1, -2);
        lpl.topMargin = dp(8);
        padre.addView(lbl, lpl);

        Spinner sp = new Spinner(ctx);
        sp.setBackground(fondo(tema.surfaceElevated, tema.lineStrong, 8));
        sp.setPadding(dp(8), dp(6), dp(8), dp(6));
        setAdapter(sp, items);
        LinearLayout.LayoutParams lps = new LinearLayout.LayoutParams(-1, -2);
        lps.topMargin = dp(4);
        padre.addView(sp, lps);
        return sp;
    }

    private void setAdapter(Spinner sp, String[] items) {
        ArrayAdapter<String> ad = new ArrayAdapter<String>(ctx, android.R.layout.simple_spinner_item, items) {
            @Override
            public View getView(int pos, View conv, ViewGroup parent) {
                TextView v = (TextView) super.getView(pos, conv, parent);
                v.setTextColor(tema.fg);
                v.setTextSize(14);
                return v;
            }

            @Override
            public View getDropDownView(int pos, View conv, ViewGroup parent) {
                TextView v = (TextView) super.getDropDownView(pos, conv, parent);
                v.setTextColor(tema.fg);
                v.setBackgroundColor(tema.surfaceElevated);
                v.setPadding(dp(14), dp(12), dp(14), dp(12));
                v.setTextSize(14);
                return v;
            }
        };
        sp.setAdapter(ad);
    }

    private void sincronizarSpinners() {
        if (sConexion == null) return;
        ignorarEventosSpinner = true;

        String[] conOpts = {"Todas", "WiFi", "Datos móviles", "Cable"};
        int idxCon = 0;
        for (int i = 1; i < conOpts.length; i++) if (conOpts[i].equals(fConexion)) idxCon = i;
        sConexion.setSelection(idxCon, false);

        Set<String> empSet = new HashSet<>();
        for (Medicion m : datos) if (!m.empresa.isEmpty()) empSet.add(m.empresa);
        List<String> emps = new ArrayList<>(empSet);
        Collections.sort(emps);
        emps.add(0, "Todas");
        setAdapter(sEmpresa, emps.toArray(new String[0]));
        int idxEmp = emps.indexOf(fEmpresa);
        if (idxEmp < 0) { idxEmp = 0; fEmpresa = ""; }
        sEmpresa.setSelection(idxEmp, false);

        String[] varKeys = {"conexion", "tecnologia", "empresa", "so", "marca", "origen"};
        int idxVar = 0;
        for (int i = 0; i < varKeys.length; i++) if (varKeys[i].equals(fVariable)) idxVar = i;
        sVariable.setSelection(idxVar, false);

        String[] metKeys = {"bajada_mbps", "subida_mbps", "latencia_ms", "jitter_ms", "senal_dbm"};
        int idxMet = 0;
        for (int i = 0; i < metKeys.length; i++) if (metKeys[i].equals(fMetrica)) idxMet = i;
        sMetrica.setSelection(idxMet, false);

        ignorarEventosSpinner = false;
    }

    private void configurarListenersSpinners() {
        AdapterView.OnItemSelectedListener listener = new AdapterView.OnItemSelectedListener() {
            @Override
            public void onItemSelected(AdapterView<?> parent, View view, int pos, long id) {
                if (ignorarEventosSpinner) return;
                if (parent == sConexion) {
                    fConexion = pos == 0 ? "" : (String) sConexion.getSelectedItem();
                } else if (parent == sEmpresa) {
                    fEmpresa = pos == 0 ? "" : (String) sEmpresa.getSelectedItem();
                } else if (parent == sVariable) {
                    String[] varKeys = {"conexion", "tecnologia", "empresa", "so", "marca", "origen"};
                    fVariable = varKeys[pos];
                } else if (parent == sMetrica) {
                    String[] metKeys = {"bajada_mbps", "subida_mbps", "latencia_ms", "jitter_ms", "senal_dbm"};
                    fMetrica = metKeys[pos];
                }
                render();
            }

            @Override
            public void onNothingSelected(AdapterView<?> parent) { }
        };
        sConexion.setOnItemSelectedListener(listener);
        sEmpresa.setOnItemSelectedListener(listener);
        sVariable.setOnItemSelectedListener(listener);
        sMetrica.setOnItemSelectedListener(listener);
    }

    private TextView crearKpiTile(LinearLayout fila, String label, boolean margenDer) {
        LinearLayout tile = new LinearLayout(ctx);
        tile.setOrientation(LinearLayout.VERTICAL);
        tile.setPadding(dp(12), dp(10), dp(12), dp(10));
        tile.setBackground(fondo(tema.surfaceAlt, tema.line, 8));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(0, -2, 1f);
        if (margenDer) lp.rightMargin = dp(8);
        fila.addView(tile, lp);

        TextView val = texto("—", 22, tema.fg, true);
        val.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
        tile.addView(val);
        TextView lbl = texto(label, 11, tema.muted, false);
        tile.addView(lbl);
        return val;
    }

    private void limpiarContenidoTarjeta(LinearLayout card) {
        // Conserva el índice 0 (título de la tarjeta)
        while (card.getChildCount() > 1) {
            card.removeViewAt(1);
        }
    }

    private int dp(int v) {
        return Math.round(v * ctx.getResources().getDisplayMetrics().density);
    }

    private GradientDrawable fondo(int color, int borde, int radio) {
        GradientDrawable g = new GradientDrawable();
        g.setColor(color);
        g.setCornerRadius(dp(radio));
        g.setStroke(dp(1), borde);
        return g;
    }

    private TextView texto(String txt, int sp, int color, boolean negrita) {
        TextView v = new TextView(ctx);
        v.setText(txt);
        v.setTextSize(sp);
        v.setTextColor(color);
        if (negrita) v.setTypeface(Typeface.DEFAULT_BOLD);
        return v;
    }

    private LinearLayout tarjeta(LinearLayout padre, String titulo) {
        LinearLayout c = new LinearLayout(ctx);
        c.setOrientation(LinearLayout.VERTICAL);
        c.setPadding(dp(16), dp(14), dp(16), dp(16));
        c.setBackground(fondo(tema.surface, tema.line, 10));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(-1, -2);
        lp.topMargin = dp(14);
        padre.addView(c, lp);
        TextView t = texto(titulo.toUpperCase(Locale.ROOT), 11, tema.muted, true);
        t.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
        t.setLetterSpacing(0.08f);
        c.addView(t);
        return c;
    }
}
