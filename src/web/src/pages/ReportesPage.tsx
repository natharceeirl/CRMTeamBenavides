import { useState } from 'react'
import { Segmented } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { Indicadores } from '../components/Indicadores'
import { estadosOrden, ordenes, serviciosFrecuentes, ventasPorDia, type EstadoOrden } from '../data/ejemplo'
import { entero, soles } from '../utils/formato'

type Periodo = 'Hoy' | 'Semana' | 'Mes'

// Wireframe temprano: cifras de ejemplo fijas, sin gráficos todavía.
const resumenSemana = { ordenesEntregadas: 14, clientesAtendidos: 21, repuestosVendidos: 63 }

export function ReportesPage() {
  const [periodo, setPeriodo] = useState<Periodo>('Semana')

  const ventasSemana = ventasPorDia.reduce((suma, dia) => suma + dia.total, 0)
  const ordenesPorEstado = (Object.keys(estadosOrden) as EstadoOrden[])
    .map((estado) => ({ estado, cantidad: ordenes.filter((orden) => orden.estado === estado).length }))
    .filter((fila) => fila.cantidad > 0)

  return (
    <>
      <BarraSuperior
        titulo="Reportes"
        acciones={<Segmented<Periodo> options={['Hoy', 'Semana', 'Mes']} value={periodo} onChange={setPeriodo} />}
      />
      <div className="pagina">
        <Indicadores
          items={[
            { etiqueta: 'Ventas', valor: `S/ ${entero(ventasSemana)}`, compacto: true },
            { etiqueta: 'Órdenes entregadas', valor: resumenSemana.ordenesEntregadas },
            { etiqueta: 'Clientes atendidos', valor: resumenSemana.clientesAtendidos },
            { etiqueta: 'Repuestos vendidos', valor: resumenSemana.repuestosVendidos },
          ]}
        />
        <div className="tres-columnas">
          <section>
            <div className="seccion-titulo">
              <h2>Ventas por día</h2>
            </div>
            <table className="tabla-simple">
              <tbody>
                {ventasPorDia.map((dia) => (
                  <tr key={dia.dia}>
                    <td>{dia.dia}</td>
                    <td>{soles(dia.total)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>
          <section>
            <div className="seccion-titulo">
              <h2>Órdenes por estado</h2>
            </div>
            <table className="tabla-simple">
              <tbody>
                {ordenesPorEstado.map((fila) => (
                  <tr key={fila.estado}>
                    <td>{estadosOrden[fila.estado]}</td>
                    <td>{entero(fila.cantidad)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>
          <section>
            <div className="seccion-titulo">
              <h2>Servicios más solicitados</h2>
            </div>
            <table className="tabla-simple">
              <tbody>
                {serviciosFrecuentes.map((fila) => (
                  <tr key={fila.servicio}>
                    <td>{fila.servicio}</td>
                    <td>{entero(fila.cantidad)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>
        </div>
      </div>
    </>
  )
}
