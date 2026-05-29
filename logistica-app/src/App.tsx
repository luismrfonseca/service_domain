import { useState, useEffect } from 'react';
import { Package, ArrowDown, ClipboardList, Compass, Box, Truck, RotateCcw, Search, User } from 'lucide-react';
import { InboundPanel } from './components/InboundPanel';
import { InventoryPanel } from './components/InventoryPanel';
import { PickingPanel } from './components/PickingPanel';
import { PackingPanel } from './components/PackingPanel';
import { ShippingPanel } from './components/ShippingPanel';
import { RmaPanel } from './components/RmaPanel';

type TabType = 'inbound' | 'inventory' | 'picking' | 'packing' | 'shipping' | 'rma';

function App() {
  const [activeTab, setActiveTab] = useState<TabType>('inbound');
  const [apiOnline, setApiOnline] = useState<boolean>(true);
  const [searchQuery, setSearchQuery] = useState<string>('');

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
    // Run periodically
    const interval = setInterval(checkApiHealth, 10000);
    return () => clearInterval(interval);
  }, []);

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
      default:
        return <InboundPanel />;
    }
  };

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
            onClick={() => setActiveTab('inbound')}
          >
            <ArrowDown /> <span>Receção GS1</span>
          </button>
          
          <button
            className={`nav-btn ${activeTab === 'inventory' ? 'active' : ''}`}
            onClick={() => setActiveTab('inventory')}
          >
            <ClipboardList /> <span>Contagem Cíclica</span>
          </button>
          
          <button
            className={`nav-btn ${activeTab === 'picking' ? 'active' : ''}`}
            onClick={() => setActiveTab('picking')}
          >
            <Compass /> <span>Picking Serpentina</span>
          </button>
          
          <button
            className={`nav-btn ${activeTab === 'packing' ? 'active' : ''}`}
            onClick={() => setActiveTab('packing')}
          >
            <Box /> <span>Packing & Balança</span>
          </button>
          
          <button
            className={`nav-btn ${activeTab === 'shipping' ? 'active' : ''}`}
            onClick={() => setActiveTab('shipping')}
          >
            <Truck /> <span>Expedição & AT</span>
          </button>
          
          <button
            className={`nav-btn ${activeTab === 'rma' ? 'active' : ''}`}
            onClick={() => setActiveTab('rma')}
          >
            <RotateCcw /> <span>Logística Inversa</span>
          </button>
        </nav>

        <div className="operator-profile">
          <div className="avatar">
            <User size={20} />
          </div>
          <div className="info">
            <h4>Operador WH-09</h4>
            <span>Online</span>
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
