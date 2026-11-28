using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Cyber Lord's Standard skin (owner 2026-10-13: "Change Cyber lord weapon to instead just show an aura around the hands
    /// (Like a glitchy / cyber aura)"; "We can use this current model as a prototype"): no hardlight gauntlets or fist shells
    /// (those are the Prototype Series, NativeCyberLordLook), the pilot's own hands and lower forearms, on any body skin,
    /// wrapped in a glitchy cyber aura. Glowing voxel primitives on the rig's hand and elbow joints (the look's CyberPiece, the
    /// same unlit glow pieces), in the look's palette, hugging the hand a few millimetres off its boxes:
    /// - shards: voxel and pixel fragments on the fist and the lower forearm that snap in and out on hashed cycles and drift
    ///   outward, a few of them floating further out;
    /// - the RGB split: a magenta and a cyan copy of some shards a few millimetres apart, re-offset every glitch step;
    /// - scanlines: short bands crawling down the forearm onto the hand;
    /// - data strands: dashed pixel columns rising off the fists in the world (CyberPiece.Rise), whatever the hand's turn;
    /// - glitch jumps: now and then a hand's whole aura jumps a few centimetres for a step or two;
    /// - a soft haze round each hand (Halo, the effects pass), its hot core and its own RGB split.
    /// It ignites over the look's FormSeconds from the form time (first seen alive, a respawn, the entrance's FormAt): the shards
    /// rush in from further out, flash and settle, one scanline sweeps the arm. It reacts to the gauntlets' signals: the
    /// striking hand's flare brightens and swells it and bursts shards off the knuckles, the mana charge lights both hands and
    /// adds strands, a guard packs the shimmer denser and faster. In ranged mode it condenses into a tight emitter point and a
    /// turning hex glyph ahead of each palm at the look's EmitterAt (the casts' anchor; nothing moves the hit origins): the
    /// right hand the main emitter with three prongs, the left the support coil (two counter-turning hexes); the scanlines run
    /// faster into them and the strands die down. The crest, the seams and the data shimmer stay (NativeCyberLordLook.PiecesIn).
    /// Seeded per pilot and a pure function of the presentation clock; no allocation. Reduced effects: slower, every piece
    /// eased in and out (no snap, blink, split or jump; never strobes); the low tier: fewer shards, one band, short strands, no
    /// split. First person keeps a lighter aura on the view model's hands.
    /// </summary>
    public static class NativeCyberLordAura
    {
        /// <summary>The most pieces the aura writes for both hands (NativeCyberLordLook.Capacity reserves them).</summary>
        public const int Max=320;
        /// <summary>The glitch steps per second (the Sheldon Intro's and the idle glitch's), full and reduced.</summary>
        public const float StepRate=NativeSheldonIntro.StepRate,ReducedStepRate=NativeSheldonIntro.ReducedStepRate;
        /// <summary>The Cyber Lord's pick in a seat's skin map (Core/NativeWeaponSkins.cs entry "cyberlord"): StandardIndex (this
        /// aura) when the entry is absent or malformed (older clients, old saves, bots), PrototypeIndex for the gauntlets.</summary>
        public static int Skin(string map)=>NativeWeaponSkins.Index(map,CyberLordSpec.Id);
        /// <summary>The guard's weight (0..1): in over .08 s from the block's start, out over .12 s after its end.</summary>
        public static float Guard(float time,float blockAt,float blockUntil)
        {
            if(!(blockAt>-90)||time<blockAt)return 0;float on=Smooth((time-blockAt)/.08f);
            return time<=blockUntil?on:on*(1-Smooth((time-blockUntil)/.12f));
        }

        // ---- the shape the aura hugs (source frame, the joints' space; NativeSourceModels mirrors x) ----
        // The fist (the wrist joint at the origin, the fingers toward -y, the knuckles toward +z: the pilot's hand boxes) as a
        // rounded box: its centre, half extents and the superellipse power; the thumb side reaches further out.
        private static readonly Vector3 FistC=new Vector3(0,-.052f,.024f),FistR=new Vector3(.084f,.078f,.104f);
        private const float FistPow=.55f,ThumbReach=1.24f;
        // The lower forearm (the elbow joint's space, the wrist at y -.25): from ArmTop down to the wrist, its rounded section.
        private const float ArmTop=-.115f,ArmBottom=-.245f,ArmRx=.086f,ArmRz=.09f,ArmPow=.6f;
        /// <summary>The scanline's run (m): down the lower forearm (ArmRun) then over the back of the hand to the knuckles.</summary>
        private const float ArmRun=ArmTop-ArmBottom,HandRun=.11f,ScanRun=ArmRun+HandRun;
        private static readonly Vector3 Knuckles=new Vector3(0,-.085f,.122f),SplitAxis=Vector3.UnitX;
        // The palette (linear; NativeCyberLordLook's cyan, white core, magenta and deep cyan).
        private static readonly Vector3 LCyan=NativeCyberLordLook.Lin(NativeCyberLordLook.Cyan),LCore=NativeCyberLordLook.Lin(NativeCyberLordLook.Core),
            LMagenta=NativeCyberLordLook.Lin(NativeCyberLordLook.Magenta),LDeep=NativeCyberLordLook.Lin(NativeCyberLordLook.Deep);

        private static float H(int n,int salt){unchecked{uint h=(uint)n*0x9e3779b1u^(uint)salt*0x85ebca6bu;h^=h>>15;h*=0x2c1b3c6du;h^=h>>12;h*=0x297a2d39u;h^=h>>15;return (h&0xffffff)/16777216f;}}
        private static int K(int a,int b){unchecked{return a*0x2c1b3c6d^b*0x297a2d39;}}
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        private static float Bump(float x,float at,float half){float d=1-Math.Abs(x-at)/half;return d<=0?0:d*d;}
        private static float Pow(float x,float p)=>x<=0?0:(float)Math.Pow(x,p);
        private static float Signed(float x,float p)=>x<0?-Pow(-x,p):Pow(x,p);
        /// <summary>A unit direction from two hashes of `key` (uniform on the sphere).</summary>
        private static Vector3 Dir(int key,int salt)
        {
            float z=2*H(key,salt)-1,a=6.2831853f*H(key,salt+1),r=(float)Math.Sqrt(Math.Max(0,1-z*z));
            return new Vector3(r*(float)Math.Cos(a),r*(float)Math.Sin(a),z);
        }
        /// <summary>The point of the fist's rounded box in direction `d` (`thumb` the thumb side's sign of x).</summary>
        private static Vector3 OnFist(Vector3 d,float thumb)
        {
            var p=new Vector3(Signed(d.X,FistPow)*FistR.X,Signed(d.Y,FistPow)*FistR.Y,Signed(d.Z,FistPow)*FistR.Z);
            if(p.X*thumb>0&&p.Y>-.09f&&p.Y<.01f&&p.Z>-.02f&&p.Z<.09f)p.X*=ThumbReach;
            return FistC+p;
        }
        /// <summary>The hash seed of pilot `seed`'s hand `h` (0 left, 1 right): its shards, bands, strands and jumps.</summary>
        internal static int HandSeed(int seed,int h)=>K(seed+1,h*7+3);
        /// <summary>A hand's glitch jump at `time` (m, signed; 0 between jumps): a hashed moment in each .9 s slot, half the
        /// slots, a step or two long; `sideways` across the hand, else along it.</summary>
        internal static float Jump(int hs,float time,out bool sideways)
        {
            const float Slot=.9f;float phase=H(hs,1)*Slot;int slot=(int)Math.Floor((time+phase)/Slot);int jk=K(hs,slot);sideways=H(jk,7)<.6f;
            if(!(H(jk,2)<.5f))return 0;float at=slot*Slot-phase+.1f+.7f*Slot*H(jk,3),len=(H(jk,4)<.4f?2:1)/StepRate;
            return time>=at&&time<at+len?(.012f+.02f*H(jk,5))*(H(jk,6)<.5f?-1:1):0;
        }
        private static Quaternion RotZ(float a)=>Quaternion.CreateFromAxisAngle(Vector3.UnitZ,a);
        private static void Put(CyberPiece[] into,ref int n,int bone,Vector3 p,Vector3 s,Quaternion r,Vector3 color,float gain,float rise=0,byte kind=CyberPiece.Aura)
        {
            if(n>=into.Length||!(gain>1e-3f))return;
            into[n++]=new CyberPiece{Bone=bone,Shape=WeaponShape.Cube,Position=p,Scale=s,Rotation=r,Color=color*gain,Rise=rise,Kind=kind};
        }

        /// <summary>
        /// The aura's pieces at `time` into `into` from index `n` (returns the new count): pilot `seed`, formed from `formAt`, the
        /// hands flared by `flare` (its Charge: the mana glow), `first` the first-person view model (its hands only), `solid` the
        /// body's solidity (the Data Dash), `quality` .3 Reduced, `low` the low graphics tier, `mode` the ranged mode (weight,
        /// charge, switch glitch), `guard` the block's weight (Guard).
        /// </summary>
        public static int Pieces(int seed,float time,float formAt,NativeCyberLordLook.Flare flare,bool first,float solid,float quality,bool low,CyberPiece[] into,int n,in NativeCyberLordLook.Mode mode,float guard)
        {
            float form=time-formAt;if(into==null||!(form>=0))return n;float sol=Clamp01(solid);if(sol<=.01f)return n;
            bool reduced=quality<.5f;float ign=Clamp01(form/NativeCyberLordLook.FormSeconds);
            float open=Clamp01(mode.Ranged),charge=Clamp01(mode.Charge),glitch=Clamp01(mode.Glitch),block=Clamp01(guard),mana=Clamp01(flare.Charge);
            int step=(int)Math.Floor(time*(reduced?ReducedStepRate:StepRate));float shrink=.85f+.15f*sol;
            // The ignition: the aura rushes in from further out and flashes as it settles (FormSeconds).
            float rush=(1-Smooth(ign))*(1-Smooth(ign)),flash=1+2.2f*Bump(ign,.55f,.3f);
            for(int h=0;h<2;h++)
            {
                bool left=h==0;float sign=left?1:-1,thumb=-sign;int hand=left?CapturedRig.LHand:CapturedRig.RHand,arm=left?CapturedRig.LElbow:CapturedRig.RElbow;
                float fl=Clamp01(left?flare.Left:flare.Right),hit=Clamp01(left?flare.HitLeft:flare.HitRight);int hs=HandSeed(seed,h);
                float lit=(.3f+.7f*Smooth(ign))*flash*(1+1.8f*fl+.9f*mana+.25f*block)*sol;
                // The glitch jump (full effects, once formed).
                bool sideways=false;float jd=reduced||ign<1?0:Jump(hs,time,out sideways);bool jumping=jd!=0;var jump=!jumping?Vector3.Zero:sideways?new Vector3(jd,0,0):new Vector3(0,jd*.5f,jd);
                // ---- shards ----
                int fistN=first?(low?6:reduced?10:15):low?9:reduced?15:26,armN=first?(low?3:reduced?4:6):low?4:reduced?8:14;
                fistN+=(int)(fistN*.6f*block+.5f)+(int)(3*mana+.5f);armN=(int)(armN*(1+.6f*block)*(1-.7f*open)+.5f);
                int total=fistN+armN,splits=0,maxSplits=low||reduced?0:first?4:10;
                float splitChance=.3f+.3f*block+(jumping?.5f:0)+.4f*glitch;
                for(int k=0;k<total;k++)
                {
                    if(k>=total*Math.Min(1,ign*1.6f))break;
                    bool onArm=k>=fistN;int key=K(hs,k+11);
                    float period=((reduced?.7f:.22f)+(reduced?.5f:.26f)*H(key,1))*(1-.35f*block);
                    float tt=(time+H(key,2)*period)/period;int cyc=(int)Math.Floor(tt);float u=tt-cyc;int ck=K(key,cyc);
                    float env=1;
                    if(reduced)env=(float)Math.Sin(Math.PI*u);
                    else
                    {
                        // Snapping in and out over its cycle, and now and then blinking out for a glitch step.
                        float on0=.12f*H(ck,3),on1=on0+.5f+.4f*H(ck,4);if(u<on0||u>on1)continue;
                        if(H(K(ck,step),5)<.1f)continue;if(ign<1&&H(K(ck,step),6)<.35f)continue;
                    }
                    Vector3 p,d;int bone;float fade=1;
                    // Most sit just off the skin, some float further out (smaller and dimmer); all drift outward over their cycle.
                    bool far=H(ck,16)<.35f;float off=(far?.016f+.03f*H(ck,6):.004f+.009f*H(ck,6))+(reduced?.006f:.014f)*u;off*=1+.8f*fl;off+=.22f*rush;
                    if(!onArm)
                    {
                        bone=hand;d=Dir(ck,20);p=OnFist(d,thumb)+d*off;
                        // Ranged: the fist's shards condense on the emitter ahead of the palm.
                        if(open>1e-3f)p=Vector3.Lerp(p,NativeCyberLordLook.EmitterAt+d*(.026f+.01f*fl),.8f*open);
                    }
                    else
                    {
                        bone=arm;float y=ArmBottom+ArmRun*Pow(H(ck,7),1.6f);if(open>1e-3f)y+=(ArmBottom-y)*.6f*open;
                        float a=6.2831853f*H(ck,8),c=(float)Math.Cos(a),s=(float)Math.Sin(a);d=new Vector3(c,0,s);
                        p=new Vector3(Signed(c,ArmPow)*ArmRx,y,Signed(s,ArmPow)*ArmRz)+d*off;fade=1-.55f*(y-ArmBottom)/ArmRun;
                    }
                    p+=jump;
                    float size=(.01f+.018f*Pow(H(ck,9),1.5f))*(far?.6f:1)*(1+.35f*fl)*shrink*(reduced?.6f+.4f*env:1)*(onArm?1:1-.35f*open);
                    var scale=new Vector3(size,size,size);float pick=H(ck,10);
                    if(pick<.22f){int axis=(int)(H(ck,12)*3);scale*=.55f;if(axis==0)scale.X*=4.7f;else if(axis==1)scale.Y*=4.7f;else scale.Z*=4.7f;}
                    float c2=H(ck,11);Vector3 col;float g;
                    if(c2<.55f){col=LCyan;g=2.2f;}else if(c2<.77f){col=LCore;g=2.6f;}else if(c2<.92f){col=LMagenta;g=2.1f;}else{col=LDeep;g=3f;}
                    g*=(.75f+.5f*H(ck,13))*env*lit*fade*(far?.75f:1);
                    // The RGB split: a magenta and a cyan copy either side, re-offset every step.
                    if(splits<maxSplits&&H(K(ck,step+5),14)<splitChance)
                    {
                        var o=SplitAxis*((.004f+.009f*H(K(k,step),15))*(1+fl));splits++;
                        Put(into,ref n,bone,p+o,scale*.9f,Quaternion.Identity,LMagenta,1.3f*lit*fade);Put(into,ref n,bone,p-o,scale*.9f,Quaternion.Identity,LCyan,1.3f*lit*fade);
                    }
                    Put(into,ref n,bone,p,scale,Quaternion.Identity,col,g);
                }
                // ---- scanlines crawling down the forearm onto the hand ----
                int bands=low||first||reduced?1:2;if(block>.5f&&!low)bands++;
                for(int b=0;b<bands;b++)
                {
                    int key=K(hs,b+101);float u;int cyc;
                    if(ign<1){u=ign*1.15f-.08f*b;cyc=-1;}
                    else{float period=(.55f+.3f*H(key,1))*(reduced?1.6f:1)/(1+1.4f*open+.4f*block);float tt=time/period+H(key,2);cyc=(int)Math.Floor(tt);u=(tt-cyc)*1.2f;}
                    if(u<0||u>1)continue;
                    float ramp=reduced?.35f:.12f,g=2.6f*Smooth(u/ramp)*Smooth((1-u)/ramp)*lit;if(!reduced&&H(K(key,step),3)<.18f)g*=.25f;
                    int faces=cyc<0?15:1|(int)(H(K(key,cyc),4)*16);float c=u*ScanRun;const float t=.006f,dz=.006f;
                    if(c<ArmRun)
                    {
                        float y=ArmTop-c;var fz=new Vector3(2*ArmRx*.9f,t,dz);var fx=new Vector3(dz,t,2*ArmRz*.9f);
                        if((faces&1)!=0)Put(into,ref n,arm,new Vector3(0,y,ArmRz+.006f)+jump,fz,Quaternion.Identity,LCore,g);
                        if((faces&2)!=0)Put(into,ref n,arm,new Vector3(0,y,-ArmRz-.006f)+jump,fz,Quaternion.Identity,LCyan,g*.9f);
                        if((faces&4)!=0)Put(into,ref n,arm,new Vector3(ArmRx+.006f,y,0)+jump,fx,Quaternion.Identity,LCyan,g*.9f);
                        if((faces&8)!=0)Put(into,ref n,arm,new Vector3(-ArmRx-.006f,y,0)+jump,fx,Quaternion.Identity,LCyan,g*.9f);
                    }
                    else
                    {
                        float y=-(c-ArmRun);bool knuckle=y<-.05f;float front=knuckle?.128f:.088f,zc=knuckle?.025f:.005f,zl=knuckle?.19f:.15f;
                        if((faces&1)!=0)Put(into,ref n,hand,new Vector3(0,y,front)+jump,new Vector3(knuckle?.13f:.14f,t,dz),Quaternion.Identity,LCore,g);
                        if((faces&2)!=0)Put(into,ref n,hand,new Vector3(0,y,-.078f)+jump,new Vector3(.14f,t,dz),Quaternion.Identity,LCyan,g*.9f);
                        if((faces&4)!=0&&!(thumb>0&&y>-.085f))Put(into,ref n,hand,new Vector3(.08f,y,zc)+jump,new Vector3(dz,t,zl),Quaternion.Identity,LCyan,g*.9f);
                        if((faces&8)!=0&&!(thumb<0&&y>-.085f))Put(into,ref n,hand,new Vector3(-.08f,y,zc)+jump,new Vector3(dz,t,zl),Quaternion.Identity,LCyan,g*.9f);
                    }
                }
                // ---- data strands rising off the fist (in the world: Rise) ----
                int strands=(first?(low?1:2):low?1:reduced?2:4)+(int)(2*mana+.5f);strands=(int)(strands*(1-open)+.5f);if(ign<1)strands=0;
                for(int w=0;w<strands;w++)
                {
                    int key=K(hs,w+201);float period=(.8f+.5f*H(key,1))*(reduced?1.4f:1)*(1-.3f*mana);
                    float tt=time/period+H(key,2);int cyc=(int)Math.Floor(tt);float u=tt-cyc;int ck=K(key,cyc);
                    var d=Dir(ck,30);if(d.Y<-.3f)d.Y=-d.Y;var root=OnFist(d,thumb)+d*.006f;
                    float head=u*(.14f+.12f*H(ck,3))*(1+.4f*mana),fade=reduced?(float)Math.Sin(Math.PI*u):1-u;int segs=low?3:5;
                    for(int j=0;j<segs;j++)
                    {
                        float rise=head-j*.028f;if(rise<0)break;
                        if(H(K(ck,j),4)<.22f)continue;if(!reduced&&H(K(ck+j,step),5)<.12f)continue;
                        float sway=.009f*(float)Math.Sin((u*1.6f+H(ck,6))*6.2831853f+j*.9f);
                        var col=j==0?LCore:H(K(ck,j+40),7)<.2f?LMagenta:LCyan;float sz=(j==0?.0105f:.008f)*shrink;
                        Put(into,ref n,hand,root+new Vector3(sway,0,sway*.5f)+jump,new Vector3(sz,sz,sz),Quaternion.Identity,col,(j==0?2.4f:1.8f)*fade*(1-.14f*j)*lit,rise);
                    }
                }
                // ---- the hit's burst: shards off the knuckles (the emitter in ranged mode); a clench only brightens ----
                if(hit>.04f)
                {
                    int count=low?3:reduced?4:8;float k=1-hit,reach=.015f+.17f*Pow(k,.7f);var from=Vector3.Lerp(Knuckles,NativeCyberLordLook.EmitterAt,open);
                    for(int i=0;i<count;i++)
                    {
                        int key=K(hs,i+301);var d=Vector3.Normalize(new Vector3((H(key,1)-.5f)*1.7f,(H(key,2)-.5f)*1.3f,.35f+.9f*H(key,3)));float sz=.018f*(.35f+.65f*hit)*shrink;
                        Put(into,ref n,hand,from+d*reach+jump,new Vector3(sz,sz,sz*.6f),RotZ(H(key,4)*3.1416f),i%3==0?LCore:i%3==1?LCyan:LMagenta,2.6f*(float)Math.Sqrt(hit)*sol);
                    }
                }
                // ---- ranged: the emitter point and the hex glyph ahead of the palm ----
                if(open>1e-3f&&ign>=.5f)
                {
                    // First person sees the palm from behind: the glyph opens wider there, so it frames the fist instead of hiding behind it.
                    bool main=!left;float wide=first?(main?2.8f:3.4f):1,r=(main?.046f:.034f)*wide,spin=time*(reduced?.7f:2.2f)*(main?1:-1)*(1+1.4f*charge);
                    float gE=(1+1.6f*charge)*(1+1.8f*fl)*sol;var at=NativeCyberLordLook.EmitterAt;
                    float u0=Smooth(open/.4f);
                    Put(into,ref n,hand,at,new Vector3(.02f,.02f,.006f)*(.5f+.5f*u0)*shrink,RotZ(.785f+.5f*spin),LCore,(main?2.6f:2.2f)*u0*gE,0,CyberPiece.AuraEmitter);
                    for(int i=0;i<6;i++)
                    {
                        if(low&&i%2==1)continue;float u=Clamp01((open-(.1f+.08f*i)*.6f)/.4f);if(u<=0)continue;
                        float flick=u<1&&!reduced&&H(K(step,i+h*6),9)<.3f?.2f:1;if(glitch>0&&!reduced&&H(K(step,i+h*6),11)<.25f*glitch)flick*=.15f;
                        float a=i*1.0471976f+.5235988f+spin;var c=at+new Vector3((float)Math.Cos(a),(float)Math.Sin(a),0)*(r*.87f)+new Vector3(0,0,.012f);
                        Put(into,ref n,hand,c,new Vector3(r*1.02f,.006f,.01f)*(.5f+.5f*Smooth(u))*shrink,RotZ(a+1.5707963f),i%2==0?LCyan:LCore,1.9f*flick*gE*(.35f+.65f*Smooth(u)),0,CyberPiece.AuraEmitter);
                    }
                    float u1=Clamp01((open-.5f)/.5f);
                    if(u1>0&&!low)
                    {
                        if(main)for(int i=0;i<3;i++)
                        {
                            // The main emitter's three prongs, turning with the glyph.
                            float a=1.5707963f+i*2.0943951f+spin;var c=at+new Vector3((float)Math.Cos(a),(float)Math.Sin(a),0)*(r*1.12f)+new Vector3(0,0,.026f);
                            Put(into,ref n,hand,c,new Vector3(.006f,.006f,.03f)*Smooth(u1)*shrink,Quaternion.Identity,LMagenta,1.7f*gE,0,CyberPiece.AuraEmitter);
                        }
                        else for(int i=0;i<6;i++)
                        {
                            // The support coil: a second, smaller hex a little ahead, turning the other way.
                            float r2=.022f*wide,a=i*1.0471976f+.5235988f-1.6f*spin;var c=at+new Vector3((float)Math.Cos(a),(float)Math.Sin(a),0)*(r2*.87f)+new Vector3(0,0,.03f);
                            Put(into,ref n,hand,c,new Vector3(r2*1.02f,.005f,.008f)*Smooth(u1)*shrink,RotZ(a+1.5707963f),i%2==0?LDeep:LCyan,2.1f*gE,0,CyberPiece.AuraEmitter);
                        }
                    }
                }
            }
            return n;
        }

        // ---- the haze: soft glows round the hands (the match and first person, in the effects pass; the entrance and the
        // portraits draw their own: NativeCyberLordIntroScript.Aura, NativePortraits CyberAuraGlow) ----
        /// <summary>Where a pilot's hands are this frame (world metres): each fist's centre, a point down its lower forearm and the
        /// emitter point ahead of its palm (the look's EmitterAt on the hand), the pilot's right, and the rig's scale (the
        /// first-person view model is drawn smaller).</summary>
        public struct HandPoints{public Vector3 LFist,LArm,LEmitter,RFist,RArm,REmitter,Right;public float Scale;}
        /// <summary>The fist's centre, a lower-forearm point and the emitter in their joints' frames (source frame).</summary>
        public static readonly Vector3 FistCentre=new Vector3(0,-.06f,.03f),ArmPoint=new Vector3(0,-.2f,0);
        /// <summary>
        /// The aura's soft haze for one pilot (the Standard skin): a cyan glow round each fist and down its lower forearm, a hot
        /// flickering core at the knuckles, and the RGB split as a magenta and a cyan glow either side, jittering every glitch step.
        /// It ignites with the pieces, brightens and swells with the striking hand's flare, the mana and a guard, follows the
        /// glitch jumps' moments, and in ranged mode shrinks onto the emitter point. Reduced effects: a steady haze, no split or
        /// flicker; the low tier: the fist glow only. `view` draws on the first-person view layer.
        /// </summary>
        public static void Halo(ICyberFxSink sink,bool view,in HandPoints at,int seed,float time,float formAt,NativeCyberLordLook.Flare flare,float solid,float quality,bool low,in NativeCyberLordLook.Mode mode,float guard)
        {
            float form=time-formAt;if(sink==null||!(form>=0))return;float sol=Clamp01(solid);if(sol<=.01f)return;
            bool reduced=quality<.5f;float ign=Clamp01(form/NativeCyberLordLook.FormSeconds),open=Clamp01(mode.Ranged),charge=Clamp01(mode.Charge),mana=Clamp01(flare.Charge),block=Clamp01(guard);
            int step=(int)Math.Floor(time*(reduced?ReducedStepRate:StepRate));float k=at.Scale>0?at.Scale:1;
            float base0=Smooth(ign*1.8f)*(1+1.6f*Bump(ign,.55f,.3f))*sol;
            for(int h=0;h<2;h++)
            {
                bool left=h==0;float fl=Clamp01(left?flare.Left:flare.Right);int hs=HandSeed(seed,h);
                Vector3 fist=left?at.LFist:at.RFist,arm=left?at.LArm:at.RArm,emit=left?at.LEmitter:at.REmitter;
                float flick=reduced?1:.8f+.4f*H(K(hs,step),40);
                float g=base0*(1+1.8f*fl+.8f*mana+.3f*block+(open>0?1.2f*charge*open:0))*flick;
                // First person sees the palm from behind: its haze stays wider and nearer the fist so it shows round it.
                float jd=reduced||ign<1?0:Jump(hs,time,out _);var centre=Vector3.Lerp(fist,emit,(view?.35f:.75f)*open)+at.Right*(jd*k);float size=k*(.34f-(view?.02f:.14f)*open)*(1+.35f*fl+.15f*block);
                sink.Sprite(view,centre,LCyan*(2.2f*g),size,NativeCyberLordFx.Soft);
                if(low)continue;
                sink.Sprite(view,Vector3.Lerp(arm,fist,.35f),LCyan*(.9f*g*(1-.7f*open)),k*.24f,NativeCyberLordFx.Soft);
                // The knuckles' hot core flickers on the steps (steady with reduced effects).
                var knuckles=Vector3.Lerp(fist+(fist-arm)*.45f,emit,open);float hot=reduced?1.1f:H(K(hs,step+77),41)<.25f?.3f:.9f+.7f*H(K(hs,step),42);
                sink.Sprite(view,knuckles,LCore*(hot*g),k*(.09f+.05f*fl),NativeCyberLordFx.Hot);
                if(reduced)continue;
                // The RGB split: a magenta and a cyan glow either side, re-offset every step.
                float split=k*(.012f+.022f*H(K(hs,step+3),43))*(1+fl);
                sink.Sprite(view,centre+at.Right*split,LMagenta*(.55f*g),size*.85f,NativeCyberLordFx.Soft);sink.Sprite(view,centre-at.Right*split,LCyan*(.5f*g),size*.85f,NativeCyberLordFx.Soft);
            }
        }
    }
}
