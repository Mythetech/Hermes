// Copyright (c) Mythetech. Licensed under the MIT License.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { installFakeWindow, tick } from './fake-window.mjs';

const page = installFakeWindow({ readyState: 'loading', runFrames: false });
await import('../dist/esm/index.js');

test('waits for load, then sends ready once after a frame', async () => {
  await tick(20);
  assert.equal(page.frames.length, 0);

  page.dispatch('load');
  assert.equal(page.frames.length, 1);
  page.frames[0]();
  await tick(20);
  assert.equal(page.ofType('ready').length, 1);

  await tick(300);
  assert.equal(page.ofType('ready').length, 1);
});
