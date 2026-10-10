import { useState } from 'react'
import { Button, Form, Input, InputNumber, Modal, Radio, Select } from 'antd'
import { useProductos } from '../api/inventario'
import { useServicios } from '../api/servicios'
import { useAgregarDetalle } from '../api/ordenes'
import { AvisoError } from './AvisoError'
import { AvisoPrecioGerencia } from './AvisoPrecioGerencia'
import { ModalAltaRapidaRepuesto, ModalAltaRapidaServicio } from './ModalesAltaRapida'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { soles } from '../utils/formato'
import {
  TIPO_AFECTACION_IGV,
  TIPO_ITEM_SERVICIO,
} from '../api/tipos'

type Props = {
  abierto: boolean
  ordenId: string
  /**
   * Mano de obra y terceros no tienen precio de lista: solo los registra quien tiene
   * `precios.modificar`. El precio de un repuesto o servicio lo puede cambiar
   * cualquiera, con la aprobación de Gerencia.
   */
  puedeModificarPrecios?: boolean
  /** Falso para Gerencia y Recepción: su precio no espera aprobación. */
  pideAprobacion?: boolean
  onCerrar: () => void
}

type Campos = {
  tipoItem: number
  productoId?: string
  servicioId?: string
  descripcion?: string
  cantidad: number
  precioUnitario?: number
  tipoAfectacionIgv: number
}

export function ModalItemOrden({
  abierto,
  ordenId,
  puedeModificarPrecios = true,
  pideAprobacion = true,
  onCerrar,
}: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const productos = useProductos()
  const servicios = useServicios()
  const agregar = useAgregarDetalle()
  const { tienePermiso } = useSesion()
  const puedeRegistrarRepuesto = tienePermiso(PERMISOS.inventarioCrear)
  const puedeRegistrarServicio = tienePermiso(PERMISOS.serviciosCrear)
  const [altaRepuesto, setAltaRepuesto] = useState(false)
  const [altaServicio, setAltaServicio] = useState(false)

  const tipoItem = Form.useWatch('tipoItem', formulario) ?? TIPO_ITEM_SERVICIO.repuesto
  const productoId = Form.useWatch('productoId', formulario)
  const servicioId = Form.useWatch('servicioId', formulario)
  const precioUnitario = Form.useWatch('precioUnitario', formulario)

  const productoElegido = (productos.data ?? []).find((p) => p.id === productoId)
  const servicioElegido = (servicios.data ?? []).find((s) => s.id === servicioId)
  const precioDeLista =
    tipoItem === TIPO_ITEM_SERVICIO.repuesto
      ? productoElegido?.precioVenta
      : tipoItem === TIPO_ITEM_SERVICIO.servicio
        ? servicioElegido?.precioSugerido
        : undefined
  const conPrecioDeLista = tipoItem === TIPO_ITEM_SERVICIO.repuesto || tipoItem === TIPO_ITEM_SERVICIO.servicio

  const cerrar = () => {
    agregar.reset()
    formulario.resetFields()
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    await agregar.mutateAsync({
      id: ordenId,
      datos: {
        tipoItem: campos.tipoItem,
        productoId: campos.tipoItem === TIPO_ITEM_SERVICIO.repuesto ? campos.productoId : null,
        servicioId: campos.tipoItem === TIPO_ITEM_SERVICIO.servicio ? campos.servicioId : null,
        descripcion:
          campos.tipoItem === TIPO_ITEM_SERVICIO.repuesto
            ? (productoElegido?.nombre ?? campos.descripcion ?? null)
            : campos.tipoItem === TIPO_ITEM_SERVICIO.servicio
            ? (servicioElegido?.nombre ?? campos.descripcion ?? null)
            : (campos.descripcion ?? null),
        cantidad: campos.cantidad,
        precioUnitario: campos.precioUnitario ?? null,
        tipoAfectacionIgv: campos.tipoAfectacionIgv,
      },
    })

    cerrar()
  }

  return (
    <Modal
      title="Agregar ítem a la orden"
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Agregar"
      cancelText="Cancelar"
      confirmLoading={agregar.isPending}
      destroyOnHidden
    >
      <AvisoError error={agregar.error} />
      <Form<Campos>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={enviar}
        initialValues={{
          tipoItem: TIPO_ITEM_SERVICIO.repuesto,
          cantidad: 1,
          tipoAfectacionIgv: TIPO_AFECTACION_IGV.gravado,
        }}
      >
        <Form.Item label="Tipo de ítem" name="tipoItem">
          <Radio.Group
            buttonStyle="solid"
            onChange={(e) => {
              const val = e.target.value
              if (val === TIPO_ITEM_SERVICIO.servicio && servicioElegido) {
                formulario.setFieldsValue({
                  precioUnitario: servicioElegido.precioSugerido,
                  tipoAfectacionIgv: servicioElegido.tipoAfectacionIgv,
                })
              }
            }}
          >
            <Radio.Button value={TIPO_ITEM_SERVICIO.repuesto}>Repuesto</Radio.Button>
            <Radio.Button value={TIPO_ITEM_SERVICIO.servicio}>Servicio</Radio.Button>
            {puedeModificarPrecios && (
              <>
                <Radio.Button value={TIPO_ITEM_SERVICIO.manoDeObra}>Mano de obra</Radio.Button>
                <Radio.Button value={TIPO_ITEM_SERVICIO.terceros}>Terceros</Radio.Button>
              </>
            )}
          </Radio.Group>
        </Form.Item>

        {tipoItem === TIPO_ITEM_SERVICIO.repuesto && (
          <>
            <Form.Item
              label="Repuesto de inventario"
              name="productoId"
              rules={[{ required: true, message: 'Selecciona el repuesto' }]}
              extra={
                puedeRegistrarRepuesto && (
                  <Button type="link" size="small" style={{ paddingInline: 0 }} onClick={() => setAltaRepuesto(true)}>
                    ¿No está en el catálogo? Registrar repuesto
                  </Button>
                )
              }
            >
              <Select
                showSearch
                optionFilterProp="label"
                loading={productos.isPending}
                placeholder="Buscar por código o nombre"
                onChange={(pId) => {
                  const p = (productos.data ?? []).find((item) => item.id === pId)
                  if (p) {
                    formulario.setFieldsValue({ precioUnitario: p.precioVenta })
                  }
                }}
                options={(productos.data ?? []).map((producto) => ({
                  value: producto.id,
                  label: `${producto.codigo} · ${producto.nombre} · stock ${producto.stockActual}`,
                }))}
              />
            </Form.Item>
            {productoElegido && (
              <p className="texto-secundario" style={{ marginTop: -8, marginBottom: 12 }}>
                Precio catálogo: {soles(productoElegido.precioVenta)} · Stock disponible: {productoElegido.stockActual}{' '}
                {productoElegido.unidad}
              </p>
            )}
          </>
        )}

        {tipoItem === TIPO_ITEM_SERVICIO.servicio && (
          <>
            <Form.Item
              label="Servicio de catálogo"
              name="servicioId"
              rules={[{ required: true, message: 'Selecciona el servicio' }]}
              extra={
                puedeRegistrarServicio && (
                  <Button type="link" size="small" style={{ paddingInline: 0 }} onClick={() => setAltaServicio(true)}>
                    ¿No está en el catálogo? Registrar servicio
                  </Button>
                )
              }
            >
              <Select
                showSearch
                optionFilterProp="label"
                loading={servicios.isPending}
                placeholder="Buscar servicio"
                onChange={(sId) => {
                  const s = (servicios.data ?? []).find((item) => item.id === sId)
                  if (s) {
                    formulario.setFieldsValue({
                      precioUnitario: s.precioSugerido,
                      tipoAfectacionIgv: s.tipoAfectacionIgv,
                    })
                  }
                }}
                options={(servicios.data ?? []).map((servicio) => ({
                  value: servicio.id,
                  label: `${servicio.nombre} · Sugerido: ${soles(servicio.precioSugerido)}`,
                }))}
              />
            </Form.Item>
            {servicioElegido && (
              <p className="texto-secundario" style={{ marginTop: -8, marginBottom: 12 }}>
                Precio sugerido: {soles(servicioElegido.precioSugerido)}
              </p>
            )}
          </>
        )}

        {(tipoItem === TIPO_ITEM_SERVICIO.manoDeObra || tipoItem === TIPO_ITEM_SERVICIO.terceros) && (
          <Form.Item
            label="Descripción del trabajo"
            name="descripcion"
            rules={[{ required: true, message: 'Describe el trabajo o concepto' }]}
          >
            <Input placeholder="Ej. Torneado de tambor de freno" />
          </Form.Item>
        )}

        <Form.Item
          label="Cantidad"
          name="cantidad"
          rules={[
            { required: true, message: 'Indica la cantidad' },
            {
              validator: (_, valor: number) =>
                tipoItem !== TIPO_ITEM_SERVICIO.repuesto || !productoElegido || valor <= productoElegido.stockActual
                  ? Promise.resolve()
                  : Promise.reject(new Error(`Stock insuficiente. Solo hay ${productoElegido.stockActual} disponibles`)),
            },
          ]}
        >
          <InputNumber min={1} style={{ width: '100%' }} />
        </Form.Item>

        {(conPrecioDeLista || puedeModificarPrecios) && (
          <Form.Item label="Precio unitario (PEN, sin IGV)" name="precioUnitario">
            <InputNumber min={0} step={0.5} style={{ width: '100%' }} placeholder="Opcional: toma el precio de lista si se deja vacío" />
          </Form.Item>
        )}
        {conPrecioDeLista && pideAprobacion && <AvisoPrecioGerencia precio={precioUnitario} deLista={precioDeLista} />}

        <Form.Item
          label="Afectación tributaria (SUNAT)"
          name="tipoAfectacionIgv"
          rules={[{ required: true, message: 'Indica la afectación de IGV' }]}
        >
          <Select
            options={[
              { value: TIPO_AFECTACION_IGV.gravado, label: 'Gravado (Aplica IGV)' },
              { value: TIPO_AFECTACION_IGV.exonerado, label: 'Exonerado (IGV 0%)' },
              { value: TIPO_AFECTACION_IGV.inafecto, label: 'Inafecto (IGV 0%)' },
            ]}
          />
        </Form.Item>
      </Form>

      <ModalAltaRapidaRepuesto
        abierto={altaRepuesto}
        onCerrar={() => setAltaRepuesto(false)}
        onRegistrado={(repuesto) => {
          setAltaRepuesto(false)
          formulario.setFieldsValue({ productoId: repuesto.id, precioUnitario: repuesto.precioVenta })
        }}
      />
      <ModalAltaRapidaServicio
        abierto={altaServicio}
        onCerrar={() => setAltaServicio(false)}
        onRegistrado={(servicio) => {
          setAltaServicio(false)
          formulario.setFieldsValue({
            servicioId: servicio.id,
            precioUnitario: servicio.precioSugerido,
            tipoAfectacionIgv: servicio.tipoAfectacionIgv,
          })
        }}
      />
    </Modal>
  )
}
