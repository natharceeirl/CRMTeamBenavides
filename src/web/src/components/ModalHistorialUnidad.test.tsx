// @vitest-environment jsdom
import { screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import type { AtencionServicioUnidad } from '../api/portal'
import type { VehiculoResponse } from '../api/tipos'
import { apiFalsa, montar } from '../test/pantalla'
import { ModalHistorialUnidad } from './ModalHistorialUnidad'

const unidad = { id: 'v1', marca: 'Yamaha', modelo: 'Crypton FI 115', placa: '1845-3C', numeroSerieVIN: null } as VehiculoResponse

const atencion: AtencionServicioUnidad = {
  ordenServicioId: 'os-6',
  numeroOrden: 'OS-000006',
  fechaIngreso: '2026-09-15T16:15:00Z',
  fechaSalida: '2026-09-17T22:30:00Z',
  estado: 'Entregada',
  estadoId: 5,
  kilometrajeIngreso: 15230,
  horasUsoIngreso: null,
  lecturaMedidorIngreso: 15230,
  tipoMedidor: 'Kilometraje',
  motivoFalla: 'No enciende en las mañanas',
  solucion: 'Cambio de batería',
  observacionesCliente: 'Revisar también las luces',
  total: 236,
  items: [
    {
      id: 'i1',
      descripcion: 'Batería YTX7L-BS',
      servicioNombre: null,
      productoCodigo: 'ELE-BAT-7L',
      tipoItem: 0,
      tipoItemNombre: 'Repuesto',
      cantidad: 1,
      precioUnitario: 177,
      total: 177,
    },
    {
      id: 'i2',
      descripcion: 'Diagnóstico del sistema eléctrico',
      servicioNombre: null,
      productoCodigo: null,
      tipoItem: 2,
      tipoItemNombre: 'ManoDeObra',
      cantidad: 1,
      precioUnitario: 59,
      total: 59,
    },
  ],
}

describe('historial de servicio de la unidad', () => {
  it('el personal ve la orden enlazada, el tipo de cada trabajo y lo que pidió el cliente', async () => {
    apiFalsa({ 'GET /vehiculos/:id/historial-servicio': [atencion] })

    montar(<ModalHistorialUnidad unidad={unidad} onCerrar={vi.fn()} paraPersonal puedeVerOrdenes />)

    const enlace = await screen.findByRole('link', { name: 'OS-000006' })
    expect(enlace.getAttribute('href')).toBe('/ordenes/os-6')
    expect(screen.getByText('Mano de obra')).toBeTruthy() // no «ManoDeObra», el nombre interno
    expect(screen.getByText('Repuesto · ELE-BAT-7L')).toBeTruthy()
    expect(screen.getByText('Revisar también las luces')).toBeTruthy()
    expect(screen.getByText('15,230 km')).toBeTruthy()
  })

  it('sin permiso para ver todas las órdenes, la referencia no es un enlace', async () => {
    apiFalsa({ 'GET /vehiculos/:id/historial-servicio': [atencion] })

    montar(<ModalHistorialUnidad unidad={unidad} onCerrar={vi.fn()} paraPersonal puedeVerOrdenes={false} />)

    await screen.findByText(/OS-000006/)
    expect(screen.queryByRole('link', { name: 'OS-000006' })).toBeNull()
  })

  it('el cliente, en su portal, no ve enlaces internos ni datos del taller', async () => {
    apiFalsa({ 'GET /vehiculos/:id/historial-servicio': [atencion] })

    montar(<ModalHistorialUnidad unidad={unidad} onCerrar={vi.fn()} />)

    await screen.findByText(/OS-000006/)
    expect(screen.queryByRole('link')).toBeNull()
    expect(screen.queryByText('Mano de obra')).toBeNull()
    expect(screen.queryByText('Revisar también las luces')).toBeNull()
  })

  it('una unidad sin atenciones lo dice', async () => {
    apiFalsa({ 'GET /vehiculos/:id/historial-servicio': [] })

    montar(<ModalHistorialUnidad unidad={unidad} onCerrar={vi.fn()} paraPersonal />)

    expect(await screen.findByText('Esta unidad todavía no tiene servicios en el taller.')).toBeTruthy()
  })
})
