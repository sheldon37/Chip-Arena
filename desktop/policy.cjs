const net=require('node:net');
function gameUrl(raw){
 if(typeof raw!=='string'||raw.length>2048)throw Error('Paste a game invite URL.');
 let url;try{url=new URL(raw.trim());}catch{throw Error('Paste the complete http:// or https:// invite URL.');}
 if(url.username||url.password)throw Error('Invite URLs cannot contain credentials.');
 const h=url.hostname,local=h==='localhost'||h==='127.0.0.1'||h==='[::1]'||/^10\.\d+\.\d+\.\d+$/.test(h)||/^192\.168\.\d+\.\d+$/.test(h)||/^172\.(1[6-9]|2\d|3[01])\.\d+\.\d+$/.test(h);
 if(url.protocol!=='https:'&&!(url.protocol==='http:'&&local))throw Error('Use an HTTPS invite, or an HTTP address on your local network.');
 if(url.port&&(+url.port<1||+url.port>65535))throw Error('Invalid port.');
 url.hash='';return url.toString();
}
function tunnelUrl(text){return text.match(/https:\/\/[a-z0-9-]+\.trycloudflare\.com/)?.[0]||null;}
async function freePort(){return new Promise((resolve,reject)=>{const server=net.createServer();server.on('error',reject);server.listen(0,'127.0.0.1',()=>{const p=server.address().port;server.close(()=>resolve(p));});});}
module.exports={gameUrl,tunnelUrl,freePort};
