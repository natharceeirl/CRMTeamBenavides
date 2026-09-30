import { nombresEstado } from '../api/ordenes'
import type { FormatoAtencionResponse } from '../api/tipos'
import { documentoImprimible, filaDato, lineaTotal } from './documento'
import { escaparHtml } from './impresion'
import { entero, fechaHoraConAnio, importe, nombreDeEnum, soles } from './formato'

/**
 * Hoja imprimible del formato de atención de una orden, con recuadros para la
 * firma del cliente y del taller. Se arma con los datos de
 * GET /api/ordenes-servicio/{id}/formato-atencion, con el mismo estilo que la
 * ficha del comprobante.
 */
export function htmlFormatoAtencion(datos: FormatoAtencionResponse): string {
  const { empresa, orden, cliente, unidad, trabajo, items, financiero } = datos
  const medidor = unidad.tipoMedidor === 'Horas' ? 'h' : 'km'
  const estado = nombresEstado[orden.estadoId] ?? nombreDeEnum(orden.estadoNombre)
  const titulo = `Orden de servicio ${orden.numeroOrden}`

  const datosEmpresa = [
    empresa.razonSocial && empresa.razonSocial !== empresa.nombreTaller ? empresa.razonSocial : null,
    empresa.ruc ? `RUC ${empresa.ruc}` : null,
    empresa.direccion,
    empresa.telefono,
    empresa.email,
  ].filter(Boolean)

  const filasItems =
    items.length === 0
      ? '<tr><td colspan="4" class="sec">Sin trabajos ni repuestos registrados.</td></tr>'
      : items
          .map(
            (item) => `<tr>
        <td>${escaparHtml(item.descripcion)}<div class="sec">${escaparHtml(
          [item.tipoItemNombre, item.afectacionIgv === 'Gravado' ? null : item.afectacionIgv].filter(Boolean).join(' · '),
        )}</div></td>
        <td class="num">${escaparHtml(item.cantidad)}</td>
        <td class="num">${importe(item.precioUnitario)}</td>
        <td class="num">${importe(item.total)}</td>
      </tr>`,
          )
          .join('')

  const hayTrabajo = [trabajo.motivoFalla, trabajo.diagnostico, trabajo.solucion, trabajo.observaciones].some(Boolean)

  return documentoImprimible(
    titulo,
    `
  <div class="cabecera">
    <div>
      <h1>${escaparHtml(empresa.nombreTaller)}</h1>
      <div class="sec">${datosEmpresa.map(escaparHtml).join(' · ')}</div>
    </div>
    <div>
      <h2 class="sin-salto">${escaparHtml(orden.numeroOrden)}</h2>
      <div class="sec derecha">Formato de atención · ${escaparHtml(estado)}</div>
      <div class="sec derecha">Emitido el ${escaparHtml(fechaHoraConAnio(datos.fechaEmision))}</div>
    </div>
  </div>

  <div class="bloque">
    <div>
      <h3>Cliente</h3>
      <table class="datos"><tbody>
        ${filaDato('Nombre', cliente.nombreCompleto)}
        ${filaDato('Razón social', cliente.razonSocial)}
        ${filaDato(cliente.tipoDocumento ?? 'Documento', cliente.numeroDocumento)}
        ${filaDato('Teléfono', cliente.telefono)}
        ${filaDato('Dirección', cliente.direccion)}
      </tbody></table>
    </div>
    <div>
      <h3>Unidad</h3>
      <table class="datos"><tbody>
        ${filaDato('Unidad', `${nombreDeEnum(unidad.tipoUnidad)} · ${unidad.marca} ${unidad.modelo}${unidad.anio ? ` (${unidad.anio})` : ''}`)}
        ${filaDato('Placa', unidad.placa)}
        ${filaDato('VIN o serie', unidad.numeroSerieVIN)}
        ${filaDato('Motor', unidad.numeroMotor)}
        ${filaDato('Color', unidad.color)}
        ${filaDato('Medidor al ingresar', unidad.lecturaIngreso == null ? null : `${entero(unidad.lecturaIngreso)} ${medidor}`)}
      </tbody></table>
    </div>
  </div>

  <div class="bloque">
    <div>
      <h3>Atención</h3>
      <table class="datos"><tbody>
        ${filaDato('Ingreso', fechaHoraConAnio(orden.fechaIngreso))}
        ${filaDato('Entrega estimada', orden.fechaEstimadaEntrega ? fechaHoraConAnio(orden.fechaEstimadaEntrega) : null)}
        ${filaDato('Salida', orden.fechaSalida ? fechaHoraConAnio(orden.fechaSalida) : null)}
        ${filaDato('Tipo de atención', `${nombreDeEnum(orden.tipoAtencion)} · ${nombreDeEnum(orden.modalidadAtencion)}`)}
        ${filaDato('Tipo de falla', orden.tipoFalla ? nombreDeEnum(orden.tipoFalla) : null)}
      </tbody></table>
    </div>
    <div>
      <h3>Responsable</h3>
      <table class="datos"><tbody>
        ${filaDato('Técnico', trabajo.tecnicoResponsable ?? 'Sin asignar')}
        ${filaDato('Correo', trabajo.tecnicoEmail)}
      </tbody></table>
    </div>
  </div>

  <div class="seccion">
    <h3>Diagnóstico</h3>
    ${
      hayTrabajo
        ? `<table class="datos"><tbody>
        ${filaDato('Falla reportada', trabajo.motivoFalla)}
        ${filaDato('Diagnóstico', trabajo.diagnostico)}
        ${filaDato('Solución', trabajo.solucion)}
        ${filaDato('Observaciones', trabajo.observaciones)}
      </tbody></table>`
        : '<p class="sec">Sin diagnóstico ni observaciones registradas.</p>'
    }
  </div>

  <div class="seccion">
    <h3>Trabajos y repuestos</h3>
    <table>
      <thead><tr><th>Concepto</th><th class="num">Cant.</th><th class="num">P. unit.</th><th class="num">Total</th></tr></thead>
      <tbody>${filasItems}</tbody>
    </table>
    <div class="totales">
      ${lineaTotal('Op. gravadas', soles(financiero.subtotalGravado))}
      ${financiero.subtotalExonerado > 0 ? lineaTotal('Op. exoneradas', soles(financiero.subtotalExonerado)) : ''}
      ${financiero.subtotalInafecto > 0 ? lineaTotal('Op. inafectas', soles(financiero.subtotalInafecto)) : ''}
      ${lineaTotal(`IGV (${financiero.porcentajeIgv} %)`, soles(financiero.montoIgv))}
      ${lineaTotal('Total', soles(financiero.total), 'total')}
      ${lineaTotal('Pagado', soles(financiero.totalPagado))}
      ${lineaTotal('Saldo pendiente', soles(financiero.saldoPendiente), 'saldo')}
    </div>
  </div>

  <div class="firmas">
    <div class="firma">
      <div class="etiqueta">Firma y DNI del cliente</div>
      <div>${escaparHtml(cliente.nombreCompleto)}</div>
    </div>
    <div class="firma">
      <div class="etiqueta">Firma y sello del taller</div>
      <div>${escaparHtml(trabajo.tecnicoResponsable ?? empresa.nombreTaller)}</div>
    </div>
  </div>`,
  )
}
