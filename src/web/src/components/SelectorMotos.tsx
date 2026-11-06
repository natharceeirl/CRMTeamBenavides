import { Select } from 'antd'
import { ESTADO_MOTO, useMotos } from '../api/motos'

type Props = {
  productoId?: string
  value?: string[]
  onChange?: (ids: string[]) => void
}

/**
 * Elige la moto exacta por su serie, entre las del modelo que están en stock. Una
 * cotización no la aparta: al confirmar la venta se comprueba que siga disponible.
 */
export function SelectorMotos({ productoId, value, onChange }: Readonly<Props>) {
  const motos = useMotos({ productoId, estado: ESTADO_MOTO.enStock }, Boolean(productoId))

  return (
    <Select<string[]>
      mode="multiple"
      aria-label="Motos por serie"
      showSearch={{ optionFilterProp: 'label' }}
      loading={motos.isPending}
      placeholder="Elige la moto por su serie"
      notFoundContent="No hay motos de este modelo en stock"
      value={value}
      onChange={(ids) => onChange?.(ids)}
      options={(motos.data ?? []).map((moto) => ({
        value: moto.id,
        label: [moto.numeroSerieVIN, moto.color, moto.anio].filter(Boolean).join(' · '),
      }))}
      style={{ minWidth: 260 }}
    />
  )
}
