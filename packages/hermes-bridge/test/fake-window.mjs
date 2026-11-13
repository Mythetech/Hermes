// Copyright (c) Mythetech. Licensed under the MIT License.

/** Installs a window with a Hermes host and records what the bridge sends. */
export function installFakeWindow({ readyState = 'complete', runFrames = true } = {}) {
  const sent = [];
  const listeners = new Map();
  const frames = [];
  const fake = { sent, frames, receive: undefined };

  globalThis.window = {
    external: {
      sendMessage: (message) => sent.push(JSON.parse(message)),
      receiveMessage: (callback) => { fake.receive = callback; },
    },
    addEventListener: (type, listener) => {
      if (!listeners.has(type)) listeners.set(type, []);
      listeners.get(type).push(listener);
    },
  };
  globalThis.document = { readyState };
  globalThis.requestAnimationFrame = (callback) => {
    if (runFrames) setTimeout(callback, 0);
    else frames.push(callback);
  };

  fake.dispatch = (type, event = {}) => {
    for (const listener of listeners.get(type) ?? []) listener(event);
  };
  fake.reply = (envelope) => fake.receive(JSON.stringify(envelope));
  fake.ofType = (type) => sent.filter((envelope) => envelope.type === type);
  return fake;
}

export const tick = (ms = 0) => new Promise((resolve) => setTimeout(resolve, ms));
