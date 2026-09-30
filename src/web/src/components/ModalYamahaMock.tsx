import { useState } from 'react'
import { Button, Input, Modal, Space } from 'antd'
import { useYamahaMockConsulta } from '../api/yamaha'
import { EtiquetaEstado } from './EtiquetaEstado'

type Props = {
  abierto: boolean
  criterioInicial?: string | null
  onCerrar: () => void
}

/** Modelos que tiene el catálogo simulado, para probar rápido. */
const EJEMPLOS = [
  { etiqueta: 'MT-03', valor: 'MT-03' },
  { etiqueta: 'MT-07', valor: 'MT-07' },
  { etiqueta: 'FZ-25', valor: 'FZ-25' },
  { etiqueta: 'NMAX 155', valor: 'NMAX-155' },
  { etiqueta: 'WaveRunner FX', valor: 'WAVERUNNER-FX' },
  { etiqueta: 'Generador EF2000iS', valor: 'GEN-EF2000IS' },
]

const fila = (etiqueta: string, valor: string | number | null | undefined) =>
  valor === null || valor === undefined || valor === '' ? null : (
    <tr key={etiqueta}>
      <td>{etiqueta}</td>
      <td>{valor}</td>
    </tr>
  )

/**
 * Consulta al catálogo de Yamaha. Hoy responde el adaptador simulado del
 * backend: todavía no hay credenciales ni documentación de la API real.
 */
export function ModalYamahaMock({ abierto, criterioInicial, onCerrar }: Readonly<Props>) {
  const [criterio, setCriterio] = useState(criterioInicial ?? 'MT-03')
  const [consultado, setConsultado] = useState(criterioInicial ?? 'MT-03')

  const query = useYamahaMockConsulta(abierto ? consultado : null)
  const datos = query.data

  const buscar = (valor: string) => {
    const limpio = valor.trim()
    if (!limpio) return
    setCriterio(limpio)
    setConsultado(limpio)
  }

  return (
    <Modal
      title={
        <Space size={8}>
          <span>Catálogo Yamaha</span>
          <EtiquetaEstado tono="suave">Datos de prueba</EtiquetaEstado>
        </Space>
      }
      open={abierto}
      onCancel={onCerrar}
      footer={<Button onClick={onCerrar}>Cerrar</Button>}
      width={800}
      destroyOnHidden
    >
      <p className="texto-secundario" style={{ marginBottom: 16 }}>
        Todavía no hay conexión con Yamaha: estas respuestas son simuladas y sirven para probar el flujo.
      </p>

      <Space.Compact style={{ width: '100%' }}>
        <Input
          placeholder="Modelo, VIN o número de serie"
          value={criterio}
          onChange={(evento) => setCriterio(evento.target.value)}
          onPressEnter={() => buscar(criterio)}
          allowClear
        />
        <Button type="primary" onClick={() => buscar(criterio)} loading={query.isFetching}>
          Consultar
        </Button>
      </Space.Compact>
      <Space wrap size={[8, 8]} style={{ marginTop: 12 }}>
        {EJEMPLOS.map((ejemplo) => (
          <Button
            key={ejemplo.valor}
            size="small"
            type={consultado === ejemplo.valor ? 'primary' : 'default'}
            onClick={() => buscar(ejemplo.valor)}
          >
            {ejemplo.etiqueta}
          </Button>
        ))}
      </Space>

      {query.isFetching && <p className="texto-secundario" style={{ marginTop: 20 }}>Consultando…</p>}

      {!query.isFetching && query.isError && (
        <p className="texto-secundario" style={{ marginTop: 20 }}>
          No hay datos para «{consultado}». Prueba con otro modelo o VIN.
        </p>
      )}

      {!query.isFetching && datos && (
        <div style={{ marginTop: 24 }}>
          <div className="bloque-modal">
            <div className="seccion-titulo" style={{ marginBottom: 8 }}>
              <h2 style={{ fontSize: 21 }}>{datos.nombreComercial}</h2>
              <EtiquetaEstado tono="neutro">{datos.categoria}</EtiquetaEstado>
            </div>
            <table className="tabla-simple">
              <tbody>
                {fila('Código de modelo', datos.codigoModelo)}
                {fila('Año', datos.anioFabricacion)}
                {fila('VIN de referencia', datos.vinEjemplo)}
                {fila('Motor de referencia', datos.numeroMotorEjemplo)}
                {fila('Garantía de fábrica', datos.estadoGarantia)}
              </tbody>
            </table>
          </div>

          <div className="bloque-modal">
            <h3>Especificaciones</h3>
            <table className="tabla-simple">
              <tbody>
                {fila('Motor', datos.especificaciones.motorTipo)}
                {fila('Cilindrada', `${datos.especificaciones.cilindradaCc} cc`)}
                {fila('Potencia', `${datos.especificaciones.potenciaHp} HP`)}
                {fila('Torque', `${datos.especificaciones.torqueNm} Nm`)}
                {fila('Refrigeración', datos.especificaciones.refrigeracion)}
                {fila('Tanque', datos.especificaciones.capacidadTanque)}
                {fila('Aceite de motor', datos.especificaciones.capacidadAceiteMotor)}
                {fila('Bujía', datos.especificaciones.tipoBujiaRecomendada)}
                {fila('Presión de neumáticos', datos.especificaciones.presionNeumaticos)}
              </tbody>
            </table>
          </div>

          <div className="dos-bloques bloque-modal">
            <div>
              <h3>Campañas técnicas</h3>
              {datos.campaniasServicio.length === 0 ? (
                <p className="texto-secundario">Sin campañas pendientes.</p>
              ) : (
                <ul style={{ margin: 0, paddingLeft: 18 }}>
                  {datos.campaniasServicio.map((campania) => (
                    <li key={campania}>{campania}</li>
                  ))}
                </ul>
              )}
            </div>
            <div>
              <h3>Mantenimiento sugerido</h3>
              <ul style={{ margin: 0, paddingLeft: 18 }}>
                {datos.intervalosMantenimiento.map((intervalo) => (
                  <li key={intervalo}>{intervalo}</li>
                ))}
              </ul>
            </div>
          </div>
        </div>
      )}
    </Modal>
  )
}
