import { useState } from 'react'
import {
  Alert,
  Button,
  Card,
  Descriptions,
  Empty,
  Input,
  List,
  Modal,
  Space,
  Spin,
  Tag,
  Typography,
} from 'antd'
import { useYamahaMockConsulta } from '../api/yamaha'

const { Text } = Typography

type Props = {
  abierto: boolean
  criterioInicial?: string | null
  onCerrar: () => void
}

const EJEMPLOS_MOCK = [
  { etiqueta: 'MT-03', valor: 'MT-03' },
  { etiqueta: 'MT-07', valor: 'MT-07' },
  { etiqueta: 'FZ-25', valor: 'FZ-25' },
  { etiqueta: 'NMAX 155', valor: 'NMAX-155' },
  { etiqueta: 'WaveRunner FX', valor: 'WAVERUNNER-FX' },
  { etiqueta: 'Generador EF2000iS', valor: 'GEN-EF2000IS' },
]

export function ModalYamahaMock({ abierto, criterioInicial, onCerrar }: Readonly<Props>) {
  const [criterio, setCriterio] = useState(criterioInicial ?? 'MT-03')
  const [criterioConsultado, setCriterioConsultado] = useState(criterioInicial ?? 'MT-03')

  const query = useYamahaMockConsulta(abierto ? criterioConsultado : null)
  const datos = query.data

  const handleBuscar = () => {
    if (criterio.trim()) {
      setCriterioConsultado(criterio.trim())
    }
  }

  const handleSeleccionarEjemplo = (valor: string) => {
    setCriterio(valor)
    setCriterioConsultado(valor)
  }

  return (
    <Modal
      title={
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span>Catálogo Técnico Yamaha</span>
          <Tag color="volcano">MOCK / DEMO</Tag>
        </div>
      }
      open={abierto}
      onCancel={onCerrar}
      footer={[
        <Button key="cerrar" onClick={onCerrar}>
          Cerrar
        </Button>,
      ]}
      width={800}
      destroyOnHidden
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        {/* Banner de aviso legal obligatorio */}
        <Alert
          type="warning"
          showIcon
          message="Ambiente de Simulación (Mock)"
          description="Esta herramienta consulta un catálogo técnico local simulado. No realiza conexiones externas ni utiliza credenciales de Yamaha Motor Corporation."
        />

        {/* Buscador */}
        <div style={{ display: 'flex', gap: 8 }}>
          <Input
            placeholder="Ingrese VIN, número de serie o modelo (ej. MT-03, FZ-25, JYARN07...)"
            value={criterio}
            onChange={(e) => setCriterio(e.target.value)}
            onPressEnter={handleBuscar}
            allowClear
          />
          <Button type="primary" onClick={handleBuscar} loading={query.isFetching}>
            Consultar Mock
          </Button>
        </div>

        {/* Chips de ejemplos rápidos */}
        <div>
          <Text type="secondary" style={{ fontSize: 12, marginRight: 8 }}>
            Pruebas rápidas:
          </Text>
          <Space wrap size={[4, 8]}>
            {EJEMPLOS_MOCK.map((ej) => (
              <Tag
                key={ej.valor}
                style={{ cursor: 'pointer' }}
                color={criterioConsultado === ej.valor ? 'blue' : 'default'}
                onClick={() => handleSeleccionarEjemplo(ej.valor)}
              >
                {ej.etiqueta}
              </Tag>
            ))}
          </Space>
        </div>

        {/* Loading */}
        {query.isFetching && (
          <div style={{ textAlign: 'center', padding: 40 }}>
            <Spin description="Consultando catálogo mock..." />
          </div>
        )}

        {/* Error o no encontrado */}
        {!query.isFetching && query.isError && (
          <div style={{ padding: 20 }}>
            <Empty
              description={`No se encontraron datos simulados para «${criterioConsultado}». Pruebe con otro modelo o VIN.`}
            />
          </div>
        )}

        {/* Resultados */}
        {!query.isFetching && datos && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <Card
              size="small"
              title={
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span>{datos.nombreComercial}</span>
                  <Tag color="blue">{datos.categoria}</Tag>
                </div>
              }
            >
              <Descriptions size="small" column={2}>
                <Descriptions.Item label="Código Modelo">{datos.codigoModelo}</Descriptions.Item>
                <Descriptions.Item label="Año Fabricación">{datos.anioFabricacion ?? 'N/D'}</Descriptions.Item>
                {datos.vinEjemplo && (
                  <Descriptions.Item label="VIN de Referencia">{datos.vinEjemplo}</Descriptions.Item>
                )}
                {datos.numeroMotorEjemplo && (
                  <Descriptions.Item label="Motor de Referencia">{datos.numeroMotorEjemplo}</Descriptions.Item>
                )}
                <Descriptions.Item label="Garantía de Fábrica" span={2}>
                  <Tag color="green">{datos.estadoGarantia}</Tag>
                </Descriptions.Item>
              </Descriptions>
            </Card>

            {/* Especificaciones */}
            <Card size="small" title="Especificaciones Técnicas (Mock)">
              <Descriptions size="small" column={2}>
                <Descriptions.Item label="Tipo de Motor" span={2}>
                  {datos.especificaciones.motorTipo}
                </Descriptions.Item>
                <Descriptions.Item label="Cilindrada">{datos.especificaciones.cilindradaCc} cc</Descriptions.Item>
                <Descriptions.Item label="Potencia">{datos.especificaciones.potenciaHp} HP</Descriptions.Item>
                <Descriptions.Item label="Torque">{datos.especificaciones.torqueNm} Nm</Descriptions.Item>
                <Descriptions.Item label="Refrigeración">{datos.especificaciones.refrigeracion}</Descriptions.Item>
                <Descriptions.Item label="Cap. Tanque">{datos.especificaciones.capacidadTanque}</Descriptions.Item>
                <Descriptions.Item label="Aceite Motor">{datos.especificaciones.capacidadAceiteMotor}</Descriptions.Item>
                <Descriptions.Item label="Bujía Sugerida">{datos.especificaciones.tipoBujiaRecomendada}</Descriptions.Item>
                <Descriptions.Item label="Presión Neumáticos">{datos.especificaciones.presionNeumaticos}</Descriptions.Item>
              </Descriptions>
            </Card>

            {/* Campañas y Mantenimientos */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
              <Card size="small" title="Campañas Técnicas (Recalls)">
                {datos.campaniasServicio.length === 0 ? (
                  <Text type="secondary" style={{ fontSize: 12 }}>
                    Sin campañas pendientes registradas.
                  </Text>
                ) : (
                  <List
                    size="small"
                    dataSource={datos.campaniasServicio}
                    renderItem={(item) => (
                      <List.Item style={{ fontSize: 12, padding: '4px 0' }}>
                        • {item}
                      </List.Item>
                    )}
                  />
                )}
              </Card>

              <Card size="small" title="Plan de Mantenimiento Sugerido">
                <List
                  size="small"
                  dataSource={datos.intervalosMantenimiento}
                  renderItem={(item) => (
                    <List.Item style={{ fontSize: 12, padding: '4px 0' }}>
                      • {item}
                    </List.Item>
                  )}
                />
              </Card>
            </div>
          </div>
        )}
      </div>
    </Modal>
  )
}
