// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
import App from './App.svelte';
import './style.css';
import { mount } from 'svelte';

const app = mount(App, { target: document.getElementById('app')! });

export default app;
