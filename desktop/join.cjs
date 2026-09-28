const {gameUrl}=require('./policy.cjs');
const DIRECTORY_ORIGIN='https://chip-arena-prototype.sheldon37.chatgpt.site';
function roomCode(raw){if(typeof raw!=='string')return null;let code=raw.trim().toUpperCase().replace(/[\s-]/g,'');if(code.length===10&&code.startsWith('CA'))code=code.slice(2);return /^[A-HJ-NP-Z2-9]{8}$/.test(code)?code:null;}
async function resolveJoin(raw,request=fetch){
 const code=roomCode(raw);if(!code)return gameUrl(raw);
 let response;try{response=await request(`${DIRECTORY_ORIGIN}/api/directory/${code}`,{cache:'no-store',redirect:'error',signal:AbortSignal.timeout(8000)});}catch{throw Error('Could not look up this room. Try again, or ask the host for an invite link.');}
 if(response.status===404)throw Error('Room code expired or was not found. Ask the host to keep their room open and copy its code.');
 if(!response.ok)throw Error('Room lookup is temporarily unavailable. Try again or use an invite link.');
 let data,url;try{data=await response.json();url=new URL(data.origin);}catch{throw Error('Invalid room lookup response.');}
 if(data.code!==code||url.protocol!=='https:'||url.username||url.password||url.port||!/^[-a-z0-9]+\.trycloudflare\.com$/.test(url.hostname)||url.pathname!=='/'||url.search||url.hash)throw Error('Invalid room lookup response.');
 url.searchParams.set('room',code);return gameUrl(url.toString());
}
module.exports={resolveJoin,roomCode};
