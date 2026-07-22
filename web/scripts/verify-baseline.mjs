import fs from "node:fs";
import path from "node:path";

const repository = path.resolve(process.cwd(), "..");
const expectedVersion = "1.0.0-alpha.11";
const failures = [];
const packageJson = JSON.parse(fs.readFileSync(path.join(repository, "web", "package.json"), "utf8"));
if (packageJson.version !== expectedVersion) failures.push(`web/package.json reports ${packageJson.version}`);
for (const relative of [
  "src/Api/ConvoLab.Api/Controllers/PlatformController.cs",
  "web/src/data/platform.ts",
  "web/src/components/Sidebar.tsx",
  "web/package-lock.json",
  "README.md",
  "CHANGELOG.md",
  "docs/Architecture/ProductReadinessAssessment.md",
  "docs/Architecture/README.md",
  "docs/MASTER_CHECKLIST_STATUS.md",
  "docs/PlatformManifest.md",
  "docs/Roadmap.md",
  "docs/releases/PlatformCore-v1.0.0-alpha.11.md",
]) {
  const content = fs.readFileSync(path.join(repository, relative), "utf8");
  if (!content.includes(expectedVersion)) failures.push(`${relative} does not report ${expectedVersion}`);
}

const ignored = new Set([".git", "bin", "obj", "node_modules", "dist", "playwright-report", "test-results"]);
const extensions = new Set([".cs", ".css", ".html", ".js", ".json", ".md", ".mjs", ".ts", ".tsx", ".yml", ".yaml"]);
const mojibake = [
  String.fromCodePoint(0xc2, 0xb7),
  String.fromCodePoint(0xe2, 0x20ac),
  String.fromCodePoint(0xc3),
  String.fromCodePoint(0xfffd),
];
function scan(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    if (ignored.has(entry.name)) continue;
    const target = path.join(directory, entry.name);
    if (entry.isDirectory()) { scan(target); continue; }
    if (!extensions.has(path.extname(entry.name))) continue;
    const content = fs.readFileSync(target, "utf8");
    if (mojibake.some(value => content.includes(value))) failures.push(`${path.relative(repository, target)} contains broken encoding`);
    if ((target.includes(`${path.sep}src${path.sep}`) || target.includes(`${path.sep}web${path.sep}src${path.sep}`)) && /\b(?:USD|EUR|GBP)\b/.test(content)) failures.push(`${path.relative(repository, target)} contains a non-ZAR currency code`);
  }
}
for (const relative of ["src", "web/src", "docs"]) scan(path.join(repository, relative));

if (failures.length) {
  console.error(`Baseline verification failed:\n- ${failures.join("\n- ")}`);
  process.exit(1);
}
console.log(`Baseline version, encoding and ZAR checks passed for ${expectedVersion}.`);
