<!-- Copyright (c) Mythetech. Licensed under the Elastic License 2.0. -->
<script lang="ts">
  import { createInvokeStore } from '@hermes/svelte';

  const runtime = createInvokeStore<string>('getRuntime');
  const platform = createInvokeStore<string>('getPlatform');
</script>

<div class="card">
  <h2>System Info</h2>
  <p>
    Runtime:
    {#if $runtime.loading}
      <span class="loading">Loading...</span>
    {:else if $runtime.error}
      <span>N/A ({$runtime.error.message})</span>
    {:else}
      <span>{$runtime.data}</span>
    {/if}
  </p>
  <p>
    Platform:
    {#if $platform.loading}
      <span class="loading">Loading...</span>
    {:else if $platform.error}
      <span>N/A ({$platform.error.message})</span>
    {:else}
      <span>{$platform.data}</span>
    {/if}
  </p>
</div>
