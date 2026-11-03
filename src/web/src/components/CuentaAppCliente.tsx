import { Button, Modal } from 'antd'
import { useCuentaAppCliente, useGenerarCodigoActivacion } from '../api/clientes'
import type { CodigoActivacionResponse } from '../api/tipos'
import { fechaHora } from '../utils/formato'
import { AvisoError } from './AvisoError'

type Props = {
  clienteId: string
  /** `clientes.editar`: Recepción y Gerencia generan el código. */
  puedeGenerar: boolean
}

/**
 * Si el cliente ya entra a la app y, si no, el código con el que la activa: lo escribe
 * en la app junto con su DNI. El código se muestra una sola vez.
 */
export function CuentaAppCliente({ clienteId, puedeGenerar }: Readonly<Props>) {
  const cuenta = useCuentaAppCliente(clienteId)
  const generar = useGenerarCodigoActivacion()
  const codigo: CodigoActivacionResponse | undefined = generar.data

  const estado = (() => {
    if (!cuenta.data) return '—'
    if (cuenta.data.tieneCuenta) return cuenta.data.email ? `Activa · ${cuenta.data.email}` : 'Activa · entra con su DNI'
    if (cuenta.data.codigoVigenteHasta) return `Sin activar · código vigente hasta el ${fechaHora(cuenta.data.codigoVigenteHasta)}`
    return 'Sin cuenta'
  })()

  return (
    <>
      <div className="seccion-titulo">
        <h2>Cuenta en la app</h2>
      </div>
      <AvisoError error={cuenta.error ?? (codigo ? null : generar.error)} />
      <p>{estado}</p>
      {puedeGenerar && cuenta.data && !cuenta.data.tieneCuenta && (
        <Button loading={generar.isPending} onClick={() => generar.mutate(clienteId)}>
          {cuenta.data.codigoVigenteHasta ? 'Generar otro código' : 'Generar código de activación'}
        </Button>
      )}
      <Modal
        open={Boolean(codigo)}
        title="Código de activación"
        okText="Listo"
        cancelButtonProps={{ style: { display: 'none' } }}
        onOk={() => generar.reset()}
        onCancel={() => generar.reset()}
      >
        {codigo && (
          <>
            <p className="codigo-activacion">{`${codigo.codigo.slice(0, 3)} ${codigo.codigo.slice(3)}`}</p>
            <p>
              Díselo al cliente: en la app toca «Activar mi cuenta» y escribe su DNI, este código y la contraseña que
              quiera. Sirve una sola vez, hasta el {fechaHora(codigo.vence)}.
            </p>
            <p className="texto-secundario">No se vuelve a mostrar: si lo pierde, genera otro.</p>
          </>
        )}
      </Modal>
    </>
  )
}
