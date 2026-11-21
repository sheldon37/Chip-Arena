#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Expected values come from tools/export_rig_dressing_fixtures.mjs, which runs the pinned original bodyModifiers,
    // spellCharge, spellMotes, BlockCooldownTint, poseFirstPersonView and buildSiren on real source rigs; nothing here is
    // produced by the C# port.
    internal static class RigDressingChecks
    {
        private static readonly string[] Parents={"chest","leftArm","rightArm","leftFoot","rightFoot"};
        private static readonly int[] ParentBones={CapturedRig.Chest,CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LFoot,CapturedRig.RFoot};
        private static int School(string spec)=>spec=="fire"?1:spec=="water"?2:spec=="lightning"?3:spec=="darkness"?4:0;
        public static void Run(string root,ConversionContent data)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/rig-dressing-parity.json")));var r=document.RootElement;string where="";
            void Near(float a,float b,string name,float tolerance=.00002f){if(!MathUtil.Finite(a)||Math.Abs(a-b)>tolerance)throw new Exception("Rig dressing "+where+" "+name+": "+a+" != "+b);}
            void Check(bool ok,string name){if(!ok)throw new Exception("Rig dressing "+where+" "+name);}
            Vector3 V(JsonElement e)=>new Vector3(e[0].GetSingle(),e[1].GetSingle(),e[2].GetSingle());
            Quaternion Q(JsonElement e)=>new Quaternion(e[0].GetSingle(),e[1].GetSingle(),e[2].GetSingle(),e[3].GetSingle());
            void Rot(Quaternion a,JsonElement e,string name)=>Near(1-Math.Abs(Quaternion.Dot(a,Q(e))),0,name);
            Dictionary<string,int> Chips(JsonElement e){var d=new Dictionary<string,int>();foreach(var p in e.EnumerateObject())d[p.Name]=p.Value.GetInt32();return d;}
            string Shape(WeaponShape s)=>s==WeaponShape.Cube?"cube":s==WeaponShape.Sphere?"sphere":s==WeaponShape.Cone?"cone":"ring";
            int Color(string spec)=>(int)data.weapons.First(w=>w.id==spec).color;
            // 1. Body chips: the native stats agree on every key bodyModifiers reads; the source stats drive the build; every
            // joint's pieces match in order after the orbit and dash-jet update.
            var chips=new BodyChip[NativeBodyChips.MaxPieces];int bodyPieces=0,bodyCases=0,orbiting=0,parked=0,jets=0;
            foreach(var c in r.GetProperty("body").EnumerateArray())
            {
                string spec=c.GetProperty("spec").GetString();var held=Chips(c.GetProperty("chips"));float time=c.GetProperty("time").GetSingle(),dash=c.GetProperty("dashTime").GetSingle();
                where="body "+spec+" "+string.Join(",",held.Select(k=>k.Key+":"+k.Value))+" t="+time;
                var stats=new ChipStats(data.baseStats);var native=NativeContentRules.Stats(data,spec,held);
                foreach(var s in c.GetProperty("stats").EnumerateObject()){stats[s.Name]=s.Value.GetDouble();Near((float)native[s.Name],(float)s.Value.GetDouble(),"native stat "+s.Name,.00001f);}
                int n=NativeBodyChips.Build(held,stats,chips);var pieces=c.GetProperty("pieces");int expected=0;
                for(int p=0;p<Parents.Length;p++)
                {
                    var list=pieces.GetProperty(Parents[p]);expected+=list.GetArrayLength();int k=0;
                    for(int i=0;i<n;i++)
                    {
                        if(chips[i].Bone!=ParentBones[p])continue;Check(k<list.GetArrayLength(),Parents[p]+" has an extra piece");var e=list[k++];var b=chips[i];NativeBodyChips.Animate(ref b,time,dash);
                        string at=Parents[p]+" piece "+(k-1);Check(e.GetProperty("shape").GetString()==Shape(b.Piece.Shape),at+" shape");
                        Check(e.GetProperty("glow").GetBoolean()==b.Piece.Glow&&e.GetProperty("shadow").GetBoolean()==!b.Piece.Glow,at+" material");Check(e.GetProperty("color").GetInt32()==b.Piece.Color,at+" colour");
                        Check(e.GetProperty("visible").GetBoolean()==b.Visible,at+" visibility");
                        Near(Vector3.Distance(b.Piece.Position,V(e.GetProperty("p"))),0,at+" position");Near(Vector3.Distance(b.Piece.Scale,V(e.GetProperty("s"))),0,at+" scale");Rot(b.Piece.Rotation,e.GetProperty("q"),at+" rotation");
                        if(b.Piece.Shape==WeaponShape.Sphere){if(b.Piece.Orbit>=0)orbiting++;else parked++;}if(b.Jet&&b.Visible)jets++;
                    }
                    Check(k==list.GetArrayLength(),Parents[p]+" piece count "+k+" != "+list.GetArrayLength());
                }
                Check(n==expected,"piece count "+n+" != "+expected);bodyPieces+=n;bodyCases++;
            }
            Check(orbiting>0&&parked>0&&jets>0,"the cases cover circling orbs, parked orbs and lit dash jets");
            // The first-person view keeps only the chest chips (the legs are hidden; the view model draws no upper arms).
            where="first person";Check(NativeBodyChips.FirstPerson(CapturedRig.Chest)&&!NativeBodyChips.FirstPerson(CapturedRig.LArm)&&!NativeBodyChips.FirstPerson(CapturedRig.RFoot),"visible joints");
            // The dash jets' clock: a roll's remaining time, an Emperor wavedash's .18 s push, nothing under a hit.
            {
                where="dash time";var wave=new NativeWaveDash();var a=new Actor{SourceSpec="sword",RollAt=4,VisualRollDuration=.46f};
                Near(NativeBodyChips.DashTime(a,wave,4.1f),.36f,"rolling");Near(NativeBodyChips.DashTime(a,wave,4.5f),0,"after the roll");
                var e=new Actor{SourceSpec="emperor",SourceAction="wave",SourceAt=6,SourceUntil=6.18f};Near(NativeBodyChips.DashTime(e,wave,6.05f),.13f,"wavedash");
                e.StunUntil=6.2f;Near(NativeBodyChips.DashTime(e,wave,6.1f),0,"wavedash cleared by a hit");e.StunUntil=0;Near(NativeBodyChips.DashTime(e,wave,6.12f),0,"a stopped wave does not restart");
                Near(NativeBodyChips.DashTime(new Actor{SourceSpec="pistol"},wave,1),0,"standing");
            }
            // 2. Spell charge: phrase choice, growth, release flash, fade, halo spin, and the Ansatsuken's Hadoken charge.
            int chargeSamples=0,shown=0;
            foreach(var c in r.GetProperty("charge").EnumerateArray())
            {
                string spec=c.GetProperty("spec").GetString();where="charge "+c.GetProperty("name").GetString();
                Near(Vector3.Distance(NativeSpellCharge.Socket,V(c.GetProperty("socket"))),0,"socket");var orb=c.GetProperty("orb");
                Check(orb.GetProperty("shape").GetString()=="sphere"&&orb.GetProperty("glow").GetBoolean()&&orb.GetProperty("color").GetInt32()==Color(spec),"orb");Near(orb.GetProperty("s")[0].GetSingle(),NativeSpellCharge.OrbScale,"orb size");
                var halos=c.GetProperty("halos");Check(halos.GetArrayLength()==2,"two halos");
                foreach(var h in halos.EnumerateArray()){Check(h.GetProperty("shape").GetString()=="ring"&&h.GetProperty("glow").GetBoolean()&&h.GetProperty("color").GetInt32()==Color(spec),"halo");Near(Vector3.Distance(NativeSpellCharge.HaloScale,V(h.GetProperty("s"))),0,"halo scale");}
                if(School(spec)>0||spec=="leet")Near(NativeSpellCharge.CastWindup(spec=="leet"?MagicPose.Leet:School(spec),c.GetProperty("attackRate").GetSingle()),c.GetProperty("castWindup").GetSingle(),"cast windup");
                var actor=new Actor{SourceSpec=spec,MagicVisual=School(spec),Hp=c.GetProperty("hp").GetSingle(),AttackAt=c.GetProperty("lastAttack").GetSingle(),CastRate=c.GetProperty("attackRate").GetSingle()};
                var ab=c.GetProperty("ability");if(ab.ValueKind!=JsonValueKind.Null){actor.AbilityId=ab.GetProperty("id").GetString();actor.AbilityAt=ab.GetProperty("start").GetSingle();actor.AbilityRelease=actor.AbilityAt+ab.GetProperty("releaseAt").GetSingle();}
                foreach(var s in c.GetProperty("samples").EnumerateArray())
                {
                    float t=s.GetProperty("time").GetSingle();where="charge "+c.GetProperty("name").GetString()+" t="+t;
                    bool charging=false;float charge=0;
                    if(spec=="ansatsuken"){var action=s.GetProperty("action");actor.SourceAction=action.ValueKind==JsonValueKind.Null?null:action.GetString();float amount=s.GetProperty("charge").GetSingle();actor.SourceAt=t-amount*NativeSpellCharge.ChargeSeconds;
                        charging=NativeSpellCharge.Charging(actor,t,out charge);if(charging)Near(charge,amount,"charge from the replicated action",.0001f);}
                    var pose=NativeSpellCharge.Sample(actor,t,charging,charge);bool vis=s.GetProperty("visible").GetBoolean();Check(pose.Visible==vis,"visibility "+pose.Visible);
                    var ps=s.GetProperty("ability");if(ps.ValueKind!=JsonValueKind.Null){Check(NativeSpellCharge.Phrase(actor,t,out float age,out float release),"ability phrase");Near(release,ps.GetProperty("releaseAt").GetSingle(),"ability release",.0001f);}
                    if(vis||spec=="ansatsuken"){if(vis)Near(pose.Scale,s.GetProperty("scale").GetSingle(),"scale",.0002f);Rot(pose.Rotation,s.GetProperty("q"),"rotation");var hq=s.GetProperty("halos");Rot(pose.Halo0,hq[0],"halo 0");Rot(pose.Halo1,hq[1],"halo 1");}
                    chargeSamples++;if(vis)shown++;
                }
            }
            // 3. Hand motes: the emission schedule, positions in the hand's frame, colour, count and power, frame by frame.
            int moteFrames=0,motes=0;var buffer=new SpellMote[16];
            foreach(var c in r.GetProperty("motes").EnumerateArray())
            {
                string spec=c.GetProperty("spec").GetString();var clock=new NativeSpellMotes();float quality=c.GetProperty("quality").GetSingle();
                var actor=new Actor{SourceSpec=spec,MagicVisual=School(spec),CastRate=c.GetProperty("attackRate").GetSingle()};var ab=c.GetProperty("ability");
                if(ab.ValueKind!=JsonValueKind.Null){actor.AbilityId=ab.GetProperty("id").GetString();actor.AbilityAt=ab.GetProperty("start").GetSingle();actor.AbilityRelease=actor.AbilityAt+ab.GetProperty("releaseAt").GetSingle();}
                foreach(var f in c.GetProperty("frames").EnumerateArray())
                {
                    double t=f.GetProperty("time").GetDouble();where="motes "+c.GetProperty("name").GetString()+" t="+t;actor.AttackAt=f.GetProperty("lastAttack").GetSingle();
                    if(School(spec)>0||spec=="leet")Near(NativeSpellCharge.CastWeight(actor,(float)t),f.GetProperty("cast").GetSingle(),"cast weight",.0002f);
                    int n=clock.Step(actor,(float)t,f.GetProperty("dt").GetDouble(),quality,School(spec)>0||spec=="leet"?Color(spec):0,buffer);var bursts=f.GetProperty("bursts");
                    Check(n==bursts.GetArrayLength(),"mote count "+n+" != "+bursts.GetArrayLength());
                    for(int i=0;i<n;i++){var e=bursts[i];var m=buffer[i];var p=m.Local+new Vector3(m.Jitter);Near(Vector3.Distance(p,V(e.GetProperty("p"))),0,"mote "+i+" position",.00005f);
                        Check(m.Color==e.GetProperty("color").GetInt32()&&m.Count==e.GetProperty("count").GetInt32(),"mote "+i+" colour/count");Near(m.Power,e.GetProperty("power").GetSingle(),"mote "+i+" power");}
                    moteFrames++;motes+=n;
                }
            }
            // 4. Block cooldown dimming and the parry flash on a white lit part, a white unlit part and an excluded part.
            int tints=0;Vector3 flashColor=NativeBlockTint.Linear(NativeBlockTint.FlashColor),glowColor=NativeBlockTint.Linear(NativeBlockTint.FlashGlow);
            Near(NativeBlockTint.FlashSeconds,r.GetProperty("parryFlash").GetSingle(),"parry flash seconds");Near(NativeSpellCharge.ChargeSeconds,r.GetProperty("arcadeChargeSeconds").GetSingle(),"charge seconds");
            foreach(var c in r.GetProperty("tint").EnumerateArray())
            {
                bool alive=c.GetProperty("hp").GetSingle()>0;float cd=c.GetProperty("blockCd").GetSingle(),bt=c.GetProperty("blockTime").GetSingle(),parry=c.GetProperty("parryAt").GetSingle(),t=c.GetProperty("time").GetSingle();
                where="tint cd="+cd+" block="+bt+" parry="+parry+" t="+t;float flash=NativeBlockTint.Flash(alive,parry,t),f=NativeBlockTint.Factor(alive,cd,bt,flash,t),k=NativeBlockTint.FlashLerp(flash);
                var lit=V(c.GetProperty("lit"));var want=Vector3.Lerp(new Vector3(f),flashColor,k);Near(Vector3.Distance(lit,want),0,"lit colour",.0001f);Near(Vector3.Distance(V(c.GetProperty("flat")),want),0,"unlit colour",.0001f);
                var glow=flash>0?glowColor*(NativeBlockTint.GlowGain*flash):Vector3.Zero;Near(Vector3.Distance(V(c.GetProperty("emissive")),glow),0,"emissive",.0001f);
                Near(Vector3.Distance(V(c.GetProperty("excluded")),Vector3.One),0,"excluded part");
                // The native overlay (colour c at opacity k over the dimmed part) reproduces a white part under unit light.
                if(flash>0)Near(Vector3.Distance(new Vector3(f)*(1-k)+NativeBlockTint.OverlayColor(flash)*k,lit+glow),0,"overlay equivalent",.0002f);
                tints++;
            }
            where="tint clocks";var blocker=new Actor{BlockUntil=3,BlockReady=5};Near(NativeBlockTint.Cooldown(blocker,4),1,"cooldown left");Near(NativeBlockTint.Blocking(blocker,2.5f),.5f,"block left");Near(NativeBlockTint.Cooldown(blocker,6),0,"cooldown over");
            // 5. The first-person class bases (Unity camera space: z negated) and the view scale and turn.
            foreach(var c in r.GetProperty("view").EnumerateArray())
            {
                string spec=c.GetProperty("spec").GetString(),cls=c.GetProperty("cls").GetString();where="view "+spec;var b=V(c.GetProperty("base"));var unity=new Vector3(b.X,b.Y,-b.Z);
                Check(NativeViewBase.IsFighter(spec)==(cls=="fighter"),"fighter class");
                var native=cls=="fighter"?NativeViewBase.Fighter:cls=="melee"?NativeViewBase.Melee:cls=="ranged"?NativeViewBase.Gun:NativeFirstPersonPose.MagicViewOffset(0)-new Vector3(0,NativeFirstPersonPose.NativeRigLift,0);
                Near(Vector3.Distance(native,unity),0,"base offset");Near(c.GetProperty("scale").GetSingle(),NativeFirstPersonPose.Scale,"view scale");Near(c.GetProperty("yaw").GetSingle(),(float)Math.PI,"view turn",.00001f);
            }
            // 6. The Siren's weapon socket stays hidden: the source runs the modules and enchant on it, but none of them shows.
            {
                var s=r.GetProperty("siren");where="siren socket";Check(s.GetProperty("meshes").GetInt32()>0&&s.GetProperty("shown").GetInt32()==0&&!s.GetProperty("socketVisible").GetBoolean(),"fixture");
                Check(!NativeWeaponDressing.SocketShown("siren")&&NativeWeaponDressing.SocketShown("sword")&&NativeWeaponDressing.SocketShown("pistol")&&NativeWeaponDressing.SocketShown("emperor"),"native socket rule");
            }
            Console.WriteLine($"PASS rig dressing: {bodyPieces} body-chip pieces over {bodyCases} loadouts, {chargeSamples} spell-charge samples ({shown} shown), {motes} hand motes over {moteFrames} frames, {tints} block-tint cases, view bases and the hidden Siren socket match the source");
        }
    }
}
#endif
