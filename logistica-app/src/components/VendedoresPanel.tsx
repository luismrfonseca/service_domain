import React, { useState, useEffect } from 'react';
import { Users, ShoppingBag, DollarSign, TrendingUp, Plus, Search, FileText, CheckCircle2, RefreshCw, Edit2, ShoppingCart, Trash2, Save, X } from 'lucide-react';

interface Cliente {
  id: string;
  no: number;
  nome: string;
  nomeFiscal: string;
  email?: string;
  phcStamp: string;
  updatedAt: string;
}

interface Produto {
  id: string;
  ref: string;
  designacao: string;
  phcStamp: string;
  gtin?: string;
  pesoUnitarioKg: number;
  volumeUnitarioM3: number;
}

interface StockItem {
  id: string;
  ref: string;
  loteCodigo?: string;
  armazem: number;
  localizacao?: string;
  quantidade: number;
  phcStamp?: string;
}

interface EncomendaLinha {
  id: string;
  ref: string;
  designacao: string;
  quantidade: number;
  preco: number;
  lote?: string;
  localizacao?: string;
  phcStamp?: string;
}

interface Encomenda {
  id: string;
  tipo: number;
  documentoNo: number;
  clienteNo: number;
  total: number;
  status: string;
  phcStamp?: string;
  createdAt: string;
  linhas: EncomendaLinha[];
}

interface DashboardStats {
  totalClientes: number;
  totalEncomendas: number;
  valorTotalVendas: number;
  encomendasPendentesSync: number;
  encomendasSincronizadas: number;
  encomendasErroSync: number;
}

type SubTabType = 'dashboard' | 'clientes' | 'nova-encomenda' | 'historico';

export const VendedoresPanel: React.FC = () => {
  const [subTab, setSubTab] = useState<SubTabType>('dashboard');
  const [loading, setLoading] = useState<boolean>(false);

  // Data States
  const [stats, setStats] = useState<DashboardStats>({
    totalClientes: 0,
    totalEncomendas: 0,
    valorTotalVendas: 0,
    encomendasPendentesSync: 0,
    encomendasSincronizadas: 0,
    encomendasErroSync: 0
  });
  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [produtos, setProdutos] = useState<Produto[]>([]);
  const [stocks, setStocks] = useState<StockItem[]>([]);
  const [encomendas, setEncomendas] = useState<Encomenda[]>([]);

  // Search & Filters
  const [searchClient, setSearchClient] = useState('');
  const [searchProduct, setSearchProduct] = useState('');

  // Forms / Actions
  const [isClientModalOpen, setIsClientModalOpen] = useState(false);
  const [editingCliente, setEditingCliente] = useState<Cliente | null>(null);
  const [clientForm, setClientForm] = useState({
    nome: '',
    nomeFiscal: '',
    email: ''
  });

  // Cart Builder State
  const [selectedClientNo, setSelectedClientNo] = useState<number | ''>('');
  const [cart, setCart] = useState<{
    ref: string;
    designacao: string;
    quantidade: number;
    preco: number;
    lote: string;
  }[]>([]);
  const [selectedProductRef, setSelectedProductRef] = useState('');
  const [selectedProductQty, setSelectedProductQty] = useState<number>(1);
  const [selectedProductPrice, setSelectedProductPrice] = useState<number>(0);
  const [selectedProductLote, setSelectedProductLote] = useState('');

  // Fetch Methods
  const fetchDashboardStats = async () => {
    try {
      const response = await fetch('/api/vendedores/dashboard');
      if (response.ok) {
        const data = await response.json();
        setStats({
          totalClientes: data.totalClientes,
          totalEncomendas: data.totalEncomendas,
          valorTotalVendas: data.valorTotalVendas,
          encomendasPendentesSync: data.encomendasPendentesSync,
          encomendasSincronizadas: data.encomendasSincronizadas,
          encomendasErroSync: data.encomendasErroSync
        });
      }
    } catch (e) {
      console.error('Error fetching dashboard stats', e);
    }
  };

  const fetchClientes = async () => {
    try {
      const response = await fetch('/api/clientes');
      if (response.ok) {
        const data = await response.json();
        setClientes(data);
      }
    } catch (e) {
      console.error('Error fetching clientes', e);
    }
  };

  const fetchProdutos = async () => {
    try {
      const response = await fetch('/api/produtos');
      if (response.ok) {
        const data = await response.json();
        setProdutos(data);
      }
    } catch (e) {
      console.error('Error fetching produtos', e);
    }
  };

  const fetchStocks = async () => {
    try {
      const response = await fetch('/api/stocks');
      if (response.ok) {
        const data = await response.json();
        setStocks(data);
      }
    } catch (e) {
      console.error('Error fetching stocks', e);
    }
  };

  const fetchEncomendas = async () => {
    try {
      const response = await fetch('/api/vendedores/encomendas');
      if (response.ok) {
        const data = await response.json();
        setEncomendas(data);
      }
    } catch (e) {
      console.error('Error fetching encomendas', e);
    }
  };

  const loadAllData = async () => {
    setLoading(true);
    await Promise.all([
      fetchDashboardStats(),
      fetchClientes(),
      fetchProdutos(),
      fetchStocks(),
      fetchEncomendas()
    ]);
    setLoading(false);
  };

  useEffect(() => {
    loadAllData();
  }, [subTab]);

  // Client CRUD Actions
  const handleOpenCreateModal = () => {
    setEditingCliente(null);
    setClientForm({ nome: '', nomeFiscal: '', email: '' });
    setIsClientModalOpen(true);
  };

  const handleOpenEditModal = (cliente: Cliente) => {
    setEditingCliente(cliente);
    setClientForm({
      nome: cliente.nome,
      nomeFiscal: cliente.nomeFiscal,
      email: cliente.email || ''
    });
    setIsClientModalOpen(true);
  };

  const handleSaveCliente = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!clientForm.nome || !clientForm.nomeFiscal) {
      alert('Nome e Nome Fiscal (NIF) são obrigatórios.');
      return;
    }

    try {
      let url = '/api/clientes';
      let method = 'POST';

      if (editingCliente) {
        url = `/api/clientes/${editingCliente.id}`;
        method = 'PUT';
      }

      const response = await fetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(clientForm)
      });

      if (response.ok) {
        setIsClientModalOpen(false);
        await fetchClientes();
        await fetchDashboardStats();
        alert(editingCliente ? 'Cliente atualizado localmente!' : 'Cliente registado localmente!');
      } else {
        const errorData = await response.json();
        alert(`Erro: ${errorData.message || 'Falha ao guardar cliente.'}`);
      }
    } catch (e) {
      console.error(e);
      alert('Erro de comunicação.');
    }
  };

  // Cart Add/Remove Actions
  const handleAddProductToCart = () => {
    if (!selectedProductRef) return;
    const prod = produtos.find(p => p.ref === selectedProductRef);
    if (!prod) return;

    // Check if item already exists in cart with same lote
    const existingIndex = cart.findIndex(item => item.ref === selectedProductRef && item.lote === selectedProductLote);
    if (existingIndex > -1) {
      const newCart = [...cart];
      newCart[existingIndex].quantidade += selectedProductQty;
      setCart(newCart);
    } else {
      setCart([...cart, {
        ref: selectedProductRef,
        designacao: prod.designacao,
        quantidade: selectedProductQty,
        preco: selectedProductPrice,
        lote: selectedProductLote
      }]);
    }

    // Reset line selections
    setSelectedProductRef('');
    setSelectedProductQty(1);
    setSelectedProductPrice(0);
    setSelectedProductLote('');
  };

  const handleRemoveFromCart = (index: number) => {
    const newCart = [...cart];
    newCart.splice(index, 1);
    setCart(newCart);
  };

  // Order Submission
  const handlePlaceOrder = async () => {
    if (!selectedClientNo) {
      alert('Por favor, selecione um cliente.');
      return;
    }
    if (cart.length === 0) {
      alert('O carrinho de compras está vazio.');
      return;
    }

    try {
      const payload = {
        clienteNo: Number(selectedClientNo),
        linhas: cart.map(item => ({
          ref: item.ref,
          designacao: item.designacao,
          quantidade: item.quantidade,
          preco: item.preco,
          lote: item.lote || null
        }))
      };

      const response = await fetch('/api/encomendas', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (response.ok) {
        setCart([]);
        setSelectedClientNo('');
        alert('Encomenda submetida com sucesso localmente! Sincronização pendente.');
        setSubTab('historico');
      } else {
        const errorData = await response.json();
        alert(`Erro ao criar encomenda: ${errorData.message || 'Falha local.'}`);
      }
    } catch (e) {
      console.error(e);
      alert('Erro de rede ao submeter encomenda.');
    }
  };

  // Auto-fill price and lote when product is selected in order tab
  const handleProductSelectChange = (ref: string) => {
    setSelectedProductRef(ref);
    // Prefill price (placeholders or standard price simulation)
    setSelectedProductPrice(1.50);
    // Prefill first available lot for this product
    const lotesForProd = stocks.filter(s => s.ref === ref && s.loteCodigo);
    if (lotesForProd.length > 0 && lotesForProd[0].loteCodigo) {
      setSelectedProductLote(lotesForProd[0].loteCodigo);
    } else {
      setSelectedProductLote('');
    }
  };

  // Filter clients and products
  const filteredClientes = clientes.filter(c => 
    c.nome.toLowerCase().includes(searchClient.toLowerCase()) ||
    c.nomeFiscal.includes(searchClient) ||
    (c.email && c.email.toLowerCase().includes(searchClient.toLowerCase()))
  );

  const filteredProdutos = produtos.filter(p =>
    p.ref.toLowerCase().includes(searchProduct.toLowerCase()) ||
    p.designacao.toLowerCase().includes(searchProduct.toLowerCase())
  );

  // Cart total computation
  const cartTotal = cart.reduce((sum, item) => sum + (item.quantidade * item.preco), 0);

  return (
    <section className="tab-pane">
      <div className="pane-header">
        <h2>Portal Web de Vendedores</h2>
        <p>Gestão de Clientes, consulta de catálogo com stock em tempo real, colocação de encomendas Offline-First e logs de sincronização ao PHC CS.</p>
      </div>

      {/* Sub-tabs Navigation */}
      <div className="btn-group" style={{ marginBottom: '10px' }}>
        <button
          className={`btn ${subTab === 'dashboard' ? 'btn-primary' : 'btn-secondary'}`}
          onClick={() => setSubTab('dashboard')}
        >
          <TrendingUp size={16} /> Painel Geral
        </button>
        <button
          className={`btn ${subTab === 'clientes' ? 'btn-primary' : 'btn-secondary'}`}
          onClick={() => setSubTab('clientes')}
        >
          <Users size={16} /> Clientes
        </button>
        <button
          className={`btn ${subTab === 'nova-encomenda' ? 'btn-primary' : 'btn-secondary'}`}
          onClick={() => setSubTab('nova-encomenda')}
        >
          <ShoppingCart size={16} /> Nova Encomenda
        </button>
        <button
          className={`btn ${subTab === 'historico' ? 'btn-primary' : 'btn-secondary'}`}
          onClick={() => setSubTab('historico')}
        >
          <FileText size={16} /> Histórico & Outbox
        </button>
        <button className="btn btn-secondary" onClick={loadAllData} disabled={loading}>
          <RefreshCw size={16} className={loading ? 'animate-spin' : ''} /> Atualizar Dados
        </button>
      </div>

      {subTab === 'dashboard' && (
        <div className="grid-layout">
          {/* Dashboard Summary Cards */}
          <div className="glass-card" style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
            <h3><TrendingUp /> Desempenho e Clientes</h3>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '16px' }}>
              <div style={{ background: 'rgba(255,255,255,0.03)', padding: '20px', borderRadius: '12px', border: '1px solid var(--border-color)', textAlign: 'center' }}>
                <Users size={28} style={{ color: 'var(--color-primary)', margin: '0 auto 10px' }} />
                <div style={{ fontSize: '32px', fontWeight: 800 }}>{stats.totalClientes}</div>
                <div style={{ fontSize: '12px', color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.5px' }}>Clientes Registados</div>
              </div>
              <div style={{ background: 'rgba(255,255,255,0.03)', padding: '20px', borderRadius: '12px', border: '1px solid var(--border-color)', textAlign: 'center' }}>
                <DollarSign size={28} style={{ color: 'var(--color-success)', margin: '0 auto 10px' }} />
                <div style={{ fontSize: '32px', fontWeight: 800 }}>{stats.valorTotalVendas.toFixed(2)} €</div>
                <div style={{ fontSize: '12px', color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.5px' }}>Total de Vendas</div>
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: 'rgba(99, 102, 241, 0.1)', padding: '16px', borderRadius: '12px', border: '1px solid rgba(99, 102, 241, 0.2)' }}>
              <div>
                <h4 style={{ fontWeight: 600 }}>Registou um cliente novo?</h4>
                <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>O registo entra imediatamente em fila de sincronização.</p>
              </div>
              <button className="btn btn-primary" onClick={() => setSubTab('clientes')}>
                Ir para Clientes
              </button>
            </div>
          </div>

          <div className="glass-card">
            <h3><ShoppingBag /> Estado da Sincronização (Outbox)</h3>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginTop: '10px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '12px', background: 'rgba(0,0,0,0.15)', borderRadius: '8px', borderLeft: '4px solid var(--color-info)' }}>
                <span style={{ fontWeight: 500 }}>Total de Encomendas de Vendas</span>
                <span style={{ fontWeight: 700 }}>{stats.totalEncomendas}</span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '12px', background: 'rgba(0,0,0,0.15)', borderRadius: '8px', borderLeft: '4px solid var(--color-warning)' }}>
                <span style={{ fontWeight: 500 }}>Aguardar Sincronização (Pendente)</span>
                <span style={{ fontWeight: 700, color: 'var(--color-warning)' }}>{stats.encomendasPendentesSync}</span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '12px', background: 'rgba(0,0,0,0.15)', borderRadius: '8px', borderLeft: '4px solid var(--color-success)' }}>
                <span style={{ fontWeight: 500 }}>Sincronizadas no PHC CS</span>
                <span style={{ fontWeight: 700, color: 'var(--color-success)' }}>{stats.encomendasSincronizadas}</span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '12px', background: 'rgba(0,0,0,0.15)', borderRadius: '8px', borderLeft: '4px solid var(--color-danger)' }}>
                <span style={{ fontWeight: 500 }}>Erros de Sincronização</span>
                <span style={{ fontWeight: 700, color: 'var(--color-danger)' }}>{stats.encomendasErroSync}</span>
              </div>
            </div>
          </div>
        </div>
      )}

      {subTab === 'clientes' && (
        <div className="glass-card full-width">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
            <h3><Users /> Base de Clientes do Vendedor</h3>
            <button className="btn btn-primary" onClick={handleOpenCreateModal}>
              <Plus size={16} /> Novo Cliente
            </button>
          </div>

          <div className="search-box" style={{ width: '100%', marginBottom: '20px' }}>
            <Search />
            <input
              type="text"
              placeholder="Pesquisar por nome, NIF ou email..."
              value={searchClient}
              onChange={(e) => setSearchClient(e.target.value)}
            />
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table className="wms-table">
              <thead>
                <tr>
                  <th>Nº Cliente</th>
                  <th>Nome</th>
                  <th>NIF</th>
                  <th>Email</th>
                  <th>Código Stamp PHC</th>
                  <th>Estado Sync</th>
                  <th>Ações</th>
                </tr>
              </thead>
              <tbody>
                {filteredClientes.map((c) => {
                  const isPending = c.phcStamp.startsWith('PENDENTE-');
                  return (
                    <tr key={c.id}>
                      <td style={{ fontWeight: 700 }}>{c.no === 0 ? 'A definir...' : c.no}</td>
                      <td>{c.nome}</td>
                      <td><code>{c.nomeFiscal}</code></td>
                      <td>{c.email || <span style={{ color: 'var(--text-muted)', fontStyle: 'italic' }}>Não disponível</span>}</td>
                      <td><code style={{ fontSize: '11px' }}>{c.phcStamp}</code></td>
                      <td>
                        <span className={`badge ${isPending ? 'badge-warning' : 'badge-success'}`}>
                          {isPending ? 'Pendente ERP' : 'Sincronizado'}
                        </span>
                      </td>
                      <td>
                        <button className="btn btn-secondary" style={{ padding: '6px 12px', fontSize: '12px' }} onClick={() => handleOpenEditModal(c)}>
                          <Edit2 size={12} /> Editar
                        </button>
                      </td>
                    </tr>
                  );
                })}
                {filteredClientes.length === 0 && (
                  <tr>
                    <td colSpan={7} style={{ textAlign: 'center', color: 'var(--text-muted)', padding: '24px' }}>
                      Nenhum cliente encontrado.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {subTab === 'nova-encomenda' && (
        <div className="grid-layout">
          {/* Order Creator Form */}
          <div className="glass-card">
            <h3><ShoppingCart /> Construtor de Encomenda</h3>

            <div className="form-group">
              <label htmlFor="order-client">Cliente de Faturação:</label>
              <select
                id="order-client"
                value={selectedClientNo}
                onChange={(e) => setSelectedClientNo(e.target.value === '' ? '' : Number(e.target.value))}
              >
                <option value="">-- Selecione o Cliente --</option>
                {clientes.map(c => (
                  <option key={c.id} value={c.no}>
                    {c.nome} (NIF: {c.nomeFiscal})
                  </option>
                ))}
              </select>
            </div>

            <div style={{ border: '1px solid var(--border-color)', padding: '16px', borderRadius: '8px', background: 'rgba(0,0,0,0.15)', marginTop: '20px' }}>
              <h4 style={{ fontSize: '14px', fontWeight: 600, marginBottom: '12px' }}>Adicionar Linha de Artigo</h4>
              
              <div className="form-group">
                <label htmlFor="search-product">Filtrar Catálogo:</label>
                <div className="search-box" style={{ width: '100%', marginBottom: '8px', padding: '6px 12px' }}>
                  <Search size={14} />
                  <input
                    id="search-product"
                    type="text"
                    placeholder="Pesquisar por ref ou designação..."
                    value={searchProduct}
                    onChange={(e) => setSearchProduct(e.target.value)}
                    style={{ fontSize: '13px' }}
                  />
                </div>
              </div>

              <div className="form-group">
                <label htmlFor="order-product">Artigo:</label>
                <select
                  id="order-product"
                  value={selectedProductRef}
                  onChange={(e) => handleProductSelectChange(e.target.value)}
                >
                  <option value="">-- Selecione o Artigo --</option>
                  {filteredProdutos.map(p => (
                    <option key={p.id} value={p.ref}>
                      {p.ref} - {p.designacao}
                    </option>
                  ))}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '12px' }}>
                <div className="form-group">
                  <label htmlFor="order-qty">Quantidade:</label>
                  <input
                    id="order-qty"
                    type="number"
                    min={1}
                    value={selectedProductQty}
                    onChange={(e) => setSelectedProductQty(Number(e.target.value))}
                  />
                </div>
                <div className="form-group">
                  <label htmlFor="order-price">Preço Unitário (€):</label>
                  <input
                    id="order-price"
                    type="number"
                    step="0.01"
                    min={0}
                    value={selectedProductPrice}
                    onChange={(e) => setSelectedProductPrice(Number(e.target.value))}
                  />
                </div>
              </div>

              <div className="form-group">
                <label htmlFor="order-lote">Lote a Reservar:</label>
                <select
                  id="order-lote"
                  value={selectedProductLote}
                  onChange={(e) => setSelectedProductLote(e.target.value)}
                >
                  <option value="">Sem Lote (Geral)</option>
                  {stocks
                    .filter(s => s.ref === selectedProductRef && s.loteCodigo)
                    .map(s => (
                      <option key={s.id} value={s.loteCodigo}>
                        Lote: {s.loteCodigo} (Disp: {s.quantidade} UN)
                      </option>
                    ))
                  }
                </select>
              </div>

              <button
                className="btn btn-secondary"
                style={{ width: '100%', marginTop: '10px' }}
                onClick={handleAddProductToCart}
                disabled={!selectedProductRef}
              >
                <Plus size={16} /> Adicionar Linha
              </button>
            </div>

            <div style={{ marginTop: '24px' }}>
              <button
                className="btn btn-primary"
                style={{ width: '100%' }}
                onClick={handlePlaceOrder}
                disabled={cart.length === 0 || !selectedClientNo}
              >
                <CheckCircle2 size={16} /> Submeter Encomenda de Venda
              </button>
            </div>
          </div>

          {/* Cart Details */}
          <div className="glass-card">
            <h3><ShoppingCart /> Carrinho de Compras ({cart.length} Linhas)</h3>

            <div className="cart-list" style={{ minHeight: '280px', maxHeight: '400px', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: '10px' }}>
              {cart.map((item, index) => (
                <div key={index} style={{ background: 'rgba(255,255,255,0.03)', border: '1px solid var(--border-color)', borderRadius: '8px', padding: '12px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <div>
                    <div style={{ fontWeight: 600, fontSize: '14px' }}>{item.ref} - {item.designacao}</div>
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
                      Qtd: <span style={{ color: 'var(--text-main)', fontWeight: 600 }}>{item.quantidade}</span> | 
                      Preço: <span style={{ color: 'var(--text-main)', fontWeight: 600 }}>{item.preco.toFixed(3)} €</span> | 
                      Lote: <span style={{ color: 'var(--text-main)' }}>{item.lote || 'N/A'}</span>
                    </div>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <div style={{ fontWeight: 700, color: 'var(--color-success)', fontSize: '15px' }}>
                      {(item.quantidade * item.preco).toFixed(2)} €
                    </div>
                    <button
                      className="btn"
                      style={{ background: 'rgba(239, 68, 68, 0.1)', color: 'var(--color-danger)', border: 'none', padding: '6px 8px' }}
                      onClick={() => handleRemoveFromCart(index)}
                    >
                      <Trash2 size={14} />
                    </button>
                  </div>
                </div>
              ))}
              {cart.length === 0 && (
                <div style={{ textAlign: 'center', color: 'var(--text-muted)', padding: '60px 0' }}>
                  <ShoppingCart size={40} style={{ opacity: 0.2, margin: '0 auto 10px' }} />
                  <span>O carrinho está vazio. Adicione artigos.</span>
                </div>
              )}
            </div>

            <div style={{ borderTop: '1px solid var(--border-color)', paddingTop: '16px', marginTop: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ fontWeight: 600, fontSize: '16px', color: 'var(--text-muted)' }}>VALOR TOTAL:</span>
              <span style={{ fontWeight: 800, fontSize: '24px', color: 'var(--color-success)' }}>{cartTotal.toFixed(2)} €</span>
            </div>
          </div>
        </div>
      )}

      {subTab === 'historico' && (
        <div className="glass-card full-width">
          <h3><FileText /> Histórico de Encomendas & Logs de Sincronização</h3>
          
          <div style={{ overflowX: 'auto', marginTop: '20px' }}>
            <table className="wms-table">
              <thead>
                <tr>
                  <th>Nº Encomenda</th>
                  <th>Nº Cliente</th>
                  <th>Data</th>
                  <th>Total (€)</th>
                  <th>Código Stamp PHC</th>
                  <th>Estado Outbox</th>
                  <th>Visualizar Linhas</th>
                </tr>
              </thead>
              <tbody>
                {encomendas.map((e) => {
                  let statusBadge = 'badge-warning';
                  if (e.status === 'Sincronizado') statusBadge = 'badge-success';
                  if (e.status === 'Erro') statusBadge = 'badge-danger';

                  return (
                    <React.Fragment key={e.id}>
                      <tr>
                        <td style={{ fontWeight: 700 }}>{e.documentoNo === 0 ? 'Pendente...' : `ENC ${e.documentoNo}`}</td>
                        <td>{e.clienteNo}</td>
                        <td>{new Date(e.createdAt).toLocaleString()}</td>
                        <td style={{ fontWeight: 700, color: 'var(--color-success)' }}>{e.total.toFixed(2)} €</td>
                        <td><code style={{ fontSize: '11px' }}>{e.phcStamp || 'A aguardar sync...'}</code></td>
                        <td>
                          <span className={`badge ${statusBadge}`}>
                            {e.status === 'PendenteSync' ? 'Pendente' : e.status}
                          </span>
                        </td>
                        <td>
                          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', fontSize: '11px' }}>
                            {e.linhas.map((l, idx) => (
                              <div key={idx}>
                                <code>{l.ref}</code> ({l.quantidade} UN @ {l.preco.toFixed(2)}€)
                              </div>
                            ))}
                          </div>
                        </td>
                      </tr>
                    </React.Fragment>
                  );
                })}
                {encomendas.length === 0 && (
                  <tr>
                    <td colSpan={7} style={{ textAlign: 'center', color: 'var(--text-muted)', padding: '24px' }}>
                      Nenhuma encomenda encontrada.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Create/Edit Client Modal Dialog */}
      {isClientModalOpen && (
        <div className="modal-backdrop" style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.6)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
          <div className="glass-card" style={{ width: '450px', background: '#0e1320', border: '1px solid rgba(255,255,255,0.1)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid var(--border-color)', paddingBottom: '12px', marginBottom: '20px' }}>
              <h3 style={{ margin: 0, border: 'none', padding: 0 }}>
                {editingCliente ? 'Editar Cliente' : 'Registar Novo Cliente'}
              </h3>
              <button
                className="btn"
                style={{ background: 'none', border: 'none', color: 'var(--text-muted)', padding: '4px' }}
                onClick={() => setIsClientModalOpen(false)}
              >
                <X size={18} />
              </button>
            </div>

            <form onSubmit={handleSaveCliente} style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              <div className="form-group">
                <label htmlFor="client-nome">Nome do Cliente:</label>
                <input
                  id="client-nome"
                  type="text"
                  placeholder="Ex: Armazéns Aliança, S.A."
                  value={clientForm.nome}
                  onChange={(e) => setClientForm({ ...clientForm, nome: e.target.value })}
                  required
                />
              </div>

              <div className="form-group">
                <label htmlFor="client-nif">NIF / Nome Fiscal:</label>
                <input
                  id="client-nif"
                  type="text"
                  placeholder="Ex: PT501987654"
                  value={clientForm.nomeFiscal}
                  onChange={(e) => setClientForm({ ...clientForm, nomeFiscal: e.target.value })}
                  required
                />
              </div>

              <div className="form-group">
                <label htmlFor="client-email">Endereço de Email:</label>
                <input
                  id="client-email"
                  type="email"
                  placeholder="Ex: compras@alianca.pt"
                  value={clientForm.email}
                  onChange={(e) => setClientForm({ ...clientForm, email: e.target.value })}
                />
              </div>

              <div className="btn-group" style={{ justifyContent: 'flex-end', marginTop: '10px' }}>
                <button type="button" className="btn btn-secondary" onClick={() => setIsClientModalOpen(false)}>
                  Cancelar
                </button>
                <button type="submit" className="btn btn-primary">
                  <Save size={16} /> Gravar
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </section>
  );
};
