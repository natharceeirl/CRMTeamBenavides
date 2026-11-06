import { useState } from 'react'
import { AutoComplete, Button, Checkbox, Form, Input, InputNumber, Modal, Select } from 'antd'
import {
  TIPO_PRODUCTO,
  useAltaRapidaCategoria,
  useCategoriasProducto,
  useGuardarProducto,
  useMarcasProductos,
} from '../api/inventario'
import type { MonedaPrecio, ProductoResponse } from '../api/tipos'
import { useSesion } from '../auth/sesion'
import { PERMISOS } from '../auth/acceso'
import { AvisoError } from './AvisoError'
import { CamposPrecio } from './CamposPrecio'
import { MONEDA_PRECIO } from '../utils/precios'

type Props = {
  abierto: boolean
  producto?: ProductoResponse | null
  /** Un modelo de moto nace sin stock: cada moto entra con su serie. */
  moto?: boolean
  onCerrar: () => void
}

type Campos = {
  categoriaId: string
  codigo: string
  nombre: string
  descripcion?: string
  unidad?: string
  precioVenta: number
  monedaPrecio: MonedaPrecio
  precioIncluyeIgv: boolean
  costo?: number
  monedaCosto: MonedaPrecio
  costoIncluyeIgv: boolean
  stockInicial?: number
  stockMinimo?: number | null
  marca?: string
  fotoUrl?: string
  /** Al editar: un repuesto sin stock pasa a ser modelo de moto, o al revés. */
  esModeloMoto?: boolean
}

const sinVacios = (valor?: string) => (valor && valor.trim() !== '' ? valor.trim() : null)

export function ModalProducto({ abierto, producto, moto = false, onCerrar }: Readonly<Props>) {
  const [formulario] = Form.useForm<Campos>()
  const categorias = useCategoriasProducto()
  const marcas = useMarcasProductos()
  const guardar = useGuardarProducto()
  const nuevaCategoria = useAltaRapidaCategoria()
  const { tienePermiso } = useSesion()
  const puedeCrearCategoria = tienePermiso(PERMISOS.inventarioCrear)
  const [creandoCategoria, setCreandoCategoria] = useState(false)
  const fotoUrl = Form.useWatch('fotoUrl', formulario)

  const crearCategoria = async (nombre: string) => {
    if (!nombre.trim()) return
    const categoria = await nuevaCategoria.mutateAsync(nombre.trim())
    formulario.setFieldsValue({ categoriaId: categoria.id })
    setCreandoCategoria(false)
  }

  const editando = Boolean(producto)
  const esModeloMoto = producto ? producto.tipoId === TIPO_PRODUCTO.vehiculo : moto

  const cerrar = () => {
    guardar.reset()
    nuevaCategoria.reset()
    setCreandoCategoria(false)
    onCerrar()
  }

  const enviar = async (campos: Campos) => {
    const comunes = {
      categoriaId: campos.categoriaId,
      codigo: campos.codigo.trim(),
      nombre: campos.nombre.trim(),
      descripcion: sinVacios(campos.descripcion),
      unidad: sinVacios(campos.unidad),
      precioVenta: campos.precioVenta,
      monedaPrecio: campos.monedaPrecio,
      precioIncluyeIgv: campos.precioIncluyeIgv,
      costo: campos.costo ?? 0,
      monedaCosto: campos.monedaCosto,
      costoIncluyeIgv: campos.costoIncluyeIgv,
      stockMinimo: campos.stockMinimo != null && !Number.isNaN(campos.stockMinimo) ? campos.stockMinimo : null,
      marca: sinVacios(campos.marca),
      fotoUrl: sinVacios(campos.fotoUrl),
    }

    // El stock inicial solo existe al crear: después se mueve por entradas,
    // salidas o ajustes, que es lo que deja rastro en el kardex.
    await guardar.mutateAsync({
      id: producto?.id,
      datos: producto
        ? {
            ...comunes,
            // Sin la casilla (hay stock), el tipo se queda como está.
            tipo:
              campos.esModeloMoto === undefined
                ? (producto.tipoId as 0 | 1)
                : campos.esModeloMoto
                  ? TIPO_PRODUCTO.vehiculo
                  : TIPO_PRODUCTO.repuesto,
          }
        : esModeloMoto
          ? { ...comunes, stockInicial: 0, tipo: TIPO_PRODUCTO.vehiculo }
          : { ...comunes, stockInicial: campos.stockInicial ?? 0 },
    })

    cerrar()
  }

  return (
    <Modal
      title={
        esModeloMoto
          ? editando
            ? 'Editar modelo de moto'
            : 'Nuevo modelo de moto'
          : editando
            ? 'Editar producto'
            : 'Registrar producto'
      }
      open={abierto}
      onCancel={cerrar}
      onOk={() => formulario.submit()}
      okText="Guardar"
      cancelText="Cancelar"
      confirmLoading={guardar.isPending}
      destroyOnHidden
    >
      <AvisoError error={guardar.error ?? nuevaCategoria.error} />
      <Form<Campos>
        form={formulario}
        layout="vertical"
        requiredMark={false}
        onFinish={enviar}
        initialValues={{
          categoriaId: producto?.categoriaId,
          codigo: producto?.codigo ?? '',
          nombre: producto?.nombre ?? '',
          descripcion: producto?.descripcion ?? '',
          unidad: producto?.unidad ?? 'unidad',
          precioVenta: producto?.precioVenta ?? 0,
          // Un repuesto nuevo va con IGV incluido: así maneja sus precios el taller.
          monedaPrecio: producto?.monedaPrecio ?? MONEDA_PRECIO.pen,
          precioIncluyeIgv: producto?.precioIncluyeIgv ?? true,
          // El costo se guarda en soles y sin IGV: así se muestra al editar.
          costo: producto?.costo ?? 0,
          monedaCosto: MONEDA_PRECIO.pen,
          costoIncluyeIgv: false,
          stockInicial: 0,
          esModeloMoto: producto?.tipoId === TIPO_PRODUCTO.vehiculo,
          stockMinimo: producto?.stockMinimo ?? undefined,
          marca: producto?.marca ?? '',
          fotoUrl: producto?.fotoUrl ?? '',
        }}
      >
        <Form.Item
          label="Categoría"
          name="categoriaId"
          rules={[{ required: true, message: 'Elige la categoría' }]}
          extra={
            puedeCrearCategoria &&
            (creandoCategoria ? (
              <Input.Search
                autoFocus
                placeholder="Nombre de la categoría nueva"
                enterButton="Crear"
                loading={nuevaCategoria.isPending}
                onSearch={crearCategoria}
                style={{ marginTop: 8 }}
              />
            ) : (
              <Button type="link" size="small" style={{ paddingInline: 0 }} onClick={() => setCreandoCategoria(true)}>
                ¿No está? Crear categoría
              </Button>
            ))
          }
        >
          <Select
            showSearch={{ optionFilterProp: 'label' }}
            loading={categorias.isPending}
            placeholder="Lubricantes, frenos, filtros…"
            options={(categorias.data ?? []).map((categoria) => ({
              value: categoria.id,
              label: categoria.nombre,
            }))}
          />
        </Form.Item>
        <Form.Item label="Código" name="codigo" rules={[{ required: true, message: 'Ingresa el código' }]}>
          <Input placeholder="REP-0001" />
        </Form.Item>
        <Form.Item label="Nombre" name="nombre" rules={[{ required: true, message: 'Ingresa el nombre' }]}>
          <Input />
        </Form.Item>
        <Form.Item label="Marca" name="marca">
          <AutoComplete
            placeholder="Yamaha, Motul, NGK…"
            options={(marcas.data ?? []).map((marca) => ({ value: marca }))}
            filterOption={(texto, opcion) => (opcion?.value ?? '').toLowerCase().includes(texto.toLowerCase())}
          />
        </Form.Item>
        <Form.Item
          label="Foto"
          name="fotoUrl"
          rules={[{ type: 'url', message: 'Pega el enlace completo de la imagen, con https://' }]}
          extra="Enlace a una imagen del repuesto, por ejemplo del catálogo del proveedor."
        >
          <Input placeholder="https://…" />
        </Form.Item>
        {fotoUrl && /^https?:\/\//.test(fotoUrl) && (
          <img src={fotoUrl} alt="Foto del repuesto" className="foto-repuesto" />
        )}
        <Form.Item label="Descripción" name="descripcion">
          <Input.TextArea rows={2} />
        </Form.Item>
        <Form.Item label="Unidad" name="unidad">
          <Input placeholder="unidad, litro, juego…" />
        </Form.Item>
        <CamposPrecio
          etiqueta="Precio de venta"
          nombreMonto="precioVenta"
          nombreMoneda="monedaPrecio"
          nombreIncluyeIgv="precioIncluyeIgv"
          ayudaDolares="Se cobra en soles al tipo de cambio del día."
        />
        <CamposPrecio
          etiqueta="Costo"
          nombreMonto="costo"
          nombreMoneda="monedaCosto"
          nombreIncluyeIgv="costoIncluyeIgv"
          ayudaDolares="Se guarda en soles al tipo de cambio de hoy."
        />
        {!editando && !esModeloMoto && (
          <Form.Item label="Stock inicial" name="stockInicial">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        )}
        {/* El tipo cambia solo sin stock: el stock de una moto son sus series. */}
        {editando && producto?.stockActual === 0 && (
          <Form.Item name="esModeloMoto" valuePropName="checked" extra="Sus motos entran por Compras, cada una con su serie.">
            <Checkbox>Es un modelo de moto</Checkbox>
          </Form.Item>
        )}
        <Form.Item
          label="Stock mínimo"
          name="stockMinimo"
          help="Opcional. Si se deja vacío, tomará el stock mínimo de la categoría o 4 por defecto."
        >
          <InputNumber min={0} style={{ width: '100%' }} placeholder="Hereda categoría o 4" />
        </Form.Item>
      </Form>
    </Modal>
  )
}
