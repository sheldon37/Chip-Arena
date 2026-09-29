// Test tooling only. Production updater remains pinned to its original HTTPS URL.
import {readFile} from 'node:fs/promises';
import {resolve} from 'node:path';
export function localFeed(directory, updateBase) {
  return async url => {
    const name=String(url).slice(updateBase.length);
    if(!String(url).startsWith(updateBase) || !(name==='latest.json'||/^arena-[a-f0-9]{16}\.json\.gz$/.test(name))) throw Error('Test feed refused unexpected URL');
    return new Response(await readFile(resolve(directory,name)),{status:200});
  };
}
