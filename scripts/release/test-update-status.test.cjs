const {test}=require('node:test');
const assert=require('node:assert/strict');
const {mkdtemp,mkdir,writeFile,rm}=require('node:fs/promises');
const {tmpdir}=require('node:os');
const {join}=require('node:path');
const {CHECK_URL,createTestStatusHandler,installTestStatusHandler}=require('./test-update-status.cjs');

test('test status uses bundled metadata, rereads changes and never falls back on feed errors',async()=>{
 const root=await mkdtemp(join(tmpdir(),'chip-feed-status-'));
 try{
  await mkdir(join(root,'test-feed'));
  const file=join(root,'test-feed','latest.json');
  let forwarded=0;
  const check=createTestStatusHandler(root,()=>{forwarded++;throw Error('Unexpected network request');});
  const request=new Request(CHECK_URL);
  let r=await check(request);assert.equal(r.status,503);
  const manifest={build:'a'.repeat(24),version:'candidate-a',file:'not-public',sha256:'not-public'};
  await writeFile(file,JSON.stringify(manifest));
  r=await check(request);assert.equal(r.status,200);assert.equal(r.headers.get('Access-Control-Allow-Origin'),'*');
  assert.equal(r.headers.get('Cache-Control'),'no-store');assert.deepEqual(await r.json(),{build:manifest.build,version:manifest.version});
  await writeFile(file,JSON.stringify({build:'b'.repeat(24),version:'candidate-b'}));
  assert.equal((await (await check(request)).json()).version,'candidate-b');
  for(const contents of ['not-json','{}',JSON.stringify({build:'x',version:'v'})]){
   await writeFile(file,contents);assert.equal((await check(request)).status,503);
  }
  assert.equal((await check(new Request(CHECK_URL,{method:'POST'}))).status,405);
  assert.equal(forwarded,0);
 }finally{await rm(root,{recursive:true,force:true});}
});

test('other HTTPS requests retain their session, method, headers and body without recursion',async()=>{
 const request=new Request('https://chip-arena-prototype.sheldon37.chatgpt.site/api/rooms/v2/exchange',{method:'POST',headers:{'X-Test':'sentinel'},body:'test-body'});
 const expected=new Response('forwarded');let calls=0;
 const check=createTestStatusHandler('/unused',async(actual,options)=>{
  calls++;assert.equal(actual,request);assert.equal(actual.method,'POST');assert.equal(actual.headers.get('X-Test'),'sentinel');
  assert.equal(await actual.text(),'test-body');assert.equal(options.bypassCustomProtocolHandlers,true);return expected;
 });
 assert.equal(await check(request),expected);assert.equal(calls,1);
});

test('handler installs only on the game partition and forwards through that session',async()=>{
 let handler;const request=new Request('https://unrelated.example/');
 const gameSession={protocol:{handle:(scheme,fn)=>{assert.equal(scheme,'https');handler=fn;}},fetch:async(actual,options)=>{
  assert.equal(actual,request);assert.equal(options.bypassCustomProtocolHandlers,true);return new Response('ok');
 }};
 installTestStatusHandler({fromPartition:partition=>{assert.equal(partition,'persist:chip-arena-game');return gameSession;}},'/fixture');
 assert.equal(await (await handler(request)).text(),'ok');
});
