import { useEffect } from 'react'
import { Button, Form, Input, InputNumber } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { useActualizarConfiguracionEmpresa, useConfiguracionEmpresa } from '../api/configuracion'

type Campos = {
  nombreEmpresa: string
  ruc?: string
  porcentajeIgv: number
}

/**
 * Datos de la empresa e IGV vigente (GET y PUT /api/configuracion/empresa).
 * Solo entra quien tiene `configuracion.editar`: la ruta ya lo exige.
 */
export function ConfiguracionPage() {
  const [formulario] = Form.useForm<Campos>()
  const configuracion = useConfiguracionEmpresa()
  const actualizar = useActualizarConfiguracionEmpresa()

  // El formulario se llena cuando llegan los datos, y otra vez si cambian al guardar.
  useEffect(() => {
    if (configuracion.data) {
      formulario.setFieldsValue({
        nombreEmpresa: configuracion.data.nombreEmpresa,
        ruc: configuracion.data.ruc ?? '',
        porcentajeIgv: configuracion.data.porcentajeIgv,
      })
    }
  }, [configuracion.data, formulario])

  const guardar = async (campos: Campos) => {
    await actualizar.mutateAsync({
      nombreEmpresa: campos.nombreEmpresa.trim(),
      ruc: campos.ruc?.trim() ? campos.ruc.trim() : null,
      porcentajeIgv: campos.porcentajeIgv,
    })
  }

  return (
    <>
      <BarraSuperior
        titulo="Configuración"
        acciones={
          <Button type="primary" loading={actualizar.isPending} onClick={() => formulario.submit()}>
            Guardar
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
                label="Nombre de la empresa"
                name="nombreEmpresa"
                className="ancho-completo"
                rules={[{ required: true, whitespace: true, message: 'Ingresa el nombre de la empresa' }]}
              >
                <Input placeholder="Team Benavides S.R.L." />
              </Form.Item>
              <Form.Item
                label="RUC"
                name="ruc"
                rules={[{ pattern: /^\d{11}$/, message: 'El RUC tiene 11 dígitos' }]}
              >
                <Input inputMode="numeric" maxLength={11} placeholder="20XXXXXXXXX" />
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
      </div>
    </>
  )
}
