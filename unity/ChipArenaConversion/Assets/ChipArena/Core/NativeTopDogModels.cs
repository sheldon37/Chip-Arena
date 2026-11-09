using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Top Dog's props, built in C# (docs/TOP-DOG.md "Looks"): the mallet, the wiggling hot dogs, the anvil, the banana peel, the cream pie, the
    /// spring, the boxing glove and its scissor arm, the mustard bottle, the sausage-link lasso, the dizzy stars and the intro's hot dog cart. Toon
    /// shapes in flat bright colours: the Runtime draws them in the pilots' own toon shader with its outline (NativeVisualBodyToon, the vertex
    /// colour as the colour), so they light and line as the characters do. Each builds into a <see cref="NativeFallenKit"/> (colours sRGB hex, made
    /// linear by the Runtime; `line` the outline). Unity's axes. Pure and deterministic.
    /// </summary>
    public static class NativeTopDogModels
    {
        public const uint Red=0xE2362C,RedDark=0xA8221C,Mustard=0xF4C430,MustardDark=0xC9921A,Cream=0xF6EBD0,Wood=0xD49A5E,WoodDark=0x9C6534,White=0xF4F1EA,
            Sausage=0xC8452E,SausageEnd=0x8E2E1F,Bun=0xE8B36A,BunTop=0xC98436,BunInner=0xF3DDB0,Ketchup=0xD22B22,
            Iron=0x4B4F57,IronLight=0x8A919C,IronDark=0x2A2D33,Peel=0xF5D53A,PeelInner=0xFBF0C2,PeelTip=0x5A3B1C,Tin=0xBFC3C9,Crust=0xE2A65A,Whip=0xFBFAF3,
            Cherry=0xD8142A,LinkAlt=0xBB3D29,Steel=0xC9CED6,Star=0xFFE14A,Ink=0x141218,GloveRed=0xD8262A,Umbrella=0xF2F0EA,Grill=0x2B2B30,Chrome=0xDCE1E8;
        private static readonly Vector3 X=Vector3.UnitX,Y=Vector3.UnitY,Z=Vector3.UnitZ;

        // ---- the mallet (held): its grip frame, the handle up +y from the hand, the head's faces fore and aft along z ----
        /// <summary>Where the head's middle sits up the handle, its radius and half length (m).</summary>
        public const float HeadAt=.98f,HeadRadius=.2f,HeadHalf=.3f,HandleTop=.86f,HandleBottom=-.2f;
        public static NativeFallenKit Mallet(int tier=0)
        {
            var k=new NativeFallenKit();int seg=tier>0?10:18;
            // The handle: tan wood, a little fatter at the top, a red and white wrapped grip, a mustard knob.
            var hp=new List<Vector3>();var hr=new List<float>();for(int i=0;i<=8;i++){float u=i/8f;hp.Add(Y*(HandleBottom+.02f+(HandleTop-HandleBottom)*u));hr.Add(.025f+.007f*u);}
            k.Tube(hp,hr,seg,Wood);
            for(int i=0;i<6;i++){float y0=-.15f+i*.055f;k.Tube(new[]{Y*y0,Y*(y0+.05f)},.031f,seg,i%2==0?Red:White);}
            k.Ellipsoid(Y*(HandleBottom-.01f),X*.046f,Z*.046f,Y*.04f,seg,tier>0?6:9,Mustard);
            // The head: a red barrel, mustard bands near each end, domed cream faces.
            var rings=new List<Vector3[]>();var cs=new List<Vector3>();float[] zs={-HeadHalf,-.24f,-.12f,0,.12f,.24f,HeadHalf};float[] rs={.17f,.188f,.198f,.2f,.198f,.188f,.17f};
            for(int i=0;i<zs.Length;i++){var c=Y*HeadAt+Z*zs[i];rings.Add(NativeFallenKit.Ring(c,X,Y,rs[i],rs[i],seg+6));cs.Add(c);}
            k.Loft(rings,cs,Red);
            foreach(float s in new[]{-1f,1})
            {
                float zb=s*.215f;k.Tube(new[]{Y*HeadAt+Z*(zb-.03f),Y*HeadAt+Z*(zb+.03f)},.207f,seg+6,Mustard);
                // The face: a low dome.
                var dome=new List<Vector3[]>();var dc=new List<Vector3>();for(int j=0;j<=4;j++){float u=j/4f,r=.168f*(float)Math.Cos(u*Math.PI/2)+.001f,z=s*(HeadHalf+.035f*(float)Math.Sin(u*Math.PI/2));var c=Y*HeadAt+Z*z;dome.Add(NativeFallenKit.Ring(c,X,Y,r,r,seg+6));dc.Add(c-Z*s*.001f*(4-j));}
                k.Loft(dome,dc,Cream,capLast:true);
            }
            k.Finish();return k;
        }
        /// <summary>The middle of the mallet's striking face, in its grip frame (the leading face is +z).</summary>
        public static Vector3 MalletFace=>Y*HeadAt+Z*(HeadHalf+.035f);

        // ---- a hot dog: in a bun, mustard on top, its sausage wiggling (alive) ----
        public const float DogLength=.26f,SausageRadius=.03f;
        /// <summary>A hot dog along +z (its middle at the origin, up +y), wiggling by `amp` (0 still, 1 squirming) at `phase` (rad). Only the part
        /// from `from` (0, the back end) to `to` (1, the front) is built, so one going into a mouth can lose its front as it goes.</summary>
        public static void HotDog(NativeFallenKit k,Matrix4x4 m,float phase,float amp,float from=0,float to=1,int tier=0)
        {
            var d=new NativeFallenKit();int n=tier>0?9:16,seg=tier>0?7:11;from=Math.Max(0,Math.Min(1,from));to=Math.Max(from+.02f,Math.Min(1,to));
            Vector3 Centre(float s,float share){float w=amp*share*(float)Math.Sin(phase+s*7.5f)*(.35f+.65f*(float)Math.Sin(Math.PI*s));float h=amp*share*.35f*(float)Math.Sin(phase*1.3f+s*5f+1);return new Vector3(w*.045f,h*.02f,(s-.5f)*DogLength);}
            var pts=new List<Vector3>();var rad=new List<float>();var cs=new List<float>();
            for(int i=0;i<=n;i++){float s=from+(to-from)*i/n;pts.Add(Centre(s,1));float end=Math.Min(s,1-s)/.12f;rad.Add(SausageRadius*(float)Math.Sqrt(Math.Max(.05f,Math.Min(1,end))));cs.Add(s);}
            d.Tube(pts,rad,seg,Sausage);
            // The bun: two soft halves under and either side of it, following its squirm a little less.
            float bf=Math.Max(from,.08f),bt=Math.Min(to,.92f);
            if(bt>bf+.02f)foreach(float side in new[]{-1f,1})
            {
                var rings=new List<Vector3[]>();var cc=new List<Vector3>();
                for(int i=0;i<=n;i++){float s=bf+(bt-bf)*i/n;var c=Centre(s,.45f)+new Vector3(side*.024f,-.012f,0);float end=Math.Min(s-.08f,.92f-s)/.1f;float r=(float)Math.Sqrt(Math.Max(.03f,Math.Min(1,end)));rings.Add(NativeFallenKit.Ring(c,X,Y,.026f*r,.036f*r,seg));cc.Add(c);}
                d.Loft(rings,cc,Bun,capFirst:true,capLast:true);
            }
            // The mustard: a zigzag along the top.
            var mp=new List<Vector3>();int zigs=tier>0?6:10;float mf=Math.Max(from,.1f),mt=Math.Min(to,.9f);
            if(mt>mf+.02f){for(int i=0;i<=zigs*2;i++){float s=mf+(mt-mf)*i/(zigs*2f);var c=Centre(s,1);mp.Add(c+new Vector3((i%2==0?-1:1)*.017f,SausageRadius*.92f,0));}d.Tube(NativeFallenKit.Curve(mp,3),.0055f,tier>0?4:6,Mustard,line:false);}
            d.Finish();k.Add(d,m);
        }

        // ---- the anvil: its feet on y=0, its horn toward +z ----
        public const float AnvilHeight=.5f,AnvilLength=.82f;
        // ---- a grand piano (Hammerspace, Core NativeDogChips): its case, the lid propped open, the keys, three legs; the keyboard to -z ----
        public const float PianoTop=1.02f,PianoLength=1.7f,PianoWidth=1.45f;
        public static NativeFallenKit Piano(int tier=0)
        {
            var k=new NativeFallenKit();int seg=tier>0?10:20;
            // The case's outline seen from above: straight on the left and the keyboard's side, the bentside curving round to the tail.
            var outline=new List<Vector3>();float w=PianoWidth,l=PianoLength;
            outline.Add(new Vector3(-w/2,0,-l/2));outline.Add(new Vector3(w/2,0,-l/2));
            for(int i=1;i<=seg;i++){float u=i/(float)seg;float x=w/2-w*.62f*(1-(float)Math.Cos(u*Math.PI*.5f))-w*.2f*u*u,z=-l/2+l*.55f*(float)Math.Sin(u*Math.PI*.5f)+l*.45f*u*u;outline.Add(new Vector3(x,0,z));}
            outline.Add(new Vector3(-w/2,0,l/2));
            Vector3[] At(float y,float s){var r=new Vector3[outline.Count];for(int i=0;i<r.Length;i++)r[i]=new Vector3(outline[i].X*s,y,outline[i].Z*s);return r;}
            var c0=Y*.62f;var c1=Y*PianoTop;
            k.Loft(new List<Vector3[]>{At(.62f,.98f),At(.65f,1),At(PianoTop-.03f,1),At(PianoTop,.99f)},new[]{c0,Y*.65f,Y*(PianoTop-.03f),c1},Ink,capFirst:true,capLast:true);
            // The lid, hinged on the straight side, propped up.
            var hinge=new Vector3(-w/2,PianoTop,0);var tilt=Matrix4x4.CreateFromAxisAngle(Z,.55f);
            Vector3[] Lid(float y){var r=At(0,1);for(int i=0;i<r.Length;i++){var p=r[i]-new Vector3(-w/2,0,0)+Y*y;r[i]=Vector3.Transform(p,tilt)+hinge;}return r;}
            k.Loft(new List<Vector3[]>{Lid(0),Lid(.03f)},new[]{Vector3.Transform(Y*0,tilt)+hinge,Vector3.Transform(Y*.03f,tilt)+hinge},Ink,capFirst:true,capLast:true);
            k.Tube(new[]{new Vector3(w*.1f,PianoTop,0),Vector3.Transform(new Vector3(w*.62f,0,0),tilt)+hinge},.018f,6,Wood);
            // The keyboard: a white slab with the black keys on it, and its cheeks.
            float kz=-l/2-.16f,ky=.84f;
            k.Loft(new List<Vector3[]>{NativeFallenKit.Ring(new Vector3(0,ky-.05f,kz),X,Z,w*.47f,.15f,seg,8),NativeFallenKit.Ring(new Vector3(0,ky,kz),X,Z,w*.47f,.15f,seg,8)},new[]{new Vector3(0,ky-.05f,kz),new Vector3(0,ky,kz)},White,capFirst:true,capLast:true);
            int keys=tier>0?8:15;for(int i=0;i<keys;i++){if(i%7==2||i%7==6)continue;float x=-w*.44f+w*.88f*(i+.75f)/keys;var c=new Vector3(x,ky+.012f,kz+.04f);k.Loft(new List<Vector3[]>{NativeFallenKit.Ring(c,X,Z,.018f,.08f,6,6),NativeFallenKit.Ring(c+Y*.025f,X,Z,.018f,.08f,6,6)},new[]{c,c+Y*.025f},Ink,capLast:true);}
            for(int s=-1;s<=1;s+=2){var c=new Vector3(s*w*.49f,ky,kz);k.Loft(new List<Vector3[]>{NativeFallenKit.Ring(c-Y*.08f,X,Z,.035f,.16f,6,8),NativeFallenKit.Ring(c+Y*.06f,X,Z,.035f,.16f,6,8)},new[]{c-Y*.08f,c+Y*.06f},Ink,capFirst:true,capLast:true);}
            // Three legs, turned, with their brass castors.
            foreach(var at in new[]{new Vector3(-w*.4f,0,-l*.42f),new Vector3(w*.4f,0,-l*.42f),new Vector3(-w*.25f,0,l*.38f)})
            {var pts=new List<Vector3>{at+Y*.04f,at+Y*.2f,at+Y*.45f,at+Y*.62f};k.Tube(pts,new[]{.035f,.05f,.065f,.07f},tier>0?6:10,Ink);k.Ellipsoid(at+Y*.03f,X*.045f,Y*.03f,Z*.045f,8,5,Mustard);}
            k.Finish();return k;
        }
        public static NativeFallenKit Anvil(int tier=0)
        {
            var k=new NativeFallenKit();int seg=tier>0?16:28;
            Vector3[] R(float y,float x,float z,float p)=>NativeFallenKit.Ring(Y*y,X,Z,x,z,seg,p);
            // Foot, waist and the face block, rounded rectangles.
            var rings=new List<Vector3[]>{R(0,.17f,.27f,6),R(.06f,.17f,.27f,6),R(.1f,.11f,.18f,5),R(.2f,.08f,.13f,4),R(.29f,.1f,.17f,5),R(.31f,.15f,.31f,8),R(AnvilHeight-.02f,.15f,.31f,8),R(AnvilHeight,.14f,.3f,8)};
            var cs=new List<Vector3>{Y*0,Y*.06f,Y*.1f,Y*.2f,Y*.29f,Y*.31f,Y*(AnvilHeight-.02f),Y*AnvilHeight};
            k.Loft(rings,cs,Iron,capFirst:true);k.Cap(R(AnvilHeight,.14f,.3f,8),Y*AnvilHeight,Y,IronLight);
            // The horn: a tapering, slightly drooping cone off the front.
            var hp=new List<Vector3>();var hr=new List<float>();for(int i=0;i<=8;i++){float u=i/8f;hp.Add(new Vector3(0,.41f-.05f*u*u,.29f+.3f*u));hr.Add(.085f*(1-u)+.006f);}
            k.Tube(hp,hr,tier>0?8:14,Iron);
            // The heel.
            k.Loft(new List<Vector3[]>{NativeFallenKit.Ring(new Vector3(0,.4f,-.3f),X,Y,.12f,.08f,seg,6),NativeFallenKit.Ring(new Vector3(0,.41f,-.39f),X,Y,.1f,.065f,seg,6)},new[]{new Vector3(0,.4f,-.3f),new Vector3(0,.41f,-.39f)},Iron,capLast:true);
            k.Finish();return k;
        }

        // ---- a banana peel, lying open on the floor (y=0) ----
        public static NativeFallenKit BananaPeel(int tier=0)
        {
            var k=new NativeFallenKit();var r=new NativeFallenKit.Rand(5);int per=tier>0?3:5;
            k.Ellipsoid(Y*.035f,X*.035f,Z*.035f,Y*.035f,tier>0?8:12,tier>0?5:8,Peel);
            k.Tube(new[]{Y*.06f,new Vector3(.005f,.1f,.01f),new Vector3(.02f,.115f,.02f)},.009f,6,PeelTip);
            for(int i=0;i<4;i++)
            {
                float a=i*(float)Math.PI/2+.3f+r.Range(-.2f,.2f),len=r.Range(.2f,.25f);var dir=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));var side=new Vector3(-dir.Z,0,dir.X);
                var pts=new List<Vector3>{Y*.05f+dir*.02f,Y*.03f+dir*(len*.35f),Y*.012f+dir*(len*.7f),Y*.02f+dir*len};var cur=NativeFallenKit.Curve(pts,per);
                var across=new List<Vector3>();var w=new List<float>();var w2=new List<float>();for(int j=0;j<cur.Count;j++){float u=j/(cur.Count-1f);across.Add(side);float ww=.038f*(float)Math.Sin(Math.PI*Math.Min(1,u*1.05f+.05f))+.004f;w.Add(ww);w2.Add(ww*.78f);}
                k.Quill(cur,across,w,Peel,depth:.12f);var inner=new List<Vector3>();foreach(var p in cur)inner.Add(p+Y*.0045f);k.Quill(inner,across,w2,PeelInner,line:false,depth:.08f);
                k.Ellipsoid(cur[cur.Count-1]+Y*.004f,dir*.012f,side*.012f,Y*.007f,6,4,PeelTip);
            }
            k.Finish();return k;
        }

        // ---- a cream pie in its tin: the tin's foot on y=0, the cream up +y ----
        public const float PieRadius=.16f;
        public static NativeFallenKit Pie(int tier=0)
        {
            var k=new NativeFallenKit();int seg=tier>0?20:36;
            Vector3[] Flute(float y,float r,float amp){var ring=NativeFallenKit.Ring(Y*y,X,Z,r,r,seg);for(int i=0;i<seg;i++){float a=i*2*(float)Math.PI/seg;ring[i]=Y*y+(ring[i]-Y*y)*(1+amp*(float)Math.Cos(a*12));}return ring;}
            k.Loft(new List<Vector3[]>{Flute(0,.125f,0),Flute(.012f,.135f,.03f),Flute(.045f,PieRadius,.04f)},new[]{Y*0,Y*.012f,Y*.045f},Tin,capFirst:true);
            k.Torus(Y*.05f,Y,.15f,.019f,seg,tier>0?6:9,Crust);
            k.Ellipsoid(Y*.048f,X*.145f,Z*.145f,Y*.05f,seg,tier>0?6:10,Whip);
            for(int i=0;i<7;i++){float a=i*2*(float)Math.PI/7;var c=new Vector3((float)Math.Cos(a)*.1f,.075f,(float)Math.Sin(a)*.1f);k.Ellipsoid(c,X*.03f,Z*.03f,Y*.025f,10,6,Whip);}
            k.Ellipsoid(Y*.115f,X*.022f,Z*.022f,Y*.022f,10,7,Cherry);k.Tube(new[]{Y*.135f,new Vector3(.01f,.16f,0),new Vector3(.02f,.17f,0)},.003f,4,PeelTip);
            k.Finish();return k;
        }

        // ---- the spring under the feet (Q): its foot on y=0, `height` tall ----
        public static void Spring(NativeFallenKit k,Matrix4x4 m,float height,int tier=0)
        {
            var d=new NativeFallenKit();int coils=6,per=tier>0?10:18;var pts=new List<Vector3>();height=Math.Max(.05f,height);
            for(int i=0;i<=coils*per;i++){float u=i/(float)(coils*per),a=u*coils*2*(float)Math.PI;pts.Add(new Vector3((float)Math.Cos(a)*.16f,.03f+u*(height-.06f),(float)Math.Sin(a)*.16f));}
            d.Tube(pts,.017f,tier>0?5:8,Steel,caps:true);
            Disc(d,0,.03f,.24f,Red,tier);Disc(d,height-.03f,height,.21f,Mustard,tier);
            d.Finish();k.Add(d,m);
        }
        private static void Disc(NativeFallenKit k,float y0,float y1,float r,uint color,int tier)
        {int seg=tier>0?14:24;k.Loft(new List<Vector3[]>{NativeFallenKit.Ring(Y*y0,X,Z,r,r,seg),NativeFallenKit.Ring(Y*y1,X,Z,r,r,seg)},new[]{Y*y0,Y*y1},color,capFirst:true,capLast:true);}

        // ---- the boxing glove on its scissor arm (4): the fist toward +z at the origin, the arm `reach` long behind it ----
        public static NativeFallenKit Glove(int tier=0)
        {
            var k=new NativeFallenKit();int u=tier>0?12:20,v=tier>0?8:12;
            k.Ellipsoid(Z*.02f,X*.11f,Y*.1f,Z*.12f,u,v,GloveRed);
            k.Ellipsoid(new Vector3(-.085f,-.02f,.04f),X*.045f,Y*.05f,Z*.07f,u/2+2,v/2+2,GloveRed);
            k.Ellipsoid(new Vector3(0,.06f,.02f),X*.07f,Y*.03f,Z*.08f,u/2+2,v/2+2,GloveRed);
            k.Tube(new[]{Z*-.09f,Z*-.17f},.082f,u,White);k.Torus(Z*-.17f,Z,.082f,.012f,u,6,White);
            k.Finish();return k;
        }
        /// <summary>The scissor arm from the glove's cuff back `reach` metres: crossed yellow bars that open as it stretches.</summary>
        public static void ScissorArm(NativeFallenKit k,Matrix4x4 m,float reach,int tier=0)
        {
            var d=new NativeFallenKit();reach=Math.Max(.1f,reach);int links=Math.Max(3,(int)Math.Ceiling(reach/.12f));const float Bar=.15f;float step=reach/links,half=Math.Max(.012f,(float)Math.Sqrt(Math.Max(0,Bar*Bar-step*step))*.5f);
            for(int i=0;i<links;i++){float z0=-.17f-i*step,z1=z0-step;d.Tube(new[]{new Vector3(-half,0,z0),new Vector3(half,0,z1)},.011f,tier>0?4:6,Mustard);d.Tube(new[]{new Vector3(half,0,z0),new Vector3(-half,0,z1)},.011f,tier>0?4:6,Mustard);}
            d.Finish();k.Add(d,m);
        }

        // ---- the mustard bottle (3): its foot on y=0, the nozzle up ----
        public const float BottleTip=.29f;
        public static NativeFallenKit Bottle(int tier=0)
        {
            var k=new NativeFallenKit();int seg=tier>0?12:20;float[] ys={0,.01f,.08f,.15f,.18f,.2f};float[] rs={.042f,.047f,.043f,.047f,.04f,.024f};
            var rings=new List<Vector3[]>();var cs=new List<Vector3>();for(int i=0;i<ys.Length;i++){rings.Add(NativeFallenKit.Ring(Y*ys[i],X,Z,rs[i],rs[i],seg));cs.Add(Y*ys[i]);}k.Loft(rings,cs,Mustard,capFirst:true);
            k.Loft(new List<Vector3[]>{NativeFallenKit.Ring(Y*.2f,X,Z,.026f,.026f,seg),NativeFallenKit.Ring(Y*.23f,X,Z,.024f,.024f,seg),NativeFallenKit.Ring(Y*.27f,X,Z,.007f,.007f,seg),NativeFallenKit.Ring(Y*BottleTip,X,Z,.005f,.005f,seg)},new[]{Y*.2f,Y*.23f,Y*.27f,Y*BottleTip},Red,capLast:true);
            k.Finish();return k;
        }

        // ---- sausage links along a line of points (the lasso; each span one link) ----
        public static void Links(NativeFallenKit k,IList<Vector3> knots,int tier=0)
        {
            var d=new NativeFallenKit();int seg=tier>0?6:9,per=tier>0?4:7;
            for(int i=0;i+1<knots.Count;i++)
            {
                var a=knots[i];var b=knots[i+1];if(Vector3.DistanceSquared(a,b)<1e-6f)continue;var pts=new List<Vector3>();var r=new List<float>();
                for(int j=0;j<=per;j++){float u=j/(float)per;pts.Add(Vector3.Lerp(a,b,u));r.Add(.006f+.026f*(float)Math.Pow(Math.Sin(Math.PI*u),.6f));}
                d.Tube(pts,r,seg,i%2==0?Sausage:LinkAlt);
            }
            d.Finish();k.Add(d,Matrix4x4.Identity);
        }

        // ---- a dizzy star (in the xy plane, facing +z) ----
        public static NativeFallenKit StarShape(int tier=0)
        {
            var k=new NativeFallenKit();var front=new Vector3[10];var back=new Vector3[10];
            for(int i=0;i<10;i++){float a=(float)Math.PI/2+i*(float)Math.PI/5,r=i%2==0?.08f:.036f;front[i]=new Vector3((float)Math.Cos(a)*r,(float)Math.Sin(a)*r,.012f);back[i]=new Vector3(front[i].X,front[i].Y,-.012f);}
            k.Loft(new List<Vector3[]>{back,front},new[]{Z*-.012f,Z*.012f},Star,capFirst:true,capLast:true);k.Finish();return k;
        }

        // ---- the intro's hot dog cart: its wheels on y=0, its long side along x, the open front toward +z ----
        public const float CartTop=.95f,CartHalf=.55f;
        /// <summary>The cart with its lid open by `lid` (0 shut, 1 thrown back) and its umbrella.</summary>
        public static NativeFallenKit Cart(float lid,int tier=0)
        {
            var k=new NativeFallenKit();int seg=tier>0?12:22;
            // The body: a rounded box in red with a white band, chrome trim, on two big wheels and a front leg.
            Vector3[] R(float y,float hx,float hz)=>NativeFallenKit.Ring(Y*y,X,Z,hx,hz,seg+10,7);
            k.Loft(new List<Vector3[]>{R(.32f,CartHalf,.3f),R(.36f,CartHalf+.02f,.32f),R(.62f,CartHalf+.02f,.32f)},new[]{Y*.32f,Y*.36f,Y*.62f},Red,capFirst:true);
            k.Loft(new List<Vector3[]>{R(.62f,CartHalf+.02f,.32f),R(.74f,CartHalf+.02f,.32f)},new[]{Y*.62f,Y*.74f},White);
            k.Loft(new List<Vector3[]>{R(.74f,CartHalf+.02f,.32f),R(CartTop-.04f,CartHalf+.02f,.32f),R(CartTop,CartHalf,.3f)},new[]{Y*.74f,Y*(CartTop-.04f),Y*CartTop},Red);
            foreach(float s in new[]{-1f,1}){k.Torus(new Vector3(s*.3f,.26f,.36f),Z,.24f,.035f,seg,8,IronDark);k.Ellipsoid(new Vector3(s*.3f,.26f,.37f),X*.06f,Y*.06f,Z*.03f,10,6,Chrome);for(int sp=0;sp<6;sp++){float a=sp*(float)Math.PI/3;k.Tube(new[]{new Vector3(s*.3f,.26f,.37f),new Vector3(s*.3f+(float)Math.Cos(a)*.21f,.26f+(float)Math.Sin(a)*.21f,.37f)},.008f,4,Chrome);}}
            k.Tube(new[]{new Vector3(-.45f,.32f,-.2f),new Vector3(-.47f,0,-.22f)},.02f,6,Chrome);k.Tube(new[]{new Vector3(.45f,.32f,-.2f),new Vector3(.47f,0,-.22f)},.02f,6,Chrome);
            // The handle bar off one end.
            k.Tube(new[]{new Vector3(CartHalf,.8f,-.2f),new Vector3(CartHalf+.35f,.92f,-.2f),new Vector3(CartHalf+.35f,.92f,.2f),new Vector3(CartHalf,.8f,.2f)},.018f,6,Chrome,caps:true);
            // The grill on top, sausages on it, and the lid on a hinge at the back.
            k.Loft(new List<Vector3[]>{R(CartTop,CartHalf-.06f,.24f),R(CartTop+.015f,CartHalf-.06f,.24f)},new[]{Y*CartTop,Y*(CartTop+.015f)},Grill,capLast:true);
            for(int i=0;i<5;i++)HotDog(k,Matrix4x4.CreateRotationY((float)Math.PI/2)*Matrix4x4.CreateTranslation(-.32f+i*.16f,CartTop+.045f,.02f*(i%2)),i*1.3f,0,0,1,tier);
            float open=lid*1.9f;var hinge=new Vector3(0,CartTop+.015f,-.24f);var lm=Matrix4x4.CreateTranslation(0,0,.24f)*Matrix4x4.CreateRotationX(-open)*Matrix4x4.CreateTranslation(hinge);
            var l=new NativeFallenKit();var dome=new List<Vector3[]>();var dc=new List<Vector3>();
            foreach(float sx in new[]{-1f,1}){var ring=new Vector3[13];for(int j=0;j<13;j++){float a=j/12f*(float)Math.PI;ring[j]=new Vector3(sx*(CartHalf-.05f),(float)Math.Sin(a)*.12f,-(float)Math.Cos(a)*.24f);}dome.Add(ring);dc.Add(new Vector3(sx*(CartHalf-.05f),.04f,0));}
            l.Loft(dome,dc,Chrome,capFirst:true,capLast:true);l.Tube(new[]{new Vector3(-.12f,.14f,0),new Vector3(-.12f,.17f,0),new Vector3(.12f,.17f,0),new Vector3(.12f,.14f,0)},.012f,6,IronDark);l.Finish();k.Add(l,lm);
            // The condiments at one end.
            var b=Bottle(tier);k.Add(b,Matrix4x4.CreateTranslation(.42f,CartTop,.17f));var kb=Bottle(tier);for(int i=0;i<kb.Color.Count;i++)if(kb.Color[i]==Mustard)kb.Color[i]=Ketchup;else if(kb.Color[i]==Red)kb.Color[i]=White;k.Add(kb,Matrix4x4.CreateTranslation(.42f,CartTop,.05f));
            // The umbrella: a pole and a striped canopy.
            k.Tube(new[]{new Vector3(-.4f,CartTop,-.22f),new Vector3(-.4f,2.15f,-.22f)},.018f,6,Chrome);
            var top=new Vector3(-.4f,2.3f,-.22f);int ribs=12;
            for(int i=0;i<ribs;i++)
            {
                float a0=i*2*(float)Math.PI/ribs,a1=(i+1)*2*(float)Math.PI/ribs;var p0=top+new Vector3((float)Math.Cos(a0)*.95f,-.32f,(float)Math.Sin(a0)*.95f);var p1=top+new Vector3((float)Math.Cos(a1)*.95f,-.32f,(float)Math.Sin(a1)*.95f);
                var mid0=top+new Vector3((float)Math.Cos(a0)*.5f,-.12f,(float)Math.Sin(a0)*.5f);var mid1=top+new Vector3((float)Math.Cos(a1)*.5f,-.12f,(float)Math.Sin(a1)*.5f);
                uint c=i%2==0?Red:Umbrella;
                for(int face=0;face<2;face++){var o=face==0?Y:-Y;var dy=face==0?Vector3.Zero:-Y*.004f;int a=k.V(top+dy,c),bb=k.V(mid0+dy,c),cc=k.V(mid1+dy,c),d=k.V(p0+dy,c),e=k.V(p1+dy,c);k.Tri(a,bb,cc,o,false);k.Quad(bb,d,e,cc,o,false);}
            }
            k.Ellipsoid(top+Y*.02f,X*.035f,Z*.035f,Y*.035f,8,5,Mustard);
            k.Finish();return k;
        }
    }
}
