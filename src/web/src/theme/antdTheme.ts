import type { ThemeConfig } from 'antd'
import { colores, fuentes } from './tokens'

export const temaAntd: ThemeConfig = {
  token: {
    colorPrimary: colores.acento600,
    colorLink: colores.acento700,
    colorText: colores.texto,
    colorTextSecondary: colores.textoSecundario,
    // antd deriva estos grises al 45 % y al 25 %, que no llegan a 4,5:1 sobre el fondo:
    // ayudas de formulario, placeholders y textos terciarios usan los tonos accesibles.
    colorTextTertiary: colores.textoSecundario,
    colorTextDescription: colores.textoSecundario,
    colorTextPlaceholder: colores.textoPlaceholder,
    // El rojo de peligro de antd (#ff4d4f) da 2,9:1 sobre el fondo; acento700 da 6,4:1.
    colorError: colores.acento700,
    colorBorder: colores.divisor,
    colorBorderSecondary: colores.divisorSuave,
    colorBgLayout: colores.fondo,
    colorBgContainer: colores.blanco,
    colorFillAlter: colores.superficie,
    fontFamily: fuentes.texto,
    fontSize: 15,
    borderRadius: 0,
    borderRadiusLG: 0,
    borderRadiusSM: 0,
    borderRadiusXS: 0,
    controlHeight: 38,
  },
  components: {
    Button: {
      fontFamily: fuentes.titulos,
      fontWeight: 700,
      primaryShadow: 'none',
      defaultShadow: 'none',
      dangerShadow: 'none',
    },
    Table: {
      headerBg: 'transparent',
      headerColor: colores.textoSecundario,
      headerSplitColor: 'transparent',
      borderColor: colores.divisorSuave,
      rowHoverBg: colores.acento100,
      // El texto de tabla vacía usa el gris de deshabilitado (1,8:1); aquí es contenido que se lee.
      colorTextDisabled: colores.textoSecundario,
    },
    Segmented: {
      itemSelectedBg: colores.acento600,
      itemSelectedColor: colores.blanco,
      trackBg: colores.superficie,
    },
    Tabs: {
      inkBarColor: colores.acento600,
      itemSelectedColor: colores.texto,
      itemHoverColor: colores.acento700,
    },
  },
}
