import { jsx as _jsx } from "react/jsx-runtime";
// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
createRoot(document.getElementById('root')).render(_jsx(StrictMode, { children: _jsx(App, {}) }));
