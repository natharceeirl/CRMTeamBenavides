import { useState } from 'react'
import { Button, Form, Select } from 'antd'
import { useClientes } from '../api/clientes'
import type { ClienteResponse } from '../api/tipos'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { ModalCliente } from './ModalCliente'

type Props = {
  etiqueta?: string
  mensaje?: string
  /** Con el cliente nuevo ya elegido: la cita, por ejemplo, limpia la unidad. */
  alRegistrar?: (cliente: ClienteResponse) => void
}

/**
 * El campo «clienteId» de un formulario. Quien tiene clientes.crear ve «Registrar cliente
 * nuevo»: el cliente se registra ahí mismo, sin salir del formulario, y queda elegido.
 */
export function CampoCliente({ etiqueta = 'Cliente', mensaje = 'Elige el cliente', alRegistrar }: Readonly<Props>) {
  const formulario = Form.useFormInstance()
  const clientes = useClientes()
  const { tienePermiso } = useSesion()
  const puedeRegistrar = tienePermiso(PERMISOS.clientesCrear)
  const [registrando, setRegistrando] = useState(false)

  return (
    <>
      <Form.Item
        label={etiqueta}
        name="clienteId"
        rules={[{ required: true, message: mensaje }]}
        extra={
          puedeRegistrar && (
            <Button type="link" size="small" style={{ paddingInline: 0 }} onClick={() => setRegistrando(true)}>
              Registrar cliente nuevo
            </Button>
          )
        }
      >
        <Select
          showSearch={{ optionFilterProp: 'label' }}
          loading={clientes.isPending}
          placeholder="Buscar cliente"
          options={(clientes.data ?? []).map((cliente) => ({ value: cliente.id, label: cliente.nombreCompleto }))}
        />
      </Form.Item>
      <ModalCliente
        abierto={registrando}
        onCerrar={() => setRegistrando(false)}
        onGuardado={(cliente) => {
          formulario.setFieldValue('clienteId', cliente.id)
          alRegistrar?.(cliente)
        }}
      />
    </>
  )
}
