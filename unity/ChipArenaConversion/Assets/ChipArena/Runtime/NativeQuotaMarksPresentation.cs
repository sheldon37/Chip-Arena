using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using Q=ChipArena.UnityConversion.NativeQuotaMarks;
using U=ChipArena.UnityConversion.NativeUi;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The last enemies of a kill quota, marked on the in-scene HUD (Claude, 2026-10-18; owner: "We need some kind of indicator for
    /// the last enemies of a quota (Like maybe last 5?) In case they get stuck so players know where to find them"). Every rule is
    /// Core/NativeQuotaMarks (the trigger, which adds, the anchor, size, pulse and fades, the edge arrows, the cue); this draws it:
    ///  - over each marked add, a downward kite 0.6 m above its head in the HUD's enemy tone (Danger: apart from the hazards' amber
    ///    and the allies' cyan), drawn over the scene so walls never hide it, pulsing gently (steady with Reduced effects; no glow
    ///    on the low graphics tier), with its distance in metres; hidden within 4 m while the add is in plain sight (a ray from the
    ///    camera to its chest), faint over the crosshair;
    ///  - an edge arrow with the distance for each marked add off screen or behind the camera;
    ///  - "LAST n · MARKED" under the objective tag (and under the J screen's KILL QUOTA bar: <see cref="NativeQuotaMarksHud.Building"/>);
    ///  - one soft ping when the markers first turn on for a quota (NativeAudio.QuotaMark: the bank's key, else a built-in ping).
    /// It reads only what every peer holds: the bound ExpeditionView's instance quota and the presented arena's adds (their
    /// presented roots, ArenaPresentation.HordeDrawnRoot). Presentation only: no rule, AI, spawn, director, network or authority
    /// change. No allocation per frame (fixed buffers, cached labels and shapes).
    /// </summary>
    public static class NativeQuotaMarksHud
    {
        /// <summary>The hunt tone: the HUD's enemy colour (HudCyberChecks: "ally cyan and enemy danger read apart").</summary>
        public static Color Tone(float a=1)=>C.Danger(a);
        /// <summary>The distance labels: the hunt tone lifted toward ink so small digits stay legible.</summary>
        public static Color LabelTone(float a=1)=>U.A(U.Mix(C.Danger(),C.Ink(),.5f),a);
        private static readonly float[] tagWidths=new float[Q.Threshold+1];private static float tagScale=-1;
        private static float TagWidth(int n)
        {
            if(tagScale!=U.S){tagScale=U.S;for(int i=0;i<tagWidths.Length;i++)tagWidths[i]=0;}
            if(tagWidths[n]<=0)tagWidths[n]=U.Measure(Q.Tag(n),11,true,true,.12f).x;return tagWidths[n];
        }
        /// <summary>The marker's kite at `centre` (UI px), `size` px tall, alpha `a`; a soft glow behind it unless `glow` is off.</summary>
        public static void Kite(Vector2 centre,float size,float a,bool glow)
        {
            if(a<=.01f||size<=0)return;float b=Q.ShapeBox;var box=new Rect(centre.x-b*.5f,centre.y-b*.5f,b,b);
            U.Push(centre,Vector2.zero,size/b);
            if(glow)U.Glow(new Rect(box.x+b*.3f,box.y+b*.3f,b*.4f,b*.4f),Tone(.28f*a),b*.3f);
            NativeIcons.DrawMarkup(new Rect(box.x+1,box.y+1.5f,b,b),"quota-mark-body",Q.MarkerBody,24,new Color(0,0,0,.55f*a));
            NativeIcons.DrawMarkup(box,"quota-mark-body",Q.MarkerBody,24,Tone(.3f*a));
            NativeIcons.DrawMarkup(box,"quota-mark-edge",Q.MarkerEdge,24,Tone(a));
            NativeIcons.DrawMarkup(box,"quota-mark-core",Q.MarkerCore,24,Tone(a));
            U.Pop();
        }
        /// <summary>A world marker: the kite centred at `at` (UI px) with its distance label above it.</summary>
        public static void Marker(Vector2 at,float size,float a,bool label,string metres,bool glow)
        {
            Kite(at,size,a,glow);if(!label)return;
            U.Text(new Rect(at.x-40,at.y-size*.5f-17,80,14),metres,10,LabelTone(a),TextAnchor.MiddleCenter,bold:true,mono:true,shadow:new Color(0,0,0,.85f*a));
        }
        /// <summary>An edge arrow: its tip at `tip` (UI px) pointing along `angle` (radians, 0 right, counter-clockwise, y up), `size`
        /// px long, with the distance label just inside it.</summary>
        public static void EdgeArrow(Vector2 tip,float angle,float size,float a,string metres)
        {
            if(a<=.01f)return;var dir=new Vector2(Mathf.Cos(angle),-Mathf.Sin(angle));float b=Q.ShapeBox,deg=-angle*Mathf.Rad2Deg;
            var centre=tip-dir*(size*.42f);var box=new Rect(centre.x-b*.5f,centre.y-b*.5f,b,b);
            U.Push(centre,Vector2.zero,size/b,deg);
            NativeIcons.DrawMarkup(new Rect(box.x+1,box.y+1.5f,b,b),"quota-arrow",Q.ArrowDart,24,new Color(0,0,0,.6f*a));
            NativeIcons.DrawMarkup(box,"quota-arrow",Q.ArrowDart,24,Tone(a));
            U.Pop();
            var at=centre-dir*(size*.5f+15);
            U.Text(new Rect(at.x-36,at.y-7,72,14),metres,10,LabelTone(a),TextAnchor.MiddleCenter,bold:true,mono:true,shadow:new Color(0,0,0,.85f*a));
        }
        /// <summary>The in-scene "LAST n · MARKED" tag, centred on `centreX` with its top at `y` (under the objective tag).</summary>
        public static void Tag(float centreX,float y,int remaining,float a)
        {
            string text=Q.Tag(remaining);if(a<=.01f||text.Length==0)return;float tw=TagWidth(remaining),w=tw+46;var r=new Rect(Mathf.Round(centreX-w*.5f),y,w,22);
            H.Tag(r,Tone(),a);Kite(new Vector2(r.x+17,r.center.y),13,a,false);
            U.Text(new Rect(r.x+29,r.y,tw+8,r.height),text,11,Tone(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f,shadow:new Color(0,0,0,.7f*a));
        }
        /// <summary>The J screen's building panel: the same line under its KILL QUOTA bar while the markers are on; returns the y
        /// under it (unchanged when they are off).</summary>
        public static float Building(Rect panel,float y,ExpeditionView v,float a)
        {
            var q=Q.Of(v);if(!q.Active)return y;string text=Q.Tag(q.Remaining);
            Kite(new Vector2(panel.x+18+7,y+8),13,a,false);
            U.Text(new Rect(panel.x+18+20,y,panel.width-56,16),text,11,Tone(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
            return y+22;
        }
    }

    public sealed partial class ConversionGame
    {
        private readonly QuotaMarkSet quotaMarks=new QuotaMarkSet();private readonly QuotaArrowSet quotaArrows=new QuotaArrowSet();private readonly QuotaMarkCue quotaCue=new QuotaMarkCue();
        private QuotaCandidate[] quotaCandidates=new QuotaCandidate[64];private readonly QuotaMarker[] quotaMarkers=new QuotaMarker[Q.Cap];private bool quotaLabelsWarm;
        /// <summary>
        /// The in-scene HUD's quota markers (ExpeditionLiveHud, after the world labels): the trigger from the view, the adds of the
        /// presented arena, the markers, the edge arrows, the LAST tag and the cue. Repaint only; nothing in menus, intros or replays.
        /// </summary>
        private void QuotaMarksHud(ExpeditionView v,float width,float height)
        {
            if(Event.current.type!=EventType.Repaint)return;
            var q=Q.Of(v);bool shown=q.Active&&ExpeditionWorldShown&&arena?.Minions!=null;
            var cue=q;cue.Active=shown;if(quotaCue.Step(cue)&&nativeAudio!=null)nativeAudio.QuotaMark();
            float level=quotaMarks.Fade(shown,Time.unscaledDeltaTime);if(!shown){quotaArrows.Clear();return;}
            if(!quotaLabelsWarm){quotaLabelsWarm=true;Q.WarmLabels();}
            // The presented arena's adds (the local client's own area: a split area's arena holds only its own).
            var list=arena.Minions;if(quotaCandidates.Length<list.Count)quotaCandidates=new QuotaCandidate[Mathf.NextPowerOfTwo(list.Count)];
            int n=0;float now=arena.Time;
            for(int i=0;i<list.Count;i++)
            {
                var m=list[i];if(m?.Body==null)continue;var a=m.Pve;
                var root=presentation.HordeDrawnRoot(m.Id,out var drawn)?drawn:m.Body.position;
                quotaCandidates[n++]=new QuotaCandidate{Id=m.Id,Kind=a!=null?NativeHordeKinds.Resolve(a.Stats?.id):0,Root=new NVector(root.x,root.y,root.z),Area=0,Crawl=a!=null&&a.Crawl,
                    Alive=m.Hp>0,Hostile=a!=null&&!m.DevMonster,Spawned=a==null||now>=a.SpawnAt,Hub=a!=null&&(a.Hub!=null||a.HubRing!=null),Boss=NativeZoneBossRules.Known(m.Kind)||a!=null&&a.BossSpawn};
            }
            var me=arena.Local;Vector3 from=me!=null&&me.Hp>0&&me.Body!=null?me.Body.position:cameraView.transform.position;
            quotaMarks.Select(quotaCandidates,n,0,new NVector(from.x,from.y,from.z));
            float aspect=width/Mathf.Max(1,height);double t=Time.unscaledTimeAsDouble;bool reduced=prefs.reducedEffects,glow=!reduced&&!presentation.LowGraphics;
            var eye=cameraView.transform.position;var physics=arena.Root.scene.GetPhysicsScene();quotaArrows.Clear();
            for(int i=0;i<quotaMarks.Count;i++)
            {
                var mk=quotaMarks.Marks[i];var vp=cameraView.WorldToViewportPoint(new Vector3(mk.Anchor.X,mk.Anchor.Y,mk.Anchor.Z));bool visible=false;
                // Plain sight (only near: the close fade is 1 beyond it): nothing of the world between the camera and the add's chest.
                if(mk.Distance<Q.CloseShow+1&&vp.z>0){var chest=new Vector3(mk.Root.X,mk.Root.Y+.3f,mk.Root.Z);var d=chest-eye;float len=d.magnitude;visible=len<.6f||!physics.Raycast(eye,d/len,out _,len-.5f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore);}
                var marker=Q.Marker(vp.x,vp.y,vp.z,aspect,mk.Distance,visible,t,reduced,level);quotaMarkers[i]=marker;
                if(!marker.OnScreen)quotaArrows.Add(Q.Arrow(vp.x,vp.y,vp.z,aspect,mk.Distance,t,reduced,level,i));
            }
            quotaArrows.Spread(aspect);Q.Declutter(quotaMarkers,quotaMarks.Count,width,height);
            // Far to near, so the nearest kite sits on top (a farther one lined up behind it is lifted clear: Q.Declutter).
            for(int i=quotaMarks.Count-1;i>=0;i--){var mk=quotaMarkers[i];if(!mk.OnScreen||mk.Alpha<=.01f)continue;NativeQuotaMarksHud.Marker(new Vector2(mk.X*width,(1-mk.Y)*height),mk.Size,mk.Alpha,mk.Label,Q.Metres(quotaMarks.Marks[i].Distance),glow);}
            for(int i=0;i<quotaArrows.Count;i++){var ar=quotaArrows.Arrows[i];NativeQuotaMarksHud.EdgeArrow(new Vector2(ar.X*width,(1-ar.Y)*height),ar.Angle,ar.Size,ar.Alpha,Q.Metres(quotaMarks.Marks[ar.Mark].Distance));}
            // Under the objective tag (below the transfer banner while one shows); the quota module shows LAST n in its own row instead.
            if(!expeditionQuota.Shown)NativeQuotaMarksHud.Tag(width*.5f,v.transfer!=null&&v.transfer.stage!=ExpeditionTransferStage.None?110:54,q.Remaining,level);
        }
    }

    public sealed partial class ArenaPresentation
    {
        /// <summary>A horde add's presented root this frame (Core/NativeHordeFlow: where its drawn rig stands, smooth on every peer,
        /// from the host's body or a guest's replicated frames); false when the kinds presentation did not pose it this frame (the
        /// caller uses the body). Read only.</summary>
        public bool HordeDrawnRoot(int id,out Vector3 root)
        {
            if(hordeKindStates.TryGetValue(id,out var s)&&s.Frame==hordeKindFrame&&s.Kind>=0&&s.Flow.Follow.Started){var p=s.Flow.Position;root=new Vector3(p.X,p.Y,p.Z);return true;}
            root=default;return false;
        }
    }
}
