#!/usr/bin/env node
/**
 * Record Calc14 mock-up walkthrough (1280×720) and export MP4 + GIF.
 * Deterministic timeline — no wall-clock drift in scene logic.
 */
import { chromium } from "playwright";
import { spawnSync } from "child_process";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const srcDir = path.dirname(fileURLToPath(import.meta.url));
const videosDir = path.join(srcDir, "..");
const htmlPath = path.join(srcDir, "walkthrough.html");
const webmPath = path.join(videosDir, ".walkthrough-record.webm");
const mp4Path = path.join(videosDir, "mockup-walkthrough.mp4");
const gifPath = path.join(videosDir, "mockup-walkthrough.gif");

/** @type {{ action: string, ms: number }[]} */
const TIMELINE = [
  { action: "reset", ms: 400 },
  { action: "disclaimerHold", ms: 2800 },
  { action: "hideDisclaimer", ms: 600 },
  { action: "sceneMainIntro", ms: 3200 },
  { action: "highlightPercent", ms: 2800 },
  { action: "highlightEquals", ms: 2800 },
  { action: "sceneHistoryIntro", ms: 2200 },
  { action: "historySelect", ms: 1400, arg: 1 },
  { action: "historySelect", ms: 1400, arg: 2 },
  { action: "sceneCollapsedIntro", ms: 2200 },
  { action: "collapseHistoryOff", ms: 2800 },
  { action: "sceneInlineIntro", ms: 2200 },
  { action: "inlineShowSuccess", ms: 3200 },
  { action: "inlineShowError", ms: 2800 },
  { action: "sceneErrorIntro", ms: 3200 },
  { action: "emphasizeLongDisplay", ms: 3200 },
  { action: "outro", ms: 2400 },
];

function runFfmpeg(args) {
  const result = spawnSync("ffmpeg", ["-y", ...args], {
    stdio: "inherit",
  });
  if (result.status !== 0) {
    throw new Error(`ffmpeg exited with code ${result.status}`);
  }
}

function formatBytes(n) {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`;
  return `${(n / (1024 * 1024)).toFixed(2)} MB`;
}

async function main() {
  if (fs.existsSync(webmPath)) fs.unlinkSync(webmPath);

  const browser = await chromium.launch({
    headless: true,
    args: ["--font-render-hinting=none"],
  });

  const context = await browser.newContext({
    viewport: { width: 1280, height: 720 },
    deviceScaleFactor: 1,
    recordVideo: {
      dir: videosDir,
      size: { width: 1280, height: 720 },
    },
  });

  const page = await context.newPage();
  await page.goto(`file://${htmlPath}`, { waitUntil: "load" });
  await page.waitForFunction(() => typeof window.__walkthroughApply === "function");

  for (const step of TIMELINE) {
    await page.evaluate(
      ({ action, arg }) => window.__walkthroughApply(action, arg),
      { action: step.action, arg: step.arg },
    );
    await page.waitForTimeout(step.ms);
  }

  const recordedPath = await page.video().path();
  await context.close();
  await browser.close();

  fs.renameSync(recordedPath, webmPath);

  runFfmpeg([
    "-i",
    webmPath,
    "-c:v",
    "libx264",
    "-preset",
    "slow",
    "-crf",
    "28",
    "-pix_fmt",
    "yuv420p",
    "-movflags",
    "+faststart",
    "-an",
    mp4Path,
  ]);

  runFfmpeg([
    "-i",
    mp4Path,
    "-vf",
    "fps=8,scale=800:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=128[p];[s1][p]paletteuse=dither=bayer:bayer_scale=3",
    "-loop",
    "0",
    gifPath,
  ]);

  fs.unlinkSync(webmPath);

  const mp4Size = fs.statSync(mp4Path).size;
  const gifSize = fs.statSync(gifPath).size;
  console.log(`Wrote ${path.basename(mp4Path)} (${formatBytes(mp4Size)})`);
  console.log(`Wrote ${path.basename(gifPath)} (${formatBytes(gifSize)})`);

  if (mp4Size > 10 * 1024 * 1024) {
    console.warn("Warning: MP4 exceeds 10 MB target");
  }
  if (gifSize > 5 * 1024 * 1024) {
    console.warn("Warning: GIF exceeds 5 MB target");
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
