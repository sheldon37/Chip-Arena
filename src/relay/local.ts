/** Local equivalent of D1 for preview/tests and standalone Node deployments. */
import {DatabaseSync} from 'node:sqlite';
import {readFileSync,readdirSync} from 'node:fs';
import {IncomingMessage,ServerResponse,createServer} from 'node:http';
import worker,{Database} from './worker.js';
export class LocalRoomDatabase implements Database {
 sql=new DatabaseSync(':memory:');
 constructor(){for(const f of readdirSync('drizzle').filter(f=>f.endsWith('.sql')).sort())this.sql.exec(readFileSync('drizzle/'+f,'utf8'));}
 prepare(sql:string){const db=this.sql;let values:any[]=[];return {bind(...v:any[]){values=v;return this;},async first<T=any>():Promise<T|null>{return (db.prepare(sql).get(...values) as T)||null;},async run(){const result=db.prepare(sql).run(...values);return {meta:{changes:Number(result.changes)}};},execute(){const statement=db.prepare(sql);if(/^\s*(SELECT|WITH)/i.test(sql))return {results:statement.all(...values)};const result=statement.run(...values);return {meta:{changes:Number(result.changes)},results:[]};}};}
 async batch(statements:any[]){this.sql.exec('BEGIN');try{const r=statements.map(s=>s.execute());this.sql.exec('COMMIT');return r;}catch(e){this.sql.exec('ROLLBACK');throw e;}}
 close(){this.sql.close();}
}
export async function serveRelay(req:IncomingMessage,res:ServerResponse,db:Database){try{const chunks:Buffer[]=[];let size=0;for await(const chunk of req){size+=chunk.length;if(size>384000){res.writeHead(413);res.end('{"error":"Room message is too large."}');return;}chunks.push(chunk);}const headers=new Headers();for(const[k,v]of Object.entries(req.headers))if(typeof v==='string')headers.set(k,v);const request=new Request('http://'+(req.headers.host||'localhost')+(req.url||'/'),{method:req.method,headers,...(req.method==='POST'?{body:Buffer.concat(chunks)}:{})});const response=await worker.fetch(request,{DB:db});const responseHeaders:Record<string,string>={};response.headers.forEach((v,k)=>responseHeaders[k]=v);res.writeHead(response.status,responseHeaders);res.end(Buffer.from(await response.arrayBuffer()));}catch(e){console.error(e);res.writeHead(500);res.end('{"error":"Local relay failed."}');}}
export function createLocalRelay(){const db=new LocalRoomDatabase();const server=createServer((req,res)=>void serveRelay(req,res,db));return {server,db};}
