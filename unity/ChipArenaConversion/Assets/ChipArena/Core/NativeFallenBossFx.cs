using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The effects' quads as data: four corners, a tint with alpha, and the shader's mode and strength (Resources/NativeFallenFx.shader). Allocation free once sized.</summary>
    public sealed class FallenFxQuads
    {
        public const byte Plain=0,Line=1,Droplet=2,Column=3,Ink=4,Crack=5,Blade=6,Trail=7,Soft=8,Wake=9;
        public readonly int Max;public int Count;public readonly float[] Corners;public readonly float[] Tint;public readonly float[] Modes;
        public FallenFxQuads(int max){Max=max;Corners=new float[max*12];Tint=new float[max*2];Modes=new float[max*2];}
        public void Clear()=>Count=0;
        /// <summary>a, b along one side (v -1), d, c along the other (v 1); u runs a to b.</summary>
        public void Add(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float alpha,float strength,byte mode,float grey=1)
        {
            if(Count>=Max)return;int o=Count*12;
            Corners[o]=a.X;Corners[o+1]=a.Y;Corners[o+2]=a.Z;Corners[o+3]=b.X;Corners[o+4]=b.Y;Corners[o+5]=b.Z;Corners[o+6]=c.X;Corners[o+7]=c.Y;Corners[o+8]=c.Z;Corners[o+9]=d.X;Corners[o+10]=d.Y;Corners[o+11]=d.Z;
            Tint[Count*2]=grey;Tint[Count*2+1]=alpha;Modes[Count*2]=mode;Modes[Count*2+1]=strength;Count++;
        }
    }
    /// <summary>A prop the effects place: one of the bank's free pieces.</summary>
    public struct FallenFxProp{public string Id;public Vector3 At;public Quaternion Turn;public float Scale,Glow;}

    /// <summary>
    /// NOCTIEL's water and effects for a frame, from the script's moves (or later Astra's record): the rings and waves the water's shader runs, its pools and dim, the quads over it, the adds' props. A pure function of the frame and the eye.
    /// Every effect is drawn in black and white. Rings are footsteps and small hits; waves cross the disc.
    /// </summary>
    public sealed class NativeFallenBossFx
    {
        public const int MaxRings=48,MaxWaves=10,MaxPools=6,MaxProps=24;
        public readonly float[] Rings=new float[MaxRings*4],Waves=new float[MaxWaves*4],Pools=new float[MaxPools*4];public int RingCount,WaveCount,PoolCount;
        public float Dim,Strike,Shake,White,KeyBoost;public int Motes;
        public readonly FallenFxQuads Quads=new FallenFxQuads(6000);
        /// <summary>This frame's attack warnings (Core NativeTelegraph, drawn by Runtime NativeTelegraphPresentation.cs): each move's
        /// own hit geometry on the authority's times, in the stage's frame.</summary>
        public readonly NativeTelegraphList Telegraphs=new NativeTelegraphList(512);public readonly FallenFxProp[] Props=new FallenFxProp[MaxProps];public int PropCount;
        private Vector3 eye,right,up;private bool live;
        private static float C(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=C(x);return x*x*(3-2*x);}
        private static float EaseOut(float x){x=C(x);return 1-(1-x)*(1-x)*(1-x);}
        private static float Hash(float n){double s=Math.Sin(n*127.1)*43758.5453;return (float)(s-Math.Floor(s));}
        private static Vector3 YawDir(float deg){double a=deg*Math.PI/180;return new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));}
        public void Ring(float x,float z,float radius,float strength){if(RingCount>=MaxRings||strength<=.004f)return;int o=RingCount++*4;Rings[o]=x;Rings[o+1]=z;Rings[o+2]=radius;Rings[o+3]=strength;}
        // A step's ring, spreading quickly then slowing, fading out.
        public void Step(float x,float z,float age,float strength,float life=2.6f){if(age<0||age>life)return;float r=5.2f*(1-(float)Math.Exp(-age*1.05f))/1.05f*1.6f;Ring(x,z,r,strength*(1-age/life)*(1-age/life));}
        public void Wave(float x,float z,float radius,float strength){if(WaveCount>=MaxWaves||strength<=.004f)return;int o=WaveCount++*4;Waves[o]=x;Waves[o+1]=z;Waves[o+2]=radius;Waves[o+3]=strength;}
        // A wave crossing the whole disc at `speed` from where `age` began.
        public void Cross(Vector3 at,float age,float speed,float strength,float life=4.4f){if(age<0||age>life)return;Wave(at.X,at.Z,age*speed,strength*(1-C(age/life)));}
        private void Pool(float x,float z,float r,float amount){if(PoolCount>=MaxPools)return;int o=PoolCount++*4;Pools[o]=x;Pools[o+1]=z;Pools[o+2]=r;Pools[o+3]=amount;}
        private void Prop(string id,Vector3 at,Quaternion turn,float scale,float glow){if(PropCount>=MaxProps)return;Props[PropCount++]=new FallenFxProp{Id=id,At=at,Turn=turn,Scale=scale,Glow=glow};}

        // ---- shapes ----
        private void FloorLine(Vector3 a,Vector3 b,float width,float alpha,float strength,byte mode=FallenFxQuads.Line)
        {
            var d=b-a;d.Y=0;float l=d.Length();if(l<.001f)return;var s=new Vector3(d.Z,0,-d.X)/l*width;var lift=new Vector3(0,.02f,0);
            Quads.Add(a-s+lift,b-s+lift,b+s+lift,a+s+lift,alpha,strength,mode);
        }
        private void Beam(Vector3 a,Vector3 b,float width,float alpha,float strength,byte mode=FallenFxQuads.Line)
        {
            var d=b-a;var view=eye-(a+b)*.5f;var s=Vector3.Cross(d,view);float l=s.Length();if(l<1e-4f)return;s=s/l*width;Quads.Add(a-s,b-s,b+s,a+s,alpha,strength,mode);
        }
        private void Sheet(Vector3 a,Vector3 b,float bottom,float top,float alpha,float strength,byte mode,float grey=1)
        {
            var lo=new Vector3(0,bottom,0);var hi=new Vector3(0,top,0);Quads.Add(a+lo,b+lo,b+hi,a+hi,alpha,strength,mode,grey);
        }
        /// <summary>A mote of light off the cloth (Runtime sheds them from the simulated hems): a soft point facing the eye, on the stage's frame.</summary>
        public void ClothMote(Vector3 at,float size,float alpha){if(alpha>.01f)Spot(at,size,alpha,.6f);}
        private void Spot(Vector3 at,float size,float alpha,float strength,byte mode=FallenFxQuads.Droplet,float grey=1)
        {
            var r=right*size;var u=up*size;Quads.Add(at-r-u,at+r-u,at+r+u,at-r+u,alpha,strength,mode,grey);
        }
        private void Circle(Vector3 c,float radius,float width,float alpha,float strength,int segs=48)
        {
            var lift=new Vector3(0,.025f,0);float pa=0;var ca=c+new Vector3(radius,0,0);
            for(int i=1;i<=segs;i++)
            {
                double t=i*2*Math.PI/segs;var q=c+new Vector3(radius*(float)Math.Cos(t),0,radius*(float)Math.Sin(t));var s0=Vector3.Normalize(ca-c)*width;var s1=Vector3.Normalize(q-c)*width;
                Quads.Add(ca-s0+lift,q-s1+lift,q+s1+lift,ca+s0+lift,alpha,strength,FallenFxQuads.Line);ca=q;pa=(float)t;
            }
        }
        /// <summary>An arc of the floor by yaw (degrees) and span.</summary>
        private void FloorArc(Vector3 c,float radius,float yaw,float span,float width,float alpha,float strength,int segs=20)
        {
            var lift=new Vector3(0,.025f,0);for(int i=0;i<segs;i++)
            {
                float s0=i/(float)segs,s1=(i+1f)/segs,y0=yaw+(s0-.5f)*span,y1=yaw+(s1-.5f)*span;var a=c+YawDir(y0)*radius;var b=c+YawDir(y1)*radius;
                float t0=1-Math.Abs(s0*2-1),t1=1-Math.Abs(s1*2-1);var n0=YawDir(y0)*(width*t0);var n1=YawDir(y1)*(width*t1);
                Quads.Add(a-n0+lift,b-n1+lift,b+n1+lift,a+n0+lift,alpha,strength,FallenFxQuads.Line);
            }
        }
        /// <summary>A crown of water at `at`: droplets thrown up and out, their arcs falling back.</summary>
        private void Burst(Vector3 at,float age,int seed,int count,float speed,float rise,float size,float strength,float spread=0)
        {
            if(age<0||age>1.6f)return;
            for(int i=0;i<count;i++)
            {
                float h1=Hash(seed+i*1.37f),h2=Hash(seed+i*2.11f+7),h3=Hash(seed+i*3.77f+13);double a=h1*2*Math.PI;float v=speed*(.35f+.65f*h2),u=rise*(.4f+.6f*h3),t=age*(.8f+.4f*h3);
                float y=u*t-7.5f*t*t;if(y<0)continue;var at2=at+new Vector3((float)Math.Cos(a)*(v*t+spread),y,(float)Math.Sin(a)*(v*t+spread));
                Spot(at2,size*(.5f+h2),1-C(age/1.3f),strength);
            }
        }
        // ---- the build ----
        public void Build(FallenBossFrame f,Vector3 eyePos,Vector3 eyeRight,Vector3 eyeUp,NativeFallenSong song,bool reduced)
        {
            live=f.Live!=null;eye=eyePos;right=eyeRight;up=eyeUp;RingCount=WaveCount=PoolCount=PropCount=0;Quads.Clear();Telegraphs.Clear();Dim=1;Strike=0;Shake=0;White=0;KeyBoost=0;float p=f.P;
            Motes=f.Phase==1?260:f.Phase==2?420:f.Phase==3?620:900;if(f.Kind==NativeFallenBossPreview.KindCutscene)Motes=p<NativeFallenBossPreview.RiseAt?60:180;
            if(f.Kind==NativeFallenBossPreview.KindChips)return;
            // The pilots' steps (stand-ins for now: the rules' pilots step the same way).
            for(int i=0;i<(f.Live==null?NativeFallenPilots.Count:0);i++)
            {
                if(!NativeFallenPilots.Shown(i,p))continue;bool walk=NativeFallenPilots.Walking(p);float every=walk?NativeFallenPilots.WalkStep:NativeFallenPilots.StepEvery;float now=(float)Math.Floor(p/every)*every;
                for(int k=0;k<5;k++){float t=now-k*every;if(!NativeFallenPilots.Shown(i,t)||walk!=NativeFallenPilots.Walking(t))break;var q=NativeFallenPilots.At(i,t);float side=(k+(int)(now/every))%2==0?.22f:-.22f;Step(q.X+side,q.Z,p-t,walk?.5f:.26f,1.9f);}
            }
            // The song's slams no longer run across the water (the owner, 2026-11-08: "not just random ripples"): the void flashes with them (Runtime NativeFallenVoid.cs).
            if(f.Kind==NativeFallenBossPreview.KindCutscene)Cutscene(f);
            if(f.Kind==NativeFallenBossPreview.KindDeath)Death(f);
            foreach(var a in f.Actions??NativeFallenBossPreview.Acts){if(p<a.At-.2f||p>a.End+3)continue;Move(a,f,p);Warn(a,f,p);}
            if(f.Live==null){GlideWake(p);AddsOf(f,p);}else{if(NativeFallenRules.Chip(f.Live,"black-water"))Dim*=.75f;foreach(var h in f.Live.hush){float k=C((p-h.hushAt)/NativeFallenRules.HushSeconds(f.Live,f.Phase,(float)Math.Sqrt(h.x*h.x+h.z*h.z)));if(k>=.4f&&k<1){var s=new NativeTelegraph.Spec{Shape=NativeTelegraph.Circle,Style=NativeTelegraph.Fallen,Fill=NativeTelegraph.FromEdge,At=new Vector3(h.x,0,h.z),A=3,Fill01=(k-.4f)/.6f,After=-1,Strength=C((k-.4f)/.1f),Seed=h.id};s.Lock=k>.9f?(k-.9f)/.1f:0;Telegraphs.Add(s);}}if(f.LiveAdds!=null)foreach(var a in f.LiveAdds)Prop(a.Id,a.At,a.Turn,a.Scale,a.Glow);}
        }
        private void GlideWake(float p)
        {
            var segs=NativeFallenBossPreview.Segs;int at=Array.FindIndex(segs,s=>s.T1>p-2.2f);if(at<0)return;
            for(int i=at;i<segs.Length&&segs[i].T0<=p;i++)
            {
                if(segs[i].Clip!="Armed-Dash-Forward")continue;float len=segs[i].T1-segs[i].T0;
                for(int k=0;k<5;k++){float t=segs[i].T0+len*(k+.5f)/5;if(t>p)break;var q=Vector3.Lerp(segs[i].A,segs[i].B,(k+.5f)/5);Step(q.X,q.Z,p-t,.38f,1.6f);if(p-t<.6f)Burst(q,p-t,k*7+(int)(segs[i].T0*3),5,3,4,.03f,2.2f);}
            }
        }
        private void Cutscene(FallenBossFrame f)
        {
            float p=f.P;
            // The one feather's touch.
            float touch=p-NativeFallenBossPreview.FeatherLand;if(touch>=0&&touch<9)Step(.25f,-50,touch*.7f,1.2f,6.5f);
            if(touch>=0&&touch<1)Burst(new Vector3(.25f,0,-50),touch,5,8,.35f,.6f,.012f,2);
            // Each pair of wings throws a slow ring across the water.
            for(int w=0;w<3;w++)Cross(Vector3.Zero,p-NativeFallenBossPreview.WingAt[w]-.2f,10,1.1f-w*.12f,7);
            // The sword drawn: water running up off the blade.
            float pull=p-NativeFallenBossPreview.PullAt;
            if(pull>=0&&pull<4)
            {
                var root=NativeFallenSongPlant.At(Vector3.Zero,180);Cross(root,pull,8,.9f,3.4f);Step(root.X,root.Z,pull,1.4f);
                for(int i=0;i<70;i++)
                {
                    float h1=Hash(i*1.31f),h2=Hash(i*2.71f+3),rate=.9f+h1*1.9f,y=(pull*rate+h2*2.2f)%5.2f;float o=(1-y/5.2f)*(.04f+.1f*h2);float fade=C(y/.3f)*(1-C((y-3.6f)/1.6f))*(1-C((pull-3.2f)/.8f));
                    Spot(root+new Vector3((float)Math.Sin(i*1.9f)*o,y,(float)Math.Cos(i*1.9f)*o),.018f+.02f*h1,fade,2.2f);
                }
            }
            // The cut at the lens, on the drop.
            float cut=p-(NativeFallenPilots.CutsceneEnd-.02f);Cross(Vector3.Zero,cut,44,1.6f,3);
        }
        private void Death(FallenBossFrame f)
        {
            var sec=NativeFallenBossPreview.Sections[f.Section];float a=f.P-sec.From,hit=a-NativeFallenBossPreview.DeathHit;
            // Light leaking from it, rising in flakes.
            if(hit<0){int n=(int)(18+Smooth((a-6)/18)*110);for(int i=0;i<n;i++){float h1=Hash(i*1.13f),h2=Hash(i*3.31f+9),y=(a*(.5f+.7f*h1)+h2*7)%7;float o=.2f+.5f*h2+y*.1f;double t=i*2.399+a*.25f;Spot(f.Boss+new Vector3((float)Math.Cos(t)*o,y+.4f,(float)Math.Sin(t)*o),.02f+.03f*h1,C(y/.5f)*(1-C(y/7)),2.5f);}}
            else
            {
                // The song's hit: the water goes white and it's gone.
                White=(float)Math.Exp(-hit*2.2f);Strike=(float)Math.Exp(-hit*1.6f);Shake=(float)Math.Exp(-hit*3)*.9f;
                Cross(f.Boss,hit,30,2,5);Cross(f.Boss,hit-.35f,22,1.4f,5);Burst(f.Boss+new Vector3(0,.3f,0),hit*.7f,91,180,9,11,.05f,3);
                Dim=1-Smooth((hit-9)/4);
                // One last feather, on the water by the sword.
                float land=hit-6;if(land>=-4&&land<8){Prop("feather-one",new Vector3(.4f+.2f*(float)Math.Sin(land*.8f),Math.Max(0,-land*.9f),f.Boss.Z-.8f)+new Vector3(f.Boss.X,0,0),Quaternion.CreateFromAxisAngle(Vector3.UnitY,land*.6f),1.5f,1.4f);if(land>=0)Step(f.Boss.X+.4f,f.Boss.Z-.8f,land*.5f,1,3.8f);}
            }
        }
        private void Move(NativeFallenBossPreview.Act a,FallenBossFrame f,float p)
        {
            float reach=NativeFallenRules.Chip(f.Live,"long-reach")?1.2f:1;float wind=p-a.At,hit=p-a.Hit,span=Math.Max(.01f,a.Hit-a.At);float up01=C(wind/span);var from=a.From;var dir=YawDir(a.Aim);
            switch(a.Id)
            {
                case "cantor-beam":case "throne-beam":Beam(a.From,a.To,a.Id=="cantor-beam"?.07f:.16f,.65f,2.2f);break;
                case "cleave":
                    if(hit>=0)
                    {
                        // The swing's fan on the water, and water thrown along it.
                        float k=1-C(hit/.42f);var c=new Vector3(f.Boss.X,0,f.Boss.Z);FloorArc(c,7.2f*reach+hit*5,a.Aim,140,.26f,k,2.2f,28);
                        Step(c.X+dir.X*6,c.Z+dir.Z*6,hit,1.2f);if(hit<1)for(int i=0;i<5;i++){float y=a.Aim+(i-2)*26;Burst(c+YawDir(y)*7,hit-i*.02f,a.Seed+i*7,reducedCount(12),4,5.5f,.035f,2.4f);}
                    }
                    break;
                case "vow":
                {
                    var end=a.To;var lane=end-from;float len=lane.Length();
                    if(hit<0)
                    {
                        // The lane's warning is a telegraph (Warn); the water only starts to part ahead.
                        var tip=from+lane*EaseOut(up01*1.4f);FloorLine(from,tip,.06f,up01*.5f,1.4f);
                    }
                    else if(hit<1.1f)
                    {
                        float k=1-C(hit/.9f);FloorLine(from,end,.9f,k,2.2f);var sd=new Vector3(lane.Z,0,-lane.X)/Math.Max(.01f,len)*(1.5f+hit*2.6f);FloorLine(from+sd,end+sd,.08f,k,1.6f);FloorLine(from-sd,end-sd,.08f,k,1.6f);
                        for(int i=0;i<4;i++){float u=(i+.5f)/4;var q=Vector3.Lerp(from,end,u);if(hit>u*.25f)Step(q.X,q.Z,hit-u*.25f,1.1f);if(hit<1.2f)Burst(q,hit-u*.25f,a.Seed+i*9,reducedCount(14),4,8,.04f,2.2f);}
                        Cross(end,hit-.2f,14,.8f,2);
                    }
                    break;
                }
                case "severance":
                    if(hit<0)
                    {
                        // The ring at its feet flattens out to the aim (the fan's warning is a telegraph: Warn).
                        FloorArc(from,3+up01*2,a.Aim,120*up01,.16f,up01*.6f,1.6f);
                    }
                    else
                    {
                        float r=34*hit+3;if(r<(live?165:90))Crescent(from,r,a.Aim,hit,a.Seed);
                    }
                    break;
                case "descent":
                {
                    var c=a.To;float toGo=-hit;
                    if(hit<0)
                    {
                        // A white ring closing on them, and the line of its fall.
                        // The ring's warning is a telegraph (Warn); the line of its fall comes down at the end.
                        if(toGo<.3f)Beam(c,c+new Vector3(0,60,0),.09f+.12f*(1-toGo/.3f),1-toGo/.3f,3);
                    }
                    else if(hit<2.2f)
                    {
                        Cross(c,hit,30,1.2f,2.2f);Cross(c,hit-.08f,20,.8f,2.2f);Step(c.X,c.Z,hit,2);Burst(c,hit,a.Seed,reducedCount(170),14,14,.05f,2.8f);
                        float k=1-C(hit/.55f);Circle(c,9+hit*5,.5f*k+.04f,k,2.4f);Shake=Math.Max(Shake,k*.7f);
                    }
                    break;
                }
                case "rain":Rain(a,p,hit,up01);break;
                case "verdict":Crack(a,p,hit,up01,a.Aim,1);break;
                case "last":Crack(a,p,hit,up01,a.Aim,1);Crack(a,p,hit,up01,a.Aim+60,2);Crack(a,p,hit,up01,a.Aim-60,3);break;
                case "sweep":
                    if(hit>=0&&hit<1.4f)
                    {
                        // Both bare wings round the full circle: a blade of air with a fading wedge behind each.
                        float turn=EaseOut(hit/.42f)*360,k=1-C((hit-.3f)/.9f);
                        for(int side=0;side<2;side++)for(int w=0;w<14;w++)
                        {
                            float y0=a.Aim+side*180+turn-w*7,y1=y0-7,alpha=k*(1-w/14f)*.5f;var a0=YawDir(y0);var a1=YawDir(y1);var lift=new Vector3(0,2.6f,0);
                            Quads.Add(from+a0*1.2f+lift,from+a1*1.2f+lift,from+a1*(28*reach)+lift,from+a0*(28*reach)+lift,alpha,1,FallenFxQuads.Wake);if(w==0){Beam(from+a0*1.2f+lift,from+a0*(28*reach)+lift,.3f,k,2.8f);FloorLine(from+a0*1.2f,from+a0*(28*reach),.5f,k,1.6f);}
                        }
                        Cross(from,hit-.2f,30,.9f,2);if(hit>.3f)Circle(from,28*reach,.3f*k,k,2.2f,64);
                        Burst(from+YawDir(a.Aim+turn)*26,hit,a.Seed,reducedCount(30),5,5,.04f,2);
                    }
                    break;
                case "eclipse":Eclipse(a,f,p,hit,up01);break;
                case "fall":if(f.Live==null)FallOfLight(a,p,hit,up01);else LiveFall(a,p,hit,up01);break;
                case "choir":case "penance":case "thrones":
                    if(hit<0&&a.Id!="thrones"){Circle(from,3+up01*2,.1f,up01,2);}
                    if(hit>=0&&hit<1){Cross(from,hit,20,.9f,1.6f);}
                    break;
            }
        }
        private int reducedCount(int n)=>n;
        // Severance: a crescent of black and white crossing the map, its edges lit, water thrown along its foot.
        private void Crescent(Vector3 c,float r,float yaw,float age,int seed)
        {
            const int n=36;const float span=112,h=4.4f;float prevT=0;var prev=c+YawDir(yaw-span/2)*r;float fade=1-C((r-68)/16);var prevWay=YawDir(yaw-span/2);
            for(int i=1;i<=n;i++)
            {
                float s=i/(float)n,y=yaw+(s-.5f)*span;var q=c+YawDir(y)*r;float tp=1-(float)Math.Pow(Math.Abs(s*2-1),2.6f);float prevS=(i-1)/(float)n;float tq=1-(float)Math.Pow(Math.Abs(prevS*2-1),2.6f);
                var lo0=new Vector3(0,.15f,0);var lo1=new Vector3(0,.15f,0);var hi0=new Vector3(0,.15f+h*tq,0);var hi1=new Vector3(0,.15f+h*tp,0);
                Quads.Add(prev+lo0,q+lo1,q+hi1,prev+hi0,fade,2.4f,FallenFxQuads.Ink,0);
                var way=YawDir(y);float reach=Math.Min(r,9);var wake=new Vector3(0,.03f,0);float wk=fade*(tp+tq)*.5f;Quads.Add(prev-prevWay*reach+wake,q-way*reach+wake,q+wake,prev+wake,wk*.7f,1.1f,FallenFxQuads.Wake);
                prevWay=way;
                if(i%3==0)Burst(q,(age*5+Hash(i))%.5f,seed+i,7,3,7,.05f,2.6f);
                prev=q;prevT=tp;
            }
            FloorArc(c,r,yaw,span,.4f,fade,2.4f,n);
            Step(c.X+YawDir(yaw).X*r,c.Z+YawDir(yaw).Z*r,.02f,1.6f);
        }
        // ---- the warnings: each move's own hit geometry (NativeFallenAuthority.FallenAttack) on its times ----
        /// <summary>Cleave's fan within 8 m (wider with Long Reach) in front by a 0.25 dot.</summary>
        public static readonly float CleaveHalf=(float)(Math.Acos(.25)*180/Math.PI);
        private static float YawOf(Vector3 d)=>d.X*d.X+d.Z*d.Z<1e-8f?0:(float)(Math.Atan2(d.X,d.Z)*180/Math.PI);
        private void Warn(NativeFallenBossPreview.Act a,FallenBossFrame f,float p)
        {
            float reach=NativeFallenRules.Chip(f.Live,"long-reach")?1.2f:1,half=live?168:84;
            NativeTelegraph.Spec W(byte shape,byte style,byte fill,Vector3 at,float yaw,float A,float B=0,float C=0,int seed=0)=>new NativeTelegraph.Spec{Shape=shape,Style=style,Fill=fill,At=at,Yaw=yaw,A=A,B=B,C=C,Seed=a.Seed+seed};
            void Add(NativeTelegraph.Spec s,float start,float hit,float liveFor=0){if(NativeTelegraph.Timed(ref s,start,hit,p,liveFor))Telegraphs.Add(s);}
            switch(a.Id)
            {
                case "cleave":Add(W(NativeTelegraph.Cone,NativeTelegraph.Fallen,NativeTelegraph.Along,a.From,a.Aim,8*reach,CleaveHalf),a.At,a.Hit,.8f);break;
                // Vow hurts within 3 m of the body as it dashes: the lane runs 3 m past either end.
                case "vow":{var d=a.To-a.From;d.Y=0;Add(W(NativeTelegraph.Lane,NativeTelegraph.Fallen,NativeTelegraph.Along,a.From,YawOf(d),d.Length()+3,3,3),a.At,a.Hit,.5f);break;}
                case "severance":
                {
                    // The fan the waves cross (a 0.5 dot), then each wave's front as it travels (34 m/s from 3 m, 1.2 m either side).
                    Add(W(NativeTelegraph.Cone,NativeTelegraph.Fallen,NativeTelegraph.Along,a.From,a.Aim,live?165:90,60),a.At,a.Hit);
                    for(int i=0;i<(NativeFallenRules.Chip(f.Live,"second-wing")?2:1);i++)
                    {
                        float age=p-a.Hit-i*.6f;if(age<0||34*age+3>(live?165:90))continue;
                        var s=W(NativeTelegraph.Wave,NativeTelegraph.Fallen,NativeTelegraph.Along,a.From,a.Aim,34*age+3,1.2f,60,i);s.Fill01=1;s.Live=1;s.After=-1;s.Strength=1;Telegraphs.Add(s);
                    }
                    break;
                }
                case "descent":Add(W(NativeTelegraph.Circle,NativeTelegraph.Fallen,NativeTelegraph.FromEdge,a.To,a.Aim,9),a.At,a.Hit);break;
                case "rain":
                    for(int lane=0;lane<8;lane++)
                    {
                        float ang=a.Seed*37+lane*45+(lane%2)*19;var d=YawDir(ang);var n=new Vector3(d.Z,0,-d.X);
                        Add(W(NativeTelegraph.Lane,NativeTelegraph.Fallen,NativeTelegraph.FromEdge,a.From*.5f+n*((lane-3.5f)*7),ang,live?168:60,1.25f,live?168:60,lane),a.At,a.Hit,2.4f);
                    }
                    break;
                case "verdict":Add(W(NativeTelegraph.Lane,NativeTelegraph.FallenKill,NativeTelegraph.FromEdge,a.To,a.Aim,half,1.5f,half),a.At,a.Hit);break;
                case "last":for(int i=0;i<3;i++)Add(W(NativeTelegraph.Lane,NativeTelegraph.FallenKill,NativeTelegraph.FromEdge,a.To,a.Aim+(i==1?60:i==2?-60:0),half,1.5f,half,i),a.At,a.Hit);break;
                case "sweep":Add(W(NativeTelegraph.Circle,NativeTelegraph.Fallen,NativeTelegraph.FromCentre,a.From,a.Aim,28*reach),a.At,a.Hit,.5f);break;
                case "eclipse":
                    // Outside the pools it kills: the pools are what the warning shows, the dark rolls in on the rest.
                    if(f.Live!=null){int i=0;foreach(var pool in f.Live.pools)Add(W(NativeTelegraph.Circle,NativeTelegraph.Safe,NativeTelegraph.FromEdge,new Vector3(pool.x,0,pool.z),0,6,0,0,i++),a.At,a.Hit);}
                    else for(int i=0;i<NativeFallenPilots.Count;i++){var at=NativeFallenPilots.At(i,a.Hit);float o=Hash(a.Seed+i)*6.28f;Add(W(NativeTelegraph.Circle,NativeTelegraph.Safe,NativeTelegraph.FromEdge,new Vector3(at.X+(float)Math.Cos(o)*5,0,at.Z+(float)Math.Sin(o)*5),0,6,0,0,i),a.At,a.Hit);}
                    break;
                case "fall":
                    if(f.Live!=null)
                        for(int x=-10;x<=10;x++)for(int z=-10;z<=10;z++)
                            if(NativeFallenRules.Column(x,z,a.Seed))Add(W(NativeTelegraph.Square,NativeTelegraph.FallenKill,NativeTelegraph.FromEdge,new Vector3(x*8+4,0,z*8+4),0,3.68f,0,0,x*31+z),a.At,a.Hit);
                    break;
            }
        }
        private void Rain(NativeFallenBossPreview.Act a,float p,float hit,float up01)
        {
            for(int lane=0;lane<8;lane++)
            {
                float ang=a.Seed*37+lane*45+(lane%2)*19,off=(lane-3.5f)*7;var d=YawDir(ang);var n=new Vector3(d.Z,0,-d.X);var c=a.From*.5f+n*off;float half=live?168:60;var s=c-d*half;var e=c+d*half;
                // The lanes' warnings are telegraphs (Warn), live while the blades fall.
                if(hit<0)continue;
                // Blades falling along the lane, one after another.
                for(int k=0;k<14;k++)
                {
                    float t=hit-k*.17f-lane*.03f;if(t<0||t>1.4f)continue;var q=c+d*((k-6.5f)*8);const float fall=.42f;
                    if(t<fall){float y=(1-t/fall)*36;Beam(q+new Vector3(0,y,0),q+new Vector3(0,y+6.5f,0),.42f,1,2.6f,FallenFxQuads.Blade);}
                    else{float g=t-fall;Step(q.X,q.Z,g,.9f,1);if(g<.8f)Burst(q,g,a.Seed+lane*31+k,6,3,5,.035f,2.4f);}
                }
            }
        }
        // A crack of white through the whole disc, brightening, then the map splits along it. `which` keeps each line's own jag.
        private void Crack(NativeFallenBossPreview.Act a,float p,float hit,float up01,float yaw,int which)
        {
            var through=a.To;var d=YawDir(yaw);var n=new Vector3(d.Z,0,-d.X);const int segs=44;float half=live?168:84;
            float show=hit<0?C(up01*1.2f):1;float grow=hit<0?EaseOut(up01*1.6f):1;float w=hit<0?.1f+1.0f*up01*up01*up01:(1.5f*(1-C(hit/.5f))+.14f*(1-C(hit/1.9f)));float k=hit<0?(.45f+.55f*up01*up01):1-C((hit-1)/1);
            if(k<=0)return;
            Vector3 Point(int i){float u=i/(float)segs*2-1;float j=(Hash(i*1.7f+a.Seed*3+which*11)-.5f)*(live||i==0||i==segs?0:3.2f);return through+d*(u*half)+n*j;}
            for(int i=0;i<segs;i++)
            {
                float u0=Math.Abs(i/(float)segs*2-1),u1=Math.Abs((i+1f)/segs*2-1);if(Math.Min(u0,u1)>grow)continue;var q0=Point(i);var q1=Point(i+1);
                FloorLine(q0,q1,w,k,3.2f,FallenFxQuads.Crack);FloorLine(q0,q1,w*3.4f,k*.28f,1.1f,FallenFxQuads.Line);
                if(hit>=0&&hit<.75f){float t=1-C(hit/.7f);Sheet(q0,q1,0,12*t*t+.6f,t*.32f,.9f,FallenFxQuads.Column);}
                if(hit>=0&&hit<1.3f&&i%2==0)Burst(q0,hit-Hash(i)*.08f,a.Seed+i*3+which*17,6,4,10,.05f,2.8f);
            }
            if(hit>=0){Strike=Math.Max(Strike,.22f*(float)Math.Exp(-hit*9));Shake=Math.Max(Shake,(float)Math.Exp(-hit*5));if(which==1)Cross(through,hit,44,1.7f,2.4f);}
        }
        private void Eclipse(NativeFallenBossPreview.Act a,FallenBossFrame f,float p,float hit,float up01)
        {
            float dark=hit<0?up01:1-C((hit-.9f)/.9f);Dim=Math.Min(Dim,1-.96f*dark);KeyBoost=-dark*.86f;
            if(f.Live!=null){foreach(var pool in f.Live.pools)Pool(pool.x,pool.z,6,dark);}
            for(int i=0;i<(f.Live==null?NativeFallenPilots.Count:0);i++)
            {
                var at=NativeFallenPilots.At(i,a.Hit);float o=Hash(a.Seed+i)*6.28f;Pool(at.X+(float)Math.Cos(o)*5,at.Z+(float)Math.Sin(o)*5,6,dark);
            }
            if(hit>=0&&hit<.9f){Strike=Math.Max(Strike,.6f*(1-C(hit/.7f)));Shake=Math.Max(Shake,1-C(hit/.5f));Cross(a.From,hit,40,1.6f,1.6f);}
        }
        // Columns of white falling on the song's bars, a square lit on the water a bar ahead, a route left free.
        private void LiveFall(NativeFallenBossPreview.Act a,float p,float hit,float up01)
        {
            for(int x=-10;x<=10;x++)for(int z=-10;z<=10;z++)
            {
                if(!NativeFallenRules.Column(x,z,a.Seed))continue;var c=new Vector3(x*8+4,0,z*8+4);var u=new Vector3(3.68f,.03f,0);var v=new Vector3(0,.03f,3.68f);
                if(hit>=0&&hit<1){float k=1-C(hit);Sheet(c-u-v,c+u-v,0,60,k,.9f,FallenFxQuads.Column);Sheet(c-u+v,c+u+v,0,60,k,.9f,FallenFxQuads.Column);}
            }
        }
        private void FallOfLight(NativeFallenBossPreview.Act a,float p,float hit,float up01)
        {
            float bar=NativeFallenSong.Bar;int now=(int)Math.Floor(hit/bar);const float cell=8;
            for(int b=Math.Max(-1,now-1);b<=now+1;b++)
            {
                float fall=a.Hit+b*bar;float t=p-fall;if(fall>a.End+.1f)continue;
                int route=(int)(Hash(a.Seed+b*2.3f)*9);
                for(int cx=-5;cx<=5;cx++)for(int cz=-5;cz<=5;cz++)
                {
                    if(cx*cx+cz*cz>26)continue;if(cx==route-4||(cz==((route*3)%7)-3&&cx>0))continue;if(Hash(cx*13.1f+cz*7.3f+b*3.7f+a.Seed)<.58f)continue;
                    var c=new Vector3(cx*cell+cell/2,0,cz*cell+cell/2);var s=new Vector3(cell*.46f,.03f,0);var q=new Vector3(0,.03f,cell*.46f);
                    if(t<0&&t>-bar){var w=new NativeTelegraph.Spec{Shape=NativeTelegraph.Square,Style=NativeTelegraph.FallenKill,Fill=NativeTelegraph.FromEdge,At=c,A=cell*.46f,Seed=cx*31+cz};if(NativeTelegraph.Timed(ref w,fall-bar,fall,p))Telegraphs.Add(w);}
                    else if(t>=0&&t<1.1f)
                    {
                        float k=1-C((t-.2f)/.9f);float top=Math.Min(70,t/.14f*70);float bottom=Math.Max(0,70-t/.14f*70);bottom=t<.14f?bottom:0;
                        var e1=c-s-q;var e2=c+s-q;var e3=c+s+q;var e4=c-s+q;Sheet(e1,e2,bottom,46,k*.55f,.34f,FallenFxQuads.Column);Sheet(e2,e3,bottom,46,k*.55f,.34f,FallenFxQuads.Column);Sheet(e3,e4,bottom,46,k*.55f,.34f,FallenFxQuads.Column);Sheet(e4,e1,bottom,46,k*.55f,.34f,FallenFxQuads.Column);
                        Beam(c+new Vector3(0,bottom,0),c+new Vector3(0,70,0),.2f+.5f*(1-C(t/.3f)),k,2.6f);FloorLine(e1,e2,.09f,k,2.2f);FloorLine(e2,e3,.09f,k,2.2f);FloorLine(e3,e4,.09f,k,2.2f);FloorLine(e4,e1,.09f,k,2.2f);
                        Quads.Add(e1,e2,e3,e4,k*.8f,.5f,FallenFxQuads.Soft);if(t>=.14f){Step(c.X,c.Z,t-.14f,.9f,1.1f);if(t<1)Burst(c,t-.14f,a.Seed+cx*5+cz*9+b,5,4,7,.04f,2.6f);}
                    }
                }
                if(t>=.14f&&t<1.4f){Cross(a.From,t-.14f,44,.7f,1.4f);Shake=Math.Max(Shake,.5f*(1-C((t-.14f)/.3f)));}
            }
        }
        // ---- the adds (docs section 9): hollow robes, husks, rings of blades ----
        private void AddsOf(FallenBossFrame f,float p)
        {
            foreach(var a in NativeFallenBossPreview.Acts)
            {
                float t=p-a.Hit,life=a.End-a.Hit;if(t<0||t>life)continue;float gone=life-t;
                if(a.Id=="choir")
                {
                    for(int i=0;i<6;i++)
                    {
                        int who=i%NativeFallenPilots.Count;float o=i*1.05f+a.Seed;var near=NativeFallenPilots.At(who,a.Hit);var at=near+new Vector3((float)Math.Cos(o)*13,0,(float)Math.Sin(o)*13);float killed=life-1.2f*i-1;float k=Smooth(t/1.1f)*(1-Smooth((t-killed)/.6f));if(k<=0)continue;
                        float bob=.7f+.18f*(float)Math.Sin(p*1.7f+i)+(1-k)*2.2f;var pilot=NativeFallenPilots.At(who,p)+new Vector3(0,1.2f,0);var mouth=at+new Vector3(0,bob+2.46f,.18f);
                        var look=pilot-at;float yaw=(float)Math.Atan2(look.X,look.Z);var turn=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw)*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,.05f*(float)Math.Sin(p*1.1f+i));Prop("cantor",at+new Vector3(0,bob,0),turn,k,1.9f);
                        Step(at.X,at.Z,(p*.8f+i*.37f)%2.2f,.35f*k,2.2f);
                        // The thin beam it sings, strengthening.
                        float sing=C((t-1.4f)/5);if(sing>0)Beam(mouth,pilot,.03f+.09f*sing,k*(.35f+.65f*sing),2.2f);
                    }
                }
                else if(a.Id=="penance")
                {
                    for(int i=0;i<9;i++)
                    {
                        int who=i%NativeFallenPilots.Count;float o=i*2.2f+a.Seed;float rise=Smooth(t/1.2f-i*.06f);float crawl=Math.Max(0,t-1.1f-i*.07f);var start=NativeFallenPilots.At(who,a.Hit)+new Vector3((float)Math.Cos(o)*11,0,(float)Math.Sin(o)*11);
                        var goal=NativeFallenPilots.At(who,p);var run=goal-start;float g=run.Length();float along=Math.Min(g-1.5f,crawl*9);var at=g>.01f?start+run/g*Math.Max(0,along):start;float killed=life-(i%3)*.8f-1;float k=1-Smooth((t-killed)/.5f);if(k<=0)continue;
                        float lurch=.09f*(float)Math.Abs(Math.Sin(p*9+i));float yaw=(float)Math.Atan2(run.X,run.Z);var turn=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,.12f+.1f*(float)Math.Sin(p*9+i));
                        Prop("penitent",at+new Vector3(0,(rise-1)*1.3f+lurch-(1-k)*1.3f,0),turn,1.05f,1);Step(at.X,at.Z,(p*2+i*.41f)%.8f,.5f,.8f);
                        if(t<1.6f+i*.07f)Burst(start,t-i*.06f,a.Seed+i*5,10,2,4,.035f,2.3f);
                    }
                }
                else if(a.Id=="thrones")
                {
                    for(int i=0;i<2;i++)
                    {
                        float o=a.Seed+i*3.14f;var at=new Vector3((float)Math.Cos(o+p*.05f)*20,16+Math.Min(2,t)*0+.5f*(float)Math.Sin(p*.7f+i),(float)Math.Sin(o+p*.05f)*20);float k=Smooth(t/1.4f)*(1-Smooth((t-life+1.4f)/.9f));if(k<=0)continue;at.Y+=(1-k)*14;
                        var target=NativeFallenPilots.At((i+1)%NativeFallenPilots.Count,p)+new Vector3(0,1,0);var d=Vector3.Normalize(target-at);var eyeTurn=FromTo(Vector3.UnitZ,d);var ring=eyeTurn*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,p*.7f+i);Prop("throne-ring",at,ring,1.6f,1);Prop("throne-eye",at,eyeTurn,1.6f,2.6f);
                        // It shoots on the song's beats, a white needle and a ring where it lands.
                        float period=NativeFallenSong.Bar;float shot=(t+i*period/2)%period;if(shot<.42f){float q=1-shot/.42f;Beam(at+d*1.1f,target,.06f+.2f*q,q,3.4f);Step(target.X,target.Z,shot,1.1f,.9f);Spot(target,1.1f*q,q,2.4f);Spot(at+d*1.2f,1.5f*q,q,3);}
                        Spot(target-new Vector3(0,.95f,0),.5f,.5f,1,FallenFxQuads.Droplet);
                    }
                }
            }
        }
        private static Quaternion FromTo(Vector3 a,Vector3 b)
        {
            var c=Vector3.Cross(a,b);float d=Vector3.Dot(a,b);if(d<-.9999f)return Quaternion.CreateFromAxisAngle(Vector3.UnitX,(float)Math.PI);var q=new Quaternion(c.X,c.Y,c.Z,1+d);return Quaternion.Normalize(q);
        }
    }
}
