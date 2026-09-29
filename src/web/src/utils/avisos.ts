import { App as AppAntd, message } from 'antd'

type Mensajes = ReturnType<typeof AppAntd.useApp>['message']

/**
 * Avisos breves en la esquina superior. La instancia la registra
 * <PuenteAvisos /> desde el App de Ant Design, para que tomen el tema; si
 * todavía no está, se usa la función estática.
 */
let instancia: Mensajes | null = null

export function registrarAvisos(nueva: Mensajes | null) {
  instancia = nueva
}

export const avisos = {
  exito: (texto: string) => {
    void (instancia ?? message).success(texto)
  },
  error: (texto: string) => {
    void (instancia ?? message).error(texto)
  },
}
