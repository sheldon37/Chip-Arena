import type {IncomingMessage,ServerResponse} from 'node:http';
import {AUTH_ORIGIN} from '../shared/dev-auth.js';
/** Portable servers delegate credentials to the game's HTTPS identity service; no secrets ship in the ZIP. */
export async function authProxy(req:IncomingMessage,res:ServerResponse){
 if(req.method!=='POST'){res.writeHead(405);res.end();return;}const origin=req.headers.origin;if(origin){let host='';try{host=new URL(origin).host;}catch{}if(host!==req.headers.host){res.writeHead(403);res.end();return;}}
 let size=0;const chunks:Buffer[]=[];for await(const chunk of req){size+=chunk.length;if(size>2048){res.writeHead(413);res.end();return;}chunks.push(chunk);}
 try{const r=await fetch(AUTH_ORIGIN+'/api/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:Buffer.concat(chunks),signal:AbortSignal.timeout(10000)});res.writeHead(r.status,{'Content-Type':'application/json','Cache-Control':'no-store'});res.end(await r.text());}catch{res.writeHead(503,{'Content-Type':'application/json'});res.end(JSON.stringify({error:'Login needs an internet connection. You can still play without an account.'}));}
}
