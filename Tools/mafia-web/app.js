"use strict";

const ui = Object.fromEntries(Array.from(document.querySelectorAll("[id]"), element => [element.id, element]));
const roomsUrl = "/v1/mafia/rooms";
const portraits = new Map();
let config;
let membership;
let room;
let selected = "";
let busy = false;
let autoRun = false;
let polling = false;
let renderedSequence = -1;

function element(tag, value, klass) {
  const item = document.createElement(tag);
  item.textContent = value;
  if (klass) item.className = klass;
  return item;
}

function showNotice(text = "") {
  ui.notice.textContent = text;
  ui.notice.hidden = !text;
}

async function request(url, method = "GET", body) {
  const headers = { "X-Quiz-Token": config?.token ?? "" };
  if (membership) headers["X-Mafia-Member"] = membership.member_token;
  if (body) headers["Content-Type"] = "application/json";
  const response = await fetch(url, { method, headers, body: body && JSON.stringify(body), cache: "no-store" });
  const data = await response.json();
  if (!response.ok) throw new Error(typeof data.detail === "string" ? data.detail : "요청에 실패했습니다.");
  return data;
}

function buildCast() {
  for (const person of config.characters) {
    const button = element("button", "", "person");
    button.type = "button";
    const label = element("span", "");
    label.append(element("strong", person.name), element("small", person.trait));
    button.append(element("span", person.id, "badge"), label, element("small", "", "role"));
    button.addEventListener("click", () => {
      selected = selected === person.id ? "" : person.id;
      render();
    });
    portraits.set(person.id, { ...person, button });
    ui.cast.append(button);
  }
}

function appendLine(message) {
  const person = portraits.get(message.speaker);
  const item = element("article", "", "line " + message.source);
  const content = element("div", "");
  const title = element("div", "", "speaker");
  title.append(element("strong", person?.name ?? message.speaker));
  if (message.elapsed_ms !== undefined) {
    const source = { ollama: "AI 질문 작성", ollama_choice: "AI 질문 선택", scripted: "기본 증언" }[message.source];
    title.append(element("small", source + " · " + (message.elapsed_ms / 1000).toFixed(1) + "초"));
  }
  content.append(title, element("p", message.text));
  item.append(element("span", person?.id ?? (message.source === "system" ? "!" : "↳"), "badge"), content);
  ui.transcript.append(item);
}

function renderTranscript() {
  const messages = room?.messages ?? [];
  const sequence = messages.at(-1)?.sequence ?? -1;
  if (renderedSequence === sequence) return;
  if (renderedSequence === -1) ui.transcript.replaceChildren();
  for (const message of messages.filter(line => line.sequence > renderedSequence)) appendLine(message);
  while (ui.transcript.childElementCount > messages.length) ui.transcript.firstElementChild.remove();
  renderedSequence = sequence;
  ui.transcript.scrollTop = ui.transcript.scrollHeight;
}

function render() {
  const inRoom = Boolean(room);
  ui.entry.hidden = inRoom;
  ui["room-bar"].hidden = !inRoom;
  ui["game-actions"].hidden = !room?.is_host;
  ui.composer.hidden = !inRoom;
  ui.pending.hidden = !room?.pending;
  ui.create.disabled = busy || !config?.ready;
  ui.join.disabled = busy || !config;
  const active = Boolean(room?.alive.includes(selected));
  const day = room?.phase === "day";
  const canAct = !busy && !autoRun && !room?.pending;

  for (const person of portraits.values()) {
    person.button.disabled = !room?.alive.includes(person.id) || room?.phase === "finished";
    person.button.setAttribute("aria-pressed", String(selected === person.id));
    person.button.lastElementChild.textContent = room?.revealed_professor === person.id
      ? "교수" : inRoom && !room.alive.includes(person.id) ? "제외" : "";
  }

  if (!inRoom) return;
  ui["room-code"].textContent = room.room_id;
  ui.members.textContent = room.members.join(" · ");
  ui.phase.textContent = room.result || (day ? room.round + "일차 · 낮" : room.round + "일차 · 밤");
  const ai = room.messages.filter(line => line.source === "ollama" || line.source === "ollama_choice").length;
  const fallback = room.messages.filter(line => line.source === "scripted").length;
  ui.counts.textContent = "AI 참여 " + ai + " · 기본 증언 " + fallback;
  ui.auto.textContent = autoRun ? "발언 진행 중" : room.round === 1 ? "6명 발언 시작" : "남은 인물 발언";
  ui.auto.disabled = ui.speak.disabled = !canAct || !day || !room.can_speak;
  ui.night.disabled = !canAct || !day;
  ui.protect.hidden = room.phase !== "night";
  ui.protect.disabled = !canAct || !active;
  ui.guess.disabled = ui.question.disabled = !canAct || !day || !active;
  ui.chat.disabled = busy;
  ui.leave.disabled = busy;
  renderTranscript();
}

function accept(snapshot) {
  const previous = room?.messages.at(-1)?.sequence ?? 0;
  const next = snapshot.messages.at(-1)?.sequence ?? 0;
  if (!room || (snapshot.revision >= room.revision && next >= previous)) {
    room = snapshot;
    if (!room.alive.includes(selected)) selected = "";
    render();
  }
}

async function refresh() {
  const current = membership;
  const snapshot = await request(roomsUrl + "/" + current.room_id);
  if (current === membership) accept(snapshot);
}

async function send(kind, text = "") {
  const snapshot = await request(roomsUrl + "/" + room.room_id + "/actions", "POST", {
    kind, revision: room.revision, target: selected, text
  });
  accept(snapshot);
}

async function run(action) {
  if (busy) return;
  showNotice();
  busy = true;
  render();
  try {
    await action();
  } catch (error) {
    showNotice(error.message);
  } finally {
    busy = false;
    render();
  }
}

function resetRoom() {
  membership = room = undefined;
  selected = "";
  renderedSequence = -1;
  ui.phase.textContent = "첫 번째 낮";
  ui.counts.textContent = "";
  ui.transcript.replaceChildren(element("p", "방을 만들거나 참가하면 같은 방의 대화가 여기에 표시됩니다.", "empty"));
}

ui.entry.addEventListener("submit", event => {
  event.preventDefault();
  const joining = event.submitter === ui.join;
  const code = ui["join-code"].value.trim().toUpperCase();
  if (joining && !/^[A-F0-9]{10}$/.test(code)) return showNotice("10자리 방 코드를 입력해 주세요.");
  const nickname = ui.nickname.value.trim();
  if (!nickname) return showNotice("닉네임을 입력해 주세요.");
  run(async () => {
    membership = await request(roomsUrl + (joining ? "/" + code + "/join" : ""), "POST", { nickname });
    await refresh();
  });
});

ui.leave.addEventListener("click", () => run(async () => {
  await request(roomsUrl + "/" + room.room_id + "/leave", "POST");
  resetRoom();
}));
for (const [button, action] of [[ui.speak, "speak"], [ui.night, "next_night"], [ui.protect, "protect"], [ui.guess, "guess"]]) {
  button.addEventListener("click", () => run(() => send(action)));
}
ui.composer.addEventListener("submit", event => {
  event.preventDefault();
  const message = ui.message.value.trim();
  if (!message) return showNotice("질문이나 채팅 내용을 입력해 주세요.");
  run(async () => {
    await send(event.submitter === ui.question ? "question" : "chat", message);
    if (ui.message.value.trim() === message) ui.message.value = "";
  });
});

ui.auto.addEventListener("click", async () => {
  const current = membership;
  autoRun = true;
  showNotice();
  render();
  try {
    while (membership === current && room.is_host && room.phase === "day" && room.can_speak) {
      await send("speak");
      while (membership === current && room.pending) {
        await new Promise(resolve => setTimeout(resolve, 400));
        if (membership === current) await refresh();
      }
    }
  } catch (error) {
    if (membership === current) showNotice(error.message);
  } finally {
    autoRun = false;
    render();
  }
});

async function initialize() {
  try {
    const first = !config;
    config = await request("/demo/config");
    if (first) buildCast();
    ui["engine-model"].textContent = config.model;
    ui["engine-status"].textContent = config.ready ? "로컬 모델 준비됨" : "모델이 아직 준비되지 않았습니다";
    ui["engine-status"].classList.toggle("ready", config.ready);
    render();
  } catch (error) {
    showNotice(error.message);
  }
}

initialize();
setInterval(async () => {
  if (!membership || busy || polling || autoRun) return;
  polling = true;
  try {
    await refresh();
  } catch (error) {
    showNotice(error.message);
  } finally {
    polling = false;
  }
}, 1000);
setInterval(() => { if (!config?.ready) initialize(); }, 5000);
