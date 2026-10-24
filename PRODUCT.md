# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

static HTML/CSS/JS (Vanilla, zero-build, GitHub Pages ready)

## Users

Estudiantes y docentes de Ingeniería de la **UTN FRT (Universidad Tecnológica Nacional — Facultad Regional Tucumán)** en la cátedra de **Comunicación / Comunicación de Datos**.
- **En clase / presentación en vivo:** El docente proyecta `estadisticas.html#vivo` con un código QR en pantalla; los estudiantes escanean desde sus celulares (WiFi del campus o red móvil 4G/5G de Personal, Claro, Movistar) y envían mediciones en tiempo real mientras la cátedra analiza la distribución estadística y el cumplimiento de las leyes teóricas.
- **Trabajo de campo / práctico de laboratorio:** Los alumnos recolectan muestras geolocalizadas (interior, exterior, en movimiento) desde web o app Android/PC para contrastar mediciones empíricas contra el límite teórico de Shannon y los topes por generación celular.

## Product Purpose

Medir empíricamente el rendimiento de redes inalámbricas (WiFi y Datos Móviles 2G/3G/4G/5G) —velocidad de bajada, subida, latencia, jitter, intensidad de señal en dBm (RSSI/RSRP) y SINR— y contrastar los datos recolectados colaborativamente con los modelos matemáticos de la cátedra:
- **Unidad 3:** Potencia en dBm a mW/µW/fW, ruido térmico de Johnson-Nyquist ($N = k \cdot T \cdot B$), conversión de RSRP por subportadora LTE (15 kHz) a potencia total del canal ($12 \times 5 \times B$ subportadoras) y relación señal a ruido ($\text{SNR}$).
- **Unidad 5:** Capacidad del canal de Claude Shannon ($C = B \cdot \log_2(1 + S/N)$) y porcentaje de aprovechamiento real del espectro.
- **Unidad 7:** Velocidades máximas teóricas por generación de red móvil (2G EDGE, 3G HSPA+, 4G LTE, 5G NR).

## Positioning

A diferencia de un test de velocidad comercial genérico (Speedtest, Fast.com), **Medidor de Señal UTN FRT** vincula directamente cada prueba de red con la física de telecomunicaciones: traduce dBm a femtowatts, calcula el piso de ruido térmico según el ancho de banda $B$ (MHz) y la temperatura $T$ (°C), corrige RSRP a potencia de portadora completa y evalúa el rendimiento real frente al límite de Shannon-Hartley en tiempo real y de forma colaborativa en el aula.

## Operating Context

- **Proyección en aula (Modo en Vivo):** Debe leerse con claridad absoluta desde el proyector del aula y en monitores de laboratorio, mostrando el QR, el contador tabular de muestras entrantes y el feed en tiempo real.
- **Uso móvil en campo:** `index.html` se opera desde teléfonos en interiores o exteriores bajo luz solar o en aula, requiriendo controles táctiles claros, lectura inmediata del velocímetro logarítmico (0–1000 Mbps) y detección automática de dispositivo (Client Hints, Network Information API, IP ASN/ISP via `ipwho.is`).
- **Independencia de backend:** Funciona de manera autónoma en modo local/demo con generación de datasets realistas de simulación (San Miguel de Tucumán) o conectado a un Google Apps Script propio configurable en `config.js`, desconectado de la planilla original de la colega.

## Capabilities and Constraints

- **Arquitectura sin compilación:** HTML5, CSS3 y JavaScript Vanilla (`config.js`, `teoria.js`, `index.html`, `estadisticas.html`, `estilos.css`), listo para alojar en GitHub Pages o abrir localmente.
- **Dependencias ligeras por CDN:** Chart.js 4.4.1 (gráficos estadísticos y dispersión), Leaflet 1.9.4 + OpenStreetMap (mapa georreferenciado por rangos de Mbps) y QRCode.js 1.0.0.
- **Exportación compatible con Excel en español:** Descarga CSV con BOM UTF-8, separador `;` y coma decimal `,`.
- **Privacidad por diseño:** Consentimiento explícito, sin almacenar números telefónicos ni IPs crudas, con geolocalización aproximada o GPS voluntario.

## Brand Commitments

- **Identidad institucional:** Mantener el sello académico `UTN FRT · Comunicación` (`Universidad Tecnológica Nacional · Facultad Regional Tucumán`).
- **Idioma y tono:** Español rioplatense/argentino claro, riguroso en la terminología física y estadística pero cercano para el estudiante ("Escaneá el código", "Bajá la app", "Desvío estándar muestral").

## Evidence on Hand

- Código fuente e ingeniería inversa completa obtenida mediante **REA** (`ev_a3d31da671906568d5bc92827179fc5a38051a368204326a6c63941324ef76b3`, `ev_923b294f194066ced3ac5e3e039cfe61bb4137d7191bafd80e275b8ce1acfe3a`, `ev_fb8ebee5f81dbcc774baf23c11ff3b0c17cd04bb57067fdab4473460072ecdf4`).
- Fórmulas de cátedra verificadas en `teoria.js` y muestra real de 9 mediciones de referencia en San Miguel de Tucumán capturadas como semilla opcional de demostración.

## Product Principles

1. **Rigor físico visible:** Las ecuaciones de ruido térmico, RSRP y capacidad de Shannon no son notas al pie; son instrumentos interactivos donde cambiar $B$ o $T$ recalcula instantáneamente toda la telemetría.
2. **Claridad instrumental:** Números tabulares (`tabular-nums`), unidades explícitas (`dBm`, `fW`, `Mbps`, `ms`, `dB`) y jerarquía tipográfica que separa el dato medido del límite teórico.
3. **Autonomía e inmediatez:** Cero fricción de despliegue; funciona al instante con datos locales, simulación estadística de 60 muestras o sincronización en la nube propia.
4. **Resiliencia en vivo:** El modo presentación y la ingesta de datos resisten datos incompletos, fechas malformadas de hojas de cálculo o desconexiones de red sin romper la interfaz.
