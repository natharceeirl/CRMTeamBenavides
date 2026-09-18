import { describe, expect, it } from 'vitest'
import { ATENCION, nombresAtencion, puedeAsignarse, puedeCerrarse, rutaFaqs } from './chatbot'

describe('estados de atención', () => {
  it('coinciden con el enum del backend', () => {
    expect(ATENCION.pendiente).toBe(0)
    expect(ATENCION.enAtencion).toBe(1)
    expect(ATENCION.resuelto).toBe(2)
    expect(ATENCION.descartado).toBe(3)
    expect(nombresAtencion[ATENCION.enAtencion]).toBe('En atención')
  })

  it('solo se toma una consulta pendiente', () => {
    expect(puedeAsignarse(ATENCION.pendiente)).toBe(true)
    expect(puedeAsignarse(ATENCION.enAtencion)).toBe(false)
    expect(puedeAsignarse(ATENCION.resuelto)).toBe(false)
  })

  it('se cierra lo que sigue abierto', () => {
    expect(puedeCerrarse(ATENCION.pendiente)).toBe(true)
    expect(puedeCerrarse(ATENCION.enAtencion)).toBe(true)
    expect(puedeCerrarse(ATENCION.resuelto)).toBe(false)
    expect(puedeCerrarse(ATENCION.descartado)).toBe(false)
  })
})

describe('rutaFaqs', () => {
  it('sin filtros pide todas', () => {
    expect(rutaFaqs('/chatbot/faqs')).toBe('/chatbot/faqs')
  })

  it('filtra por categoría y por búsqueda', () => {
    expect(rutaFaqs('/chatbot/faqs', { categoria: 'Horarios' })).toBe('/chatbot/faqs?categoria=Horarios')
    expect(rutaFaqs('/chatbot/faqs', { busqueda: '  aceite  ' })).toBe('/chatbot/faqs?busqueda=aceite')
  })

  it('ignora la búsqueda en blanco', () => {
    expect(rutaFaqs('/chatbot/faqs', { busqueda: '   ' })).toBe('/chatbot/faqs')
  })

  it('sirve igual para la ruta de administración', () => {
    expect(rutaFaqs('/chatbot/admin/faqs', { categoria: 'Taller' })).toBe(
      '/chatbot/admin/faqs?categoria=Taller',
    )
  })
})
