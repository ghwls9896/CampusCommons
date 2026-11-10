// Simulated browser devices exercise the real app.js, never a personal camera.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';

const code = readFileSync(new URL('../app.js', import.meta.url), 'utf8');
const settle = () => new Promise(resolve => setImmediate(resolve));
const deferred = () => {let resolve; const promise = new Promise(r => {resolve = r;}); return {promise, resolve};};

function browser({pendingPermission = false} = {}) {
  const elements = new Map();
  const el = id => {
    if (!elements.has(id)) elements.set(id, {textContent:'', hidden:false, disabled:false,
      listeners:{}, addEventListener(type, fn) {this.listeners[type] = fn;},
      removeAttribute(name) {delete this[name];}});
    return elements.get(id);
  };
  Object.assign(el('video'), {videoWidth:1280, videoHeight:720, play:async () => {}});
  let stopped = 0, accesses = 0, now = 0;
  const track = {stop() {stopped++;}, addEventListener() {}};
  const stream = {getTracks:() => [track], getVideoTracks:() => [track]};
  const permission = deferred(), inference = [], timers = [], docListeners = {};
  const context = {
    document:{hidden:false, getElementById:el,
      querySelector:() => ({content:'test-token'}),
      createElement:() => ({getContext:() => ({drawImage(){}}), toBlob:fn => fn({size:50})}),
      addEventListener:(type, fn) => {docListeners[type] = fn;}},
    navigator:{mediaDevices:{getUserMedia:async () => {accesses++; return pendingPermission ? permission.promise : stream;}}},
    window:{addEventListener(){}}, performance:{now:() => now}, Date, Math, AbortController,
    URL:{createObjectURL:() => 'blob:test', revokeObjectURL(){}},
    setTimeout:(fn, delay) => {timers.push({fn, delay}); return timers.length;},
    clearTimeout(){},
    fetch:async (path, options) => {
      if (path === '/api/config') return {ok:true, json:async () => ({interval_ms:1000, mode:'accurate', device:'test'})};
      const reply = deferred(); inference.push({options, reply}); return reply.promise;
    }
  };
  vm.runInNewContext(code, context);
  return {el, timers, inference, permission, stream, context, docListeners,
    get stopped(){return stopped;}, get accesses(){return accesses;},
    clock(value){now = value;},
    respond(index, state = 'phone') {inference[index].reply.resolve({ok:true, json:async () => ({
      ok:true, state, probabilities:{studying:.1, phone:.7, other:.1, away:.1}, confidence:.7,
      seconds:.2, mode:'accurate', device:'test'})});}};
}

test('loading the page does not access the camera', async () => {
  const b = browser(); await settle();
  assert.equal(b.accesses, 0);
  assert.equal(b.inference.length, 0);
});

test('one-second start-to-start scheduling subtracts inference time', async () => {
  const b = browser(); await settle();
  await b.el('start').listeners.click(); await settle();
  assert.equal(b.inference.length, 1);
  assert.equal(b.timers.length, 0, 'no timer can queue a second request while busy');
  b.clock(200); b.respond(0); await settle();
  assert.equal(b.el('verdict').textContent, '휴대폰 사용중');
  assert.equal(b.timers.at(-1).delay, 800);
});

test('slow inference schedules only the next fresh capture', async () => {
  const b = browser(); await settle();
  await b.el('start').listeners.click(); await settle();
  b.clock(1500); b.respond(0); await settle();
  assert.equal(b.timers.at(-1).delay, 0);
  b.timers.at(-1).fn(); await settle();
  assert.equal(b.inference.length, 2);
});

test('displayed result stays rateable while the next slow inference is pending', async () => {
  const b = browser(); await settle();
  await b.el('start').listeners.click(); await settle();
  b.clock(1500); b.respond(0); await settle();
  assert.equal(b.el('correct').disabled, false);
  b.timers.at(-1).fn(); await settle();
  assert.equal(b.inference.length, 2);
  assert.equal(b.el('verdict').textContent, '휴대폰 사용중');
  assert.equal(b.el('correct').disabled, false, 'the visible result must remain rateable');
  b.el('correct').listeners.click();
  assert.equal(b.el('correct').disabled, true);
  assert.equal(b.el('incorrect').disabled, true, 'only one vote for this result');
  assert.match(b.el('score').textContent, /맞음 1개/);
  b.clock(3000); b.respond(1); await settle();
  assert.equal(b.el('correct').disabled, false, 'a new displayed result gets its own vote');
});

test('analysis error clears the previous result and disables its feedback', async () => {
  const b = browser(); await settle();
  await b.el('start').listeners.click(); await settle();
  b.respond(0); await settle();
  b.timers.at(-1).fn(); await settle();
  b.inference[1].reply.resolve({ok:false, json:async () => ({ok:false, error:'test failure'})});
  await settle();
  assert.equal(b.el('verdict').textContent, '분석 오류');
  assert.equal(b.el('snapshot').hidden, true);
  assert.equal(b.el('correct').disabled, true);
  assert.equal(b.el('incorrect').disabled, true);
});

test('stop releases camera and a late response cannot overwrite stopped state', async () => {
  const b = browser(); await settle();
  await b.el('start').listeners.click(); await settle();
  b.el('stop').listeners.click();
  assert.equal(b.stopped, 1);
  assert.equal(b.inference[0].options.signal.aborted, true);
  b.respond(0); await settle();
  assert.equal(b.el('verdict').textContent, '중지됨');
  assert.equal(b.el('snapshot').hidden, true);
  assert.equal(b.timers.length, 0);
});

test('stop while permission is pending stops tracks when permission arrives', async () => {
  const b = browser({pendingPermission:true}); await settle();
  const starting = b.el('start').listeners.click();
  b.el('stop').listeners.click();
  b.permission.resolve(b.stream); await starting; await settle();
  assert.equal(b.stopped, 1);
  assert.equal(b.inference.length, 0);
});

test('backgrounding the page releases the camera', async () => {
  const b = browser(); await settle();
  await b.el('start').listeners.click(); await settle();
  b.context.document.hidden = true;
  b.docListeners.visibilitychange();
  assert.equal(b.stopped, 1);
});
