import { useState } from 'react'
import { Alert, Button, Popconfirm, Radio, Select, Space, Table, Upload, type TableProps } from 'antd'
import { BarraSuperior } from '../components/BarraSuperior'
import { AvisoError } from '../components/AvisoError'
import { EtiquetaEstado } from '../components/EtiquetaEstado'
import { useCategoriasProducto } from '../api/inventario'
import {
  NOMBRE_PLANTILLA_IMPORTACION,
  RUTA_PLANTILLA_IMPORTACION,
  cantidadesPorImportar,
  describirCantidades,
  hojasDelResultado,
  problemaDelArchivo,
  repuestosDelHistorial,
  textoImportados,
  textoPorImportar,
  totalOmitidos,
  useEjecutarImportacion,
  useHistorialImportaciones,
  useValidarImportacion,
} from '../api/importaciones'
import {
  MODO_DUPLICADOS,
  type ErrorFilaImportacion,
  type HistorialImportacionItem,
  type ModoDuplicados,
  type ResultadoImportacionExcel,
  type ResumenHojaImportacion,
} from '../api/tipos'
import { descargarArchivo } from '../utils/descarga'
import { fechaHora } from '../utils/formato'

const TIPOS_EXCEL = '.xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'

type FilaResumen = ResumenHojaImportacion & { hoja: string }
type FilaError = ErrorFilaImportacion & { clave: number }

const columnasResumen = (importado: boolean, conOmitidos: boolean): TableProps<FilaResumen>['columns'] => [
  { title: 'Hoja', dataIndex: 'hoja' },
  { title: 'Filas leídas', dataIndex: 'totalFilasLeidas', align: 'right', className: 'num' },
  { title: importado ? 'Nuevas' : 'Por crear', dataIndex: 'filasValidas', align: 'right', className: 'num' },
  ...(conOmitidos
    ? [{ title: 'Ya registradas', dataIndex: 'registrosOmitidos', align: 'right' as const, className: 'num' }]
    : []),
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
  {
    title: 'Ya registrados',
    dataIndex: 'modoDuplicados',
    render: (modo: ModoDuplicados | null) => (modo === MODO_DUPLICADOS.omitir ? 'Se omitieron' : 'Error'),
  },
  { title: 'Clientes', dataIndex: 'clientesProcesados', align: 'right', className: 'num' },
  { title: 'Unidades', dataIndex: 'unidadesProcesadas', align: 'right', className: 'num', render: (valor?: number) => valor ?? 0 },
  { title: 'Repuestos', key: 'repuestos', align: 'right', className: 'num', render: (_, item) => repuestosDelHistorial(item) },
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

  const mensaje = resultado.soloValidacion ? textoPorImportar(resultado) : textoImportados(resultado)
  return <Alert type="success" showIcon message={mensaje} style={{ marginBottom: 16 }} />
}

/**
 * Carga inicial de clientes, unidades y repuestos desde la plantilla de Excel.
 * Primero se valida sin guardar nada y, si no hay errores, se importa todo junto.
 * Solo con `configuracion.editar`, igual que la API.
 */
export function ImportacionesPage() {
  const [archivo, setArchivo] = useState<File | null>(null)
  const [errorArchivo, setErrorArchivo] = useState<string | null>(null)
  const [modoDuplicados, setModoDuplicados] = useState<ModoDuplicados>(MODO_DUPLICADOS.error)
  const [categoriaId, setCategoriaId] = useState<string>()
  const [resultado, setResultado] = useState<ResultadoImportacionExcel | null>(null)
  const [hoja, setHoja] = useState<string>()
  const [errorDescarga, setErrorDescarga] = useState<unknown>(null)

  const categorias = useCategoriasProducto()
  const historial = useHistorialImportaciones()
  const validar = useValidarImportacion()
  const ejecutar = useEjecutarImportacion()

  // Otro archivo, otra categoría u otra regla para lo ya registrado obligan a validar de nuevo.
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
    setCategoriaId(undefined)
    olvidarResultado()
    // No se sube sola: espera a «Validar».
    return false
  }

  const quitarArchivo = () => {
    setArchivo(null)
    setErrorArchivo(null)
    setCategoriaId(undefined)
    olvidarResultado()
  }

  const elegirModo = (modo: ModoDuplicados) => {
    setModoDuplicados(modo)
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
    validar.mutate({ archivo, categoriaId, modoDuplicados }, { onSuccess: setResultado })
  }

  const importar = () => {
    if (!archivo) return
    ejecutar.mutate(
      { archivo, categoriaId, modoDuplicados },
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

  const aImportar = resultado ? describirCantidades(cantidadesPorImportar(resultado)) : ''
  // Con todo ya registrado y omitido, importar no crearía nada.
  const listoParaImportar = Boolean(resultado?.soloValidacion && resultado.exitoso && aImportar)
  const importado = Boolean(resultado && !resultado.soloValidacion && resultado.exitoso)
  // La categoría solo se pide para la hoja Catalogo de la plantilla anterior; la hoja Repuestos trae la suya.
  const faltaCategoria = resultado?.errores.some((error) => error.columna === 'CategoriaId') ?? false
  const pideCategoria = faltaCategoria || Boolean(categoriaId)

  const filasResumen: FilaResumen[] = resultado ? hojasDelResultado(resultado) : []
  const conOmitidos = Boolean(resultado && (modoDuplicados === MODO_DUPLICADOS.omitir || totalOmitidos(resultado) > 0))
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
            <p>La plantilla trae tres hojas. Clientes es obligatoria y debe ir con Unidades, con Repuestos o con las dos.</p>
            <ul>
              <li>
                <strong>Clientes:</strong> tipo de documento (DNI, RUC o CE), número, nombre, teléfono, correo y dirección.
              </li>
              <li>
                <strong>Unidades:</strong> documento del propietario, que debe estar en la hoja Clientes o ya registrado;
                tipo (Motocicleta, Cuatrimoto, MotoAcuatica, Generador u Otro); placa, obligatoria en las motocicletas;
                serie o VIN, obligatoria si no hay placa; motor, marca, modelo, año y medidor en kilómetros u horas con su
                lectura.
              </li>
              <li>
                <strong>Repuestos:</strong> código, nombre, categoría, unidad de medida, costo, precio de venta, stock actual,
                stock mínimo (opcional) y marca (opcional). Si la categoría no existe, se crea.
              </li>
            </ul>
            <p className="texto-secundario">
              Con una sola fila con error no se importa nada. Un documento, una placa, una serie o un código repetido dentro
              del mismo archivo siempre es un error.
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

            <div style={{ marginBottom: 20 }}>
              <div className="etiqueta" style={{ marginBottom: 6 }}>
                Si un cliente, una unidad o un repuesto ya está registrado
              </div>
              <Radio.Group value={modoDuplicados} onChange={(evento) => elegirModo(evento.target.value as ModoDuplicados)}>
                <Space direction="vertical" size={4}>
                  <Radio value={MODO_DUPLICADOS.error}>Marcarlo como error y no importar nada</Radio>
                  <Radio value={MODO_DUPLICADOS.omitir}>Saltarlo e importar solo lo nuevo</Radio>
                </Space>
              </Radio.Group>
              <div className="texto-secundario" style={{ marginTop: 6 }}>
                Lo que se salta no se modifica. Sus unidades nuevas igual quedan a nombre del cliente ya registrado.
              </div>
            </div>

            {pideCategoria && (
              <div style={{ maxWidth: 420, marginBottom: 20 }}>
                <div className="etiqueta" style={{ marginBottom: 6 }}>
                  Categoría de los repuestos de la hoja Catalogo
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
                  status={faltaCategoria && !categoriaId ? 'error' : undefined}
                  style={{ width: '100%' }}
                />
                <div className="texto-secundario" style={{ marginTop: 6 }}>
                  El archivo usa la hoja Catalogo de la plantilla anterior: todos sus repuestos entran en esta categoría.
                  Elegirla y volver a validar.
                </div>
              </div>
            )}

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
                columns={columnasResumen(importado, conOmitidos)}
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
              scroll={{ x: 'max-content' }}
              locale={{ emptyText: 'Todavía no se importó ningún archivo' }}
            />
          </section>
        </div>
      </div>
    </>
  )
}
