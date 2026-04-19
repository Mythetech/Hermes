<script lang="ts">
  import { hermesConnected } from '@hermes/svelte';
  import GreetCard from './lib/GreetCard.svelte';
  import SystemInfo from './lib/SystemInfo.svelte';
  import LiveTicker from './lib/LiveTicker.svelte';
</script>

<h1>Hermes + Svelte</h1>
<p class="subtitle">Svelte SPA running in a native desktop window</p>

<p>
  Hermes Bridge:
  {#if $hermesConnected}
    <span class="status connected">Connected</span>
  {:else}
    <span class="status disconnected">Not available (running in browser)</span>
  {/if}
</p>

<GreetCard />
<SystemInfo />
<LiveTicker />
