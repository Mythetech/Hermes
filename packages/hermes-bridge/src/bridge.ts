import type { Envelope, HermesExternal, InvokeOptions, PageErrorEnvelope, ReadyEnvelope } from './types.js';

interface PendingInvocation {
  resolve: (value: unknown) => void;
  reject: (reason: Error) => void;
  timer?: ReturnType<typeof setTimeout>;
}

type EventCallback = (data: unknown) => void;

const READY_FALLBACK_MS = 250;

let idCounter = 0;

function generateId(): string {
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    return crypto.randomUUID();
  }
  return `hermes_${++idCounter}_${Date.now()}`;
}

function getHermesExternal(): HermesExternal | null {
  if (typeof window === 'undefined') return null;
  const ext = window.external as unknown as HermesExternal | undefined;
  if (ext && typeof ext.sendMessage === 'function') {
    return ext;
  }
  return null;
}

class HermesBridge {
  private pending = new Map<string, PendingInvocation>();
  private listeners = new Map<string, Set<EventCallback>>();
  private initialized = false;
  private ext: HermesExternal | null = null;

  constructor() {
    this.attach();
  }

  get isHermes(): boolean {
    return getHermesExternal() !== null;
  }

  private ensureInitialized(): void {
    if (this.initialized) return;
    this.initialized = true;

    this.ext = getHermesExternal();
    if (!this.ext) return;

    this.ext.receiveMessage((raw: string) => {
      try {
        const envelope = JSON.parse(raw) as Envelope;
        this.handleMessage(envelope);
      } catch {
        // Not a bridge message
      }
    });
  }

  /** Calls a .NET handler with every argument given, and resolves with its result. */
  invoke<T = unknown>(method: string, ...args: unknown[]): Promise<T> {
    return this.call<T>(method, {}, args);
  }

  /** Calls a .NET handler like invoke, with options such as a timeout that are never sent as arguments. */
  invokeWithOptions<T = unknown>(method: string, options: InvokeOptions, ...args: unknown[]): Promise<T> {
    return this.call<T>(method, options, args);
  }

  private call<T>(method: string, options: InvokeOptions, args: unknown[]): Promise<T> {
    this.ensureInitialized();

    if (!this.ext) {
      return Promise.reject(new Error('Not running in Hermes'));
    }

    const id = generateId();
    const envelope = JSON.stringify({ type: 'invoke', id, method, args });
    const ext = this.ext;

    return new Promise<T>((resolve, reject) => {
      const pending: PendingInvocation = {
        resolve: resolve as (value: unknown) => void,
        reject,
      };

      if (options.timeout != null && options.timeout > 0) {
        pending.timer = setTimeout(() => {
          this.pending.delete(id);
          reject(new Error(`Invoke '${method}' timed out after ${options.timeout}ms`));
        }, options.timeout);
      }

      this.pending.set(id, pending);
      ext.sendMessage(envelope);
    });
  }

  send(name: string, data?: unknown): void {
    this.ensureInitialized();

    if (!this.ext) return;

    const envelope = JSON.stringify({ type: 'event', name, data: data ?? null });
    this.ext.sendMessage(envelope);
  }

  on<T = unknown>(name: string, callback: (data: T) => void): () => void {
    this.ensureInitialized();

    if (!this.ext) {
      return () => {};
    }

    let callbacks = this.listeners.get(name);
    if (!callbacks) {
      callbacks = new Set();
      this.listeners.set(name, callbacks);
    }

    const wrapped: EventCallback = (data) => callback(data as T);
    callbacks.add(wrapped);

    return () => {
      callbacks!.delete(wrapped);
      if (callbacks!.size === 0) {
        this.listeners.delete(name);
      }
    };
  }

  /** Inside Hermes, reports page errors and tells Hermes once the page has rendered. */
  private attach(): void {
    if (typeof window === 'undefined' || typeof window.addEventListener !== 'function' || typeof document === 'undefined') {
      return;
    }

    this.ensureInitialized();
    if (!this.ext) return;

    window.addEventListener('error', (event) => this.reportError('error', event.error, event.message));
    window.addEventListener('unhandledrejection', (event) => this.reportError('unhandledrejection', event.reason));

    if (document.readyState === 'complete') {
      this.scheduleReady();
    } else {
      window.addEventListener('load', () => this.scheduleReady(), { once: true });
    }
  }

  private scheduleReady(): void {
    let sent = false;
    const send = () => {
      if (sent) return;
      sent = true;
      const envelope: ReadyEnvelope = { type: 'ready' };
      this.ext?.sendMessage(JSON.stringify(envelope));
    };

    // A frame means the first render has painted; the timer covers windows whose frames are throttled.
    requestAnimationFrame(() => setTimeout(send, 0));
    setTimeout(send, READY_FALLBACK_MS);
  }

  private reportError(source: PageErrorEnvelope['source'], error: unknown, fallbackMessage?: string): void {
    const err = error instanceof Error ? error : undefined;
    const envelope: PageErrorEnvelope = {
      type: 'page-error',
      source,
      name: err?.name ?? 'Error',
      message: err?.message ?? (error != null ? String(error) : fallbackMessage ?? ''),
      stack: err?.stack ?? null,
    };
    this.ext?.sendMessage(JSON.stringify(envelope));
  }

  private handleMessage(envelope: Envelope): void {
    switch (envelope.type) {
      case 'result': {
        const pending = this.pending.get(envelope.id);
        if (pending) {
          this.pending.delete(envelope.id);
          if (pending.timer) clearTimeout(pending.timer);
          pending.resolve(envelope.value);
        }
        break;
      }
      case 'error': {
        const pending = this.pending.get(envelope.id);
        if (pending) {
          this.pending.delete(envelope.id);
          if (pending.timer) clearTimeout(pending.timer);
          pending.reject(new Error(envelope.message));
        }
        break;
      }
      case 'event': {
        const callbacks = this.listeners.get(envelope.name);
        if (callbacks) {
          for (const cb of callbacks) {
            try {
              cb(envelope.data);
            } catch (e) {
              console.error(`[hermes/bridge] Event handler error for '${envelope.name}':`, e);
              this.reportError('event-handler', e);
            }
          }
        }
        break;
      }
    }
  }
}

export const bridge = new HermesBridge();
