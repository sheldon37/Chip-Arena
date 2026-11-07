import {execFileSync} from 'node:child_process';
import {appendFileSync} from 'node:fs';
const {BASE_SHA,HEAD_SHA,GITHUB_OUTPUT}=process.env;
if(!/^[a-f0-9]{40}$/.test(BASE_SHA||'')||!/^[a-f0-9]{40}$/.test(HEAD_SHA||''))throw Error('Missing exact comparison SHAs.');
const files=execFileSync('git',['diff','--name-only','-z',BASE_SHA,HEAD_SHA],{encoding:'utf8'}).split('\0').filter(Boolean);
const docsOnly=files.every(f=>/^(docs\/|(?:README|AGENTS|CLAUDE)\.md$|\.github\/PULL_REQUEST_TEMPLATE\.md$)/.test(f));
// Unity-only changes are verified by unity-conversion.yml (native-source, native-updater-windows).
const nativeOnly=files.every(f=>/^(unity\/|docs\/|(?:README|AGENTS|CLAUDE)\.md$|\.github\/PULL_REQUEST_TEMPLATE\.md$|\.github\/workflows\/(?:unity-conversion|unity-prototype|native-cloudflare)\.yml$)/.test(f));
// The frozen Electron game's suites are not run in CI (owner, 2026-10-15). Everything else checks the relay the
// Unity client uses (accounts, room directory). The HTTPS room round trip (tests/https-network.ts) plays the Electron
// game over /exchange, which the Unity client does not use, so it stays local (npm run verify:relay-full).
const mode=docsOnly?'docs':nativeOnly?'native':'relay';
console.log(`Verification scope: ${mode}; ${files.length} changed paths.`);
if(GITHUB_OUTPUT)appendFileSync(GITHUB_OUTPUT,`mode=${mode}\n`);
