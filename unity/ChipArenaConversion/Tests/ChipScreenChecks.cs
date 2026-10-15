using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

/// <summary>
/// The Expedition wrist chip screen (owner 2026-10-15: "we should also make it a holographic screen projected from the wrist. That way
/// its easier to notice were selecting a chip"; "The mouse should be taken over for the chip selection, we shouldnt have to hold alt to
/// click."; Core/NativeChipScreen.cs, Runtime/NativeChipScreenPresentation.cs, Runtime/NativeExpeditionChipScreen.cs): its place on the
/// display wrist's side, the unfold out of the wrist and the fold back, the cards on it, the HUD's canvas maps (the faces registered on the
/// world cards from the chooser shot) and the cursor picking the card it is over with no Alt, a pick's card flying into the hand by the
/// plant's contact, anyone else's screen as blank backs (no offer), the quad bound and no allocation, the light budget under the bloom
/// (owner 2026-10-15: "Holographic display kind of buggy, and display is odd. Can we make it like a better holographic display with no
/// artifacting?": the body's summed light under NativeBloom's threshold, read from the shader itself), no coplanar or stacked layers, the
/// red edge only for a real hit, and the runtime hooks (the screen replaces the small panel in an Expedition only; the pointer is the chooser
/// hold's, no Alt; the 2D tray where the screen cannot be read; the Boss Test untouched). Managed only: no Unity Editor or Windows play is run.
/// </summary>
internal static class ChipScreenChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("Chip screen: "+what);}
    private static readonly Vector3 Body=new Vector3(0,.865f,0);
    private static string rootDir;
    // A number the shader defines in its SHARED block (#define NAME value).
    private static float Define(string shader,string name)
    {
        var m=Regex.Match(shader,@"#define "+name+@"\s+\(?(-?[0-9.]+)\)?\s");Check(m.Success,"the shader defines "+name);
        return float.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);
    }
    public static void Run(string root)
    {
        rootDir=root;
        var log=new List<string>();Placement(log);Unfold(log);Cards(log);Maps(log);Pointer(log);Fly(log);Others(log);Budget(log);Light(root,log);Layers(log);Alert(root,log);Hooks(root,log);
        foreach(var line in log)Console.WriteLine("PASS chip screen: "+line);
    }

    // A sink that keeps every vertex of the stream (four vertices and two triangles a quad, in draw order).
    private sealed class Sink:INativeChipScreenSink
    {
        public readonly List<(Vector3 at,Vector4 a,Vector4 b,Vector4 c,Vector4 d,Vector4 e)> V=new List<(Vector3,Vector4,Vector4,Vector4,Vector4,Vector4)>();public int Triangles;
        public int Vertex(Vector3 at,Vector4 a,Vector4 b,Vector4 c,Vector4 d,Vector4 e){V.Add((at,a,b,c,d,e));return V.Count-1;}
        public void Triangle(int a,int b,int c){Triangles++;}
        public void Clear(){V.Clear();Triangles=0;}
        public int Count=>V.Count/4;
        public float Kind(int q)=>V[q*4].a.Z;
        public Vector3 Corner(int q,int k)=>V[q*4+k].at;
        public Vector3 Centre(int q)=>(V[q*4].at+V[q*4+2].at)*.5f;
        public Vector3 Normal(int q)=>Vector3.Normalize(Vector3.Cross(V[q*4+1].at-V[q*4].at,V[q*4+3].at-V[q*4].at));
        public int First(float kind){for(int q=0;q<Count;q++)if(Math.Abs(Kind(q)-kind)<.01f)return q;return -1;}
        public int Number(float kind){int n=0;for(int q=0;q<Count;q++)if(Math.Abs(Kind(q)-kind)<.01f)n++;return n;}
        // The screen quad's channels (NativeChipScreen.Build): count + 4 x mode, card share, lit, seed; the cards' visibility; tints.
        public (int count,int mode,float lit,Vector4 vis,Vector4 tints,float alert,float opacity) Screen(int q){var v=V[q*4];int cm=(int)Math.Round(v.c.X);return (cm%4,cm/4,v.c.Z,v.d,v.e,v.b.Z,v.a.W);}
    }
    private sealed class Counting:INativeChipScreenSink{public int N;public int Vertex(Vector3 at,Vector4 a,Vector4 b,Vector4 c,Vector4 d,Vector4 e)=>N++;public void Triangle(int a,int b,int c){}}
    private static ChipScreenLook Look(float unfold=1,bool mine=true)=>new ChipScreenLook{Unfold=unfold,Time=2.3f,Count=3,Lit=-1,Seed=NativeChipSelect.Seed(3,5),Mine=mine,C0=0x9aa3b5,C1=0x37e08a,C2=0x4aa8ff,FlyCard=true,Chosen=-1};
    private static readonly Vector3 Wrist=new Vector3(.2f,1,.3f);

    // ---- the place: ahead on the display wrist's side at chest height, turned to the other front quarter ----
    private static void Placement(List<string> log)
    {
        foreach(float facing in new[]{0f,90f,-135f})foreach(bool left in new[]{true,false})
        {
            var p=NativeChipScreen.Place(Body,facing,left,1);double y=facing*Math.PI/180;var ahead=new Vector3((float)Math.Sin(y),0,(float)Math.Cos(y));var right=new Vector3(ahead.Z,0,-ahead.X);
            var rel=p.Centre-Body;float across=Vector3.Dot(rel,right),forward=Vector3.Dot(rel,ahead),side=left?-1:1;
            Check(Math.Abs(across-side*NativeChipScreen.Aside)<1e-4f&&Math.Abs(forward-NativeChipScreen.Ahead)<1e-4f&&Math.Abs(rel.Y-NativeChipScreen.Up)<1e-4f,"facing "+facing+(left?" left":" right")+": ahead on the display wrist's side at chest height");
            // Its normal turned Turn degrees from straight ahead toward the other side (where the chooser camera stands); Right is the reader's right.
            Check(Math.Abs(Vector3.Dot(p.Normal,ahead)-(float)Math.Cos(NativeChipScreen.Turn*Math.PI/180))<1e-4f&&Math.Sign(Vector3.Dot(p.Normal,right))==-side,"turned toward the other front quarter");
            Check(Math.Abs(Vector3.Dot(p.Right,p.Normal))<1e-5f&&Vector3.Cross(p.Up,p.Right).Length()>.999f&&Vector3.Dot(Vector3.Cross(p.Right,p.Up),p.Normal)<0,"Right reads left to right for a viewer in front (Unity's left-handed axes)");
            var big=NativeChipScreen.Place(Body,facing,left,1.5f);Check(Math.Abs(big.Width-1.5f*p.Width)<1e-4f&&Math.Abs(Vector3.Distance(big.Centre,Body)-1.5f*Vector3.Distance(p.Centre,Body))<1e-3f,"it scales with the rig");
        }
        log.Add("placed "+NativeChipScreen.Ahead+" m ahead, "+NativeChipScreen.Aside+" m out on the display wrist's side and "+NativeChipScreen.Up+" m over the body centre, "+NativeChipScreen.Width+" x "+NativeChipScreen.Height+" m, turned "+NativeChipScreen.Turn+" deg toward the other front quarter, scaled with the rig");
    }

    // ---- unfolding out of the wrist, folding back ----
    private static void Unfold(List<string> log)
    {
        var open=NativeChipScreen.Place(Body,0,true,1);var emitter=new Vector3(-.2f,1.0f,.35f);
        var shut=NativeChipScreen.Shown(open,emitter,0);Check(Vector3.Distance(shut.Centre,emitter)<1e-4f&&shut.Width<open.Width*.05f&&shut.Height<open.Height*.03f,"folded it is a sliver at the wrist");
        var full=NativeChipScreen.Shown(open,emitter,1);Check(Vector3.Distance(full.Centre,open.Centre)<1e-4f&&Math.Abs(full.Width-open.Width)<1e-5f&&Math.Abs(full.Height-open.Height)<1e-5f,"open it is the open screen");
        float prevW=0,prevH=0,prevD=Vector3.Distance(emitter,open.Centre)+1;
        for(int k=0;k<=40;k++)
        {
            var p=NativeChipScreen.Shown(open,emitter,k/40f);float d=Vector3.Distance(p.Centre,open.Centre);
            Check(p.Width>=prevW-1e-6f&&p.Height>=prevH-1e-6f&&d<=prevD+1e-6f,"it unfolds steadily out of the wrist");prevW=p.Width;prevH=p.Height;prevD=d;
        }
        var mid=NativeChipScreen.Shown(open,emitter,NativeChipScreen.WidthFirst);Check(mid.Width>open.Width*.99f&&mid.Height<open.Height*.4f,"its width opens first, then its height");
        // The look's unfold (NativeChipSelectHolo.Unfold of the weight) times the plant's fold: a pick folds it back into the wrist.
        Check(NativeChipPlant.Fold(0,false)>=.999f&&NativeChipPlant.Fold(NativeChipPlant.FoldTo,false)<=1e-3f&&NativeChipPlant.Fold(NativeDraftPlant.ReturnEnd,true)>=.999f,"a pick folds it into the wrist (a queued pick opens it again)");
        var sink=new Counting();Check(NativeChipScreen.Build(sink,open,emitter,Look(0))==0&&sink.N==0,"folded away it draws nothing");
        log.Add("it unfolds out of the wrist emitter (the centre travels out, the width opens first, then the height; flicker while it boots) and folds back the same way, by the pick's fold (NativeChipPlant.Fold "+NativeChipPlant.FoldFrom+"-"+NativeChipPlant.FoldTo+" s), nothing drawn folded");
    }

    // ---- the cards on it ----
    private static void Cards(List<string> log)
    {
        var open=NativeChipScreen.Place(Body,30,false,1);var sink=new Sink();
        foreach(int count in new[]{1,2,3})
        {
            float prev=-1e9f;
            for(int i=0;i<count;i++)
            {
                var r=NativeChipScreen.CardRect(i,count);Check(r.X>=20&&r.X+r.Z<=NativeChipScreen.Width*NativeChipScreen.Canvas-20&&r.Y>=100&&r.Y+r.W<=NativeChipScreen.Height*NativeChipScreen.Canvas-80,count+" cards: card "+i+" inside the screen, clear of the header and the footer");
                Check(r.X>prev,count+" cards: left to right");prev=r.X+r.Z;
            }
            var a=NativeChipScreen.CardRect(0,count);var b=NativeChipScreen.CardRect(count-1,count);Check(Math.Abs((a.X+b.X+b.Z)*.5f-NativeChipScreen.Width*NativeChipScreen.Canvas*.5f)<.5f,count+" cards: centred");
        }
        // The cards are drawn inside the screen's one quad (no card layers): mine in my offers' rarities, the lit one passed on, dealt as
        // it opens (each a little after the one before).
        sink.Clear();var look=Look();look.Lit=1;NativeChipScreen.Build(sink,open,Wrist,look);Check(sink.Number(NativeChipScreen.KindScreen)==1&&sink.Number(NativeChipScreen.KindCard)==0,"one screen quad, the cards inside it");
        var sc=sink.Screen(sink.First(NativeChipScreen.KindScreen));
        Check(sc.count==3&&sc.mode==NativeChipScreen.ModeMine&&sc.tints.X==NativeChipScreen.Pack(look.C0)&&sc.tints.Y==NativeChipScreen.Pack(look.C1)&&sc.tints.Z==NativeChipScreen.Pack(look.C2)&&sc.tints.W==NativeChipScreen.Pack(NativeChipSelectHolo.Cyan),"mine show my offers' rarities on the cyan screen");
        Check(sc.lit==1&&sc.vis.X>.99f&&sc.vis.Y>.99f&&sc.vis.Z>.99f,"the lit card marked, all three dealt once open");
        foreach(int count in new[]{1,2}){sink.Clear();var l=Look();l.Count=count;NativeChipScreen.Build(sink,open,Wrist,l);var v=sink.Screen(sink.First(NativeChipScreen.KindScreen));Check(v.count==count&&(count>1||v.vis.Y==0)&&v.vis.Z==0,count+" cards: only they show");}
        float prevDeal=2;for(int i=0;i<3;i++){sink.Clear();NativeChipScreen.Build(sink,open,Wrist,Look(.7f));float d=i==0?sink.Screen(0+sink.First(NativeChipScreen.KindScreen)).vis.X:i==1?sink.Screen(sink.First(NativeChipScreen.KindScreen)).vis.Y:sink.Screen(sink.First(NativeChipScreen.KindScreen)).vis.Z;Check(d<=prevDeal+1e-6f,"dealt left to right as it opens");prevDeal=d;}
        // The card's place in the shader is the Core's slot (its ratios of the open screen, which the HUD's faces use too).
        var shader=File.ReadAllText(Path.Combine(rootDir,"Assets/ChipArena/Resources/NativeChipScreen.shader"));
        Check(Define(shader,"CS_W")==NativeChipScreen.Width&&Define(shader,"CS_H")==NativeChipScreen.Height&&Define(shader,"CS_CARD_W")==NativeChipScreen.CardWidth&&Define(shader,"CS_CARD_GAP")==NativeChipScreen.CardGap&&Define(shader,"CS_CARDS_Y")==NativeChipScreen.CardsY,"the shader lays the cards out on the Core's slots");
        log.Add("1-3 cards in a centred row inside the screen, clear of the header and footer, drawn inside its one quad on the Core's slots (no card layers); mine in my offers' rarities under the HUD's faces, the hovered or focused card marked, dealt left to right as it opens");
    }

    // ---- the HUD's maps: the faces on the world cards from the chooser shot ----
    private static float worstMis,worstRow;
    private static void Maps(List<string> log)
    {
        // Fit and Inverse round trip (any non-degenerate affine map), and a degenerate one refused.
        var rng=new Random(5);
        for(int k=0;k<200;k++)
        {
            Vector2 R()=>new Vector2((float)rng.NextDouble()*1600,(float)rng.NextDouble()*900);var s0=R();var s1=s0+new Vector2(300+(float)rng.NextDouble()*200,(float)rng.NextDouble()*60-30);var s2=s0+new Vector2((float)rng.NextDouble()*80-40,150+(float)rng.NextDouble()*100);
            var m=NativeChipScreen.CanvasMap.Fit(new Vector2(40,120),s0,new Vector2(1020,120),s1,new Vector2(40,330),s2);
            Check(Vector2.Distance(m.Screen(new Vector2(40,120)),s0)<.01f&&Vector2.Distance(m.Screen(new Vector2(1020,120)),s1)<.01f&&Vector2.Distance(m.Screen(new Vector2(40,330)),s2)<.01f,"Fit passes through its three points");
            var c=new Vector2((float)rng.NextDouble()*1060,(float)rng.NextDouble()*520);Check(m.Inverse(m.Screen(c),out var back)&&Vector2.Distance(back,c)<.05f,"Inverse undoes Screen");
        }
        Check(!new NativeChipScreen.CanvasMap{AxisU=new Vector2(1,0),AxisV=new Vector2(2,0)}.Inverse(Vector2.Zero,out _),"a degenerate map is refused");
        // From the chooser shot (either wrist, three aspects, standing and turned): the cards' band map against the true projection.
        foreach(bool left in new[]{true,false})foreach(float aspect in new[]{4f/3,16f/9,21f/9})foreach(float facing in new[]{0f,70f})
        {
            var shot=Shot(left,aspect,facing,out var open);float W=900*aspect,H=900;
            Vector2 Px(Vector3 p)=>Project(p,shot,aspect,W,H);
            var first=NativeChipScreen.CardRect(0,3);var last=NativeChipScreen.CardRect(2,3);
            var map=Band(open,first.X,first.Y,last.X+last.Z,first.Y+first.W,Px);
            for(int i=0;i<3;i++)
            {
                var r=NativeChipScreen.CardRect(i,3);
                foreach(var c in new[]{new Vector2(r.X,r.Y),new Vector2(r.X+r.Z,r.Y),new Vector2(r.X,r.Y+r.W),new Vector2(r.X+r.Z,r.Y+r.W),new Vector2(r.X+r.Z*.5f,r.Y+r.W*.5f)})
                    worstMis=Math.Max(worstMis,Vector2.Distance(map.Screen(c),Px(NativeChipScreen.CanvasPoint(open,c.X,c.Y)))/W);
            }
            float row=(map.Screen(new Vector2(last.X+last.Z,first.Y))-map.Screen(new Vector2(first.X,first.Y))).Length()/W;worstRow=worstRow==0?row:Math.Min(worstRow,row);
            Check(row>=.3f,"the cards' row spans "+row.ToString("0.00")+" of the width");
        }
        // An affine map cannot follow the row's perspective exactly (the near card a little larger): under the faces the slots are a
        // dim frame just outside each face (Faced), so the residual reads as the slot's glow, not as a second card.
        Check(worstMis<.01f,"the faces sit on the world cards within "+(worstMis*100).ToString("0.00")+"% of the width");
        {
            var sink=new Sink();var look=Look();look.Faced=true;look.Lit=0;NativeChipScreen.Build(sink,NativeChipScreen.Place(Body,0,true,1),new Vector3(-.2f,1,.3f),look);
            Check(sink.Screen(sink.First(NativeChipScreen.KindScreen)).mode==NativeChipScreen.ModeFaced&&NativeChipScreenLight.FacedRimLight<NativeChipScreenLight.CardRimLight&&NativeChipScreenLight.FacedLitLight<NativeChipScreenLight.CardLitLight,"under the faces the slots are a dim frame just outside each face");
        }
        log.Add("the HUD maps each band (header, cards, footer) through where the chooser shot projects its four corners: the faces sit on the world cards within "+(worstMis*100).ToString("0.00")+"% of the width (either wrist, 4:3-21:9, turned; the slots under them a dim frame), the row at least "+worstRow.ToString("0.00")+" of the width; Fit/Inverse round-trip, degenerate maps refused");
    }
    // As the runtime's ExpeditionBand: the band's map from where its four corners project.
    private static NativeChipScreen.CanvasMap Band(in ChipScreenPlace p,float u0,float v0,float u1,float v1,Func<Vector3,Vector2> px)
        =>NativeChipScreen.CanvasMap.Rect(u0,v0,u1,v1,px(NativeChipScreen.CanvasPoint(p,u0,v0)),px(NativeChipScreen.CanvasPoint(p,u1,v0)),px(NativeChipScreen.CanvasPoint(p,u0,v1)),px(NativeChipScreen.CanvasPoint(p,u1,v1)));
    // The settled chooser shot of a pilot at Body facing `facing` (open world) and the open screen it frames.
    private static ChooserCameraShot Shot(bool left,float aspect,float facing,out ChipScreenPlace open)
    {
        var cam=new NativeChooserCamera();ChooserCameraShot s=default;float t=0;var eye=Body+Vector3.UnitY*NativeViewRules.EyeHeight;
        for(int f=0;f<120;f++){t+=1/60f;s=cam.Frame(new ChooserCameraInput{Want=true,Pilot=1,Now=t,Dt=1/60f,Body=Body,Scale=1,Facing=facing,ScreenLeft=left,Aspect=aspect,Camera=eye-new Vector3(0,0,3),Pivot=eye,Yaw=facing,Pitch=10,Fov=60},(o,d,dist,r)=>dist);}
        Check(s.Applied&&s.Weight>=1,"the chooser shot settles");open=NativeChipScreen.Place(Body,facing,left,1);return s;
    }
    private static Vector2 Project(Vector3 p,in ChooserCameraShot s,float aspect,float W,float H)
    {
        NativeCameraRig.Basis(s.Yaw,s.Pitch,out var f,out var r);var u=Vector3.Cross(f,r);var d=p-s.Position;float z=Vector3.Dot(d,f),tanV=(float)Math.Tan(s.Fov*Math.PI/360);
        return new Vector2(W*.5f+Vector3.Dot(d,r)/z/tanV*H*.5f,H*.5f-Vector3.Dot(d,u)/z/tanV*H*.5f);
    }

    // ---- the cursor: the card it is over, with no Alt ----
    private static void Pointer(List<string> log)
    {
        int hits=0,misses=0;
        foreach(bool left in new[]{true,false})foreach(float aspect in new[]{16f/9,21f/9})
        {
            var shot=Shot(left,aspect,0,out var open);float W=900*aspect,H=900;Vector2 Px(Vector3 p)=>Project(p,shot,aspect,W,H);
            var first=NativeChipScreen.CardRect(0,3);var last=NativeChipScreen.CardRect(2,3);var map=Band(open,first.X,first.Y,last.X+last.Z,first.Y+first.W,Px);
            for(int i=0;i<3;i++)
            {
                var r=NativeChipScreen.CardRect(i,3);
                // The cursor over a face (IMGUI maps it back through the same map the face is drawn with) picks it, as does anywhere well
                // inside the world card under it; between the faces, above or below the row, none.
                foreach(var c in new[]{new Vector2(r.X+r.Z*.5f,r.Y+r.W*.5f),new Vector2(r.X+r.Z*.03f,r.Y+r.W*.05f),new Vector2(r.X+r.Z*.97f,r.Y+r.W*.95f)})
                {Check(NativeChipScreen.Hit(map,map.Screen(c),3)==i,(left?"left":"right")+" wrist: the cursor over face "+i+" picks it");hits++;}
                foreach(var c in new[]{new Vector2(r.X+r.Z*.5f,r.Y+r.W*.5f),new Vector2(r.X+r.Z*.2f,r.Y+r.W*.25f),new Vector2(r.X+r.Z*.8f,r.Y+r.W*.75f)})
                {Check(NativeChipScreen.Hit(map,Px(NativeChipScreen.CanvasPoint(open,c.X,c.Y)),3)==i,(left?"left":"right")+" wrist: the cursor over world card "+i+" picks it");hits++;}
                if(i<2){var gap=new Vector2(r.X+r.Z+NativeChipScreen.CardGap*NativeChipScreen.Canvas*.5f,r.Y+r.W*.5f);Check(NativeChipScreen.Hit(map,map.Screen(gap),3)==-1,"between the faces: none");misses++;}
            }
            foreach(var c in new[]{new Vector2(500,40),new Vector2(500,490),new Vector2(-50,250)}){Check(NativeChipScreen.Hit(map,map.Screen(c),3)==-1,"off the cards: none");misses++;}
        }
        log.Add("the cursor (free, no Alt: the pointer is the chooser hold's) picks the card it is over through the cards' map: "+hits+" points over a face or well inside its world card hit that card, "+misses+" between, above, below or beside the row hit none");
    }

    // ---- a pick: the card flies into the hand by the contact ----
    private static void Fly(List<string> log)
    {
        var open=NativeChipScreen.Place(Body,0,true,1);var hand=new Vector3(-.15f,1.0f,.32f);float size=.06f;float prevWidth=float.MaxValue,maxBow=0;
        foreach(int chosen in new[]{0,1,2})
        {
            Check(NativeChipScreen.Fly(open,chosen,3,0,hand,size,out var at0,out float w0),"the flight starts at the pick");var slot=NativeChipScreen.Slot(chosen,3,open.Width);
            Check(Vector3.Distance(at0,open.At(slot.X,slot.Y))<.03f&&Math.Abs(w0-NativeChipScreen.CardWidth)<1e-4f,"from its place on the screen, card-sized");
            prevWidth=float.MaxValue;
            for(float age=0;age<NativeDraftPlant.Contact;age+=1/120f)
            {
                Check(NativeChipScreen.Fly(open,chosen,3,age,hand,size,out var at,out float w),"in flight before the contact");Check(w<=prevWidth+1e-6f,"it shrinks toward the chip");prevWidth=w;
                var straight=Vector3.Lerp(open.At(slot.X,slot.Y),hand,MathUtil.Smooth(age/NativeDraftPlant.Contact));maxBow=Math.Max(maxBow,Vector3.Dot(at-straight,open.Normal));
            }
            Check(NativeChipScreen.Fly(open,chosen,3,NativeDraftPlant.Contact-1e-3f,hand,size,out var late,out float lw)&&Vector3.Distance(late,hand)<.01f&&Math.Abs(lw-size)<.005f,"the hand has it at the contact");
            Check(!NativeChipScreen.Fly(open,chosen,3,NativeDraftPlant.Contact,hand,size,out _,out _)&&!NativeChipScreen.Fly(open,chosen,3,-.1f,hand,size,out _,out _),"gone once the hand has it (the plant's chip seats it)");
        }
        Check(maxBow>.05f,"the path bows toward the viewer");
        Check(NativeDraftPlant.Contact==.62f&&NativeDraftPlant.CarryEnd==1.14f&&NativeDraftPlant.PulseStart==1.14f&&NativeDraftPlant.ReturnEnd==1.95f,"the plant's timing is unchanged (.62 / 1.14 / 1.95 s)");
        // On the screen the chosen card has left and the others fade with the plant.
        var sink=new Sink();var look=Look();look.Plant=true;look.Chosen=2;look.PlantAge=.1f;look.Fade=NativeChipPlant.Fade(.1f);look.Catch=hand;look.CatchSize=size;
        NativeChipScreen.Build(sink,open,new Vector3(-.2f,1,.3f),look);var on=sink.Screen(sink.First(NativeChipScreen.KindScreen));int fly=sink.First(NativeChipScreen.KindCard);
        Check(on.vis.Z==0&&on.vis.X>0&&on.vis.X<=look.Fade+1e-4f&&fly>=0&&sink.V[fly*4].a.W>=.999f,"two fading on the screen and the chosen card in flight");
        look.PlantAge=.5f;look.Fade=NativeChipPlant.Fade(.5f);sink.Clear();NativeChipScreen.Build(sink,open,new Vector3(-.2f,1,.3f),look);on=sink.Screen(sink.First(NativeChipScreen.KindScreen));
        Check(on.vis.X<1e-4f&&on.vis.Y<1e-4f&&sink.Number(NativeChipScreen.KindCard)==1,"the others gone by "+NativeChipPlant.FadeTo+" s, the chosen still flying");
        look.FlyCard=false;sink.Clear();NativeChipScreen.Build(sink,open,new Vector3(-.2f,1,.3f),look);Check(sink.Number(NativeChipScreen.KindCard)==0,"mine flies on the HUD over the screen instead (no second card)");
        log.Add("a pick: the chosen card leaves its place card-sized, bows "+(maxBow*100).ToString("0")+" cm toward the viewer and shrinks into the hand by the contact (NativeDraftPlant.Contact "+NativeDraftPlant.Contact+" s; carry "+NativeDraftPlant.CarryEnd+", return "+NativeDraftPlant.ReturnEnd+" s unchanged), the others fade by "+NativeChipPlant.FadeTo+" s; mine flies once, on the HUD's faces");
    }

    // ---- anyone else's screen: blank backs, never an offer ----
    private static void Others(List<string> log)
    {
        var open=NativeChipScreen.Place(Body,0,false,1);var sink=new Sink();var look=Look(1,false);look.C0=0xff00ff;look.C1=0xffd400;look.C2=0x00ff00;look.Lit=2;
        NativeChipScreen.Build(sink,open,new Vector3(.2f,1,.3f),look);var on=sink.Screen(sink.First(NativeChipScreen.KindScreen));float cyan=NativeChipScreen.Pack(NativeChipSelectHolo.Cyan);
        Check(on.mode==NativeChipScreen.ModeBacks&&on.tints.X==cyan&&on.tints.Y==cyan&&on.tints.Z==cyan,"anyone else's cards are blank cyan backs, whatever the offers");
        Check(on.count==3&&on.lit==-1,"the standard three, none lit");
        look.Plant=true;look.Chosen=-1;look.PlantAge=.2f;look.Fade=NativeChipPlant.Fade(.2f);look.Catch=new Vector3(.15f,1,.3f);look.CatchSize=.06f;sink.Clear();NativeChipScreen.Build(sink,open,new Vector3(.2f,1,.3f),look);
        int fly=sink.First(NativeChipScreen.KindCard);Check(fly>=0&&sink.V[fly*4].c.X==cyan,"their pick's flying card is blank too");
        log.Add("anyone else's screen shows the standard three as blank cyan backs (their offers, count and pick stay private) and their pick's card flies blank");
    }

    // ---- the quad bound, no allocation ----
    private static void Budget(List<string> log)
    {
        var open=NativeChipScreen.Place(Body,0,true,1);var emitter=new Vector3(-.2f,1,.3f);int most=0;
        foreach(float unfold in new[]{0f,.01f,.5f,1f})foreach(bool low in new[]{false,true})foreach(bool reduced in new[]{false,true})foreach(bool plant in new[]{false,true})foreach(bool mine in new[]{false,true})
        {
            var look=Look(unfold,mine);look.Low=low;look.Reduced=reduced;look.Plant=plant;look.PlantAge=.2f;look.Fade=.5f;look.Catch=emitter;look.CatchSize=.06f;
            var sink=new Sink();int quads=NativeChipScreen.Build(sink,open,emitter,look);Check(quads<=NativeChipScreen.Quads(look)&&sink.V.Count==quads*4&&sink.Triangles==quads*2,"the quads within the bound");most=Math.Max(most,quads);
            // Once open it is steady: no flicker, whatever the clock (only the opening flickers, and only without Reduced effects).
            if(unfold>=1){var a=new Sink();var b=new Sink();look.Time=1.1f;NativeChipScreen.Build(a,open,emitter,look);look.Time=7.37f;NativeChipScreen.Build(b,open,emitter,look);
                Check(Math.Abs(a.V[a.First(NativeChipScreen.KindScreen)*4].a.W-b.V[b.First(NativeChipScreen.KindScreen)*4].a.W)<1e-6f&&a.V[a.First(NativeChipScreen.KindScreen)*4].a.W>=.999f,"steady once open (no strobing)");}
        }
        var counting=new Counting();var l=Look();l.Plant=true;l.PlantAge=.2f;l.Fade=.5f;l.Catch=emitter;l.CatchSize=.06f;for(int i=0;i<50;i++)NativeChipScreen.Build(counting,open,emitter,l);
        long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<3000;i++){l.Time=i/60f;l.Unfold=(i%90)/89f;NativeChipScreen.Build(counting,open,emitter,l);NativeChipScreen.Shown(open,emitter,l.Unfold);}
        long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used==0,"no allocation per frame ("+used+" bytes)");
        log.Add("at most "+most+" quads a screen (the bound held in every tier and state), steady once open (no flicker), no allocation per frame");
    }

    // ---- the light under the bloom (owner 2026-10-15: "...a better holographic display with no artifacting?") ----
    private static void Light(string root,List<string> log)
    {
        string shader=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeChipScreen.shader"));
        // The shader's numbers are the Core's budget (one source of truth for the check below).
        var pairs=new (string name,float value)[]{("CS_BODY_A",NativeChipScreenLight.BodyAlpha),("CS_BODY",NativeChipScreenLight.BodyLight),("CS_GRID",NativeChipScreenLight.GridLight),("CS_SCAN",NativeChipScreenLight.ScanLight),
            ("CS_RIM",NativeChipScreenLight.RimLight),("CS_RIM_W",NativeChipScreenLight.RimWidth),("CS_FRESNEL",NativeChipScreenLight.FresnelLight),("CS_SLOT_A",NativeChipScreenLight.SlotAlpha),("CS_SLOT",NativeChipScreenLight.SlotLight),
            ("CS_GLYPH",NativeChipScreenLight.GlyphLight),("CS_FACED_GLOW",NativeChipScreenLight.FacedGlow),("CS_RULE",NativeChipScreenLight.RuleLight),("CS_LINE",NativeChipScreenLight.LineLight),("CS_LINE_W",NativeChipScreenLight.LineWidth),
            ("CS_BRACKET",NativeChipScreenLight.BracketLight),("CS_CARD_RIM",NativeChipScreenLight.CardRimLight),("CS_CARD_LIT",NativeChipScreenLight.CardLitLight),("CS_FACED_RIM",NativeChipScreenLight.FacedRimLight),
            ("CS_FACED_LIT",NativeChipScreenLight.FacedLitLight),("CS_SCAN_IN",NativeChipScreenLight.ScanInLight),("CS_RAY",NativeChipScreenLight.RayLight),("CS_RAY_A",NativeChipScreenLight.RayAlpha),("CS_RING",NativeChipScreenLight.RingLight),("CS_MOTE",NativeChipScreenLight.MoteLight)};
        foreach(var (name,value) in pairs)Check(Math.Abs(Define(shader,name)-value)<1e-6f,"the shader's "+name+" is the Core's "+value);
        Check(Define(shader,"CS_DANGER_HEX")==NativeChipSelectHolo.Danger,"the alert's red is the cyber palette's danger");
        // Every tint is an sRGB colour: at most 1 on its brightest channel in linear light (the tone, the alert, every rarity).
        float tintMax=0;foreach(int hex in new[]{NativeChipSelectHolo.Cyan,NativeChipSelectHolo.CyanSoft,NativeChipSelectHolo.Danger,0xf1f5f9,0x79e59a,0x70b5ff,0xce8aff,0xffae55,0xff6475,0xb3b5c2,0xff354e,0x4dff83,0xb7a8ff}){var l=NativePortraitRules.Linear(hex);tintMax=Math.Max(tintMax,Math.Max(l.X,Math.Max(l.Y,l.Z)));}
        Check(tintMax<=1.0001f,"tints are at most 1 in linear light");
        // The body: everything that can light one pixel of it, each at its brightest, summed, stays under NativeBloom's threshold, and so
        // does what a bright lit wall behind it adds through the glass. The bloom adds nothing at or under the threshold.
        float body=NativeChipScreenLight.BodyBudget*tintMax,behind=body+NativeChipScreenLight.BrightScene*(1-NativeChipScreenLight.BodyAlpha);
        Check(body<NativeChipScreenLight.Threshold&&behind<NativeChipScreenLight.Threshold,"the body's summed light "+body.ToString("0.000")+" (over a bright wall "+behind.ToString("0.000")+") stays under the bloom's threshold "+NativeChipScreenLight.Threshold);
        Check(NativeBloomRules.Weight(behind)==0&&NativeBloomRules.Weight(body)==0,"the bloom adds nothing to the body");
        // Only thin lines go over it, as a soft glow: the frame line at its brightest leaves a tenth of a unit to the bloom (a soft few-pixel
        // halo, not a blow-out), and it is at most 3 px wide from the chooser shot at 1080 lines.
        float over=NativeBloomRules.Weight(NativeChipScreenLight.LineLight*tintMax);Check(over>0&&over<=.12f&&NativeBloomRules.Weight(NativeChipScreenLight.CardLitLight)<=over&&NativeBloomRules.Weight(NativeChipScreenLight.BracketLight)<=over&&NativeBloomRules.Weight(NativeChipScreenLight.RingLight)<=over,"only the thin lines glow, softly ("+over.ToString("0.000")+" left to the bloom at the frame line)");
        var shot=Shot(true,16f/9,0,out var open);float pxPerM=1080/(2*(float)Math.Tan(shot.Fov*Math.PI/360)*Vector3.Distance(shot.Position,open.Centre));
        Check(NativeChipScreenLight.LineWidth*pxPerM<=3,"the frame line is "+(NativeChipScreenLight.LineWidth*pxPerM).ToString("0.0")+" px wide from the chooser shot");
        // Alpha-blended glass (premultiplied), no depth write, one pass and one queue: drawn again it converges on its own colour.
        Check(shader.Contains("Blend One OneMinusSrcAlpha")&&!shader.Contains("Blend One One\n")&&!Regex.IsMatch(shader,@"Blend One One\s")&&shader.Contains("ZWrite Off")&&shader.Contains("ZTest LEqual")&&Regex.Matches(shader,@"\bPass\b").Count==1&&shader.Contains("\"Queue\"=\"Transparent+2\""),"alpha-blended glass, no depth write, one pass and queue");
        Check(shader.Contains("multi_compile_fog")&&shader.Contains("UNITY_APPLY_FOG_COLOR(i.fogCoord,keep,float4(0,0,0,0));")&&shader.Contains("return c*keep.r;"),"the fog fades it out, glass and light");
        Check(shader.Contains("float CsLine(float d,float w,float px){float hw=max(w,px)*0.5;return saturate((hw-abs(d))/px+0.5)*min(1.0,w/px);}")&&shader.Contains("saturate((sp/px-3.0)/3.0)")&&shader.Contains("saturate((min(wh.x/32.0,wh.y/16.0)/px-6.0)/6.0)"),"lines keep their energy under a pixel; the scanlines and the grid fade out before they alias (no shimmer)");
        log.Add("light in linear HDR under NativeBloom (threshold "+NativeChipScreenLight.Threshold+"): the body's summed light "+body.ToString("0.000")+", "+behind.ToString("0.000")+" over a bright wall (the shader's own CS_ numbers), nothing to the bloom; only the thin frame line ("+(NativeChipScreenLight.LineWidth*pxPerM).ToString("0.0")+" px), brackets, ring and a lit card's rim glow, softly ("+over.ToString("0.000")+"); premultiplied glass, no depth write, one pass; no aliasing grid or scanlines");
    }

    // ---- no stacked or coplanar layers, a stable order ----
    private static void Layers(List<string> log)
    {
        int screens=0,pairs=0;float nearest=float.MaxValue;
        foreach(float facing in new[]{0f,140f})foreach(bool left in new[]{true,false})foreach(bool mine in new[]{true,false})foreach(float unfold in new[]{.3f,.8f,1f})foreach(float age in new[]{-1f,.2f,.5f})
        {
            var open=NativeChipScreen.Place(Body,facing,left,1);var emitter=Body+new Vector3(left?-.25f:.25f,.15f,.25f);var look=Look(unfold,mine);
            if(age>=0){look.Plant=true;look.Chosen=1;look.PlantAge=age;look.Fade=NativeChipPlant.Fade(age);look.Catch=emitter+new Vector3(0,.05f,.05f);look.CatchSize=.06f;}
            for(int t=0;t<4;t++)
            {
                look.Time=t*.37f;var sink=new Sink();NativeChipScreen.Build(sink,open,emitter,look);if(sink.Count==0)continue;screens++;
                int screen=sink.First(NativeChipScreen.KindScreen);Check(sink.Number(NativeChipScreen.KindScreen)==1,"one screen quad");
                // Back to front: the rays, the ring and the motes before the screen, a flying card after it.
                for(int q=0;q<sink.Count;q++){float k=sink.Kind(q);Check(k==NativeChipScreen.KindCard?q>screen:q<screen||q==screen,"drawn back to front: behind it, it, in front of it");}
                var shown=NativeChipScreen.Shown(open,emitter,unfold);var n=sink.Normal(screen);Check(Math.Abs(Math.Abs(Vector3.Dot(n,shown.Normal))-1)<1e-4f,"the screen quad lies in the screen's plane");
                // No two quads in (nearly) one plane overlapping: parallel within a degree and closer than 2 mm, with overlapping extents.
                for(int a=0;a<sink.Count;a++)for(int b=a+1;b<sink.Count;b++)
                {
                    var na=sink.Normal(a);var nb=sink.Normal(b);if(Math.Abs(Vector3.Dot(na,nb))<.9998f)continue;pairs++;
                    float gap=Math.Abs(Vector3.Dot(sink.Centre(b)-sink.Centre(a),na));nearest=Math.Min(nearest,gap);
                    if(gap>=.002f)continue;
                    Check(!Overlap(sink,a,b,na),"no coplanar overlapping layers ("+sink.Kind(a)+" and "+sink.Kind(b)+", "+(gap*1000).ToString("0.0")+" mm apart)");
                }
                // The motes stay short of the screen's plane (they travel only part of the way along a ray).
                for(int q=0;q<sink.Count;q++)if(sink.Kind(q)==NativeChipScreen.KindMote){float gap=Math.Abs(Vector3.Dot(sink.Centre(q)-shown.Centre,shown.Normal));float far=Math.Abs(Vector3.Dot(emitter-shown.Centre,shown.Normal));Check(gap>=(1-NativeChipScreen.MoteReach)*far-1e-4f,"the motes stay behind the screen");}
            }
        }
        log.Add(screens+" screens (both wrists, mine and others', opening, open, picking): one screen quad with the cards inside it, drawn after the rays, ring and motes behind it and before a flying card; no two quads coplanar and overlapping ("+pairs+" parallel pairs, the nearest "+(nearest*1000).ToString("0")+" mm apart)");
    }

    // Two convex quads in (nearly) one plane: do they overlap there (separating axes on their edges)?
    private static bool Overlap(Sink s,int a,int b,Vector3 n)
    {
        var e1=Vector3.Normalize(s.Corner(a,1)-s.Corner(a,0));var e2=Vector3.Cross(n,e1);var o=s.Corner(a,0);
        Vector2 P(int q,int k){var d=s.Corner(q,k)-o;return new Vector2(Vector3.Dot(d,e1),Vector3.Dot(d,e2));}
        foreach(int q in new[]{a,b})for(int k=0;k<4;k++)
        {
            var edge=P(q,(k+1)%4)-P(q,k);var axis=new Vector2(-edge.Y,edge.X);if(axis.LengthSquared()<1e-14f)continue;
            float amin=float.MaxValue,amax=float.MinValue,bmin=float.MaxValue,bmax=float.MinValue;
            for(int j=0;j<4;j++){float pa=Vector2.Dot(P(a,j),axis),pb=Vector2.Dot(P(b,j),axis);amin=Math.Min(amin,pa);amax=Math.Max(amax,pa);bmin=Math.Min(bmin,pb);bmax=Math.Max(bmax,pb);}
            if(amax<=bmin||bmax<=amin)return false;
        }
        return true;
    }

    // ---- the red edge: a real recent hit only ----
    private static void Alert(string root,List<string> log)
    {
        Check(NativeChipScreen.Alert(float.PositiveInfinity)==0&&NativeChipScreen.Alert(float.NaN)==0&&NativeChipScreen.Alert(-.05f)==0&&NativeChipScreen.Alert(NativeChipScreen.AlertSpan)==0&&NativeChipScreen.Alert(5)==0,"no hit (or one long ago): no red");
        Check(NativeChipScreen.Alert(0)==1,"full at the hit");float prev=1;for(float a=0;a<NativeChipScreen.AlertSpan;a+=.01f){float v=NativeChipScreen.Alert(a);Check(v<=prev+1e-6f&&v>=0,"it fades");prev=v;}
        Check(NativeChipScreen.AlertSpan<=NativeChipSelect.FlinchRise+NativeChipSelect.FlinchFall,"within the hit search's window ("+(NativeChipSelect.FlinchRise+NativeChipSelect.FlinchFall)+" s)");
        // Without a hit the screen carries no alert; with one, only its edge reddens (the shader mixes the danger red into the edge tone
        // only: the frame line, the brackets, the rim and the scan line, never the glass, the grid or the cards).
        var open=NativeChipScreen.Place(Body,0,true,1);var sink=new Sink();NativeChipScreen.Build(sink,open,Wrist,Look());Check(sink.Screen(sink.First(NativeChipScreen.KindScreen)).alert==0,"no hit, no alert");
        var hit=Look();hit.Alert=NativeChipScreen.Alert(.1f);sink.Clear();NativeChipScreen.Build(sink,open,Wrist,hit);Check(sink.Screen(sink.First(NativeChipScreen.KindScreen)).alert>.5f,"a hit just now: the alert");
        string shader=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeChipScreen.shader"));
        Check(shader.Contains("float3 edge=lerp(tone,CsLinear(CsHex(CS_DANGER_HEX)),saturate(B.z));")&&Regex.Matches(shader,@"\bedge\*").Count==3&&shader.Contains("float a=CS_BODY_A*inside;float3 e=tone*(CS_BODY*inside);")&&shader.Contains("e+=tone*((CS_GRID*grid+CS_SCAN*scan)*inside);"),"only the edge takes the red (the rim, the frame line and brackets, the scan line)");
        // The runtime reads the hit from the live events, as the flinch does (NativeEventFeedback.Attacked: a damaging hit on this pilot).
        string screen=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeChipScreenPresentation.cs")),wrist=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeChipSelectPresentation.cs"));
        Check(screen.Contains("Alert=NativeChipScreen.Alert(ChipHitAge(id,slot.Time))")&&wrist.Contains("if(NativeEventFeedback.Attacked(e,id,time,window))return time-e.Time;")&&!screen.Contains("Attacked="),"bound to the newest damaging hit on that pilot (the live events, as the flinch)");
        log.Add("the red edge only for a real damaging hit on that pilot (the live events, as the flinch reads them): full at the hit, gone "+NativeChipScreen.AlertSpan+" s later, no blinking, on the frame line, brackets and rim only; no hit, no red");
    }

    // ---- the runtime hooks ----
    private static void Hooks(string root,List<string> log)
    {
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
        string wrist=R("Runtime/NativeChipSelectPresentation.cs"),screen=R("Runtime/NativeChipScreenPresentation.cs"),exp=R("Runtime/NativeExpeditionChipScreen.cs"),game=R("Runtime/ConversionGame.cs"),ui=R("Runtime/NativeUi.cs"),core=R("Core/NativeChipScreen.cs");
        // In an Expedition the world look builds the screen in place of the small panel; first person and the Boss Test keep theirs.
        Check(wrist.Contains("if(!first&&ExpeditionChoosing!=null)return BuildChipScreen(sink,slot,frame,look,id);"),"an Expedition's world look draws the wrist screen");
        Check(screen.Contains("Count=mine?slot.Count:3")&&screen.Contains("Lit=mine?ChipScreenLit:-1")&&screen.Contains("Chosen=mine?slot.PlantChosen:-1")&&screen.Contains("Mine=mine"),"anyone else's screen: the standard three, no hover, no chosen card");
        Check(screen.Contains("look.Weight=0;if(slot.PlantAge<NativeDraftPlant.Contact)look.ChipShown=false;"),"the small panel hidden; the plant's chip only once the hand has the card");
        // The screens: their own mesh and shader, built into their own stream, drawn once after every look.
        Check(screen.Contains("if(ChipScreenShader())quads+=NativeChipScreen.Build(chipScreenSink,open,frame.Emitter,screen);")&&screen.Contains("Resources.Load<Shader>(\"NativeChipScreen\")")&&screen.Contains("m.SetUVs(4,chipScreenSink.E);")
            &&wrist.Contains("chipWorldSink.Clear();chipScreenSink.Clear();")&&wrist.Contains("            DrawChipScreens();")&&screen.Contains("public bool ChipScreenAvailable=>!chipHoloFailed&&ChipScreenShader();"),"the screens draw with their own mesh and shader (the 2D tray if it is missing)");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/NativeChipScreen.shader.meta"))&&File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Editor/NativeBuildChecks.cs")).Contains("\"NativeChipScreen\""),"the shader is imported and build-checked");
        // The pointer: the chooser hold's (Astra's ExpeditionCameraHeld) while the screen or its follow-through shows; never Alt.
        Check(exp.Contains("private bool ExpeditionPickPointer=>ExpeditionCameraHeld&&(ExpeditionPickShown||ExpeditionFollowing);")&&!exp.Contains("KeyCode.LeftAlt")&&!exp.Contains("KeyCode.RightAlt")&&!game.Contains("KeyCode.LeftAlt"),"the chip cursor needs no Alt");
        Check(game.Contains("&&!ExpeditionPickPointer;Cursor.lockState=look?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!look;"),"the cursor is free and shown while choosing, locked again as the hold ends");
        Check(game.Contains("if(!lockPoint.HasValue&&!ExpeditionLookSettling&&!cinematic.Directing&&!ExpeditionCameraHeld&&!ExpeditionPickPointer){yaw+=")&&exp.Contains("if(expeditionPointerWas&&!pointer)expeditionRelockFrame=Time.frameCount;"),"mouse look returns after the re-lock settles (no jump)");
        // The screen's cards: the faces drawn through the cards' map, IMGUI mapping the cursor back through it; the 2D tray where it cannot be read.
        Check(ui.Contains("public static void PushAffine(Vector2 origin,Vector2 axisU,Vector2 axisV)")&&exp.Contains("ExpeditionPush(maps.Cards);try{int click=DrawTray(t,chips,i=>held(chips[i].id),elapsed,enabled,-1,i=>false,true);if(click>=0)choice=click;"),"the faces and the click through the cards' map");
        Check(exp.Contains("if(screen&&!readable&&state.Unfold>=.99f&&(chooserCamera.Progress>=1||!ChooserFramed))screen=false;")&&exp.Contains(":ExpeditionPickTray(reward,chips,"),"where the settled camera cannot read the screen, the 2D tray");
        Check(exp.Contains("if(ExpeditionFollowing){if(!expeditionFollowScreen||!ExpeditionScreenFollowGui())ExpeditionFollowGui(W,H);return;}"),"the follow-through on the screen, else the 2D hand-off");
        Check(Regex.Matches(exp,Regex.Escape("+\" PICK  ·  CLICK A CARD  ·  C OR ESC CLOSES\";")).Count==2&&!exp.Contains("HOLD ALT"),"the key lines (the screen's and the 2D tray's) read CLICK A CARD, no Alt");
        Check(!core.Contains("UnityEngine")&&!core.Contains("Random")&&!core.Contains("DateTime"),"the Core is engine-free and deterministic");
        log.Add("runtime: an Expedition's world look draws the screen in place of the small panel (first person and the Boss Test keep theirs); anyone else's shows no offer; the cursor is free with no Alt while the camera is held and re-locks with a settled look; the faces and the click go through the cards' map, the 2D tray where the settled camera cannot read it");
    }
}
