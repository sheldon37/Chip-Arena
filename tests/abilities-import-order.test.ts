// Import-order guard (design §3.1): abilities.ts is the FIRST import of this process, so a runtime cycle between
// abilities.ts, content.ts, the kit leaves and pong.ts would surface here as a TDZ ReferenceError at load time.
import {kitFor,abilityUser,ABILITY_DEFS,HOST_CAPS,abilityActive} from '../src/shared/abilities.js';
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {WEAPONS,CHIPS,eligibleChips} from '../src/shared/content.js';
import {GUN_WEAPON} from '../src/shared/gun-kit.js';
import {MAGIC_WEAPON} from '../src/shared/magic-kit.js';
import {PONG_CHIP} from '../src/shared/pong.js';

test('abilities.ts loads first without a cycle; kits, moved WEAPONS rows and the Pong row resolve',()=>{
 assert.equal(abilityUser('pistol'),true);assert.equal(abilityUser('leet'),false);assert.equal(kitFor('fire')!.e.id,'meteor-strike');assert.ok(ABILITY_DEFS['frag-grenade']);
 assert.deepEqual(HOST_CAPS,{abilities:1});assert.equal(abilityActive('rifle','q'),true);
 for(const spec of ['pistol','rifle','machinegun'] as const)assert.equal(WEAPONS[spec],GUN_WEAPON[spec]);
 for(const spec of ['fire','water','lightning','darkness'] as const)assert.equal(WEAPONS[spec],MAGIC_WEAPON[spec]);
 assert.ok(CHIPS.includes(PONG_CHIP));assert.ok(eligibleChips('pistol',{}).some(c=>c.id==='pong'));
});
