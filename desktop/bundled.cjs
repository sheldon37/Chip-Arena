const {resolve,sep}=require('node:path');
const GAME_HOME='arena://game/';
function pageOrigin(raw){try{const u=new URL(raw);if(u.username||u.password)return '';if(u.protocol==='arena:'&&u.hostname==='game'&&!u.port)return 'arena://game';return ['http:','https:'].includes(u.protocol)?u.origin:'';}catch{return '';}}
function assetPath(root,raw){try{const u=new URL(raw);if(pageOrigin(raw)!=='arena://game')return null;const name=decodeURIComponent(u.pathname==='/'?'/index.html':u.pathname);if(/[\\\0:]/.test(name)||name.split('/').some(x=>x==='..'||x==='.')||!/^\/(?:assets|audio|music|images)\/.+\.(?:js|css|wasm|png|jpg|webp|svg|mp3|wav)$|^\/index\.html$/.test(name))return null;const base=resolve(root,'dist/client'),path=resolve(base,'.'+name);return path.startsWith(base+sep)?path:null;}catch{return null;}}
module.exports={GAME_HOME,pageOrigin,assetPath};
