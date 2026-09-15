import type { ThemeConfig } from 'antd'
import { colores, fuentes } from './tokens'

export const temaAntd: ThemeConfig = {
  token: {
    colorPrimary: colores.acento600,
    colorLink: colores.acento700,
    colorText: colores.texto,
    colorTextSecondary: colores.textoSecundario,
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
