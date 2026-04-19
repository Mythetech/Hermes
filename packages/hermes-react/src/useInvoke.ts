import { useState, useEffect, useCallback, useRef } from 'react';
import { bridge } from '@hermes/bridge';

export interface UseInvokeResult<TResult> {
  data: TResult | null;
  loading: boolean;
  error: Error | null;
  invoke: (...args: unknown[]) => Promise<TResult>;
  refetch: () => Promise<TResult>;
}

export function useInvoke<TResult = unknown>(method: string): UseInvokeResult<TResult> {
  const [data, setData] = useState<TResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const mountedRef = useRef(true);
  const lastArgsRef = useRef<unknown[]>([]);

  const invoke = useCallback(async (...args: unknown[]): Promise<TResult> => {
    lastArgsRef.current = args;
    setLoading(true);
    setError(null);
    try {
      const result = await bridge.invoke<TResult>(method, ...args);
      if (mountedRef.current) {
        setData(result);
        setLoading(false);
      }
      return result;
    } catch (err) {
      const wrapped = err instanceof Error ? err : new Error(String(err));
      if (mountedRef.current) {
        setError(wrapped);
        setLoading(false);
      }
      throw wrapped;
    }
  }, [method]);

  const refetch = useCallback((): Promise<TResult> => {
    return invoke(...lastArgsRef.current);
  }, [invoke]);

  useEffect(() => {
    mountedRef.current = true;
    let cancelled = false;

    bridge.invoke<TResult>(method)
      .then(result => {
        if (!cancelled) {
          setData(result);
          setLoading(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
      mountedRef.current = false;
    };
  }, [method]);

  return { data, loading, error, invoke, refetch };
}
