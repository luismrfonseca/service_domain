import { useState, useEffect } from 'react';
import { Weight, Scale, Plug, Unplug, Plus, Trash2, CheckCircle2, AlertOctagon } from 'lucide-react';
import { connectToScale, disconnectScale, validatePackingWeight } from '../utils/wmsUtils';
import type { PackedItem, WeightValidationResult } from '../utils/wmsUtils';

export const PackingPanel = () => {
  // Balance Scale Weights
  const [actualWeight, setActualWeight] = useState<number>(0.0);
  const [scaleConnected, setScaleConnected] = useState<boolean>(false);
  
  // Package Form
  const [boxTare, setBoxTare] = useState<number>(0.350); // Standard box weight in kg
  const [packedItems, setPackedItems] = useState<PackedItem[]>([
    { ref: 'SKU-99023', quantidade: 15, peso_unitario_kg: 0.800 }
  ]);
  
  // Validation Results
  const [validationResult, setValidationResult] = useState<WeightValidationResult | null>(null);
  const [loadingValidation, setLoadingValidation] = useState<boolean>(false);

  // Sync simulator slider with actual weight if scale is NOT connected
  const handleSliderChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!scaleConnected) {
      setActualWeight(parseFloat(e.target.value));
    }
  };

  // Connect to physical hardware scale
  const handleConnectScale = async () => {
    try {
      await connectToScale(
        (weight) => {
          setActualWeight(weight);
        },
        (err) => {
          alert('Erro ao ligar à porta série: ' + err.message);
          setScaleConnected(false);
        }
      );
      setScaleConnected(true);
    } catch (e) {
      console.error(e);
    }
  };

  // Disconnect scale
  const handleDisconnectScale = async () => {
    await disconnectScale();
    setScaleConnected(false);
  };

  // Clean scale resources on unmount
  useEffect(() => {
    return () => {
      disconnectScale();
    };
  }, []);

  const handleAddItemRow = () => {
    setPackedItems(prev => [
      ...prev,
      { ref: '', quantidade: 1, peso_unitario_kg: 0.100 }
    ]);
  };

  const handleRemoveItemRow = (index: number) => {
    setPackedItems(prev => prev.filter((_, i) => i !== index));
  };

  const handleItemChange = (index: number, field: keyof PackedItem, value: any) => {
    setPackedItems(prev => {
      const next = [...prev];
      next[index] = {
        ...next[index],
        [field]: value
      };
      return next;
    });
  };

  // Perform Weight validation by calling backend endpoint
  const handleValidateWeight = async () => {
    setLoadingValidation(true);
    try {
      const itemsPayload = packedItems
        .filter(item => item.ref.trim() !== '')
        .map(item => ({
          ref: item.ref,
          quantidade: Number(item.quantidade),
          pesoUnitarioKg: Number(item.peso_unitario_kg)
        }));

      const payload = {
        actualWeight: actualWeight,
        boxTare: Number(boxTare),
        packedItems: itemsPayload,
        tolerancePercent: 2.0 // Standard 2%
      };

      const response = await fetch('/api/logistica/packing/validate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (response.ok) {
        const result: WeightValidationResult = await response.json();
        setValidationResult(result);
      } else {
        const localResult = validatePackingWeight(actualWeight, packedItems, boxTare);
        setValidationResult(localResult);
      }
    } catch (e) {
      console.error('Error validating packing weight via API:', e);
      const localResult = validatePackingWeight(actualWeight, packedItems, boxTare);
      setValidationResult(localResult);
    } finally {
      setLoadingValidation(false);
    }
  };

  // Auto-validate when actual weight changes to make the interface feel alive
  useEffect(() => {
    if (packedItems.length > 0 && packedItems[0].ref !== '') {
      const localResult = validatePackingWeight(actualWeight, packedItems, boxTare);
      setValidationResult(localResult);
    }
  }, [actualWeight, boxTare, packedItems]);

  return (
    <section className="tab-pane">
      <div className="pane-header">
        <h2>Packing Bench & Pesagem Automatizada</h2>
        <p>Realize a pesagem de caixas usando a Web Serial API ou o simulador de balança, verificando desvios em relação ao peso teórico.</p>
      </div>

      <div className="grid-layout">
        <div className="glass-card">
          <h3>
            <Scale /> Balança Eletrónica (Pesagem Física)
          </h3>

          <div className="scale-screen">
            <div className="scale-glow"></div>
            <span className="scale-lbl">
              {scaleConnected ? 'Comunicação Serial RS232 Ativa' : 'Simulador / Balança Desligada'}
            </span>
            <span className="scale-weight">
              {actualWeight.toFixed(3)}
            </span>
            <span className="scale-unit">KG</span>
          </div>

          <div className="btn-group">
            {!scaleConnected ? (
              <button className="btn btn-primary" onClick={handleConnectScale}>
                <Plug size={16} /> Ligar Balança (Web Serial)
              </button>
            ) : (
              <button className="btn btn-danger" onClick={handleDisconnectScale}>
                <Unplug size={16} /> Desligar Balança
              </button>
            )}
          </div>

          <div className="sim-slider-box">
            <label htmlFor="sim-weight-range">
              <strong>Simulador Manual de Balança (Deslize para alterar peso):</strong>
            </label>
            <input
              id="sim-weight-range"
              type="range"
              min="0"
              max="30"
              step="0.01"
              value={actualWeight}
              onChange={handleSliderChange}
              disabled={scaleConnected}
            />
            <div className="slider-labels">
              <span>0.00 Kg</span>
              <span style={{ color: 'var(--color-info)', fontWeight: 600 }}>{actualWeight.toFixed(2)} Kg</span>
              <span>30.00 Kg</span>
            </div>
          </div>
        </div>

        <div className="glass-card">
          <h3>
            <Weight /> Controlo de Tolerância de Peso
          </h3>

          <div className="packing-form">
            <div className="form-group">
              <label htmlFor="box-tare">Peso Tara da Caixa de Cartão (Kg):</label>
              <input
                id="box-tare"
                type="number"
                step="0.01"
                value={boxTare}
                onChange={(e) => setBoxTare(parseFloat(e.target.value) || 0)}
              />
            </div>

            <div className="form-group">
              <label>Artigos Embalados no Volume:</label>
              <div className="packed-items-list">
                {packedItems.map((item, index) => (
                  <div key={index} className="packed-item-row">
                    <input
                      type="text"
                      placeholder="Ref SKU"
                      value={item.ref}
                      onChange={(e) => handleItemChange(index, 'ref', e.target.value)}
                    />
                    <input
                      type="number"
                      placeholder="Qtd"
                      value={item.quantidade}
                      onChange={(e) => handleItemChange(index, 'quantidade', parseInt(e.target.value, 10) || 0)}
                      min="1"
                    />
                    <input
                      type="number"
                      placeholder="Peso"
                      step="0.001"
                      value={item.peso_unitario_kg}
                      onChange={(e) => handleItemChange(index, 'peso_unitario_kg', parseFloat(e.target.value) || 0)}
                    />
                    <button className="remove-row-btn" onClick={() => handleRemoveItemRow(index)}>
                      <Trash2 size={16} />
                    </button>
                  </div>
                ))}
              </div>

              <button className="btn btn-secondary btn-sm" onClick={handleAddItemRow} style={{ marginTop: '8px', alignSelf: 'flex-start' }}>
                <Plus size={14} /> Adicionar Linha
              </button>
            </div>

            <button
              className="btn btn-primary full-width-btn"
              onClick={handleValidateWeight}
              disabled={loadingValidation}
            >
              Validar Peso do Volume
            </button>
          </div>
        </div>

        {validationResult && (
          <div className="glass-card full-width">
            <h3>Resultado da Validação de Cubagem</h3>
            
            <div style={{ display: 'flex', gap: '32px', alignItems: 'center' }}>
              <div
                style={{
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                  justifyContent: 'center',
                  width: '120px',
                  height: '120px',
                  borderRadius: '50%',
                  backgroundColor: validationResult.isValid ? 'var(--glow-success)' : 'var(--glow-danger)',
                  border: `2px solid ${validationResult.isValid ? 'var(--color-success)' : 'var(--color-danger)'}`,
                  boxShadow: `0 0 15px ${validationResult.isValid ? 'var(--glow-success)' : 'var(--glow-danger)'}`,
                  color: validationResult.isValid ? 'var(--color-success)' : 'var(--color-danger)'
                }}
              >
                {validationResult.isValid ? <CheckCircle2 size={40} /> : <AlertOctagon size={40} />}
                <span style={{ fontSize: '12px', fontWeight: 700, marginTop: '8px', textTransform: 'uppercase' }}>
                  {validationResult.isValid ? 'Conforme' : 'Bloqueado'}
                </span>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '20px', flexGrow: 1 }}>
                <div style={{ background: 'rgba(0,0,0,0.15)', padding: '12px', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
                  <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>PESO LIDO (REAL)</span>
                  <div style={{ fontSize: '20px', fontWeight: 700, marginTop: '4px' }}>{actualWeight.toFixed(3)} KG</div>
                </div>
                <div style={{ background: 'rgba(0,0,0,0.15)', padding: '12px', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
                  <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>PESO TEÓRICO</span>
                  <div style={{ fontSize: '20px', fontWeight: 700, marginTop: '4px' }}>{validationResult.theoreticalWeight.toFixed(3)} KG</div>
                </div>
                <div style={{ background: 'rgba(0,0,0,0.15)', padding: '12px', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
                  <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>TOLERÂNCIA PERMITIDA</span>
                  <div style={{ fontSize: '13px', fontWeight: 600, marginTop: '6px', color: 'var(--text-main)' }}>
                    [{validationResult.minAllowed.toFixed(3)} ~ {validationResult.maxAllowed.toFixed(3)}] KG
                  </div>
                </div>
                <div style={{ background: 'rgba(0,0,0,0.15)', padding: '12px', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
                  <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>DESVIO</span>
                  <div style={{ fontSize: '20px', fontWeight: 700, marginTop: '4px', color: validationResult.isValid ? 'var(--color-success)' : 'var(--color-danger)' }}>
                    {validationResult.deviation > 0 ? `+${validationResult.deviation.toFixed(3)}` : validationResult.deviation.toFixed(3)} KG
                  </div>
                </div>
              </div>
            </div>

            {!validationResult.isValid && (
              <div className="warning-alert" style={{ marginTop: '20px' }}>
                <AlertOctagon />
                <span>
                  <strong>Aviso de Segurança:</strong> O peso real diverge do peso teórico expectável em mais de 2%. O volume foi travado na estação de packing para verificação manual de artigos e evitar erros de expedição.
                </span>
              </div>
            )}
          </div>
        )}
      </div>
    </section>
  );
};
