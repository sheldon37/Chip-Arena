// Test tooling only. Production updater remains pinned to its original HTTPS URL.
import {readFile} from 'node:fs/promises';
import {resolve} from 'node:path';
const FEED_NAME=/^(latest\.json|latest-v2\.json|arena-[a-f0-9]{16}\.json\.gz|arena-[a-f0-9]{16}\.pack)$/;
export function localFeed(directory, updateBase) {
  return async url => {
    const name=String(url).slice(updateBase.length);
    if(!String(url).startsWith(updateBase) || !FEED_NAME.test(name)) throw Error('Test feed refused unexpected URL');
    // A missing file is a 404, so an updater asking for latest-v2.json falls back to schema 1 like on a real channel.
    try{return new Response(await readFile(resolve(directory,name)),{status:200});}catch{return new Response('Not found',{status:404});}
  };
}
