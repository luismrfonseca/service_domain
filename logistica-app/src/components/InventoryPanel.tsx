import { useState, useEffect } from 'react';
import { ClipboardCheck, Plus, ShieldAlert, CheckCircle } from 'lucide-react';

interface StockItem {
  id: string;
  ref: string;
  loteCodigo?: string | null;
  armazem: number;
  localizacao: string;
  quantidade: number;
}

interface LinhaContagem {
  id: string;
  stockId: string;
  quantidadeSistema: number;
  quantidadeContada1?: number | null;
  operador1Id?: string | null;
  quantidadeContada2?: number | null;
  operador2Id?: string | null;
  dataAprovacao?: string | null;
  ajusteAplicado: boolean;
  stock: StockItem;
}

interface OrdemContagem {
  id: string;
  tipoContagem: string;
  estado: string; // PENDENTE, EM_RECONTAGEM, CONCLUIDO
  dataCriacao: string;
  supervisorId?: string | null;
  linhas: LinhaContagem[];
}

export const InventoryPanel = () => {
  // Stocks & selection for new orders
  const [stocks, setStocks] = useState<StockItem[]>([]);
  const [selectedStockIds, setSelectedStockIds] = useState<string[]>([]);
  const [countType, setCountType] = useState('ABC');
  const [supervisorId, setSupervisorId] = useState('sup_01');
  const [operatorId, setOperatorId] = useState('op_01');

  // Active count orders & state
  const [activeOrders, setActiveOrders] = useState<OrdemContagem[]>([]);
  const [selectedOrderId, setSelectedOrderId] = useState<string>('');
  
  // Loading & success feedback
  const [loadingStocks, setLoadingStocks] = useState(false);
  const [loadingOrders, setLoadingOrders] = useState(false);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Operator count recording values
  const [countedQuantities, setCountedQuantities] = useState<Record<string, string>>({});

  const fetchData = async () => {
    setLoadingStocks(true);
    setLoadingOrders(true);
    try {
      const stocksRes = await fetch('/api/stocks');
      if (stocksRes.ok) {
        const stocksData = await stocksRes.json();
        setStocks(stocksData);
      }

      const ordersRes = await fetch('/api/logistica/contagens/pendentes');
      if (ordersRes.ok) {
        const ordersData = await ordersRes.json();
        setActiveOrders(ordersData);
        if (ordersData.length > 0 && !selectedOrderId) {
          setSelectedOrderId(ordersData[0].id);
        }
      }
    } catch (e) {
      console.error('Error loading inventory data:', e);
    } finally {
      setLoadingStocks(false);
      setLoadingOrders(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  const handleCreateCountOrder = async () => {
    if (selectedStockIds.length === 0) {
      alert('Selecione pelo menos um registo de stock para auditar.');
      return;
    }

    try {
      const response = await fetch('/api/logistica/contagens', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          tipoContagem: countType,
          supervisorId: supervisorId,
          stockIds: selectedStockIds
        })
      });

      if (response.ok) {
        const res = await response.json();
        setSuccessMsg(`Ordem de contagem criada com sucesso (Ordem ID: ${res.ordemId.substring(0, 8)}).`);
        setSelectedStockIds([]);
        fetchData();
      } else {
        alert('Erro ao criar ordem de contagem.');
      }
    } catch (e) {
      console.error(e);
      alert('Falha na ligação com o servidor.');
    }
  };

  const handleRecordCount = async (linhaId: string) => {
    const qtyStr = countedQuantities[linhaId];
    if (qtyStr === undefined || qtyStr === '') {
      alert('Por favor introduza a quantidade física contada.');
      return;
    }

    const qty = parseFloat(qtyStr);
    if (isNaN(qty) || qty < 0) {
      alert('A quantidade contada deve ser um valor válido superior ou igual a zero.');
      return;
    }

    try {
      const response = await fetch('/api/logistica/contagens/registar', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ordemId: selectedOrderId,
          linhaId: linhaId,
          quantidadeContada: qty,
          operadorId: operatorId
        })
      });

      if (response.ok) {
        setSuccessMsg('Contagem registada com sucesso.');
        setCountedQuantities(prev => {
          const next = { ...prev };
          delete next[linhaId];
          return next;
        });
        fetchData();
      } else {
        const err = await response.json();
        alert(`Erro ao registar: ${err.message || 'Erro desconhecido'}`);
      }
    } catch (e) {
      console.error(e);
      alert('Erro de comunicação.');
    }
  };

  const handleApproveOrder = async (ordemId: string) => {
    try {
      const response = await fetch('/api/logistica/contagens/aprovar', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ordemId: ordemId,
          supervisorId: supervisorId
        })
      });

      if (response.ok) {
        setSuccessMsg('Ordem de contagem aprovada e inventário reconciliado no ERP.');
        if (selectedOrderId === ordemId) {
          setSelectedOrderId('');
        }
        fetchData();
      } else {
        alert('Erro ao aprovar ordem.');
      }
    } catch (e) {
      console.error(e);
    }
  };

  const handleStockToggle = (id: string) => {
    setSelectedStockIds(prev => 
      prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]
    );
  };

  const selectedOrder = activeOrders.find(o => o.id === selectedOrderId);

  return (
    <section className="tab-pane">
      <div className="pane-header">
        <h2>Contagem Cíclica e Governação de Stock</h2>
        <p>Desenhe inventários geográficos ou por classe ABC. Divergências acionam tarefas de recontagem cega automática.</p>
      </div>

      {successMsg && (
        <div className="warning-alert" style={{ backgroundColor: 'rgba(16, 185, 129, 0.1)', borderColor: 'rgba(16, 185, 129, 0.3)', color: '#34d399', marginBottom: '8px' }}>
          <CheckCircle size={20} />
          <span>{successMsg}</span>
          <button onClick={() => setSuccessMsg(null)} style={{ marginLeft: 'auto', background: 'none', border: 'none', color: '#34d399', cursor: 'pointer', fontWeight: 'bold' }}>X</button>
        </div>
      )}

      <div style={{ display: 'flex', gap: '20px', marginBottom: '10px' }}>
        <div className="form-group" style={{ flexGrow: 1 }}>
          <label>Supervisor Ativo:</label>
          <select value={supervisorId} onChange={(e) => setSupervisorId(e.target.value)}>
            <option value="sup_01">Supervisor WH-01 (Ana Silva)</option>
            <option value="sup_02">Supervisor WH-02 (Pedro Santos)</option>
          </select>
        </div>
        <div className="form-group" style={{ flexGrow: 1 }}>
          <label>Operador no Terminal:</label>
          <select value={operatorId} onChange={(e) => setOperatorId(e.target.value)}>
            <option value="op_01">Operador WH-09 (António)</option>
            <option value="op_02">Operador WH-10 (Mariana)</option>
            <option value="op_03">Operador WH-11 (Carlos)</option>
          </select>
        </div>
      </div>

      <div className="grid-layout">
        <div className="glass-card">
          <h3>
            <Plus /> Abrir Nova Ordem de Contagem
          </h3>

          <div className="form-group">
            <label htmlFor="count-type-select">Tipo de Auditoria:</label>
            <select
              id="count-type-select"
              value={countType}
              onChange={(e) => setCountType(e.target.value)}
            >
              <option value="ABC">Classe ABC (Foco em Rotação)</option>
              <option value="GEOGRAFICA">Geográfica (Foco em Corredor)</option>
              <option value="EXCECAO">Exceção (Ajustes Rápidos)</option>
            </select>
          </div>

          <div className="form-group">
            <label>Escolher Lotes no Inventário:</label>
            {loadingStocks ? (
              <p className="loading-text">A ler stock...</p>
            ) : stocks.length === 0 ? (
              <p className="placeholder-text">Sem registos de stock no armazém.</p>
            ) : (
              <div className="stock-multiselect">
                {stocks.map(item => (
                  <label key={item.id} className="stock-select-row">
                    <input
                      type="checkbox"
                      checked={selectedStockIds.includes(item.id)}
                      onChange={() => handleStockToggle(item.id)}
                    />
                    <span>
                      <strong>{item.ref}</strong> (Lote: {item.loteCodigo || 'N/A'}) - <strong>{item.localizacao}</strong> [Qtd: {item.quantidade}]
                    </span>
                  </label>
                ))}
              </div>
            )}
          </div>

          <button className="btn btn-primary full-width-btn" onClick={handleCreateCountOrder} style={{ marginTop: '10px' }}>
            <ClipboardCheck size={18} /> Criar Ordem de Contagem
          </button>
        </div>

        <div className="glass-card">
          <h3>
            <ClipboardCheck /> Execução de Contagem (Operadores)
          </h3>

          <div className="form-group">
            <label htmlFor="active-order-select">Ordem de Contagem Ativa:</label>
            {loadingOrders ? (
              <p className="loading-text">A ler ordens...</p>
            ) : activeOrders.length === 0 ? (
              <p className="placeholder-text">Sem ordens pendentes. Crie uma no painel esquerdo.</p>
            ) : (
              <select
                id="active-order-select"
                value={selectedOrderId}
                onChange={(e) => setSelectedOrderId(e.target.value)}
              >
                <option value="">Selecione uma ordem...</option>
                {activeOrders.map(o => (
                  <option key={o.id} value={o.id}>
                    {o.tipoContagem} - ID: {o.id.substring(0, 8)} ({o.estado})
                  </option>
                ))}
              </select>
            )}
          </div>

          {selectedOrder && (
            <div className="active-order-box" style={{ marginTop: '16px' }}>
              <div className="active-order-header">
                <span>Metodologia: <strong>{selectedOrder.tipoContagem}</strong></span>
                <span className={`badge ${selectedOrder.estado === 'EM_RECONTAGEM' ? 'badge-warning' : 'badge-info'}`}>
                  {selectedOrder.estado}
                </span>
              </div>

              <div className="active-order-lines">
                {selectedOrder.linhas.map(line => {
                  const alreadyCountedByThisOp = line.operador1Id === operatorId;
                  const isRecountRequired = selectedOrder.estado === 'EM_RECONTAGEM' && line.quantidadeContada1 !== line.quantidadeSistema;
                  const isFullyCounted = line.quantidadeContada2 !== null && line.quantidadeContada2 !== undefined;
                  const isWaitingRecount = line.quantidadeContada1 !== null && line.quantidadeContada1 !== undefined && line.quantidadeContada2 === null && isRecountRequired;
                  
                  return (
                    <div key={line.id} className="count-line-card">
                      <div className="count-line-info">
                        <div>
                          <strong>{line.stock?.ref || 'Item'}</strong> (Lote: {line.stock?.loteCodigo || 'N/A'})
                          <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
                            Localização: <strong>{line.stock?.localizacao}</strong>
                          </div>
                        </div>
                        <div style={{ textAlign: 'right' }}>
                          {alreadyCountedByThisOp && <span className="badge badge-success" style={{ marginBottom: '4px', display: 'block' }}>Contado por Si</span>}
                          {isWaitingRecount && <span className="badge badge-warning" style={{ marginBottom: '4px', display: 'block' }}>Aguardando Recontagem</span>}
                        </div>
                      </div>

                      <div className="count-line-actions">
                        <div className="count-input-wrap">
                          <input
                            type="number"
                            placeholder="QTD física"
                            value={countedQuantities[line.id] || ''}
                            onChange={(e) => setCountedQuantities(prev => ({ ...prev, [line.id]: e.target.value }))}
                            disabled={isFullyCounted || (line.quantidadeContada1 !== null && line.quantidadeContada1 !== undefined && !isRecountRequired)}
                          />
                        </div>
                        
                        <button
                          className="btn btn-secondary btn-sm"
                          onClick={() => handleRecordCount(line.id)}
                          disabled={
                            isFullyCounted || 
                            (line.quantidadeContada1 !== null && line.quantidadeContada1 !== undefined && !isRecountRequired) ||
                            (isWaitingRecount && alreadyCountedByThisOp)
                          }
                        >
                          Gravar Contagem
                        </button>
                      </div>

                      {(line.quantidadeContada1 !== null && line.quantidadeContada1 !== undefined) && (
                        <div style={{ fontSize: '11px', color: 'var(--text-muted)', background: 'rgba(0,0,0,0.1)', padding: '6px 10px', borderRadius: '4px' }}>
                          <div>Contagem 1: {line.quantidadeContada1} UN (Operador: {line.operador1Id})</div>
                          {line.quantidadeContada2 !== null && line.quantidadeContada2 !== undefined && (
                            <div style={{ color: 'var(--color-warning)' }}>Contagem 2 (Cega): {line.quantidadeContada2} UN (Operador: {line.operador2Id})</div>
                          )}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          )}
        </div>

        <div className="glass-card full-width">
          <h3>
            <ShieldAlert /> Dashboard de Governação (Supervisores)
          </h3>

          <div className="table-container">
            <table className="wms-table">
              <thead>
                <tr>
                  <th>Ordem ID</th>
                  <th>Artigo / Lote</th>
                  <th>Localização</th>
                  <th>Qtd. Sistema</th>
                  <th>Qtd. Contada 1</th>
                  <th>Qtd. Contada 2 (Cega)</th>
                  <th>Desvio (Diferença)</th>
                  <th>Ações de Ajuste</th>
                </tr>
              </thead>
              <tbody>
                {activeOrders.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="text-center">Não existem ordens de contagem pendentes.</td>
                  </tr>
                ) : (
                  activeOrders.map(order => {
                    return order.linhas.map(linha => {
                      const finalQty = linha.quantidadeContada2 ?? linha.quantidadeContada1;
                      const deviation = finalQty !== undefined && finalQty !== null ? finalQty - linha.quantidadeSistema : 0;
                      
                      const isLineReadyToApprove = linha.quantidadeContada1 !== null && linha.quantidadeContada1 !== undefined && 
                        (linha.quantidadeContada1 === linha.quantidadeSistema || (linha.quantidadeContada2 !== null && linha.quantidadeContada2 !== undefined));

                      return (
                        <tr key={linha.id}>
                          <td>{order.tipoContagem} - {order.id.substring(0, 8)}</td>
                          <td style={{ fontWeight: 600 }}>{linha.stock?.ref} <span style={{ fontWeight: 400, color: 'var(--text-muted)' }}>({linha.stock?.loteCodigo || 'Sem Lote'})</span></td>
                          <td>{linha.stock?.localizacao}</td>
                          <td>{linha.quantidadeSistema} UN</td>
                          <td>{linha.quantidadeContada1 !== null ? `${linha.quantidadeContada1} UN` : '-'}</td>
                          <td>{linha.quantidadeContada2 !== null ? `${linha.quantidadeContada2} UN` : '-'}</td>
                          <td>
                            {finalQty !== undefined && finalQty !== null ? (
                              <span style={{ color: deviation === 0 ? 'var(--color-success)' : 'var(--color-danger)', fontWeight: 600 }}>
                                {deviation > 0 ? `+${deviation}` : deviation} UN
                              </span>
                            ) : (
                              <span style={{ color: 'var(--text-muted)' }}>Pendente</span>
                            )}
                          </td>
                          <td>
                            <button
                              className="btn btn-primary btn-sm"
                              onClick={() => handleApproveOrder(order.id)}
                              disabled={!isLineReadyToApprove || order.estado === 'CONCLUIDO'}
                            >
                              Aplicar Ajuste
                            </button>
                          </td>
                        </tr>
                      );
                    });
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </section>
  );
};
