using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>One chooser's wrist screen this frame (world space): the open screen, where it is drawn now, its unfold, the wrist
    /// emitter, and a pick's state (the plant's chip where the hand takes the card, its size, the card chosen).</summary>
    public struct ChipScreenState
    {
        public ChipScreenPlace Open,Shown;public float Unfold,PlantAge,CatchSize;public Vector3 Emitter,Catch;public bool Planting;public int Chosen,Count;
    }

    // The Expedition chip screen (Claude, 2026-10-15; owner: "we should also make it a holographic screen projected from the wrist. That
    // way its easier to notice were selecting a chip"; Core/NativeChipScreen.cs, docs/EXPEDITION-PRESENTATION.md "Chip picks"). In an
    // Expedition every choosing pilot's display wrist projects the chip screen in place of the small wrist panel: mine in my offers'
    // rarities under the faces the HUD draws over it (NativeExpeditionChipScreen.cs), anyone else's as blank cyan backs. The wrist plant's
    // chip and the cuff's pulse still come from the wrist hologram (its panel hidden; its chip once the hand has the card).
    public sealed partial class ArenaPresentation
    {
        /// <summary>Set by ConversionGame's HUD each frame for my screen: the card lit (the cursor's or the keyboard's; -1 none), the
        /// faces' height (m; 0 the standard) and whether the HUD draws the faces (then my chosen card flies on the HUD, not here).</summary>
        public int ChipScreenLit=-1;public float ChipScreenCardHeight;public bool ChipScreenFaces;
        /// <summary>The screen can be drawn (the hologram's shader loaded); else the HUD keeps the 2D tray.</summary>
        public bool ChipScreenAvailable=>!chipHoloFailed;

        private int BuildChipScreen(ChipHoloSink sink,ChipSelectSlot slot,ChipHoloFrame frame,ChipHoloLook look,int id)
        {
            var open=ScreenPlace(slot);bool mine=id==LocalId;
            var screen=new ChipScreenLook{Unfold=NativeChipSelectHolo.Unfold(slot.Weight)*(slot.Planting?slot.Plant.Fold:1),Time=slot.Time,Count=mine?slot.Count:3,Lit=mine?ChipScreenLit:-1,Seed=slot.Seed,
                CardHeight=mine?ChipScreenCardHeight:0,Mine=mine,Attacked=look.Attacked,Reduced=look.Reduced,Low=look.Low,C0=slot.C0,C1=slot.C1,C2=slot.C2,
                Plant=slot.Planting,FlyCard=!mine||!ChipScreenFaces,Faced=mine&&ChipScreenFaces,Chosen=mine?slot.PlantChosen:-1,PlantAge=slot.PlantAge,Fade=slot.Plant.Fade,Catch=frame.Chip,CatchSize=look.ChipSize};
            int quads=NativeChipScreen.Build(sink,open,frame.Emitter,screen);
            // The plant's chip once the hand has the card (the screen's card flies to it until then) and the cuff's pulse; no wrist panel.
            look.Weight=0;if(slot.PlantAge<NativeDraftPlant.Contact)look.ChipShown=false;
            return quads+NativeChipSelectHolo.Build(sink,frame,look);
        }
        // The open screen from the rig root's frame (its body centre, facing and scale: the rig's own, as the chooser camera's) and the
        // class's side (NativeChipScreen.OnLeft).
        private static ChipScreenPlace ScreenPlace(ChipSelectSlot slot)
        {
            var m=slot.Root;var body=m.MultiplyPoint3x4(Vector3.zero);var f=m.MultiplyVector(Vector3.forward);float facing=Mathf.Atan2(f.x,f.z)*Mathf.Rad2Deg,scale=f.magnitude;
            return NativeChipScreen.Place(new NVector(body.x,body.y,body.z),facing,slot.ScreenLeft,scale>.01f?scale:1);
        }
        /// <summary>Pilot `id`'s wrist screen as last posed (this frame or the one before), in an Expedition; false while its look is down.</summary>
        public bool ChipScreen(int id,out ChipScreenState state)
        {
            state=default;if(ExpeditionChoosing==null||!chipSelect.TryGetValue(id,out var slot)||slot.Frame<Time.frameCount-1||slot.Frame<0)return false;
            var open=ScreenPlace(slot);var m=slot.Root;var e=slot.Pose.Emitter;var emitter=m.MultiplyPoint3x4(new Vector3(-e.X,e.Y,e.Z));var c=slot.Plant.Chip;
            float unfold=NativeChipSelectHolo.Unfold(slot.Weight)*(slot.Planting?slot.Plant.Fold:1);
            state=new ChipScreenState{Open=open,Shown=NativeChipScreen.Shown(open,new NVector(emitter.x,emitter.y,emitter.z),unfold),Unfold=unfold,Emitter=emitter,Planting=slot.Planting,PlantAge=slot.PlantAge,
                Catch=m.MultiplyPoint3x4(new Vector3(-c.X,c.Y,c.Z)),CatchSize=slot.Plant.ChipSize*slot.RootScale,Chosen=slot.PlantChosen,Count=slot.Count};
            return true;
        }
        /// <summary>Pilot `id`'s display wrist (the screen's emitter, world) and the screen's side as last posed, for the chooser camera;
        /// false while its look is down.</summary>
        internal bool ChipSelectWrist(int id,out Vector3 world,out bool screenLeft)
        {
            world=default;screenLeft=false;if(!chipSelect.TryGetValue(id,out var slot)||slot.Frame<Time.frameCount-2||slot.Weight<=.05f)return false;
            var v=slot.Pose.Emitter;world=slot.Root.MultiplyPoint3x4(new Vector3(-v.X,v.Y,v.Z));screenLeft=slot.ScreenLeft;return true;
        }
    }
}
