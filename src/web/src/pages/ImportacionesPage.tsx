import { useState } from 'react'
import { Alert, Button, Popconfirm, Select, Space, Table, Upload, type TableProps } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { EtiquetaEstado } from '../components/EtiquetaEstado'
import { useCategoriasProducto } from '../api/inventario'
import {
  NOMBRE_PLANTILLA_IMPORTACION,
  RUTA_PLANTILLA_IMPORTACION,
  describirCantidades,
  problemaDelArchivo,
  textoImportados,
  useEjecutarImportacion,
  useHistorialImportaciones,
  useValidarImportacion,
} from '../api/importaciones'
import type {
  ErrorFilaImportacion,
  HistorialImportacionItem,
  ResultadoImportacionExcel,
  ResumenHojaImportacion,
} from '../api/tipos'
import { descargarArchivo } from '../utils/descarga'
import { fechaHora } from '../utils/formato'

const TIPOS_EXCEL = '.xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'

type FilaResumen = ResumenHojaImportacion & { hoja: string }
type FilaError = ErrorFilaImportacion & { clave: number }

const columnasResumen = (importado: boolean): TableProps<FilaResumen>['columns'] => [
  { title: 'Hoja', dataIndex: 'hoja' },
  { title: 'Filas leídas', dataIndex: 'totalFilasLeidas', align: 'right', className: 'num' },
  { title: 'Válidas', dataIndex: 'filasValidas', align: 'right', className: 'num' },
  { title: 'Con error', dataIndex: 'filasConError', align: 'right', className: 'num' },
  ...(importado ? [{ title: 'Creados', dataIndex: 'registrosCreados', align: 'right' as const, className: 'num' }] : []),
]

const columnasErrores: TableProps<FilaError>['columns'] = [
  { title: 'Hoja', dataIndex: 'hoja' },
  // La fila 0 es un problema del archivo o de la hoja entera.
  { title: 'Fila', dataIndex: 'fila', className: 'num', render: (fila: number) => (fila > 0 ? fila : '—') },
  { title: 'Columna', dataIndex: 'columna' },
  { title: 'Valor leído', dataIndex: 'valorLeido', render: (valor: string | null) => valor || '—' },
  { title: 'Problema', dataIndex: 'mensaje' },
]

const columnasHistorial: TableProps<HistorialImportacionItem>['columns'] = [
  { title: 'Fecha', dataIndex: 'fecha', className: 'num', render: (fecha: string) => fechaHora(fecha) },
  { title: 'Usuario', dataIndex: 'usuarioNombre', render: (nombre: string | null) => nombre ?? '—' },
  { title: 'Archivo', dataIndex: 'nombreArchivo' },
  {
    title: 'Resultado',
    dataIndex: 'exitoso',
    render: (exitoso: boolean) =>
      exitoso ? <EtiquetaEstado tono="hecho">Importado</EtiquetaEstado> : <EtiquetaEstado tono="suave">Rechazado</EtiquetaEstado>,
  },
  { title: 'Clientes', dataIndex: 'clientesProcesados', align: 'right', className: 'num' },
  { title: 'Productos', dataIndex: 'catalogoProcesados', align: 'right', className: 'num' },
  { title: 'Errores', dataIndex: 'totalErrores', align: 'right', className: 'num' },
]

function AvisoResultado({ resultado }: Readonly<{ resultado: ResultadoImportacionExcel }>) {
  if (!resultado.exitoso) {
    const errores = resultado.errores.length
    const cantidad = `${errores} ${errores === 1 ? 'error' : 'errores'}`
    const mensaje = resultado.soloValidacion
      ? `El archivo tiene ${cantidad}. No se importa nada hasta corregirlos en el Excel y volver a validarlo.`
      : `No se importó ningún registro: hay ${cantidad}.`
    return <Alert type="error" showIcon message={mensaje} style={{ marginBottom: 16 }} />
  }

  if (resultado.soloValidacion) {
    const cantidades = describirCantidades(resultado.clientes.filasValidas, resultado.catalogo.filasValidas)
    return <Alert type="success" showIcon message={`El archivo está listo: se crearán ${cantidades}.`} style={{ marginBottom: 16 }} />
  }

  return <Alert type="success" showIcon message={textoImportados(resultado)} style={{ marginBottom: 16 }} />
}

/**
 * Carga inicial de clientes y repuestos desde la plantilla de Excel. Primero se
 * valida sin guardar nada y, si no hay errores, se importa todo junto. Solo con
 * `configuracion.editar`, igual que la API.
 */
export function ImportacionesPage() {
  const [archivo, setArchivo] = useState<File | null>(null)
  const [errorArchivo, setErrorArchivo] = useState<string | null>(null)
  const [categoriaId, setCategoriaId] = useState<string>()
  const [resultado, setResultado] = useState<ResultadoImportacionExcel | null>(null)
  const [hoja, setHoja] = useState<string>()
  const [errorDescarga, setErrorDescarga] = useState<unknown>(null)

  const categorias = useCategoriasProducto()
  const historial = useHistorialImportaciones()
  const validar = useValidarImportacion()
  const ejecutar = useEjecutarImportacion()

  // Otro archivo u otra categoría obligan a validar de nuevo.
  const olvidarResultado = () => {
    setResultado(null)
    setHoja(undefined)
    validar.reset()
    ejecutar.reset()
  }

  const elegirArchivo = (elegido: File) => {
    const problema = problemaDelArchivo(elegido)
    setErrorArchivo(problema)
    setArchivo(problema ? null : elegido)
    olvidarResultado()
    // No se sube sola: espera a «Validar».
    return false
  }

  const quitarArchivo = () => {
    setArchivo(null)
    setErrorArchivo(null)
    olvidarResultado()
  }

  const elegirCategoria = (id: string | undefined) => {
    setCategoriaId(id)
    olvidarResultado()
  }

  const descargarPlantilla = () => {
    setErrorDescarga(null)
    descargarArchivo(RUTA_PLANTILLA_IMPORTACION, NOMBRE_PLANTILLA_IMPORTACION).catch(setErrorDescarga)
  }

  const validarArchivo = () => {
    if (!archivo) return
    olvidarResultado()
    validar.mutate({ archivo, categoriaId }, { onSuccess: setResultado })
  }

  const importar = () => {
    if (!archivo) return
    ejecutar.mutate(
      { archivo, categoriaId },
      {
        onSuccess: (respuesta) => {
          setResultado(respuesta)
          setHoja(undefined)
          // Ya importado: el mismo archivo daría duplicados en todas sus filas.
          if (respuesta.exitoso) setArchivo(null)
        },
      },
    )
  }

  const listoParaImportar = Boolean(resultado?.soloValidacion && resultado.exitoso)
  const importado = Boolean(resultado && !resultado.soloValidacion && resultado.exitoso)
  const faltaCategoria = resultado?.errores.some((error) => error.columna === 'CategoriaId') ?? false
  const aImportar = resultado ? describirCantidades(resultado.clientes.filasValidas, resultado.catalogo.filasValidas) : ''

  const filasResumen: FilaResumen[] = resultado
    ? [
        { hoja: 'Clientes', ...resultado.clientes },
        { hoja: 'Catalogo', ...resultado.catalogo },
      ]
    : []
  const errores: FilaError[] = (resultado?.errores ?? []).map((error, indice) => ({ ...error, clave: indice }))
  const hojasConError = [...new Set(errores.map((error) => error.hoja))]
  const erroresVisibles = hoja ? errores.filter((error) => error.hoja === hoja) : errores

  const opcionesCategoria = (categorias.data ?? [])
    .filter((categoria) => categoria.activo)
    .map((categoria) => ({ value: categoria.id, label: categoria.nombre }))

  return (
    <>
      <BarraSuperior titulo="Importar datos" />
      <div className="pagina">
        <div className="formulario">
          <section className="bloque">
            <h2>Plantilla</h2>
            <p>
              La plantilla trae dos hojas. En Clientes van el tipo de documento (DNI, RUC o CE), el número, el nombre, el
              teléfono, el correo y la dirección; en Catalogo, el código y el nombre de cada repuesto. Los repuestos entran
              con precio, costo y stock en cero y se completan después en Repuestos.
            </p>
            <p className="texto-secundario">
              Con una sola fila con error no se importa nada. Un documento o un código que ya está registrado también cuenta
              como error.
            </p>
            <AvisoError error={errorDescarga} />
            <Button onClick={descargarPlantilla}>Descargar plantilla</Button>
          </section>

          <section className="bloque">
            <h2>Archivo</h2>
            <Space wrap size={12} align="center" style={{ marginBottom: 16 }}>
              <Upload accept={TIPOS_EXCEL} showUploadList={false} beforeUpload={elegirArchivo}>
                <Button>{archivo ? 'Cambiar archivo' : 'Elegir archivo'}</Button>
              </Upload>
              {archivo && (
                <>
                  <span>{archivo.name}</span>
                  <Button type="link" onClick={quitarArchivo} style={{ paddingInline: 0 }}>
                    Quitar
                  </Button>
                </>
              )}
            </Space>
            {errorArchivo && <Alert type="error" showIcon message={errorArchivo} style={{ marginBottom: 16 }} />}
            <div style={{ maxWidth: 420, marginBottom: 20 }}>
              <div className="etiqueta" style={{ marginBottom: 6 }}>
                Categoría de los repuestos nuevos
              </div>
              <Select
                allowClear
                showSearch
                optionFilterProp="label"
                placeholder="Elegir categoría"
                value={categoriaId}
                onChange={elegirCategoria}
                options={opcionesCategoria}
                loading={categorias.isPending}
                status={faltaCategoria ? 'error' : undefined}
                style={{ width: '100%' }}
              />
              <div className="texto-secundario" style={{ marginTop: 6 }}>
                Todos los repuestos de la hoja Catalogo entran en esta categoría. Si la hoja está vacía, no hace falta.
              </div>
            </div>
            <AvisoError error={categorias.error ?? validar.error ?? ejecutar.error} />
            <Space wrap size={12}>
              <Button onClick={validarArchivo} disabled={!archivo} loading={validar.isPending}>
                Validar
              </Button>
              <Popconfirm
                title={`Importar ${aImportar}`}
                description="Los registros se crean todos juntos y no se pueden deshacer desde esta pantalla."
                okText="Importar"
                cancelText="Cancelar"
                onConfirm={importar}
                disabled={!listoParaImportar}
              >
                <Button type="primary" disabled={!listoParaImportar} loading={ejecutar.isPending}>
                  {listoParaImportar ? `Importar ${aImportar}` : 'Importar'}
                </Button>
              </Popconfirm>
            </Space>
          </section>

          {resultado && (
            <section className="bloque">
              <h2>{resultado.soloValidacion ? 'Resultado de la validación' : 'Resultado de la importación'}</h2>
              <AvisoResultado resultado={resultado} />
              <Table
                rowKey="hoja"
                size="small"
                columns={columnasResumen(importado)}
                dataSource={filasResumen}
                pagination={false}
                style={{ marginBottom: errores.length > 0 ? 24 : 0 }}
              />
              {errores.length > 0 && (
                <>
                  {hojasConError.length > 1 && (
                    <div className="filtros">
                      <Select
                        allowClear
                        placeholder="Hoja"
                        value={hoja}
                        onChange={setHoja}
                        options={hojasConError.map((nombre) => ({ value: nombre, label: nombre }))}
                        style={{ width: 200 }}
                      />
                    </div>
                  )}
                  <Table
                    rowKey="clave"
                    columns={columnasErrores}
                    dataSource={erroresVisibles}
                    pagination={{ pageSize: 50, hideOnSinglePage: true, showSizeChanger: false }}
                  />
                </>
              )}
            </section>
          )}

          <section className="bloque">
            <h2>Historial</h2>
            <AvisoError error={historial.error} />
            <Table
              rowKey="id"
              columns={columnasHistorial}
              dataSource={historial.data ?? []}
              pagination={false}
              loading={historial.isPending}
              locale={{ emptyText: 'Todavía no se importó ningún archivo' }}
            />
          </section>
        </div>
      </div>
    </>
  )
}
