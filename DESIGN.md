# Sistema de Diseño — Medidor de Señal UTN FRT (`DESIGN.md`)

<!-- impeccable:design-schema 1 -->

## 1. Identidad Visual y Mundo de Referencia

**Mundo:** Cuaderno de Telemetría e Instrumentación de Radiofrecuencia (`seed:c63de84a781842ff`).
Inspirado en manuales de referencia técnica de ingeniería, hojas de registro de espectro y tableros de conmutación de laboratorio. La interfaz prioriza la lectura inmediata de magnitudes físicas (`dBm`, `fW`, `Mbps`, `ms`, `dB`, `MHz`) mediante una retícula estructural continua, contraste cromático calibrado y tipografía numérica tabular.

## 2. Paleta Cromática y Tokens

### Modo Claro (Papel Técnico de Alta Luminancia)
- `--bg` (`#F5F4EE`): Papel técnico cálido con micro-retícula de puntos (`#D5D2C6` cada `20px`).
- `--surface` (`#FDFCF9`): Superficie principal de los paneles de instrumentación.
- `--surface-alt` (`#ECEAE1`): Superficie secundaria para cabeceras de tabla, selectores de pliego y etiquetas de telemetría.
- `--surface-elevated` (`#FFFFFF`): Controles interactivos activos, campos de entrada y modo en vivo.
- `--fg` (`#0F172A`): Tinta principal azul pizarra profunda (contraste > 14:1 sobre `--surface`).
- `--fg-secondary` (`#334155`): Tinta secundaria para etiquetas de formulario y metadatos.
- `--muted` (`#526074`): Texto auxiliar, unidades y ejes de gráficos (contraste ≥ 4.8:1).
- `--line` (`#D5D2C6`) y `--line-strong` (`#B6B2A2`): Filetes divisorios capilares de 1px.

### Modo Oscuro (Osciloscopio Nocturno)
- `--bg` (`#0A0F1A`), `--surface` (`#111827`), `--surface-alt` (`#182235`), `--surface-elevated` (`#1E293B`).
- `--fg` (`#F1F5F9`), `--fg-secondary` (`#CBD5E1`), `--muted` (`#94A3B8`).
- `--line` (`#24324B`), `--line-strong` (`#384B6B`).

### Colores Semánticos de Portadora y Estado
- `--accent` (`#1849C6` en claro / `#6096FF` en oscuro): Azul cobalto de portadora principal, acciones primarias y curva teórica de Shannon.
- `--good` (`#117A43` / `#34D399`): Enlace óptimo (20–100 Mbps) y confirmación de registro.
- `--amber` (`#B46900` / `#F59E0B`): Atenuación moderada (5–20 Mbps) y enlace celular móvil.
- `--bad` (`#B93829` / `#F87171`): Señal crítica (< 5 Mbps), errores y estado de captura en vivo.

## 3. Tipografía y Escala

- **Display y Cuerpo (`--font-display`, `--font-body`):** `"Chivo"`, sans-serif neo-grotesca de ingeniería con excelente legibilidad en paneles densos.
  - `h1`: `clamp(1.75rem, 2.8vw, 2.25rem)`, peso `800`, interlineado `1.18`, tracking `-0.025em`.
  - `h2`: `1.28rem`, peso `700`, interlineado `1.32`, tracking `-0.015em`.
  - Cuerpo: `1rem` (`16px`), interlineado `1.55`, medida máxima `72ch`.
- **Datos y Telemetría (`--font-mono`):** `"JetBrains Mono"`, monoespaciada con `font-variant-numeric: tabular-nums` obligatorio en KPIs, tablas, ejes, ecuaciones y velocímetro.
  - Lectura principal de velocímetro: `3.1rem`, peso `700`.
  - Indicadores KPI (`.metrica .v`): `1.65rem`, peso `700`.
  - Encabezados de columna (`th`): `0.72rem`, peso `700`, mayúsculas con tracking `0.05em`.

## 4. Componentes y Reglas de Composición

1. **Cabecera de Instrumento (`header.top`):** Integra el sello institucional `UTN FRT · Comunicación`, el título de la vista, el selector de pliego (`Medidor` / `Estadísticas`) con transición en pasos (`90ms steps(2)`) y el conmutador de tema (`Claro` / `Oscuro`).
2. **Banda Continua de Indicadores (`.kpis`):** Grilla de 5 celdas unidas por filetes de `1px` (`gap: 1px` sobre fondo `--line`), evitando tarjetas sueltas repetitivas.
3. **Paneles sin anidamiento (`.card`):** Contenedores de primer nivel con `border: 1px solid var(--line)`, radio de `10px` y sombra desplazada suave (`--shadow-panel`). Dentro de un `.card`, las subdivisiones usan líneas divisorias (`border-top` / `border-bottom`) y ritmo espacial en lugar de sub-tarjetas anidadas.
4. **Gráficos y Cartografía:**
   - En el gráfico de dispersión Señal vs. Bajada, el radio de los puntos escala según la magnitud de descarga y se superpone la curva punteada del límite teórico de Shannon-Hartley para el ancho de banda $B$ y temperatura $T$ seleccionados.
   - En el mapa Leaflet, los marcadores escalan su radio y color según el rango de caudal medido.
