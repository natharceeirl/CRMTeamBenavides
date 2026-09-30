import { describe, expect, it } from 'vitest'
import { cambiosDelDetalle, camposDelDetalle, etiquetaDeCampo, nombreEntidad } from './auditoria'

describe('auditoría', () => {
  it('muestra el detalle JSON como campos legibles', () => {
    expect(camposDelDetalle('{"MontoApertura":200,"Observaciones":null}')).toEqual([
      { campo: 'Monto apertura', valor: '200' },
      { campo: 'Observaciones', valor: '—' },
    ])
  })

  it('un detalle que no es objeto JSON se muestra tal cual', () => {
    expect(camposDelDetalle('texto libre')).toBe('texto libre')
    expect(camposDelDetalle('[1,2]')).toBe('[1,2]')
    expect(camposDelDetalle(null)).toBeNull()
  })

  it('de valores anteriores y nuevos muestra solo lo que cambió', () => {
    const detalle = JSON.stringify({
      ValoresAnteriores: { Ruc: '20000000001', Direccion: 'Av. Ejército 101' },
      ValoresNuevos: { Ruc: '20000000001', Direccion: null },
    })
    expect(cambiosDelDetalle(detalle)).toEqual([{ campo: 'Direccion', antes: 'Av. Ejército 101', despues: '—' }])
    expect(cambiosDelDetalle('{"MontoApertura":200}')).toBeNull()
  })

  it('nombra las entidades en español y deja pasar las desconocidas', () => {
    expect(nombreEntidad('MovimientoCajaChica')).toBe('Movimiento de caja')
    expect(nombreEntidad('Algo')).toBe('Algo')
    expect(etiquetaDeCampo('ValorNuevo')).toBe('Valor nuevo')
  })
})
