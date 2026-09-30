// Test fixture only: the menu update badge reads the same test channel as the Update button.
const {readFile}=require('node:fs/promises');
const {join}=require('node:path');
const {CHECK_URL}=require('./test-update-status.cjs');
function createRemoteStatusHandler(gameRoot,forward){
  return async request=>{
    // Accounts, room discovery and every other HTTPS request pass through unchanged.
    if(request.url!==CHECK_URL)return forward(request,{bypassCustomProtocolHandlers:true});
    const headers={'Cache-Control':'no-store','Access-Control-Allow-Origin':'*'};
    if(request.method!=='GET')return new Response('Method not allowed',{status:405,headers});
    try{
      const channel=JSON.parse(await readFile(join(gameRoot,'test-channel.json'),'utf8'));
      const response=await forward(new Request(channel.base+'latest.json?t='+Date.now(),{cache:'no-store'}),{bypassCustomProtocolHandlers:true});
      if(!response.ok)throw Error('Test channel unavailable');
      const manifest=await response.json();
      if(!/^[a-f0-9]{24}$/.test(manifest.build)||typeof manifest.version!=='string')throw Error('Invalid test manifest');
      return Response.json({build:manifest.build,version:manifest.version},{headers});
    }catch{
      // Never fall back to the production feed.
      return Response.json({error:'Test update channel unavailable'},{status:503,headers});
    }
  };
}
function installRemoteStatusHandler(session,gameRoot){
  const gameSession=session.fromPartition('persist:chip-arena-game');
  gameSession.protocol.handle('https',createRemoteStatusHandler(gameRoot,(request,options)=>gameSession.fetch(request,options)));
}
module.exports={createRemoteStatusHandler,installRemoteStatusHandler};
