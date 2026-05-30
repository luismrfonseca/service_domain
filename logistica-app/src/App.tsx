import { useState, useEffect } from 'react';
import { Package, ArrowDown, ClipboardList, Compass, Box, Truck, RotateCcw, Search, User, LogOut, TrendingUp } from 'lucide-react';
import { InboundPanel } from './components/InboundPanel';
import { InventoryPanel } from './components/InventoryPanel';
import { PickingPanel } from './components/PickingPanel';
import { PackingPanel } from './components/PackingPanel';
import { ShippingPanel } from './components/ShippingPanel';
import { RmaPanel } from './components/RmaPanel';
import { LoginPanel } from './components/LoginPanel';
import { VendedoresPanel } from './components/VendedoresPanel';

type TabType = 'inbound' | 'inventory' | 'picking' | 'packing' | 'shipping' | 'rma' | 'vendedores';

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(() => {
    return !!localStorage.getItem('wms_operator');
  });
  const [operatorId, setOperatorId] = useState<string>(() => {
    return localStorage.getItem('wms_operator') || '';
  });
  const [activeTab, setActiveTab] = useState<TabType>(() => {
    // Read initial hash route
    const hash = window.location.hash.replace('#/', '');
    const validTabs: TabType[] = ['inbound', 'inventory', 'picking', 'packing', 'shipping', 'rma', 'vendedores'];
    if (validTabs.includes(hash as TabType)) {
      return hash as TabType;
    }
    return 'inbound';
  });
  
  const [apiOnline, setApiOnline] = useState<boolean>(true);
  const [searchQuery, setSearchQuery] = useState<string>('');

  // Handle URL hash changes for true SPA routing
  useEffect(() => {
    const handleHashChange = () => {
      const hash = window.location.hash.replace('#/', '');
      const validTabs: TabType[] = ['inbound', 'inventory', 'picking', 'packing', 'shipping', 'rma', 'vendedores'];
      
      if (hash === 'login') {
        if (isAuthenticated) {
          window.location.hash = '#/' + activeTab;
        }
      } else if (validTabs.includes(hash as TabType)) {
        if (!isAuthenticated) {
          window.location.hash = '#/login';
        } else {
          setActiveTab(hash as TabType);
        }
      } else {
        // Fallback or default redirect
        if (isAuthenticated) {
          window.location.hash = '#/' + activeTab;
        } else {
          window.location.hash = '#/login';
        }
      }
    };

    window.addEventListener('hashchange', handleHashChange);
    
    // Initial redirect check
    if (!isAuthenticated) {
      window.location.hash = '#/login';
    } else {
      const hash = window.location.hash.replace('#/', '');
      const validTabs: TabType[] = ['inbound', 'inventory', 'picking', 'packing', 'shipping', 'rma', 'vendedores'];
      if (!validTabs.includes(hash as TabType)) {
        window.location.hash = '#/' + activeTab;
      }
    }

    return () => window.removeEventListener('hashchange', handleHashChange);
  }, [isAuthenticated, activeTab]);

  // Ping backend to check API status dynamically
  const checkApiHealth = async () => {
    try {
      const response = await fetch('/api/stocks');
      if (response.ok) {
        setApiOnline(true);
      } else {
        setApiOnline(false);
      }
    } catch (e) {
      setApiOnline(false);
    }
  };

  useEffect(() => {
    checkApiHealth();
    const interval = setInterval(checkApiHealth, 10000);
    return () => clearInterval(interval);
  }, []);

  const handleLogin = (opId: string) => {
    localStorage.setItem('wms_operator', opId);
    setOperatorId(opId);
    setIsAuthenticated(true);
    window.location.hash = '#/inbound';
  };

  const handleLogout = () => {
    localStorage.removeItem('wms_operator');
    setOperatorId('');
    setIsAuthenticated(false);
    window.location.hash = '#/login';
  };

  const navigateTo = (tab: TabType) => {
    window.location.hash = '#/' + tab;
  };

  const renderActivePanel = () => {
    switch (activeTab) {
      case 'inbound':
        return <InboundPanel />;
      case 'inventory':
        return <InventoryPanel />;
      case 'picking':
        return <PickingPanel />;
      case 'packing':
        return <PackingPanel />;
      case 'shipping':
        return <ShippingPanel />;
      case 'rma':
        return <RmaPanel />;
      case 'vendedores':
        return <VendedoresPanel />;
      default:
        return <InboundPanel />;
    }
  };

  if (!isAuthenticated) {
    return <LoginPanel onLogin={handleLogin} />;
  }

  return (
    <div className="glass-app">
      {/* Sidebar Navigation */}
      <aside className="sidebar">
        <div className="logo-area">
          <Package className="logo-icon" />
          <div className="logo-text">
            <h1>WMS Hub</h1>
            <span>Logística & Stock</span>
          </div>
        </div>

        <nav className="nav-menu">
          <button
            className={`nav-btn ${activeTab === 'inbound' ? 'active' : ''}`}
            onClick={() => navigateTo('inbound')}
          >
            <ArrowDown /> <span>Receção GS1</span>
          </button>
          
          <button
            className={`nav-btn ${activeTab === 'inventory' ? 'active' : ''}`}
            onClick={() => navigateTo('inventory')}
          >
            <ClipboardList /> <span>Contagem Cíclica</span>
          </button>
          
          <button
            className={`nav-btn ${activeTab === 'picking' ? 'active' : ''}`}
            onClick={() => navigateTo('picking')}
          >
            <Compass /> <span>Picking Serpentina</span>
          </button>
          
          <button
            className={`nav-btn ${activeTab === 'packing' ? 'active' : ''}`}
            onClick={() => navigateTo('packing')}
          >
            <Box /> <span>Packing & Balança</span>
          </button>
          
          <button
            className={`nav-btn ${activeTab === 'shipping' ? 'active' : ''}`}
            onClick={() => navigateTo('shipping')}
          >
            <Truck /> <span>Expedição & AT</span>
          </button>
          
          <button
            className={`nav-btn ${activeTab === 'rma' ? 'active' : ''}`}
            onClick={() => navigateTo('rma')}
          >
            <RotateCcw /> <span>Logística Inversa</span>
          </button>

          <button
            className={`nav-btn ${activeTab === 'vendedores' ? 'active' : ''}`}
            onClick={() => navigateTo('vendedores')}
            id="nav-vendedores-btn"
          >
            <TrendingUp /> <span>Portal Vendedores</span>
          </button>
        </nav>

        <div className="sidebar-footer">
          <div className="operator-profile">
            <div className="avatar">
              <User size={20} />
            </div>
            <div className="info">
              <h4>Operador {operatorId}</h4>
              <span>Online</span>
            </div>
          </div>
          <div className="logout-btn-wrap">
            <button className="logout-btn" onClick={handleLogout}>
              <LogOut /> <span>Sair do Terminal</span>
            </button>
          </div>
        </div>
      </aside>

      {/* Main Content Area */}
      <main className="main-content">
        <header className="top-bar">
          <div className="search-box">
            <Search />
            <input
              type="text"
              placeholder="Pesquisar referências, lotes, localizações..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
          </div>
          <div className="system-status">
            <div className={`status-indicator ${apiOnline ? 'online' : 'offline'}`}></div>
            <span>{apiOnline ? 'API Conectada (ERP)' : 'API Sem Ligação'}</span>
          </div>
        </header>

        {/* Tab Panel */}
        {renderActivePanel()}
      </main>
    </div>
  );
}

export default App;
