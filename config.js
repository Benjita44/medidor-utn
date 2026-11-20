// Configuración del Medidor de Señal (UTN FRT · Comunicación)
window.CONFIG = {
  // Endpoint de nuestro Backend Propio (por defecto "/api/mediciones" servido por server.js).
  // También podés pegar aquí la URL de tu propia implementación de Google Apps Script (ver backend/google-apps-script.gs),
  // o dejarla vacía ("") para operar exclusivamente con almacenamiento en navegador.
  SCRIPT_URL: "https://script.google.com/macros/s/AKfycbw3aFZwnii8ENYhQgDPl2qAefmcTeZ2HE-QDPz2cuogEJQWPQqG6i21825SfMNFxq5ZVw/exec",

  // Servidor para el test de velocidad (permite CORS, gratuito).
  SPEED_HOST: "https://speed.cloudflare.com",

  // Servicio para detectar el proveedor de internet a partir de la IP pública.
  IP_INFO_URL: "https://ipwho.is/",
};

// Columnas de cada medición (mismo orden que en la planilla de Google Sheets).
window.CAMPOS = [
  "id", "fecha", "tipo_dispositivo", "nombre_dispositivo", "marca", "modelo", "so", "version_so", "navegador",
  "conexion", "tecnologia", "tipo_red_detectado", "empresa", "isp_detectado", "asn",
  "bajada_mbps", "subida_mbps", "latencia_ms", "jitter_ms",
  "senal_dbm", "barras", "lugar", "lat", "lon", "precision_m", "ubicacion_fuente", "ciudad", "comentario", "sinr_db", "ancho_mhz", "origen",
];

window.EMPRESAS = ["Personal", "Claro", "Movistar", "Otra"];
