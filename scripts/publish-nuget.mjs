import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const rootDir = join(dirname(fileURLToPath(import.meta.url)), '..');
const pkg = JSON.parse(readFileSync(join(rootDir, 'package.json'), 'utf8'));
const secret = JSON.parse(readFileSync(join(rootDir, 'secret.json'), 'utf8'));

const packagePath = join(rootDir, 'UAParser.Core', 'bin', 'Release', `UAParser.Core.${pkg.version}.nupkg`);

console.log(`Publishing ${packagePath} to NuGet...`);
execFileSync('dotnet', [
    'nuget', 'push', packagePath,
    '--source', 'https://api.nuget.org/v3/index.json',
    '--api-key', secret.api
], { stdio: 'inherit' });
