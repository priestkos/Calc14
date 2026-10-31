#!/usr/bin/env node
import { chromium } from "playwright";
import { fileURLToPath } from "url";
import path from "path";

const srcDir = path.dirname(fileURLToPath(import.meta.url));
const outDir = path.join(srcDir, "..");

const pages = [
  { html: "01-main-window-keypad.html", png: "01-main-window-keypad.png" },
  { html: "02-history-interaction.html", png: "02-history-interaction.png" },
  { html: "03-history-collapsed-empty.html", png: "03-history-collapsed-empty.png" },
  { html: "04-inline-calc-expression.html", png: "04-inline-calc-expression.png" },
  { html: "05-error-long-display.html", png: "05-error-long-display.png" },
];

const browser = await chromium.launch();
const context = await browser.newContext({
  viewport: { width: 1280, height: 720 },
  deviceScaleFactor: 1,
});
const page = await context.newPage();

for (const { html, png } of pages) {
  const fileUrl = `file://${path.join(srcDir, html)}`;
  await page.goto(fileUrl, { waitUntil: "load" });
  await page.screenshot({
    path: path.join(outDir, png),
    type: "png",
    fullPage: false,
  });
  console.log("Wrote", png);
}

await browser.close();
