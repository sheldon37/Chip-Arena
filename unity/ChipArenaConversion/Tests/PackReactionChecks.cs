using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChipArena.UnityConversion;

/// <summary>
/// The owner's Explosive pack reaction clips in the native hit layer (Core/NativePackReact, NativePackClips.Generated): the
/// exported tracks match tools/pack-reactions-provenance.json byte for byte and decode deterministically into finite,
/// bounded channels; no pack FBX/.meta is in the project; reaction selection per weapon, direction, block and stun; the
/// stun's whole-body share and the Stunned loop are exactly zero from the authoritative stun end; non-melee hits and gun and
/// magic pilots keep the procedural layer only; finite bones over every clip; blades kept off the head and torso; no
/// allocation per frame; the MeleeMotion hooks. Presentation only (HitReactionChecks covers the procedural layer).
/// </summary>
internal static class PackReactionChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("Pack reactions: "+what);}
    public static void Run(string root)
    {
        var log=new List<string>();
        Clips(root,log);Selection(log);Blocks(log);StunEnd(log);NonMelee(log);Finite(log);Clearance(log);Allocation(log);Hooks(root,log);
        foreach(var line in log)Console.WriteLine("PASS pack reactions: "+line);
    }
    private static void Clips(string root,List<string> log)
    {
        var names=NativePackClips.Names;var slots=NativePackClips.Slots;
        Check(names.Length==5&&NativePackClips.Sets.Length==names.Length&&slots.Length==8,"five reaction sets of eight slots");
        Check(names[NativePackRules.Sword]=="sword"&&names[NativePackRules.Daggers]=="daggers"&&names[NativePackRules.Greatsword]=="greatsword"&&names[NativePackRules.Shield]=="shield"&&names[NativePackRules.Unarmed]=="unarmed","set order matches NativePackRules");
        foreach(PackSlot slot in Enum.GetValues(typeof(PackSlot)))if(slot!=PackSlot.None)Check(slots[(int)slot]==Char.ToLowerInvariant(slot.ToString()[0])+slot.ToString().Substring(1),"slot order matches PackSlot ("+slot+")");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/pack-reactions-provenance.json")));
        var prov=new Dictionary<string,JsonElement>();foreach(var c in doc.RootElement.GetProperty("clips").EnumerateArray())prov[c.GetProperty("clip").GetString()]=c;
        Check(prov.Count==names.Length*slots.Length&&doc.RootElement.GetProperty("channels").GetInt32()==CapturedClip.Channels,"the provenance lists every clip");
        int frames=0;float worstUnit=0;
        for(int s=0;s<names.Length;s++)for(int k=0;k<slots.Length;k++)
        {
            var c=NativePackClips.Sets[s][k];string key=names[s]+"/"+slots[k];Check(c!=null&&c.Name==key,"clip "+key+" in place");
            var p=prov[key];Check(p.GetProperty("frames").GetInt32()==c.Frames&&p.GetProperty("fps").GetInt32()==c.Fps&&p.GetProperty("source").GetString()==c.Source&&p.GetProperty("loop").GetBoolean()==c.Loop,key+": size and source match the provenance");
            string sha=Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(c.Data))).ToLowerInvariant();Check(sha==p.GetProperty("sha256").GetString(),key+": track is the exporter's output ("+sha+")");
            // Decoding is deterministic: a fresh decode of the same text gives the same floats, bit for bit.
            var a=c.Decode();var b=new CapturedClip(c.Name,c.Source,c.Fps,c.Frames,c.Loop,c.Marks,c.Pace,c.Data).Decode();Check(a.Length==b.Length&&a.Length==c.Frames*CapturedClip.Channels,key+": decoded size");
            for(int i=0;i<a.Length;i++){Check(BitConverter.SingleToInt32Bits(a[i])==BitConverter.SingleToInt32Bits(b[i]),key+": deterministic decode");Check(!float.IsNaN(a[i])&&!float.IsInfinity(a[i])&&Math.Abs(a[i])<4,key+": finite, bounded channel "+(i%CapturedClip.Channels));}
            for(int f=0;f<c.Frames;f++)
            {
                int o=f*CapturedClip.Channels;
                foreach(int u in new[]{MeleeCapture.Blade,MeleeCapture.Edge,MeleeCapture.LFwd,MeleeCapture.LUp}){float l=new Vector3(a[o+u],a[o+u+1],a[o+u+2]).Length();worstUnit=Math.Max(worstUnit,Math.Abs(l-1));Check(Math.Abs(l-1)<.05f,key+": unit direction channels");}
                for(int j=MeleeCapture.Pelvis;j<=MeleeCapture.RAnkle;j+=3)Check(new Vector3(a[o+j],a[o+j+1],a[o+j+2]).Length()<Math.PI+.01f,key+": joint turns within half a turn");
                Check(a[o+MeleeCapture.TwoHand]>=-.01f&&a[o+MeleeCapture.TwoHand]<=1.01f,key+": two-hand weight in [0,1]");
            }
            if(k==(int)PackSlot.Stun)Check(c.Loop&&c.Marks.Length==0,key+": the Stunned clip loops");
            else if(k==(int)PackSlot.Guard)Check(c.Frames==2&&!c.Loop,key+": the guard reference is one frame");
            else Check(!c.Loop&&c.Marks.Length==3&&c.Marks[0]==0&&c.Marks[1]>0&&c.Marks[1]<c.Marks[2]&&Math.Abs(c.Marks[2]-c.Duration)<1e-3f,key+": marks [start, impact, settled]");
            frames+=c.Frames;
        }
        Check(NativePackClips.Sets[NativePackRules.Greatsword][(int)PackSlot.HitF].Source.StartsWith("2Hand-Sword")&&NativePackClips.Sets[NativePackRules.Shield][(int)PackSlot.Block].Source.StartsWith("Armed-Shield")&&NativePackClips.Sets[NativePackRules.Unarmed][(int)PackSlot.HitF].Source.StartsWith("Unarmed"),"each style takes its own pack set");
        // Only retargeted tracks are committed: no pack FBX or its .meta anywhere in the Unity project.
        foreach(var f in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories))
        {
            string rel=f.Substring(root.Length).Replace('\\','/');if(rel.Contains("/bin/")||rel.Contains("/obj/")||rel.Contains("/Library/"))continue;
            Check(!f.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)&&!f.EndsWith(".fbx.meta",StringComparison.OrdinalIgnoreCase),"no FBX in the project ("+rel+")");
        }
        log.Add(names.Length*slots.Length+" clips ("+frames+" frames) match the exporter's provenance (SHA-256 per track), decode bit-identically, finite and bounded (unit channels within "+worstUnit.ToString("0.000")+"); no FBX or .meta from the pack in the project");
    }
    // ---- scenes (HitReactionChecks' lockstep rigs: with and without the layer) ----
    private static HitReactionChecks.Run2 Scene(string victim,string attacker,float angle,float stun,float damage,Action<HitReactionChecks.Run2,float> observe,float until=3.2f,bool head=false)
        =>HitReactionChecks.Scene(victim,attacker,angle,stun,damage,observe,until,head,attacker=="siren"?"secondary":"direct",NativeHitRules.Fighter(attacker));
    private static void Selection(List<string> log)
    {
        Check(NativePackRules.SetFor("sword",HitVictim.Melee)==NativePackRules.Sword&&NativePackRules.SetFor("daggers",HitVictim.Melee)==NativePackRules.Daggers&&NativePackRules.SetFor("greatsword",HitVictim.Melee)==NativePackRules.Greatsword&&NativePackRules.SetFor("shield",HitVictim.Melee)==NativePackRules.Shield,"melee weapon styles take their pack sets");
        Check(NativePackRules.SetFor("emperor",HitVictim.Fighter)==NativePackRules.Unarmed&&NativePackRules.SetFor("ansatsuken",HitVictim.Fighter)==NativePackRules.Unarmed,"the DEV fighters take the Unarmed set");
        foreach(var s in new[]{"pistol","rifle","machinegun"})Check(NativePackRules.SetFor(s,HitVictim.Gun)==NativePackRules.None,s+" keeps the procedural layer");
        foreach(var s in new[]{"fire","water","lightning","darkness","leet"})Check(NativePackRules.SetFor(s,HitVictim.Magic)==NativePackRules.None,s+" keeps the procedural layer");
        Check(NativePackRules.SetFor("sword",HitVictim.Gun)==NativePackRules.None&&NativePackRules.SetFor(null,HitVictim.Melee)==NativePackRules.None,"only the captured melee and fighter routes");
        Check(NativePackRules.Direction(new Vector3(0,0,-1),1)==PackSlot.HitF&&NativePackRules.Direction(new Vector3(.2f,0,-1),1.29f)==PackSlot.HitF&&NativePackRules.Direction(new Vector3(0,0,-1),1.4f)==PackSlot.HitF2,"from the front F1, a heavy blow F2");
        Check(NativePackRules.Direction(new Vector3(1,0,.3f),1)==PackSlot.HitR&&NativePackRules.Direction(new Vector3(-1,0,-.3f),1)==PackSlot.HitL,"pushed to its left: struck on its right (R1), and the reverse");
        Check(NativePackRules.Direction(new Vector3(.3f,0,1),1)==PackSlot.None&&NativePackRules.Direction(new Vector3(float.NaN,0,float.NaN),1)==PackSlot.HitF&&NativePackRules.Direction(Vector3.Zero,1)==PackSlot.HitF,"from behind none (procedural lurch); degenerate pushes read as frontal");
        // Through the layer: the clip each victim plays per attacker side and weapon, and the frames it drives the stun.
        var seen=new List<string>();
        void Expect(string victim,string attacker,float angle,int set,PackSlot slot)
        {
            int gotSet=NativePackRules.None;PackSlot got=PackSlot.None;float peak=0;bool drove=false;
            Scene(victim,attacker,angle,.25f,30,(s,t)=>{var L=s.On.Layer;if(L.PackWeight>peak){peak=L.PackWeight;gotSet=L.PackSet;got=L.Slot;}if(t<HitReactionChecks.Hit+.2f)drove|=L.Drives(t);},2.6f);
            if(slot==PackSlot.None)Check(peak==0&&!drove,victim+" by "+attacker+" at "+angle+": no clip, the stun stays the route's ("+got+" "+peak+")");
            else Check(peak>.99f&&gotSet==set&&got==slot&&drove,victim+" by "+attacker+" at "+angle+": "+NativePackClips.Names[set]+"/"+slot+" drives the stun (got set "+gotSet+" "+got+" at "+peak+", drives "+drove+")");
            seen.Add(victim+" "+angle+"°→"+slot);
        }
        Expect("sword","sword",0,NativePackRules.Sword,PackSlot.HitF);Expect("sword","greatsword",0,NativePackRules.Sword,PackSlot.HitF2);Expect("sword","sword",-90,NativePackRules.Sword,PackSlot.HitR);
        Expect("sword","sword",90,NativePackRules.Sword,PackSlot.HitL);Expect("sword","sword",180,NativePackRules.Sword,PackSlot.None);
        Expect("daggers","sword",0,NativePackRules.Daggers,PackSlot.HitF);Expect("greatsword","sword",-90,NativePackRules.Greatsword,PackSlot.HitR);Expect("shield","sword",90,NativePackRules.Shield,PackSlot.HitL);
        Expect("rifle","sword",0,NativePackRules.None,PackSlot.None);Expect("fire","greatsword",0,NativePackRules.None,PackSlot.None);
        // The DEV fighter route (posed over the native pose here; Runtime runs it on MeleeMotion): the Unarmed set.
        {
            var ledger=new NativeHitLedger();var layer=new NativeHitLayer();var scratch=new MeleeRig();var pose=new CapturedRig.Pose();scratch.WritePose(pose);
            var a=new Actor{Id=1,SourceSpec="emperor",Hp=100,Grounded=true};var foe=new Actor{Id=2,SourceSpec="sword",Position=new V3(0,0,1.3f),Hp=100};
            var h=MeleeReaction.Hit(new V3(0,.865f,0),0,new V3(0,0,-1),new V3(0,.965f,0),2,30,false);
            a.ReactionAt=2;a.ReactionUntil=2.24f;a.ReactionYaw=h.Yaw;a.ReactionStrength=h.Strength;a.ReactionDir=new V3(h.Direction.X,h.Direction.Y,h.Direction.Z);a.StunUntil=2.18f;
            ledger.Observe(new Snapshot{Actors=new[]{a,foe},Events=new[]{new ArenaEvent{Id=1,Kind=EventKind.Hit,Source=2,Target=1,Time=2,Amount=30,Detail="direct"}}});
            int set=NativePackRules.None;for(float t=2;t<2.2f;t+=1/60f){layer.Over(scratch,pose,a,ledger,HitVictim.Fighter,t,0);if(layer.PackWeight>0)set=layer.PackSet;}
            Check(set==NativePackRules.Unarmed,"a fighter struck from the front plays the Unarmed GetHit ("+set+")");
        }
        log.Add("selection: sets by style (gun and magic procedural), F1 front / F2 heavy front / R1 / L1 by side, none from behind; through the layer: "+string.Join(", ",seen)+", fighter Unarmed");
    }
    private static void Blocks(List<string> log)
    {
        // A string of blocked greatsword blows alternates the shield set's two block hits; a lone block takes the first.
        var slots=new List<PackSlot>();float peak=0,after=0;bool driveMelee=false,driveGun=false;
        {
            var s=new HitReactionChecks.Run2("shield");float[] blows={2f,2.3f,2.6f,3.6f};int i=0;PackSlot shown=PackSlot.None;
            for(float t=1;t<4.3f;t+=HitReactionChecks.Dt)
            {
                foreach(var r in s.Both){r.A.BlockAt=1.9f;r.A.BlockUntil=4.2f;}
                if(i<blows.Length&&t>=blows[i]-1e-4f){s.Ledger.Observe(new Snapshot{Actors=new[]{s.On.A,new Actor{Id=2,SourceSpec="greatsword",Position=new V3(0,0,1.3f)}},Events=new[]{new ArenaEvent{Id=++HitReactionChecks.eventId,Kind=EventKind.Block,Source=1,Target=2,Time=t}}});i++;shown=PackSlot.None;}
                s.Step(t,HitReactionChecks.Dt);var L=s.On.Layer;
                if(L.PackBlockWeight>.5f&&shown==PackSlot.None){shown=L.BlockSlot;slots.Add(shown);}
                peak=Math.Max(peak,L.PackBlockWeight);if(t>2.6f+NativePackRules.BlockSpan+.02f&&t<3.6f)after=Math.Max(after,L.PackBlockWeight);
                if(t>2&&t<2.2f)driveMelee|=L.Drives(t)||L.DrivesBlock(t);
            }
            var g=new HitReactionChecks.Run2("sword");
            for(float t=1;t<2.5f;t+=HitReactionChecks.Dt)
            {
                foreach(var r in g.Both){r.A.BlockAt=1.9f;r.A.BlockUntil=2.4f;}
                if(t>=2-1e-4f&&t<2+HitReactionChecks.Dt-1e-4f)g.Ledger.Observe(new Snapshot{Actors=new[]{g.On.A,new Actor{Id=3,SourceSpec="rifle",Position=new V3(0,0,8)}},Events=new[]{new ArenaEvent{Id=++HitReactionChecks.eventId,Kind=EventKind.Block,Source=1,Target=3,Time=t}}});
                g.Step(t,HitReactionChecks.Dt);driveGun|=g.On.Layer.DrivesBlock(t)||g.On.Layer.PackBlockWeight>0;
            }
        }
        Check(slots.Count==4&&slots[0]==PackSlot.Block&&slots[1]==PackSlot.Block2&&slots[2]==PackSlot.Block&&slots[3]==PackSlot.Block,"a string of blocks alternates Block-GetHit 1/2, a lone block takes 1 ("+string.Join(",",slots)+")");
        Check(peak>.7f&&after==0&&driveMelee,"the block hit plays inside its window and is gone after it ("+peak+", "+after+")");
        Check(!driveGun,"a blocked bullet plays no block hit");
        log.Add("blocks: Block-GetHit "+string.Join("/",slots)+" along a string, peak weight "+peak.ToString("0.00")+", none past its .47 s window, none for blocked bullets");
    }
    private static void StunEnd(List<string> log)
    {
        // The rules: exactly zero at and after the stun end and the clip window's end.
        foreach(float span in new[]{.08f,.12f,.18f,.28f,.6f,1.2f})
        {
            Check(NativePackRules.StunFade(2+span,2,2+span)==0&&NativePackRules.StunFade(2+span+.001f,2,2+span)==0&&NativePackRules.StunFade(2+span-.001f,2,2+span)>0&&NativePackRules.StunFade(2+span-.001f,2,2+span)<.05f,"the stun fade ends exactly at the stun end ("+span+")");
            Check(NativePackRules.Weight(span,span)==0&&NativePackRules.Weight(0,span)==0&&NativePackRules.Weight(span*.5f,span)>0,"a clip window's weight is 0 at both ends ("+span+")");
            Check(NativeHitRules.Daze(2+span,2,2+span)==0,"the daze is 0 at the stun end ("+span+")");
        }
        Check(NativePackRules.LongStun(.28f)==0&&NativePackRules.LongStun(.6f)==1&&NativePackRules.LongStun(float.NaN)==0,"the Stunned loop is for long stuns only");
        // Through the layer, for a melee hit stun, the swap-shop stun (.5 s from its blow, the only one in play long enough for
        // the Stunned loop) and a longer stun: on the first frame at or past StunUntil every stun share is 0.
        var lines=new List<string>();
        foreach(var victim in new[]{"sword","greatsword","daggers"})foreach(float stun in new[]{.18f,.5f,1.2f})
        {
            float until=0,lastBody=0,lastLoop=0,body=-1,loop=-1,daze=-1,peakLoop=0;
            Scene(victim,"sword",0,stun,30,(s,t)=>
            {
                var L=s.On.Layer;until=s.On.A.StunUntil;
                if(t<until){lastBody=L.PackBody;lastLoop=L.PackStunWeight;peakLoop=Math.Max(peakLoop,L.PackStunWeight);}
                else if(body<0){body=L.PackBody;loop=L.PackStunWeight;daze=L.DazeWeight;}
            },HitReactionChecks.Hit+stun+.3f);
            Check(body==0&&loop==0&&daze==0,victim+" "+stun+" s stun: the whole-body share, the Stunned loop and the daze are 0 from StunUntil ("+body+", "+loop+", "+daze+")");
            // A melee hit stun eases the GetHit clip's whole-body share out up to its last frame; a long stun outlasts the clip
            // and the Stunned loop eases out instead.
            if(stun>1)Check(peakLoop>.9f&&lastLoop>0&&lastLoop<.35f,victim+": a long stun holds the Stunned loop ("+peakLoop+") and eases it out up to the last frame ("+lastLoop+")");
            else if(stun>.3f)Check(peakLoop>.25f&&peakLoop<.6f&&lastLoop<.05f,victim+": a swap-shop stun shows the Stunned loop under the blow's clip ("+peakLoop+") and eases it out by the last frame ("+lastLoop+")");
            else Check(peakLoop==0&&lastBody>0&&lastBody<.35f,victim+": a melee hit stun shows no Stunned loop ("+peakLoop+") and eases the whole-body share out up to the last frame ("+lastBody+")");
            lines.Add(victim+" "+stun+"s: last frame body "+lastBody.ToString("0.00")+" loop "+lastLoop.ToString("0.00")+(stun>.3f?", peak loop "+peakLoop.ToString("0.00"):""));
        }
        // A freezer-block freeze (StunMelee 1.2 s with no blow of its own): no clip, no Stunned loop; the procedural daze only
        // over its last .3 s, 0 from StunUntil.
        foreach(var victim in new[]{"sword","greatsword"})
        {
            var r=new HitReactionChecks.Run2(victim);float clips=0,early=0,after=-1,until=0;
            for(float t=1;t<3.6f;t+=HitReactionChecks.Dt)
            {
                if(until==0&&t>=HitReactionChecks.Hit-1e-4f){until=t+1.2f;r.On.A.StunUntil=r.Off.A.StunUntil=until;}
                r.Step(t,HitReactionChecks.Dt);var L=r.On.Layer;
                if(until>0&&t<until){clips=Math.Max(clips,L.PackWeight+L.PackStunWeight+L.PackBlockWeight);if(t<until-.3f)early=Math.Max(early,L.DazeWeight);}
                else if(until>0&&after<0)after=L.DazeWeight+L.PackStunWeight+L.PackBody;
            }
            Check(clips==0&&early==0&&after==0,victim+": a freeze with no blow plays no pack clip or Stunned loop ("+clips+"), dazes only its last .3 s ("+early+" before) and ends at StunUntil ("+after+")");
        }
        lines.Add("freeze without a blow: no clip or loop");
        log.Add("stun end: fade, clip window and daze exactly 0 at StunUntil; through the layer every stun share is 0 on the first frame past it ("+string.Join("; ",lines)+")");
    }
    private static void NonMelee(List<string> log)
    {
        foreach(var victim in new[]{"sword","daggers","greatsword","shield"})foreach(var attacker in new[]{"pistol","fire"})
        {
            float m=0,w=0;bool drove=false;Scene(victim,attacker,0,0,23,(s,t)=>{m=Math.Max(m,s.MaxDelta());var L=s.On.Layer;w=Math.Max(w,Math.Max(L.PackWeight,Math.Max(L.PackStunWeight,L.PackBlockWeight)));drove|=L.Drives(t);},2.8f);
            Check(m<1e-5f&&w==0&&!drove,victim+" hit by "+attacker+": no clip and no layer ("+m+", "+w+")");
        }
        foreach(var victim in new[]{"rifle","fire"})
        {
            float w=0;Scene(victim,"greatsword",0,.28f,52,(s,t)=>{var L=s.On.Layer;w=Math.Max(w,Math.Max(L.PackWeight,Math.Max(L.PackStunWeight,L.PackBlockWeight)));},2.8f);
            Check(w==0,victim+": a melee blow on a "+victim+" pilot stays procedural ("+w+")");
        }
        log.Add("pistol and magic hits on the four melee styles play no clip and add nothing; gun and magic pilots stay procedural under melee blows");
    }
    private static void Finite(List<string> log)
    {
        // Every clip of every set over a guard pose, at several weights and times through and past its length.
        var pack=new NativePackPose();var r=new MeleeRig();var pose=new CapturedRig.Pose();var row=new float[CapturedClip.Channels];int samples=0;
        string[] specs={"sword","daggers","greatsword","shield",null};
        for(int s=0;s<NativePackClips.Sets.Length;s++)
        {
            var melee=specs[s]!=null?MeleeSet.For(specs[s]):null;
            for(int k=0;k<NativePackClips.Slots.Length;k++)
            {
                var c=NativePackClips.Sets[s][k];
                foreach(float w in new[]{.25f,1})for(float t=-.1f;t<c.Duration+.2f;t+=c.Duration/9)
                {
                    r.Reset();r.GroupYaw=0;if(melee!=null){melee.Idle.Sample(0,row);MeleeCapture.Apply(r,melee,row,CaptureOptions.Of(1,melee.Fit,legs:0));}
                    pack.Layer(r,c,t,k==(int)PackSlot.Stun?NativePackRules.Clip(s,PackSlot.Guard):null,0,w,1,1,s!=NativePackRules.Shield||k>=(int)PackSlot.Block,melee,s==NativePackRules.Greatsword,1,.5f);
                    r.WritePose(pose);pose.World();samples++;
                    for(int b=0;b<CapturedRig.Bones.Length;b++){var p=pose.WorldPosition[b];var q=pose.WorldRotation[b];Check(!float.IsNaN(p.X+p.Y+p.Z+q.X+q.Y+q.Z+q.W)&&!float.IsInfinity(p.X+p.Y+p.Z)&&Math.Abs(q.Length()-1)<1e-3f&&p.Length()<3,NativePackClips.Names[s]+"/"+NativePackClips.Slots[k]+": finite bones at "+t);}
                }
            }
        }
        // Degenerate inputs leave the rig as it was.
        r.Reset();var before=new Quaternion[CapturedRig.Bones.Length];Array.Copy(r.Q,before,before.Length);
        pack.Layer(r,NativePackRules.Clip(0,PackSlot.HitF),.1f,null,0,float.NaN,1,1,true,MeleeSet.For("sword"),false,1,1);pack.Layer(r,null,.1f,null,0,1,1,1,true,null,false,1,1);pack.Layer(r,NativePackRules.Clip(0,PackSlot.HitF),.1f,null,0,0,1,1,true,null,false,1,1);
        for(int b=0;b<before.Length;b++)Check(r.Q[b]==before[b],"NaN, missing or zero-weight clips change nothing");
        log.Add(samples+" layered poses over every clip of every set: finite bones, unit rotations; NaN, missing and zero-weight clips change nothing");
    }
    // Signed distance of a point from a rig box (negative inside).
    private static float Inside(CapturedRig.Pose pose,CapturedRig.Box box,Vector3 p)
    {
        var q=pose.WorldRotation[box.Bone];Vector3 c=pose.WorldPosition[box.Bone]+Vector3.Transform(box.Position,q),l=Vector3.Transform(p-c,Quaternion.Conjugate(q)),h=box.Size*.5f;
        return Math.Max(Math.Abs(l.X)-h.X,Math.Max(Math.Abs(l.Y)-h.Y,Math.Abs(l.Z)-h.Z));
    }
    private static void Clearance(List<string> log)
    {
        // Blades held through stunned reactions from the front and sides stay out of the head and torso as far as the route's
        // own pose does (the arms follow the captures through MeleeClearance, and the layer clears the blade once more after the
        // procedural accent on top).
        var boxes=new List<CapturedRig.Box>();foreach(var b in CapturedRig.Boxes)if(b.Bone==CapturedRig.Head&&b.Size.X>.3f||b.Bone==CapturedRig.Chest&&b.Size.X>.5f)boxes.Add(b);
        float worst=0;int frames=0,touching=0;
        float Depth(CapturedRig.Pose pose,MeleeSet set)
        {
            float d=0;var q=pose.WorldRotation[CapturedRig.RHand];Vector3 g=pose.WorldPosition[CapturedRig.RHand];
            for(float s=.25f;s<=set.Tip.Z;s+=.08f){Vector3 p=g+Vector3.Transform(new Vector3(0,0,s),q);foreach(var b in boxes)d=Math.Max(d,-Inside(pose,b,p));}
            return d;
        }
        foreach(var victim in new[]{"sword","daggers","greatsword"})foreach(float angle in new[]{0f,-90,90})foreach(var attacker in new[]{"sword","greatsword"})
        {
            var set=MeleeSet.For(victim);
            Scene(victim,attacker,angle,.28f,40,(s,t)=>{frames++;float d=Depth(s.On.Pose,set)-Depth(s.Off.Pose,set);worst=Math.Max(worst,d);if(d>.01f)touching++;},2.7f);
        }
        Check(worst<.02f,"blades stay out of the head and torso (at most "+worst+" m deeper than the route's own pose; "+touching+" frames past 1 cm)");
        log.Add("blades over "+frames+" stunned-reaction frames (sword, daggers, greatsword; front and sides): "+touching+" frames more than 1 cm deeper into the head or torso than the route's own pose, at most "+(worst*100).ToString("0.0")+" cm");
    }
    private static void Allocation(List<string> log)
    {
        var ledger=new NativeHitLedger();var layer=new NativeHitLayer();var scratch=new MeleeRig();var pose=new CapturedRig.Pose();
        var set=MeleeSet.For("greatsword");var row=new float[CapturedClip.Channels];set.Idle.Sample(0,row);MeleeCapture.Apply(scratch,set,row,CaptureOptions.Of(1,set.Fit,legs:0));scratch.WritePose(pose);
        var a=new Actor{Id=1,SourceSpec="greatsword",Hp=100,Grounded=true,ReactionAt=2,ReactionUntil=2.24f,ReactionStrength=.9f,ReactionDir=new V3(-1,0,-.2f),StunUntil=3.2f,BlockAt=3.3f,BlockUntil=3.9f};
        var snap=new Snapshot{Actors=new[]{a,new Actor{Id=2,SourceSpec="sword",Position=new V3(1.3f,0,0)}},Events=new[]{new ArenaEvent{Id=1,Kind=EventKind.Hit,Source=2,Target=1,Time=2,Detail="direct"},new ArenaEvent{Id=2,Kind=EventKind.Block,Source=1,Target=2,Time=3.4f}}};
        ledger.Observe(snap);for(float t=1.9f;t<3.9f;t+=1/60f){layer.Over(scratch,pose,a,ledger,HitVictim.Melee,t,0);}
        long before=GC.GetAllocatedBytesForCurrentThread();float seen=0;
        for(int i=0;i<1200;i++){float t=1.95f+i/600f;ledger.Observe(snap);layer.Over(scratch,pose,a,ledger,HitVictim.Melee,t,0);seen=Math.Max(seen,layer.PackWeight+layer.PackStunWeight+layer.PackBlockWeight);layer.Drives(t);layer.DrivesBlock(t);}
        long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(seen>0,"the allocation run plays pack clips");Check(used==0,"no allocation per frame with pack clips ("+used+" bytes over 1200 frames)");
        log.Add("no allocation per frame with the hit, Stunned and block clips playing (1200 frames)");
    }
    private static void Hooks(string root,List<string> log)
    {
        string mm=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/MeleeMotion.cs"));
        Check(mm.Contains("if(HitLayer!=null&&HitLayer.Drives(time)){Ready(set,p,time,MeleeReady(p,time));State=\"hit reaction\";}"),"the captured melee route holds the guard while the pack clip drives its stun");
        Check(mm.Contains("if(!p.Juggle&&!p.Down&&p.HitStunUntil>time){var h=ReactionOf(p);if(h==null||!React(p,h,time,true))FallbackFlinch(p,time);}"),"the fighter route keeps its own recoil (the head snap) under the Unarmed clip");
        Check(mm.Contains("time>=impact&&!(HitLayer!=null&&HitLayer.DrivesBlock(time))"),"the captured block hit yields to the layer's block clip");
        Check(mm.Contains("Stagger(set,p,time,MeleeReady(p,time));State=\"hit reaction\";")&&mm.Contains("FallbackFlinch(p,time);"),"without the layer (source parity, intro and viewer rigs) the route's own stagger stays");
        log.Add("MeleeMotion hooks: a driven stun holds the guard (captured melee; the fighters keep their recoil under the clip), a driven block skips the captured block hit; without the layer the source port is unchanged");
    }
}
