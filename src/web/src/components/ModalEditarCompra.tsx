import dayjs, { type Dayjs } from 'dayjs'
import { DatePicker, Form, Input, Modal, Select } from 'antd'
import { useEditarCompra, useProveedores } from '../api/compras'
import type { CompraResponse } from '../api/tipos'
import { AvisoError } from './AvisoError'

type Props = {
  compra: CompraResponse | null
  onCerrar: () => void
}

type Campos = {
  proveedorId: string
  serie: string
  numero: string
  fechaEmision: Dayjs
  fechaVencimiento?: Dayjs | null
  guiaRemision?: string
  observaciones?: string
}

const sinVacios = (valor?: string) => (valor?.trim() ? valor.trim() : null)

/**
 * Corrige los datos del comprobante. Lo que ya movió stock y costo (tipo de
 * comprobante, líneas, moneda, tipo de cambio e IGV) no se edita.
 */
export function ModalEditarCompra({ compra, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const proveedores = useProveedores(compra !== null)
  const editar = useEditarCompra()
  const fechaEmision = Form.useWatch('fechaEmision', formulario)

  const cerrar = () => {
    editar.reset()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    if (!compra) return
    await editar.mutateAsync({
      id: compra.id,
      datos: {
        proveedorId: campos.proveedorId,
        serie: campos.serie.trim().toUpperCase(),
        numero: campos.numero.trim(),
        fechaEmision: campos.fechaEmision.format('YYYY-MM-DD'),
        fechaVencimiento: campos.fechaVencimiento?.format('YYYY-MM-DD') ?? null,
        guiaRemision: sinVacios(campos.guiaRemision),
        observaciones: sinVacios(campos.observaciones),
      },
    })
    cerrar()
  }

  // Si el proveedor se dio de baja, sigue en la lista para no perderlo al guardar.
  const opcionesProveedor = (proveedores.data ?? []).map((proveedor) => ({
    value: proveedor.id,
    label: `${proveedor.razonSocial} · ${proveedor.numeroDocumento}`,
  }))
  if (compra && !opcionesProveedor.some((opcion) => opcion.value === compra.proveedorId)) {
    opcionesProveedor.push({ value: compra.proveedorId, label: `${compra.proveedorNombre} · ${compra.proveedorDocumento}` })
  }

  return (
    <Modal
      title={compra ? `Editar ${compra.numeroCompra}` : 'Editar compra'}
      open={compra !== null}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Guardar"
      cancelText="Cancelar"
      confirmLoading={editar.isPending}
      destroyOnHidden
    >
      <AvisoError error={editar.error ?? proveedores.error} />
      <p className="texto-secundario" style={{ marginBottom: 16 }}>
        El tipo de comprobante, los repuestos, la moneda y el IGV no se editan porque ya movieron stock y costo. Si
        están mal, anula la compra y regístrala de nuevo.
      </p>
      {compra && (
        <Form<Campos>
          form={formulario}
          layout="vertical"
          requiredMark={false}
          onFinish={enviar}
          initialValues={{
            proveedorId: compra.proveedorId,
            serie: compra.serie,
            numero: compra.numero,
            fechaEmision: dayjs(compra.fechaEmision),
            fechaVencimiento: compra.fechaVencimiento ? dayjs(compra.fechaVencimiento) : null,
            guiaRemision: compra.guiaRemision ?? '',
            observaciones: compra.observaciones ?? '',
          }}
        >
          <Form.Item label="Proveedor" name="proveedorId" rules={[{ required: true, message: 'Elige el proveedor' }]}>
            <Select showSearch optionFilterProp="label" loading={proveedores.isPending} options={opcionesProveedor} />
          </Form.Item>
          <div className="formulario-grid">
            <Form.Item
              label="Serie"
              name="serie"
              rules={[
                { required: true, whitespace: true, message: 'Ingresa la serie' },
                { pattern: /^[A-Za-z0-9]{1,10}$/, message: 'Hasta 10 letras o números' },
              ]}
            >
              <Input maxLength={10} style={{ textTransform: 'uppercase' }} />
            </Form.Item>
            <Form.Item
              label="Número"
              name="numero"
              rules={[
                { required: true, whitespace: true, message: 'Ingresa el número' },
                { pattern: /^[A-Za-z0-9-]{1,20}$/, message: 'Letras, números o guiones' },
              ]}
            >
              <Input maxLength={20} />
            </Form.Item>
            <Form.Item label="Emisión" name="fechaEmision" rules={[{ required: true, message: 'Indica la fecha' }]}>
              <DatePicker format="DD/MM/YYYY" disabledDate={(dia) => dia.isAfter(dayjs(), 'day')} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item
              label="Vencimiento"
              name="fechaVencimiento"
              dependencies={['fechaEmision']}
              rules={[
                ({ getFieldValue }) => ({
                  validator: async (_, vencimiento: Dayjs | null | undefined) => {
                    const emision: Dayjs | undefined = getFieldValue('fechaEmision')
                    if (vencimiento && emision && vencimiento.isBefore(emision, 'day')) {
                      throw new Error('No puede ser anterior a la emisión')
                    }
                  },
                }),
              ]}
            >
              <DatePicker
                format="DD/MM/YYYY"
                disabledDate={(dia) => (fechaEmision ? dia.isBefore(fechaEmision, 'day') : false)}
                style={{ width: '100%' }}
              />
            </Form.Item>
            <Form.Item label="Guía de remisión" name="guiaRemision">
              <Input maxLength={50} />
            </Form.Item>
          </div>
          <Form.Item label="Observaciones" name="observaciones">
            <Input.TextArea rows={2} maxLength={500} />
          </Form.Item>
        </Form>
      )}
    </Modal>
  )
}
