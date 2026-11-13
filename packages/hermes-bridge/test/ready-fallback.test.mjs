// Copyright (c) Mythetech. Licensed under the MIT License.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { installFakeWindow, tick } from './fake-window.mjs';

const page = installFakeWindow({ runFrames: false });
await import('../dist/esm/index.js');

test('sends ready from the fallback timer when frames never run', async () => {
  await tick(50);
  assert.equal(page.ofType('ready').length, 0);

  await tick(300);
  assert.equal(page.ofType('ready').length, 1);
});
