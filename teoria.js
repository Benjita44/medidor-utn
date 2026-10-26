// Cálculos teóricos de la cátedra (Comunicación de Datos, UTN FRT) aplicados a las mediciones.
// Unidad 3: dBm, RSSI, ruido térmico (kTB) y SNR. Unidad 5: Shannon. Unidad 7: velocidades por generación.
window.TEORIA = (() => {
  const K = 1.38e-23; // constante de Boltzmann [J/K]

  // Unidad 3: P[mW] = 10^(dBm/10)
  const dbmAmW = (dbm) => Math.pow(10, dbm / 10);

  // Muestra una potencia en la unidad más legible (mW, µW, nW, pW, fW, aW)
  function potenciaLegible(mW) {
    const u = [["mW", 1], ["µW", 1e-3], ["nW", 1e-6], ["pW", 1e-9], ["fW", 1e-12]];
    for (const [nombre, f] of u) if (mW >= f) return `${(mW / f).toLocaleString("es-AR", { maximumFractionDigits: 2 })} ${nombre}`;
    return `${(mW / 1e-15).toLocaleString("es-AR", { maximumFractionDigits: 2 })} aW`;
  }

  // Unidad 3: N = k·T·B ; N[dBm] = 10·log10(kTB / 1 mW)
  const ruidoTermicoDbm = (anchoMHz, tempC) => 10 * Math.log10((K * (tempC + 273) * anchoMHz * 1e6) / 1e-3);

  // En 4G/5G Android muestra RSRP: la potencia de UNA subportadora de 15 kHz.
  // Un canal LTE de B MHz tiene 5·B bloques de 12 subportadoras, así que la potencia total
  // (sumando en dB, como en la Unidad 3) es: S = RSRP + 10·log10(12 · 5 · B).
  // En WiFi el RSSI ya es la potencia de todo el canal.
  const subportadoras = (anchoMHz) => 12 * 5 * anchoMHz;
  const potenciaTotalDbm = (dbm, anchoMHz, conexion) =>
    conexion === "WiFi" ? dbm : dbm + 10 * Math.log10(subportadoras(anchoMHz));

  // Unidad 3: SNR[dB] = S[dBm] - N[dBm]
  const snrDb = (dbm, anchoMHz, tempC, conexion) => potenciaTotalDbm(dbm, anchoMHz, conexion) - ruidoTermicoDbm(anchoMHz, tempC);

  // Unidad 5: C = B·log2(1 + S/N)
  const shannonMbps = (anchoMHz, snr) => anchoMHz * Math.log2(1 + Math.pow(10, snr / 10));

  // Curva teórica de referencia C(dBm) para superponer en el gráfico de dispersión.
  // Usa una eficiencia espectral práctica (ajustada por overhead de protocolo y ruido de figura de receptor NF ~ 9 dB)
  // y la cota superior ideal de Shannon-Hartley.
  function curvaShannon(anchoMHz, tempC, conexion = "Datos móviles", dbmMin = -118, dbmMax = -48, paso = 2) {
    const puntos = [];
    for (let dbm = dbmMin; dbm <= dbmMax; dbm += paso) {
      // Incorporamos figura de ruido típica de receptor móvil/WiFi + interferencia base (NF efectiva ~ 18 dB)
      // para que la curva sea visualmente comparable en escala con las bajadas empíricas, además del valor ideal.
      const snrIdeal = snrDb(dbm, anchoMHz, tempC, conexion);
      const snrPractico = Math.max(-10, snrIdeal - 22);
      const cIdeal = shannonMbps(anchoMHz, snrIdeal);
      const cPractica = shannonMbps(anchoMHz, snrPractico) * 0.65;
      puntos.push({ x: dbm, y: +cPractica.toFixed(1), ideal: +cIdeal.toFixed(0) });
    }
    return puntos;
  }

  // Rangos de calidad. WiFi: RSSI de la Unidad 3 (sensibilidad típica -82 dBm).
  // Celular: rangos usuales del nivel de señal 4G (RSRP) que muestra Android.
  function calidad(dbm, conexion) {
    const r = conexion === "WiFi"
      ? [[-50, "Excelente"], [-67, "Buena"], [-75, "Regular"], [-82, "Mala"]]
      : [[-80, "Excelente"], [-90, "Buena"], [-100, "Regular"], [-110, "Mala"]];
    for (const [lim, txt] of r) if (dbm >= lim) return txt;
    return "Sin servicio útil";
  }

  // Unidad 7: Ancho de banda digital, servicios WAN inalámbricos (velocidad máxima teórica)
  const MAXIMOS = {
    "2G": { texto: "EDGE: hasta 473,6 Kbps", mbps: 0.4736 },
    "3G": { texto: "UMTS hasta 2 Mbps · HSPA+ hasta 42 Mbps", mbps: 42 },
    "4G": { texto: "LTE: 100 Mbps a 1 Gbps", mbps: 100 },
    "5G": { texto: "5G NR: varios Gbps", mbps: 1000 },
  };

  // Cálculo de capacidad para canales guiados (Par trenzado UTP Ethernet IEEE 802.3ab 1000BASE-T / 100BASE-TX)
  // Combina el Teorema de Nyquist (C = 2·B·log2(M)) y el Teorema de Shannon-Hartley (C = B·log2(1 + S/N)),
  // más el despeje inverso de la SNR mínima requerida por el caudal medido.
  function calcularCable(bajadaMbps, anchoMHz, tempC = 25) {
    // En 1000BASE-T (Cat 5e / Cat 6) se usan 4 pares trenzados de 62,5 MHz c/u (B agregado = 250 MHz) y PAM-5 (M = 5 niveles).
    const bEfectivo = anchoMHz && anchoMHz > 0 ? anchoMHz : 250;
    const pares = 4;
    const bPorPar = bEfectivo / pares;
    const nivelesM = 5; // Modulación 4D-PAM5 (±1 V, ±0.5 V, 0 V)
    const snrDisenoDb = 24.0; // SNR típica en el receptor ecualizado frente a diafonía NEXT/FEXT para BER < 10^-10
    const cNyquist = 2 * bEfectivo * Math.log2(nivelesM); // C_Nyquist = 2·B·log2(M) => ~1161 Mbps brutos en 250 MHz (1000 Mbps netos con FEC Trellis)
    const cEstándar = 1000; // 1000BASE-T nominal [Mbps]
    const cShannon = shannonMbps(bEfectivo, snrDisenoDb);
    const ruidoTermico = ruidoTermicoDbm(bEfectivo, tempC);

    // Despeje inverso de Shannon: R = B·log2(1 + S/N) => S/N = 2^(R/B) - 1
    let snrMinDb = null;
    if (bajadaMbps != null && bajadaMbps > 0 && bEfectivo > 0) {
      const snrLin = Math.pow(2, bajadaMbps / bEfectivo) - 1;
      if (snrLin > 0) snrMinDb = 10 * Math.log10(snrLin);
    }

    return {
      estandar: "IEEE 802.3ab (1000BASE-T · Cat 5e/6)",
      modulacion: "4D-PAM5 (5 niveles, ±1 V en 100 Ω)",
      pares,
      bEfectivo,
      bPorPar,
      nivelesM,
      snrDisenoDb,
      cNyquist,
      cEstandar: cEstándar,
      cShannon,
      ruidoTermico,
      snrMinDb,
    };
  }

  // Correlación lineal de Pearson
  function pearson(xs, ys) {
    const n = xs.length;
    if (n < 3) return null;
    const mx = xs.reduce((a, b) => a + b, 0) / n, my = ys.reduce((a, b) => a + b, 0) / n;
    let sxy = 0, sxx = 0, syy = 0;
    for (let i = 0; i < n; i++) { sxy += (xs[i] - mx) * (ys[i] - my); sxx += (xs[i] - mx) ** 2; syy += (ys[i] - my) ** 2; }
    return sxx && syy ? sxy / Math.sqrt(sxx * syy) : null;
  }

  // Sanitiza el campo version_so cuando Google Sheets corrompe un string numérico ("17.5") en una fecha ISO ("2026-07-18T03:00:00.000Z")
  function limpiarVersionSO(v) {
    if (v == null || v === "") return "";
    const s = String(v).trim();
    if (/^\d{4}-\d{2}-\d{2}T/.test(s)) {
      const d = new Date(s);
      if (!Number.isNaN(d.getTime())) return `${d.getUTCDate()}.${d.getUTCMonth() + 1}`;
      return "";
    }
    return s;
  }

  return {
    dbmAmW,
    potenciaLegible,
    ruidoTermicoDbm,
    subportadoras,
    potenciaTotalDbm,
    snrDb,
    shannonMbps,
    curvaShannon,
    calidad,
    MAXIMOS,
    calcularCable,
    pearson,
    limpiarVersionSO,
  };
})();
