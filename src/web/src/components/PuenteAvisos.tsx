import { useEffect } from 'react'
import { App as AppAntd } from 'antd'
import { registrarAvisos } from '../utils/avisos'

/** Deja los avisos de Ant Design, con el tema aplicado, a mano de la capa de API. */
export function PuenteAvisos() {
  const { message } = AppAntd.useApp()

  useEffect(() => {
    registrarAvisos(message)
    return () => registrarAvisos(null)
  }, [message])

  return null
}
