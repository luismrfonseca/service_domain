import React, { useState } from 'react';
import { Package, Lock, User as UserIcon, AlertTriangle } from 'lucide-react';

interface LoginPanelProps {
  onLogin: (operatorId: string) => void;
}

export function LoginPanel({ onLogin }: LoginPanelProps) {
  const [operatorId, setOperatorId] = useState('WH-09');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!operatorId.trim()) {
      setError('Por favor introduza o código de operador.');
      return;
    }
    if (!password.trim()) {
      setError('Por favor introduza a senha de acesso.');
      return;
    }

    setIsLoading(true);
    setError(null);

    // Mock authentication: Operator WH-09, Password wms123 (or admin/admin)
    setTimeout(() => {
      setIsLoading(false);
      const normalizedOp = operatorId.trim().toUpperCase();
      if ((normalizedOp === 'WH-09' && password === 'wms123') || (normalizedOp === 'ADMIN' && password === 'admin') || password === 'wms123') {
        onLogin(operatorId.trim());
      } else {
        setError('Senha de acesso incorreta ou operador inválido. (Dica: utilize a senha "wms123")');
      }
    }, 850);
  };

  return (
    <div className="login-container">
      <div className="login-glow"></div>
      <div className="login-card glass-card">
        <div className="login-header">
          <div className="login-logo">
            <Package size={40} />
          </div>
          <h2>WMS Hub</h2>
          <p>Portal do Operador de Armazém</p>
        </div>

        <form onSubmit={handleSubmit} className="login-form">
          {error && (
            <div className="error-alert">
              <AlertTriangle size={18} />
              <span>{error}</span>
            </div>
          )}

          <div className="form-group">
            <label htmlFor="operator-id">Código do Operador</label>
            <div className="input-with-icon">
              <UserIcon size={18} className="input-icon" />
              <input
                id="operator-id"
                type="text"
                value={operatorId}
                onChange={(e) => setOperatorId(e.target.value)}
                placeholder="Ex: WH-09"
                disabled={isLoading}
                required
              />
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="password">Senha de Acesso</label>
            <div className="input-with-icon">
              <Lock size={18} className="input-icon" />
              <input
                id="password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Introduza a senha"
                disabled={isLoading}
                required
              />
            </div>
          </div>

          <button type="submit" className="btn btn-primary full-width-btn" disabled={isLoading}>
            {isLoading ? 'A autenticar...' : 'Entrar no Terminal'}
          </button>
        </form>

        <div className="login-footer">
          <span>Service Domain Logistics v1.0.0</span>
        </div>
      </div>
    </div>
  );
}
