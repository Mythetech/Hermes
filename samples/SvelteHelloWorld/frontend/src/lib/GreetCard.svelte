<!-- Copyright (c) Mythetech. Licensed under the Elastic License 2.0. -->
<script lang="ts">
  import { bridge } from '@hermes/bridge';
  import { hermesConnected } from '@hermes/svelte';

  let name = $state('World');
  let output = $state('');

  async function greet() {
    if (!$hermesConnected) {
      output = `Hello from JavaScript, ${name}! (bridge not available)`;
      return;
    }

    try {
      output = await bridge.invoke<string>('greet', name);
    } catch (e) {
      output = `Error: ${e instanceof Error ? e.message : e}`;
    }
  }
</script>

<div class="card">
  <input type="text" bind:value={name} placeholder="Enter your name" />
  <button onclick={greet}>Greet from C#</button>
  {#if output}
    <p class="output">{output}</p>
  {/if}
</div>
