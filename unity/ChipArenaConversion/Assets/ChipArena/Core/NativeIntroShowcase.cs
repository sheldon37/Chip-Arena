using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "DEV · Pipeline Proof" (I0, Intros v2 framework, 2026-11-20): a small DEV-only intro that proves the v2 pipeline end to end
    /// (the registry, the camera rig with a speed ramp, the set bank with its void fallback, the name card, the cue timeline). It may be
    /// removed once real intros land. 8 s, the name at 5.9 s. A slow crane down out of the dark onto a pilot standing in a pool of light;
    /// a column of light drops on the beat, a ring of motes lifts, the camera orbits half round and settles on the hero frame.
    /// </summary>
    public sealed class NativeIntroShowcase:NativeIntroV2Script
    {
        public const float Total=8f,CardAt=5.9f,DropAt=2.2f,TurnAt=3.4f,PoseAt=4.6f;
        /// <summary>The showcase's set bank (local, git-ignored: built by tools/intros/showcase_set.py).</summary>
        public const string Bank="ChipArenaIntroShowcase";
        private static readonly Vector3 Cold=NativeEntranceFx.Hex(0x8fb0ff),White=new Vector3(1,1,1);
        public NativeIntroShowcase():base(NativeIntroCatalog.ShowcaseId,Total,CardAt,DropAt,0x7fa6ff)
        {
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(0,.88f,0,Facing+.6f).Planted(1).Drop(-.02f).J(Hd,.25f).J(C,.08f).J(LA,-.15f,0,.2f).J(RA,-.15f,0,-.2f);
            k.Key(DropAt-.1f,stand).Root(0,.88f,0,Facing+.6f).Planted(1).Drop(-.04f).J(Hd,.3f).J(C,.12f).J(LA,-.12f,0,.18f).J(RA,-.12f,0,-.18f);
            k.Key(DropAt+.15f,stand).Root(0,.88f,0,Facing+.6f).Planted(1).Drop(-.1f).J(B,.12f).J(Hd,-.35f).J(C,-.1f).J(LA,-.4f,0,.5f).J(RA,-.4f,0,-.5f);
            k.Key(TurnAt,stand).Root(0,.88f,0,Facing+.25f).Planted(1).Drop(-.03f).J(Hd,-.1f).J(LA,-.2f,0,.25f).J(RA,-.2f,0,-.25f);
            k.Key(PoseAt,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.02f).J(C,0,.1f,0).J(Hd,-.05f,-.08f,0).J(RA,-.55f,0,-.25f).J(RE,-1.2f).J(LA,-.25f,0,.2f).J(LE,-.3f).ImpactKey();
            k.Key(Total,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.02f).J(C,0,.12f,0).J(Hd,-.06f,-.1f,0).J(RA,-.55f,0,-.25f).J(RE,-1.2f).J(LA,-.25f,0,.2f).J(LE,-.3f);Keys=k;
            Start=Stance(.16f,.07f,.1f,-.18f,-.1f,-.42f,Facing+.6f);
            Steps=new[]{Step(true,TurnAt-.3f,TurnAt+.05f,.15f,.08f,.05f),Step(false,TurnAt-.05f,TurnAt+.3f,-.17f,-.08f,-.4f)};
            // The camera: a crane down out of the dark, a ramp through the light's drop, a half orbit, the hero frame.
            Rig.Crane(0,new Vector3(1.4f,3.6f,5.2f),new Vector3(.9f,1.4f,3.4f),-.6f,new Vector3(0,.4f,0),new Vector3(0,1.1f,0),44,38).Ease(NativeIntroEase.InOut).Focus(0,0,.35f)
               .Orbit(DropAt-.05f,Vector3.Zero,25,-60,3.0f,2.6f,1.25f,1.05f,new Vector3(0,1.15f,0),new Vector3(0,1.1f,0),40,36).Follow(NativeEntranceAnchor.Chest,.08f).In(NativeIntroCut.Blend,.45f).Handheld(.6f)
               .Hero(PoseAt-.15f,1,1.15f,3.0f,31).In(NativeIntroCut.Blend,.5f)
               .Bars(0,1).Bars(CardAt-.4f,.55f).Black(0,1).Black(.05f,0);
            Ramps.Ramp(DropAt+.02f,.35f,.32f,.9f);
        }
        public override string SetBank=>Bank;
        public override string Music=>"audio/intros/signal-spin";
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(.1f,"intro2.showcase.rise","magic.blink-strike","dash",.35f),
            new NativeIntroCue(DropAt,"intro2.showcase.drop","impact.energy","lightning",.8f),
            new NativeIntroCue(TurnAt,"intro2.showcase.turn","melee.miss","dash",.4f),
            new NativeIntroCue(PoseAt,"intro2.showcase.pose","gun.rocket.dud","metal",.5f),
            new NativeIntroCue(CardAt,"intro2.showcase.name","structure.thud","stone",.7f),
        };
        public override NativeIntroCue[] Cues=>cues;
        private readonly Vector3[] shardAt=new Vector3[24];private readonly Quaternion[] shardTurn=new Quaternion[24];
        /// <summary>The bank's set (tools: F:/Chip Arena Development/scratch/intro-framework/tools/showcase-bank, procedural, original):
        /// the polished disc with its straight inlays lighting on the drop, five monoliths behind facing in, and shards lifting (Batch).</summary>
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("set/floor"))return false;
            set.Piece("set/floor",Vector3.Zero,Quaternion.Identity,1,Vector3.One,Cold*(.35f+1.4f*Flash(t,DropAt,.04f,.6f)));
            // (a monolith's face is its mesh's +z: turned by a+pi it faces the pilot from its place on the circle)
            if(set.Has("set/monolith"))for(int i=0;i<5;i++){float a=Facing+3.1416f+(i-2)*.5f;set.Piece("set/monolith",new Vector3((float)Math.Sin(a)*7.5f,0,(float)Math.Cos(a)*7.5f),NativeEntranceMath.Yaw(a+3.1416f),1,Vector3.One,Cold*(.15f+.85f*NativeEntranceMath.Ramp(t,DropAt+i*.08f,DropAt+.4f+i*.08f)));}
            if(t>DropAt&&set.Has("set/shard"))
            {
                int n=Context.Low?10:24;
                for(int i=0;i<n;i++){float a=i*.2618f+t*.6f,u=((t-DropAt)*.2f+i/24f)%1;shardAt[i]=new Vector3((float)Math.Sin(a)*(1.1f+.5f*u),.1f+2.6f*u,(float)Math.Cos(a)*(1.1f+.5f*u));shardTurn[i]=Quaternion.CreateFromYawPitchRoll(t*1.3f+i,t*.9f+i*.5f,0);}
                set.Batch("set/shard",new ReadOnlySpan<Vector3>(shardAt,0,n),new ReadOnlySpan<Quaternion>(shardTurn,0,n),1,Vector3.One);
            }
            return true;
        }
        protected override NativeIntroVoidLook VoidLook(float t){var v=NativeIntroVoidLook.Of(Cold);v.PoolLevel=.25f+.75f*NativeEntranceMath.Ramp(t,DropAt-.05f,DropAt+.3f)+.8f*Flash(t,DropAt,.03f,.5f);return v;}
        public override void Lights(float t,NativeEntranceLights l)
        {
            base.Lights(t,l);float drop=Flash(t,DropAt,.03f,.45f);
            if(drop>.01f)l.Add(new Vector3(0,3.2f,0),White*(9*drop),7);
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            NativeIntroVoid.Motes(fx,t,q,Cold);
            // The column of light: soft stacked glows from above onto the pilot at the drop.
            float c=NativeEntranceMath.Ramp(t,DropAt-.03f,DropAt+.05f)*(1-.6f*NativeEntranceMath.Ramp(t,DropAt+.4f,DropAt+1.6f));
            if(c>.01f)for(int i=0;i<8;i++){float y=.2f+i*.45f;fx.Glow(new Vector3(0,y,0),Cold*(.25f*c*(1-i/9f)),0,.55f+.06f*i,0,0,4300+i);}
            // A lifting swirl of motes around the pilot after the drop.
            if(t>DropAt)for(int i=0;i<(q<.5f?10:24);i++){float a=i*.2618f+t*.8f,u=((t-DropAt)*.25f+i/24f)%1;var p=new Vector3((float)Math.Sin(a)*(.6f+.3f*u),.1f+2.2f*u,(float)Math.Cos(a)*(.6f+.3f*u));fx.Glow(p,Cold*(.5f*(1-u)),0,.035f,0,u,4400+i);}
        }
    }
}
