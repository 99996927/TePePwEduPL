// Fails when the built app references anything outside its own host (fonts must be self-hosted).
// Usage: node scripts/no-cdn-check.mjs [dir]   (default: dist/hr-app/browser)
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const dir = resolve(process.argv[2] ?? join(root, 'dist', 'hr-app', 'browser'));
const ALLOWED = ['www.w3.org'];
const URL_PATTERN = /https?:\/\/[^\s"'`)<>\\]+/g;

if (!existsSync(dir)) {
  console.error(`fonts:check: ${dir} not found. Run "npm run build" first.`);
  process.exit(2);
}

function* files(d) {
  for (const entry of readdirSync(d)) {
    const p = join(d, entry);
    if (statSync(p).isDirectory()) yield* files(p);
    else if (entry === 'index.html' || entry.endsWith('.css')) yield p;
  }
}

const violations = [];
for (const file of files(dir)) {
  for (const url of readFileSync(file, 'utf8').match(URL_PATTERN) ?? []) {
    const host = new URL(url).hostname;
    if (!ALLOWED.includes(host)) violations.push(`${file}: ${url}`);
  }
}

if (violations.length > 0) {
  console.error('fonts:check: external references found:\n' + violations.join('\n'));
  process.exit(1);
}
console.log('fonts:check: no external references.');
