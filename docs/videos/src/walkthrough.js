/** Deterministic walkthrough — driven frame-by-frame from render.mjs */

function $(id) {
  return document.getElementById(id);
}

function easeInOutCubic(t) {
  return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
}

function easeOutQuad(t) {
  return 1 - (1 - t) * (1 - t);
}

function clamp(v, lo, hi) {
  return Math.max(lo, Math.min(hi, v));
}

function lerp(a, b, t) {
  return a + (b - a) * t;
}

function centerOf(el, rootRect) {
  const r = el.getBoundingClientRect();
  return {
    x: r.left - rootRect.left + r.width / 2,
    y: r.top - rootRect.top + r.height / 2,
  };
}

const SCENE_NAMES = ["main", "history", "collapsed", "inline", "error"];

const state = {
  bannerHidden: false,
  activeScene: "main",
  prevScene: "main",
  sceneBlendStart: 0,
  sceneBlendDur: 450,
  caption: "",
  captionFadeStart: null,
  cursor: { x: 640, y: 380, visible: false },
  cursorClickUntil: 0,
  ripples: [],
  pressedUntil: new Map(),
  historySelected: -1,
  mainPctHighlight: false,
  mainEqHighlight: false,
  collapseHidden: false,
  inlineSuccess: false,
  inlineError: false,
  longEmphasis: false,
};

/** @type {{ at: number, id: number, run: (t: number) => void }[]} */
const SCHEDULE = [];
let scheduleId = 0;

function schedule(at, run) {
  SCHEDULE.push({ at, id: scheduleId++, run });
}

function setCaption(text, t) {
  if (state.caption !== text) {
    state.captionFadeStart = t;
    state.caption = text;
    $("caption").textContent = text;
    $("caption").classList.remove("is-fading");
    requestAnimationFrame(() => $("caption").classList.remove("is-fading"));
  }
}

function resetDom() {
  $("disclaimer-banner").classList.remove("is-hidden");
  state.bannerHidden = false;
  setCaption("Calc14 UI mock-up walkthrough", 0);

  $("main-entry").textContent = "10";
  $("main-sub").textContent = "200 +";
  document.querySelectorAll("#main-keys .key").forEach((k) => {
    k.classList.remove("key-highlight", "is-pressed");
  });

  $("history-entry").textContent = "220";
  document.querySelectorAll("#history-list li").forEach((li) => {
    li.classList.remove("selected", "is-pressed");
  });

  $("collapse-cb").checked = true;
  $("collapse-panel").classList.remove("is-collapsed", "is-hiding");
  $("collapse-clear").classList.remove("is-collapsed");

  $("log-success").classList.remove("is-shown");
  $("log-success").classList.add("is-hidden");
  $("log-error").classList.remove("is-shown");
  $("log-error").classList.add("is-hidden");
  $("inline-history-line").classList.add("is-hidden");
  $("inline-chat-hint")?.classList.remove("is-pulse");

  $("error-window").style.opacity = "1";
  $("long-window").style.opacity = "0.35";
  $("long-window").style.outline = "none";
  $("error-window").style.outline = "none";

  state.historySelected = -1;
  state.mainPctHighlight = false;
  state.mainEqHighlight = false;
  state.collapseHidden = false;
  state.inlineSuccess = false;
  state.inlineError = false;
  state.longEmphasis = false;
  state.pressedUntil.clear();
  state.ripples = [];
  $("ripple-layer").innerHTML = "";
}

function goScene(name, t) {
  if (state.activeScene === name) return;
  state.prevScene = state.activeScene;
  state.activeScene = name;
  state.sceneBlendStart = t;
}

function spawnRipple(x, y, t) {
  const el = document.createElement("div");
  el.className = "click-ripple";
  el.style.left = `${x}px`;
  el.style.top = `${y}px`;
  $("ripple-layer").appendChild(el);
  state.ripples.push({ el, born: t, dur: 480 });
}

function simulateClick(el, t) {
  if (!el) return;
  const rootRect = $("root").getBoundingClientRect();
  const { x, y } = centerOf(el, rootRect);
  state.cursor.x = x;
  state.cursor.y = y;
  state.cursorClickUntil = t + 120;
  spawnRipple(x, y, t);
  state.pressedUntil.set(el, t + 160);
  el.classList.add("is-pressed");
}

function moveCursorTo(el, startT, dur, t) {
  const rootRect = $("root").getBoundingClientRect();
  const target = centerOf(el, rootRect);
  const progress = clamp((t - startT) / dur, 0, 1);
  const eased = easeInOutCubic(progress);
  if (progress <= 0) return;
  state.cursor.visible = true;
  if (!state.cursorMoveFrom) {
    state.cursorMoveFrom = { x: state.cursor.x, y: state.cursor.y };
    state.cursorMoveTo = target;
    state.cursorMoveStart = startT;
    state.cursorMoveDur = dur;
  }
  state.cursor.x = lerp(state.cursorMoveFrom.x, state.cursorMoveTo.x, eased);
  state.cursor.y = lerp(state.cursorMoveFrom.y, state.cursorMoveTo.y, eased);
  if (progress >= 1) {
    state.cursorMoveFrom = null;
  }
}

function beginCursorMove(fromX, fromY, el, startT, dur) {
  const rootRect = $("root").getBoundingClientRect();
  const target = centerOf(el, rootRect);
  return {
    fromX,
    fromY,
    toX: target.x,
    toY: target.y,
    startT,
    dur,
  };
}

function applyCursorMove(move, t) {
  const progress = clamp((t - move.startT) / move.dur, 0, 1);
  const eased = easeInOutCubic(progress);
  state.cursor.visible = true;
  state.cursor.x = lerp(move.fromX, move.toX, eased);
  state.cursor.y = lerp(move.fromY, move.toY, eased);
  return progress >= 1;
}

function applySceneStyles(t) {
  const transitioning = state.activeScene !== state.prevScene;
  const blend = transitioning
    ? clamp((t - state.sceneBlendStart) / state.sceneBlendDur, 0, 1)
    : 1;
  const eased = easeOutQuad(blend);

  document.querySelectorAll(".walkthrough-scene").forEach((el) => {
    const name = el.dataset.scene;
    let opacity = 0;
    let ty = 10;
    if (name === state.activeScene) {
      if (!transitioning || blend >= 1) {
        opacity = 1;
        ty = 0;
      } else {
        opacity = eased;
        ty = lerp(10, 0, eased);
      }
    } else if (transitioning && name === state.prevScene && blend < 1) {
      opacity = 1 - eased;
      ty = lerp(0, -8, eased);
    }
    el.style.opacity = String(opacity);
    el.style.transform = `translateY(${ty}px)`;
    el.classList.toggle("is-visible", opacity > 0.05);
  });
}

function applyBanner(t) {
  const banner = $("disclaimer-banner");
  if (state.bannerHidden) {
    banner.classList.add("is-hidden");
  } else {
    banner.classList.remove("is-hidden");
  }
}

function applyCursorDom(t) {
  const cursor = $("cursor");
  cursor.classList.toggle("is-visible", state.cursor.visible);
  cursor.classList.toggle("is-clicking", t < state.cursorClickUntil);
  cursor.style.transform = `translate(${state.cursor.x}px, ${state.cursor.y}px)`;

  for (const [el, until] of state.pressedUntil) {
    if (t >= until) {
      el.classList.remove("is-pressed");
      state.pressedUntil.delete(el);
    }
  }

  state.ripples = state.ripples.filter((rip) => {
    const p = clamp((t - rip.born) / rip.dur, 0, 1);
    const scale = lerp(0.6, 2.8, easeOutQuad(p));
    const op = lerp(0.85, 0, p);
    rip.el.style.transform = `scale(${scale})`;
    rip.el.style.opacity = String(op);
    if (p >= 1) {
      rip.el.remove();
      return false;
    }
    return true;
  });
}

function applyMainHighlights() {
  const pct = document.querySelector('[data-scene="main"] [data-key="pct"]');
  const eq = document.querySelector('[data-scene="main"] [data-key="eq"]');
  pct?.classList.toggle("key-highlight", state.mainPctHighlight);
  eq?.classList.toggle("key-highlight", state.mainEqHighlight);
}

function applyHistorySelection() {
  const items = [...document.querySelectorAll("#history-list li")];
  items.forEach((li, i) => {
    li.classList.toggle("selected", i === state.historySelected);
  });
  const sel = items[state.historySelected];
  if (sel) {
    $("history-entry").textContent = sel.dataset.result ?? "220";
  }
}

function applyCollapse() {
  $("collapse-cb").checked = !state.collapseHidden;
  $("collapse-panel").classList.toggle("is-hiding", state.collapseHidden);
  if (state.collapseHidden) {
    $("collapse-panel").classList.add("is-collapsed");
    $("collapse-clear").classList.add("is-collapsed");
  }
}

function applyInline() {
  $("log-success").classList.toggle("is-hidden", !state.inlineSuccess);
  $("log-success").classList.toggle("is-shown", state.inlineSuccess);
  $("log-error").classList.toggle("is-hidden", !state.inlineError);
  $("log-error").classList.toggle("is-shown", state.inlineError);
  $("inline-history-line").classList.toggle("is-hidden", !state.inlineSuccess);
}

function applyErrorScene() {
  if (state.longEmphasis) {
    $("error-window").style.opacity = "0.35";
    $("long-window").style.opacity = "1";
    $("long-window").style.outline = "2px solid rgba(242, 115, 26, 0.8)";
    $("error-window").style.outline = "none";
  } else {
    $("error-window").style.opacity = "1";
    $("long-window").style.opacity = "0.35";
    $("long-window").style.outline = "none";
    $("error-window").style.outline = "none";
  }
}

function applyEqualsResult() {
  $("main-entry").textContent = "220";
  $("main-sub").textContent = "";
}

/** Build scripted timeline (absolute ms) */
function buildSchedule() {
  SCHEDULE.length = 0;
  let at = 0;

  schedule(at, () => {
    resetDom();
    state.activeScene = "main";
    state.prevScene = "main";
    state.cursor.visible = false;
    state.cursor.x = 640;
    state.cursor.y = 400;
  });

  at += 2600;
  schedule(at, (t) => {
    state.bannerHidden = true;
    applyBanner(t);
  });

  at += 700;
  schedule(at, (t) => {
    goScene("main", t);
    setCaption("Main window — keypad, pending operation, and history", t);
  });

  /** Cursor: drift to % key */
  const pctMoveStart = at + 900;
  const pctEl = () => document.querySelector('[data-scene="main"] [data-key="pct"]');
  schedule(pctMoveStart, (t) => {
    state.pendingMove = beginCursorMove(680, 420, pctEl(), t, 750);
  });

  const pctClick = pctMoveStart + 820;
  schedule(pctClick, (t) => {
    simulateClick(pctEl(), t);
    state.mainPctHighlight = true;
    setCaption("% after +/− applies percent of the accumulator (200 + 10%)", t);
  });

  const eqMoveStart = pctClick + 1100;
  const eqEl = () => document.querySelector('[data-scene="main"] [data-key="eq"]');
  schedule(eqMoveStart, (t) => {
    state.pendingMove = beginCursorMove(state.cursor.x, state.cursor.y, eqEl(), t, 650);
  });

  const eqClick = eqMoveStart + 700;
  schedule(eqClick, (t) => {
    simulateClick(eqEl(), t);
    state.mainPctHighlight = false;
    state.mainEqHighlight = true;
    applyEqualsResult();
    setCaption("Keypad — AC, CE, operators, digits, %, and =", t);
  });

  at = eqClick + 2400;
  schedule(at, (t) => {
    goScene("history", t);
    setCaption("History — click a line to load its result", t);
  });

  at += 1100;
  const histItems = () => [...document.querySelectorAll("#history-list li")];
  const clickHistory = (index, moveStart) => {
    schedule(moveStart, (t) => {
      const el = histItems()[index];
      state.cursor.visible = true;
      state.pendingMove = beginCursorMove(
        state.cursor.visible ? state.cursor.x : 600,
        state.cursor.visible ? state.cursor.y : 350,
        el,
        t,
        600,
      );
    });
    schedule(moveStart + 620, (t) => {
      const el = histItems()[index];
      simulateClick(el, t);
      state.historySelected = index;
      applyHistorySelection();
      setCaption("History — click a line to load its result", t);
    });
  };

  clickHistory(1, at);
  at += 2200;
  clickHistory(2, at);

  at += 2400;
  schedule(at, (t) => {
    state.cursor.visible = false;
    goScene("collapsed", t);
    setCaption("Collapsible history — uncheck History to hide the pane", t);
  });

  at += 1200;
  const cbEl = () => $("collapse-cb");
  schedule(at, (t) => {
    state.pendingMove = beginCursorMove(
      state.cursor.visible ? state.cursor.x : 640,
      state.cursor.visible ? state.cursor.y : 380,
      cbEl(),
      t,
      700,
    );
  });
  schedule(at + 740, (t) => {
    simulateClick(cbEl(), t);
    state.collapseHidden = true;
    applyCollapse();
  });

  at += 2600;
  schedule(at, (t) => {
    state.cursor.visible = false;
    goScene("inline", t);
    setCaption("Inline /calc <expr> — evaluates without the keypad", t);
  });

  at += 1400;
  schedule(at, (t) => {
    $("inline-chat-hint")?.classList.add("is-pulse");
    state.inlineSuccess = true;
    applyInline();
    setCaption("Success logs expr = result and prepends the same line to history", t);
  });

  at += 3200;
  schedule(at, (t) => {
    state.inlineError = true;
    applyInline();
    setCaption("Invalid expressions log Could not evaluate (no history entry)", t);
  });

  at += 2800;
  schedule(at, (t) => {
    state.cursor.visible = false;
    goScene("error", t);
    setCaption("Divide-by-zero shows Error; empty history shows No calculations yet.", t);
  });

  at += 3400;
  schedule(at, (t) => {
    state.longEmphasis = true;
    applyErrorScene();
    setCaption("Long entries shrink the display font (17-digit cap)", t);
  });

  at += 3200;
  schedule(at, (t) => {
    state.cursor.visible = false;
    setCaption("Mock-ups only — behaviour described in README and tests", t);
  });

  return at + 2600;
}

const DURATION_MS = buildSchedule();
const fired = new Set();

function tick(t) {
  for (const item of SCHEDULE) {
    if (item.at <= t && !fired.has(item.id)) {
      fired.add(item.id);
      item.run(t);
    }
  }

  if (state.pendingMove) {
    const done = applyCursorMove(state.pendingMove, t);
    if (done) state.pendingMove = null;
  }

  applySceneStyles(t);
  applyBanner(t);
  applyMainHighlights();
  applyHistorySelection();
  applyCollapse();
  applyInline();
  applyErrorScene();
  applyCursorDom(t);
}

window.__walkthroughInit = () => {
  fired.clear();
  state.pendingMove = null;
  resetDom();
  state.activeScene = "main";
  state.prevScene = "main";
  state.sceneBlendStart = 0;
  document.querySelectorAll(".walkthrough-scene").forEach((el) => {
    const on = el.dataset.scene === "main";
    el.style.opacity = on ? "1" : "0";
    el.style.transform = on ? "translateY(0)" : "translateY(10px)";
  });
  tick(0);
};

window.__walkthroughTick = (t) => {
  tick(t);
};

window.__walkthroughDuration = DURATION_MS;

/** Legacy hook for debugging */
window.__walkthroughApply = (actionName) => {
  throw new Error(`Discrete actions removed; use __walkthroughTick. Unknown: ${actionName}`);
};
