import { useState, useEffect } from 'react';
import { Compass, Route, CheckCircle, Navigation } from 'lucide-react';

interface EncomendaLinha {
  id: string;
  ref: string;
  designacao: string;
  quantidade: number;
  preco: number;
  lote?: string | null;
  localizacao: string;
}

interface Encomenda {
  id: string;
  documentoNo: number;
  clienteNo: number;
  data: string;
  status: string;
  linhas: EncomendaLinha[];
}

export const PickingPanel = () => {
  const [pendingOrders, setPendingOrders] = useState<Encomenda[]>([]);
  const [selectedOrderId, setSelectedOrderId] = useState<string>('');
  const [loadingOrders, setLoadingOrders] = useState(false);
  const [loadingRoute, setLoadingRoute] = useState(false);
  
  // Sorted lines in S-Shape
  const [optimizedLines, setOptimizedLines] = useState<EncomendaLinha[]>([]);
  
  // Picked lines tracking
  const [pickedLines, setPickedLines] = useState<Record<string, { quantidade: number; lote: string; localizacao: string; confirmed: boolean }>>({});
  
  const [submitting, setSubmitting] = useState(false);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const fetchOrders = async () => {
    setLoadingOrders(true);
    try {
      const response = await fetch('/api/logistica/picking/pendentes');
      if (response.ok) {
        const data = await response.json();
        setPendingOrders(data);
      }
    } catch (e) {
      console.error(e);
    } finally {
      setLoadingOrders(false);
    }
  };

  useEffect(() => {
    fetchOrders();
  }, []);

  const loadOptimizedRoute = async (orderId: string) => {
    if (!orderId) {
      setOptimizedLines([]);
      return;
    }
    setLoadingRoute(true);
    setSuccessMessage(null);
    try {
      const response = await fetch(`/api/logistica/picking/optimized/${orderId}`);
      if (response.ok) {
        const data = await response.json();
        setOptimizedLines(data);
        
        // Setup initial confirmation state
        const initial: Record<string, { quantidade: number; lote: string; localizacao: string; confirmed: boolean }> = {};
        data.forEach((line: EncomendaLinha) => {
          initial[line.id] = {
            quantidade: line.quantidade,
            lote: line.lote || 'L-DEFAULT',
            localizacao: line.localizacao,
            confirmed: false
          };
        });
        setPickedLines(initial);
      }
    } catch (e) {
      console.error(e);
    } finally {
      setLoadingRoute(false);
    }
  };

  const handleOrderSelect = (orderId: string) => {
    setSelectedOrderId(orderId);
    loadOptimizedRoute(orderId);
  };

  const toggleConfirmLine = (lineId: string) => {
    setPickedLines(prev => ({
      ...prev,
      [lineId]: {
        ...prev[lineId],
        confirmed: !prev[lineId].confirmed
      }
    }));
  };

  const handleQtyChange = (lineId: string, qty: number) => {
    setPickedLines(prev => ({
      ...prev,
      [lineId]: {
        ...prev[lineId],
        quantidade: qty
      }
    }));
  };

  const handleLoteChange = (lineId: string, lote: string) => {
    setPickedLines(prev => ({
      ...prev,
      [lineId]: {
        ...prev[lineId],
        lote: lote
      }
    }));
  };

  const handleConfirmPicking = async () => {
    if (!selectedOrderId) return;

    const unconfirmed = Object.values(pickedLines).some(l => !l.confirmed);
    if (unconfirmed) {
      const proceed = window.confirm('Algumas linhas não foram assinaladas como recolhidas. Confirma a conclusão parcial?');
      if (!proceed) return;
    }

    setSubmitting(true);
    try {
      const linesDto = Object.entries(pickedLines)
        .filter(([_, data]) => data.confirmed)
        .map(([lineId, data]) => ({
          linhaId: lineId,
          quantidadeRecolhida: Number(data.quantidade),
          lote: data.lote,
          localizacao: data.localizacao
        }));

      if (linesDto.length === 0) {
        alert('Recolha pelo menos um artigo da encomenda.');
        setSubmitting(false);
        return;
      }

      const payload = {
        encomendaId: selectedOrderId,
        linhas: linesDto
      };

      const response = await fetch('/api/logistica/picking', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (response.ok) {
        setSuccessMessage('Picking concluído com sucesso. Guia de Remessa criada e sincronizada.');
        setOptimizedLines([]);
        setSelectedOrderId('');
        fetchOrders();
      } else {
        const err = await response.json();
        alert(`Erro ao submeter picking: ${err.message || 'Erro desconhecido'}`);
      }
    } catch (e) {
      console.error(e);
      alert('Erro de rede.');
    } finally {
      setSubmitting(false);
    }
  };

  const selectedOrder = pendingOrders.find(o => o.id === selectedOrderId);

  return (
    <section className="tab-pane">
      <div className="pane-header">
        <h2>Preparação de Encomendas (Picking Otimizado)</h2>
        <p>Minimize a distância percorrida pelo operador utilizando a heurística S-Shape (Serpentina).</p>
      </div>

      {successMessage && (
        <div className="warning-alert" style={{ backgroundColor: 'rgba(16, 185, 129, 0.1)', borderColor: 'rgba(16, 185, 129, 0.3)', color: '#34d399', marginBottom: '8px' }}>
          <CheckCircle size={20} />
          <span>{successMessage}</span>
        </div>
      )}

      <div className="grid-layout">
        <div className="glass-card">
          <h3>
            <Compass /> Encomendas para Preparar
          </h3>
          
          {loadingOrders ? (
            <p className="loading-text">A ler encomendas pendentes...</p>
          ) : pendingOrders.length === 0 ? (
            <p className="placeholder-text">Não há encomendas de clientes ativas para preparar.</p>
          ) : (
            <div className="order-list">
              {pendingOrders.map(order => (
                <div
                  key={order.id}
                  className={`order-item-card ${selectedOrderId === order.id ? 'selected' : ''}`}
                  onClick={() => handleOrderSelect(order.id)}
                >
                  <div className="order-info-meta">
                    <span className="order-ref-no">Doc Nº {order.documentoNo || order.id.substring(0, 8)}</span>
                    <span className="order-details-meta">
                      Cliente: #{order.clienteNo} | Data: {new Date(order.data).toLocaleDateString()}
                    </span>
                  </div>
                  <span className="badge badge-info">
                    {order.linhas.length} Linhas
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="glass-card">
          <h3>
            <Route /> Rota Otimizada S-Shape (Serpentina)
          </h3>

          <div className="picking-status-info">
            <span>
              {selectedOrder ? (
                <>Encomenda: <strong>Doc #{selectedOrder.documentoNo || selectedOrder.id.substring(0, 8)}</strong></>
              ) : (
                'Selecione uma encomenda à esquerda'
              )}
            </span>
            {selectedOrder && (
              <span className="route-badge">
                <Navigation size={12} /> Serpentina Ativo
              </span>
            )}
          </div>

          {loadingRoute ? (
            <p className="loading-text">A calcular rota ótima...</p>
          ) : optimizedLines.length === 0 ? (
            <p className="placeholder-text">Selecione uma encomenda na lista para ver o percurso de recolha ordenado por corredores.</p>
          ) : (
            <>
              <div className="optimized-list">
                {optimizedLines.map((line, index) => {
                  const state = pickedLines[line.id] || { quantidade: line.quantidade, lote: '', localizacao: line.localizacao, confirmed: false };
                  
                  return (
                    <div key={line.id} className={`picking-line-item ${state.confirmed ? 'completed' : ''}`}>
                      <div className="pick-seq-no">
                        {index + 1}
                      </div>

                      <div className="pick-line-main">
                        <span className="pick-sku-title">{line.ref}</span>
                        <span className="pick-sku-desc">{line.designacao}</span>
                        <span className="pick-location-badge">
                          LOC: {line.localizacao}
                        </span>
                        
                        {!state.confirmed ? (
                          <div style={{ display: 'flex', gap: '8px', marginTop: '8px' }}>
                            <input
                              type="number"
                              style={{ width: '60px', padding: '4px', background: 'rgba(0,0,0,0.3)', border: '1px solid var(--border-color)', color: '#fff', borderRadius: '4px', fontSize: '12px', textAlign: 'center' }}
                              value={state.quantidade}
                              onChange={(e) => handleQtyChange(line.id, parseInt(e.target.value, 10) || 0)}
                            />
                            <input
                              type="text"
                              style={{ width: '90px', padding: '4px', background: 'rgba(0,0,0,0.3)', border: '1px solid var(--border-color)', color: '#fff', borderRadius: '4px', fontSize: '12px' }}
                              value={state.lote}
                              onChange={(e) => handleLoteChange(line.id, e.target.value)}
                              placeholder="Lote"
                            />
                          </div>
                        ) : (
                          <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '4px' }}>
                            Recolhido: {state.quantidade} UN | Lote: {state.lote}
                          </div>
                        )}
                      </div>

                      <div className="pick-line-qty">
                        <span className="qty-val-label">{line.quantidade}</span>
                        <button
                          className={`btn ${state.confirmed ? 'btn-secondary' : 'btn-primary'} btn-sm`}
                          onClick={() => toggleConfirmLine(line.id)}
                        >
                          {state.confirmed ? 'Editar' : 'Recolhido'}
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>

              <div className="btn-group" style={{ marginTop: '20px' }}>
                <button
                  className="btn btn-primary full-width-btn"
                  onClick={handleConfirmPicking}
                  disabled={submitting}
                >
                  <CheckCircle size={18} /> Confirmar Picking e Gerar Guia
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </section>
  );
};
