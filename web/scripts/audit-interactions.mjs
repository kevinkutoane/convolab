import fs from "node:fs";
import path from "node:path";
import ts from "typescript";

const sourceRoot = path.resolve("src");
const files = [];
const failures = [];

function collect(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const target = path.join(directory, entry.name);
    if (entry.isDirectory()) collect(target);
    else if (target.endsWith(".tsx")) files.push(target);
  }
}

function location(source, node) {
  const point = source.getLineAndCharacterOfPosition(node.getStart(source));
  return `${path.relative(sourceRoot, source.fileName)}:${point.line + 1}`;
}

function attribute(opening, source, name) {
  return opening.attributes.properties.find(
    item => ts.isJsxAttribute(item) && item.name.getText(source) === name,
  );
}

function literalAttributeValue(value) {
  if (!value?.initializer) return undefined;
  if (ts.isStringLiteral(value.initializer)) return value.initializer.text;
  if (ts.isJsxExpression(value.initializer) && value.initializer.expression && ts.isStringLiteral(value.initializer.expression)) {
    return value.initializer.expression.text;
  }
  return undefined;
}

collect(sourceRoot);

for (const file of files) {
  const content = fs.readFileSync(file, "utf8");
  const source = ts.createSourceFile(file, content, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);

  function visit(node) {
    if (ts.isJsxElement(node) || ts.isJsxSelfClosingElement(node)) {
      const opening = ts.isJsxElement(node) ? node.openingElement : node;
      const tag = opening.tagName.getText(source);
      if (tag === "button") {
        const onClick = attribute(opening, source, "onClick");
        const type = literalAttributeValue(attribute(opening, source, "type"));
        if (!onClick && type !== "submit") {
          failures.push(`${location(source, opening)} button has no onClick handler and is not a submit button`);
        }
      }

      if (tag === "a" || tag === "Link" || tag === "NavLink") {
        const destination = literalAttributeValue(attribute(opening, source, tag === "a" ? "href" : "to"));
        if (destination === "" || destination === "#" || destination?.startsWith("javascript:")) {
          failures.push(`${location(source, opening)} ${tag} has a placeholder destination`);
        }
      }
    }
    ts.forEachChild(node, visit);
  }

  visit(source);
}

if (failures.length) {
  console.error("Interaction audit failed:\n" + failures.map(item => `- ${item}`).join("\n"));
  process.exitCode = 1;
} else {
  console.log(`Interaction audit passed (${files.length} TSX files checked).`);
}
