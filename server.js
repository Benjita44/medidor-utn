// ==========================================================================
// Servidor Local + API de Telemetría (UTN FRT · Comunicación)
// Sin dependencias externas (Node.js puro) · Persistencia en data/mediciones.json
// ==========================================================================

"use strict";

const http = require("http");
const fs = require("fs");
const path = require("path");
const crypto = require("crypto");

const PORT = Number(process.env.PORT) || 3000;
const ROOT_DIR = __dirname;
const DATA_DIR = path.join(ROOT_DIR, "data");
const DATA_FILE = path.join(DATA_DIR, "mediciones.json");

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

// Semilla inicial cargada desde config.js si no existe data/mediciones.json
function obtenerSemillaInicial() {
  try {
    const sandbox = { window: {} };
    const code = fs.readFileSync(path.join(ROOT_DIR, "config.js"), "utf8");
    const fn = new Function("window", code);
    fn(sandbox.window);
    return Array.isArray(sandbox.window.SEMILLA_INICIAL) ? sandbox.window.SEMILLA_INICIAL : [];
  } catch (e) {
    return [];
  }
}

function asegurarBaseDeDatos() {
  if (!fs.existsSync(DATA_DIR)) {
    fs.mkdirSync(DATA_DIR, { recursive: true });
  }
  if (!fs.existsSync(DATA_FILE)) {
    const inicial = obtenerSemillaInicial();
    fs.writeFileSync(DATA_FILE, JSON.stringify(inicial, null, 2), "utf8");
  }
}

function leerMediciones() {
  asegurarBaseDeDatos();
  try {
    const raw = fs.readFileSync(DATA_FILE, "utf8");
    const lista = JSON.parse(raw);
    return Array.isArray(lista) ? lista : [];
  } catch (e) {
    return [];
  }
}

function guardarMediciones(lista) {
  asegurarBaseDeDatos();
  const tmp = DATA_FILE + ".tmp";
  fs.writeFileSync(tmp, JSON.stringify(lista, null, 2), "utf8");
  fs.renameSync(tmp, DATA_FILE);
}

// Sanitiza y valida un registro entrante según las reglas de seguridad de ECC
function sanitizarRegistro(entrada) {
  if (!entrada || typeof entrada !== "object") {
    throw new Error("El cuerpo de la petición debe ser un objeto JSON válido.");
  }

  const limpio = {};
  for (const campo of CAMPOS) {
    const valor = entrada[campo];

    if (campo in RANGOS_NUMERICOS) {
      if (valor === "" || valor == null) {
        limpio[campo] = "";
      } else {
        const num = Number(valor);
        const [min, max] = RANGOS_NUMERICOS[campo];
        if (Number.isNaN(num) || num < min || num > max) {
          throw new Error(`Valor fuera de rango en '${campo}' (${min} a ${max}).`);
        }
        limpio[campo] = num;
      }
    } else if (campo === "id") {
      limpio.id = typeof valor === "string" && valor.trim() ? valor.trim().slice(0, 64) : crypto.randomUUID();
    } else if (campo === "fecha") {
      const f = valor ? new Date(valor) : new Date();
      limpio.fecha = Number.isNaN(f.getTime()) ? new Date().toISOString() : f.toISOString();
    } else {
      let str = valor == null ? "" : String(valor).trim().slice(0, 160);
      // Prevenir inyección de fórmulas en CSV/Excel (=, +, -, @ al inicio)
      if (/^[=+\-@]/.test(str)) {
        str = "'" + str;
      }
      limpio[campo] = str;
    }
  }

  if (!limpio.conexion) throw new Error("El campo 'conexion' es obligatorio.");
  if (!limpio.empresa) throw new Error("El campo 'empresa' es obligatorio.");

  return limpio;
}

// Control de tasa simple por IP (máximo 30 escrituras por minuto)
const ventanaRateLimit = new Map();
function verificarRateLimit(ip) {
  const ahora = Date.now();
  const registro = ventanaRateLimit.get(ip) || { cuenta: 0, inicio: ahora };
  if (ahora - registro.inicio > 60000) {
    registro.cuenta = 1;
    registro.inicio = ahora;
  } else {
    registro.cuenta += 1;
  }
  ventanaRateLimit.set(ip, registro);
  return registro.cuenta <= 30;
}

const MIME_TYPES = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "application/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".png": "image/png",
  ".svg": "image/svg+xml",
  ".ico": "image/x-icon",
};

function responderJSON(res, status, payload) {
  res.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Methods": "GET, POST, DELETE, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type",
    "Cache-Control": "no-store",
  });
  res.end(JSON.stringify(payload));
}

function leerBody(req, maxBytes = 65536) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let total = 0;
    req.on("data", (chunk) => {
      total += chunk.length;
      if (total > maxBytes) {
        reject(new Error("Payload demasiado grande."));
        req.destroy();
        return;
      }
      chunks.push(chunk);
    });
    req.on("end", () => resolve(Buffer.concat(chunks).toString("utf8")));
    req.on("error", reject);
  });
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://${req.headers.host || "localhost"}`);

  if (req.method === "OPTIONS") {
    return responderJSON(res, 200, { ok: true });
  }

  // ---------- Endpoint de API: /api/mediciones ----------
  if (url.pathname === "/api/mediciones") {
    if (req.method === "GET") {
      const filas = leerMediciones();
      return responderJSON(res, 200, {
        ok: true,
        modo: "backend-propio",
        total: filas.length,
        filas,
      });
    }

    if (req.method === "POST") {
      const ip = req.socket.remoteAddress || "local";
      if (!verificarRateLimit(ip)) {
        return responderJSON(res, 429, { ok: false, error: "Demasiadas solicitudes. Intentá en un minuto." });
      }
      try {
        const rawBody = await leerBody(req);
        const parsed = JSON.parse(rawBody);
        const accion = url.searchParams.get("accion") || parsed.accion;

        // Soporte para agregar lote de simulación o limpiar simulación
        if (accion === "lote" && Array.isArray(parsed.filas)) {
          const actuales = leerMediciones();
          const nuevas = parsed.filas.map(sanitizarRegistro);
          const combinadas = actuales.concat(nuevas);
          guardarMediciones(combinadas);
          return responderJSON(res, 201, { ok: true, agregadas: nuevas.length, total: combinadas.length });
        }

        if (accion === "limpiar-simulacion") {
          const actuales = leerMediciones();
          const filtradas = actuales.filter((d) => !String(d.comentario || "").startsWith("DATO DE EJEMPLO"));
          guardarMediciones(filtradas);
          return responderJSON(res, 200, { ok: true, total: filtradas.length });
        }

        const fila = sanitizarRegistro(parsed);
        const actuales = leerMediciones();
        actuales.push(fila);
        guardarMediciones(actuales);
        return responderJSON(res, 201, { ok: true, fila, total: actuales.length });
      } catch (err) {
        return responderJSON(res, 400, { ok: false, error: err.message });
      }
    }
  }

  // ---------- Servidor de Archivos Estáticos ----------
  let relPath = decodeURIComponent(url.pathname);
  if (relPath === "/") relPath = "/estadisticas.html";

  const safePath = path.normalize(path.join(ROOT_DIR, relPath));
  if (!safePath.startsWith(ROOT_DIR)) {
    res.writeHead(403);
    return res.end("Acceso denegado");
  }

  if (fs.existsSync(safePath) && fs.statSync(safePath).isFile()) {
    const ext = path.extname(safePath).toLowerCase();
    res.writeHead(200, {
      "Content-Type": MIME_TYPES[ext] || "application/octet-stream",
      "Cache-Control": "no-cache",
    });
    return fs.createReadStream(safePath).pipe(res);
  }

  res.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" });
  res.end("404 · Recurso no encontrado");
});

server.listen(PORT, "0.0.0.0", () => {
  asegurarBaseDeDatos();
  console.log(`====================================================================`);
  console.log(`  UTN FRT · Comunicación — Servidor y API de Telemetría Activos`);
  console.log(`  Estadísticas : http://localhost:${PORT}/estadisticas.html`);
  console.log(`  Medidor      : http://localhost:${PORT}/index.html`);
  console.log(`  API REST     : http://localhost:${PORT}/api/mediciones`);
  console.log(`  Base de datos: ${DATA_FILE}`);
  console.log(`====================================================================`);
});
