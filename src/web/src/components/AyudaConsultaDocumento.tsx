import type { UseQueryResult } from '@tanstack/react-query'
import type { ConsultaDocumentoResponse } from '../api/tipos'

type Props = {
  consulta: UseQueryResult<ConsultaDocumentoResponse>
  /** El formulario de un cliente avisa si ese documento ya es de otro cliente. */
  avisarClienteExistente?: boolean
}

/** Lo que pasó con la consulta del DNI o RUC, debajo del campo del documento. */
export function AyudaConsultaDocumento({ consulta, avisarClienteExistente = true }: Readonly<Props>) {
  if (consulta.fetchStatus === 'idle' && consulta.status === 'pending') return null

  if (consulta.isFetching) {
    return <span className="texto-secundario">Buscando en RENIEC y SUNAT…</span>
  }

  if (consulta.isError) {
    // No encontrado, sin cuota o sin respuesta: se escribe a mano.
    return <span className="texto-secundario">{consulta.error.message}</span>
  }

  const datos = consulta.data
  if (!datos) return null

  if (avisarClienteExistente && datos.clienteExistenteNombre) {
    return (
      <span className="aviso-precio" style={{ display: 'block' }}>
        Ese documento ya es del cliente {datos.clienteExistenteNombre}.
      </span>
    )
  }

  if (datos.tipoDocumentoId === 1) {
    const estado = [datos.estado, datos.condicion].filter(Boolean).join(' · ')
    return <span className="texto-secundario">Datos de SUNAT{estado ? ` (${estado})` : ''}. Revísalos antes de guardar.</span>
  }

  return <span className="texto-secundario">Datos de RENIEC. Revísalos antes de guardar.</span>
}
