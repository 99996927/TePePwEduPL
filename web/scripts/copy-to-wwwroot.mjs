// Copies dist/hr-app/browser/* into ../src/Hr.Api/wwwroot/, clearing it first
// (except .gitkeep).
import { cpSync, existsSync, mkdirSync, readdirSync, rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const source = join(root, 'dist', 'hr-app', 'browser');
const target = resolve(root, '..', 'src', 'Hr.Api', 'wwwroot');

if (!existsSync(source)) {
  console.error(`build:wwwroot: ${source} not found. Run "npm run build" first.`);
  process.exit(1);
}

mkdirSync(target, { recursive: true });
for (const entry of readdirSync(target)) {
  if (entry !== '.gitkeep') rmSync(join(target, entry), { recursive: true, force: true });
}
for (const entry of readdirSync(source)) {
  cpSync(join(source, entry), join(target, entry), { recursive: true });
}
console.log(`build:wwwroot: copied ${source} to ${target}`);
