const status=document.querySelector('#status');
function message(text){status.textContent=text;}
window.arena.onStatus(message);
async function run(action){const buttons=[...document.querySelectorAll('#local,#online,#join button,#retry')];buttons.forEach(b=>b.disabled=true);try{await action();}catch(e){message(e.message.replace(/^Error invoking remote method '[^']+': Error: /,''));}finally{buttons.forEach(b=>b.disabled=false);}}
document.querySelector('#local').onclick=()=>run(()=>window.arena.launch('local'));
document.querySelector('#online').onclick=()=>run(()=>window.arena.launch('online'));
document.querySelector('#join').onsubmit=e=>{e.preventDefault();run(()=>window.arena.join(document.querySelector('#invite').value));};
document.querySelector('#copy').onclick=async()=>{const url=await window.arena.copy();message(url?'Server link copied: '+url:'Host online first to create a public link.');};
document.querySelector('#stop').onclick=()=>run(()=>window.arena.stop());

document.querySelector('#retry').onclick=()=>run(()=>window.arena.retry());
