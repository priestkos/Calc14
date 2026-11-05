#!/usr/bin/env node
/**
 * Record Calc14 mock-up walkthrough (1280×720 @ 30fps) and export MP4 + GIF.
 * Frame-driven timeline for exact duration and smooth cursor motion.
 */
import { chromium } from "playwright";
import { spawnSync } from "child_process";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { mkdir, rm } from "node:fs/promises";

const srcDir = path.dirname(fileURLToPath(import.meta.url));
const videosDir = path.join(srcDir, "..");
const framesDir = path.join(srcDir, "frames");
const htmlPath = path.join(srcDir, "walkthrough.html");

const FPS = 30;
const DURATION_SEC = 15;
const FRAME_COUNT = FPS * DURATION_SEC;

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
  await rm(framesDir, { recursive: true, force: true });
  await mkdir(framesDir, { recursive: true });

  const mp4Path = path.join(videosDir, "mockup-walkthrough.mp4");
  const gifPath = path.join(videosDir, "mockup-walkthrough.gif");

  const browser = await chromium.launch({
    headless: true,
    args: ["--font-render-hinting=none"],
  });

  const page = await browser.newPage();
  await page.setViewportSize({ width: 1280, height: 720 });
  await page.goto(`file://${htmlPath}`, { waitUntil: "load" });
  await page.waitForFunction(() => typeof window.__walkthroughTick === "function");
  await page.evaluate(() => window.__walkthroughInit());

  for (let i = 0; i < FRAME_COUNT; i += 1) {
    const t = (i / FPS) * 1000;
    await page.evaluate((ms) => window.__walkthroughTick(ms), t);
    const name = `frame-${String(i).padStart(5, "0")}.png`;
    await page.screenshot({ path: path.join(framesDir, name), type: "png" });
    if (i % 90 === 0) console.log(`Frame ${i}/${FRAME_COUNT}`);
  }

  await browser.close();

  runFfmpeg([
    "-framerate",
    String(FPS),
    "-i",
    path.join(framesDir, "frame-%05d.png"),
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

  await rm(framesDir, { recursive: true, force: true });

  const mp4Size = fs.statSync(mp4Path).size;
  const gifSize = fs.statSync(gifPath).size;
  console.log(
    `Wrote ${path.basename(mp4Path)} (${formatBytes(mp4Size)}, ${DURATION_SEC}s)`,
  );
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
