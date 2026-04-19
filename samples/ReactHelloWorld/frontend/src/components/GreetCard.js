import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { useState } from 'react';
import { useInvoke } from '@hermes/bridge-react';
import './GreetCard.css';
function GreetCard() {
    const [name, setName] = useState('World');
    const { data, loading, error, invoke } = useInvoke('greet');
    const handleGreet = async () => {
        try {
            await invoke(name);
        }
        catch {
            // Error captured in hook state
        }
    };
    return (_jsxs("div", { className: "card", children: [_jsxs("div", { className: "greet-form", children: [_jsx("input", { type: "text", value: name, onChange: (e) => setName(e.target.value), placeholder: "Enter your name" }), _jsx("button", { onClick: handleGreet, disabled: loading, children: loading ? 'Greeting...' : 'Greet from C#' })] }), data && _jsx("p", { className: "output", children: data }), error && _jsx("p", { className: "output error", children: error.message })] }));
}
export default GreetCard;
