import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { useInvoke } from '@hermes/bridge-react';
import './SystemInfo.css';
function SystemInfo() {
    const runtime = useInvoke('getRuntime');
    const platform = useInvoke('getPlatform');
    return (_jsxs("div", { className: "card system-info", children: [_jsx("h2", { children: "System Info" }), _jsxs("p", { children: ["Runtime:", ' ', _jsx("span", { className: "info-value", children: runtime.loading ? 'Loading...' : runtime.data ?? 'N/A' })] }), _jsxs("p", { children: ["Platform:", ' ', _jsx("span", { className: "info-value", children: platform.loading ? 'Loading...' : platform.data ?? 'N/A' })] })] }));
}
export default SystemInfo;
