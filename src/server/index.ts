import {selectionSeconds} from '../shared/draft-settings.js';
import {DirectoryLease} from './directory-lease.js';
import {onlineRoomCode,formatOnlineCode} from '../shared/room-directory.js';
import {monitorEventLoopDelay} from 'node:perf_hooks';
import {SocketOutput,SOCKET_COMPRESSION} from './socket-output.js';
import {publicInviteOrigin} from '../shared/invite-url.js';
import {verifyPlayerToken} from '../shared/account-auth.js';
import {authProxy} from './auth-proxy.js';
import {newRoomCode,normalizeRoomCode} from '../shared/relay-protocol.js';
import http from 'node:http';
import {LocalRoomDatabase,serveRelay} from '../relay/local.js';
const relayDb=new LocalRoomDatabase();
import {readFile} from 'node:fs/promises';
import {extname} from 'node:path';
import {staticPath} from './static-path.js';
import {randomBytes} from 'node:crypto';
import {WebSocketServer,WebSocket} from 'ws';
import {Simulation,initPhysics,sanitizeInput} from '../shared/simulation.js';
import {CONFIG,ClassId} from '../shared/config.js';
import {CLASS_INFO} from '../shared/content.js';
await initPhysics();
const port=Number(process.env.PORT)||8788;
const origins=(process.env.ALLOWED_ORIGINS||'').split(',').filter(Boolean);
interface Room{code:string;directory:DirectoryLease;directoryReady?:boolean;sim:Simulation;clients:Map<string,WebSocket>;owner:string;lastUsed:number;rematch:Set<string>;}
const rooms=new Map<string,Room>();
const loop=monitorEventLoopDelay({resolution:10});loop.enable();let loopP95Ms=0;setInterval(()=>{loopP95Ms=Math.round(loop.percentile(95)/1e6);loop.reset();},2000).unref();
let inviteOrigin:string|null=null;
process.on('message',(message:any)=>{if(message?.type!=='public-invite')return;const origin=publicInviteOrigin(message.url);if(!origin)return;inviteOrigin=origin;for(const r of rooms.values())if(r.clients.size)void refreshDirectory(r);if(process.connected)process.send?.({type:'public-invite-ready',url:origin});});
const server=http.createServer(async(req,res)=>{if(req.url==='/api/auth/login'){await authProxy(req,res);return;}if(req.url==='/api/network'){res.writeHead(200,{'Content-Type':'application/json','Cache-Control':'no-store'});res.end(JSON.stringify({transport:'websocket',endpoint:'/socket',inviteOrigin}));return;}if(req.url?.startsWith('/api/rooms/')){await serveRelay(req,res,relayDb);return;}if(req.url==='/health'){res.writeHead(200,{'Content-Type':'application/json'});res.end(JSON.stringify({ok:true,application:'chip-arena',rooms:rooms.size,simulationHz:CONFIG.simulationHz,snapshotHz:CONFIG.snapshotHz,loopP95Ms}));return;}
 let pathname:string;try{pathname=decodeURIComponent((req.url||'/').split('?')[0]);}catch{res.writeHead(400);res.end('Invalid path');return;}const path=staticPath('dist/client',pathname);if(!path){res.writeHead(403);res.end();return;}try{const bytes=await readFile(path);res.writeHead(200,{'Content-Type':({'.html':'text/html','.js':'text/javascript','.css':'text/css','.svg':'image/svg+xml','.wasm':'application/wasm','.mp3':'audio/mpeg','.wav':'audio/wav'} as Record<string,string>)[extname(path)]||'application/octet-stream','Cache-Control':'no-cache'});res.end(bytes);}catch{res.writeHead(404);res.end('Build the client with npm run build, or use npm run dev.');}});
const wss=new WebSocketServer({server,path:'/socket',maxPayload:24000,perMessageDeflate:SOCKET_COMPRESSION});
const output=new SocketOutput();
const send=(ws:WebSocket,value:unknown)=>output.send(ws,JSON.stringify(value));
const broadcast=(r:Room,value:{type:string;[key:string]:unknown})=>{const packet=JSON.stringify(value);for(const ws of r.clients.values())output.send(ws,packet,value.type==='snapshot');};
async function refreshDirectory(r:Room){await r.directory.refresh(inviteOrigin);if(r.directory.ready!==r.directoryReady){r.directoryReady=r.directory.ready;broadcast(r,{type:'invite',origin:inviteOrigin,onlineCode:r.directory.ready?formatOnlineCode(r.code):null});}}
setInterval(()=>{for(const r of rooms.values())if(r.clients.size)void refreshDirectory(r);},5000).unref();
const maxConnections=100;let connections=0;
wss.on('connection',(ws,req)=>{if(++connections>maxConnections){ws.close(1013,'Server full');connections--;return;}if(origins.length&&(!req.headers.origin||!origins.includes(req.headers.origin))){ws.close(1008,'Origin not allowed');connections--;return;}
 const id=randomBytes(8).toString('hex');let room:Room|undefined;let messages=0,windowStart=Date.now();let alive=true,joining=false;
 ws.on('pong',()=>alive=true);const heartbeat=setInterval(()=>{if(!alive){ws.terminate();return;}alive=false;ws.ping();},15000);
 ws.on('message',async buffer=>{if(Date.now()-windowStart>1000){messages=0;windowStart=Date.now();}if(++messages>CONFIG.maxInputRate+20){ws.close(1008,'Message rate exceeded');return;}let msg:any;try{msg=JSON.parse(buffer.toString());}catch{return;}if(!msg||typeof msg.type!=='string')return;
 const fail=(message:string)=>send(ws,{type:'error',message});
 if(msg.type==='ping'){send(ws,{type:'pong',at:typeof msg.at==='number'&&Number.isFinite(msg.at)?msg.at:0,network:{loopP95Ms,tickMs:room?.sim.tickMs||0,...output.diagnostics(ws)}});return;}
 if(msg.type==='create'||msg.type==='join'){if(joining)return;joining=true;const developer=await verifyPlayerToken(msg.authToken);joining=false;if(ws.readyState!==WebSocket.OPEN)return;if(room)return fail('Already in a room. Leave first.');if(msg.type==='create'){if(rooms.size>=CONFIG.maxRooms)return fail('Server room limit reached.');let code:string;do{code=newRoomCode();}while(rooms.has(code));const sandbox=process.env.ALLOW_TEST_ROOMS==='true'&&msg.testRoom===true;room={code,directory:new DirectoryLease(code),sim:new Simulation({map:msg.map,selectionSeconds:selectionSeconds(msg.selectionSeconds),skipIntros:msg.skipIntros===true,bothDraft:msg.bothDraft===true,draftChoices:msg.draftChoices,sandbox}),clients:new Map(),owner:id,lastUsed:Date.now(),rematch:new Set()};rooms.set(code,room);}else{const joinCode=typeof msg.code==='string'?(onlineRoomCode(msg.code)||normalizeRoomCode(msg.code)||(/^[A-F0-9]{6}$/i.test(msg.code)?msg.code.toUpperCase():null)):null;if(!joinCode)return fail('Enter the room code from your host.');room=rooms.get(joinCode);if(!room)return fail('Room not found.');if(room.sim.players.size>=CONFIG.maxPlayers){room=undefined;return fail('Room is full.');}if(room.sim.phase!=='lobby'){room=undefined;return fail('This match has already started.');}}
 const cls:ClassId=Object.hasOwn(CLASS_INFO,msg.class)?msg.class:'ranged';room.sim.addPlayer(id,typeof msg.name==='string'?msg.name:'Pilot',cls,null,typeof msg.color==='string'?msg.color:'#ff6b35',msg.cosmetics,developer);room.clients.set(id,ws);if(msg.type==='create')room.sim.configureLobby({gameMode:msg.gameMode,friendlyFire:msg.friendlyFire,wins:msg.wins,roundsPerSet:msg.roundsPerSet,randomMapEachSet:msg.randomMapEachSet,map:msg.map,botCount:msg.botCount,botDifficulty:msg.botDifficulty});send(ws,{type:'welcome',id,code:room.code,testRoom:room.sim.options.sandbox,owner:room.owner,inviteOrigin,onlineCode:room.directory.ready?formatOnlineCode(room.code):null});void refreshDirectory(room);broadcast(room,{type:'snapshot',state:room.sim.snapshot()});return;}
 if(!room)return fail('Create or join a room first.');room.lastUsed=Date.now();
 if(msg.type==='inputs')room.sim.queueInputs(id,msg.inputs,msg.epoch);
 else if(msg.type==='input'){const input=sanitizeInput(msg.input);if(input)room.sim.setInput(id,input);}
 else if(msg.type==='team'){const target=typeof msg.target==='string'?msg.target:id;if(target===id||room.owner===id)room.sim.setTeam(target,msg.team);}
 else if(msg.type==='class')room.sim.setClass(id,msg.class);
 else if(msg.type==='ready')room.sim.setReady(id,msg.ready===true);
 else if(msg.type==='spec')room.sim.selectSpec(id,msg.id);
 else if(msg.type==='chip')room.sim.selectChip(id,msg.id);
 else if(msg.type==='settings'&&room.owner===id){if(room.sim.configureLobby(msg))broadcast(room,{type:'snapshot',state:room.sim.snapshot()});}
 else if(msg.type==='rematch'&&!room.sim.lobbyReturn&&room.sim.phase==='match'){room.rematch.add(id);broadcast(room,{type:'rematchVotes',count:room.rematch.size});if([...room.sim.players.values()].filter(p=>p.connected&&!p.bot).every(p=>room!.rematch.has(p.id))){room.rematch.clear();room.sim.rematch();}}
 else if((msg.type==='lobby'||msg.type==='cancel-lobby')&&room.owner===id){const changed=msg.type==='lobby'?room.sim.requestLobbyReturn():room.sim.cancelLobbyReturn();if(changed){room.rematch.clear();broadcast(room,{type:'snapshot',state:room.sim.snapshot()});}}
 else if(msg.type==='debug'){if(!room.sim.options.sandbox||room.owner!==id)return fail('Debug commands are disabled for this player and room.');room.sim.debug(msg.command,id);}
 else if(msg.type==='leave')ws.close(1000,'Left room');
 });
 ws.on('close',()=>{connections--;clearInterval(heartbeat);if(room){room.clients.delete(id);room.sim.disconnect(id);room.rematch.delete(id);if(room.owner===id){room.owner=room.clients.keys().next().value||'';broadcast(room,{type:'owner',id:room.owner});}broadcast(room,{type:'notice',message:`Player disconnected. ${CONFIG.disconnectSeconds} seconds to forfeit.`});}});
 ws.on('error',()=>{});
});
let last=performance.now(),acc=0,snap=0;setInterval(()=>{const now=performance.now();acc+=Math.min((now-last)/1000,.1);last=now;let steps=0;while(acc>=1/CONFIG.simulationHz&&steps<6){for(const r of rooms.values())r.sim.step();acc-=1/CONFIG.simulationHz;steps++;snap++;if(snap>=CONFIG.simulationHz/CONFIG.snapshotHz){snap=0;for(const r of rooms.values())broadcast(r,{type:'snapshot',state:r.sim.snapshot()});}}for(const[code,r]of rooms)if(!r.clients.size&&Date.now()-r.lastUsed>30000){r.sim.dispose();rooms.delete(code);}},4);
server.listen(port,'0.0.0.0',()=>console.log(`Chip Arena authoritative server: http://localhost:${port} | WS /socket | ${CONFIG.simulationHz} Hz sim, ${CONFIG.snapshotHz} Hz snapshots`));
