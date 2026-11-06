import type { ReactNode } from 'react'
import { Form, Input } from 'antd'

type Props = {
  /** Dónde están los campos en el formulario: [] o ['clienteNuevo']. */
  prefijo?: string[]
  /** Con RUC: un solo campo, la razón social. */
  esEmpresa: boolean
  /** Con DNI el primer apellido es obligatorio; con otro documento puede faltar. */
  pideApellido: boolean
  /** Lo que pasó con la consulta del documento, bajo el primer campo. */
  ayuda?: ReactNode
}

export type NombrePorPartes = {
  nombreCompleto?: string
  nombres?: string
  apellidoPaterno?: string
  apellidoMaterno?: string
}

const limpio = (texto?: string) => texto?.trim().replaceAll(/\s+/g, ' ') || null

/**
 * El nombre que se guarda: una persona une nombres, primer y segundo apellido, en ese
 * orden; una empresa usa su razón social. El backend aplica la misma regla.
 */
export function nombreDelCliente(campos: NombrePorPartes, esEmpresa: boolean) {
  if (esEmpresa) {
    return { nombreCompleto: limpio(campos.nombreCompleto) ?? '', nombres: null, apellidoPaterno: null, apellidoMaterno: null }
  }
  const nombres = limpio(campos.nombres)
  const apellidoPaterno = limpio(campos.apellidoPaterno)
  const apellidoMaterno = limpio(campos.apellidoMaterno)
  return {
    nombreCompleto: [nombres, apellidoPaterno, apellidoMaterno].filter(Boolean).join(' '),
    nombres,
    apellidoPaterno,
    apellidoMaterno,
  }
}

/** Nombres, primer y segundo apellido de una persona, como en su DNI; o la razón social de una empresa. */
export function CamposNombreCliente({ prefijo = [], esEmpresa, pideApellido, ayuda }: Readonly<Props>) {
  if (esEmpresa) {
    return (
      <Form.Item
        label="Nombre o razón social"
        name={[...prefijo, 'nombreCompleto']}
        extra={ayuda}
        rules={[{ required: true, whitespace: true, message: 'Escribe la razón social' }]}
      >
        <Input maxLength={200} />
      </Form.Item>
    )
  }

  return (
    <>
      <Form.Item
        label="Nombres"
        name={[...prefijo, 'nombres']}
        extra={ayuda}
        rules={[{ required: true, whitespace: true, message: 'Escribe los nombres' }]}
      >
        <Input maxLength={100} />
      </Form.Item>
      <Form.Item
        label="Primer apellido"
        name={[...prefijo, 'apellidoPaterno']}
        rules={pideApellido ? [{ required: true, whitespace: true, message: 'Escribe el primer apellido' }] : []}
      >
        <Input maxLength={60} />
      </Form.Item>
      <Form.Item label="Segundo apellido" name={[...prefijo, 'apellidoMaterno']}>
        <Input maxLength={60} />
      </Form.Item>
    </>
  )
}
