// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
import { bridge } from '@hermes/bridge';
import GreetCard from './components/GreetCard';
import SystemInfo from './components/SystemInfo';
import './App.css';

function App() {
  return (
    <div className="app">
      <h1>Hermes Web</h1>
      <p className="subtitle">
        React + TypeScript SPA running in a native desktop window
      </p>
      <GreetCard />
      <SystemInfo />
      <p className="bridge-status">
        Hermes Bridge: {bridge.isHermes ? 'Connected' : 'Not available (running in browser)'}
      </p>
    </div>
  );
}

export default App;
