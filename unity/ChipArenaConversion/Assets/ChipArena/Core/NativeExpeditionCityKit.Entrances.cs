using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The town kit's live instance entrances (Claude, 2026-10-17; docs/EXPEDITION-HUB.md "Join circle presentation", the hub sides
    // of docs/EXPEDITION-CENTRAL-STATION.md and EXPEDITION-CITY-IRONWORKS.md 10.6). When the hub record routes to the area whose
    // live layout door stands at a doorway block (exp-layout-5: exp-transit, exp-foundry), that doorway is an entrance, not a sealed
    // frontage, dressed in its zone's accent (NativeExpeditionJoinZones): the station's boards give way to
    // the airlock (drawn by the state pieces at the door's closure, as at Ward Zero) framed in the station's cream tile and teal;
    // the foundry's welded shutter is rolled up into its drum over the airlock, with its gate sign and amber beacons; the signs name
    // the zones; each join circle has a light in its zone's accent. A reserved route keeps the sealed look. Static dressing only:
    // no collider, spawn, record or route moves; the doorway blocks themselves stay as they are.
    public sealed partial class NativeExpeditionCityKit
    {
        private readonly List<string> lettering=new List<string>();
        /// <summary>Every line of block lettering the kit drew (at least one glyph placed), in order: the checks read the signs.</summary>
        public string[] Lettering=Array.Empty<string>();

        /// <summary>Whether the hub record routes to `area` (a live entrance), not a reserved doorway.</summary>
        private bool RouteLive(string area){if(hub==null||area==null)return false;foreach(var r in hub.Routes)if(r.To==area)return !r.Reserved;return false;}
        /// <summary>The area whose door (in the live layout) stands against block i: the door whose closure volume lies within a
        /// metre of the block's box; null when none does. The doorway dressing keys on this, never on a map id.</summary>
        private string DoorArea(int i)
        {
            var b=blocks[i];if(b==null)return null;
            foreach(var d in area?.doors??Array.Empty<ExpeditionDoorLayout>())
            {
                if(d.closure==null)continue;double dx=Math.Abs(d.closure.x-b.pos.x)-b.size.x/2,dz=Math.Abs(d.closure.z-b.pos.z)-b.size.z/2;if(dx>1||dz>1)continue;
                foreach(var a in area.anchors??Array.Empty<ExpeditionWorldAnchor>())if(a.id==d.anchor)return a.building;
            }
            return null;
        }
        /// <summary>The live zone a doorway block opens onto (its door's area routed by the hub record), else null.</summary>
        private string LiveDoor(int i){var to=DoorArea(i);return RouteLive(to)?to:null;}
        /// <summary>A zone's lettering colour: its accent toward white.</summary>
        private static int ZoneInk(string to)=>NativeExpeditionJoinScene.Mix(NativeExpeditionJoinZones.Of(to).Accent,0xFFFFFF,.4f);
        /// <summary>A route's name on the town's signs: the zone's name (`brief`: its short name, for the wall signs and the painted
        /// markers), or SEALED after the short name while the doorway is reserved.</summary>
        public static string RouteSign(ExpeditionHubRoute r,bool brief)
        {
            var z=NativeExpeditionJoinZones.Of(r.To);string name=brief?z.Short:z.Name;
            return r.Reserved?z.Short+" SEALED":name;
        }

        // ---------------- City Ironworks: the shutter rolled up over the airlock ----------------
        private void FoundryLive(int i,string to)
        {
            int accent=NativeExpeditionJoinZones.Of(to).Accent;
            var f=MakeFace(i,0,1);if(f==null)return;double u=(f.U0+f.U1)/2,w=f.U1-f.U0,h=f.Top-f.Base;
            // The shutter's band over the airlock (the unit's header reaches 4.2 m): the curtain rolled into its drum, a hazard
            // hem, the gate's sign lit amber, and the guide rails down either side of the opening.
            Begin();FBox(f,u,h-.4,.03,w,.8,.06,P(NativeBlockLooks.Corrugated,0x4A4E4C,RoleLandmark));
            FBox(f,u,h-.36,.24,w-.3,.52,.42,With(P(NativeBlockLooks.SteelGreen,0x3A3E3A,RoleLandmark),shadow:true));
            FBox(f,u,4.31,.47,w-.5,.08,.04,With(P(NativeBlockLooks.Demolition,0,RoleLandmark),detail:true));
            FBox(f,u,h-.36,.455,TextWidth("NO 1 GATE",.07)+.36,.32,.02,P(NativeBlockLooks.Plain,0x161412,RoleSign));
            Text(f,"NO 1 GATE",u,h-.36-2.5*.07,.47,.07,.012,Glow(accent,1.6f,RoleSign,DriveSteady));End();
            foreach(double s in new[]{-1.0,1.0})
            {
                double ru=u+s*(w/2+.25);Begin();FBox(f,ru,(h-.8)/2,.08,.24,h-.8,.16,P(NativeBlockLooks.Demolition,0,RoleLandmark));End();
                // An amber beacon on each rail's head, turning (a strobe drive), and its light.
                At(f,ru,0,.25,out double bx,out double bz);Begin();Box(i,bx,f.Base+h-.62,bz,.26,.22,.26,P(NativeBlockLooks.SteelGreen,0x2A2A28,RoleLandmark));
                Box(i,bx,f.Base+h-.42,bz,.2,.18,.2,Glow(AmberHex,2.2f,RoleLandmark,DriveStrobe,s>0?.5f:0));Light(bx,f.Base+h-.4,bz,AmberHex,.7,8,.9,GlowBeacon,s>0?.5f:0);End();
            }
            // The gate sign's light over the airlock, in the zone's amber (a sign's glow, not a street lamp: the wilds stay dark).
            At(f,u,0,1.4,out double lx,out double lz);Light(lx,f.Base+4.1,lz,accent,.9,8,.4,GlowNeon);
        }

        // ---------------- Central Station: the boards gone, the airlock framed in the station's tile ----------------
        private void TransitLive(int i,string to)
        {
            int accent=NativeExpeditionJoinZones.Of(to).Accent;
            var f=MakeFace(i,2,1);if(f==null)return;double h=f.Top-f.Base;
            // The airlock unit covers the doorway (state pieces); the frontage's edges either side are the station's cream tile with a
            // teal edge light.
            foreach(double s in new[]{-1.0,1.0})
            {
                double edge=s*3.6,outer=s*(f.U1-.02);double u0=Math.Min(edge,outer),u1=Math.Max(edge,outer);if(u1-u0<.08)continue;
                Begin();FBox(f,(u0+u1)/2,h/2,.03,u1-u0,h,.06,P(NativeWardZeroLooks.Tile,0xC8BCA0,RoleLandmark,NativeCyberCityLooks.Bank));
                FBox(f,s>0?u0+.03:u1-.03,h/2,.07,.04,h-.2,.02,Glow(accent,1.3f,RoleLandmark,DriveSteady));End();
            }
            // The canopy's teal downlight over the circle's side of the airlock.
            At(f,0,0,1.6,out double lx,out double lz);Light(lx,f.Base+4.1,lz,accent,.85,8,.4,GlowNeon);
        }

        // ---------------- the join circles' lights ----------------
        /// <summary>A light at each join circle in its zone's accent, low over the pad's emitter (the town's light picker lights
        /// the pad, the pilots in it and the street round it from there; NativeExpeditionJoinScene draws the pad).</summary>
        private void EntranceLights()
        {
            foreach(var d in area?.doors??Array.Empty<ExpeditionDoorLayout>())
            {
                if(d.join==null)continue;string building=null;foreach(var a in area.anchors??Array.Empty<ExpeditionWorldAnchor>())if(a.id==d.anchor){building=a.building;break;}
                Light(d.join.x,d.join.y+.35,d.join.z,NativeExpeditionJoinZones.Of(building).Accent,1.1,8.5,.22,GlowNeon);
            }
        }
    }
}
