"""Validate exported scene topology and source-specific lifetimes, without claiming rendered acceptance."""
import base64, gzip, json, math, unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def resource(name):
    envelope=json.loads((ROOT/f'Assets/Conversion/Resources/{name}.json').read_text())
    assert envelope['schema']==1 and envelope['commit']=='05950eaef80ff2483a9c44d898cad98be1a76bbc'
    return json.loads(gzip.decompress(base64.b64decode(envelope['data'])))
class CinematicChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.clips={c['key']:c for c in resource('SceneMotion')['clips']}
        models=resource('SourceModels')
        cls.models={m['id']:m for m in models['models']}
        cls.textures=models['textures']
    def test_procedural_textures_contain_one_complete_rgba_base_level(self):
        # The Windows failure came from treating these base-only exports as a full mip chain.
        self.assertEqual(len(self.textures),3)
        for i,texture in enumerate(self.textures):
            with self.subTest(texture=i):
                self.assertGreater(texture['width'],0)
                self.assertGreater(texture['height'],0)
                self.assertEqual(texture['channels'],4)
                pixels=base64.b64decode(texture['pixels'],validate=True)
                self.assertEqual(len(pixels),texture['width']*texture['height']*4)
                self.assertEqual(len(texture['repeat']),2)
                self.assertTrue(all(math.isfinite(v) and v>0 for v in texture['repeat']))
        for model in self.models.values():
            for section in model['sections']:
                self.assertTrue(-1<=section['texture']<len(self.textures),model['id'])
    def test_complete_geometry_and_tracks(self):
        self.assertEqual(len(self.clips),31)
        # 133 source models plus the six Ward Zero beacon beams exported as moving parts and the horde-tongue pieces
        # (the Siren minions' black-flame tongues, animated by Core/NativeMinionFx.cs).
        self.assertEqual(len(self.models),140)
        for key,c in self.clips.items():
            self.assertIn(key,self.models)
            self.assertEqual(len(c['values']),c['frames']*c['tracks']*20,key)
            self.assertTrue(all(math.isfinite(v) for v in c['values']),key)
            self.assertEqual(len(c['environment']),c['frames']*8,key)
            for l in c['lights']:
                self.assertEqual(len(l['values']),c['frames']*11,key)
                self.assertTrue(all(math.isfinite(v) for v in l['values']),key)
            for s in self.models[key]['sections']:
                self.assertTrue(0<=s['bone']<c['tracks'],key)
                self.assertTrue(any(abs(v)>.00001 for v in s['positions']),key+' collapsed mesh')
                self.assertTrue(all(math.isfinite(v) for v in s['positions']+s['normals']),key)
            self.assertIn(len(c['victim']),(0,22),key)
    def test_ward_zero_bakes_a_rendered_frame(self):
        # The hall is baked from its first source update: the gallery crowd stands in place (not stacked at the origin),
        # zero-scale instances are dropped, and glows carry their intensity instead of full brightness.
        sections=self.models['map-siren']['sections'];positions=[s['positions'] for s in sections]
        origin=sum(1 for p in positions for i in range(0,len(p),3) if abs(p[i])<1 and abs(p[i+2])<1 and 0<=p[i+1]<=2.5)
        self.assertLess(origin,8)
        self.assertFalse(any(max(s['emission'][i:i+3])>.9 for s in sections for i in range(0,len(s['emission']),4)))
        ward=json.loads((ROOT/'Assets/Conversion/Resources/WardMotion.json').read_text())['parts']
        beams=[p for p in ward if p['kind']=='beam']
        self.assertEqual(len(beams),6)
        for p in beams:self.assertTrue(self.models[p['id']]['sections'][0]['transparent'])
    def test_stage_hazard_parts(self):
        hazards={m['id']:m for m in resource('HazardModels')['models']}
        self.assertEqual(set(hazards),{'hazard-'+n for n in ('saw','saw-track','spikes','spikes-cones','spikes-surface','lava','lava-surface','lava-currents','lava-bubble-0','lava-bubble-1')})
        for m in hazards.values():
            self.assertTrue(m['sections']and all(s['texture']==-1 for s in m['sections']),m['id'])
        # Animated glows are exported as unit white; presentation supplies the source colour and intensity.
        for id in ('hazard-spikes-surface','hazard-lava-surface'):
            e=hazards[id]['sections'][0]['emission'];self.assertTrue(all(abs(e[i]-1)<1e-6 for i in range(0,len(e),4)),id)
    def test_clown_drop_and_victim(self):
        c=self.clips['death-dev-clown']
        def opacity(track,t):return c['values'][(track*c['frames']+round(t/c['duration']*(c['frames']-1)))*20+16]
        self.assertGreaterEqual(c['dropped'],0)
        self.assertEqual(opacity(c['dropped'],.1),0)
        self.assertGreater(opacity(c['dropped'],.5),0)
        self.assertEqual(len(c['victim']),22)
        for spec in ('pistol','rifle','greatsword','fire','leet'):
            self.assertIn('loose-'+spec,self.models)
    def test_siren_blackout_and_real_stage(self):
        c=self.clips['stage-dev-siren']
        light=lambda f:sum(l['values'][f*11+6] for l in c['lights'])
        self.assertGreater(max(light(f) for f in range(c['frames'])),10)
        self.assertAlmostEqual(light(c['frames']-1),0,places=4)
        self.assertGreater(len(self.models['stage-dev-siren']['sections']),20)
    def test_siren_entrance_face_rides_the_head_track(self):
        # The dev-siren face (Core/NativeSirenSmileFace.cs: the stare and the acted smile) is drawn natively on the recorded
        # head track; the bake leaves out the source's eyes (one held brow cut) and its x-stretched grin rows.
        c=self.clips['stage-dev-siren']
        self.assertTrue(1<=c['head']<c['tracks'])
        self.assertTrue(all('head' not in clip for key,clip in self.clips.items() if key!='stage-dev-siren'))
        def glow(s):return sum(abs(s['colors'][k]-s['emission'][k]) for k in range(3))<1e-4
        sections=self.models['stage-dev-siren']['sections']
        self.assertFalse([s['bone'] for s in sections if glow(s) and len(s['positions'])==120*3 and s['colors'][0]>.99])
        self.assertFalse([s['bone'] for s in sections if glow(s) and abs(s['colors'][0]-.896)<.01 and abs(s['colors'][1]-.871)<.01])
        intros=(ROOT/'Assets/ChipArena/Runtime/NativeIntros.cs').read_text(encoding='utf-8')
        self.assertIn('if(style=="dev-siren")SirenEntranceFace(clip,age,camera);',intros)
    def test_every_staged_entrance_has_its_scene_and_motion(self):
        # NativeIntroRules.Entrances is what RenderIntro stages (anything else stages the pilot's weapon signature). The DEV
        # entrances play their baked stages; the weapon signatures re-stage their baked clips through the entrance director;
        # the procedural entrances and the Bow's Arrowfall (keyed natively, Core/NativeBowEntrance.cs) are the director's own
        # and need no bake.
        import re
        core=(ROOT/'Assets/ChipArena/Core/NativeIntroRules.cs').read_text(encoding='utf-8')
        styles=re.findall(r'"([a-z-]+)"',re.search(r'Entrances=\{([^}]*)\}',core).group(1))
        procedural=re.findall(r'"([a-z-]+)"',re.search(r'Procedural=\{([^}]*)\}',core).group(1))
        retired=re.findall(r'"([a-z-]+)"',re.search(r'Retired=\{([^}]*)\}',core).group(1))
        self.assertEqual(len(styles),23)
        native=set(procedural)|{'weapon-bow'}
        self.assertEqual(set(retired),{'neon-drop','vector-slide','signal-spin','arc-salute','pulse-wave'})
        self.assertFalse(set(retired)&set(styles))
        self.assertTrue(set(procedural)<=set(styles))
        motion={c['key'] for c in resource('SourceMotion')['clips']}
        director=(ROOT/'Assets/ChipArena/Core/NativeEntranceDirector.cs').read_text(encoding='utf-8')
        for style in styles:
            with self.subTest(style=style):
                if style=='weapon-bow':
                    self.assertIn('{NativeBowEntrance.Id,()=>new NativeBowEntrance()}',director)
                elif style in procedural or style.startswith('weapon-'):
                    self.assertIn('{"'+style+'",',director)
                if style in native:continue
                self.assertIn('intro-'+style,motion)
                if style.startswith('dev-'):
                    self.assertIn('stage-'+style,self.clips)
                    self.assertIn('stage-'+style,self.models)
    def test_siren_cell_is_lit_by_its_bulb_through_the_entrance_light_conversion(self):
        # The cell has no directional light and almost no ambient: only the bulb and fill show it, so their conversion
        # into Unity lights (NativeEntranceLight, colour space and intensity mode) decides whether the room is visible.
        c=self.clips['stage-dev-siren']
        lit=[f for f in range(c['frames']) if any(l['kind']=='point' and l['values'][f*11+6]>20 for l in c['lights'])]
        self.assertGreater(len(lit),60)
        for f in lit:
            self.assertFalse(any(l['kind']=='directional' and l['values'][f*11+6]>0 for l in c['lights']),f)
            self.assertLess(sum(l['values'][f*11+6]*max(l['values'][f*11+3:f*11+6]) for l in c['lights'] if l['kind'] in ('ambient','hemisphere')),.05,f)
        lighting=(ROOT/'Assets/ChipArena/Runtime/NativeLighting.cs').read_text(encoding='utf-8')
        intro=lighting[lighting.index('public static void Intro('):lighting.index('public static void Clock(')]
        self.assertIn('NativeEntranceLight.Light(',intro)
        self.assertIn('NativeEntranceLight.Ambient(',intro)
        self.assertNotIn('light.intensity=intensity',intro)
        self.assertIn('env[e+2]).gamma',intro)
    def test_intro_rig_holds_only_a_weapon_intros_weapon(self):
        # player-intros.ts builds a weapon only for a weapon intro; the L33T Throne's staff is its own stage prop.
        intros=(ROOT/'Assets/ChipArena/Runtime/NativeIntros.cs').read_text(encoding='utf-8')
        self.assertIn('SourceCharacter(introRig,actor,arena.Time,weapon:false,halo:age)',intros)  # the Auditor halo on the entrance age
        self.assertIn('NativeIntroRules.IntroWeapon(style)',intros)
        self.assertIn('stage-dev-matrix',self.clips)
        # The intro clips are baked from the rig player-intros.ts arms (braceWeapon's support grip needs the weapon).
        export=(ROOT/'tools/export_motion.mjs').read_text(encoding='utf-8')
        self.assertIn('armIntro(rig,style);',export)
        self.assertIn('checkGrip(rig,style,age);',export)
    def test_intro_glow_fade_only_touches_blast_glow_voxels(self):
        # NativeIntros.IntroStage fades transparent stage tracks whose tint changes by alpha (additive ChipBlasts glow voxels);
        # on every entrance stage those are emissive voxel boxes of the weapon intros, nothing else.
        touched=0
        for key,c in self.clips.items():
            if not key.startswith('stage-'):continue
            frames,values=c['frames'],c['values']
            for s in self.models[key]['sections']:
                if not s['transparent'] or not s['positions']:continue
                o=s['bone']*frames*20
                if all(values[o+f*20+17:o+f*20+20]==values[o+17:o+20] for f in range(frames)):continue
                with self.subTest(stage=key,bone=s['bone']):
                    self.assertTrue(key.startswith('stage-weapon-'))
                    self.assertEqual(len(s['positions']),24*3)
                    self.assertTrue(any(v>0 for i,v in enumerate(s['emission']) if i%4!=3))
                touched+=1
        self.assertGreater(touched,500)
    def test_demon_and_flames_are_animated(self):
        for key in ('death-dev-raging-demon','death-siren-crush','death-siren-dissipate','stage-dev-ansatsuken'):
            c=self.clips[key]
            self.assertGreater(c['tracks'],100,key)
            self.assertNotEqual(c['values'][:c['frames']*20],c['values'][-c['frames']*20:],key)
    def test_wounds_bind_to_exported_pilot_parts(self):
        data=json.loads((ROOT/'Assets/Conversion/Resources/WoundParts.json').read_text())
        self.assertEqual(len(data['parts']),31)
        names={s['part'] for s in self.models['pilot']['sections']}
        for p in data['parts']:
            self.assertIn(p['name'],names)
            self.assertTrue(0<=p['bone']<22)
            self.assertEqual(len(p['matrix']),16)
            self.assertGreater(len(p['solids']),0)
            for solid in p['solids']:
                for axis in 'xyz':self.assertLess(solid['min'][axis],solid['max'][axis])
    def test_scenery_motion_and_machine_geometry(self):
        data=json.loads((ROOT/'Assets/Conversion/Resources/SceneryMotion.json').read_text())
        self.assertEqual(len(data['maps']),14)
        for m in data['maps']:
            self.assertIn('map-'+m['id'],self.models)
            self.assertIn(len(m['snow']),(0,480))
            self.assertEqual(len(m['smoke'])%4,0)
            self.assertTrue(all(math.isfinite(v) for v in m['smoke']))
            for seed in m['snow']:self.assertGreater(seed['speed'],0)
        for name in ('scenery-snow','scenery-water','summon-toy','summon-zombie','pve-pickup','machine-moving-chip','machine-moving-med','machine-moving-recruit'):
            self.assertTrue(self.models[name]['sections'])
    def test_slender_hierarchy_and_source_image(self):
        import hashlib
        data=json.loads((ROOT/'Assets/Conversion/Resources/SlenderRig.json').read_text())
        nodes=data['nodes'];self.assertEqual(len(nodes),49)
        for i,n in enumerate(nodes):
            self.assertTrue(-1<=n['parent']<i)
            self.assertEqual(len(n['rotation']),4)
            self.assertAlmostEqual(sum(v*v for v in n['rotation']),1,places=5)
        for s in self.models['slender']['sections']:self.assertTrue(0<=s['bone']<len(nodes))
        b=(ROOT/'Assets/Conversion/Resources/SlenderScare.jpg').read_bytes()
        self.assertEqual(hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest(),'39813e99bae17cec879ecff572a0a06980361c44')
if __name__=='__main__':unittest.main()
