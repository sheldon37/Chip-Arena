using UnityEngine;
using G=ChipArena.UnityConversion.NativeBossGuide;
using H=ChipArena.UnityConversion.NativeHudChrome;
using Q=ChipArena.UnityConversion.NativeQuotaMarks;
using U=ChipArena.UnityConversion.NativeUi;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The way to the boss room, drawn (Core/NativeBossGuide has the rules; the owner, 2026-11-07: "When clearing quota on instances as well; Can
    /// we make an animated guide to show where the boss room is?"). Once the instance's quota is met and its boss has arrived, until the viewer is
    /// in its room: the trail of chevrons on the floor along the real way there (Backrooms, Poolrooms), a light running along it; the marker
    /// over the boss through walls with the distance to walk; an edge arrow when it's off screen; "THE HUM · 42 m" under the objective; one soft
    /// ping when it first turns on. All in the boss's own colours. Repaint only for the HUD; nothing in menus, intros or replays.
    /// </summary>
    public sealed partial class ConversionGame
    {
        private readonly BossGuideRoute bossGuideRoute=new BossGuideRoute();private float bossGuideLevel;private string bossGuideCue;
        private void BossGuideHud(ExpeditionView v,float width,float height)
        {
            if(Event.current.type!=EventType.Repaint)return;
            var g=G.Of(v);var me=arena?.Local;bool alive=me!=null&&me.Hp>0&&me.Body!=null;
            Vector3 feet=alive?me.Body.position-Vector3.up*NativeLocomotionRules.HalfHeight:Vector3.zero;var at=new Vector3(g.At.X,g.At.Y,g.At.Z);
            var flat=new Vector2(feet.x-at.x,feet.z-at.z);bool shown=g.Active&&alive&&ExpeditionWorldShown&&flat.magnitude>g.Near;
            bossGuideLevel=Mathf.MoveTowards(bossGuideLevel,shown?1:0,Time.unscaledDeltaTime/(shown?.6f:.3f));
            string key=g.Active?g.Area+"|"+g.Archetype:null;if(shown&&key!=bossGuideCue){bossGuideCue=key;nativeAudio?.QuotaMark();}if(!g.Active)bossGuideCue=null;
            bool routed=false;
            if(shown&&arena!=null)
            {
                var from=new NVector(feet.x,feet.y,feet.z);
                if(arena.MapId==NativeBackroomsGenerator.InstanceMap)routed=bossGuideRoute.Update(arena.GeneratedRun?.Backrooms?.Layout,from,g.At);
                else if(NativePoolroomsGenerator.IsMap(arena.MapId))routed=bossGuideRoute.Update(arena.GeneratedRun?.Poolrooms?.Layout,from,g.At);
                else bossGuideRoute.Clear();
            }
            presentation.BossGuide(routed||bossGuideLevel>0&&bossGuideRoute.Valid?bossGuideRoute:null,bossGuideLevel,g.Active?g.Deep:0xffb02e,g.Active?g.Light:0xffd27a);
            if(bossGuideLevel<=.01f||!g.Active||cameraView==null)return;
            float walk=routed?bossGuideRoute.Length:flat.magnitude;bool reduced=prefs.reducedEffects,glow=!reduced&&!presentation.LowGraphics;
            var deep=NativeArena.ColorOf(g.Deep);var light=NativeArena.ColorOf(g.Light);double t=Time.unscaledTimeAsDouble;float wave=reduced?0:.5f+.5f*Mathf.Sin((float)(t*2.6));
            var head=at+Vector3.up*2.8f;var vp=cameraView.WorldToViewportPoint(head);float aspect=width/Mathf.Max(1,height);bool onScreen=vp.z>0&&vp.x>=.02f&&vp.x<=.98f&&vp.y>=.04f&&vp.y<=.96f;
            float a=bossGuideLevel*(reduced?.95f:.8f+.2f*wave);
            if(onScreen){float size=Mathf.Lerp(36,24,Mathf.Clamp01((walk-10)/60))*(1+.08f*wave);GuideKite(new Vector2(vp.x*width,(1-vp.y)*height),size,deep,light,a,glow,Q.Metres(walk));}
            else{float angle=Q.Direction(vp.x,vp.y,vp.z,aspect);Q.Place(angle,aspect,out float ax,out float ay);GuideArrow(new Vector2(ax*width,(1-ay)*height),angle,28,deep,light,a,Q.Metres(walk));}
            if(!expeditionQuota.Shown)GuideTag(width*.5f,v.transfer!=null&&v.transfer.stage!=ExpeditionTransferStage.None?110:54,G.Tag(g.Name,walk),deep,light,bossGuideLevel);
        }
        private static void GuideKite(Vector2 centre,float size,Color deep,Color light,float a,bool glow,string metres)
        {
            float b=Q.ShapeBox;var box=new Rect(centre.x-b*.5f,centre.y-b*.5f,b,b);
            U.Push(centre,Vector2.zero,size/b);
            if(glow)U.Glow(new Rect(box.x+b*.25f,box.y+b*.25f,b*.5f,b*.5f),new Color(deep.r,deep.g,deep.b,.35f*a),b*.4f);
            NativeIcons.DrawMarkup(new Rect(box.x+1,box.y+1.5f,b,b),"quota-mark-body",Q.MarkerBody,24,new Color(0,0,0,.55f*a));
            NativeIcons.DrawMarkup(box,"quota-mark-body",Q.MarkerBody,24,new Color(deep.r,deep.g,deep.b,.35f*a));
            NativeIcons.DrawMarkup(box,"quota-mark-edge",Q.MarkerEdge,24,new Color(light.r,light.g,light.b,a));
            NativeIcons.DrawMarkup(box,"quota-mark-core",Q.MarkerCore,24,new Color(light.r,light.g,light.b,a));
            U.Pop();
            U.Text(new Rect(centre.x-50,centre.y-size*.5f-30,100,14),"BOSS",10,new Color(light.r,light.g,light.b,a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.2f,shadow:new Color(0,0,0,.85f*a));
            U.Text(new Rect(centre.x-50,centre.y-size*.5f-17,100,14),metres,10,new Color(light.r,light.g,light.b,a),TextAnchor.MiddleCenter,bold:true,mono:true,shadow:new Color(0,0,0,.85f*a));
        }
        private static void GuideArrow(Vector2 tip,float angle,float size,Color deep,Color light,float a,string metres)
        {
            if(a<=.01f)return;var dir=new Vector2(Mathf.Cos(angle),-Mathf.Sin(angle));float b=Q.ShapeBox,deg=-angle*Mathf.Rad2Deg;var centre=tip-dir*(size*.42f);var box=new Rect(centre.x-b*.5f,centre.y-b*.5f,b,b);
            U.Push(centre,Vector2.zero,size/b,deg);
            NativeIcons.DrawMarkup(new Rect(box.x+1,box.y+1.5f,b,b),"quota-arrow",Q.ArrowDart,24,new Color(0,0,0,.6f*a));
            NativeIcons.DrawMarkup(box,"quota-arrow",Q.ArrowDart,24,new Color(light.r,light.g,light.b,a));
            U.Pop();
            var at=centre-dir*(size*.5f+16);
            U.Text(new Rect(at.x-40,at.y-14,80,14),"BOSS",9,new Color(light.r,light.g,light.b,a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.2f,shadow:new Color(0,0,0,.85f*a));
            U.Text(new Rect(at.x-40,at.y,80,14),metres,10,new Color(light.r,light.g,light.b,a),TextAnchor.MiddleCenter,bold:true,mono:true,shadow:new Color(0,0,0,.85f*a));
        }
        private static void GuideTag(float centreX,float y,string text,Color deep,Color light,float a)
        {
            if(a<=.01f||string.IsNullOrEmpty(text))return;float tw=U.Measure(text,11,true,true,.12f).x,w=tw+46;var r=new Rect(Mathf.Round(centreX-w*.5f),y,w,22);
            H.Tag(r,deep,a);GuideKite(new Vector2(r.x+17,r.center.y+1),14,deep,light,a,false,"");
            U.Text(new Rect(r.x+29,r.y,tw+8,r.height),text,11,new Color(light.r,light.g,light.b,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f,shadow:new Color(0,0,0,.7f*a));
        }
    }

    public sealed partial class ArenaPresentation
    {
        private BossGuideRoute bossGuide;private float bossGuideAlpha;private int bossGuideDeep,bossGuideLight,bossGuideFrame=-10;
        /// <summary>The way to the boss room this frame (ConversionGame's HUD sets it; null for none), drawn on the floor in the next glow pass.</summary>
        public void BossGuide(BossGuideRoute route,float level,int deep,int light){bossGuide=route;bossGuideAlpha=level;bossGuideDeep=deep;bossGuideLight=light;bossGuideFrame=Time.frameCount;}
        private void DrawBossGuide(IKitFxSink sink,float time)
        {if(bossGuide==null||bossGuideAlpha<=.01f||Time.frameCount-bossGuideFrame>2)return;G.Draw(sink,bossGuide,time,bossGuideAlpha,bossGuideDeep,bossGuideLight,Mathf.Clamp01(WeaponEffectsQuality),LowGraphics);}
    }
}
