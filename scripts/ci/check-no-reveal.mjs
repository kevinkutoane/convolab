import fs from "node:fs";
import path from "node:path";

const sourceExtensions = new Set([".cs", ".js", ".mjs", ".ts", ".tsx"]);
const ignoredDirectories = new Set([".git", "bin", "obj", "dist", "node_modules", "playwright-report", "test-results"]);
const approvedBoundaryFiles = new Set([
  "src/Application/ConvoLab.Application/Settings/SettingsContracts.cs",
  "src/Api/ConvoLab.Api/Security/EntraAuthentication.cs",
  "src/Infrastructure/ConvoLab.Infrastructure/Settings/CompositeSecretStore.cs",
  "src/Infrastructure/ConvoLab.Infrastructure/Settings/ProviderValidationService.cs",
  "src/Infrastructure/ConvoLab.Infrastructure/Intelligence/GeminiIntelligenceExecutor.cs",
  "src/Infrastructure/ConvoLab.Infrastructure/Operations/Backups/BackupKeyProvider.cs",
  "src/tests/ConvoLab.Infrastructure.IntegrationTests/Settings/OperationalSecretStoreTests.cs",
]);

const revealPattern = /\b[A-Za-z_$][\w$]*\.RevealValue\s*\(\s*\)/;
const suspiciousPatterns = [
  { name: "logger or console output", pattern: /\b(?:_?logger|log|console|debug)\s*\.\s*\w+\s*\([^;]*RevealValue\s*\(/i },
  { name: "HTTP response exposure", pattern: /\b(?:response|httpResponse|body|headers?|content)\b[^;=]*(?:=|Add|Append|Write)[^;]*RevealValue\s*\(/i },
  { name: "telemetry exposure", pattern: /\b(?:activity|telemetry|tag|tags|meter|metric|span)\b[^;=]*(?:=|Add|SetTag|SetBaggage|TagObject)[^;]*RevealValue\s*\(/i },
  { name: "exception message", pattern: /\bthrow\s+new\b[^;]*(?:RevealValue\s*\(|\$"|"\s*\+|string\.Format)/i },
  { name: "interpolation or concatenation", pattern: /(?:\$"[^"]*RevealValue\s*\(|["'][^"']*["']\s*\+[^;]*RevealValue\s*\(|RevealValue\s*\([^;]*\+\s*["'])/i },
];

function relative(root, file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function collectFiles(root, directory = root) {
  const files = [];
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    if (entry.isDirectory() && relative(root, path.join(directory, entry.name)) === "scripts/ci") continue;
    if (entry.isDirectory() && !ignoredDirectories.has(entry.name)) {
      files.push(...collectFiles(root, path.join(directory, entry.name)));
    } else if (entry.isFile() && sourceExtensions.has(path.extname(entry.name))) {
      files.push(path.join(directory, entry.name));
    }
  }
  return files;
}

export function scan(root) {
  const findings = [];
  for (const file of collectFiles(root)) {
    const relativePath = relative(root, file);
    const approved = approvedBoundaryFiles.has(relativePath);
    const lines = fs.readFileSync(file, "utf8").split(/\r?\n/);
    lines.forEach((line, index) => {
      if (!revealPattern.test(line)) return;
      const suspicious = suspiciousPatterns.find(({ pattern }) => pattern.test(line));
      if (!approved || suspicious) {
        findings.push({
          file: relativePath,
          line: index + 1,
          reason: suspicious?.name ?? "RevealValue outside an approved resolution boundary",
        });
      }
    });
  }
  return findings;
}

const root = path.resolve(process.argv[2] ?? ".");
const findings = scan(root);
if (findings.length > 0) {
  console.error("Unsafe RevealValue exposure patterns found:");
  for (const finding of findings) {
    console.error(`- ${finding.file}:${finding.line}: ${finding.reason}`);
  }
  process.exit(2);
}
console.log("RevealValue exposure guard passed.");
