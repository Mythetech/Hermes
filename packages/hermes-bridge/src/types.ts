export interface InvokeEnvelope {
  type: 'invoke';
  id: string;
  method: string;
  args: unknown[];
}

export interface ResultEnvelope {
  type: 'result';
  id: string;
  value: unknown;
}

export interface ErrorEnvelope {
  type: 'error';
  id: string;
  message: string;
}

export interface EventEnvelope {
  type: 'event';
  name: string;
  data: unknown;
}

export type Envelope = InvokeEnvelope | ResultEnvelope | ErrorEnvelope | EventEnvelope;

export interface InvokeOptions {
  timeout?: number;
}

export interface HermesExternal {
  sendMessage(message: string): void;
  receiveMessage(callback: (message: string) => void): void;
}
