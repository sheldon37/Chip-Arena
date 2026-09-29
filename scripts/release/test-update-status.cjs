// Test fixture only: the menu and manual updater read the same bundled feed.
const {readFile}=require('node:fs/promises');
const {join}=require('node:path');
const CHECK_URL='https://chip-arena-prototype.sheldon37.chatgpt.site/api/update-check';
function createTestStatusHandler(gameRoot,forward){
  return async request=>{
    // Leave accounts, room discovery and every other HTTPS request unchanged.
    if(request.url!==CHECK_URL)return forward(request,{bypassCustomProtocolHandlers:true});
    const headers={'Cache-Control':'no-store','Access-Control-Allow-Origin':'*'};
    if(request.method!=='GET')return new Response('Method not allowed',{status:405,headers});
    try{
      const manifest=JSON.parse(await readFile(join(gameRoot,'test-feed','latest.json'),'utf8'));
      if(!/^[a-f0-9]{24}$/.test(manifest.build)||typeof manifest.version!=='string')throw Error('Invalid test manifest');
      return Response.json({build:manifest.build,version:manifest.version},{headers});
    }catch{
      // Never fall back to production when the test feed is missing or malformed.
      return Response.json({error:'Bundled test update feed unavailable'},{status:503,headers});
    }
  };
}
function installTestStatusHandler(session,gameRoot){
  const gameSession=session.fromPartition('persist:chip-arena-game');
  gameSession.protocol.handle('https',createTestStatusHandler(gameRoot,(request,options)=>gameSession.fetch(request,options)));
}
module.exports={CHECK_URL,createTestStatusHandler,installTestStatusHandler};
