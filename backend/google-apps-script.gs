// ==========================================================================
// Google Apps Script Propio — UTN FRT · Comunicación (Medidor de Señal)
// ==========================================================================
// INSTRUCCIONES PARA PUBLICAR TU PROPIA PLANILLA EN 1 MINUTO:
// 1. Creá una hoja de cálculo nueva en Google Sheets (https://sheets.new).
// 2. En el menú superior, abrí "Extensiones" -> "Apps Script".
// 3. Borrá el contenido que viene por defecto y pegá todo este archivo.
// 4. Hacé clic en "Implementar" -> "Nueva implementación".
// 5. Elegí el tipo "Aplicación web":
//    - Ejecutar como: "Yo"
//    - Quién tiene acceso: "Cualquier persona"
// 6. Copiá la URL de la aplicación web generada y pegala en `config.js` en `SCRIPT_URL`.
// ==========================================================================

const CAMPOS = [
  "id", "fecha", "tipo_dispositivo", "nombre_dispositivo", "marca", "modelo", "so", "version_so", "navegador",
  "conexion", "tecnologia", "tipo_red_detectado", "empresa", "isp_detectado", "asn",
  "bajada_mbps", "subida_mbps", "latencia_ms", "jitter_ms",
  "senal_dbm", "barras", "lugar", "lat", "lon", "precision_m", "ubicacion_fuente", "ciudad", "comentario", "sinr_db", "ancho_mhz", "origen",
];

const RANGOS_NUMERICOS = {
  bajada_mbps: [0, 10000],
  subida_mbps: [0, 10000],
  latencia_ms: [0, 10000],
  jitter_ms: [0, 10000],
  senal_dbm: [-150, -10],
  barras: [0, 5],
  lat: [-90, 90],
  lon: [-180, 180],
  precision_m: [0, 100000],
  sinr_db: [-40, 60],
  ancho_mhz: [1, 800],
};

function obtenerHoja() {
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  let hoja = ss.getSheetByName("Mediciones") || ss.getSheets()[0];
  if (hoja.getLastRow() === 0) {
    hoja.appendRow(CAMPOS);
    hoja.getRange(1, 1, 1, CAMPOS.length).setFontWeight("bold");
    // Forzar formato de texto plano en la columna version_so (columna 8) para evitar que Sheets convierta "18.7" en una fecha
    hoja.getRange("H:H").setNumberFormat("@");
  }
  return hoja;
}

function doGet() {
  const hoja = obtenerHoja();
  const datos = hoja.getDataRange().getValues();
  if (datos.length <= 1) {
    return respuestaJSON({ ok: true, filas: [] });
  }
  const encabezados = datos[0].map((h) => String(h).trim());
  const filas = datos.slice(1).map((fila) => {
    const obj = {};
    for (let i = 0; i < encabezados.length; i++) {
      obj[encabezados[i]] = fila[i];
    }
    return obj;
  });
  return respuestaJSON({ ok: true, filas });
}

function doPost(e) {
  const lock = LockService.getScriptLock();
  lock.waitLock(10000);
  try {
    const body = JSON.parse(e.postData.contents || "{}");
    const hoja = obtenerHoja();
    const fila = CAMPOS.map((campo) => {
      const val = body[campo];
      if (campo in RANGOS_NUMERICOS) {
        if (val === "" || val == null) return "";
        const num = Number(val);
        const [min, max] = RANGOS_NUMERICOS[campo];
        if (isNaN(num) || num < min || num > max) return "";
        return num;
      }
      if (campo === "version_so") {
        // Prefijo apóstrofo o formato texto para impedir conversión automática a fecha
        return val == null || val === "" ? "" : String(val).trim().slice(0, 32);
      }
      let str = val == null ? "" : String(val).trim().slice(0, 160);
      if (/^[=+\-@]/.test(str)) str = "'" + str;
      return str;
    });
    hoja.appendRow(fila);
    return respuestaJSON({ ok: true });
  } catch (err) {
    return respuestaJSON({ ok: false, error: String(err) });
  } finally {
    lock.releaseLock();
  }
}

function respuestaJSON(obj) {
  return ContentService
    .createTextOutput(JSON.stringify(obj))
    .setMimeType(ContentService.MimeType.JSON);
}
