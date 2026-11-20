// ==========================================================================
// Servidor Local + Lectura de Señal WiFi (UTN FRT · Comunicación)
// Sin dependencias externas (Node.js puro) · Persistencia en Google Sheets
// ==========================================================================

"use strict";

const http = require("http");
const fs = require("fs");
const path = require("path");
const { execSync } = require("child_process");

const PORT = Number(process.env.PORT) || 3000;
const ROOT_DIR = __dirname;

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
    "Access-Control-Allow-Methods": "GET, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type",
    "Cache-Control": "no-store",
  });
  res.end(JSON.stringify(payload));
}

const server = http.createServer((req, res) => {
  const url = new URL(req.url, `http://${req.headers.host || "localhost"}`);

  if (req.method === "OPTIONS") {
    return responderJSON(res, 200, { ok: true });
  }

  // ---------- Endpoint de API: /api/senal (lectura real de netsh wlan en Windows) ----------
  if (url.pathname === "/api/senal" && req.method === "GET") {
    try {
      const out = execSync("netsh wlan show interfaces", { encoding: "utf8", timeout: 2500 });
      const info = {};
      for (const line of out.split(/\r?\n/)) {
        const idx = line.indexOf(":");
        if (idx <= 0) continue;
        const k = line.slice(0, idx).trim().normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase();
        const v = line.slice(idx + 1).trim();
        if (k === "estado" || k === "state") info.estado = v.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase();
        else if (k === "ssid") info.ssid = v;
        else if (k === "banda" || k === "band") info.banda = v;
        else if (k === "canal" || k === "channel") info.canal = v;
        else if (k === "tipo de radio" || k === "radio type") info.radio = v;
        else if (k === "senal" || k === "signal") info.senal_pct = parseInt(v.replace("%", ""), 10);
        else if (k === "rssi") info.rssi = parseInt(v, 10);
      }
      if (info.estado && (info.estado.startsWith("conectado") || info.estado.startsWith("connected"))) {
        const dbm = Number.isFinite(info.rssi)
          ? info.rssi
          : Number.isFinite(info.senal_pct)
          ? Math.round(info.senal_pct / 2 - 100)
          : null;
        const barras = dbm == null ? null : dbm >= -55 ? 5 : dbm >= -65 ? 4 : dbm >= -75 ? 3 : dbm >= -82 ? 2 : dbm >= -90 ? 1 : 0;
        return responderJSON(res, 200, {
          ok: true,
          fuente: "netsh-wlan",
          conexion: "WiFi",
          senal_dbm: dbm,
          barras,
          ssid: info.ssid || "",
          radio: info.radio || "",
          banda: info.banda || "",
          canal: info.canal || "",
        });
      }
      return responderJSON(res, 200, { ok: false, motivo: "wifi-desconectado" });
    } catch (e) {
      return responderJSON(res, 200, { ok: false, motivo: "no-disponible" });
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
  console.log(`====================================================================`);
  console.log(`  UTN FRT · Comunicación — Servidor Local Activo`);
  console.log(`  Estadísticas : http://localhost:${PORT}/estadisticas.html`);
  console.log(`  Medidor      : http://localhost:${PORT}/index.html`);
  console.log(`  Señal WiFi   : http://localhost:${PORT}/api/senal`);
  console.log(`====================================================================`);
});
