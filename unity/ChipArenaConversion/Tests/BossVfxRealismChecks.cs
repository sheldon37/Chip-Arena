#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The zone bosses' effects realism pass (owner 2026-10-19: "rework the Ironworks and Metro boss visual effects to look more
    // realistic (When Ironworks "pours" it looks like minecraft lava right now. I want it to flow realistically."), presentation
    // only (docs/EXPEDITION-BOSS-CITY-IRONWORKS.md and docs/EXPEDITION-BOSS-CENTRAL-STATION.md "Effects realism"):
    //  - the telegraphs and cues are the ones before the pass: the same telegraph quads (count and FNV hash of every corner,
    //    colour, mode, blend and layer, frame by frame) and the same cue events (key, time, place) over both DEV previews and the
    //    Converter's authority scenario, against values recorded on the code before the pass with these same procedures;
    //  - every authority pool is drawn exactly on its hazard: its centre, its farthest edge on the radius, its nearest within the
    //    lobes' 6%, the full radius from its hit to its `until` (the visible hot area is the damaging area), still hot at its
    //    `until`, cold within .4 s after it and gone once solidified; the pour's puddle before its hit stays inside the radius;
    //  - the pour's stream lands on the authority's pool point (its arc's end on the hazard), the preview's on the rule's point;
    //  - a fresh effects builder at any time draws the same live pools as one that ran from the start (host, guests, the killcam
    //    and late joiners draw the same frame from the same replicated times); two builders draw the same mesh and particles;
    //  - the reduced effects quality and the low tier draw fewer particles (and the low tier a lighter molten mesh), never fewer
    //    telegraphs; nothing allocates per frame (the authority scenario and the station's preview with its glass);
    //  - with no boss nothing is built; the presentation's sources name no authority, network or rules code, the authority's
    //    pool rules are the ones the preview mirrors, and the authority, rules and network sources name none of the new types.
    internal static class BossVfxRealismChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Boss effects realism: "+what);count++;}
        // The baseline: recorded on the code before the pass (claude/sleepy-ritchie-g8ruhv 351c26f) with these procedures.
        private const int IwPreviewFrames=8092,IwPreviewCues=303,IwAuthorityCues=15,StPreviewFrames=6480,StPreviewCues=354;
        private const long IwPreviewTele=166089,IwAuthorityTele=65378,StPreviewTele=45120;
        private const uint IwPreviewHash=3043605766u,IwPreviewCueHash=1961581023u,IwAuthorityHash=1763906170u,IwAuthorityCueHash=285182648u,StPreviewHash=4237512322u,StPreviewCueHash=3946706737u;
        private static readonly Vector3 Target=new Vector3(-28f,1.6f,2.6f);
        public static void Run(string root)
        {
            None();Baseline(out string baseline);Pools(out string pools);Stream(out string stream);SameFrames(out string same);Tiers(out string tiers);Allocation(out string alloc);Source(root);
            Console.WriteLine("PASS boss effects realism ("+count+" checks: "+baseline+"; "+pools+"; "+stream+"; "+same+"; "+tiers+"; "+alloc+"; no boss builds nothing; the sources stay presentation only and the authority's pool rules are the mirrored ones)");
        }
        private static uint Mix(uint h,uint x)=>(h^x)*16777619u;
        private static uint Bits(float f)=>(uint)BitConverter.SingleToInt32Bits(f);

        // ---- no boss: nothing ----
        private static void None()
        {
            var none=IronworksBossView.None;var rig=new IronworksBossRig();var fx=new NativeIronworksBossFx();
            for(int f=0;f<60;f++){float t=f/60f;rig.Update(t,1/60f,none,IronworksBossGround.Hall,Vector3.Zero,false,true,Target,double.NaN,double.NaN);fx.Build(rig,none,t,-26,3,-6,1,0,0,0,1,0,1,false);
                Check(fx.Vfx.Count==0&&fx.Melt.VertexCount==0&&fx.Melt.PoolCount==0&&fx.Quads.Count==0,"no Converter: no particle, no molten metal, no quad");}
            var sn=StationBossView.None;var sr=new StationBossRig();var sf=new NativeStationBossFx();
            for(int f=0;f<60;f++){float t=f/60f;sr.Update(t,1/60f,sn,StationBossGround.Station,Vector3.Zero,false,true,new Vector3(3.5f,1.6f,10.5f));sf.Build(sr,sn,t,0,2,10,1,0,0,0,1,0,1,false);
                Check(sf.Vfx.Count==0&&sf.Quads.Count==0,"no 6:47: no particle, no quad");}
        }

        // ---- the telegraphs and cues against the baseline ----
        private static void Baseline(out string summary)
        {
            {
                var rig=new IronworksBossRig();var fx=new NativeIronworksBossFx();var v=IronworksBossView.None;var buf=new IronworksBossCue[NativeIronworksBossCues.QueueSize];double lastArr=double.NaN,lastDeath=double.NaN;
                float[] blowAt=new float[8],blowSize=new float[8];var blowPush=new Vector3[8];int frames=(int)(NativeIronworksBossPreview.Loop*60);long tele=0;uint hash=2166136261u,cueHash=2166136261u;int cues=0;
                for(int f=0;f<frames;f++)
                {
                    float t=f/60f;NativeIronworksBossPreview.View(t,0,ref v,out var vel,out var tg,out var arr,out var death);rig.Update(t,1/60f,v,IronworksBossGround.Hall,vel,false,true,new Vector3(tg.X,1.6f,tg.Y),arr,death);
                    int nb=NativeIronworksBossPreview.Blows(t,0,blowAt,blowPush,blowSize);for(int i=0;i<nb;i++)if(blowAt[i]>t-1/60f&&blowAt[i]<=t)rig.Hit(blowAt[i],blowPush[i],blowSize[i]);
                    fx.Build(rig,v,t,-26,3,-6,1,0,0,0,1,0,1,false);tele+=fx.Telegraphs;hash=Mix(Mix(hash,(uint)fx.Telegraphs),fx.TelegraphHash);
                    float af=(float)(double.IsNaN(lastArr)?-1:lastArr),at=(float)(double.IsNaN(arr)?-1:arr),df=(float)(double.IsNaN(lastDeath)?-1:lastDeath),de=(float)(double.IsNaN(death)?-1:death);
                    int n=NativeIronworksBossCues.Collect(v,rig,fx,t-1/60f,t,af,at,df,de,buf);for(int i=0;i<n;i++){cues++;cueHash=Mix(Mix(Mix(Mix(cueHash,buf[i].Key),Bits(buf[i].At)),Bits(buf[i].At3.X)),Bits(buf[i].At3.Z));}
                    lastArr=arr;lastDeath=death;
                }
                Check(frames==IwPreviewFrames,"the Converter preview's length is the baseline's ("+frames+")");
                Check(tele==IwPreviewTele&&hash==IwPreviewHash,"THE CONVERTER's telegraphs in its DEV preview are the ones before the pass, quad for quad and frame for frame ("+tele+", "+hash+")");
                Check(cues==IwPreviewCues&&cueHash==IwPreviewCueHash,"THE CONVERTER's cues in its DEV preview are the ones before the pass ("+cues+", "+cueHash+")");
            }
            {
                var rig=new IronworksBossRig();var fx=new NativeIronworksBossFx();var buf=new IronworksBossCue[NativeIronworksBossCues.QueueSize];var v=Scenario();
                long tele=0;uint hash=2166136261u,cueHash=2166136261u;int cues=0;
                for(int f=0;f<=660;f++)
                {
                    float t=f/60f;rig.Update(t,f==0?0:1/60f,v,IronworksBossGround.Hall,Vector3.Zero,false,true,Target,double.NaN,double.NaN);
                    fx.Build(rig,v,t,-26,3,-6,1,0,0,0,1,0,1,false);tele+=fx.Telegraphs;hash=Mix(Mix(hash,(uint)fx.Telegraphs),fx.TelegraphHash);
                    int n=NativeIronworksBossCues.Collect(v,rig,fx,t-1/60f,t,-1,-1,-1,-1,buf);for(int i=0;i<n;i++){cues++;cueHash=Mix(Mix(Mix(Mix(cueHash,buf[i].Key),Bits(buf[i].At)),Bits(buf[i].At3.X)),Bits(buf[i].At3.Z));}
                }
                Check(tele==IwAuthorityTele&&hash==IwAuthorityHash,"THE CONVERTER's telegraphs from the authority's hazards (warning rings before each hit, the fading ring on each pool) are the ones before the pass ("+tele+", "+hash+")");
                Check(cues==IwAuthorityCues&&cueHash==IwAuthorityCueHash,"the authority's landing cues are the ones before the pass ("+cues+", "+cueHash+")");
            }
            {
                var rig=new StationBossRig();var fx=new NativeStationBossFx();var cbuf=new StationBossCue[NativeStationBossCues.QueueSize];var v=StationBossView.None;
                int frames=(int)(NativeStationBossPreview.Loop*60);long tele=0;uint hash=2166136261u,cueHash=2166136261u;int cues=0;
                for(int f=0;f<frames;f++)
                {
                    float t=f/60f;NativeStationBossPreview.View(t,0,ref v,out var vel,out var tg);rig.Update(t,1/60f,v,StationBossGround.Station,vel,false,true,new Vector3(tg.X,1.6f,tg.Y));
                    fx.GlassCount=NativeStationBossPreview.Glass(v,fx.GlassXZ);fx.DrawGlass=true;fx.Build(rig,v,t,4,3,14,1,0,0,0,1,0,1,false);tele+=fx.Telegraph;hash=Mix(Mix(hash,(uint)fx.Telegraph),fx.TelegraphHash);
                    int n=NativeStationBossCues.Collect(v,rig,t-1/60f,t,cbuf);for(int i=0;i<n;i++){cues++;cueHash=Mix(Mix(Mix(Mix(cueHash,cbuf[i].Key),Bits(cbuf[i].At)),Bits(cbuf[i].At3.X)),Bits(cbuf[i].At3.Z));}
                }
                Check(frames==StPreviewFrames,"the 6:47 preview's length is the baseline's ("+frames+")");
                Check(tele==StPreviewTele&&hash==StPreviewHash,"THE 6:47's telegraphs (lanes, doors, glass rings, puddles, Terminus, the horns) are the ones before the pass, quad for quad and frame for frame ("+tele+", "+hash+")");
                Check(cues==StPreviewCues&&cueHash==StPreviewCueHash,"THE 6:47's cues are the ones before the pass ("+cues+", "+cueHash+")");
            }
            summary="telegraphs and cues identical to the pre-pass baseline (Converter preview "+IwPreviewTele+" telegraph quads and "+IwPreviewCues+" cues over "+IwPreviewFrames+" frames, its authority scenario "+IwAuthorityTele+" and "+IwAuthorityCues+", the 6:47 preview "+StPreviewTele+" and "+StPreviewCues+")";
        }
        // The Converter's authority scenario: standing at boss point 0 facing east, a pour from 1 s (windup 1.2 s), its pool by the
        // rule (6 m ahead from the strike + 1.6 s, 4 s, 3 m), four drops of a blow, the deslag's three patches, a charge's trail.
        private static IronworksBossView Scenario()
        {
            var v=IronworksBossView.None;v.live=true;v.x=-35;v.z=0;v.yaw=90;v.phase=1;v.maxHp=6000;v.hp=6000;v.spawned=-100;v.action="pour";v.actionAt=1;v.actionYaw=90;v.targetX=-29;v.targetZ=0;v.hazards=Hazards();return v;
        }
        private static NativeZoneBossHazardFrame[] Hazards()=>new[]{
            new NativeZoneBossHazardFrame{id=1,kind="pour",x=-29,y=0,z=0,radius=3,hitAt=3.8f,until=7.8f},
            new NativeZoneBossHazardFrame{id=2,kind="blow",x=-31,y=0,z=4,radius=1,hitAt=3.0f,until=4.0f},new NativeZoneBossHazardFrame{id=3,kind="blow",x=-33,y=0,z=-5,radius=1,hitAt=3.3f,until=4.3f},
            new NativeZoneBossHazardFrame{id=4,kind="blow",x=-38,y=0,z=6,radius=1,hitAt=3.6f,until=4.6f},new NativeZoneBossHazardFrame{id=5,kind="blow",x=-40,y=0,z=-3,radius=1,hitAt=4.1f,until=5.1f},
            new NativeZoneBossHazardFrame{id=6,kind="deslag",x=-43,y=0,z=-2.5f,radius=1.4f,hitAt=5.0f,until=11.0f},new NativeZoneBossHazardFrame{id=7,kind="deslag",x=-43,y=0,z=0,radius=1.4f,hitAt=5.4f,until=11.4f},
            new NativeZoneBossHazardFrame{id=8,kind="deslag",x=-43,y=0,z=2.5f,radius=1.4f,hitAt=5.8f,until=11.8f},
            new NativeZoneBossHazardFrame{id=9,kind="charge",x=-36,y=0,z=5,radius=1.8f,hitAt=1.0f,until=4.0f},new NativeZoneBossHazardFrame{id=10,kind="charge",x=-34,y=0,z=5,radius=1.8f,hitAt=1.2f,until=4.2f},
            new NativeZoneBossHazardFrame{id=11,kind="charge",x=-32,y=0,z=5,radius=1.8f,hitAt=1.4f,until=4.4f},new NativeZoneBossHazardFrame{id=12,kind="charge",x=-30,y=0,z=5,radius=1.8f,hitAt=1.6f,until=4.6f}};
        private static bool Find(NativeMoltenFlow m,int id,out MoltenPoolShape s){for(int i=0;i<m.PoolCount;i++)if(m.Pools[i].Id==id){s=m.Pools[i];return true;}s=default;return false;}

        // ---- every pool on its hazard, at every phase ----
        /// <summary>The farthest the drawn edge is pulled inward (lobes and splash fingers), as a share of the radius.</summary>
        private const float EdgeTolerance=.061f;
        private static void Pools(out string summary)
        {
            var rig=new IronworksBossRig();var fx=new NativeIronworksBossFx();var v=Scenario();int samples=0;float worstIn=0,worstOut=0,coldest=1;
            for(int f=0;f<=60*14;f++)
            {
                float t=f/60f;rig.Update(t,f==0?0:1/60f,v,IronworksBossGround.Hall,Vector3.Zero,false,true,Target,double.NaN,double.NaN);fx.Build(rig,v,t,-26,3,-6,1,0,0,0,1,0,1,false);
                foreach(var h in v.hazards)
                {
                    bool has=Find(fx.Melt,h.id,out var s);string what=h.kind+" "+h.id+" at "+t.ToString("0.000")+" s";
                    if(t<h.hitAt)
                    {
                        if(h.kind!="pour"){Check(!has,what+": no pool before its hit");continue;}
                        if(has)Check(s.Spread<1&&s.EdgeMax<=h.radius*(1+1e-5f)+1e-5f,what+": the pour's puddle spreading under the stream stays inside the radius ("+s.EdgeMax.ToString("0.000")+")");
                        continue;
                    }
                    if(t<=h.until)
                    {
                        Check(has,what+": the pool is drawn from its hit to its until");if(!has)continue;
                        Check(Math.Abs(s.X-h.x)<1e-4f&&Math.Abs(s.Z-h.z)<1e-4f,what+": centred on the hazard");
                        Check(s.Spread==1,what+": at its full radius from the hit");
                        Check(s.EdgeMax<=h.radius*(1+1e-5f)+1e-5f&&s.EdgeMax>=h.radius*(1-1e-4f),what+": its farthest edge on the radius ("+s.EdgeMax.ToString("0.0000")+" of "+h.radius+")");
                        Check(s.EdgeMin>=h.radius*(1-EdgeTolerance),what+": its nearest edge within the lobes' tolerance ("+(1-s.EdgeMin/h.radius).ToString("0.000")+")");
                        Check(s.Heat>=.5f,what+": still hot ("+s.Heat.ToString("0.00")+")");
                        worstIn=Math.Max(worstIn,1-s.EdgeMin/h.radius);worstOut=Math.Max(worstOut,s.EdgeMax/h.radius-1);coldest=Math.Min(coldest,s.Heat);samples++;
                        continue;
                    }
                    float after=t-h.until;
                    if(after>.36f&&has)Check(s.Heat<.02f&&s.EdgeMax<=h.radius*(1+1e-5f),what+": cold once expired ("+s.Heat.ToString("0.000")+")");
                    if(after>NativeIronworksBossFx.SolidifySeconds+1e-3f)Check(!has,what+": gone once solidified");
                }
            }
            Check(samples>2000,"pools sampled ("+samples+")");
            summary=samples+" pool samples over 12 authority hazards (pour 3 m, blow 1 m, deslag 1.4 m, charge 1.8 m): centred to 0.1 mm, full radius from the hit, the farthest edge on the radius (+"+(worstOut*100).ToString("0.000")+"%), the nearest within "+(worstIn*100).ToString("0.0")+"% (tolerance "+(EdgeTolerance*100).ToString("0.0")+"%), hot to the until (at least "+coldest.ToString("0.00")+"), cold .36 s after, gone after "+NativeIronworksBossFx.SolidifySeconds+" s";
        }

        // ---- the pour's stream lands on the authority's point ----
        private static void Stream(out string summary)
        {
            var rig=new IronworksBossRig();var fx=new NativeIronworksBossFx();var v=Scenario();var pool=v.hazards[0];int landed=0;float worst=0;
            for(int f=0;f<=60*6;f++)
            {
                float t=f/60f;rig.Update(t,f==0?0:1/60f,v,IronworksBossGround.Hall,Vector3.Zero,false,true,Target,double.NaN,double.NaN);fx.Build(rig,v,t,-26,3,-6,1,0,0,0,1,0,1,false);
                if(fx.Melt.Streams==0||float.IsNaN(fx.StreamFlight))continue;
                Check(Math.Abs(fx.StreamLand.X-pool.x)<1e-5f&&Math.Abs(fx.StreamLand.Z-pool.z)<1e-5f&&Math.Abs(fx.StreamLand.Y-pool.y)<1e-5f,"the stream aims at the authority's pool ("+t.ToString("0.00")+" s)");
                var end=NativeMoltenFlow.Arc(fx.StreamLip,fx.StreamLaunch,fx.StreamFlight,out _);float miss=Vector3.Distance(end,fx.StreamLand);worst=Math.Max(worst,miss);
                Check(miss<.005f,"the stream's arc ends on the authority's pool point ("+(miss*1000).ToString("0.0")+" mm at "+t.ToString("0.00")+" s)");landed++;
            }
            Check(landed>60,"the stream was drawn ("+landed+" frames)");
            // The preview (no record): the rule's point, 6 m ahead on the locked facing.
            var p=v;p.hazards=null;var rig2=new IronworksBossRig();var fx2=new NativeIronworksBossFx();int ruled=0;
            for(int f=0;f<=60*4;f++){float t=f/60f;rig2.Update(t,f==0?0:1/60f,p,IronworksBossGround.Hall,Vector3.Zero,false,true,Target,double.NaN,double.NaN);fx2.Build(rig2,p,t,-26,3,-6,1,0,0,0,1,0,1,false);
                if(fx2.Melt.Streams==0)continue;var rule=new Vector3(p.x,p.y,p.z)+BossMath.Forward(p.actionYaw*BossMath.Deg)*NativeIronworksBossFx.PourPoolAhead;
                Check(Vector3.Distance(fx2.StreamLand,rule)<1e-4f,"without a record the stream lands on the rule's point");ruled++;}
            Check(ruled>30,"the preview's stream was drawn ("+ruled+")");
            summary="the pour's stream lands on the authority's pool point over "+landed+" frames (arc end within "+(worst*1000).ToString("0.00")+" mm), the preview's on the rule's point";
        }

        // ---- the same frames from the same replicated times ----
        private static uint Hash(NativeIronworksBossFx fx)
        {
            uint h=2166136261u;var m=fx.Melt;for(int i=0;i<m.VertexCount;i++){var q=m.V[i];h=Mix(Mix(Mix(Mix(h,Bits(q.Px)),Bits(q.Py)),Bits(q.Pz)),Bits(q.Bx));}
            for(int i=0;i<fx.Vfx.Count;i++){var it=fx.Vfx.Items[i];h=Mix(Mix(Mix(Mix(Mix(h,(uint)it.Kind),Bits(it.At.X)),Bits(it.At.Y)),Bits(it.At.Z)),Bits(it.Size));}
            return h;
        }
        private static void SameFrames(out string summary)
        {
            var v=Scenario();var r1=new IronworksBossRig();var r2=new IronworksBossRig();var a=new NativeIronworksBossFx();var b=new NativeIronworksBossFx();int frames=0,fresh=0;
            for(int f=0;f<=60*12;f++)
            {
                float t=f/60f;r1.Update(t,f==0?0:1/60f,v,IronworksBossGround.Hall,Vector3.Zero,false,true,Target,double.NaN,double.NaN);r2.Update(t,f==0?0:1/60f,v,IronworksBossGround.Hall,Vector3.Zero,false,true,Target,double.NaN,double.NaN);
                a.Build(r1,v,t,-26,3,-6,1,0,0,0,1,0,1,false);b.Build(r2,v,t,-26,3,-6,1,0,0,0,1,0,1,false);Check(Hash(a)==Hash(b)&&a.Melt.VertexCount==b.Melt.VertexCount&&a.Vfx.Count==b.Vfx.Count,"two builders draw the same molten mesh and particles ("+t.ToString("0.00")+" s)");frames++;
                if(f%30==15)
                {
                    // A late joiner (or the killcam) starting here draws the same live pools.
                    var c=new NativeIronworksBossFx();c.Build(r1,v,t,-26,3,-6,1,0,0,0,1,0,1,false);
                    foreach(var h in v.hazards){if(t<h.hitAt||t>h.until)continue;bool ha=Find(a.Melt,h.id,out var sa),hc=Find(c.Melt,h.id,out var sc);
                        Check(ha&&hc&&sa.EdgeMin==sc.EdgeMin&&sa.EdgeMax==sc.EdgeMax&&sa.Heat==sc.Heat&&sa.Crust==sc.Crust&&sa.X==sc.X&&sa.Z==sc.Z,"a builder starting at "+t.ToString("0.00")+" s draws pool "+h.id+" as one that ran from the start");fresh++;}
                }
            }
            summary="two builders draw the same frames ("+frames+"), a builder joining late draws the same live pools ("+fresh+" comparisons)";
        }

        // ---- the tiers: fewer particles and a lighter mesh, the same telegraphs ----
        private static void Tiers(out string summary)
        {
            long[] parts=new long[3],verts=new long[3];int frames=0;
            var tiers=new (float effects,bool low)[]{(1,false),(.25f,false),(.5f,true)};
            var rigs=new IronworksBossRig[3];var fxs=new NativeIronworksBossFx[3];for(int k=0;k<3;k++){rigs[k]=new IronworksBossRig();fxs[k]=new NativeIronworksBossFx();}
            var v=IronworksBossView.None;float[] busy={20.3f,21.2f,28.0f,29.6f,30.4f,35.6f,65.3f,72.6f,95.5f,109.4f,128.0f};
            for(int f=0;f<=(int)(130*60);f++)
            {
                float t=f/60f;NativeIronworksBossPreview.View(t,0,ref v,out var vel,out var tg,out var arr,out var death);
                bool sample=busy.Any(b=>Math.Abs(b-t)<.5f/60f);
                for(int k=0;k<3;k++){rigs[k].Update(t,1/60f,v,IronworksBossGround.Hall,vel,false,true,new Vector3(tg.X,1.6f,tg.Y),arr,death);if(sample)fxs[k].Build(rigs[k],v,t,-26,3,-6,1,0,0,0,1,0,tiers[k].effects,tiers[k].low);}
                if(!sample)continue;frames++;
                for(int k=0;k<3;k++){parts[k]+=fxs[k].Vfx.Count;verts[k]+=fxs[k].Melt.VertexCount;}
                Check(fxs[1].TelegraphHash==fxs[0].TelegraphHash&&fxs[2].TelegraphHash==fxs[0].TelegraphHash&&fxs[1].Telegraphs==fxs[0].Telegraphs&&fxs[2].Telegraphs==fxs[0].Telegraphs,"the telegraphs are never thinned ("+t.ToString("0.0")+" s)");
            }
            // The authority scenario too (the pools' fumes and sparks thin, the pools themselves stay exact).
            var av=Scenario();var ar=new IronworksBossRig[3];var af=new NativeIronworksBossFx[3];for(int k=0;k<3;k++){ar[k]=new IronworksBossRig();af[k]=new NativeIronworksBossFx();}
            for(int f=0;f<=60*8;f++){float t=f/60f;for(int k=0;k<3;k++){ar[k].Update(t,f==0?0:1/60f,av,IronworksBossGround.Hall,Vector3.Zero,false,true,Target,double.NaN,double.NaN);af[k].Build(ar[k],av,t,-26,3,-6,1,0,0,0,1,0,tiers[k].effects,tiers[k].low);parts[k]+=af[k].Vfx.Count;verts[k]+=af[k].Melt.VertexCount;}
                if(f%20==0)Check(af[2].Melt.PoolCount==af[0].Melt.PoolCount,"the low tier draws every pool");}
            Check(frames==busy.Length,"busy frames sampled");
            Check(parts[1]<parts[0]&&parts[2]<parts[0],"THE CONVERTER: reduced effects and the low tier draw fewer particles ("+parts[0]+" / "+parts[1]+" / "+parts[2]+")");
            Check(verts[2]<verts[0]*.7f&&verts[1]<=verts[0],"THE CONVERTER: the low tier's molten mesh is lighter (the reduced quality throws fewer droplets) ("+verts[0]+" / "+verts[1]+" / "+verts[2]+")");
            // THE 6:47.
            long[] sp=new long[3];var srs=new StationBossRig[3];var sfs=new NativeStationBossFx[3];for(int k=0;k<3;k++){srs[k]=new StationBossRig();sfs[k]=new NativeStationBossFx();}
            var sv=StationBossView.None;float[] sbusy={5.0f,8.4f,26.2f,26.6f,40.0f,49.0f,58.0f,64.8f,71.0f,73.2f,82.7f,102.5f};int sframes=0;
            for(int f=0;f<=(int)(104*60);f++)
            {
                float t=f/60f;NativeStationBossPreview.View(t,0,ref sv,out var vel,out var tg);bool sample=sbusy.Any(b=>Math.Abs(b-t)<.5f/60f);
                for(int k=0;k<3;k++){srs[k].Update(t,1/60f,sv,StationBossGround.Station,vel,false,true,new Vector3(tg.X,1.6f,tg.Y));if(sample){sfs[k].GlassCount=NativeStationBossPreview.Glass(sv,sfs[k].GlassXZ);sfs[k].DrawGlass=true;sfs[k].Build(srs[k],sv,t,4,3,14,1,0,0,0,1,0,tiers[k].effects,tiers[k].low);}}
                if(!sample)continue;sframes++;for(int k=0;k<3;k++)sp[k]+=sfs[k].Vfx.Count;
                Check(sfs[1].TelegraphHash==sfs[0].TelegraphHash&&sfs[2].TelegraphHash==sfs[0].TelegraphHash,"THE 6:47's telegraphs are never thinned ("+t.ToString("0.0")+" s)");
            }
            Check(sp[1]<sp[0]&&sp[2]<sp[0],"THE 6:47: reduced effects and the low tier draw fewer particles ("+sp[0]+" / "+sp[1]+" / "+sp[2]+")");
            summary="particles full / reduced (.25) / low tier: THE CONVERTER "+parts[0]+" / "+parts[1]+" / "+parts[2]+" (molten vertices "+verts[0]+" / "+verts[1]+" / "+verts[2]+"), THE 6:47 "+sp[0]+" / "+sp[1]+" / "+sp[2]+", telegraphs identical";
        }

        // ---- nothing allocates per frame ----
        private static void Allocation(out string summary)
        {
            var rig=new IronworksBossRig();var fx=new NativeIronworksBossFx();var v=Scenario();
            void Frame(int f){float t=f/60f;rig.Update(t,f==0?0:1/60f,v,IronworksBossGround.Hall,Vector3.Zero,false,true,Target,double.NaN,double.NaN);fx.Build(rig,v,t,-26,3,-6,1,0,0,0,1,0,1,false);}
            const int frames=60*13;for(int f=0;f<frames;f++)Frame(f);long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<frames;f++)Frame(f);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"THE CONVERTER's effects from the authority's hazards allocate nothing per frame ("+used+" bytes over "+frames+" frames)");
            var sr=new StationBossRig();var sf=new NativeStationBossFx();var sv=StationBossView.None;
            void SFrame(int f){float t=f/60f;NativeStationBossPreview.View(t,0,ref sv,out var vel,out var tg);sr.Update(t,1/60f,sv,StationBossGround.Station,vel,false,true,new Vector3(tg.X,1.6f,tg.Y));
                sf.GlassCount=NativeStationBossPreview.Glass(sv,sf.GlassXZ);sf.DrawGlass=true;sf.Build(sr,sv,t,4,3,14,1,0,0,0,1,0,1,false);}
            int sframes=(int)(NativeStationBossPreview.Loop*60);for(int f=0;f<sframes;f++)SFrame(f);before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<sframes;f++)SFrame(f);long sused=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(sused==0,"THE 6:47's effects with the preview's glass allocate nothing per frame ("+sused+" bytes over "+sframes+" frames)");
            summary="0 bytes per frame (the Converter's authority scenario over "+frames+" frames, the 6:47's preview with its glass over "+sframes+")";
        }

        // ---- the sources ----
        private static void Source(string root)
        {
            string A(string rel)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",rel));
            var mine=new[]{"Core/NativeBossVfx.cs","Core/NativeMoltenFlow.cs","Core/NativeIronworksBossFx.cs","Core/NativeStationBossFx.cs","Runtime/NativeBossVfxSubmit.cs","Resources/NativeMoltenFlow.shader"};
            foreach(var f in mine)
            {
                string s=A(f);
                foreach(var bad in new[]{"NativeZoneBossAuthority","NativeNetwork","BossHurt","ZoneHazardPilot","bossPools","SpawnPve","Rigidbody","Collider","Physics.","Damage(","NativeZoneCombat","ExpeditionDirector","hp-=","Hp-="})
                    Check(!s.Contains(bad),f+" stays presentation only (no "+bad+")");
            }
            // The authority's pool rules the presentation mirrors in the DEV preview and assumes for the pour's stream (presentation
            // reads them, never the reverse): a change there must update NativeIronworksBossFx's mirror.
            string auth=A("Runtime/NativeZoneBossAuthority.cs");
            Check(auth.Contains("if(z.action==\"pour\")bossPools.Add((BossWorld(0,6),z.hitAt+1.6f,z.hitAt+5.6f,3,0,")&&NativeIronworksBossFx.PourPoolAhead==6&&NativeIronworksBossFx.PourPoolDelay==1.6f&&NativeIronworksBossFx.PourPoolLife==4&&NativeIronworksBossFx.PourPoolRadius==3,
                "the authority's pour pool is the rule the presentation mirrors (6 m ahead, strike + 1.6 s, 4 s, 3 m)");
            Check(auth.Contains("bossPools.Add((BossWorld((i-1)*2.5f,-8),hit,hit+6,1.4f,")&&auth.Contains("z.hitAt+delay,z.hitAt+delay+1,1,16,")&&auth.Contains("bossPools.Add((next,Time,Time+3,1.8f,"),"the deslag, blow and charge pools are the rules the preview mirrors");
            foreach(var f in new[]{"Runtime/NativeZoneBossAuthority.cs","Core/NativeZoneBossRules.cs","Runtime/NativeExpeditionZoneHazards.cs"})
            {
                string s=A(f);foreach(var t in new[]{"NativeBossVfx","NativeMoltenFlow","BossVfxItem","MoltenVertex","NativeBossVfxSubmit"})Check(!s.Contains(t),f+" names no presentation type ("+t+")");
            }
            foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"NativeNetwork*.cs").Concat(Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Core"),"NativeNetwork*.cs")))
            {
                string s=File.ReadAllText(f);foreach(var t in new[]{"NativeBossVfx","NativeMoltenFlow","MoltenVertex"})Check(!s.Contains(t),Path.GetFileName(f)+" names no presentation type ("+t+")");
            }
            string sh=A("Resources/NativeMoltenFlow.shader");Check(sh.Contains("// SHARED BEGIN")&&sh.Contains("// SHARED END")&&sh.Contains("Cull Off")&&File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/NativeMoltenFlow.shader.meta")),"the molten flow shader keeps its shared section (the offline harness) and its meta");
            string build=A("Editor/NativeBuildChecks.cs");Check(build.Contains("\"NativeMoltenFlow\""),"the editor build check compiles the molten flow shader");
        }
    }
}
#endif
