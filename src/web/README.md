# Web administrativa · Team Benavides

Web administrativa, conectada a la API (`src/CRMTeamBenavides.Api`). Las reglas de negocio y los permisos viven en el backend; acá no se duplican.

## Requisitos

- Node 24 y npm 11
- .NET 10 SDK, para generar y verificar los tipos de la API

## Comandos

```bash
npm install
npm run dev              # abre http://localhost:5173
npm run build            # compila TypeScript y genera dist/
npm test                 # pruebas (vitest)
npm run lint             # oxlint, sin avisos permitidos
npm run tipos:generar    # vuelve a generar los tipos de la API
npm run verificar        # tipos de la API al día + TypeScript + lint + pruebas
```

## Tipos de la API

Los contratos de la API (`Features/**/*Contracts.cs`, los records terminados en `Request`, `Response` o `Dto`) se traducen a TypeScript con `tools/GeneradorTipos` y quedan en `src/api/generado/contratos.ts`. Ese archivo no se edita a mano; `src/api/tipos.ts` lo reexporta y suma solo los tipos propios de la web.

- **Si cambia un contrato en el back**, correr `npm run tipos:generar` y commitear el archivo generado junto con el cambio. Si queda desactualizado, `npm run verificar` falla y dice qué correr.
- **Si la web deja de compilar** después de generar, es que el contrato cambió: hay que ajustar la pantalla, no el archivo generado.
- El generador sigue cómo serializa la API: propiedades en camelCase; enums como número, salvo los marcados con `JsonStringEnumConverter`; `| null` donde C# admite null. En las peticiones, los campos que admiten null o tienen valor por defecto son opcionales, y las propiedades calculadas (solo `get`) no se mandan.

## Pruebas

- Las funciones (`*.test.ts`) corren en Node, sin navegador.
- Las pruebas de pantallas y componentes (`*.test.tsx`) empiezan con `// @vitest-environment jsdom` y usan las ayudas de `src/test/pantalla.tsx`:
  - `montar` pone los mismos proveedores que la app: antd, React Query, la sesión y el router;
  - `apiFalsa` reemplaza `fetch` por respuestas en memoria y deja ver lo que se mandó. Una ruta que no está definida responde 404, así que una llamada inesperada no pasa en silencio;
  - `usuarioConSesion` inicia una sesión con los roles y permisos que devuelve `/auth/me`.
- Se busca por lo que ve el usuario (rol, texto, etiqueta), no por clases de antd.

## Estructura

| Carpeta o archivo | Contenido |
|---|---|
| `src/theme/tokens.ts` | Colores y tipografía: la única fuente de valores visuales |
| `src/theme/antdTheme.ts` | Tema de Ant Design construido con los tokens |
| `src/styles/global.css` | Estilos generales; usa las variables CSS que publica `tokens.ts` |
| `src/layout/AppLayout.tsx` | Barra lateral negra y contenedor de páginas |
| `src/components` | Logo, barra superior, indicadores, gráficos, etiquetas de estado y widget del chatbot |
| `src/api/generado/contratos.ts` | Tipos de los contratos de la API, generados (no editar a mano) |
| `src/api/tipos.ts` | Reexporta los generados y suma los tipos propios de la web |
| `src/utils/series.ts` | Agrupar por día y elegir los cortes del eje de los gráficos |
| `src/pages` | Una pantalla por ruta |
| `src/test/pantalla.tsx` | Ayudas para probar pantallas: proveedores, API falsa y sesión |
| `src/data/ejemplo.ts` | Resto del wireframe; solo quedan los nueve estados propuestos del mapa funcional |

## Pantallas

| Ruta | Pantalla |
|---|---|
| `/login` | Inicio de sesión |
| `/` | Tablero |
| `/ordenes` | Órdenes de servicio |
| `/ordenes/nueva` | Nueva orden (recepción) |
| `/ordenes/OT-000482` | Detalle de orden |
| `/clientes` y `/clientes/c1` | Clientes y ficha de cliente |
| `/unidades` | Unidades de las cuatro líneas |
| `/repuestos` | Repuestos e inventario |
| `/ventas` | Ventas y cotizaciones |
| `/reportes` | Reportes: órdenes abiertas por día, vendido por día y faltantes de stock, con la tabla completa debajo de cada gráfico |
| `/usuarios` | Usuarios y matriz de roles y permisos |

El widget del chatbot aparece abajo a la derecha en todas las pantallas internas.

## Diseño

Colores y tipografía del proyecto de Claude Design (variante con barra lateral negra): Space Grotesk para títulos, IBM Plex Sans para texto y acento rojo `#ec3013`. Los estados de orden son la propuesta del mapa funcional y siguen pendientes de validar con el cliente.

## Gráficos

Dibujados a mano en HTML y SVG (`GraficoBarras` y `GraficoArea`), sin librería
de charts: son cuatro formas simples y una dependencia de ese tamaño no se
justifica en un bundle que ya pesa.

Reglas que siguen, y que conviene mantener si se agregan más:

- **Un solo tono por gráfico.** El largo de la barra ya dice cuánto, así que el
  color queda libre. En «Órdenes por estado» se usa para separar lo que sigue en
  el taller (acento) de lo que ya cerró (gris `neutro500`).
- **Los colores salen de `tokens.ts`**, nunca escritos en el componente. El gris
  de atenuación está validado contra el fondo: 3,25:1 de contraste y ΔE 12,1 en
  deuteranopía frente al acento.
- **El texto nunca lleva el color del dato**: ejes, valores y leyendas usan las
  fichas de texto. La identidad la da la marca de color al lado.
- **Nada se lee solo al pasar el cursor.** Cada gráfico tiene su tabla debajo o
  el valor escrito, y el de área se recorre con las flechas del teclado.
- Una sola serie no lleva leyenda: el título de la sección ya dice qué se mide.

La web no tiene modo oscuro, así que los gráficos tampoco: hay una sola
superficie (`#f3f2f2`) y contra ella están validados.
