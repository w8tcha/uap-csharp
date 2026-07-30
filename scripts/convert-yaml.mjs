import { execFileSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const rootDir = join(dirname(fileURLToPath(import.meta.url)), '..');
const projectDir = join(rootDir, 'YamlConverter');
const outputDir = join(projectDir, 'bin', 'Debug', 'net10.0');

console.log('Building YamlConverter...');
execFileSync('dotnet', ['build', projectDir, '-c', 'Debug'], { stdio: 'inherit' });

console.log('Converting regexes.yaml -> regexes.json...');
const exe = process.platform === 'win32' ? join(outputDir, 'YamlConverter.exe') : join(outputDir, 'YamlConverter');
execFileSync(exe, [], { cwd: outputDir, stdio: 'inherit' });
