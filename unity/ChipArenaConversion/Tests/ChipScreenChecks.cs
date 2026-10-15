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
/// plant's contact, anyone else's screen as blank backs (no offer), the quad bound and no allocation, and the runtime hooks (the screen
/// replaces the small panel in an Expedition only; the pointer is the chooser hold's, no Alt; the 2D tray where the screen cannot be read;
/// the Boss Test untouched). Managed only: no Unity Editor or Windows play is run.
/// </summary>
internal static class ChipScreenChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("Chip screen: "+what);}
    private static readonly Vector3 Body=new Vector3(0,.865f,0);
    public static void Run(string root)
    {
        var log=new List<string>();Placement(log);Unfold(log);Cards(log);Maps(log);Pointer(log);Fly(log);Others(log);Budget(log);Hooks(root,log);
        foreach(var line in log)Console.WriteLine("PASS chip screen: "+line);
    }

    // A sink that keeps the quads' kinds, colours and centres (four vertices, two triangles each).
    private sealed class Sink:INativeChipHoloSink
    {
        public readonly List<(Vector3 at,Vector4 col,Vector4 uv)> V=new List<(Vector3,Vector4,Vector4)>();public int Triangles;
        public int Vertex(Vector3 at,Vector4 color,Vector4 uv,Vector4 extra){V.Add((at,color,uv));return V.Count-1;}
        public void Triangle(int a,int b,int c){Triangles++;}
        public void Clear(){V.Clear();Triangles=0;}
        public IEnumerable<(Vector3 centre,Vector4 col,float kind,float width)> Quads(float kind)
        {
            for(int i=0;i+3<V.Count;i+=4)if(Math.Abs(V[i].uv.Z-kind)<.01f)yield return ((V[i].at+V[i+2].at)*.5f,V[i].col,V[i].uv.Z,(V[i+1].at-V[i].at).Length());
        }
    }
    private sealed class Counting:INativeChipHoloSink{public int N;public int Vertex(Vector3 at,Vector4 c,Vector4 uv,Vector4 e)=>N++;public void Triangle(int a,int b,int c){}}
    private static Vector4 Rgb(int hex)=>new Vector4(((hex>>16)&255)/255f,((hex>>8)&255)/255f,(hex&255)/255f,0);
    private static bool SameRgb(Vector4 a,int hex){var b=Rgb(hex);return Math.Abs(a.X-b.X)<1e-4f&&Math.Abs(a.Y-b.Y)<1e-4f&&Math.Abs(a.Z-b.Z)<1e-4f;}
    private static ChipScreenLook Look(float unfold=1,bool mine=true)=>new ChipScreenLook{Unfold=unfold,Time=2.3f,Count=3,Lit=-1,Seed=NativeChipSelect.Seed(3,5),Mine=mine,C0=0x9aa3b5,C1=0x37e08a,C2=0x4aa8ff,FlyCard=true,Chosen=-1};

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
        // Mine in my offers' rarities, the lit card brought forward and brighter.
        sink.Clear();var look=Look();look.Lit=1;NativeChipScreen.Build(sink,open,new Vector3(.2f,1,.3f),look);var cards=new List<(Vector3 centre,Vector4 col,float kind,float width)>(sink.Quads(NativeChipSelectHolo.KindCard));
        Check(cards.Count==3&&SameRgb(cards[0].col,look.C0)&&SameRgb(cards[1].col,look.C1)&&SameRgb(cards[2].col,look.C2),"mine show my offers' rarities");
        Check(Vector3.Dot(cards[1].centre-open.Centre,open.Normal)>Vector3.Dot(cards[0].centre-open.Centre,open.Normal)+.01f&&cards[1].col.W>cards[0].col.W,"the lit card stands out toward the viewer, brighter");
        // Under attack the screen blinks red, cards and all.
        sink.Clear();look=Look();look.Attacked=true;look.Reduced=true;NativeChipScreen.Build(sink,open,new Vector3(.2f,1,.3f),look);foreach(var c in sink.Quads(NativeChipSelectHolo.KindCard))Check(SameRgb(c.col,NativeChipSelectHolo.Danger),"under attack the cards blink red");
        log.Add("1-3 cards in a centred row inside the screen, clear of the header and footer; mine in my offers' rarities under the HUD's faces, the hovered or focused card forward and brighter, red while under attack");
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
        // An affine map cannot follow the row's perspective exactly (the near card a little larger): under the faces the world cards
        // are a dim backing glow (Faced), so the residual reads as glow, not as a second card.
        Check(worstMis<.01f,"the faces sit on the world cards within "+(worstMis*100).ToString("0.00")+"% of the width");
        {
            var sink=new Sink();var look=Look();look.Faced=true;look.Lit=0;NativeChipScreen.Build(sink,NativeChipScreen.Place(Body,0,true,1),new Vector3(-.2f,1,.3f),look);
            foreach(var c in sink.Quads(NativeChipSelectHolo.KindCard))Check(c.col.W<=.5f,"under the faces the world cards are a dim glow");
        }
        log.Add("the HUD maps each band (header, cards, footer) through where the chooser shot projects its four corners: the faces sit on the world cards within "+(worstMis*100).ToString("0.00")+"% of the width (either wrist, 4:3-21:9, turned; the cards under them a dim glow), the row at least "+worstRow.ToString("0.00")+" of the width; Fit/Inverse round-trip, degenerate maps refused");
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
        NativeChipScreen.Build(sink,open,new Vector3(-.2f,1,.3f),look);var cards=new List<(Vector3 centre,Vector4 col,float kind,float width)>(sink.Quads(NativeChipSelectHolo.KindCard));
        Check(cards.Count==3&&cards[2].col.W>=.999f&&cards[0].col.W<.8f*look.Fade+1e-3f,"two fading on the screen and the chosen card in flight");
        look.PlantAge=.5f;look.Fade=NativeChipPlant.Fade(.5f);sink.Clear();NativeChipScreen.Build(sink,open,new Vector3(-.2f,1,.3f),look);Check(new List<(Vector3,Vector4,float,float)>(sink.Quads(NativeChipSelectHolo.KindCard)).Count==1,"the others gone by "+NativeChipPlant.FadeTo+" s, the chosen still flying");
        look.FlyCard=false;sink.Clear();NativeChipScreen.Build(sink,open,new Vector3(-.2f,1,.3f),look);Check(new List<(Vector3,Vector4,float,float)>(sink.Quads(NativeChipSelectHolo.KindCard)).Count==0,"mine flies on the HUD over the screen instead (no second card)");
        log.Add("a pick: the chosen card leaves its place card-sized, bows "+(maxBow*100).ToString("0")+" cm toward the viewer and shrinks into the hand by the contact (NativeDraftPlant.Contact "+NativeDraftPlant.Contact+" s; carry "+NativeDraftPlant.CarryEnd+", return "+NativeDraftPlant.ReturnEnd+" s unchanged), the others fade by "+NativeChipPlant.FadeTo+" s; mine flies once, on the HUD's faces");
    }

    // ---- anyone else's screen: blank backs, never an offer ----
    private static void Others(List<string> log)
    {
        var open=NativeChipScreen.Place(Body,0,false,1);var sink=new Sink();var look=Look(1,false);look.C0=0xff00ff;look.C1=0xffd400;look.C2=0x00ff00;
        NativeChipScreen.Build(sink,open,new Vector3(.2f,1,.3f),look);int n=0;
        foreach(var c in sink.Quads(NativeChipSelectHolo.KindCard)){Check(SameRgb(c.col,NativeChipSelectHolo.Cyan),"anyone else's cards are blank cyan backs, whatever the offers");n++;}
        Check(n==3,"the standard three");
        look.Plant=true;look.Chosen=-1;look.PlantAge=.2f;look.Fade=NativeChipPlant.Fade(.2f);look.Catch=new Vector3(.15f,1,.3f);look.CatchSize=.06f;sink.Clear();NativeChipScreen.Build(sink,open,new Vector3(.2f,1,.3f),look);
        foreach(var c in sink.Quads(NativeChipSelectHolo.KindCard))Check(SameRgb(c.col,NativeChipSelectHolo.Cyan),"their pick's flying card is blank too");
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
        }
        var counting=new Counting();var l=Look();l.Plant=true;l.PlantAge=.2f;l.Fade=.5f;l.Catch=emitter;l.CatchSize=.06f;for(int i=0;i<50;i++)NativeChipScreen.Build(counting,open,emitter,l);
        long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<3000;i++){l.Time=i/60f;l.Unfold=(i%90)/89f;NativeChipScreen.Build(counting,open,emitter,l);NativeChipScreen.Shown(open,emitter,l.Unfold);}
        long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used==0,"no allocation per frame ("+used+" bytes)");
        log.Add("at most "+most+" quads a screen (the bound held in every tier and state), no allocation per frame");
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
