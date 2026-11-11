// Points git at the repository's hooks in .githooks, so the pre-commit formatter runs for everyone.
// Runs from `npm install` and from the first .NET build. Never fails the install:
// outside a git checkout (e.g. a Docker build) there is nothing to set up.
import { execFileSync } from 'node:child_process';

try {
  const root = execFileSync('git', ['rev-parse', '--show-toplevel'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
  execFileSync('git', ['-C', root, 'config', 'core.hooksPath', '.githooks'], { stdio: 'ignore' });
} catch {
  // Not a git checkout, or git isn't installed.
}
