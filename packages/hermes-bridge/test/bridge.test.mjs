// Copyright (c) Mythetech. Licensed under the MIT License.
import { test } from 'node:test';
import assert from 'node:assert/strict';

const sent = [];
let receive;
globalThis.window = {
  external: {
    sendMessage: (message) => sent.push(JSON.parse(message)),
    receiveMessage: (callback) => { receive = callback; },
  },
};

const { bridge } = await import('../dist/esm/index.js');

function reply(envelope) {
  receive(JSON.stringify(envelope));
}

test('invoke sends an object with a timeout key as an argument', async () => {
  const call = bridge.invoke('save', { timeout: 30, name: 'x' });
  const envelope = sent.at(-1);

  assert.equal(envelope.method, 'save');
  assert.deepEqual(envelope.args, [{ timeout: 30, name: 'x' }]);
  reply({ type: 'result', id: envelope.id, value: 'ok' });
  assert.equal(await call, 'ok');
});

test('invokeWithOptions times out', async () => {
  await assert.rejects(bridge.invokeWithOptions('slow', { timeout: 20 }), /timed out after 20ms/);
});

test('invokeWithOptions sends the remaining arguments', async () => {
  const call = bridge.invokeWithOptions('add', { timeout: 1000 }, 1, 2);
  const envelope = sent.at(-1);

  assert.deepEqual(envelope.args, [1, 2]);
  reply({ type: 'result', id: envelope.id, value: 3 });
  assert.equal(await call, 3);
});

test('result resolves the matching call', async () => {
  const first = bridge.invoke('first');
  const firstEnvelope = sent.at(-1);
  const second = bridge.invoke('second');
  const secondEnvelope = sent.at(-1);

  reply({ type: 'result', id: secondEnvelope.id, value: 'two' });
  reply({ type: 'result', id: firstEnvelope.id, value: 'one' });

  assert.equal(await first, 'one');
  assert.equal(await second, 'two');
});
