# Sistema de Diseño — Medidor de Señal UTN FRT (`DESIGN.md`)

<!-- impeccable:design-schema 1 -->

## 1. Identidad Visual y Mundo de Referencia

**Mundo:** Cuaderno de Telemetría e Instrumentación de Radiofrecuencia (`seed:c63de84a781842ff`).
Inspirado en manuales de referencia técnica de ingeniería en telecomunicaciones, analizadores de espectro (Keysight / Rohde & Schwarz) y tableros de laboratorio. La interfaz prioriza la lectura inmediata y científicamente rigurosa de magnitudes físicas (`dBm`, `fW`, `Mbps`, `ms`, `dB`, `MHz`) mediante una retícula continua, contraste cromático estricto (WCAG AA/AAA) y tipografía numérica tabular.

## 2. Paleta Cromática y Tokens

### Modo Claro (Papel Técnico de Alta Luminancia)
- `--bg` (`#F5F4EE`): Papel técnico cálido con micro-retícula de puntos (`#D5D2C6` cada `20px`).
- `--surface` (`#FDFCF9`): Superficie principal de los paneles de instrumentación.
- `--surface-alt` (`#ECEAE1`): Superficie secundaria para cabeceras de tabla, selectores de pliego y etiquetas de telemetría.
- `--surface-elevated` (`#FFFFFF`): Controles interactivos activos, campos de entrada y modo en vivo.
- `--fg` (`#0F172A`): Tinta principal azul pizarra profunda (contraste > 14:1 sobre `--surface`).
- `--fg-secondary` (`#334155`): Tinta secundaria para etiquetas de formulario y metadatos.
- `--muted` (`#526074`): Texto auxiliar, unidades y ejes de gráficos (contraste ≥ 5.1:1).
- `--line` (`#D5D2C6`): Filetes divisorios capilares de 1px.
- `--line-strong` (`#989484`): Trazo estructural de arco del velocímetro y bordes reforzados (contraste no-textual ≥ 3.05:1 según WCAG 2.1 SC 1.4.11).

### Modo Oscuro (Osciloscopio Nocturno)
- `--bg` (`#0A0F1A`), `--surface` (`#111827`), `--surface-alt` (`#182235`), `--surface-elevated` (`#1E293B`).
- `--fg` (`#F1F5F9`), `--fg-secondary` (`#CBD5E1`), `--muted` (`#94A3B8`).
- `--line` (`#24324B`), `--line-strong` (`#384B6B`).

### Colores Semánticos de Portadora y Estado
- `--accent` (`#1849C6` en claro / `#6096FF` en oscuro): Azul cobalto de portadora principal, acciones primarias y curva teórica de Shannon.
- `--good` / `--green` (`#117A43` en claro / `#34D399` en oscuro): Enlace óptimo (> 20 Mbps), caudal real de bajada y confirmación de registro.
- `--amber` (`#945300` en claro / `#F59E0B` en oscuro): Atenuación moderada (5–20 Mbps), enlace celular y subida real. Calibrado para contraste ≥ 5.1:1 sobre `--amber-soft` (`#FEF3C7`).
- `--bad` (`#B93829` / `#F87171`): Señal crítica (< 5 Mbps), errores y testigo de captura en vivo.

## 3. Tipografía y Escala

- **Display y Cuerpo (`--font-display`, `--font-body`):** `"Chivo"`, sans-serif neo-grotesca de ingeniería con alta legibilidad en paneles densos.
  - `h1`: `clamp(1.75rem, 2.8vw, 2.25rem)`, peso `800`, interlineado `1.18`, tracking `-0.025em`.
  - `h2`: `1.28rem`, peso `700`, interlineado `1.32`, tracking `-0.015em`.
  - Cuerpo: `1rem` (`16px`), interlineado `1.55`, medida máxima `72ch`.
- **Datos y Telemetría (`--font-mono`):** `"JetBrains Mono"`, monoespaciada con `font-variant-numeric: tabular-nums` obligatorio en KPIs, tablas, ejes, ecuaciones y velocímetro.
  - Lectura principal de velocímetro: `3.1rem`, peso `700`.
  - Indicadores KPI (`.metrica .v`): `1.65rem`, peso `700`.
  - Encabezados de columna (`th`): `0.76rem` (12.16px), peso `700`, mayúsculas con tracking `0.05em`.
  - Etiquetas funcionales mínimas: piso de `0.76rem` (≥ 12px) cumpliendo estándares de legibilidad para interfaces de instrumentación.

## 4. Componentes y Reglas de Composición

1. **Cabecera de Instrumento (`header.top`):** Integra el sello institucional `UTN FRT · Comunicación`, el título de la vista, el selector de pliego (`Medidor` / `Estadísticas`) con transición en pasos (`90ms steps(2)`) y el conmutador de tema (`Claro` / `Oscuro`).
2. **Velocímetro Reactivo de Alto Rendimiento:** Trazo y ticks generados estáticamente en DOM vectorial, con actualización de aguja y arco desacoplada en `requestAnimationFrame` sin layout thrashing ni re-parseo de SVG.
3. **Diagnóstico RF Inmediato (`#diagnosticoRF`):** En `index.html`, traduce de inmediato las mediciones en magnitudes físicas de telecomunicaciones: potencia en dBm y femtowatts, SNR sobre piso térmico Johnson-Nyquist y porcentaje de aprovechamiento del límite de Claude Shannon ($C = B \log_2(1 + S/N)$).
4. **Revelación Progresiva en Registro:** Agrupa parámetros avanzados de hardware y red autodetectados en `<details class="meta-tecnica">`, reduciendo la carga cognitiva móvil de 12 campos a 3 esenciales (Medio, Entorno y Consentimiento).
5. **Consola de Aula Magna en Modo en Vivo (`#panelVivo`):** En pantallas anchas (`≥ 960px`), dispone un layout en pantalla dividida con el código QR ampliado (260px) y el contador gigante fijo a la izquierda, y el feed en vivo con distribución en tiempo real a la derecha, evitando la pérdida visual del QR durante la clase.
6. **Dispersión Colectiva de Shannon:** Gráfico cartesiano de dispersión $(x=\text{dBm}, y=\text{Mbps})$ que proyecta a todos los estudiantes de la clase frente a la curva teórica continua de Shannon-Hartley calculada a partir de los parámetros activos de $B$ y $T$.
7. **Accesibilidad e Interacción de Teclado:** Fila de tabla seleccionable mediante teclado (`Enter` / `Espacio`), contenedores horizontales con `tabindex="0" role="region"`, `role="meter"` en velocímetro, `aria-live="polite"` en feeds y estados, y áreas táctiles mínimas de 44px en dispositivos táctiles (`@media (pointer: coarse)`).
