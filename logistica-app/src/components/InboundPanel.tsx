import { useState, useEffect } from 'react';
import { Barcode, CheckCircle, Compass, FlaskConical, AlertTriangle, Play } from 'lucide-react';
import { parseGS1Barcode } from '../utils/wmsUtils';
import type { Gs1Result } from '../utils/wmsUtils';

interface EncomendaLinha {
  id: string;
  ref: string;
  designacao: string;
  quantidade: number;
  preco: number;
  lote?: string | null;
  localizacao?: string | null;
}

interface Encomenda {
  id: string;
  documentoNo: number;
  clienteNo: number;
  data: string;
  status: string;
  linhas: EncomendaLinha[];
}

interface PutawaySuggestion {
  localizacaoId: string;
  zona: string;
  corredor: string;
  estante: string;
  prateleira: string;
  alveolo: string;
  maxPesoKg: number;
  maxVolumeM3: number;
  reason: string;
}

export const InboundPanel = () => {
  const [purchaseOrders, setPurchaseOrders] = useState<Encomenda[]>([]);
  const [selectedPO, setSelectedPO] = useState<Encomenda | null>(null);
  const [loading, setLoading] = useState(false);
  const [barcodeInput, setBarcodeInput] = useState('');
  
  // Decoded GS1 Result
  const [gs1Result, setGs1Result] = useState<Gs1Result | null>(null);
  const [putawaySuggestion, setPutawaySuggestion] = useState<PutawaySuggestion | null>(null);
  
  // Local lines tracking for receipt
  const [receivedLines, setReceivedLines] = useState<Record<string, { quantidade: number; lote: string; localizacao: string }>>({});
  const [submitting, setSubmitting] = useState(false);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Fetch pending POs
  const fetchPendingPOs = async () => {
    setLoading(true);
    try {
      const response = await fetch('/api/logistica/rececao/pendentes');
      if (response.ok) {
        const data = await response.json();
        setPurchaseOrders(data);
      }
    } catch (error) {
      console.error('Error fetching purchase orders:', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchPendingPOs();
  }, []);

  // Update local forms when a PO is selected
  useEffect(() => {
    if (selectedPO) {
      const initial: Record<string, { quantidade: number; lote: string; localizacao: string }> = {};
      selectedPO.linhas.forEach(line => {
        initial[line.id] = {
          quantidade: 0,
          lote: '',
          localizacao: 'RECEÇÃO-A1'
        };
      });
      setReceivedLines(initial);
      setGs1Result(null);
      setPutawaySuggestion(null);
      setSuccessMessage(null);
    }
  }, [selectedPO]);

  // Decode GS1 Barcode (Calls API Endpoint to validate parsing)
  const handleDecodeBarcode = async (rawString: string) => {
    if (!rawString.trim()) return;
    
    try {
      const response = await fetch('/api/logistica/gs1/decode', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ rawBarcode: rawString })
      });
      
      if (response.ok) {
        const result: Gs1Result = await response.json();
        setGs1Result(result);
        
        // Find matching SKU in selected PO if possible
        if (result.gtin && selectedPO) {
          const matchedLine = selectedPO.linhas.find(l => l.ref.toLowerCase().includes(result.gtin!.toLowerCase()) || result.gtin!.includes(l.ref));
          const targetRef = matchedLine ? matchedLine.ref : 'SKU-99023';
          const targetQty = result.quantity || 1;
          
          // Fetch Directed Putaway Suggestion
          fetchPutawaySuggestion(targetRef, targetQty);
          
          if (matchedLine) {
            // Auto-fill values in form
            setReceivedLines(prev => ({
              ...prev,
              [matchedLine.id]: {
                quantidade: result.quantity || prev[matchedLine.id].quantidade,
                lote: result.lot || prev[matchedLine.id].lote,
                localizacao: prev[matchedLine.id].localizacao
              }
            }));
          }
        } else {
          fetchPutawaySuggestion('SKU-99023', result.quantity || 15);
        }
      }
    } catch (error) {
      console.error('Error decoding barcode:', error);
      const localResult = parseGS1Barcode(rawString);
      setGs1Result(localResult);
    }
  };

  const fetchPutawaySuggestion = async (refCode: string, qty: number) => {
    try {
      const response = await fetch(`/api/logistica/putaway/sugestao?refCode=${refCode}&quantidade=${qty}`);
      if (response.ok) {
        const suggestion = await response.json();
        setPutawaySuggestion(suggestion);
        
        if (selectedPO) {
          const matchedLine = selectedPO.linhas.find(l => l.ref === refCode);
          if (matchedLine) {
            setReceivedLines(prev => ({
              ...prev,
              [matchedLine.id]: {
                ...prev[matchedLine.id],
                localizacao: suggestion.localizacaoId
              }
            }));
          }
        }
      }
    } catch (error) {
      console.error('Error fetching putaway suggestion:', error);
    }
  };

  const handleApplySampleBarcode = (type: 'valid' | 'invalid') => {
    const samples = {
      valid: "01056012345678901727031510LOTE12345\u001D370150",
      invalid: "01056012345678901727030010LOTE12345\u001D370150"
    };
    
    const code = samples[type];
    setBarcodeInput(code);
    handleDecodeBarcode(code);
  };

  const handleLineChange = (lineId: string, field: 'quantidade' | 'lote' | 'localizacao', value: any) => {
    setReceivedLines(prev => ({
      ...prev,
      [lineId]: {
        ...prev[lineId],
        [field]: value
      }
    }));
  };

  const handleConfirmRececao = async () => {
    if (!selectedPO) return;
    
    setSubmitting(true);
    try {
      const linesDto = Object.entries(receivedLines).map(([lineId, data]) => ({
        linhaId: lineId,
        quantidadeRecebida: Number(data.quantidade),
        lote: data.lote || null,
        localizacao: data.localizacao
      })).filter(l => l.quantidadeRecebida > 0);

      if (linesDto.length === 0) {
        alert('Introduza quantidades superiores a 0 para pelo menos um artigo.');
        setSubmitting(false);
        return;
      }

      const payload = {
        encomendaId: selectedPO.id,
        linhas: linesDto
      };

      const response = await fetch('/api/logistica/rececao', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (response.ok) {
        setSuccessMessage('Receção de mercadoria confirmada com sucesso no WMS e enviada para o ERP.');
        setSelectedPO(null);
        setGs1Result(null);
        setPutawaySuggestion(null);
        fetchPendingPOs();
      } else {
        const errorData = await response.json();
        alert(`Erro ao submeter receção: ${errorData.message || 'Erro desconhecido'}`);
      }
    } catch (error) {
      console.error('Error submitting PO receipt:', error);
      alert('Falha ao registar receção de mercadoria.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section className="tab-pane">
      <div className="pane-header">
        <h2>Receção & Descodificação GS1</h2>
        <p>Selecione uma Encomenda de Fornecedor ativa (PO) e utilize o descodificador GS1 para extrair GTIN, Lote e Validade.</p>
      </div>

      <div className="grid-layout">
        <div className="glass-card">
          <h3>
            <Barcode /> Seleção de Encomenda de Fornecedor
          </h3>
          
          <div className="form-group">
            <label htmlFor="po-select">Encomenda de Fornecedor Pendente:</label>
            {loading ? (
              <p className="loading-text">A carregar encomendas de fornecedores...</p>
            ) : purchaseOrders.length === 0 ? (
              <p className="placeholder-text">Não há encomendas de fornecedores ativas sincronizadas com o ERP.</p>
            ) : (
              <select
                id="po-select"
                value={selectedPO?.id || ''}
                onChange={(e) => {
                  const po = purchaseOrders.find(p => p.id === e.target.value);
                  setSelectedPO(po || null);
                }}
              >
                <option value="">Selecione uma encomenda...</option>
                {purchaseOrders.map(po => (
                  <option key={po.id} value={po.id}>
                    PO {po.documentoNo || po.id.substring(0, 8)} - Fornecedor #{po.clienteNo} ({new Date(po.data).toLocaleDateString()})
                  </option>
                ))}
              </select>
            )}
          </div>

          {selectedPO && (
            <div style={{ marginTop: '20px' }}>
              <h4 style={{ marginBottom: '10px', fontSize: '14px', color: 'var(--text-muted)' }}>Linhas da Encomenda</h4>
              <div className="table-container">
                <table className="wms-table">
                  <thead>
                    <tr>
                      <th>SKU</th>
                      <th>Artigo</th>
                      <th>Qtd. Esperada</th>
                      <th>Qtd. Recebida</th>
                      <th>Lote</th>
                      <th>Localização</th>
                    </tr>
                  </thead>
                  <tbody>
                    {selectedPO.linhas.map(line => {
                      const details = receivedLines[line.id] || { quantidade: 0, lote: '', localizacao: '' };
                      return (
                        <tr key={line.id}>
                          <td style={{ fontWeight: 600 }}>{line.ref}</td>
                          <td>{line.designacao}</td>
                          <td style={{ textAlign: 'center' }}>{line.quantidade}</td>
                          <td>
                            <input
                              type="number"
                              style={{ width: '70px', padding: '6px', background: 'rgba(0,0,0,0.3)', border: '1px solid var(--border-color)', color: '#fff', borderRadius: '4px', textAlign: 'center' }}
                              value={details.quantidade}
                              onChange={(e) => handleLineChange(line.id, 'quantidade', parseInt(e.target.value, 10) || 0)}
                              min="0"
                              max={line.quantidade * 1.5}
                            />
                          </td>
                          <td>
                            <input
                              type="text"
                              style={{ width: '90px', padding: '6px', background: 'rgba(0,0,0,0.3)', border: '1px solid var(--border-color)', color: '#fff', borderRadius: '4px' }}
                              value={details.lote}
                              onChange={(e) => handleLineChange(line.id, 'lote', e.target.value)}
                              placeholder="LOTE"
                            />
                          </td>
                          <td>
                            <input
                              type="text"
                              style={{ width: '100px', padding: '6px', background: 'rgba(0,0,0,0.3)', border: '1px solid var(--border-color)', color: '#fff', borderRadius: '4px' }}
                              value={details.localizacao}
                              onChange={(e) => handleLineChange(line.id, 'localizacao', e.target.value)}
                              placeholder="LOC"
                            />
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>

              <div className="btn-group">
                <button
                  className="btn btn-primary full-width-btn"
                  onClick={handleConfirmRececao}
                  disabled={submitting}
                >
                  <CheckCircle /> Confirmar Receção de Mercadoria
                </button>
              </div>
            </div>
          )}

          {successMessage && (
            <div className="warning-alert" style={{ backgroundColor: 'rgba(16, 185, 129, 0.1)', borderColor: 'rgba(16, 185, 129, 0.3)', color: '#34d399', marginTop: '16px' }}>
              <CheckCircle />
              <span>{successMessage}</span>
            </div>
          )}
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
          <div className="glass-card">
            <h3>
              <Barcode /> Simulação de Leitura Ótica
            </h3>
            <div className="form-group">
              <label htmlFor="barcode-input">String GS1 Decodificada (Raw):</label>
              <textarea
                id="barcode-input"
                rows={3}
                value={barcodeInput}
                onChange={(e) => setBarcodeInput(e.target.value)}
                placeholder="01056012345678901727031510LOTE12345\u001D370150"
              />
            </div>
            <div className="btn-group">
              <button
                className="btn btn-primary"
                onClick={() => handleDecodeBarcode(barcodeInput)}
                disabled={!barcodeInput.trim()}
              >
                <Play size={16} /> Descodificar
              </button>
              <button className="btn btn-secondary" onClick={() => handleApplySampleBarcode('valid')}>
                <FlaskConical size={16} /> Exemplo Válido
              </button>
              <button className="btn btn-danger" onClick={() => handleApplySampleBarcode('invalid')}>
                <AlertTriangle size={16} /> Exemplo com Erro (Dia 00)
              </button>
            </div>
          </div>

          {gs1Result && (
            <div className="glass-card">
              <h3>
                <CheckCircle /> Campos Extraídos (GS1 AIs)
              </h3>
              
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div style={{ background: 'rgba(0,0,0,0.2)', padding: '12px', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
                  <span style={{ fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>GTIN (01)</span>
                  <div style={{ fontSize: '16px', fontWeight: 600, marginTop: '4px' }}>{gs1Result.gtin || '-'}</div>
                </div>
                <div style={{ background: 'rgba(0,0,0,0.2)', padding: '12px', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
                  <span style={{ fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Data de Validade (17)</span>
                  <div style={{ fontSize: '16px', fontWeight: 600, marginTop: '4px' }}>{gs1Result.expiryDate || '-'}</div>
                </div>
                <div style={{ background: 'rgba(0,0,0,0.2)', padding: '12px', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
                  <span style={{ fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Lote (10)</span>
                  <div style={{ fontSize: '16px', fontWeight: 600, marginTop: '4px' }}>{gs1Result.lot || '-'}</div>
                </div>
                <div style={{ background: 'rgba(0,0,0,0.2)', padding: '12px', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
                  <span style={{ fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Quantidade (37)</span>
                  <div style={{ fontSize: '16px', fontWeight: 600, marginTop: '4px' }}>{gs1Result.quantity || '-'}</div>
                </div>
              </div>

              {gs1Result.validationError && (
                <div className="warning-alert">
                  <AlertTriangle />
                  <span>{gs1Result.validationError}</span>
                </div>
              )}
            </div>
          )}

          {putawaySuggestion && (
            <div className="glass-card">
              <h3>
                <Compass /> Sugestão de Arrumação Dirigida (Directed Putaway)
              </h3>
              
              <div className="putaway-wrapper">
                <div className={`location-beacon ${putawaySuggestion.zona === 'CQ' ? 'cq-zone' : ''}`}>
                  <span className="beacon-label">Local Sugerido</span>
                  <span className="beacon-val">{putawaySuggestion.localizacaoId}</span>
                </div>
                
                <div className="location-details">
                  <p><strong>Zona de Armazenagem:</strong> {putawaySuggestion.zona}</p>
                  <p><strong>Coordenadas 3D:</strong> Corredor {putawaySuggestion.corredor}, Estante {putawaySuggestion.estante}, Prateleira {putawaySuggestion.prateleira}, Alvéolo {putawaySuggestion.alveolo}</p>
                  <p><strong>Limites Físicos:</strong> Max Peso: {putawaySuggestion.maxPesoKg} kg | Max Volume: {putawaySuggestion.maxVolumeM3} m³</p>
                  <div className="reasoning">
                    <strong>Motivo:</strong> {putawaySuggestion.reason}
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </section>
  );
};
