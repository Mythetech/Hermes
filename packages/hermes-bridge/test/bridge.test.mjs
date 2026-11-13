// Copyright (c) Mythetech. Licensed under the MIT License.
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { installFakeWindow, tick } from './fake-window.mjs';

const page = installFakeWindow();
const sent = page.sent;
const { bridge } = await import('../dist/esm/index.js');
const reply = page.reply;

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

test('forwards an uncaught error', () => {
  const error = new TypeError('boom');
  page.dispatch('error', { error, message: 'Uncaught TypeError: boom' });

  const forwarded = page.ofType('page-error').at(-1);
  assert.equal(forwarded.source, 'error');
  assert.equal(forwarded.name, 'TypeError');
  assert.equal(forwarded.message, 'boom');
  assert.equal(forwarded.stack, error.stack);
});

test('forwards an unhandled rejection of a non-Error', () => {
  page.dispatch('unhandledrejection', { reason: 'nope' });

  const forwarded = page.ofType('page-error').at(-1);
  assert.equal(forwarded.source, 'unhandledrejection');
  assert.equal(forwarded.name, 'Error');
  assert.equal(forwarded.message, 'nope');
  assert.equal(forwarded.stack, null);
});

test('forwards a throwing event callback with source event-handler', () => {
  const unsubscribe = bridge.on('tick', () => { throw new RangeError('listener'); });
  const original = console.error;
  console.error = () => {};
  try {
    reply({ type: 'event', name: 'tick', data: 1 });
  } finally {
    console.error = original;
    unsubscribe();
  }

  const forwarded = page.ofType('page-error').at(-1);
  assert.equal(forwarded.source, 'event-handler');
  assert.equal(forwarded.name, 'RangeError');
  assert.equal(forwarded.message, 'listener');
});

test('sends ready once the document has loaded and a frame has passed', async () => {
  await tick(20);

  assert.equal(page.ofType('ready').length, 1);
});

