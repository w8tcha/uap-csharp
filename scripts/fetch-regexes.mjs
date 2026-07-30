import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const rootDir = join(dirname(fileURLToPath(import.meta.url)), '..');
const rawBase = 'https://raw.githubusercontent.com/ua-parser/uap-core/master';

const downloads = [
    { url: `${rawBase}/regexes.yaml`, dest: join(rootDir, 'UAParser.Core', 'regexes.yaml') },
    ...[
        'regexes.js',
        'sample.js',
        'test.js',
        'test_device.yaml',
        'test_os.yaml',
        'test_ua.yaml'
    ].map(name => ({ url: `${rawBase}/tests/${name}`, dest: join(rootDir, 'UAParser.Tests', 'tests', name) }))
];

async function download({ url, dest }) {
    const response = await fetch(url);
    if (!response.ok) {
        throw new Error(`Failed to download ${url}: ${response.status} ${response.statusText}`);
    }

    await mkdir(dirname(dest), { recursive: true });
    await writeFile(dest, Buffer.from(await response.arrayBuffer()));
    console.log(`Downloaded ${url} -> ${dest}`);
}

await Promise.all(downloads.map(download));
