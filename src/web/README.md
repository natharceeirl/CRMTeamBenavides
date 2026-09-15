# Web administrativa · Team Benavides

Wireframes navegables de la web administrativa, con **datos de ejemplo** (tarea del lunes 14/09). Todavía no se conectan a la API.

## Requisitos

- Node 24 y npm 11

## Comandos

```bash
npm install
npm run dev      # abre http://localhost:5173
npm run build    # compila TypeScript y genera dist/
```

## Estructura

| Carpeta o archivo | Contenido |
|---|---|
| `src/theme/tokens.ts` | Colores y tipografía: la única fuente de valores visuales |
| `src/theme/antdTheme.ts` | Tema de Ant Design construido con los tokens |
| `src/styles/global.css` | Estilos generales; usa las variables CSS que publica `tokens.ts` |
| `src/layout/AppLayout.tsx` | Barra lateral negra y contenedor de páginas |
| `src/components` | Logo, barra superior, indicadores, etiqueta de estado y widget del chatbot |
| `src/pages` | Una pantalla por ruta |
| `src/data/ejemplo.ts` | Datos de ejemplo; no son datos reales del cliente |

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
| `/reportes` | Reportes (versión temprana, sin gráficos) |
| `/usuarios` | Usuarios y matriz de roles y permisos |

El widget del chatbot aparece abajo a la derecha en todas las pantallas internas.

## Diseño

Colores y tipografía del proyecto de Claude Design (variante con barra lateral negra): Space Grotesk para títulos, IBM Plex Sans para texto y acento rojo `#ec3013`. Los estados de orden son la propuesta del mapa funcional y siguen pendientes de validar con el cliente.
