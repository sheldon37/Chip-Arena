#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // A dead pilot's model corpse starts from the drawn pose, drops the weapon it held and keeps its gear (owner 2026-11-14: "Yes, fix
    // them"; Core NativeCorpseDress, Runtime NativeCorpseDressPresentation.cs): the drawn-pose gate (every death seen as it happens, none
    // first seen late), every class's held weapon decided (drops, part of the body, put away, ammunition, none), the hand it leaves, its
    // box, the gear's failing light and dead device, each gear piece worn by exactly one half of a cut at every stage, and the Runtime
    // hooks (the weapon read before the rig moves, the corpse's carried things after its model is made, drawn each frame, gone with it).
    internal static class CorpseDressChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Corpse dress: "+what);count++;}
        public static void Run(string root)
        {
            count=0;
            // ---- the pose: a death seen as it happens, from a body drawn this frame or the one before ----
            foreach(float age in new[]{0f,.001f,.1f,.249f})Check(NativeCorpseDress.FromDrawn(age),"a fresh death ("+age+" s) starts from the drawn pose");
            foreach(float age in new[]{-.01f,.25f,1f,30f,float.NaN})Check(!NativeCorpseDress.FromDrawn(age),"a death first seen late ("+age+" s) starts from the rig's pose");
            for(int f=10;f<14;f++)
            {
                Check(NativeCorpseDress.Drawn(f,f)&&NativeCorpseDress.Drawn(f-1,f)&&NativeCorpseDress.Drawn(f-2,f),"a body drawn this frame or just before shows the death's moment");
                Check(!NativeCorpseDress.Drawn(f-3,f)&&!NativeCorpseDress.Drawn(f+1,f)&&!NativeCorpseDress.Drawn(-10,f),"a body not drawn of late (first person, the voxel body) does not");
            }
            // ---- the weapon: every class decided ----
            using var content=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")));
            var specs=content.RootElement.GetProperty("weapons").EnumerateArray().Select(w=>w.GetProperty("id").GetString()).ToArray();
            string[] drops={"pistol","rifle","machinegun","fire","water","lightning","darkness","leet","sword","daggers","shield","greatsword","bow","glaive","katana","dev-greatsword","dev-katana"};
            string[] body={"emperor","ansatsuken","cyberlord"},away={"topdog","fangs"},ammo={"bombadier"},none={"siren"};
            Check(specs.Length>=24,"every class read: "+specs.Length);
            foreach(var spec in specs)
            {
                int lists=(drops.Contains(spec)?1:0)+(body.Contains(spec)?1:0)+(away.Contains(spec)?1:0)+(ammo.Contains(spec)?1:0)+(none.Contains(spec)?1:0);
                Check(lists==1,spec+": a new class needs its corpse's weapon decided here");
                var want=drops.Contains(spec)?NativeCorpseDress.Held.Drops:body.Contains(spec)?NativeCorpseDress.Held.Body:away.Contains(spec)?NativeCorpseDress.Held.PutAway:ammo.Contains(spec)?NativeCorpseDress.Held.Ammo:NativeCorpseDress.Held.None;
                Check(NativeCorpseDress.Of(spec)==want&&NativeCorpseDress.Drops(spec)==(want==NativeCorpseDress.Held.Drops),spec+": "+NativeCorpseDress.Of(spec)+" (want "+want+")");
                // The hand it leaves is the hand that holds it.
                bool left=spec=="fire"||spec=="water"||spec=="lightning"||spec=="darkness"||spec=="leet"||spec=="bow";
                Check(NativeCorpseDress.Hand(spec)==(left?CapturedRig.LHand:CapturedRig.RHand)&&NativeCorpseDress.Hand(spec)==NativeLobbyPoses.HeldBone(spec),spec+": leaves the hand that holds it");
                Check(NativeCorpseDress.OffHand(spec)==(spec=="daggers"),spec+": only the daggers keep a second blade in the other hand");
            }
            Check(NativeCorpseDress.Of(null)==NativeCorpseDress.Held.None&&NativeCorpseDress.Of("")==NativeCorpseDress.Held.None&&NativeCorpseDress.Hand(null)==CapturedRig.RHand,"no class, no weapon");
            Check(NativeCorpseDress.SeatEase>0&&NativeCorpseDress.SeatEase<=2,"the dropped weapon eases out of what it started in, never pops");
            Check(NativeCorpseDress.Box(.001f)==NativeCorpseDress.BoxLeast&&NativeCorpseDress.Box(9)==NativeCorpseDress.BoxMost&&NativeCorpseDress.Box(.5f)==.5f&&NativeCorpseDress.Box(float.NaN)==NativeCorpseDress.BoxLeast,"the weapon's box: never thinner than a floor can hold, never past a greatsword");
            // ---- the gear's light: held, failing in stutters, then faint ----
            float end=NativeCorpseDress.GlowHold+NativeCorpseDress.GlowOut;int dips=0;
            for(int seed=1;seed<40;seed++)
            {
                Check(NativeCorpseDress.DeadGlow(0,seed)==1&&NativeCorpseDress.DeadGlow(NativeCorpseDress.GlowHold*.99f,seed)==1&&NativeCorpseDress.DeadGlow(float.NaN,seed)==1,"the light holds a moment");
                Check(NativeCorpseDress.DeadGlow(end,seed)==NativeCorpseDress.GlowLeft&&NativeCorpseDress.DeadGlow(60,seed)==NativeCorpseDress.GlowLeft,"then rests faint");
                float early=0;int n=0;
                for(float t=0;t<end+.5f;t+=1/120f)
                {
                    float g=NativeCorpseDress.DeadGlow(t,seed);Check(g<=1&&g>=NativeCorpseDress.GlowLeft-1e-6f,"the light within its bounds at "+t);
                    float smooth=t<NativeCorpseDress.GlowHold?1:NativeCorpseDress.GlowLeft+(1-NativeCorpseDress.GlowLeft)*(1-MathUtil.Smooth((t-NativeCorpseDress.GlowHold)/NativeCorpseDress.GlowOut));
                    Check(g<=smooth+1e-5f,"a stutter only ever dims it");if(g<smooth-.05f)dips++;
                    if(t<NativeCorpseDress.GlowHold+NativeCorpseDress.GlowOut*.3f){early+=g;n++;}
                }
                Check(early/n>.5f,"bright through its first stretch");
            }
            Check(dips>40,"it fails in stutters ("+dips+" dimmed samples)");
            for(float g=0;g<=1;g+=.25f)
            {
                var d=NativeCorpseDress.Device(g);
                Check(d.Lid==0&&!d.Chip&&!d.BayChip&&!d.LensChip&&d.Bay==0&&Math.Abs(d.Strips-g)<1e-6f&&Math.Abs(d.Lens-.6f*g)<1e-6f,"the dead device: lid shut, bay dark, strips and lens at the light "+g);
            }
            // ---- the gear on a cut body: each piece on exactly one shown half at every stage ----
            foreach(var stage in new[]{NativeSplitShow.Stage.Making,NativeSplitShow.Stage.On,NativeSplitShow.Stage.Failed})
                for(int side=0;side<2;side++)
                    Check((NativeSplitShow.Wears(0,side,stage)?1:0)+(NativeSplitShow.Wears(1,side,stage)?1:0)==1&&(!NativeSplitShow.Wears(1,side,stage)||NativeSplitShow.Shown(1,stage)),"side "+side+"'s gear worn once, "+stage);
            Hooks(root);
            Console.WriteLine("PASS corpse dress ("+count+" checks: the drawn-pose gate, every class's dropped weapon decided and its hand, the box, the gear's failing light and dead device, the gear once per cut, runtime hooks)");
        }
        private static void Hooks(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f)).Replace("\r\n","\n");
            string death=R("NativeDeathPresentation.cs"),dress=R("NativeCorpseDressPresentation.cs"),vb=R("NativeVisualBodyPresentation.cs"),bisect=R("NativeBisectCorpse.cs");
            // The weapon is read off the rig where it was drawn, before the rig moves onto the drawn body; every death seen as it happens moves it.
            int read=death.IndexOf("var held=pilot?VisualHeldOf(rig,death):null;",StringComparison.Ordinal),move=death.IndexOf("if(pilot&&!temporary&&NativeCorpseDress.FromDrawn(age))VisualRigFromDrawn(rig,death);",StringComparison.Ordinal);
            int fit=death.IndexOf("if(pilot)VisualCorpseFit(corpse,arena);",StringComparison.Ordinal),carry=death.IndexOf("if(pilot)VisualCorpseCarry(corpse,death,held);",StringComparison.Ordinal),drawnOff=death.IndexOf("visualDeathDrawn=null;",StringComparison.Ordinal),dispose=death.IndexOf("if(temporary)rig.Dispose();",StringComparison.Ordinal);
            Check(read>0&&move>read&&fit>move&&carry>fit&&drawnOff>carry&&dispose>carry,"the weapon read before the rig moves, the corpse's carried things once its model is made and fitted, before a temporary rig goes");
            Check(!death.Contains("VictimDeathGap")&&!vb.Contains("body.Motion.VictimAt-death.at")&&vb.Contains("||!NativeCorpseDress.Drawn(body.PosedFrame,Time.frameCount))return false;")&&vb.Contains("if(visualDeathDrawn!=null)VisualCopyDrawn(visualDeathDrawn,binding.Bones);"),"every model death from the drawn pose, not a victim clip's alone");
            int drawn=death.IndexOf("VisualCorpseCarried(corpse,death,alpha,clock);",StringComparison.Ordinal),wound=death.IndexOf("KillWound(corpse,shown,alpha);",StringComparison.Ordinal),remains=death.IndexOf("if(remains)return;",drawn>0?drawn:0,StringComparison.Ordinal);
            Check(drawn>0&&wound>drawn&&remains>wound,"drawn each frame after the body, kept bodies included");
            Check(death.Contains("VisualCorpsesShown();\n            CorpseCarriedShown();")&&death.Contains("private void ClearCorpses(){CorpseCarriedClear();"),"gone with their corpses, the pack mounts back in the pool");
            Check(death.Contains("int hand=WeaponHand(death);")&&bisect.Contains("private static int WeaponHand(NativeDeath death)=>NativeCorpseDress.Hand(death.actor?.SourceSpec);"),"the dropped weapon's body leaves the hand that held it, and goes with that hand's half");
            // The held weapon: hidden as the game hid it, the bake's or the pack's model where the live body drew one, else the game's model.
            Check(dress.Contains("if(!NativeCorpseDress.Drops(spec)||!NativeWeaponDressing.SocketShown(spec)||a.WeaponAway||BladeHidden(a,t))return null;")&&dress.Contains("if(spec==NativeKatana.Spec&&KatanaSheathed(a,t)||GlaiveShown(a,t)<.5f)return null;"),"a weapon the game hid drops nothing");
            Check(dress.Contains("if(fit.Scale!=1&&rig.HandFitted)frame=frame*Matrix4x4.Translate(fit.Centre)*Matrix4x4.Scale(Vector3.one/fit.Scale)*Matrix4x4.Translate(-fit.Centre);")&&dress.Contains("h.Frame=NativeBowWire.Is(spec)?rig.Bones[CapturedRig.LHand].localToWorldMatrix:h.BoneR;")&&dress.Contains("h.Model=HeldWeaponModel(rig,a,false);"),"the model where the live body drew it: the bake's unfitted mount, the pack bow at the bow hand, the game's model");
            Check(dress.Contains("var entry=host.Root.transform.Find(\"VisualWeapons\")?.Find(held.Entry);")&&dress.Contains("w.Pack??=PackWeaponMake(host.Root,held.Pack);")&&dress.Contains("r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.enabled=true;"),"the corpse's own copy of the bake's model, a pooled pack mount; no shadow, shown whatever a cut turned off");
            // Its body: seated round the model, loose of the body's parts, with a cut-off hand.
            Check(dress.Contains("weapon.maxDepenetrationVelocity=NativeCorpseDress.SeatEase;")&&dress.Contains("on.SeatWeapon(at,turn,new Vector3(NativeCorpseDress.Box(size.x),NativeCorpseDress.Box(size.y),NativeCorpseDress.Box(size.z)),held.Hand);")&&dress.Contains("Physics.IgnoreCollision(box,other);")&&dress.Contains("if(Severed.Contains(j)){weapon.linearVelocity=grip.linearVelocity;"),"its body seated round the model, loose of the body, flying with a cut-off hand");
            Check(dress.Contains("if(half.Host!=null&&VisualCorpseWhole(first)&&visualCorpses.TryGetValue(first,out var whole)&&whole.Root!=null)host=whole;"),"a split body's weapon on the half that draws its hand at every stage");
            // The gear: the live body's rules, each piece on its half, gone with a crushed part.
            Check(dress.Contains("k.Dog=HotDogOn(c.Root)||NativeHotDog.Worn(VisualOutfitOf(death.id));k.Armour=WearsArmour(death.id);")&&dress.Contains("if(k.Core!=null&&coreSide&&chestKept&&!k.Dog)")&&dress.Contains("if(k.Chest!=null&&coreSide&&chestKept&&!k.Armour)")&&dress.Contains("var dev=NativeCorpseDress.Device(glow);"),"no core on the Hot Dog, no emblem under the armour, the device shut");
            Check(dress.Contains("bool coreSide=!split||NativeSplitShow.Wears(mine,k.CoreSide,stage)")&&dress.Contains("split&&!NativeSplitShow.Wears(mine,k.DeviceSide,stage)")&&dress.Contains("half.IsCrushed(k.DevicePart)"),"each gear piece on the half its bone went to, none on a crushed part");
            Check(dress.Contains("var anchors=new VisualBody{Look=VisualLookOf(death.id),GearCore=k.Core,GearDevice=k.Device,GearRadius=k.Radius};VisualAnchorsFor(anchors);"),"the gear at the outfit's anchors, as the live body's");
        }
    }
}
#endif
