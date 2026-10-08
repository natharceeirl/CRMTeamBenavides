import { Form, Input, InputNumber, Modal } from 'antd'
import { useAbrirCaja, useCerrarCaja, useRegistrarMovimientoCaja } from '../api/caja'
import { TIPO_MOVIMIENTO_CAJA, type CajaChicaDetalleResponse } from '../api/tipos'
import { soles } from '../utils/formato'
import { AvisoError } from './AvisoError'

type Base = {
  abierto: boolean
  onCerrar: () => void
}

export function ModalAperturaCaja({ abierto, onCerrar }: Readonly<Base>) {
  const [formulario] = Form.useForm<{ montoApertura: number; observaciones?: string }>()
  const abrir = useAbrirCaja()

  const cerrar = () => {
    abrir.reset()
    onCerrar()
  }

  return (
    <Modal
      title="Abrir caja chica"
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Abrir caja"
      cancelText="Cancelar"
      confirmLoading={abrir.isPending}
      destroyOnHidden
    >
      <AvisoError error={abrir.error} />
      <Form
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={async (campos) => {
          await abrir.mutateAsync({ montoApertura: campos.montoApertura, observaciones: campos.observaciones?.trim() || null })
          cerrar()
        }}
      >
        <Form.Item
          label="Monto inicial (S/)"
          name="montoApertura"
          extra="El efectivo con el que empieza la caja."
          rules={[{ required: true, message: 'Escribe el monto inicial' }]}
        >
          <InputNumber min={0.01} precision={2} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item label="Observaciones" name="observaciones">
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
      </Form>
    </Modal>
  )
}

export function ModalCierreCaja({ abierto, onCerrar, caja }: Readonly<Base & { caja: CajaChicaDetalleResponse }>) {
  const [formulario] = Form.useForm<{ observaciones?: string }>()
  const cerrarCaja = useCerrarCaja()

  const cerrar = () => {
    cerrarCaja.reset()
    onCerrar()
  }

  return (
    <Modal
      title="Cerrar caja chica"
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Cerrar caja"
      okButtonProps={{ danger: true }}
      cancelText="Cancelar"
      confirmLoading={cerrarCaja.isPending}
      destroyOnHidden
    >
      <AvisoError error={cerrarCaja.error} />
      <table className="tabla-simple" style={{ marginBottom: 16 }}>
        <tbody>
          <tr>
            <td>Monto inicial</td>
            <td>{soles(caja.montoApertura)}</td>
          </tr>
          <tr>
            <td>Ingresos en efectivo</td>
            <td>{soles(caja.totalIngresosEfectivo ?? caja.totalIngresos)}</td>
          </tr>
          <tr>
            <td>Egresos</td>
            <td>{soles(caja.totalEgresos)}</td>
          </tr>
          <tr>
            <td>
              <strong>Efectivo al cerrar</strong>
            </td>
            <td>
              <strong>{soles(caja.saldoCalculado)}</strong>
            </td>
          </tr>
        </tbody>
      </table>
      {(caja.totalIngresosOtrosMetodos ?? 0) > 0 && (
        <p className="texto-secundario">
          Además se cobraron {soles(caja.totalIngresosOtrosMetodos ?? 0)} por Yape, Plin, tarjeta o transferencia: quedan
          registrados, pero no entran al cajón.
        </p>
      )}
      <p className="texto-secundario">Después de cerrarla no se registran más movimientos en esta caja.</p>
      <Form
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={async (campos) => {
          await cerrarCaja.mutateAsync({ observaciones: campos.observaciones?.trim() || null })
          cerrar()
        }}
      >
        <Form.Item label="Observaciones" name="observaciones">
          <Input.TextArea rows={2} maxLength={500} placeholder="Diferencias al contar el efectivo, entregas…" />
        </Form.Item>
      </Form>
    </Modal>
  )
}

type Movimiento = {
  monto: number
  concepto: string
  referencia?: string
}

export function ModalMovimientoCaja({
  abierto,
  onCerrar,
  tipo,
  saldo,
}: Readonly<Base & { tipo: number; saldo: number }>) {
  const [formulario] = Form.useForm<Movimiento>()
  const registrar = useRegistrarMovimientoCaja()
  const esEgreso = tipo === TIPO_MOVIMIENTO_CAJA.egreso

  const cerrar = () => {
    registrar.reset()
    onCerrar()
  }

  return (
    <Modal
      title={esEgreso ? 'Registrar egreso' : 'Registrar ingreso'}
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Registrar"
      cancelText="Cancelar"
      confirmLoading={registrar.isPending}
      destroyOnHidden
    >
      <AvisoError error={registrar.error} />
      {esEgreso && (
        <p className="texto-secundario" style={{ marginBottom: 16 }}>
          Saldo disponible: <strong>{soles(saldo)}</strong>
        </p>
      )}
      <Form<Movimiento>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={async (campos) => {
          await registrar.mutateAsync({
            tipo,
            monto: campos.monto,
            concepto: campos.concepto.trim(),
            referencia: campos.referencia?.trim() || null,
          })
          cerrar()
        }}
      >
        <Form.Item
          label="Monto (S/)"
          name="monto"
          rules={[
            { required: true, message: 'Escribe el monto' },
            {
              // La caja no puede entregar más efectivo del que tiene.
              validator: async (_, monto: number | undefined) => {
                if (esEgreso && monto != null && monto > saldo) {
                  throw new Error(`No puede pasar el saldo disponible (${soles(saldo)}).`)
                }
              },
            },
          ]}
        >
          <InputNumber min={0.01} precision={2} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item
          label={esEgreso ? 'Motivo del gasto' : 'Concepto'}
          name="concepto"
          rules={[
            {
              required: true,
              whitespace: true,
              message: esEgreso ? 'Todo egreso lleva su motivo' : 'Escribe el concepto',
            },
          ]}
        >
          <Input
            maxLength={200}
            placeholder={esEgreso ? 'Movilidad del taller, útiles de limpieza…' : 'Reposición de caja, vuelto…'}
          />
        </Form.Item>
        <Form.Item label="Referencia" name="referencia">
          <Input maxLength={100} placeholder="N.° de boleta o recibo (opcional)" />
        </Form.Item>
      </Form>
    </Modal>
  )
}
