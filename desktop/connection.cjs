const {setTimeout:delay}=require('node:timers/promises');

function retryable(error){
 if(error.retryable===false)return false;
 if(error.retryable===true)return true;
 return /ERR_(NAME_NOT_RESOLVED|NAME_RESOLUTION_FAILED|CONNECTION_REFUSED|CONNECTION_RESET|CONNECTION_CLOSED|CONNECTION_TIMED_OUT|TIMED_OUT|NETWORK_CHANGED|INTERNET_DISCONNECTED|ADDRESS_UNREACHABLE)|ENOTFOUND|EAI_AGAIN/.test(String(error.message||error.code));
}

/** Retrying a connection never owns or stops the server process. */
async function connectWithRetry(attempt,{signal,onRetry=()=>{},resetDNS=async()=>{},timeoutMs=120000,now=Date.now,sleep=(ms,signal)=>delay(ms,undefined,{signal})}={}){
 const started=now();let count=0;
 while(true){
  signal?.throwIfAborted();
  try{return await attempt(signal);}
  catch(error){
   signal?.throwIfAborted();
   if(!retryable(error))throw error;
   const remaining=timeoutMs-(now()-started);
   if(remaining<=0)throw Error('The server address is still unreachable. Use Retry connection to try this same link again. ('+error.message+')',{cause:error});
   onRetry(++count,Math.ceil(remaining/1000));
   await resetDNS().catch(()=>{});
   await sleep(Math.min(2500,remaining),signal);
  }
 }
}

async function openReachableWindow(win,url,{signal,onRetry,timeoutMs,attemptTimeoutMs=10000}={}){
 const session=win.webContents.session,isTunnel=new URL(url).hostname.endsWith('.trycloudflare.com');
 return connectWithRetry(async()=>{
  const controller=new AbortController();let timeout,rejectCancelled;
  const cancelled=new Promise((_,reject)=>{rejectCancelled=reject;});
  const abort=()=>{
   // Reject before aborting fetch/navigation so cancellation keeps its original reason.
   rejectCancelled(signal.reason||Error('Launch cancelled.'));
   controller.abort();
   if(!win.isDestroyed())win.webContents.stop();
  };
  signal?.addEventListener('abort',abort,{once:true});
  const timed=new Promise((_,reject)=>{timeout=setTimeout(()=>{
   // A fetch AbortError would otherwise hide the retryable timeout.
   reject(Error('ERR_TIMED_OUT waiting for the game server'));
   controller.abort();
   if(!win.isDestroyed())win.webContents.stop();
  },attemptTimeoutMs);});
  const operation=(async()=>{
   if(isTunnel){
    const response=await session.fetch(new URL('/health',url).toString(),{signal:controller.signal,cache:'no-store',redirect:'error'});
    if(!response.ok){const e=Error('Server is starting (HTTP '+response.status+').');e.retryable=response.status>=500||response.status===404||response.status===429;throw e;}
    const health=await response.json();
    if(health.application!=='chip-arena')throw Error('This invite does not point to a Chip Arena server.');
   }
   // A timed-out probe must never navigate after a later attempt has started.
   controller.signal.throwIfAborted();signal?.throwIfAborted();
   await win.loadURL(url);
  })();
  try{return await Promise.race([operation,timed,cancelled]);}
  finally{clearTimeout(timeout);signal?.removeEventListener('abort',abort);}
 },{signal,onRetry,timeoutMs,resetDNS:()=>session.clearHostResolverCache()});
}
module.exports={retryable,connectWithRetry,openReachableWindow};
