import React, { useState, useEffect } from 'react';
import { RotateCcw, FileText, CheckCircle2, ShieldCheck, Bell } from 'lucide-react';

interface RmaLinha {
  id: string;
  ref: string;
  quantidade: number;
  grading?: string | null;
  destinoLocalizacao?: string | null;
}

interface Rma {
  id: string;
  rmaCodigo: string;
  invoiceRef: string;
  clienteNo: number;
  status: string; // Iniciada, Autorizada, Recebida, Inspecionada, Concluida
  linhas: RmaLinha[];
}

export const RmaPanel: React.FC = () => {
  // RMA Registration Form
  const [rmaCode, setRmaCode] = useState('RMA-2026-90451');
  const [invoiceRef, setInvoiceRef] = useState('FT 2026/8940');
  const [clientNo, setClientNo] = useState(509123456);
  const [sku, setSku] = useState('SKU-99023');
  const [qty, setQty] = useState(3);
  const [submittingRma, setSubmittingRma] = useState(false);

  // Active RMAs list
  const [activeRmas, setActiveRmas] = useState<Rma[]>([]);
  const [loadingRmas, setLoadingRmas] = useState(false);
  const [selectedRmaId, setSelectedRmaId] = useState<string>('');

  // Webhook JSON state
  const [webhookJson, setWebhookJson] = useState<string>('');
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const fetchRmas = async () => {
    setLoadingRmas(true);
    try {
      const response = await fetch('/api/logistica/rma');
      if (response.ok) {
        const data = await response.json();
        setActiveRmas(data);
        if (data.length > 0 && !selectedRmaId) {
          setSelectedRmaId(data[0].id);
        }
      }
    } catch (e) {
      console.error(e);
    } finally {
      setLoadingRmas(false);
    }
  };

  useEffect(() => {
    fetchRmas();
  }, []);

  const handleCreateRma = async () => {
    if (!rmaCode || !invoiceRef || !sku || qty <= 0) {
      alert('Introduza dados válidos no formulário.');
      return;
    }

    setSubmittingRma(true);
    try {
      const payload = {
        rmaCodigo: rmaCode,
        invoiceRef: invoiceRef,
        clienteNo: Number(clientNo),
        linhas: [
          {
            ref: sku,
            quantidade: Number(qty)
          }
        ]
      };

      const response = await fetch('/api/logistica/rma', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (response.ok) {
        setSuccessMessage('Registo de devolução (RMA Draft) criado com sucesso.');
        setRmaCode(prev => {
          const match = prev.match(/\d+$/);
          if (match) {
            const nextNum = parseInt(match[0], 10) + 1;
            return prev.replace(/\d+$/, nextNum.toString());
          }
          return prev + '-1';
        });
        fetchRmas();
      } else {
        alert('Erro ao criar rascunho de RMA.');
      }
    } catch (e) {
      console.error(e);
    } finally {
      setSubmittingRma(false);
    }
  };

  const handleGradeLine = async (rmaId: string, lineId: string, grade: 'A' | 'B' | 'C') => {
    try {
      const response = await fetch('/api/logistica/rma/grade', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          rmaId: rmaId,
          linhaId: lineId,
          grading: grade
        })
      });

      if (response.ok) {
        const result = await response.json();
        setSuccessMessage(`CQ Grade ${grade} gravada. Artigo reencaminhado para a localização física: ${result.destino}`);
        fetchRmas();
      } else {
        alert('Erro ao submeter classificação de qualidade.');
      }
    } catch (e) {
      console.error(e);
    }
  };

  const handleSettleRma = async (rmaId: string) => {
    try {
      const response = await fetch('/api/logistica/rma/settle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          rmaId: rmaId
        })
      });

      if (response.ok) {
        const result = await response.json();
        setSuccessMessage('Devolução concluída. Webhook financeiro emitido com sucesso para o ERP.');
        setWebhookJson(JSON.stringify(result.webhookSent, null, 2));
        fetchRmas();
      } else {
        alert('Erro ao concluir devolução.');
      }
    } catch (e) {
      console.error(e);
    }
  };

  const selectedRma = activeRmas.find(r => r.id === selectedRmaId);

  return (
    <section className="tab-pane">
      <div className="pane-header">
        <h2>Logística Inversa (Devoluções & RMA)</h2>
        <p>Registe devoluções, realize inspeções de CQ com matriz de grading física e emita notificações automáticas de reembolso.</p>
      </div>

      {successMessage && (
        <div className="warning-alert" style={{ backgroundColor: 'rgba(16, 185, 129, 0.1)', borderColor: 'rgba(16, 185, 129, 0.3)', color: '#34d399', marginBottom: '8px' }}>
          <CheckCircle2 size={20} />
          <span>{successMessage}</span>
          <button onClick={() => setSuccessMessage(null)} style={{ marginLeft: 'auto', background: 'none', border: 'none', color: '#34d399', cursor: 'pointer', fontWeight: 'bold' }}>X</button>
        </div>
      )}

      <div className="grid-layout">
        <div className="glass-card">
          <h3>
            <RotateCcw /> Criar Registo RMA (Entrada Devolução)
          </h3>

          <div className="form-group">
            <label htmlFor="rma-code-in">Código RMA (ID Devolução):</label>
            <input
              id="rma-code-in"
              type="text"
              value={rmaCode}
              onChange={(e) => setRmaCode(e.target.value)}
            />
          </div>

          <div className="form-group">
            <label htmlFor="rma-inv-ref">Fatura de Venda Relacionada (ERP):</label>
            <input
              id="rma-inv-ref"
              type="text"
              value={invoiceRef}
              onChange={(e) => setInvoiceRef(e.target.value)}
            />
          </div>

          <div className="form-group">
            <label htmlFor="rma-cli-no">Cliente Nº (NIF):</label>
            <input
              id="rma-cli-no"
              type="number"
              value={clientNo}
              onChange={(e) => setClientNo(parseInt(e.target.value, 10) || 0)}
            />
          </div>

          <div style={{ display: 'flex', gap: '10px' }}>
            <div className="form-group" style={{ flexGrow: 2 }}>
              <label htmlFor="rma-sku-in">Artigo SKU:</label>
              <input
                id="rma-sku-in"
                type="text"
                value={sku}
                onChange={(e) => setSku(e.target.value)}
              />
            </div>
            <div className="form-group" style={{ flexGrow: 1 }}>
              <label htmlFor="rma-qty-in">Quantidade:</label>
              <input
                id="rma-qty-in"
                type="number"
                value={qty}
                onChange={(e) => setQty(parseInt(e.target.value, 10) || 0)}
                min="1"
              />
            </div>
          </div>

          <button
            className="btn btn-primary full-width-btn"
            onClick={handleCreateRma}
            disabled={submittingRma}
            style={{ marginTop: '10px' }}
          >
            <FileText size={16} /> Criar Registo de Entrada
          </button>
        </div>

        <div className="glass-card">
          <h3>
            <ShieldCheck /> Inspeção de CQ e Matriz de Grading
          </h3>

          <div className="form-group">
            <label htmlFor="active-rma-select">Escolha a Devolução Recebida:</label>
            {loadingRmas ? (
              <p className="loading-text">A ler devoluções activas...</p>
            ) : activeRmas.length === 0 ? (
              <p className="placeholder-text">Sem devoluções pendentes de inspeção no armazém.</p>
            ) : (
              <select
                id="active-rma-select"
                value={selectedRmaId}
                onChange={(e) => setSelectedRmaId(e.target.value)}
              >
                <option value="">Selecione um RMA...</option>
                {activeRmas.map(rma => (
                  <option key={rma.id} value={rma.id}>
                    {rma.rmaCodigo} - Cliente {rma.clienteNo} ({rma.status})
                  </option>
                ))}
              </select>
            )}
          </div>

          {selectedRma && (
            <div className="active-order-box" style={{ marginTop: '16px' }}>
              <div className="active-order-header">
                <span>Ref Fatura: <strong>{selectedRma.invoiceRef}</strong></span>
                <span className={`badge ${selectedRma.status === 'Concluida' ? 'badge-success' : 'badge-warning'}`}>
                  {selectedRma.status}
                </span>
              </div>

              <div className="active-order-lines">
                {selectedRma.linhas.map(linha => {
                  const hasGrading = linha.grading !== null && linha.grading !== undefined;
                  return (
                    <div key={linha.id} className="count-line-card" style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
                      <div>
                        <strong>{linha.ref}</strong> - Qtd Devolvida: <strong>{linha.quantidade} UN</strong>
                        {hasGrading && (
                          <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '4px' }}>
                            Grau: <span style={{ color: 'var(--color-primary)', fontWeight: 600 }}>{linha.grading}</span> | Destino: <strong>{linha.destinoLocalizacao}</strong>
                          </div>
                        )}
                      </div>

                      <div className="rma-grading-actions">
                        <button
                          className={`grade-btn grade-a ${linha.grading === 'A' ? 'selected' : ''}`}
                          onClick={() => handleGradeLine(selectedRma.id, linha.id, 'A')}
                          disabled={selectedRma.status === 'Concluida'}
                          title="Grau A: Excelente. Reentrar em picking disponível."
                        >
                          A
                        </button>
                        <button
                          className={`grade-btn grade-b ${linha.grading === 'B' ? 'selected' : ''}`}
                          onClick={() => handleGradeLine(selectedRma.id, linha.id, 'B')}
                          disabled={selectedRma.status === 'Concluida'}
                          title="Grau B: Recondicionamento (Zona VAS)."
                        >
                          B
                        </button>
                        <button
                          className={`grade-btn grade-c ${linha.grading === 'C' ? 'selected' : ''}`}
                          onClick={() => handleGradeLine(selectedRma.id, linha.id, 'C')}
                          disabled={selectedRma.status === 'Concluida'}
                          title="Grau C: Defeituoso/Quarentena. Enviar para sucata."
                        >
                          C
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>

              <div className="btn-group">
                <button
                  className="btn btn-primary full-width-btn"
                  onClick={() => handleSettleRma(selectedRma.id)}
                  disabled={
                    selectedRma.status === 'Concluida' || 
                    selectedRma.linhas.some(l => l.grading === null || l.grading === undefined)
                  }
                >
                  <CheckCircle2 size={16} /> Concluir Inspeção e Reembolsar
                </button>
              </div>
            </div>
          )}
        </div>

        <div className="glass-card full-width">
          <h3>
            <Bell /> Notificação Webhook para ERP (Reconciliação Financeira)
          </h3>
          <div className="webhook-grid">
            <div className="webhook-details">
              <span>Payload JSON enviado ao ERP (porta 5000) contendo as notas de crédito calculadas:</span>
              <span className="badge badge-info">HTTP POST - application/json</span>
            </div>
            <pre className="json-viewer" style={{ minHeight: '140px' }}>
              {webhookJson || 
`{
  "event": "rma.inspection_completed",
  "status": "Aguardando conclusão de devolução para enviar webhook ao ERP..."
}`}
            </pre>
          </div>
        </div>
      </div>
    </section>
  );
};
