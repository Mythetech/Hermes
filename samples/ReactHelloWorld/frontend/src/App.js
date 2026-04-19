import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { bridge } from '@hermes/bridge';
import GreetCard from './components/GreetCard';
import SystemInfo from './components/SystemInfo';
import './App.css';
function App() {
    return (_jsxs("div", { className: "app", children: [_jsx("h1", { children: "Hermes Web" }), _jsx("p", { className: "subtitle", children: "React + TypeScript SPA running in a native desktop window" }), _jsx(GreetCard, {}), _jsx(SystemInfo, {}), _jsxs("p", { className: "bridge-status", children: ["Hermes Bridge: ", bridge.isHermes ? 'Connected' : 'Not available (running in browser)'] })] }));
}
export default App;
