import fs from "node:fs";
import path from "node:path";

const repository = fs.existsSync(path.join(process.cwd(), "web", "package.json"))
  ? process.cwd()
  : path.resolve(process.cwd(), "..");
const failures = [];
const currentVersion = "1.0.0-enterprise";

const authoritativeFiles = [
  "Directory.Build.props",
  "package.json",
  "web/package.json",
  "src/Application/ConvoLab.Application/Operations/OperationalContracts.cs",
  "web/src/data/platform.ts",
];

for (const relative of authoritativeFiles) {
  const absolutePath = path.join(repository, relative);
  if (!fs.existsSync(absolutePath)) continue;
  const content = fs.readFileSync(absolutePath, "utf8");
  if (!content.includes(currentVersion)) {
    failures.push(`${relative} does not report ${currentVersion}`);
  }
}

const historicalReleases = {
  "docs/releases/PlatformCore-v1.0.0-alpha.18.md": "1.0.0-alpha.18",
  "docs/releases/PlatformCore-v1.0.0-alpha.19.md": "1.0.0-alpha.19",
};

for (const [relative, expectedHistoricalVersion] of Object.entries(historicalReleases)) {
  const absolutePath = path.join(repository, relative);
  if (!fs.existsSync(absolutePath)) continue;
  const content = fs.readFileSync(absolutePath, "utf8");
  if (!content.includes(expectedHistoricalVersion)) {
    failures.push(`${relative} does not document historical release ${expectedHistoricalVersion}`);
  }
}

const roadmapPath = path.join(repository, "docs/Roadmap.md");
if (fs.existsSync(roadmapPath)) {
  const roadmapContent = fs.readFileSync(roadmapPath, "utf8");
  if (!roadmapContent.includes(currentVersion)) {
    failures.push(`docs/Roadmap.md does not report the release candidate ${currentVersion}`);
  }
}

const platformPath = path.join(repository, "web/src/data/platform.ts");
if (fs.existsSync(platformPath)) {
  const platformContent = fs.readFileSync(platformPath, "utf8");
  if (platformContent.includes("alpha.18 — Security & Compliance Hardening")) {
    failures.push("web/src/data/platform.ts retains stale alpha.18 workstream metadata");
  }
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
console.log(`Baseline version, encoding and ZAR checks passed for ${currentVersion}.`);
