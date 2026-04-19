<!-- Copyright (c) Mythetech. Licensed under the Elastic License 2.0. -->
<script lang="ts">
  import { createEventStore, hermesConnected } from '@hermes/svelte';

  const tick = createEventStore<number>('tick');
</script>

<div class="card">
  <h2>Live Ticker</h2>
  {#if !$hermesConnected}
    <p class="loading">Requires Hermes bridge (not available in browser)</p>
  {:else if $tick === undefined}
    <p class="loading">Waiting for first tick...</p>
  {:else}
    <p class="ticker">{$tick}s</p>
    <p>Elapsed seconds from C# timer, pushed via <code>bridge.Send()</code></p>
  {/if}
</div>
