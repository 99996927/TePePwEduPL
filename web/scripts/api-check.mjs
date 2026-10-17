// Generates the API client into a temp directory and compares it with
// src/app/api. Exits non-zero when any file is added, missing or different.
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readdirSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const committed = join(root, 'src', 'app', 'api');
const tempRoot = mkdtempSync(join(tmpdir(), 'hr-api-check-'));
const generated = join(tempRoot, 'api');

function listFiles(dir) {
  const result = [];
  const walk = (current) => {
    for (const entry of readdirSync(current, { withFileTypes: true })) {
      const full = join(current, entry.name);
      if (entry.isDirectory()) walk(full);
      else result.push(relative(dir, full).split('\\').join('/'));
    }
  };
  try {
    walk(dir);
  } catch (error) {
    if (error.code !== 'ENOENT') throw error;
  }
  return result.sort();
}

try {
  const bin = join(root, 'node_modules', 'ng-openapi-gen', 'lib', 'index.js');
  const run = spawnSync(
    process.execPath,
    [bin, '--config', 'ng-openapi-gen.json', '--output', generated],
    { cwd: root, stdio: 'inherit' },
  );
  if (run.status !== 0) {
    console.error('api:check: generator failed.');
    process.exit(run.status ?? 1);
  }

  const expected = listFiles(generated);
  const actual = listFiles(committed);
  const problems = [];
  for (const file of expected) {
    if (!actual.includes(file)) problems.push(`missing: ${file}`);
    else if (!readFileSync(join(generated, file)).equals(readFileSync(join(committed, file))))
      problems.push(`differs: ${file}`);
  }
  for (const file of actual) {
    if (!expected.includes(file)) problems.push(`stale: ${file}`);
  }

  if (problems.length > 0) {
    console.error('api:check: src/app/api is out of date. Run "npm run api:gen".');
    for (const problem of problems) console.error(`  ${problem}`);
    process.exit(1);
  }
  console.log(`api:check: OK (${expected.length} files).`);
} finally {
  rmSync(tempRoot, { recursive: true, force: true });
}
