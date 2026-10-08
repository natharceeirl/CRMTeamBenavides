import { useEffect } from 'react'
import { Button, Form, Input, InputNumber, Table, type TableProps } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { Indicadores } from '../components/Indicadores'
import {
  useActualizarConfiguracionEmpresa,
  useConfiguracionEmpresa,
  useHistorialTipoCambio,
  useRegistrarTipoCambio,
  useTipoCambio,
} from '../api/configuracion'
import type { HistorialTipoCambioResponse } from '../api/tipos'
import { fechaHora } from '../utils/formato'
import { tipoDeCambio } from '../utils/tipoCambio'
import { ConversorDolares } from '../components/ConversorDolares'

type Campos = {
  nombreEmpresa: string
  razonSocial?: string
  ruc?: string
  direccion?: string
  telefono?: string
  email?: string
  porcentajeIgv: number
}

type CamposTipoCambio = {
  valorCompra: number
  valorVenta: number
  observaciones?: string
}

const sinVacios = (valor?: string) => valor?.trim() ?? ''

const columnasHistorial: TableProps<HistorialTipoCambioResponse>['columns'] = [
  { title: 'Vigente desde', dataIndex: 'fechaVigencia', className: 'num', render: (fecha: string) => fechaHora(fecha) },
  {
    title: 'Moneda',
    key: 'moneda',
    render: (_, registro) => `${registro.monedaOrigen} → ${registro.monedaDestino}`,
  },
  { title: 'Compra', dataIndex: 'valorCompra', align: 'right', className: 'num', render: (valor: number) => tipoDeCambio(valor) },
  { title: 'Venta', dataIndex: 'valorVenta', align: 'right', className: 'num', render: (valor: number) => tipoDeCambio(valor) },
  { title: 'Registró', dataIndex: 'usuarioNombre', render: (nombre: string | null) => nombre ?? '—' },
  { title: 'Observaciones', dataIndex: 'observaciones', render: (texto: string | null) => texto ?? '—' },
]

/**
 * Datos de la empresa, IGV y tipo de cambio del día. Solo entra quien tiene
 * `configuracion.editar`: la ruta ya lo exige.
 */
export function ConfiguracionPage() {
  const [formulario] = Form.useForm<Campos>()
  const [formularioTipoCambio] = Form.useForm<CamposTipoCambio>()
  const configuracion = useConfiguracionEmpresa()
  const actualizar = useActualizarConfiguracionEmpresa()
  const tipoCambio = useTipoCambio()
  const historial = useHistorialTipoCambio()
  const registrarTipoCambio = useRegistrarTipoCambio()

  // El formulario se llena cuando llegan los datos, y otra vez si cambian al guardar.
  useEffect(() => {
    if (configuracion.data) {
      const datos = configuracion.data
      formulario.setFieldsValue({
        nombreEmpresa: datos.nombreEmpresa,
        razonSocial: datos.razonSocial ?? '',
        ruc: datos.ruc ?? '',
        direccion: datos.direccion ?? '',
        telefono: datos.telefono ?? '',
        email: datos.email ?? '',
        porcentajeIgv: datos.porcentajeIgv,
      })
    }
  }, [configuracion.data, formulario])

  // El API toma null como «sin cambios»: un campo vaciado se manda como texto vacío.
  const guardar = async (campos: Campos) => {
    await actualizar.mutateAsync({
      nombreEmpresa: campos.nombreEmpresa.trim(),
      razonSocial: sinVacios(campos.razonSocial),
      ruc: sinVacios(campos.ruc),
      direccion: sinVacios(campos.direccion),
      telefono: sinVacios(campos.telefono),
      email: sinVacios(campos.email),
      porcentajeIgv: campos.porcentajeIgv,
    })
  }

  const registrar = async (campos: CamposTipoCambio) => {
    await registrarTipoCambio.mutateAsync({
      valorCompra: campos.valorCompra,
      valorVenta: campos.valorVenta,
      observaciones: campos.observaciones?.trim() || null,
    })
    formularioTipoCambio.resetFields()
  }

  const vigente = tipoCambio.data

  return (
    <>
      <BarraSuperior
        titulo="Configuración"
        acciones={
          <Button type="primary" loading={actualizar.isPending} onClick={() => formulario.submit()}>
            Guardar datos de la empresa
          </Button>
        }
      />
      <div className="pagina">
        <AvisoError error={configuracion.error ?? actualizar.error} />
        <Form<Campos>
          form={formulario}
          layout="vertical"
          requiredMark={false}
          className="formulario"
          onFinish={guardar}
          disabled={configuracion.isPending}
        >
          <section className="bloque">
            <h2>Empresa</h2>
            <div className="formulario-grid">
              <Form.Item
                label="Nombre comercial"
                name="nombreEmpresa"
                rules={[{ required: true, whitespace: true, message: 'Ingresa el nombre de la empresa' }]}
              >
                <Input placeholder="Team Benavides" maxLength={150} />
              </Form.Item>
              <Form.Item label="Razón social" name="razonSocial" extra="Si se deja vacía, se usa el nombre comercial.">
                <Input placeholder="Team Benavides S.R.L." maxLength={200} />
              </Form.Item>
              <Form.Item
                label="RUC"
                name="ruc"
                rules={[{ pattern: /^\d{11}$/, message: 'El RUC tiene 11 dígitos' }]}
              >
                <Input inputMode="numeric" maxLength={11} placeholder="20XXXXXXXXX" />
              </Form.Item>
              <Form.Item label="Teléfono" name="telefono">
                <Input maxLength={30} placeholder="054 000000" />
              </Form.Item>
              <Form.Item label="Dirección" name="direccion" className="ancho-completo">
                <Input maxLength={250} placeholder="Av. …, Arequipa" />
              </Form.Item>
              <Form.Item label="Correo" name="email" rules={[{ type: 'email', message: 'Revisa el correo' }]}>
                <Input maxLength={150} placeholder="contacto@teambenavides.pe" />
              </Form.Item>
              <Form.Item label="Moneda base" extra="Todos los montos del sistema están en soles.">
                <Input value="Soles (PEN)" disabled />
              </Form.Item>
            </div>
          </section>

          <section className="bloque">
            <h2>Impuestos</h2>
            <div className="formulario-grid">
              <Form.Item
                label="IGV (%)"
                name="porcentajeIgv"
                rules={[{ required: true, message: 'Ingresa el porcentaje de IGV' }]}
                extra="Se aplica a los ítems que se agreguen desde ahora. Las líneas ya registradas conservan el IGV con el que se calcularon."
              >
                <InputNumber min={0} max={100} precision={2} style={{ width: '100%' }} />
              </Form.Item>
            </div>
          </section>
        </Form>

        <section className="bloque formulario">
          <h2>Tipo de cambio</h2>
          <AvisoError error={tipoCambio.error ?? historial.error ?? registrarTipoCambio.error} />
          {vigente?.configurado ? (
            <>
              <Indicadores
                tamano="mediano"
                items={[
                  { etiqueta: 'Compra', valor: tipoDeCambio(vigente.valorCompra) },
                  { etiqueta: 'Venta (vigente)', valor: tipoDeCambio(vigente.valorVenta ?? vigente.tipoCambio) },
                  { etiqueta: 'Actualizado', valor: fechaHora(vigente.fechaActualizacion) },
                ]}
              />
              {vigente.ultimoUsuarioNombre && (
                <p className="texto-secundario" style={{ marginTop: 12 }}>
                  Lo registró {vigente.ultimoUsuarioNombre}.
                </p>
              )}
            </>
          ) : (
            !tipoCambio.isPending && <p className="texto-secundario">Todavía no se registra un tipo de cambio.</p>
          )}
          {vigente?.configurado && (
            <div style={{ marginTop: 24 }}>
              <h3 style={{ marginTop: 0 }}>Convertir un precio en dólares</h3>
              <ConversorDolares />
            </div>
          )}

          <Form<CamposTipoCambio>
            form={formularioTipoCambio}
            layout="vertical"
            requiredMark={false}
            onFinish={registrar}
            style={{ marginTop: 24 }}
          >
            <div className="formulario-grid">
              <Form.Item
                label="Compra (S/ por US$)"
                name="valorCompra"
                rules={[{ required: true, message: 'Ingresa el valor de compra' }]}
              >
                <InputNumber min={0.0001} precision={4} step={0.001} style={{ width: '100%' }} />
              </Form.Item>
              <Form.Item
                label="Venta (S/ por US$)"
                name="valorVenta"
                dependencies={['valorCompra']}
                rules={[
                  { required: true, message: 'Ingresa el valor de venta' },
                  ({ getFieldValue }) => ({
                    // En una casa de cambio la venta nunca está por debajo de la compra.
                    validator: async (_, venta: number | undefined) => {
                      const compra = getFieldValue('valorCompra') as number | undefined
                      if (venta != null && compra != null && venta < compra) {
                        throw new Error('La venta no puede ser menor que la compra.')
                      }
                    },
                  }),
                ]}
              >
                <InputNumber min={0.0001} precision={4} step={0.001} style={{ width: '100%' }} />
              </Form.Item>
              <Form.Item label="Observaciones" name="observaciones" className="ancho-completo">
                <Input maxLength={250} placeholder="Fuente: SUNAT, casa de cambio…" />
              </Form.Item>
            </div>
            <Button htmlType="submit" loading={registrarTipoCambio.isPending}>
              Registrar tipo de cambio del día
            </Button>
          </Form>

          <Table
            rowKey="id"
            columns={columnasHistorial}
            dataSource={historial.data ?? []}
            pagination={(historial.data?.length ?? 0) > 10 ? { pageSize: 10 } : false}
            loading={historial.isPending}
            locale={{ emptyText: 'Sin registros de tipo de cambio' }}
            style={{ marginTop: 24 }}
          />
        </section>
      </div>
    </>
  )
}
