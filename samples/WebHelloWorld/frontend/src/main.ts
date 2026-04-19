// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
import { bridge } from '@hermes/bridge';
import './style.css';

const greetBtn = document.querySelector<HTMLButtonElement>('#greet-btn')!;
const nameInput = document.querySelector<HTMLInputElement>('#name-input')!;
const greetOutput = document.querySelector<HTMLParagraphElement>('#greet-output')!;
const runtimeInfo = document.querySelector<HTMLSpanElement>('#runtime-info')!;
const platformInfo = document.querySelector<HTMLSpanElement>('#platform-info')!;
const bridgeStatus = document.querySelector<HTMLSpanElement>('#bridge-status')!;

bridgeStatus.textContent = bridge.isHermes ? 'Connected' : 'Not available (running in browser)';

greetBtn.addEventListener('click', async () => {
  const name = nameInput.value || 'World';

  if (!bridge.isHermes) {
    greetOutput.textContent = `Hello from JavaScript, ${name}! (bridge not available)`;
    return;
  }

  try {
    const result = await bridge.invoke<string>('greet', name);
    greetOutput.textContent = result;
  } catch (e) {
    greetOutput.textContent = `Error: ${e instanceof Error ? e.message : e}`;
  }
});

async function loadSystemInfo() {
  if (!bridge.isHermes) {
    runtimeInfo.textContent = 'N/A (browser)';
    platformInfo.textContent = 'N/A (browser)';
    return;
  }

  try {
    const [runtime, platform] = await Promise.all([
      bridge.invoke<string>('getRuntime'),
      bridge.invoke<string>('getPlatform'),
    ]);
    runtimeInfo.textContent = runtime;
    platformInfo.textContent = platform;
  } catch (e) {
    runtimeInfo.textContent = 'Error loading';
    platformInfo.textContent = 'Error loading';
  }
}

loadSystemInfo();
