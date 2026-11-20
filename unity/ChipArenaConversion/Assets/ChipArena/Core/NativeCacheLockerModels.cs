using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The MERIDIAN CIVIL DEFENCE cache locker, CD-647, sculpted in code like the machines (Claude, 2026-11-19; plan R9 and W6;
    /// the board 3 lockers, F:/Chip Arena Development/scratch/meridian/board3/work/gc/cd647_locker.py, of which this is the game's
    /// form). A squat pressed-steel field locker, 0.90 x 0.55 m and at most 0.62 m tall, inside A2's 0.92 x 0.62 x 0.58 m block's
    /// plan and under the 0.63 m chest floor limit. Four grades of one locker, which part in greyscale as well as by colour:
    /// STANDARD olive with hazard-orange corners (black chevrons on them), RARE cobalt with chrome corners and a chrome carry bar,
    /// EPIC violet with glowing seams and an armoured spine, LEGENDARY the approved black-and-gold loaf with two black ribs, two
    /// gold bands, cast corner armour and a plinth.
    /// <para>The frame: the origin on the floor under its middle, its front toward -z (its back to the wall at +z); the runtime
    /// turns it by the anchor's yaw (NativeCacheOpenFx.ToWorld). Three models per tier: the body, the lid (about the hinge at
    /// (0, 0.45, 0.27)) and a latch (about its pivot at (0, 0.37, -0.287); drawn twice, at x = -0.33 and 0.33).</para>
    /// <para>Bones (the bank's convention): 0 the lit body; 1 the tinted lights (the lid's LED strip, EPIC's seams); 2 and 3 the
    /// lock lamps' faces in alternating pairs (LEGENDARY's strobe); -2 (NativeMachineModels.Glass) the tray's see-through glow.
    /// Every light is baked white and tinted per draw (the tier colour, the red after the shut). Engine-free and deterministic.</para>
    /// </summary>
    public static class NativeCacheLockerModels
    {
        public const string Prefix="cache-cd647-";
        public static string BodyId(NativeCacheTier t)=>bodyIds[NativeCacheTiers.Index(t)];
        public static string LidId(NativeCacheTier t)=>lidIds[NativeCacheTiers.Index(t)];
        public static string LatchId(NativeCacheTier t)=>latchIds[NativeCacheTiers.Index(t)];
        private static readonly string[] bodyIds={Prefix+"standard",Prefix+"rare",Prefix+"epic",Prefix+"legendary"},lidIds={Prefix+"lid-standard",Prefix+"lid-rare",Prefix+"lid-epic",Prefix+"lid-legendary"},
            latchIds={Prefix+"latch-standard",Prefix+"latch-rare",Prefix+"latch-epic",Prefix+"latch-legendary"};
        public const int Body=0,Lights=1,LampsA=2,LampsB=3,Glass=NativeMachineModels.Glass;
        /// <summary>The locker's size and marks (metres, its frame).</summary>
        public const float W=.90f,D=.55f,X0=-W/2,X1=W/2,Z0=-D/2,Z1=D/2,Skid=.06f,Body0=.06f,Body1=NativeCacheOpenFx.Body1,LidTop=NativeCacheOpenFx.LidTop;
        public static readonly Vector3 Hinge=new Vector3(0,Body1,Z1-.005f),LatchPivot=new Vector3(0,.37f,Z0-.012f);
        /// <summary>A2's solid block, 0.92 x 0.62 x 0.58 m (wide, tall, deep: R9's locker is "about 0.90 x 0.60 x 0.55 m"), centred
        /// on the anchor and standing on its floor, and the chest floor limit: the shut locker stays inside both.</summary>
        public static readonly Vector3 Block=new Vector3(.92f,.62f,.58f);public const float FloorLimit=.63f;
        /// <summary>The see-through glow's alpha (NativeSourceTransparent reads the colour alpha).</summary>
        public const float GlowAlpha=.6f;

        private static WeaponPaint S(int hex,float sheen=.03f)=>WeaponPaint.Solid(hex,sheen);
        private static readonly WeaponPaint Steel=S(0x6c717b,.05f),SteelDark=S(0x3c3f46,.03f),Rubber=S(0x141418,0),Void=S(0x060608,0),Tray=S(0x1b1d22,.02f),Cart=S(0x2a2d34,.03f),
            Galv=S(0x9aa0aa,.06f),Black=S(0x121216,.02f),PaintBlack=S(0x0b0b0d,0),Gold=S(0xd2a54a,.16f),Chrome=S(0xc9d0da,.2f);
        private static readonly WeaponPaint LightWhite=WeaponPaint.Glow(0xffffff,2.4f,.32f),FaceWhite=WeaponPaint.Glow(0xffffff,2.2f,.4f),StripWhite=WeaponPaint.Glow(0xffffff,1.4f,.18f),InnerWhite=WeaponPaint.Glow(0xffffff,2.2f,.35f);
        private struct Look{public WeaponPaint Body,BodyLight,BodyDark,Trim,Stencil;}
        private static Look LookOf(NativeCacheTier t)
        {
            switch(t)
            {
                case NativeCacheTier.Rare:return new Look{Body=S(0x22489a),BodyLight=S(0x2c58b2),BodyDark=S(0x172f66),Trim=Chrome,Stencil=S(0xe8eaf0,.05f)};
                case NativeCacheTier.Epic:return new Look{Body=S(0x46286e),BodyLight=S(0x56338a),BodyDark=S(0x2c1946),Trim=S(0x7a7f8c,.06f),Stencil=S(0xd9c8f2,.06f)};
                case NativeCacheTier.Legendary:return new Look{Body=S(0x17171c),BodyLight=S(0x22222a),BodyDark=S(0x0d0d10),Trim=Gold,Stencil=Gold};
                default:return new Look{Body=S(0x4d5733),BodyLight=S(0x5b6740),BodyDark=S(0x353d22),Trim=S(0xe0661e,.05f),Stencil=S(0xe6e2d4,.04f)};
            }
        }
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private static void B(WeaponSculpt s,in WeaponPaint c,float x0,float x1,float y0,float y1,float z0,float z1,float bevel=0)
            =>s.Box(c,V((x0+x1)*.5f,(y0+y1)*.5f,(z0+z1)*.5f),V(Math.Abs(x1-x0),Math.Abs(y1-y0),Math.Abs(z1-z0)),bevel);
        // A flat mark facing -z (the front) at depth z: half width hw, half height hh.
        private static void Front(WeaponSculpt s,in WeaponPaint c,float cx,float cy,float z,float hw,float hh)=>s.Decal(c,V(cx,cy,z),V(-hw,0,0),V(0,hh,0),.0008f);
        // A cylinder along z (a lamp, a disc facing -z) between z0 and z1; along x (a hinge pin); along y (a crest on the crown).
        private static void AlongZ(WeaponSculpt s,in WeaponPaint c,float r,float x,float y,float z0,float z1,int sides){s.Push(WeaponSculpt.T(x,y,0));s.Cylinder(c,r,z0,z1,sides);s.Pop();}
        private static void AlongX(WeaponSculpt s,in WeaponPaint c,float r,float y,float z,float x0,float x1,int sides){s.Push(WeaponSculpt.Ry((float)(Math.PI/2))*WeaponSculpt.T(0,y,z));s.Cylinder(c,r,x0,x1,sides);s.Pop();}
        private static void AlongY(WeaponSculpt s,in WeaponPaint c,float r,float x,float z,float y0,float y1,int sides){s.Push(WeaponSculpt.Rx(-(float)(Math.PI/2))*WeaponSculpt.T(x,0,z));s.Cylinder(c,r,y0,y1,sides);s.Pop();}
        private static void Clear(WeaponMesh mesh,float alpha,Action draw)
        {
            var section=mesh.Section(Glass,WeaponStretch.None);int from=section.Colors.Count;draw();
            for(int i=from;i<section.Colors.Count;i++){var c=section.Colors[i];section.Colors[i]=new Vector4(c.X,c.Y,c.Z,alpha);}
        }

        // ---- the body ----
        /// <summary>The locker's body for a tier (bones 0-3 and the tray's glow on -2).</summary>
        public static WeaponMesh BuildBody(NativeCacheTier tier)
        {
            var mesh=new WeaponMesh{Spec=BodyId(tier),Tier=0};var s=new WeaponSculpt(mesh,0);var k=LookOf(tier);bool leg=tier==NativeCacheTier.Legendary;
            const float zf=Z0;float tray=NativeCacheOpenFx.TrayY,wall=.029f;
            s.Into(Body);
            // the feet: skids on rubber feet, or (LEGENDARY) an armoured plinth frame on cast corner feet
            if(leg)
            {
                B(s,Steel,X0+.01f,X1-.01f,.012f,Skid,Z0+.01f,Z0+.05f);B(s,Steel,X0+.01f,X1-.01f,.012f,Skid,Z1-.05f,Z1-.01f);
                B(s,Steel,X0+.01f,X0+.05f,.012f,Skid,Z0+.05f,Z1-.05f);B(s,Steel,X1-.05f,X1-.01f,.012f,Skid,Z0+.05f,Z1-.05f);
                foreach(float cx in new[]{X0+.04f,X1-.04f})foreach(float cz in new[]{Z0+.04f,Z1-.04f})B(s,Black,cx-.045f,cx+.045f,0,Skid+.005f,cz-.045f,cz+.045f,.008f);
            }
            else
            {
                foreach(float sx in new[]{-.32f,.32f})
                {
                    B(s,Steel,sx-.028f,sx+.028f,.02f,Skid,Z0+.02f,Z1-.02f);
                    foreach(float sz in new[]{Z0+.05f,Z1-.05f})B(s,Rubber,sx-.03f,sx+.03f,0,.02f,sz-.03f,sz+.03f);
                }
            }
            // the body: a pressed box, the tray's floor inside it, walls 29 mm round the mouth
            B(s,k.Body,X0,X1,Body0,tray,Z0,Z1,leg?.006f:.014f);
            B(s,k.Body,X0,X1,tray,Body1,Z0,Z0+wall);B(s,k.Body,X0,X1,tray,Body1,Z1-wall,Z1);
            B(s,k.Body,X0,X0+wall,tray,Body1,Z0+wall,Z1-wall);B(s,k.Body,X1-wall,X1,tray,Body1,Z0+wall,Z1-wall);
            s.Decal(Tray,V(0,tray,0),V(X1-wall-.004f,0,0),V(0,0,-(Z1-wall-.004f)),.0015f);
            // the front: pressed ribs (EPIC: a line of light along each), or (LEGENDARY) a bolted armour plate
            float pz=zf;
            if(leg)
            {
                B(s,Black,X0+.07f,X1-.07f,Body0+.03f,Body1-.035f,zf-.005f,zf,.003f);pz=zf-.005f;
                foreach(float y in new[]{Body0+.045f,Body1-.05f})for(int i=0;i<6;i++){float x=X0+.085f+i*(W-.17f)/5;B(s,Steel,x-.006f,x+.006f,y-.006f,y+.006f,zf-.008f,zf-.005f);}
            }
            else foreach(float y in new[]{.108f,.136f,.164f})
            {
                B(s,k.BodyLight,-.40f,.40f,y-.009f,y+.009f,zf-.012f,zf,.004f);
                if(tier==NativeCacheTier.Epic){s.Into(Lights);B(s,LightWhite,-.39f,.39f,y-.0025f,y+.0025f,zf-.0135f,zf-.0115f);s.Into(Body);}
            }
            // the plate: riveted steel (LEGENDARY black) with the tier's word over CD-647, stencilled (a bar each), a rule under
            const float px0=-.34f,px1=.34f,py0=.186f,py1=.364f;
            B(s,leg?Black:Steel,px0,px1,py0,py1,pz-.006f,pz+.001f,.003f);
            foreach(var (rx,ry) in new[]{(px0+.016f,py0+.016f),(px1-.016f,py0+.016f),(px0+.016f,py1-.016f),(px1-.016f,py1-.016f)})AlongZ(s,leg?Gold:Galv,.007f,rx,ry,pz-.009f,pz-.006f,6);
            float zt=pz-.0062f,pxw=Math.Min(.0095f,.5f/(NativeCacheTiers.Name(tier).Length*6-1)),wx=leg?.03f:0,hw=.5f*(NativeCacheTiers.Name(tier).Length*6-1)*pxw;
            Front(s,k.Stencil,wx,py1-.016f-3.5f*pxw,zt,hw,3.5f*pxw);Front(s,k.Stencil,wx,py0+.016f+3.5f*.0095f,zt,.5f*35*.0095f,3.5f*.0095f);
            Front(s,k.Stencil,0,py0+.0885f,zt,(px1-px0)*.5f-.04f,.0025f);
            if(leg)AlongZ(s,Gold,.042f,px0+.062f,(py0+py1)*.5f+.01f,zt-.0015f,zt,16);
            // the roundel and the line MERIDIAN CIVIL DEFENCE . CD-647 under the plate (a bar at this size)
            AlongZ(s,k.Stencil,.022f,-.385f,.081f,pz-.003f,pz,12);AlongZ(s,k.Body,.016f,-.385f,.081f,pz-.0035f,pz-.003f,12);
            Front(s,k.Stencil,.03f,.068f,pz-.0008f,.27f,.004f);
            // the corners: folded caps in the trim (STANDARD black chevrons pointing up; EPIC a lit seam down each front cap), or
            // (LEGENDARY) cast corner armour
            if(leg)
            {
                foreach(float cx in new[]{X0,X1})foreach(float cz in new[]{Z0,Z1})
                {float sx=cx<0?1:-1,sz=cz<0?1:-1;B(s,Black,cx-sx*.006f,cx+sx*.065f,Body0,Body1-.01f,cz-sz*.006f,cz+sz*.065f);}
                // two gold service bands round the body, under the lid and over the plinth
                foreach(float y in new[]{Body0+.012f,Body1-.028f})
                {B(s,Gold,X0-.002f,X1+.002f,y,y+.016f,Z0-.012f,Z0-.008f);B(s,Gold,X0-.004f,X0,y,y+.016f,Z0-.008f,Z1-.07f);B(s,Gold,X1,X1+.004f,y,y+.016f,Z0-.008f,Z1-.07f);}
            }
            else foreach(float cx in new[]{X0,X1})foreach(float cz in new[]{Z0,Z1})
            {
                float sx=cx<0?1:-1,sz=cz<0?1:-1,y0=Body0+.004f,y1=Body1-.006f;const float w=.055f,t=.004f;
                B(s,k.Trim,cx,cx+sx*w,y0,y1,cz-sz*t,cz);B(s,k.Trim,cx-sx*t,cx,y0,y1,cz,cz+sz*w);
                if(cz>0)continue;float xa=Math.Min(cx,cx+sx*w),xb=Math.Max(cx,cx+sx*w),xm=(xa+xb)*.5f,zz=cz-t-.0006f;
                if(tier==NativeCacheTier.Standard)for(int c=0;c<3;c++)
                {
                    float yy=.14f+c*.085f;
                    s.Quad(PaintBlack,V(xa+.005f,yy,zz),V(xm,yy+.03f,zz),V(xm,yy+.045f,zz),V(xa+.005f,yy+.015f,zz));
                    s.Quad(PaintBlack,V(xm,yy+.03f,zz),V(xb-.005f,yy,zz),V(xb-.005f,yy+.015f,zz),V(xm,yy+.045f,zz));
                }
                if(tier==NativeCacheTier.Epic){s.Into(Lights);B(s,LightWhite,xm-.003f,xm+.003f,y0+.02f,y1-.02f,zz-.0015f,zz);s.Into(Body);}
            }
            // the sides: pressed louvres and handles (RARE's chrome), or (LEGENDARY) lifting rings on cast lugs
            foreach(float xs in new[]{X0,X1})
            {
                float sx=xs<0?-1:1;
                for(int i=0;i<5;i++){float y=.21f+i*.03f;B(s,Void,xs-.0005f*sx,xs+.0015f*sx,y,y+.012f,-.11f,.11f);B(s,k.BodyLight,xs,xs+.01f*sx,y+.012f,y+.02f,-.115f,.115f);}
                foreach(float zz in new[]{-.13f,.13f})B(s,Steel,xs,xs+sx*.007f,leg?.33f:.335f,leg?.38f:.375f,zz-(leg?.03f:.018f),zz+(leg?.03f:.018f));
                if(leg)foreach(float zz in new[]{-.13f,.13f}){s.Push(WeaponSculpt.Ry((float)(Math.PI/2))*WeaponSculpt.T(xs+sx*.003f,.32f,zz));s.Torus(Gold,Vector3.Zero,.035f,.006f,8,3);s.Pop();}
                else{var h=tier==NativeCacheTier.Rare?Chrome:Rubber;B(s,h,xs+sx*.003f,xs+sx*.0095f,.28f,.296f,-.14f,.14f);
                    B(s,h,xs+sx*.003f,xs+sx*.0095f,.28f,.362f,-.14f,-.124f);B(s,h,xs+sx*.003f,xs+sx*.0095f,.28f,.362f,.124f,.14f);}
            }
            // the hinges at the back
            foreach(float hx in new[]{-.29f,.29f}){AlongX(s,Steel,leg?.014f:.012f,Hinge.Y,Z1-.002f,hx-.06f,hx+.06f,8);B(s,Steel,hx-.055f,hx+.055f,Body1-.05f,Body1-.008f,Z1,Z1+.006f);}
            // the tray: three cartridges in their slots (the slots' glow and each cartridge's light stripe on the glass bone)
            foreach(float sx in NativeCacheOpenFx.Slots){B(s,Cart,sx-.075f,sx+.075f,tray+.004f,tray+.089f,-.011f,.011f);B(s,Steel,sx-.055f,sx+.055f,tray+.089f,tray+.093f,-.007f,.007f);}
            Clear(mesh,GlowAlpha,()=>{s.Into(Glass);foreach(float sx in NativeCacheOpenFx.Slots){s.Decal(InnerWhite,V(sx,tray+.002f,0),V(.095f,0,0),V(0,0,-.025f),.0015f);Front(s,InnerWhite,sx,tray+.061f,-.0125f,.063f,.011f);}});
            // the lock lamps in their wire guards: one per grade (LEGENDARY's behind a guarded black bar with gold bars)
            int n=NativeCacheTiers.Lamps(tier);
            if(leg)
            {
                s.Into(Body);B(s,Black,-.205f,.205f,.382f,.43f,zf-.01f,zf,.003f);
                for(int i=0;i<=n;i++){float x=NativeCacheOpenFx.LampX(tier,0)-.045f+i*.09f;B(s,Gold,x-.004f,x+.004f,.38f,.432f,zf-.0145f,zf-.01f);}
                B(s,Gold,-.205f,.205f,.427f,.433f,zf-.0145f,zf-.01f);
                for(int i=0;i<n;i++){s.Into(i%2==0?LampsA:LampsB);AlongZ(s,FaceWhite,.017f,NativeCacheOpenFx.LampX(tier,i),NativeCacheOpenFx.LampY,zf-.012f,zf-.01f,12);}
            }
            else for(int i=0;i<n;i++)
            {
                float x=NativeCacheOpenFx.LampX(tier,i);s.Into(Body);
                AlongZ(s,Steel,.026f,x,NativeCacheOpenFx.LampY,zf-.01f,zf,10);B(s,Galv,x-.03f,x+.03f,NativeCacheOpenFx.LampY-.0025f,NativeCacheOpenFx.LampY+.0025f,zf-.0145f,zf-.0125f);
                B(s,Galv,x-.0025f,x+.0025f,NativeCacheOpenFx.LampY-.03f,NativeCacheOpenFx.LampY+.03f,zf-.0145f,zf-.0125f);
                s.Into(i%2==0?LampsA:LampsB);AlongZ(s,FaceWhite,.02f,x,NativeCacheOpenFx.LampY,zf-.012f,zf-.01f,12);
            }
            return mesh;
        }

        // ---- the lid (its hinge bone): the loaf, its LED strip, the tier's crown ----
        /// <summary>The loaf's section in (z, y): its front lip, the rounded shoulders, the crown, the back's fall to the hinge,
        /// closed along its underside.</summary>
        public static List<Vector2> LidProfile()
        {
            var pts=new List<Vector2>{new Vector2(Z0-.012f,Body1-.02f),new Vector2(Z0-.012f,Body1+.035f)};const int n=6;
            float r=.11f,cz=Z0-.012f+r,cy=Body1+.035f;
            for(int i=1;i<=n;i++){double a=Math.PI-Math.PI/2*i/n;pts.Add(new Vector2(cz+(float)Math.Cos(a)*r,cy+(float)Math.Sin(a)*(LidTop-Body1-.035f)));}
            float r2=.16f,cz2=Z1+.006f-r2;
            for(int i=0;i<=n;i++){double a=Math.PI/2*(1-(double)i/n);pts.Add(new Vector2(cz2+(float)Math.Cos(a)*r2,Body1+.01f+(float)Math.Sin(a)*(LidTop-Body1-.01f)));}
            pts.Add(new Vector2(Z1+.006f,Body1-.005f));return pts;
        }
        // The profile as a slab outline (slab x = -z, y = y; counter-clockwise), grown outward by `grow`.
        private static Vector3[] Outline(float grow)
        {
            var p=LidProfile();var o=new Vector3[p.Count];for(int i=0;i<o.Length;i++)o[i]=new Vector3(-p[i].X,p[i].Y,0);
            if(grow==0)return o;var g=WeaponSculpt.Inset(o,-grow,0);
            // grown over the crown only as far as A2's block allows at the lip and the back (its depth, 0.58 m)
            for(int i=0;i<g.Length;i++)g[i].X=Math.Max(-(Block.Z*.5f-.001f),Math.Min(Block.Z*.5f-.001f,g[i].X));return g;
        }
        // A slab of the loaf's profile across x from x0 to x1 (slab z runs along the locker's x).
        private static void Loaf(WeaponSculpt s,in WeaponPaint side,in WeaponPaint caps,float x0,float x1,float grow,float bevel,WeaponPaint? under=null)
        {
            var o=Outline(grow);int last=o.Length-1;var sd=side;var un=under??side;
            s.Push(WeaponSculpt.Ry((float)(Math.PI/2)));s.Slab(i=>i==last?un:sd,caps,caps,o,x0,x1,bevel);s.Pop();
        }
        /// <summary>The lid for a tier (bone 0 its body, bone 1 its LED strip and EPIC's seams), in the locker frame, shut.</summary>
        public static WeaponMesh BuildLid(NativeCacheTier tier)
        {
            var mesh=new WeaponMesh{Spec=LidId(tier),Tier=0};var s=new WeaponSculpt(mesh,0);var k=LookOf(tier);bool leg=tier==NativeCacheTier.Legendary;
            s.Into(Body);Loaf(s,k.Body,k.BodyDark,X0-.006f,X1+.006f,0,.006f,SteelDark);
            // the lip's recessed LED strip: the runtime's price and rarity text's place (the same on every tier), and its keepers
            float lz=Z0-.012f;Front(s,Void,0,Body1+.011f,lz-.0008f,.2f,.017f);
            s.Into(Lights);Front(s,StripWhite,0,Body1+.011f,lz-.0016f,.194f,.013f);s.Into(Body);
            foreach(float sx in new[]{-.33f,.33f})B(s,Steel,sx-.028f,sx+.028f,Body1+.004f,Body1+.03f,lz-.003f,lz);
            if(tier==NativeCacheTier.Rare)
            {
                // the bright band round the lid's foot and the chrome carry bar on the crown (its silhouette)
                B(s,Chrome,X0-.009f,X1+.009f,Body1+.036f,Body1+.054f,lz-.002f,lz);B(s,Chrome,X0-.009f,X0-.006f,Body1+.036f,Body1+.054f,lz,Z1);B(s,Chrome,X1+.006f,X1+.009f,Body1+.036f,Body1+.054f,lz,Z1);
                foreach(float x in new[]{-.17f,.17f}){B(s,Steel,x-.02f,x+.02f,LidTop-.012f,LidTop+.002f,-.045f,.005f);B(s,Chrome,x-.009f,x+.009f,LidTop,LidTop+.028f,-.029f,-.011f);}
                B(s,Chrome,-.17f,.17f,LidTop+.02f,LidTop+.033f,-.029f,-.011f,.004f);
            }
            if(tier==NativeCacheTier.Epic)
            {
                // the lid's lit seam round its foot, the armoured spine over the crown with its own lit seam
                s.Into(Lights);B(s,LightWhite,X0-.007f,X1+.007f,Body1-.016f,Body1-.011f,lz-.0015f,lz);B(s,LightWhite,X0-.0075f,X0-.006f,Body1-.016f,Body1-.011f,lz,Z1);B(s,LightWhite,X1+.006f,X1+.0075f,Body1-.016f,Body1-.011f,lz,Z1);
                s.Into(Body);B(s,Steel,-.035f,.035f,LidTop-.004f,LidTop+.03f,-.17f,.11f,.006f);
                s.Into(Lights);B(s,LightWhite,-.006f,.006f,LidTop+.03f,LidTop+.033f,-.165f,.105f);s.Into(Body);
            }
            if(leg)
            {
                // the approved loaf, armoured: two raised black ribs over it, two gold bands strapped over the crown outside them,
                // and the flat stencil crest (the legendary chip's crown in a ring) on the crown's flat
                foreach(float xr in new[]{-.15f,.15f})Loaf(s,Black,Black,xr-.025f,xr+.025f,.022f,0);
                foreach(float xb in new[]{-.3f,.3f})Loaf(s,Gold,Gold,xb-.016f,xb+.016f,.006f,0);
                AlongY(s,Gold,.075f,0,-.035f,LidTop,LidTop+.0012f,14);AlongY(s,Black,.06f,0,-.035f,LidTop+.0012f,LidTop+.0016f,14);
                AlongY(s,Gold,.03f,0,-.035f,LidTop+.0016f,LidTop+.002f,5);
            }
            // the lid's corners: folded caps in the trim, or (LEGENDARY) cast armour caps
            foreach(float sx in new[]{-1f,1f})foreach(float sz in new[]{1f,-1f})
            {
                float xx=sx>0?X1+.006f:X0-.006f,zz=sz>0?Z0-.012f:Z1+.006f,xo=xx+sx*.003f,zo=zz-sz*.003f;
                if(leg)B(s,Black,Math.Min(xo,xx-sx*.07f),Math.Max(xo,xx-sx*.07f),Body1-.019f,Body1+.05f,Math.Min(zo,zz+sz*.07f),Math.Max(zo,zz+sz*.07f));
                else B(s,k.Trim,Math.Min(xo,xx-sx*.05f),Math.Max(xo,xx-sx*.05f),Body1-.019f,Body1+.045f,Math.Min(zo,zz+sz*.05f),Math.Max(zo,zz+sz*.05f));
            }
            return mesh;
        }
        /// <summary>A lever latch, centred on x = 0 (drawn at x = -0.33 and 0.33, about LatchPivot): its keeper plate and arm.</summary>
        public static WeaponMesh BuildLatch(NativeCacheTier tier)
        {
            var mesh=new WeaponMesh{Spec=LatchId(tier),Tier=0};var s=new WeaponSculpt(mesh,0);var k=LookOf(tier);const float zf=Z0;
            s.Into(Body);B(s,Steel,-.035f,.035f,.355f,.395f,zf-.005f,zf);
            var arm=tier==NativeCacheTier.Rare||tier==NativeCacheTier.Legendary?k.Trim:Steel;
            s.Push(WeaponSculpt.T(0,.4125f,zf-.0095f));s.Box(arm,Vector3.Zero,V(.045f,.07f,.008f),.002f);s.Pop();
            AlongX(s,Galv,.006f,.38f,zf-.007f,-.03f,.03f,6);
            return mesh;
        }
        /// <summary>Every model of every tier (id, mesh), for the bank and the checks.</summary>
        public static IEnumerable<(string id,WeaponMesh mesh)> All()
        {
            foreach(var t in NativeCacheTiers.All){yield return (BodyId(t),BuildBody(t));yield return (LidId(t),BuildLid(t));yield return (LatchId(t),BuildLatch(t));}
        }
        /// <summary>A tier's whole locker's triangles (the body, the lid and two latches).</summary>
        public static int Triangles(NativeCacheTier t)=>BuildBody(t).Triangles+BuildLid(t).Triangles+2*BuildLatch(t).Triangles;
    }
}
