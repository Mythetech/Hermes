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

export interface HermesDropFile {
  name: string;
  size: number;
  type: string;
}

/** Detail of the hermesdrop event. File paths stay in .NET; resolve dropId there. */
export interface HermesDropEventDetail {
  dropId: string;
  x: number;
  y: number;
  files: HermesDropFile[];
  types: string[];
}

declare global {
  interface WindowEventMap {
    hermesdrop: CustomEvent<HermesDropEventDetail>;
  }

  interface DocumentEventMap {
    hermesdrop: CustomEvent<HermesDropEventDetail>;
  }

  interface HTMLElementEventMap {
    hermesdrop: CustomEvent<HermesDropEventDetail>;
  }
}
