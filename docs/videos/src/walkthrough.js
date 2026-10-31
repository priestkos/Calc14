/** Deterministic walkthrough state — driven by render.mjs via window.__walkthroughApply */

function $(id) {
  return document.getElementById(id);
}

function setCaption(text) {
  $("caption").textContent = text;
}

function showScene(name) {
  document.querySelectorAll(".walkthrough-scene").forEach((el) => {
    el.classList.toggle("is-active", el.dataset.scene === name);
  });
}

function resetAll() {
  $("disclaimer-banner").classList.remove("is-hidden");
  setCaption("");
  showScene("main");

  $("main-entry").textContent = "10";
  $("main-sub").textContent = "200 +";
  document.querySelectorAll("#main-keys .key-highlight").forEach((k) =>
    k.classList.remove("key-highlight"),
  );

  $("history-entry").textContent = "220";
  document.querySelectorAll("#history-list li").forEach((li) =>
    li.classList.remove("selected"),
  );

  $("collapse-cb").checked = true;
  $("collapse-panel").classList.remove("is-collapsed");
  $("collapse-clear").classList.remove("is-collapsed");

  $("log-success").classList.add("is-hidden");
  $("log-error").classList.add("is-hidden");
  $("inline-history-line").classList.add("is-hidden");

  $("error-window").style.opacity = "1";
  $("long-window").style.opacity = "0.35";
  $("long-window").style.outline = "none";
  $("error-window").style.outline = "none";
}

const actions = {
  reset() {
    resetAll();
  },
  disclaimerHold() {
    resetAll();
    setCaption("Calc14 UI mock-up walkthrough");
  },
  hideDisclaimer() {
    $("disclaimer-banner").classList.add("is-hidden");
  },
  sceneMainIntro() {
    showScene("main");
    setCaption("Main window — keypad, pending operation, and history");
  },
  highlightPercent() {
    document.querySelector('[data-key="pct"]')?.classList.add("key-highlight");
    setCaption("% after +/− applies percent of the accumulator (200 + 10%)");
  },
  highlightEquals() {
    document.querySelector('[data-key="pct"]')?.classList.remove("key-highlight");
    document.querySelector('[data-key="eq"]')?.classList.add("key-highlight");
    $("main-entry").textContent = "220";
    $("main-sub").textContent = "";
    setCaption("Keypad — AC, CE, operators, digits, %, and =");
  },
  sceneHistoryIntro() {
    showScene("history");
    setCaption("History — click a line to load its result");
  },
  historySelect(index = 0) {
    const items = [...document.querySelectorAll("#history-list li")];
    items.forEach((li, i) => li.classList.toggle("selected", i === index));
    const target = items[index];
    if (target) {
      $("history-entry").textContent = target.dataset.result ?? "220";
    }
    setCaption("History — click a line to load its result");
  },
  sceneCollapsedIntro() {
    showScene("collapsed");
    setCaption("Collapsible history — uncheck History to hide the pane");
  },
  collapseHistoryOff() {
    $("collapse-cb").checked = false;
    $("collapse-panel").classList.add("is-collapsed");
    $("collapse-clear").classList.add("is-collapsed");
  },
  sceneInlineIntro() {
    showScene("inline");
    setCaption("Inline /calc <expr> — evaluates without the keypad");
  },
  inlineShowSuccess() {
    $("log-success").classList.remove("is-hidden");
    $("inline-history-line").classList.remove("is-hidden");
    setCaption("Success logs expr = result and prepends the same line to history");
  },
  inlineShowError() {
    $("log-error").classList.remove("is-hidden");
    setCaption("Invalid expressions log Could not evaluate (no history entry)");
  },
  sceneErrorIntro() {
    showScene("error");
    setCaption("Divide-by-zero shows Error; empty history shows No calculations yet.");
  },
  emphasizeLongDisplay() {
    $("error-window").style.opacity = "0.35";
    $("long-window").style.opacity = "1";
    $("long-window").style.outline = "2px solid rgba(242, 115, 26, 0.8)";
    setCaption("Long entries shrink the display font (17-digit cap)");
  },
  outro() {
    setCaption("Mock-ups only — behaviour described in README and tests");
  },
};

window.__walkthroughApply = (actionName, arg) => {
  const fn = actions[actionName];
  if (!fn) {
    throw new Error(`Unknown walkthrough action: ${actionName}`);
  }
  fn(arg);
};

resetAll();
