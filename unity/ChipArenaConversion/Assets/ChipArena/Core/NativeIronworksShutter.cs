using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The City Ironworks' converter shutter (Claude, 2026-10-16; docs/EXPEDITION-CITY-IRONWORKS.md sections 2.3 and 2.5, owner
    /// decision 11): the instance gate `exp-foundry:gate:converter` (opens "boss", PR #254) drawn as the works' own heavy steel
    /// roller shutter in place of the generic state gate. NativeExpeditionStateScene hands this gate's closure here
    /// (<see cref="Owns"/>), so it is built from the same view (the director's closures), rig frame, presentation clock and
    /// timing as every other gate and drawn by the same draw (NativeBlockLit boxes and one effects batch); nothing else changes.
    /// <list type="bullet">
    /// <item>The pose follows the closure's states with the generic gate's timing (NativeExpeditionStates.GateRise: in step with
    /// the collider over the one-second transition): sealed, the curtain is down and locked, its bottom bar's magenta strip and
    /// the two status lamps lit in the expedition's closure colour; opening, the lock bolts draw back into the guides, the curtain
    /// rolls up into the drum slat by slat and its bar kicks dust and sparks off the guides; open, it is gone into the drum and the
    /// lamps are dark; sealing runs it down again.</item>
    /// <item>Its steel frame (guide channels, the drum's housing, hazard stripes, the sign) is the kit's (NativeIronworksKit.Converter),
    /// so the opening reads as a shutter whatever its state.</item>
    /// </list>
    /// Engine-free and allocation free (writes into the state scene's reused lists).
    /// </summary>
    public static class NativeIronworksShutter
    {
        public const string GateId="exp-foundry:gate:converter";
        /// <summary>Whether this presentation draws gate `id` (the generic state gate leaves it): the converter shutter only, so the
        /// live manifest's gates and every other instance's are untouched.</summary>
        public static bool Owns(string id)=>id==GateId;
        /// <summary>The curtain's slat pitch (m) and the curtain's depth.</summary>
        public const float Pitch=.19f,Depth=.07f;
        public const int SteelHex=0x3A3836,SlatHex=0x4A4642,SlatDarkHex=0x2E2B28,BarHex=0x24211E,MagentaHex=NativeCyberCityLooks.MagentaHex,DustHex=0x3A322A,SparkHex=0xFFB060,PaintHex=0xD8D4C8;
        /// <summary>The shutter's pose for a closure view at `t`: Rise 0 down .. 1 up in the drum, Lock 1 bolted .. 0 drawn back,
        /// Lamp the status lamps' level (lit while it blocks, flashing while it moves) and the closure's phase.</summary>
        public struct Pose {public float Rise,Lock,Lamp;public ExpeditionClosurePhase Phase;}
        public static Pose PoseOf(ExpeditionClosureView c,double t)
        {
            var p=new Pose{Phase=NativeExpeditionStates.Phase(c)};p.Rise=NativeExpeditionStates.GateRise(c,t,out float unlock);p.Lock=1-unlock;
            float prog=NativeExpeditionStates.Progress(c,t);
            switch(p.Phase){case ExpeditionClosurePhase.Sealed:p.Lamp=1;break;case ExpeditionClosurePhase.Open:p.Lamp=0;break;default:p.Lamp=(prog*6%1)<.5f?1:.25f;break;}
            return p;
        }
        /// <summary>Adds the shutter's boxes and effects for closure `c` at `t` in its rig frame `f`.</summary>
        public static void Build(List<ExpeditionKitBox> parts,int capacity,NativeFxQuads quads,in ExpeditionFrame f,ExpeditionClosureView c,double t,in ExpeditionEye eye,bool reduced,bool low)
        {
            var pose=PoseOf(c,t);float W=f.W,H=f.H,half=W/2,lift=pose.Rise*H;int metal=NativeCyberCityLooks.Metal,bank=NativeCyberCityLooks.Bank,tube=NativeCyberCityLooks.Tube;
            if(pose.Rise<.999f)
            {
                // The curtain from its bottom bar up to the drum: slats every Pitch (paired on the low tier), a stencil on the party's side.
                int step=low?2:1;
                for(int i=0;;i+=step)
                {
                    float v0=lift+.12f+i*Pitch;if(v0>=H)break;float v1=Math.Min(H,v0+Pitch*step-.012f);
                    Box(parts,capacity,f,-half,half,v0,v1,-Depth/2,Depth/2,metal,bank,i%3==0?SlatDarkHex:SlatHex,1,1,900+i,0,lift);
                }
                Box(parts,capacity,f,-half,half,lift,lift+.14f,-Depth/2-.03f,Depth/2+.03f,NativeBlockLooks.Demolition,0,0xF4B321,1,1,880,0,lift);
                Box(parts,capacity,f,-half+.15f,half-.15f,lift+.135f,lift+.165f,Depth/2+.02f,Depth/2+.04f,tube,bank,MagentaHex,2.4f,pose.Phase==ExpeditionClosurePhase.Sealed?1:.5f,881,0,lift);
                if(!low)for(float u=-half+1;u<half;u+=2)Box(parts,capacity,f,u-.04f,u+.04f,lift+.14f,H,Depth/2,Depth/2+.02f,metal,bank,SlatDarkHex,1,1,882+(int)u,0,lift);
                // NO ENTRY on the party's side, riding the curtain until it rolls into the drum.
                float sv=lift+1.9f;if(sv+.5f<H)Letters(parts,capacity,f,0,sv,Depth/2+.005f,.1f,PaintHex,lift);
            }
            // The lock bolts: out of the bar's ends into the guides while bolted, drawn back as it unlocks.
            if(pose.Rise<.02f)for(int k=-1;k<=1;k+=2){float s=k*(half-.35f-.4f*(1-pose.Lock)),e=s+k*.62f;Box(parts,capacity,f,Math.Min(s,e),Math.Max(s,e),.02f,.12f,-.06f,.06f,metal,bank,SteelHex,1,1,890+k,0,0);}
            // The status lamps on the jambs over the opening (the expedition's closure colour): lit sealed, flashing while it moves.
            for(int k=-1;k<=1;k+=2){float u=k*(half+.2f);Box(parts,capacity,f,u-.12f,u+.12f,H+.25f,H+.49f,-.38f,.38f,tube,bank,MagentaHex,2.6f,pose.Lamp,895+k,0,0);}
            // Moving: dust off the bar and sparks off the guides.
            bool moving=pose.Phase==ExpeditionClosurePhase.Opening||pose.Phase==ExpeditionClosurePhase.Sealing;if(!moving||quads==null)return;float prog=NativeExpeditionStates.Progress(c,t);
            int n=reduced||low?6:16;
            for(int i=0;i<n;i++)
            {
                double h=NativeIronworksKit.Hash(i,7,c.id.Length),u=-half+h*W,age=(prog+NativeIronworksKit.Hash(i,8,3)*.4)%1,y=lift*.3+age*1.4;f.Point((float)u,(float)y,.25f,out float x,out float yy,out float z);
                Sprite(quads,eye,x,yy,z,.5+age*1.2,.22,.19,.16,.35*(1-age),false);
            }
            for(int k=-1;k<=1;k+=2)for(int i=0;i<(low?3:8);i++)
            {
                double a=NativeIronworksKit.Hash(i,9,k+3),age=(prog*3+a)%1;f.Point(k*half,lift+(float)(age*-.8),(float)(a-.5)*.3f,out float x,out float y,out float z);
                Sprite(quads,eye,x,y,z,.06,2.6,1.4,.4,1-age,true);
            }
        }
        private static float Lin(int hex,int shift){double c=((hex>>shift)&255)/255.0;return (float)(c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4));}
        private static void Box(List<ExpeditionKitBox> parts,int capacity,in ExpeditionFrame f,float u0,float u1,float v0,float v1,float d0,float d1,int look,int flags,int hex,float gain,float level,int seed,float du,float dv)
        {
            if(parts.Count>=capacity)return;float uc=(u0+u1)/2,vc=(v0+v1)/2,dc=(d0+d1)/2;f.Point(uc,vc,dc,out float x,out float y,out float z);f.Point(uc-du,vc-dv,dc,out float rx,out float ry,out float rz);
            parts.Add(new ExpeditionKitBox{X=x,Y=y,Z=z,Hx=Math.Abs(u1-u0)/2,Hy=Math.Abs(v1-v0)/2,Hz=Math.Abs(d1-d0)/2,Yaw=f.Yaw,Look=look,Flags=flags,Hex=hex,Gain=gain,Level=level,RestX=rx,RestY=ry,RestZ=rz,Seed=NativeExpeditionStates.Hash(seed,31)});
        }
        /// <summary>The curtain's stencil and its glyph boxes, worked out once (GlyphBoxes allocates; the frame must not).</summary>
        public const string Stencil="NO ENTRY";
        private static readonly int[][][] StencilGlyphs=Glyphs(Stencil);
        private static int[][][] Glyphs(string text){var o=new int[text.Length][][];for(int i=0;i<text.Length;i++)o[i]=NativeExpeditionCityKit.GlyphBoxes(text[i]).ToArray();return o;}
        // Stencil letters on the curtain's party side (glyph cells `cell` m, centred at u, baseline v; letters run along +u).
        private static void Letters(List<ExpeditionKitBox> parts,int capacity,in ExpeditionFrame f,float u,float v,float d,float cell,int hex,float lift)
        {
            var text=StencilGlyphs;float width=text.Length*4*cell-cell;
            for(int i=0;i<text.Length;i++)for(int j=0;j<text[i].Length;j++)
            {
                var g=text[i][j];
                float cu=u-width/2+i*4*cell+(g[0]+g[2]*.5f)*cell,cv=v+(5-g[1]-g[3]*.5f)*cell;
                // The frame's u runs to the right of a viewer facing it from the party's side when F points at the party.
                Box(parts,capacity,f,-cu-g[2]*cell/2,-cu+g[2]*cell/2,cv-g[3]*cell/2,cv+g[3]*cell/2,d,d+.01f,NativeBlockLooks.Plain,0,hex,1,1,960+i,0,lift);
            }
        }
        private static void Sprite(NativeFxQuads q,in ExpeditionEye e,float x,float y,float z,double size,double r,double g,double b,double a,bool additive)
        {
            double h=size/2,ax=e.Rx*h,ay=e.Ry*h,az=e.Rz*h,bx=e.Ux*h,by=e.Uy*h,bz=e.Uz*h;double k=additive?1:a;
            q.Add(x-ax-bx,y-ay-by,z-az-bz,x+ax-bx,y+ay-by,z+az-bz,x+ax+bx,y+ay+by,z+az+bz,x-ax+bx,y-ay+by,z-az+bz,r*k,g*k,b*k,a,1,additive,1);
        }
    }
}
