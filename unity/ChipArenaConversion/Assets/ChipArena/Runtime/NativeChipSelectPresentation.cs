using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;
using NVector4=System.Numerics.Vector4;
namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        // PvE chip selection, as the other pilots see it (Core/NativeChipSelect.cs; docs/PVE-CHIP-SELECT.md): every party pilot
        // whose PvE row has an open pick (the replicated NativePveRowFrame.open, rebuilt into NativePveRow.Open on every guest)
        // raises the display wrist and looks down at a hardlight panel over it (NativeChipSelectPose, laid over the route's pose
        // through the rig's Overlay after it is posed), and the hologram (NativeChipSelectHolo) is drawn with
        // Resources/NativeChipSelect.shader: one world mesh for every selecting pilot, one for the first-person view model (the
        // local pilot's wrist, seen when the personal pick screen is not covering the arena: the close after Choose later, a
        // menu or a hold over the open pick). The "SELECTING CHIP" tag is ConversionGame's (NativeChipSelectHud.cs). Reads the
        // arena the pilots belong to (WeaponLoadout: live, guest replica; the killcam replay has no PvE rows) and the snapshot's
        // events (the red alarm); writes nothing back. Allocation-free per frame once a pilot's slot exists.
        // An Expedition (ExpeditionChoosing / ExpeditionPlant set): the look follows members[].choosing (mine in first person too: the
        // authority holds the chooser), a hit flinches it (NativeChipSelect.Flinch: the hit reaction shows through), and each pick plays
        // the wrist plant over it (Core/NativeChipPlant.cs: the chosen card leaves the panel as the chip and goes into the cuff, on
        // NativeDraftPlant's timeline, the look held up through it even after the tray has closed).
        private sealed class ChipSelectSlot
        {
            public readonly NativeChipSelectTrack Track=new NativeChipSelectTrack();public readonly NativeChipSelectPose Pose=new NativeChipSelectPose();
            // The open pick's cards (cached when the pick changes): count, rarity colours (sRGB hex), rainbow, the beat (the pick's
            // replicated grant time: the gestures' clock) and the gesture seed.
            public int Pick=-1,Count=3,Seed,C0=NativeChipSelectHolo.Cyan,C1=NativeChipSelectHolo.Cyan,C2=NativeChipSelectHolo.Cyan;public bool Rainbow;public float Beat;
            // Posed this frame (the hologram and the tag draw only then), the rig root's frame then, and the look's clocks.
            public int Frame=-1;public Matrix4x4 Root;public float Weight,Age,Redeal,Time,Scale=1,RootScale=1;public ChipSelectGesture Gesture;
            // The Expedition wrist plant (Core/NativeChipPlant.cs): its layer, whether it is posed this frame, its cue's key, the card it
            // took, the chip's colour, whether another pick follows (latched over the return) and, after a plant that did not stay, until
            // when the look waits before it rises again.
            public readonly NativeChipPlantPose Plant=new NativeChipPlantPose();public bool Planting,PlantStay;public int PlantKey=int.MinValue,PlantChosen=-1,PlantRgb=NativeChipSelectHolo.Cyan;public float PlantAge=-1,SettleUntil=float.NegativeInfinity;
        }
        private readonly Dictionary<int,ChipSelectSlot> chipSelect=new Dictionary<int,ChipSelectSlot>();private readonly ChipSelectSlot chipView=new ChipSelectSlot();
        private Func<CapturedRig.Pose,bool> chipSelectLayer;private ChipSelectSlot chipLayerSlot;private ChipSelectInput chipLayerInput;private ChipPlantInput chipLayerPlant;private bool chipLayerPlanting;
        private sealed class ChipHoloSink : INativeChipHoloSink
        {
            public readonly List<Vector3> Points=new List<Vector3>(256);public readonly List<Color> Colors=new List<Color>(256);public readonly List<Vector4> Uv=new List<Vector4>(256),Extra=new List<Vector4>(256);public readonly List<int> Indices=new List<int>(384);
            public int Vertex(NVector at,NVector4 color,NVector4 uv,NVector4 extra){Points.Add(new Vector3(at.X,at.Y,at.Z));Colors.Add(new Color(color.X,color.Y,color.Z,color.W));Uv.Add(new Vector4(uv.X,uv.Y,uv.Z,uv.W));Extra.Add(new Vector4(extra.X,extra.Y,extra.Z,extra.W));return Points.Count-1;}
            public void Triangle(int a,int b,int c){Indices.Add(a);Indices.Add(b);Indices.Add(c);}
            public void Clear(){Points.Clear();Colors.Clear();Uv.Clear();Extra.Clear();Indices.Clear();}
        }
        private readonly ChipHoloSink chipWorldSink=new ChipHoloSink(),chipViewSink=new ChipHoloSink();private Mesh chipWorldMesh,chipViewMesh;private Material chipHoloMaterial;private bool chipHoloFailed;
        private static readonly int ChipHoloId=Shader.PropertyToID("_ChipHolo");
        /// <summary>Dev Sandbox "Preview Chip Selecting": every sandbox pilot shows the look on stand-in offers until it is turned off.</summary>
        public bool ChipSelectPreview;private float chipPreviewAt=float.NaN;
        // The stand-in offers of the preview (a rare, an epic and a legendary card).
        private static readonly string[] ChipPreviewRarities={"rare","epic","legendary"};
        /// <summary>
        /// An Expedition's open picks, set by ConversionGame while a run is bound (NativeExpeditionChipScreen.cs; null otherwise, and the
        /// PvE rows drive the look). It returns pilot `id`'s open pick, or null when it is not choosing: the view's public
        /// members[].choosing (the host's flag, PR #233) as a stand-in NativePvePick with a stable id and the time it was first seen. My
        /// own carries my shown reward's offers and tone; anyone else's has no Offers (the view's privacy rule), so its cards draw cyan.
        /// </summary>
        public Func<int,NativePvePick> ExpeditionChoosing;
        /// <summary>
        /// An Expedition's wrist plants, set by ConversionGame with ExpeditionChoosing (NativeExpeditionChipScreen.cs): pilot `id`'s plant
        /// at presentation time `time` (ChipPlantCue: none when not planting). Mine runs from my pick (the tray's follow-through clock);
        /// anyone else's from the authority's Install event for them. The authority holds the chooser until the final Install plus
        /// NativeDraftPlant.ReturnEnd (PR #237), the plant's span, so the look stays up through it even after the tray has closed.
        /// </summary>
        public Func<int,float,ChipPlantCue> ExpeditionPlant;
        /// <summary>Set by ConversionGame each frame: my Expedition chip tray (or its follow-through) is on screen. First person, my
        /// display wrist eases beside the tray (NativeChipSelectPose.TrayWrist) instead of under its cards.</summary>
        public bool ChipTrayShown;private float chipTrayView;

        // Whether pilot `id` shows the look, and its open pick (null in the preview).
        private bool ChipSelecting(int id,float time,out NativePvePick pick)
        {
            pick=null;var arena=WeaponLoadout;if(arena==null)return false;var p=LoadoutPilot(id);if(p==null)return false;
            if(arena.Sandbox&&ChipSelectPreview){if(float.IsNaN(chipPreviewAt))chipPreviewAt=time;return p.Hp>0&&p.Seat.connected;}
            if(!arena.Sandbox||!ChipSelectPreview)chipPreviewAt=float.NaN;
            var v=arena.Pve;
            if(ExpeditionChoosing!=null){pick=ExpeditionChoosing(id);return NativeChipSelect.Selecting(true,pick?.Id??0,p.Hp>0,p.Seat.connected,v?.Holding==true,!string.IsNullOrEmpty(v?.Cinematic),false);}
            if(v==null||!v.Rows.TryGetValue(id,out var row))return false;pick=row.Open;
            return NativeChipSelect.Selecting(true,row.Open?.Id??0,p.Hp>0,p.Seat.connected,v.Holding,!string.IsNullOrEmpty(v.Cinematic),v.Stage=="ended");
        }
        // The open pick's cards and beat, read once when it changes (PveChip searches the content).
        private void ChipCards(ChipSelectSlot slot,int id,NativePvePick pick,int pickId)
        {
            slot.Pick=pickId;slot.Seed=NativeChipSelect.Seed(id,pickId);slot.Rainbow=pick?.Tier=="rainbow";var arena=WeaponLoadout;
            int count=pick?.Offers?.Count??ChipPreviewRarities.Length;slot.Count=Mathf.Clamp(count,1,3);slot.Beat=pick!=null?pick.At:chipPreviewAt;
            for(int i=0;i<3;i++)
            {
                string rarity=pick==null?ChipPreviewRarities[i]:i<count&&pick.Offers!=null?arena?.PveChip(pick.Offers[i])?.rarity:null;
                int hex=rarity==null?NativeChipSelectHolo.Cyan:ColorHex(NativeChipCard.Rarity(rarity));if(i==0)slot.C0=hex;else if(i==1)slot.C1=hex;else slot.C2=hex;
            }
        }
        private static int ColorHex(Color c)=>(Mathf.RoundToInt(Mathf.Clamp01(c.r)*255)<<16)|(Mathf.RoundToInt(Mathf.Clamp01(c.g)*255)<<8)|Mathf.RoundToInt(Mathf.Clamp01(c.b)*255);
        // PrepareCapturedPose, after the route's pose (and the other overlays): the selecting pose over the arena rig.
        private void ChipSelectPose(CapturedAnimation.Player rig,Actor a,float time)
        {
            if(Viewer||rig==null||a==null)return;bool selecting=ChipSelecting(a.Id,time,out var pick);
            var cue=ExpeditionChoosing!=null&&ExpeditionPlant!=null?ExpeditionPlant(a.Id,time):ChipPlantCue.None;bool planting=cue.Active&&a.Alive;
            if(!chipSelect.TryGetValue(a.Id,out var slot)){if(!selecting&&!planting)return;slot=new ChipSelectSlot();chipSelect[a.Id]=slot;}
            // A plant that ended with nothing to follow it has already lowered the look over its return: drop it there, and let it rise
            // again only after Settle (the authority's flag can read choosing a moment past the plant's end; another pilot's queued pick
            // then rises again).
            if(slot.Planting&&!planting){slot.Planting=false;if(!slot.PlantStay){slot.Track.Reset();slot.SettleUntil=time+NativeChipPlant.Settle;}}
            if(!planting&&time<slot.SettleUntil&&slot.SettleUntil-time<=NativeChipPlant.Settle)selecting=false;
            if(planting)
            {
                if(cue.Key!=slot.PlantKey){slot.PlantKey=cue.Key;slot.Plant.Reset();var chip=cue.Chip==null?null:WeaponLoadout?.PveChip(cue.Chip);slot.PlantRgb=chip!=null?ColorHex(NativeChipCard.Rarity(chip.rarity)):NativeChipSelectHolo.Cyan;slot.PlantChosen=cue.Chosen;}
                if(cue.Age<NativeDraftPlant.ReturnStart)slot.PlantStay=cue.Stay;slot.Planting=true;slot.PlantAge=cue.Age;
            }
            // A plant keeps the look's pick (its cards, colours and beat): the tray closing or the next reward never re-deals it mid-plant.
            int pickId=planting&&slot.Track.Pick!=0?slot.Track.Pick:pick?.Id??(selecting||planting?1:slot.Track.Pick);float w=slot.Track.Step(selecting||planting,pickId,time,!a.Alive);
            if(w<=1e-4f){slot.Frame=-1;slot.Planting=false;return;}
            if(selecting&&!planting&&slot.Pick!=pickId)ChipCards(slot,a.Id,pick,pickId);
            float look=planting?NativeChipPlant.Weight(w,cue.Age,slot.PlantStay):w,flinch=NativeChipSelect.Flinch(ChipHitAge(a.Id,time));
            chipLayerSlot=slot;chipLayerInput=new ChipSelectInput{Weight=look,Clock=time-slot.Beat,Time=time,Reach=rig.WeaponReach,Spec=a.SourceSpec,Seed=slot.Seed,Cards=slot.Count,Reduced=WeaponEffectsQuality<.5f,
                Flinch=flinch,Hush=planting?NativeChipPlant.Hush(cue.Age):0};
            chipLayerPlanting=planting;chipLayerPlant=new ChipPlantInput{Age=cue.Age,Weight=look,Flinch=flinch,Chosen=slot.PlantChosen,Cards=slot.Count,Stay=slot.PlantStay};
            chipSelectLayer??=ChipLayer;rig.Overlay(chipSelectLayer);
            slot.Frame=Time.frameCount;slot.Root=rig.Root.transform.localToWorldMatrix;slot.Weight=look;slot.Time=time;slot.Age=slot.Track.Age(time);slot.Redeal=slot.Track.SinceChange(time);slot.Gesture=slot.Pose.Gesture;slot.Scale=slot.RootScale=1;
        }
        // The overlay (cached once: no closure per frame): the hologram pose, then the wrist plant over it.
        private bool ChipLayer(CapturedRig.Pose pose)
        {
            chipLayerSlot.Pose.Apply(pose,chipLayerInput);if(chipLayerPlanting)chipLayerSlot.Plant.Apply(pose,chipLayerPlant,chipLayerSlot.Pose,chipLayerInput);return true;
        }
        // Seconds since the newest damaging hit on pilot `id` (the live arena's buffer, as the tag's alarm reads it), or +inf.
        private float ChipHitAge(int id,float time)
        {
            var events=WeaponLoadout?.Events;if(events==null)return float.PositiveInfinity;float window=NativeChipSelect.FlinchRise+NativeChipSelect.FlinchFall;
            for(int i=events.Count-1;i>=0;i--){var e=events[i];if(time-e.Time>=window)break;if(NativeEventFeedback.Attacked(e,id,time,window))return time-e.Time;}
            return float.PositiveInfinity;
        }
        /// <summary>Where pilot `id`'s plant or hologram is this frame (world space, for the tray's follow-through to fly its card into):
        /// the chip while it plants, else the panel; first person (mine) the view model's. False when nothing is posed.</summary>
        public bool ChipSelectFocus(int id,out Vector3 world,out bool viewModel)
        {
            world=default;viewModel=false;ChipSelectSlot slot;
            if(FirstPerson&&id==LocalId&&chipView.Frame==Time.frameCount){slot=chipView;viewModel=true;}else if(!chipSelect.TryGetValue(id,out slot)||slot.Frame!=Time.frameCount)return false;
            var v=slot.Planting?slot.Plant.Chip:slot.Pose.PanelCentre;world=slot.Root.MultiplyPoint3x4(new Vector3(-v.X,v.Y,v.Z));return true;
        }
        // RenderFirstPerson, once the view rig is placed (and swayed): the local pilot's raised wrist and its hologram in view.
        private void ChipSelectView(CapturedAnimation.Player view,Actor a,float time)
        {
            chipView.Frame=-1;if(Viewer||ScopeActive||view==null||a==null||ViewCamera==null||!chipSelect.TryGetValue(a.Id,out var slot)||slot.Frame!=Time.frameCount)return;
            var root=view.Root.transform;var cam=ViewCamera.transform;
            NVector Local(Vector3 world){var l=root.InverseTransformPoint(world);return new NVector(-l.x,l.y,l.z);}
            var wrist=NativeChipSelectPose.ViewWrist(a.SourceSpec);
            chipTrayView=Mathf.MoveTowards(chipTrayView,ChipTrayShown?1:0,Time.unscaledDeltaTime/.3f);
            if(chipTrayView>0){float tanV=Mathf.Tan(ViewCamera.fieldOfView*.5f*Mathf.Deg2Rad);var beside=NativeChipSelectPose.TrayWrist(a.SourceSpec,tanV*ViewCamera.aspect,tanV);wrist=NVector.Lerp(wrist,beside,Mathf.SmoothStep(0,1,chipTrayView));}
            float flinch=NativeChipSelect.Flinch(ChipHitAge(a.Id,slot.Time));bool planting=slot.Planting&&NativeChipPlant.Active(slot.PlantAge);
            if(chipView.PlantKey!=slot.PlantKey){chipView.PlantKey=slot.PlantKey;chipView.Plant.Reset();}
            chipLayerSlot=chipView;chipLayerInput=new ChipSelectInput{Weight=slot.Weight,Clock=slot.Time-slot.Beat,Time=slot.Time,Reach=view.WeaponReach,Spec=a.SourceSpec,Seed=slot.Seed,Cards=slot.Count,Reduced=WeaponEffectsQuality<.5f,FirstPerson=true,
                ViewWrist=Local(cam.TransformPoint(new Vector3(wrist.X,wrist.Y,wrist.Z))),ViewEye=Local(cam.position),Flinch=flinch,Hush=planting?NativeChipPlant.Hush(slot.PlantAge):0};
            chipLayerPlanting=planting;chipLayerPlant=new ChipPlantInput{Age=slot.PlantAge,Weight=slot.Weight,Flinch=flinch,Chosen=slot.PlantChosen,Cards=slot.Count,FirstPerson=true,Stay=slot.PlantStay};
            chipSelectLayer??=ChipLayer;view.Overlay(chipSelectLayer);
            chipView.Pick=slot.Pick;chipView.Count=slot.Count;chipView.Seed=slot.Seed;chipView.Rainbow=slot.Rainbow;chipView.C0=slot.C0;chipView.C1=slot.C1;chipView.C2=slot.C2;chipView.Beat=slot.Beat;
            chipView.Planting=planting;chipView.PlantRgb=slot.PlantRgb;chipView.PlantChosen=slot.PlantChosen;chipView.PlantStay=slot.PlantStay;
            chipView.Frame=Time.frameCount;chipView.Root=root.localToWorldMatrix;chipView.Weight=slot.Weight;chipView.Time=slot.Time;chipView.Age=slot.Age;chipView.Redeal=slot.Redeal;chipView.Gesture=chipView.Pose.Gesture;chipView.Scale=root.lossyScale.x*chipView.Pose.PanelScale;chipView.RootScale=root.lossyScale.x;
            if(BuildChipHolo(chipViewSink,chipView,true,null,a.Id)>0)DrawChipHolo(chipViewSink,ref chipViewMesh,ViewCamera,10);
        }
        // Render, after every rig is posed: the world hologram of every selecting pilot (not my own in first person: its view
        // model carries it).
        private void RenderChipSelect(Snapshot s)
        {
            if(Viewer||chipSelect.Count==0)return;chipWorldSink.Clear();int built=0;
            foreach(var pair in chipSelect)
            {
                var slot=pair.Value;if(slot.Frame!=Time.frameCount||FirstPerson&&pair.Key==LocalId||!ReviveVisible(pair.Key,slot.Time))continue;
                built+=BuildChipHolo(chipWorldSink,slot,false,s,pair.Key);
            }
            if(built>0)DrawChipHolo(chipWorldSink,ref chipWorldMesh,null,0);
        }
        private int BuildChipHolo(ChipHoloSink sink,ChipSelectSlot slot,bool first,Snapshot s,int id)
        {
            if(first)sink.Clear();var pose=slot.Pose;var m=slot.Root;
            NVector P(NVector v){var w=m.MultiplyPoint3x4(new Vector3(-v.X,v.Y,v.Z));return new NVector(w.x,w.y,w.z);}
            NVector D(NVector v){var w=m.MultiplyVector(new Vector3(-v.X,v.Y,v.Z)).normalized;return new NVector(w.x,w.y,w.z);}
            var frame=new ChipHoloFrame{Emitter=P(pose.Emitter),Centre=P(pose.PanelCentre),Right=D(pose.PanelRight),Up=D(pose.PanelUp),Normal=D(pose.PanelNormal),
                Width=(first?NativeChipSelectPose.ViewWidth:NativeChipSelectPose.PanelWidth)*slot.Scale,Height=(first?NativeChipSelectPose.ViewHeight:NativeChipSelectPose.PanelHeight)*slot.Scale};
            var look=new ChipHoloLook{Weight=slot.Weight,Age=slot.Age,Redeal=slot.Redeal,Time=slot.Time,Gesture=slot.Gesture,Count=slot.Count,Seed=slot.Seed,Rainbow=slot.Rainbow,
                Attacked=ChipAttacked(s,id,slot.Time),Reduced=WeaponEffectsQuality<.5f,Low=LowGraphics,FirstPerson=first,C0=slot.C0,C1=slot.C1,C2=slot.C2};
            // The wrist plant: the chosen card leaves the folding panel as the chip, the cuff pulses (Core/NativeChipPlant.cs).
            if(slot.Planting)
            {
                var plant=slot.Plant;look.Plant=true;look.Fold=plant.Fold;look.Fade=plant.Fade;look.Chosen=plant.Card;look.ChipShown=plant.ChipShown;look.ChipSize=plant.ChipSize*slot.RootScale;look.ChipRgb=slot.PlantRgb;look.Pulse=plant.Pulse;
                look.Gesture=new ChipSelectGesture{Card=plant.Card,SinceContact=-1};frame.Chip=P(plant.Chip);frame.ChipRight=D(plant.ChipRight);frame.ChipUp=D(plant.ChipUp);frame.CuffNormal=D(plant.CuffNormal);
            }
            return NativeChipSelectHolo.Build(sink,frame,look);
        }
        // The Electron alarm (hologram.ts underAttack): a damaging hit on the pilot in the last AttackedSeconds (the snapshot's events; the
        // view model reads the live arena's buffer).
        private bool ChipAttacked(Snapshot s,int id,float time)
        {
            // Newest first, stopping at the window (events append in time order).
            if(s?.Events!=null){var list=s.Events;for(int i=list.Length-1;i>=0;i--){if(time-list[i].Time>=NativeChipSelectHolo.AttackedSeconds)break;if(NativeEventFeedback.Attacked(list[i],id,time,NativeChipSelectHolo.AttackedSeconds))return true;}return false;}
            var events=WeaponLoadout?.Events;if(events==null)return false;
            for(int i=events.Count-1;i>=0;i--){var e=events[i];if(time-e.Time>=NativeChipSelectHolo.AttackedSeconds)break;if(NativeEventFeedback.Attacked(e,id,time,NativeChipSelectHolo.AttackedSeconds))return true;}return false;
        }
        private void DrawChipHolo(ChipHoloSink sink,ref Mesh mesh,Camera camera,int layer)
        {
            if(chipHoloFailed||sink.Points.Count==0)return;
            if(chipHoloMaterial==null)
            {
                var shader=Resources.Load<Shader>("NativeChipSelect");
                if(shader==null||!shader.isSupported){chipHoloFailed=true;Debug.LogWarning("The chip selection hologram needs shader NativeChipSelect; the pose and the tag still show.");return;}
                chipHoloMaterial=new Material(shader){name="Native chip selection hologram",hideFlags=HideFlags.HideAndDontSave};materials.Add(chipHoloMaterial);
            }
            if(mesh==null){mesh=new Mesh{name="Native chip selection hologram",hideFlags=HideFlags.HideAndDontSave};mesh.MarkDynamic();}
            mesh.Clear();mesh.SetVertices(sink.Points);mesh.SetColors(sink.Colors);mesh.SetUVs(0,sink.Uv);mesh.SetUVs(1,sink.Extra);mesh.SetTriangles(sink.Indices,0,true);
            chipHoloMaterial.SetVector(ChipHoloId,new Vector4(hazardClock,WeaponEffectsQuality<.5f?0:1,0,LowGraphics?1:0));
            Graphics.DrawMesh(mesh,Matrix4x4.identity,chipHoloMaterial,layer,camera,0,null,ShadowCastingMode.Off,false);
        }
        /// <summary>The tag's state for pilot `id` this frame (NativeChipSelectHud.cs): its weight, seconds since this run opened,
        /// and whether it is being hit; false when it shows no look.</summary>
        public bool ChipSelectTagState(int id,out float weight,out float age,out bool attacked)
        {
            weight=age=0;attacked=false;if(!chipSelect.TryGetValue(id,out var slot)||slot.Frame!=Time.frameCount)return false;
            weight=slot.Weight;age=slot.Age;attacked=ChipAttacked(null,id,slot.Time);return weight>1e-3f;
        }
    }
}
