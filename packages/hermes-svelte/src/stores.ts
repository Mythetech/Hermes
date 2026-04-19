// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
import { readable, type Readable } from 'svelte/store';
import { bridge } from '@hermes/bridge';

export interface InvokeState<T> {
  loading: boolean;
  data?: T;
  error?: Error;
}

export const hermesConnected: Readable<boolean> = readable(false, (set) => {
  set(bridge.isHermes);
});

export function createInvokeStore<T>(method: string, ...args: unknown[]): Readable<InvokeState<T>> {
  return readable<InvokeState<T>>({ loading: true }, (set) => {
    if (!bridge.isHermes) {
      set({ loading: false, error: new Error('Not running in Hermes') });
      return;
    }

    bridge.invoke<T>(method, ...args)
      .then((data) => set({ loading: false, data }))
      .catch((error) => set({ loading: false, error: error instanceof Error ? error : new Error(String(error)) }));
  });
}

export function createEventStore<T>(eventName: string, initialValue?: T): Readable<T | undefined> {
  return readable<T | undefined>(initialValue, (set) => {
    if (!bridge.isHermes) return;

    return bridge.on<T>(eventName, (data) => {
      set(data);
    });
  });
}
