import React, { useState } from 'react';
import { Landmark, Printer, Code, Send, Clock, CheckCircle } from 'lucide-react';

export const ShippingPanel: React.FC = () => {
  // AT SOAP State
  const [docNo, setDocNo] = useState('GT 2026/1024');
  const [plate, setPlate] = useState('AA-00-XX');
  const [atCode, setAtCode] = useState<string | null>(null);
  const [atStatus, setAtStatus] = useState<string | null>(null);
  const [soapResponseXml, setSoapResponseXml] = useState<string>('');
  const [submittingAT, setSubmittingAT] = useState(false);

  // Carrier Label State
  const [carrier, setCarrier] = useState('CTT_EXPRESSO_13H');
  const [recipient, setRecipient] = useState('João Silva');
  const [trackingNumber, setTrackingNumber] = useState<string | null>(null);
  const [zplLabel, setZplLabel] = useState<string>('');
  const [generatingLabel, setGeneratingLabel] = useState(false);

  const handleSubmitAT = async () => {
    setSubmittingAT(true);
    setAtCode(null);
    setSoapResponseXml('');
    try {
      const payload = {
        NIFRemetente: '599999990',
        TipoDocumento: 'GT',
        NumeroDocumento: docNo,
        DataEmissao: new Date().toISOString(),
        IdentificacaoViatura: plate,
        LinhasArtigos: [
          {
            Codigo: 'SKU-99023',
            Descricao: 'Snack Proteico de Amendoim',
            Quantidade: 150,
            UnidadeMedida: 'UN'
          }
        ]
      };

      const response = await fetch('/api/logistica/shipping/at-comunicar', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (response.ok) {
        const result = await response.json();
        setAtCode(result.codigoAT);
        setAtStatus(result.estado);
        setSoapResponseXml(result.soapEnvelope);
      } else {
        alert('Falha na comunicação com o WebService da AT.');
      }
    } catch (e) {
      console.error(e);
      alert('Erro de rede ao ligar ao servidor.');
    } finally {
      setSubmittingAT(false);
    }
  };

  const handleGenerateLabel = async () => {
    setGeneratingLabel(true);
    setTrackingNumber(null);
    setZplLabel('');
    try {
      const payload = {
        carrier_service: carrier,
        recipient: {
          name: recipient,
          street: 'Rua Central de Viseu, N 45',
          postal_code: '3500-100',
          city: 'Viseu',
          country: 'PT'
        }
      };

      const response = await fetch('/api/logistica/shipping/carrier-label', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (response.ok) {
        const result = await response.json();
        setTrackingNumber(result.trackingNumber);
        setZplLabel(result.zplLabel);
      } else {
        alert('Erro ao ligar ao servidor da transportadora.');
      }
    } catch (e) {
      console.error(e);
      alert('Erro de comunicação.');
    } finally {
      setGeneratingLabel(false);
    }
  };

  return (
    <section className="tab-pane">
      <div className="pane-header">
        <h2>Expedição e Comunicação Fiscal (Autoridade Tributária)</h2>
        <p>Cumprimento do Regime de Bens em Circulação em Portugal (Decreto-Lei nº 198/2012) via WebService SOAP com a AT e etiquetas térmicas ZPL.</p>
      </div>

      <div className="grid-layout">
        <div className="glass-card">
          <h3>
            <Landmark /> Comunicação SOAP à AT (Finanças)
          </h3>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', fontSize: '13px', background: 'rgba(0,0,0,0.2)', padding: '12px', borderRadius: '8px', border: '1px solid var(--border-color)', marginBottom: '16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Clock size={14} style={{ color: 'var(--color-success)' }} />
              <span>NTP Clock Server (<code>hora.oal.ul.pt</code>): <span className="badge badge-success">Sincronizado</span></span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <CheckCircle size={14} style={{ color: 'var(--color-success)' }} />
              <span>Certificado SSL AT (<code>Assinado_AT_Prod_2026.pfx</code>): <span className="badge badge-success">Instalado</span></span>
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="shipping-doc-no">Número de Guia de Transporte:</label>
            <input
              id="shipping-doc-no"
              type="text"
              value={docNo}
              onChange={(e) => setDocNo(e.target.value)}
            />
          </div>

          <div className="form-group">
            <label htmlFor="shipping-plate">Matrícula do Veículo Transportador:</label>
            <input
              id="shipping-plate"
              type="text"
              value={plate}
              onChange={(e) => setPlate(e.target.value)}
            />
          </div>

          <button
            className="btn btn-primary full-width-btn"
            onClick={handleSubmitAT}
            disabled={submittingAT}
            style={{ marginTop: '10px' }}
          >
            <Send size={16} /> Submeter Guia e Validar SOAP
          </button>

          {atCode && (
            <div className="at-response-box">
              <h4>Código Oficial Concedido pela AT:</h4>
              <div className="at-code">{atCode}</div>
              <span className="badge badge-success">{atStatus}</span>
            </div>
          )}
        </div>

        <div className="glass-card">
          <h3>
            <Code /> Payload XML SOAP Gerado e Transmitido
          </h3>
          
          <pre className="xml-viewer" style={{ height: '310px', maxHeight: '310px' }}>
            {soapResponseXml || 
`<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:doc="https://servicos.portaldasfinancas.gov.pt/sgdtws/documentosTransporte">
   <soapenv:Header />
   <soapenv:Body>
      <!-- Submeta a guia para visualizar o payload SOAP de resposta recebido da AT -->
   </soapenv:Body>
</soapenv:Envelope>`}
          </pre>
        </div>

        <div className="glass-card full-width">
          <h3>
            <Printer /> Geração de Etiquetas e Tracking da Transportadora
          </h3>

          <div className="carrier-generator-grid">
            <div className="carrier-form" style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              <div className="form-group">
                <label htmlFor="carrier-select">Serviço Expresso de Encomendas:</label>
                <select
                  id="carrier-select"
                  value={carrier}
                  onChange={(e) => setCarrier(e.target.value)}
                >
                  <option value="CTT_EXPRESSO_13H">CTT Expresso - 13H (Dia Seguinte)</option>
                  <option value="DPD_CLASSIC">DPD - Classic Road</option>
                  <option value="DHL_EXPRESS_WORLD">DHL - Express Worldwide</option>
                </select>
              </div>

              <div className="form-group">
                <label htmlFor="recipient-name">Nome do Destinatário:</label>
                <input
                  id="recipient-name"
                  type="text"
                  value={recipient}
                  onChange={(e) => setRecipient(e.target.value)}
                />
              </div>

              <button
                className="btn btn-secondary"
                onClick={handleGenerateLabel}
                disabled={generatingLabel}
                style={{ width: '100%' }}
              >
                <Printer size={16} /> Gerar Tracking e Etiqueta
              </button>

              {trackingNumber && (
                <div style={{ background: 'rgba(0,0,0,0.15)', padding: '12px', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
                  <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>CÓDIGO DE TRACKING</div>
                  <div style={{ fontSize: '18px', fontWeight: 700, marginTop: '4px', letterSpacing: '1px' }}>{trackingNumber}</div>
                </div>
              )}
            </div>

            <div className="zpl-output-box">
              <label>Código ZPL da Impressora Térmica (Zebra 300DPI):</label>
              <pre style={{ height: '220px', minHeight: '220px' }}>
                {zplLabel || 
`^XA
^CF0,30
^FO50,50^FDZPL Labels Output^FS
^XZ`}
              </pre>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
};
