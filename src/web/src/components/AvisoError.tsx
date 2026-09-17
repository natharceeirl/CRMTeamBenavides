import { Alert } from 'antd'

/** Muestra el error de una consulta o de una mutación, si lo hay. */
export function AvisoError({ error }: Readonly<{ error: unknown }>) {
  if (!error) {
    return null
  }

  const mensaje = error instanceof Error ? error.message : 'Ocurrió un error inesperado.'

  return <Alert type="error" message={mensaje} showIcon style={{ marginBottom: 16 }} />
}
