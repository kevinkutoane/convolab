import { gzipSync } from "node:zlib";
import { existsSync, readFileSync, statSync, writeFileSync } from "node:fs";
import path from "node:path";

const root = process.cwd();
const manifestPath = path.join(root, "dist", ".vite", "manifest.json");
if (!existsSync(manifestPath)) throw new Error("Vite manifest not found. Run the production build first.");

const manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
const entries = Object.entries(manifest);
const entry = entries.find(([, value]) => value.isEntry);
if (!entry) throw new Error("No Vite entry was found in the manifest.");

const limits = {
  initialJs: { raw: 300 * 1024, gzip: 100 * 1024 },
  routeJs: { raw: 200 * 1024, gzip: 70 * 1024 },
  initialCss: { raw: 70 * 1024, gzip: 15 * 1024 },
  routeCss: { raw: 60 * 1024, gzip: 12 * 1024 },
};
const failures = [];

function graph(key, visited = new Set()) {
  if (visited.has(key) || !manifest[key]) return visited;
  visited.add(key);
  for (const dependency of manifest[key].imports ?? []) graph(dependency, visited);
  return visited;
}

function assetSize(file) {
  const target = path.join(root, "dist", file);
  const content = readFileSync(target);
  return { raw: statSync(target).size, gzip: gzipSync(content).length };
}

function filesFor(keys, extension) {
  const files = new Set();
  for (const key of keys) {
    const item = manifest[key];
    if (!item) continue;
    if (item.file?.endsWith(extension)) files.add(item.file);
    if (extension === ".css") for (const file of item.css ?? []) files.add(file);
  }
  return files;
}

function summarize(kind, name, files, budget) {
  const assets = [...files];
  const size = assets.reduce((total, file) => {
    const current = assetSize(file);
    return { raw: total.raw + current.raw, gzip: total.gzip + current.gzip };
  }, { raw: 0, gzip: 0 });
  const row = { kind, name, assets, raw: size.raw, gzip: size.gzip, rawKiB: Number((size.raw / 1024).toFixed(1)), gzipKiB: Number((size.gzip / 1024).toFixed(1)) };
  if (size.raw > budget.raw || size.gzip > budget.gzip) failures.push(`${kind} ${name}: ${size.raw} raw / ${size.gzip} gzip`);
  return row;
}

const [entryKey] = entry;
const initialGraph = graph(entryKey);
const initialFiles = filesFor(initialGraph, ".js");
const initialCssFiles = filesFor(initialGraph, ".css");
const rows = [
  summarize("initial-js", entryKey, initialFiles, limits.initialJs),
  summarize("initial-css", entryKey, initialCssFiles, limits.initialCss),
];

for (const [key, value] of entries) {
  if (!value.isDynamicEntry) continue;
  const routeGraph = graph(key);
  const routeJs = filesFor(routeGraph, ".js");
  const routeCss = filesFor(routeGraph, ".css");
  for (const file of initialFiles) routeJs.delete(file);
  for (const file of initialCssFiles) routeCss.delete(file);
  rows.push(summarize("route-js", key, routeJs, limits.routeJs));
  for (const file of routeCss) rows.push(summarize("route-css", file, new Set([file]), limits.routeCss));
}

console.table(rows.map(({ kind, name, rawKiB, gzipKiB }) => ({ kind, name, raw: `${rawKiB} KiB`, gzip: `${gzipKiB} KiB` })));
writeFileSync(path.join(root, "dist", "bundle-report.json"), `${JSON.stringify({ limits, assets: rows }, null, 2)}\n`);
writeFileSync(path.join(root, "dist", "bundle-report.md"), [
  "# Studio bundle report",
  "",
  "| Kind | Entry | Raw KiB | Gzip KiB |",
  "| --- | --- | ---: | ---: |",
  ...rows.map(row => `| ${row.kind} | \`${row.name}\` | ${row.rawKiB} | ${row.gzipKiB} |`),
  "",
].join("\n"));
if (failures.length) {
  console.error(`Bundle budget exceeded:\n- ${failures.join("\n- ")}`);
  process.exit(1);
}
console.log(`Aggregate bundle budgets passed for the initial graph and ${entries.filter(([, value]) => value.isDynamicEntry).length} lazy routes.`);
