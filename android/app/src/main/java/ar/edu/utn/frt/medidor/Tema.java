package ar.edu.utn.frt.medidor;

import android.content.Context;
import android.content.SharedPreferences;
import android.content.res.Configuration;
import android.graphics.Color;

/**
 * Paleta cromática y gestión de tema (Sistema, Claro, Oscuro)
 * alineada con DESIGN.md ("Cuaderno de Telemetría e Instrumentación RF").
 */
public class Tema {

    public static final String MODO_SISTEMA = "sistema";
    public static final String MODO_CLARO = "claro";
    public static final String MODO_OSCURO = "oscuro";

    public final boolean oscuro;
    public final String modo;

    public final int bg;
    public final int surface;
    public final int surfaceAlt;
    public final int surfaceElevated;
    public final int fg;
    public final int fgSecondary;
    public final int muted;
    public final int line;
    public final int lineStrong;
    public final int accent;
    public final int accentSoft;
    public final int amber;
    public final int good;
    public final int bad;
    public final int violet;

    private Tema(String modo, boolean oscuro) {
        this.modo = modo;
        this.oscuro = oscuro;
        if (oscuro) {
            // Modo Oscuro (Osciloscopio Nocturno)
            bg = Color.parseColor("#0A0F1A");
            surface = Color.parseColor("#111827");
            surfaceAlt = Color.parseColor("#182235");
            surfaceElevated = Color.parseColor("#1E293B");
            fg = Color.parseColor("#F1F5F9");
            fgSecondary = Color.parseColor("#CBD5E1");
            muted = Color.parseColor("#94A3B8");
            line = Color.parseColor("#24324B");
            lineStrong = Color.parseColor("#384B6B");
            accent = Color.parseColor("#6096FF");
            accentSoft = Color.parseColor("#1E3A5F");
            amber = Color.parseColor("#F59E0B");
            good = Color.parseColor("#34D399");
            bad = Color.parseColor("#F87171");
            violet = Color.parseColor("#A78BFA");
        } else {
            // Modo Claro (Papel Técnico de Alta Luminancia)
            bg = Color.parseColor("#F5F4EE");
            surface = Color.parseColor("#FDFCF9");
            surfaceAlt = Color.parseColor("#ECEAE1");
            surfaceElevated = Color.parseColor("#FFFFFF");
            fg = Color.parseColor("#0F172A");
            fgSecondary = Color.parseColor("#334155");
            muted = Color.parseColor("#526074");
            line = Color.parseColor("#D5D2C6");
            lineStrong = Color.parseColor("#989484");
            accent = Color.parseColor("#1849C6");
            accentSoft = Color.parseColor("#E0E8FA");
            amber = Color.parseColor("#945300");
            good = Color.parseColor("#117A43");
            bad = Color.parseColor("#B93829");
            violet = Color.parseColor("#7C3AED");
        }
    }

    public static boolean sistemaEsOscuro(Context ctx) {
        int night = ctx.getResources().getConfiguration().uiMode & Configuration.UI_MODE_NIGHT_MASK;
        return night == Configuration.UI_MODE_NIGHT_YES;
    }

    public static String cargarModo(SharedPreferences prefs) {
        String m = prefs.getString("tema", MODO_SISTEMA);
        if (!MODO_CLARO.equals(m) && !MODO_OSCURO.equals(m)) return MODO_SISTEMA;
        return m;
    }

    public static void guardarModo(SharedPreferences prefs, String modo) {
        prefs.edit().putString("tema", modo).apply();
    }

    public static Tema resolver(Context ctx, SharedPreferences prefs) {
        String modo = cargarModo(prefs);
        boolean osc;
        if (MODO_OSCURO.equals(modo)) osc = true;
        else if (MODO_CLARO.equals(modo)) osc = false;
        else osc = sistemaEsOscuro(ctx);
        return new Tema(modo, osc);
    }
}
