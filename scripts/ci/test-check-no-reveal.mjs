import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { scan } from "./check-no-reveal.mjs";

const root = fs.mkdtempSync(path.join(os.tmpdir(), "convolab-reveal-guard-"));
const write = (file, content) => {
  const target = path.join(root, file);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, content);
};

write("src/Infrastructure/ConvoLab.Infrastructure/Operations/Backups/BackupKeyProvider.cs",
  "var secretValue = result.RevealValue();");
assert.deepEqual(scan(root), [], "approved secret-resolution boundary should pass");

write("src/UnsafeLogging.cs",
  "_logger.LogInformation(\"secret {Value}\", result.RevealValue());");
assert.equal(scan(root).length, 1, "unsafe logger use should fail");

write("src/UnsafeHttp.cs",
  "response.Body = result.RevealValue();");
assert.equal(scan(root).length, 2, "unsafe HTTP exposure should fail");

write("src/UnsafeTelemetry.cs",
  "activity.SetTag(\"secret\", result.RevealValue());");
assert.equal(scan(root).length, 3, "unsafe telemetry exposure should fail");

write("src/UnsafeString.cs",
  "var message = $\"secret={result.RevealValue()}\";");
assert.equal(scan(root).length, 4, "unsafe string exposure should fail");

write("src/UnsafeException.cs",
  "throw new InvalidOperationException($\"secret={result.RevealValue()}\");");
assert.equal(scan(root).length, 5, "unsafe exception exposure should fail");

console.log("RevealValue guard fixture tests passed.");
