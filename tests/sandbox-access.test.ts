// The Developer Sandbox is for DEV accounts only (owner 2026-10-04). main.ts cannot be imported in Node, so these pin its
// source, as tests/pve-ui.test.ts does for the home menu.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';

const main=readFileSync(new URL('../src/client/main.ts',import.meta.url),'utf8');
test('the Developer Sandbox is gated on a DEV account (or the local dev server) at every entry point',()=>{
 assert.ok(main.includes('function sandboxAllowed(){return developer||!!(import.meta as any).env.DEV;}'),'one rule: a DEV account, or npm run dev');
 assert.ok(main.includes(`data-action="sandbox"\${sandboxAllowed()?'':' hidden'}>`),'the home link is hidden for everyone else');
 assert.ok(main.includes("case 'sandbox':if(!sandboxAllowed())return;"),'a forced click does nothing');
 assert.ok(main.includes('function setupLocal(sandbox=false){sandbox=sandbox&&sandboxAllowed();'),'no sandbox session without access (menu, WebMCP, window.chipArena)');
 assert.ok(main.includes("if(x.sandbox===true&&!sandboxAllowed())throw Error('The Developer Sandbox needs a DEV account.');"),'the WebMCP tool says why');
 assert.ok(main.includes("else if(type==='debug'){if(sandboxAllowed())sim.debug(data.command,id);}"),'local debug commands need access');
 assert.ok(main.includes("if(!state.options.sandbox||screen!=='game'||!sandboxAllowed()){el.innerHTML='';return;}"),'the panel is not drawn without access');
 assert.ok(main.includes("e.code==='F3'&&(state.options.sandbox&&sandboxAllowed()||(import.meta as any).env.DEV)"),'F3 debug overlay');
 assert.ok(main.includes('const allowed=sandboxAllowed();')&&main.includes('if(state?.options.sandbox&&sandboxAllowed()!==allowed)drawSandbox();}'),'losing DEV mid-session clears the panel; a routine account refresh leaves it alone');
});
