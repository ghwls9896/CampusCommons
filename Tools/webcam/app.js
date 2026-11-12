'use strict';
const byId = id => document.getElementById(id);
const video = byId('video');
const token = document.querySelector('meta[name="session-token"]').content;
const labels = {studying:'공부중', phone:'휴대폰 사용중', other:'다른활동중', away:'자리비움', drowsy:'자는중'};
const INTERVAL_MS = 1000;
let stream = null, generation = 0, timer = null, request = null, snapshotURL = null;
let correct = 0, incorrect = 0;
let lastCapture = null;

function showScores(scores = {}) {
  for (const state of Object.keys(labels)) {
    const value = scores[state] ?? 0;
    byId(`bar-${state}`).value = value;
    byId(`pct-${state}`).textContent = `${Math.round(value * 100)}%`;
  }
}

function clearSnapshot() {
  byId('snapshot').hidden = true;
  byId('snapshot').removeAttribute('src');
  if (snapshotURL) URL.revokeObjectURL(snapshotURL);
  snapshotURL = null;
}

function feedback(enabled) {
  byId('correct').disabled = !enabled;
  byId('incorrect').disabled = !enabled;
}

function stop(message = '카메라를 중지했습니다.') {
  generation++;
  clearTimeout(timer);
  request?.abort();
  request = null;
  stream?.getTracks().forEach(track => track.stop());
  stream = null;
  lastCapture = null;
  video.srcObject = null;
  clearSnapshot();
  byId('placeholder').hidden = false;
  byId('camera-state').textContent = '카메라 OFF';
  byId('start').disabled = false;
  byId('stop').disabled = true;
  byId('verdict').textContent = '중지됨';
  byId('progress').textContent = message;
  byId('reason').textContent = '영상과 판정 사진을 저장하지 않았습니다.';
  byId('timing').textContent = '';
  showScores();
  feedback(false);
}

async function analyze(run) {
  if (run !== generation || !stream) return;
  const started = performance.now();
  const actualInterval = lastCapture === null ? null : (started - lastCapture) / 1000;
  lastCapture = started;
  byId('progress').textContent = '현재 사진을 분석하고 있습니다…';
  const canvas = document.createElement('canvas');
  // Sample a bounded camera image; the backend applies the selected native mode.
  const scale = Math.min(1, 512 / Math.max(video.videoWidth, video.videoHeight));
  canvas.width = Math.max(1, Math.round(video.videoWidth * scale));
  canvas.height = Math.max(1, Math.round(video.videoHeight * scale));
  canvas.getContext('2d').drawImage(video, 0, 0, canvas.width, canvas.height);
  const captured = new Date();
  try {
    const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', .85));
    if (run !== generation || !stream) return;
    if (!blob) throw new Error('카메라 사진을 만들지 못했습니다.');
    request = new AbortController();
    const response = await fetch('/api/infer', {method:'POST', headers:{
      'Content-Type':'image/jpeg', 'X-Session-Token':token
    }, body:blob, signal:request.signal});
    const result = await response.json();
    if (run !== generation || !stream) return;
    if (!response.ok || !result.ok) throw new Error(result.error || '모델 응답을 확인하지 못했습니다.');
    clearSnapshot();
    snapshotURL = URL.createObjectURL(blob);
    byId('snapshot').src = snapshotURL;
    byId('snapshot').hidden = false;
    if (!Object.hasOwn(labels, result.state)) throw new Error('모델이 다섯 범주 중 하나를 반환하지 않았습니다.');
    byId('verdict').textContent = labels[result.state];
    showScores(result.probabilities);
    byId('reason').textContent = `QEV ${result.mode} · ${Math.round(result.confidence * 100)}% 선택 점수`;
    const intervalText = actualInterval === null ? '' : ` · 실제 간격 ${actualInterval.toFixed(2)}초`;
    byId('timing').textContent = `${captured.toLocaleTimeString('ko-KR')} 촬영 · ${canvas.width}×${canvas.height} · 분석 ${result.seconds.toFixed(2)}초${intervalText}`;
    byId('progress').textContent = '아래 사진에 대한 추정입니다. 직접 확인해 주세요.';
    feedback(true);
  } catch (error) {
    if (run !== generation) return;
    clearSnapshot();
    feedback(false);
    byId('verdict').textContent = '분석 오류';
    byId('reason').textContent = '';
    byId('timing').textContent = '';
    showScores();
    byId('progress').textContent = error.message || '분석 중 오류가 발생했습니다.';
  } finally {
    if (run === generation && stream) {
      request = null;
      timer = setTimeout(() => analyze(run), Math.max(0, INTERVAL_MS - (performance.now() - started)));
    }
  }
}

byId('start').addEventListener('click', async () => {
  feedback(false);
  byId('start').disabled = true;
  byId('stop').disabled = false;
  byId('progress').textContent = '카메라 접근 허용을 기다립니다…';
  const run = ++generation;
  let acquired;
  try {
    if (!navigator.mediaDevices?.getUserMedia) throw new Error('이 브라우저에서는 카메라를 사용할 수 없습니다. Chrome 또는 Safari에서 열어 주세요.');
    acquired = await navigator.mediaDevices.getUserMedia({audio:false, video:{width:{ideal:1280}, height:{ideal:720}, facingMode:'user'}});
    if (run !== generation || document.hidden) {
      acquired.getTracks().forEach(track => track.stop());
      return;
    }
    stream = acquired;
    video.srcObject = stream;
    await video.play();
    if (run !== generation) return;
    stream.getVideoTracks().forEach(track => track.addEventListener('ended', () => {
      if (run === generation) stop('카메라 연결이 끊겼습니다.');
    }));
    byId('placeholder').hidden = true;
    byId('camera-state').textContent = '카메라 ON · 로컬 분석';
    byId('verdict').textContent = '판정 대기';
    analyze(run);
  } catch (error) {
    acquired?.getTracks().forEach(track => track.stop());
    if (run !== generation) return;
    stop(error.name === 'NotAllowedError' ? '카메라 권한이 허용되지 않았습니다. 브라우저의 카메라 권한을 확인해 주세요.' : error.message);
  }
});
byId('stop').addEventListener('click', () => stop());
for (const name of ['correct', 'incorrect']) {
  byId(name).addEventListener('click', () => {
    if (name === 'correct') correct++; else incorrect++;
    feedback(false);
    byId('score').textContent = `이번 화면에서 평가: 맞음 ${correct}개 · 틀림 ${incorrect}개 (저장 안 함)`;
  });
}
document.addEventListener('visibilitychange', () => {
  if (document.hidden) stop('다른 탭으로 이동하여 카메라를 중지했습니다.');
});
window.addEventListener('pagehide', () => stop());

fetch('/api/config').then(response => {
  if (!response.ok) throw new Error('모델 설정을 읽지 못했습니다.');
  return response.json();
}).then(config => {
  if (config.mode !== 'fast' || config.interval_ms !== INTERVAL_MS) throw new Error('지원하는 QEV 모드 / 1초 설정이 아닙니다.');
  if (!stream) byId('progress').textContent = `${config.model} · ${config.device.toUpperCase()}`;
}).catch(error => {
  byId('progress').textContent = error.message;
  byId('start').disabled = true;
});
