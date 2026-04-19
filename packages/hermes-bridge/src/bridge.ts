import type { Envelope, HermesExternal, InvokeOptions } from './types.js';

interface PendingInvocation {
  resolve: (value: unknown) => void;
  reject: (reason: Error) => void;
  timer?: ReturnType<typeof setTimeout>;
}

type EventCallback = (data: unknown) => void;

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

  invoke<T = unknown>(method: string, ...args: unknown[]): Promise<T> {
    this.ensureInitialized();

    if (!this.ext) {
      return Promise.reject(new Error('Not running in Hermes'));
    }

    let options: InvokeOptions = {};
    let invokeArgs: unknown[];

    if (args.length > 0 && typeof args[0] === 'object' && args[0] !== null && 'timeout' in (args[0] as object)) {
      options = args[0] as InvokeOptions;
      invokeArgs = args.slice(1);
    } else {
      invokeArgs = args;
    }

    const id = generateId();
    const envelope = JSON.stringify({ type: 'invoke', id, method, args: invokeArgs });
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
            }
          }
        }
        break;
      }
    }
  }
}

export const bridge = new HermesBridge();
