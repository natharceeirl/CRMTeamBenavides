// Única fuente de colores y tipografía de la web.
// Valores tomados del diseño de Claude Design (sistema Modernist, variante con barra lateral negra).

export const colores = {
  fondo: '#f3f2f2',
  superficie: '#eae9e9',
  blanco: '#ffffff',
  texto: '#201e1d',
  textoSecundario: 'rgba(32, 30, 29, 0.68)',
  divisor: 'rgba(32, 30, 29, 0.4)',
  divisorSuave: 'rgba(32, 30, 29, 0.16)',
  acento: '#ec3013',
  acento100: '#fff2ef',
  acento200: '#ffe0d9',
  acento600: '#dd2b0f',
  acento700: '#ae1800',
  acento800: '#7c1405',
  neutro100: '#f8f4f4',
  neutro300: '#d7d3d3',
  neutro800: '#444141',
  marcaRojo: '#ff0000',
  marcaNegro: '#000000',
} as const

export const fuentes = {
  titulos: '"Space Grotesk", system-ui, sans-serif',
  texto: '"IBM Plex Sans", system-ui, sans-serif',
} as const

// Expone los tokens como variables CSS para que global.css no repita valores.
export function aplicarVariablesCss(raiz: HTMLElement = document.documentElement) {
  const variables: Record<string, string> = {
    '--color-fondo': colores.fondo,
    '--color-superficie': colores.superficie,
    '--color-blanco': colores.blanco,
    '--color-texto': colores.texto,
    '--color-texto-secundario': colores.textoSecundario,
    '--color-divisor': colores.divisor,
    '--color-divisor-suave': colores.divisorSuave,
    '--color-acento': colores.acento,
    '--color-acento-100': colores.acento100,
    '--color-acento-200': colores.acento200,
    '--color-acento-600': colores.acento600,
    '--color-acento-700': colores.acento700,
    '--color-acento-800': colores.acento800,
    '--color-neutro-100': colores.neutro100,
    '--color-neutro-300': colores.neutro300,
    '--color-neutro-800': colores.neutro800,
    '--fuente-titulos': fuentes.titulos,
    '--fuente-texto': fuentes.texto,
  }
  for (const [nombre, valor] of Object.entries(variables)) {
    raiz.style.setProperty(nombre, valor)
  }
}
