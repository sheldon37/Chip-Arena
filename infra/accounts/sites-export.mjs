// The operator provisions the recipient public key out of band. Requests cannot select it.
export const accountTables = ['player_accounts','account_audit','account_xp','account_xp_awards','player_invites'];
const encode = buffer => {
  const bytes = new Uint8Array(buffer);
  let str = '';
  for (let i = 0; i < bytes.length; i += 8192) str += String.fromCharCode(...bytes.subarray(i,i+8192));
  return btoa(str);
};
export async function exportAccounts(request, env) {
  const deny = () => new Response(null, {status:404});
  if (request.method !== 'POST' || !env.ACCOUNT_EXPORT_RECIPIENT || !/^[0-9a-f]{64}$/.test(env.ACCOUNT_EXPORT_TOKEN ?? '')) return deny();
  const token = request.headers.get('Authorization');
  if (!token || token !== 'Bearer ' + env.ACCOUNT_EXPORT_TOKEN) return deny();
  try {
    const recipient = await crypto.subtle.importKey('spki', Uint8Array.from(atob(env.ACCOUNT_EXPORT_RECIPIENT), c => c.charCodeAt(0)),
      {name:'RSA-OAEP',hash:'SHA-256'}, false, ['wrapKey']);
    if (recipient.algorithm.modulusLength < 3072) throw Error();
    const catalog = "SELECT type,name,tbl_name,sql FROM sqlite_schema WHERE type IN ('table','index','trigger','view') AND name NOT GLOB 'sqlite_*' AND name NOT GLOB '_cf_*' AND tbl_name NOT IN ('d1_migrations','__drizzle_migrations') ORDER BY type DESC,name";
    const queries = [catalog, ...accountTables.map(t => 'SELECT * FROM ' + t)];
    const results = await env.DB.batch(queries.map(q => env.DB.prepare(q)));
    if (results.length !== queries.length || results.some(r => !r.success || !Array.isArray(r.results))) throw Error();
    const payload = new TextEncoder().encode(JSON.stringify({schema:results[0].results,
      rows:Object.fromEntries(accountTables.map((t,i) => [t,results[i+1].results]))}, (_,v) => {
        if (typeof v === 'number' && !Number.isSafeInteger(v)) throw Error();
        return v;
      }));
    const key = await crypto.subtle.generateKey({name:'AES-GCM',length:256},true,['encrypt']);
    const iv = crypto.getRandomValues(new Uint8Array(12));
    const sealed = await crypto.subtle.encrypt({name:'AES-GCM',iv},key,payload);
    const wrapped = await crypto.subtle.wrapKey('raw',key,recipient,'RSA-OAEP');
    return Response.json({format:'chip-account-export-1',wrapped:encode(wrapped),iv:encode(iv),sealed:encode(sealed)},
      {headers:{'Cache-Control':'no-store'}});
  } catch { return new Response(null,{status:503,headers:{'Cache-Control':'no-store'}}); }
}
