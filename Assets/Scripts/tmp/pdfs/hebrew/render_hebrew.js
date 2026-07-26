const path = require("path");
const { pathToFileURL } = require("url");
const { chromium } = require("C:/Users/Yoav Cohen/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");

async function main() {
  const workspace = "C:/UniversityProject/Aerial-Plane-Attack-Cool-XXX3/Assets/Scripts";
  const input = path.join(workspace, "tmp/pdfs/hebrew/guide_he.html");
  const output = path.join(
    workspace,
    "output/pdf/Aerial_Plane_Attack_Grader_Study_Guide_HE.pdf"
  );

  const browser = await chromium.launch({
    headless: true,
    executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  });
  const page = await browser.newPage({ viewport: { width: 1280, height: 1600 } });
  await page.goto(pathToFileURL(input).href, { waitUntil: "networkidle" });
  await page.evaluate(() => document.fonts.ready);

  const checks = await page.evaluate(() =>
    [...document.querySelectorAll(".page")].map((el, index) => {
      const rect = el.getBoundingClientRect();
      let maxBottom = rect.top;
      for (const child of el.children) {
        if (child.classList.contains("footer")) continue;
        maxBottom = Math.max(maxBottom, child.getBoundingClientRect().bottom);
      }
      return {
        page: index + 1,
        clientHeight: el.clientHeight,
        scrollHeight: el.scrollHeight,
        contentBottom: Math.round(maxBottom - rect.top),
        availableBottom: Math.round(rect.height - 42),
        overflow:
          el.scrollHeight > el.clientHeight + 1 ||
          maxBottom > rect.bottom - 34,
      };
    })
  );

  console.log(JSON.stringify(checks, null, 2));
  const bad = checks.filter((x) => x.overflow);
  if (bad.length) {
    throw new Error(`Detected page overflow: ${bad.map((x) => x.page).join(", ")}`);
  }

  await page.pdf({
    path: output,
    format: "A4",
    printBackground: true,
    preferCSSPageSize: true,
    margin: { top: "0", right: "0", bottom: "0", left: "0" },
  });
  await browser.close();
  console.log(output);
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
