using System;
using System.Collections.Generic;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The caches in the live scene (Claude, 2026-11-19; R9, W6): each frame the open moments read from the bound view (Core
    /// NativeCacheClock: the generation changed with the cooldown still ahead; the burst public, the buyer only from the viewer's
    /// own private reward; a late joiner's first-seen clock), handed to the presentation (ArenaPresentation.CacheShows) and the
    /// hologram pieces (their lid and flare); the cache.* sounds on R9's beats (requests: each plays an existing sound until its
    /// recording lands); the F prompt at the cache (RARE CACHE · $175 · 3 CHIPS · KEEP 1 · F · OPEN CACHE, or $75 SHORT in red,
    /// or RESTOCKS IN 0:42; every word through ExpeditionLabels); and the DEV preview of the four lockers and the hologram (DEV
    /// panel · Previews). The steel locker draws only where A2's solid chest block stands in the map (CacheSolid); until then
    /// the live cache keeps its hologram body. Presentation only: buying is still the scene's F interaction with the host's
    /// reach test (NativeArena.TryExpeditionInteract); nothing here buys, rolls, places or sends.
    /// </summary>
    public sealed partial class ConversionGame
    {
        private readonly NativeCacheClock cacheClock=new NativeCacheClock();
        private readonly List<NativeCacheShow> cachePreviewShows=new List<NativeCacheShow>(8),cachePreviewClicks=new List<NativeCacheShow>(8);
        private readonly Dictionary<string,float> cacheHeard=new Dictionary<string,float>(StringComparer.Ordinal);
        private readonly Dictionary<string,bool> cacheSolidAt=new Dictionary<string,bool>(StringComparer.Ordinal);private string cacheSolidMap;
        private Func<ExpeditionWorldAnchor,bool> cacheSolid;private double cachePreviewPrevious=double.NaN;
        private readonly ExpeditionLabels cacheLabels=new ExpeditionLabels();

        /// <summary>ExpeditionWorldFrame (LateUpdate, before the presentation renders): this frame's caches.</summary>
        private void CacheWorldFrame()
        {
            if(presentation==null)return;presentation.CacheReduced=prefs.reducedEffects;
            var me=arena?.ViewPilot;presentation.CacheDockKnown=me!=null&&me.Hp>0;if(me!=null)presentation.CacheDock=me.Body.position+Vector3.up*.35f;
            if(NativeCachePreview.On&&arena!=null&&cameraView!=null&&!menu)
            {
                double now=arena.Time;if(double.IsNaN(NativeCachePreview.Start))CachePreviewPlace(now);
                cachePreviewShows.Clear();cachePreviewClicks.Clear();NativeCachePreview.Fill(cachePreviewShows,cachePreviewClicks,now,double.IsNaN(cachePreviewPrevious)?now:cachePreviewPrevious);cachePreviewPrevious=now;
                presentation.CacheShows=cachePreviewShows;CacheSounds(cachePreviewShows,cachePreviewClicks);CachePreviewHologram(now);return;
            }
            cachePreviewPrevious=double.NaN;cachePreviewWorld?.Clear();
            if(LiveExpedition==null||!ExpeditionWorldShown||!expeditionMapReady){cacheClock.Observe(null,0);presentation.CacheShows=null;cacheHeard.Clear();return;}
            cacheClock.Solid=cacheSolid??=CacheSolid;cacheClock.Observe(LiveExpedition,Time.unscaledTimeAsDouble);
            // the steel locker stands for A2's solid block, so the block's own box is left undrawn (its collider stays)
            foreach(var id in cacheSolidBlocks)NativeArenaDressing.Hidden.Add(id);
            presentation.CacheShows=cacheClock.Items;CacheSounds(cacheClock.Items,cacheClock.Clicks);
        }
        private readonly HashSet<string> cacheSolidBlocks=new HashSet<string>(StringComparer.Ordinal);
        /// <summary>A2's solid chest block at an anchor: a map block of the chest's size (0.92 x 0.62 x 0.58 m, turned by the anchor's
        /// yaw) centred on it and standing on its floor. None exists before A2, so the live cache keeps its hologram.</summary>
        private bool CacheSolid(ExpeditionWorldAnchor a)
        {
            if(arena==null||a?.id==null)return false;if(cacheSolidMap!=arena.MapId){cacheSolidMap=arena.MapId;cacheSolidAt.Clear();cacheSolidBlocks.Clear();}
            if(cacheSolidAt.TryGetValue(a.id,out bool solid))return solid;var b=NativeCacheLockerModels.Block;
            foreach(var k in arena.Map?.blocks??Array.Empty<ContentBlock>())
            {
                if(k==null)continue;float dx=k.pos.x-a.x,dz=k.pos.z-a.z,lo=k.pos.y-k.size.y*.5f;
                bool size=Mathf.Abs(k.size.y-b.Y)<.03f&&(Mathf.Abs(k.size.x-b.X)<.03f&&Mathf.Abs(k.size.z-b.Z)<.03f||Mathf.Abs(k.size.x-b.Z)<.03f&&Mathf.Abs(k.size.z-b.X)<.03f);
                if(size&&dx*dx+dz*dz<.15f*.15f&&Mathf.Abs(lo-a.y)<.1f){solid=true;if(k.id!=null)cacheSolidBlocks.Add(k.id);break;}
            }
            if(cacheSolidAt.Count>64)cacheSolidAt.Clear();cacheSolidAt[a.id]=solid;return solid;
        }
        // The cache.* cues on R9's beats, once each as the opening's clock crosses them (none for a late joiner's opened cache),
        // and the restock's click. Positional at the cache.
        private void CacheSounds(List<NativeCacheShow> shows,List<NativeCacheShow> clicks)
        {
            if(nativeAudio==null)return;
            foreach(var s in shows)
            {
                if(s.Id==null||s.State!=NativeCacheState.Opening){if(s.Id!=null)cacheHeard.Remove(s.Id);continue;}
                float before=cacheHeard.TryGetValue(s.Id,out float b)?b:-1;if(s.Since<before)before=-1;cacheHeard[s.Id]=s.Since;
                if(before<0&&s.Since>.3f)continue;var at=new Vector3(s.X,s.Y+.5f,s.Z);
                foreach(var c in NativeCacheOpenFx.Cues(s.Tier,prefs.reducedEffects))if(c.At>before&&c.At<=s.Since)nativeAudio.Cue(c.Key,c.Fallback,at,c.Gain,c.Rate);
            }
            foreach(var s in clicks){var c=NativeCacheOpenFx.Restock;nativeAudio.Cue(c.Key,c.Fallback,new Vector3(s.X,s.Y+.5f,s.Z),c.Gain,c.Rate);}
            if(cacheHeard.Count>64)cacheHeard.Clear();
        }

        // ---- the F prompt at the cache (and the DEV preview's labels) ----
        /// <summary>CombatWorldHud: the prompt for the cache within reach (the layout's interaction point and range, from the feet
        /// with a clear line, as the host's reach test), and the DEV preview's labels.</summary>
        private void CacheHud(float width,float height)
        {
            if(Event.current.type!=EventType.Repaint||arena==null||cameraView==null)return;
            if(NativeCachePreview.On){CachePreviewLabels(width,height);return;}
            var me=arena.Local;if(LiveExpedition==null||me==null||me.Hp<=0||!ExpeditionWorldShown)return;
            var feet=me.Body.position-Vector3.up*NativeLocomotionRules.HalfHeight;string id=null;float best=float.MaxValue;
            foreach(var s in cacheClock.Items)
            {
                CacheReach(s,out var point,out float range);float d=(point-feet).magnitude;
                if(d<=range&&d<best&&arena.ExpeditionVisible(NativeArena.Vec(feet),NativeArena.Vec(point))){best=d;id=s.Id;}
            }
            if(id==null)return;
            foreach(var p in ExpeditionPieces.Pieces)if(p.kind==ExpeditionPieceKind.Chest&&p.anchor==id){CachePrompt(width,height,p);return;}
        }
        // The interaction point and range of a cache: the layout's (A2: anchor + forward x 0.9 m, range 2.0), else the anchor, 2 m.
        private void CacheReach(in NativeCacheShow s,out Vector3 point,out float range)
        {
            point=new Vector3(s.X,s.Y,s.Z);range=2;var layout=deploymentBundle?.Layout??expeditionLayout;if(layout?.areas==null)return;
            foreach(var a in layout.areas){if(a?.interactions==null)continue;foreach(var i in a.interactions)if(i!=null&&i.anchor==s.Id){point=new Vector3(i.x,i.y,i.z);range=i.range>0?i.range:2;return;}}
        }
        /// <summary>The prompt: the cache's title in its tier colour and its offer, then F · OPEN CACHE, or the shortfall in red, or the
        /// restock's countdown (the piece's own cached words).</summary>
        private static void CachePrompt(float width,float height,in ExpeditionWorldPiece p)
        {
            float y=height-188,w=520,x=width*.5f-w/2;var shade=C.Void(.85f);
            U.Text(new Rect(x,y,w,24),p.label,17,NativeExpeditionHud.Tone(p.tone),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.1f,shadow:shade);
            if(!string.IsNullOrEmpty(p.detail))U.Text(new Rect(x,y+24,w,20),p.detail,13,C.Ink(),TextAnchor.MiddleCenter,mono:true,shadow:shade);
            if(!p.actionable)return;
            if(p.extraAlert)U.Text(new Rect(x,y+46,w,22),p.extra,14,C.Danger(),TextAnchor.MiddleCenter,bold:true,mono:true,shadow:shade);
            else U.Text(new Rect(x,y+46,w,22),NativeCacheText.Prompt,15,C.Good(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f,shadow:shade);
        }

        // ---- the DEV preview (DEV panel · Previews · Cache openings) ----
        private NativeExpeditionWorldPresentation cachePreviewWorld;private bool cachePreviewWorldFailed;
        private readonly NativeCacheClock cachePreviewStamp=new NativeCacheClock();
        private readonly ExpeditionView cachePreviewView=new ExpeditionView{run="dev-cache-preview",area="dev-cache",anchors=new[]{new ExpeditionWorldAnchor{id="dev-cache-hologram",area="dev-cache",kind="chest"}},
            chests=new[]{new ExpeditionChestOffer{id="dev-cache-hologram",generation="dev",price=100}}};
        private static readonly int[] CachePreviewPrices={100,175,275,400};
        /// <summary>The DEV panel's toggle: the row stands 4.5 m ahead of you on your floor, its fronts toward you; its first cycle
        /// begins at the next whole 9 s of the arena clock.</summary>
        private void CachePreviewToggle()
        {
            NativeCachePreview.On=!NativeCachePreview.On;NativeCachePreview.Start=double.NaN;cachePreviewPrevious=double.NaN;
            DevSay(NativeCachePreview.On?"Cache openings preview · on (presentation only: the four lockers and the hologram cycle sale, opening and restock; odd cycles your view as the buyer)":"Cache openings preview · off",false);
        }
        private void CachePreviewPlace(double now)
        {
            // ahead along the view's yaw (the pilot's input yaw: a capture's place= turns it), the row's fronts back toward you
            var f=Quaternion.Euler(0,yaw,0)*Vector3.forward;var me=arena.Local;
            float floor=me!=null?me.Body.position.y-NativeLocomotionRules.HalfHeight-NativeLocomotionRules.Skin:cameraView.transform.position.y-1.6f;
            var at=(me!=null?me.Body.position:cameraView.transform.position)+f*4.5f;float rowYaw=Mathf.Atan2(f.x,f.z)*Mathf.Rad2Deg+180;
            NativeCachePreview.Place(at.x,floor,at.z,rowYaw,now);
        }
        // The hologram at the row's end through today's world pieces (its crate, its flare and its beacon column).
        private void CachePreviewHologram(double now)
        {
            if(cachePreviewWorld==null&&!cachePreviewWorldFailed){cachePreviewWorld=NativeExpeditionWorldPresentation.Create();cachePreviewWorldFailed=cachePreviewWorld==null;}
            if(cachePreviewWorld==null)return;NativeCacheShow h=default;foreach(var s in cachePreviewShows)if(!s.Locker)h=s;
            var a=cachePreviewView.anchors[0];a.x=h.X;a.y=h.Y;a.z=h.Z;a.yaw=h.Yaw;var o=cachePreviewView.chests[0];cachePreviewView.elapsed=now;
            o.cooldown=h.State==NativeCacheState.Opening?now+30:0;cachePreviewStamp.Items.Clear();cachePreviewStamp.Items.Add(h);
            cachePreviewWorld.Frame(cachePreviewView,cameraView.transform.position,now,prefs.reducedEffects,cachePreviewStamp);
        }
        // Each preview cache's label at its projected point, as the live world labels draw them: the title in its tier colour and
        // the offer; the second locker short of its price ($75 SHORT in red); the restock's countdown after the shut.
        private void CachePreviewLabels(float width,float height)
        {
            float tan=Mathf.Tan(cameraView.fieldOfView*.5f*Mathf.Deg2Rad),half=cameraView.pixelHeight*.5f;
            for(int i=0;i<cachePreviewShows.Count;i++)
            {
                var s=cachePreviewShows[i];var centre=new Vector3(s.X,s.Y+(s.Locker?.95f:NativeExpeditionWorld.GlyphHeight(ExpeditionPieceKind.Chest)),s.Z);var sp=cameraView.WorldToScreenPoint(centre);if(sp.z<=.05f)continue;
                var at=new Vector2(sp.x/U.S,(Screen.height-sp.y)/U.S);float k=half/(sp.z*tan)/U.S;int price=CachePreviewPrices[NativeCacheTiers.Index(s.Tier)];
                var p=new ExpeditionWorldPiece{kind=ExpeditionPieceKind.Chest,look=ExpeditionPieceLook.Chest,tier=s.Tier};
                if(s.State==NativeCacheState.ForSale)
                {
                    p.label=NativeCacheTiers.Title(s.Tier);p.detail=NativeCacheText.Detail(cacheLabels,price);p.tone=NativeCacheTiers.Tone(s.Tier);p.actionable=true;
                    if(s.Short){p.extra=NativeCacheText.Short(cacheLabels,75);p.extraAlert=true;}
                }
                else{p.label=NativeCacheText.Restocking(cacheLabels,Mathf.Max(0,42-(int)Mathf.Floor(s.Since)));p.detail=NativeCacheTiers.Title(s.Tier);p.tone=ExpeditionTone.Muted;p.settled=true;}
                NativeExpeditionHud.Piece(p,at,at,k,true,false,1,9);
            }
        }
    }
}
