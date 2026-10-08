// Optional public-endpoint test adapter for environments whose Node fetch cannot
// use the provided HTTP proxy. Tokens and payloads go to curl stdin, never argv.
import {spawn} from 'node:child_process';
export const curlFetch:typeof fetch=async(url,init={})=>new Promise((resolve,reject)=>{
 const args=['--silent','--show-error','--max-time','10','--config','-','--write-out','\n%{http_code}'];
 const child=spawn('curl',args,{stdio:['pipe','pipe','pipe']});let out='',err='';const abort=()=>child.kill();init.signal?.addEventListener('abort',abort,{once:true});
 child.stdout.on('data',b=>out+=b);child.stderr.on('data',b=>err+=b);child.on('error',reject);child.on('close',code=>{init.signal?.removeEventListener('abort',abort);if(code!==0){reject(Error(init.signal?.aborted?'Public request timeout':err||'curl request failed'));return;}const index=out.lastIndexOf('\n'),status=Number(out.slice(index+1));if(status<100){reject(Error('Invalid public HTTP response'));return;}resolve(new Response(out.slice(0,index),{status,headers:{'Content-Type':'application/json'}}));});
 const headers=new Headers(init.headers);const lines=['url = '+JSON.stringify(String(url)),'request = '+JSON.stringify(init.method||'GET')];headers.forEach((v,k)=>lines.push('header = '+JSON.stringify(k+': '+v)));if(init.body)lines.push('data = '+JSON.stringify(String(init.body)));child.stdin.end(lines.join('\n')+'\n');
});
