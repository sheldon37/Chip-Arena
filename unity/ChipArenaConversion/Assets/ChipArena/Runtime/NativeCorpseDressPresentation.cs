using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // A dead pilot's model corpse carries what the live body carried (owner 2026-11-14: "Yes, fix them", on the corpse gaps found with
    // the helmet's loss at death; Core NativeCorpseDress). Presentation only: the cosmetic bodies' own physics scene, no rule, damage,
    // timing or networking.
    //  - The weapon in hand drops: the model the hand held (the bake's life-size model, the pack bank's, or the game's model in the
    //    pilot's skin, with its chip modules), read off the rig where it was drawn before the corpse is made (VisualHeldOf), drawn on the
    //    dropped weapon's cosmetic body, which is seated round that model (NativeCorpse.SeatWeapon: its middle, turn and size, loose of
    //    the body's own parts, flying with a hand cut off). The bake's models are the corpse's own copies (each model body carries every
    //    entry, hidden); the pack bank's are pooled mounts. A split body's is drawn by the half its hand went to (by the first half
    //    while that one draws the whole body); the daggers' second blade stays in the left hand. Kept off: Core NativeCorpseDress.Of.
    //  - The gear stays on: the chest core, the wrist device (lid shut) and the back emblem at the corpse model's own anchors (the live
    //    body's VisualGear rules: no core on the Hot Dog, no emblem under the robot armour), each on the half its bone went to, gone with
    //    a crushed part, their light failing (NativeCorpseDress.DeadGlow).
    internal sealed partial class NativeCorpse
    {
        /// <summary>The dropped weapon's cosmetic body (null: this body drops none).</summary>
        internal Rigidbody WeaponBody=>weapon;
        /// <summary>The dropped weapon's body seated round the model weapon the pilot held (ArenaPresentation.VisualCorpseCarry): at its
        /// middle `at`, turned `turn`, its box `size`; loose of every part of the body (it lies on the floor and never pushes the body
        /// apart); a weapon whose hand `hand` was cut off the body flies with that hand's piece.</summary>
        internal void SeatWeapon(Vector3 at,Quaternion turn,Vector3 size,int hand)
        {
            if(weapon==null)return;weapon.transform.SetPositionAndRotation(at,turn);weapon.position=at;weapon.rotation=turn;
            var box=weapon.GetComponent<BoxCollider>();
            if(box!=null)
            {
                box.center=Vector3.zero;box.size=size;
                foreach(var part in Parts.Values){var other=part!=null?part.GetComponent<Collider>():null;if(other!=null)Physics.IgnoreCollision(box,other);}
                var pair=Host??Twin;if(pair!=null)foreach(var part in pair.Parts.Values){var other=part!=null?part.GetComponent<Collider>():null;if(other!=null)Physics.IgnoreCollision(box,other);}
            }
            if(!weapon.isKinematic&&hand>=0&&hand<22&&Parts.TryGetValue(Anchor[hand],out var grip))
                for(int j=Anchor[hand];j>=0&&j!=CapturedRig.Pelvis;j=CapturedRig.Bones[j].Parent)if(Severed.Contains(j)){weapon.linearVelocity=grip.linearVelocity;weapon.angularVelocity=grip.angularVelocity;break;}
            Physics.SyncTransforms();
        }
    }
    public sealed partial class NativeSourceModels
    {
        /// <summary>The box, in the frame `into`, of model `id`'s parts each at `bone(its joint)` (those `skip` leaves out); false without any.</summary>
        public bool BoundsIn(string id,Matrix4x4 into,Func<int,Matrix4x4> bone,Func<int,bool> skip,out Bounds bounds)
        {
            bounds=default;if(id==null||!models.TryGetValue(id,out var model))return false;bool any=false;var to=into.inverse;
            foreach(var part in model)
            {
                if(part.Mesh==null||skip!=null&&skip(part.Bone))continue;var m=to*(bone!=null?bone(part.Bone):Matrix4x4.identity);var b=part.Mesh.bounds;
                for(int k=0;k<8;k++){var p=m.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1)));if(!any){bounds=new Bounds(p,Vector3.zero);any=true;}else bounds.Encapsulate(p);}
            }
            return any;
        }
    }
    public sealed partial class ArenaPresentation
    {
        // What a dying pilot's hand held, read off its rig as last drawn: the bake's entry, the pack bank's weapon or the game's model; the
        // main piece's frame (world), the weapon bones of both hands (the game's model and the modules ride them), whether the main piece
        // shows (a thrown dagger's does not), the daggers' second blade, and the main piece's box in its frame.
        private sealed class CorpseHeld{public string Spec,Entry,Model;public PackWeaponAsset Pack;public int Hand;public bool Main=true,Left;public Matrix4x4 Frame,BoneR,BoneL;public Bounds Box;public Actor Actor;public float At;}
        // A half's (or a whole corpse's) carried things: its gear's anchors on its model (the core, the device and its radius, the chest
        // bone and the emblem's frame on it), the parts they ride, the cut's side of their bones, whether it is the Hot Dog (no core) or wears the
        // robot armour (no emblem), and the dropped weapon when its body is this one's.
        private sealed class CorpseCarry{public Transform Core,Device,Chest;public float Radius;public Matrix4x4 Emblem=EmblemFrame;public int CorePart=-1,DevicePart=-1,CoreSide,DeviceSide;public Color Lens=Color.cyan;public bool Dog,Armour;public CorpseWeapon Weapon;}
        // A dropped weapon: what was held, its frames in the weapon body's (the main piece's, the hands' weapon bones), the model it is drawn
        // with (the instance's bake entry or a pooled pack mount, on `Host`), and the second blade's half, part and frame at death.
        private sealed class CorpseWeapon
        {
            public CorpseHeld Held;public Matrix4x4 Rel,RelR,RelL,Drop;public VisualCorpse Host;public Transform Mount;public GameObject Model;public VisualWeapon Pack;public Renderer[] Shown=Array.Empty<Renderer>();
            public NativeCorpse LeftOn;public int LeftPart=-1;public Func<int,Matrix4x4> Bone;public Func<int,bool> Visible;
        }
        private readonly Dictionary<NativeCorpse,CorpseCarry> corpseCarries=new Dictionary<NativeCorpse,CorpseCarry>();private readonly List<NativeCorpse> corpseCarriesGone=new List<NativeCorpse>();
        private static readonly Dictionary<string,Stack<VisualWeapon>> corpsePacks=new Dictionary<string,Stack<VisualWeapon>>(StringComparer.Ordinal);private static GameObject corpsePackHolder;
        // The bake's entries as the prefab carries them: the fit (its centre and scale) and the model's box in the entry's frame.
        private struct CorpseFit{public Vector3 Centre;public float Scale;public Bounds Box;}
        private static readonly Dictionary<string,CorpseFit?> corpseFits=new Dictionary<string,CorpseFit?>(StringComparer.Ordinal);
        private static bool CorpseEntry(string entry,out CorpseFit fit)
        {
            if(!corpseFits.TryGetValue(entry,out var known))
            {
                known=null;var e=visualBodyPrefab!=null?visualBodyPrefab.transform.Find("VisualWeapons")?.Find(entry):null;var model=e!=null?e.Find("Model"):null;
                if(model!=null)
                {
                    var f=new CorpseFit{Scale=1};var t=e.Find("Fit");if(t!=null&&t.localScale.x>.05f&&Mathf.Abs(t.localScale.x-1)>1e-4f){f.Centre=t.localPosition;f.Scale=t.localScale.x;}
                    bool any=false;var to=e.worldToLocalMatrix;
                    foreach(var mf in model.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if(mf.sharedMesh==null)continue;var m=to*mf.transform.localToWorldMatrix;var b=mf.sharedMesh.bounds;
                        for(int k=0;k<8;k++){var p=m.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1)));if(!any){f.Box=new Bounds(p,Vector3.zero);any=true;}else f.Box.Encapsulate(p);}
                    }
                    if(any)known=f;
                }
                corpseFits[entry]=known;
            }
            fit=known??default;return known.HasValue;
        }

        // PresentDeath, before the rig is moved onto the drawn body (VisualRigFromDrawn) and the corpse is made: what the hand held, where
        // it was drawn. A weapon the game hid at that moment (put away, sheathed, thrown, veiled, a stowed chip look) drops nothing.
        private CorpseHeld VisualHeldOf(CapturedAnimation.Player rig,NativeDeath death)
        {
            var a=death?.actor;if(rig?.Bones==null||rig.Root==null||a==null||sourceModels==null||!VisualDeathFor(death))return null;
            string spec=a.SourceSpec;float t=death.at;
            if(!NativeCorpseDress.Drops(spec)||!NativeWeaponDressing.SocketShown(spec)||a.WeaponAway||BladeHidden(a,t))return null;
            if(spec==NativeKatana.Spec&&KatanaSheathed(a,t)||GlaiveShown(a,t)<.5f)return null;
            float reach=WeaponReach(rig,a,t);
            var h=new CorpseHeld{Spec=spec,Hand=NativeCorpseDress.Hand(spec),Main=!(spec=="daggers"&&a.MainWeaponAway),Actor=a,At=t};
            h.BoneR=SourceWeaponBone(rig,a,t,false,CapturedRig.RHand,reach);h.BoneL=SourceWeaponBone(rig,a,t,false,CapturedRig.LHand,reach);
            if(WeaponSkin(a)==NativeWeaponSkins.StandardIndex)
            {
                // The bake's model (a gun's series where it carries one), as VisualWeaponDrawn mounts it: the fitted hand's frame unfitted.
                int series=NativeWeaponSkins.Series(SeatWeaponSkins(a.Id),spec);string entry=series>0?NativeWeaponSkins.SeriesEntry(spec,series):null;
                if(entry==null||!CorpseEntry(entry,out _))entry=spec;
                if(CorpseEntry(entry,out var fit))
                {
                    var frame=h.BoneR;if(fit.Scale!=1&&rig.HandFitted)frame=frame*Matrix4x4.Translate(fit.Centre)*Matrix4x4.Scale(Vector3.one/fit.Scale)*Matrix4x4.Translate(-fit.Centre);
                    h.Entry=entry;h.Frame=frame;h.Box=fit.Box;return h;
                }
                // The pack bank's (the bow at the bow hand, the daggers' second blade at the left's weapon bone).
                var pack=PackWeaponAssetOf(spec);
                if(pack?.Mesh!=null){h.Pack=pack;h.Frame=NativeBowWire.Is(spec)?rig.Bones[CapturedRig.LHand].localToWorldMatrix:h.BoneR;h.Left=pack.Fit.Dual&&NativeCorpseDress.OffHand(spec);h.Box=pack.Mesh.bounds;return h;}
            }
            // The game's model in the pilot's skin, its parts on the hands' weapon bones (a staff's every part on the staff's frame).
            h.Model=HeldWeaponModel(rig,a,false);if(!sourceModels.Has(h.Model))return null;
            h.Frame=h.BoneR;h.Left=NativeCorpseDress.OffHand(spec);var r=h.BoneR;var l=h.BoneL;bool left=h.Left;
            if(!sourceModels.BoundsIn(h.Model,h.Frame,b=>b==CapturedRig.LHand?l:r,b=>left&&b==CapturedRig.LHand,out h.Box))h.Box=new Bounds(new Vector3(0,0,.2f),new Vector3(.1f,.1f,.5f));
            return h;
        }

        // PresentDeath, once the corpse's model is made and fitted: each half's gear read off its model, and the dropped weapon's body seated
        // round the weapon the hand held (`held`; null: none).
        private void VisualCorpseCarry(NativeCorpse corpse,NativeDeath death,CorpseHeld held)
        {
            if(corpse==null||death==null||!visualCorpses.ContainsKey(corpse))return;
            CarryRead(corpse,death);if(corpse.Twin!=null)CarryRead(corpse.Twin,death);
            var on=corpse.WeaponBody!=null?corpse:corpse.Twin?.WeaponBody!=null?corpse.Twin:null;
            if(held==null||on==null||!visualCorpses.TryGetValue(on,out var c)||c.Root==null||!corpseCarries.TryGetValue(on,out var carry))return;
            var f=held.Frame;Vector3 x=f.GetColumn(0),y=f.GetColumn(1),z=f.GetColumn(2);if(!(x.sqrMagnitude>1e-10f&&y.sqrMagnitude>1e-10f&&z.sqrMagnitude>1e-10f))return;
            var turn=Quaternion.LookRotation(z,y);Vector3 at=f.MultiplyPoint3x4(held.Box.center),size=Vector3.Scale(held.Box.size,new Vector3(x.magnitude,y.magnitude,z.magnitude));
            on.SeatWeapon(at,turn,new Vector3(NativeCorpseDress.Box(size.x),NativeCorpseDress.Box(size.y),NativeCorpseDress.Box(size.z)),held.Hand);
            var seat=Matrix4x4.TRS(at,turn,Vector3.one).inverse;var w=new CorpseWeapon{Held=held,Rel=seat*held.Frame,RelR=seat*held.BoneR,RelL=seat*held.BoneL};
            if(held.Left)
            {
                // The second blade stays in its hand: the half its hand went to, riding that hand's part.
                var first=corpse;var left=first;
                if(first.Twin!=null&&first.Bisect is BisectFx fx&&fx.Request!=null&&fx.Request.HalfOf(CapturedRig.LHand,CutNum(first.Bind[CapturedRig.LHand].GetColumn(3)))==1)left=first.Twin;
                if(left.Parts.ContainsKey(left.Anchor[CapturedRig.LHand])){w.LeftOn=left;w.LeftPart=left.Anchor[CapturedRig.LHand];}
            }
            carry.Weapon=w;
        }
        // A half's gear anchors on its model, with the look's anchors (VisualAnchorsFor), the chest bone (the game chest joint's: the topmost
        // bone on it), the parts they ride and, on a split body, the side of the cut each one's bone is on.
        private void CarryRead(NativeCorpse half,NativeDeath death)
        {
            if(!visualCorpses.TryGetValue(half,out var c)||c.Root==null||c.Bones==null||c.Joint==null||c.Parent==null)return;var k=new CorpseCarry();corpseCarries[half]=k;
            foreach(var t in c.Root.GetComponentsInChildren<Transform>(true)){if(t.name=="VisualGearCore")k.Core=t;else if(t.name=="VisualGearDevice")k.Device=t;else if(t.name=="VisualGearDeviceRim")k.Radius=t.localPosition.magnitude;}
            var anchors=new VisualBody{Look=VisualLookOf(death.id),GearCore=k.Core,GearDevice=k.Device,GearRadius=k.Radius};VisualAnchorsFor(anchors);k.Radius=anchors.GearRadius;k.Emblem=anchors.Emblem;
            if(k.Device!=null&&!(k.Radius>.01f))k.Device=null;
            int chest=-1;for(int v=0;v<c.Bones.Length&&chest<0;v++)if(c.Bones[v]!=null&&c.Joint[v]==CapturedRig.Chest&&(c.Parent[v]<0||c.Joint[c.Parent[v]]!=CapturedRig.Chest))chest=v;
            k.Chest=chest>=0?c.Bones[chest]:null;k.CorePart=chest>=0?c.Part[chest]:-1;
            int device=k.Device!=null?CarryBone(c,k.Device):-1;k.DevicePart=device>=0?c.Part[device]:-1;
            if(ColorUtility.TryParseHtmlString(death.actor?.AccentColor??"#70e1d0",out var lens))k.Lens=lens.linear;
            k.Dog=HotDogOn(c.Root)||NativeHotDog.Worn(VisualOutfitOf(death.id));k.Armour=WearsArmour(death.id);
            var first=half.Host??half;
            if(first.Twin!=null&&first.Bisect is BisectFx fx&&fx.Request!=null)
            {
                k.CoreSide=fx.Request.HalfOf(CapturedRig.Chest,CutNum(k.Core!=null?k.Core.position:(Vector3)first.Bind[CapturedRig.Chest].GetColumn(3)))==1?1:0;
                if(k.Device!=null)k.DeviceSide=fx.Request.HalfOf(device>=0?c.Joint[device]:CapturedRig.RElbow,CutNum(k.Device.position))==1?1:0;
            }
        }
        // The index in the corpse's bones of `t` or its nearest ancestor among them (-1: none).
        private static int CarryBone(VisualCorpse c,Transform t){for(;t!=null;t=t.parent){int v=Array.IndexOf(c.Bones,t);if(v>=0)return v;}return -1;}

        // PresentDeath, each frame once the corpse is drawn: each half's gear and the dropped weapon, where its model shows this frame.
        private void VisualCorpseCarried(NativeCorpse corpse,NativeDeath death,float alpha,float clock)
        {
            if(corpse==null||death==null||sourceModels==null||ClassicBodies||!corpseCarries.ContainsKey(corpse))return;
            float glow=NativeCorpseDress.DeadGlow(clock-death.at,death.id);
            CarryShow(corpse,death,alpha,glow);if(corpse.Twin!=null)CarryShow(corpse.Twin,death,alpha,glow);
        }
        private void CarryShow(NativeCorpse half,NativeDeath death,float alpha,float glow)
        {
            if(!corpseCarries.TryGetValue(half,out var k)||!visualCorpses.TryGetValue(half,out var c)||c.Root==null)return;
            if(k.Weapon!=null)CarryWeapon(half,c,k.Weapon,death,alpha);
            if(c.Frame==Time.frameCount&&c.Root.activeSelf&&alpha>.001f)CarryGear(half,c,k,death,alpha,glow);
        }
        // The gear on a shown model: as the live body wears it (VisualGear), the device's lid shut, all at the dead glow, faded with the body;
        // under Frostbreak in the ice's colour and dark; none through Soul Drift's ghost.
        private void CarryGear(NativeCorpse half,VisualCorpse c,CorpseCarry k,NativeDeath death,float alpha,float glow)
        {
            if(c.Look==1||k.Core==null&&k.Device==null&&k.Chest==null||!sourceModels.PilotGear(k.Radius>.01f?k.Radius:.04f))return;
            var first=half.Host??half;bool split=first.Twin!=null;int mine=half.Host==null?0:1;var stage=split?VisualSplitStage(first):NativeSplitShow.Stage.On;
            bool frost=c.Look==2;Color tint=Color.white,lens=k.Lens;if(frost&&ColorUtility.TryParseHtmlString(c.LookHex,out var ice)){tint=ice.linear;lens=tint;glow=0;}
            var accent=AccentTint(frost?c.LookHex:death.actor?.AccentColor);gearBone??=GearBoneOf;corpseFade=alpha;var fade=alpha<1?corpseFadeOf??=_=>corpseFade:null;
            bool coreSide=!split||NativeSplitShow.Wears(mine,k.CoreSide,stage),chestKept=k.CorePart<0||!half.IsCrushed(k.CorePart);
            // (No core on the Hot Dog, as alive; no emblem under the robot armour's back plate.)
            if(k.Core!=null&&coreSide&&chestKept&&!k.Dog)
            {gearMatrix=k.Core.localToWorldMatrix;sourceModels.Queue(NativeSourceModels.GearCore,gearMatrix,gearBone,null,0,null,tint,accent,1,fade,null,new Vector4(glow,glow,glow,1),null,1);}
            if(k.Chest!=null&&coreSide&&chestKept&&!k.Armour)
            {gearMatrix=k.Chest.localToWorldMatrix*k.Emblem;sourceModels.Queue(NativeSourceModels.GearEmblem,gearMatrix,gearBone,null,0,null,tint,accent,1,fade,null,new Vector4(glow,glow,glow,1),null,1);}
            if(k.Device==null||split&&!NativeSplitShow.Wears(mine,k.DeviceSide,stage)||k.DevicePart>=0&&half.IsCrushed(k.DevicePart))return;
            var dev=NativeCorpseDress.Device(glow);var m=k.Device.localToWorldMatrix;float r=Mathf.Round(Mathf.Clamp(k.Radius,.02f,.08f)*1000)/1000;
            gearMatrix=m;sourceModels.Queue(sourceModels.GearDeviceFor(k.Radius),m,gearBone,null,0,null,tint,accent,1,fade,null,new Vector4(dev.Strips,dev.Strips,dev.Strips,1),null,1);
            var h=NativePilotGear.Hinge(r);var shine=lens*dev.Lens;
            gearMatrix=m*Matrix4x4.TRS(new Vector3(-h.X,h.Y,h.Z),Quaternion.AngleAxis(dev.Lid*NativePilotGear.LidOpenDegrees,Vector3.forward),Vector3.one);
            sourceModels.Queue(NativeSourceModels.GearLid,gearMatrix,gearBone,null,0,null,tint,accent,1,fade,null,new Vector4(shine.r,shine.g,shine.b,1),null,1);
        }
        // The dropped weapon at its body's presented frame, on the instance that draws it: the first half's while that one draws the whole
        // body (the cut being made, or failed), else its own half's; hidden with it.
        private void CarryWeapon(NativeCorpse half,VisualCorpse c,CorpseWeapon w,NativeDeath death,float alpha)
        {
            var held=w.Held;var drop=half.PresentedWeapon;var first=half.Host??half;var host=c;
            if(half.Host!=null&&VisualCorpseWhole(first)&&visualCorpses.TryGetValue(first,out var whole)&&whole.Root!=null)host=whole;
            bool shown=drop.HasValue&&host.Root!=null&&host.Frame==Time.frameCount&&host.Root.activeSelf&&alpha>.001f;
            if(drop.HasValue)w.Drop=drop.Value;
            if(held.Model!=null){if(shown&&host.Look!=1)CarrySource(w,host,death,alpha);if(shown&&host.Look==0)CarryModules(w,death);return;}
            if(w.Host!=host)CarryMount(w,host);
            if(w.Model==null||w.Mount==null)return;
            bool main=shown&&held.Main&&VisualWeaponMount(w.Mount,w.Drop*w.Rel);
            if(w.Model.activeSelf!=main)w.Model.SetActive(main);
            foreach(var r in w.Shown)if(r!=null&&main&&!r.enabled)r.enabled=true;
            var pack=w.Pack;
            if(pack?.LeftMount!=null&&pack.LeftModel!=null)
            {
                bool left=shown&&w.LeftOn!=null&&!w.LeftOn.IsCrushed(w.LeftPart)&&CarryShown(w.LeftOn)&&VisualWeaponMount(pack.LeftMount,w.LeftOn.PartFrom(w.LeftPart)*held.BoneL);
                if(pack.LeftModel.activeSelf!=left)pack.LeftModel.SetActive(left);
            }
            if(shown&&host.Look==0)CarryModules(w,death);
        }
        // Whether a half's model (or the first half's whole body standing in for it) shows this frame.
        private bool CarryShown(NativeCorpse half)
        {
            var first=half.Host??half;var on=half.Host!=null&&VisualCorpseWhole(first)?first:half;
            return visualCorpses.TryGetValue(on,out var c)&&c.Root!=null&&c.Frame==Time.frameCount&&c.Root.activeSelf;
        }
        // The bake's entry on `host` (every model body carries them, hidden), or a pooled pack mount moved under it; its renderers join the
        // body's (its dissolve and kill looks reach them), cast no shadow (as the corpse) and show whatever a cut turned off.
        private void CarryMount(CorpseWeapon w,VisualCorpse host)
        {
            if(w.Model!=null)w.Model.SetActive(false);
            var held=w.Held;w.Host=host;w.Mount=null;w.Model=null;
            if(held.Entry!=null)
            {
                var entry=host.Root.transform.Find("VisualWeapons")?.Find(held.Entry);var model=entry!=null?entry.Find("Model"):null;if(model==null)return;
                w.Mount=entry;w.Model=model.gameObject;
            }
            else if(held.Pack!=null)
            {
                if(w.Pack==null)
                {
                    var key=held.Pack.Fit.Spec;if(corpsePacks.TryGetValue(key,out var pool))while(pool.Count>0&&w.Pack==null){var p=pool.Pop();if(p?.Mount!=null)w.Pack=p;}
                    w.Pack??=PackWeaponMake(host.Root,held.Pack);
                }
                foreach(var t in new[]{w.Pack.Mount,w.Pack.LeftMount})if(t!=null){t.SetParent(host.Root.transform,false);t.gameObject.layer=host.Root.layer;}
                w.Mount=w.Pack.Mount;w.Model=w.Pack.Model;
            }
            if(w.Model==null)return;
            var mine=new List<Renderer>();foreach(var t in new[]{w.Mount,w.Pack?.LeftMount})if(t!=null)mine.AddRange(t.GetComponentsInChildren<Renderer>(true));
            var added=new List<Renderer>();foreach(var r in mine){r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.enabled=true;if(host.All==null||Array.IndexOf(host.All,r)<0)added.Add(r);}
            if(added.Count>0){var all=new Renderer[(host.All?.Length??0)+added.Count];host.All?.CopyTo(all,0);added.CopyTo(all,host.All?.Length??0);host.All=all;host.Dissolve=-1;}
            w.Shown=mine.ToArray();
        }
        // The game's model of the weapon: the main hand's parts on the dropped weapon, the daggers' second blade on its hand; Frostbreak
        // ices it.
        private void CarrySource(CorpseWeapon w,VisualCorpse host,NativeDeath death,float alpha)
        {
            w.Bone??=b=>b==CapturedRig.LHand&&w.LeftOn!=null?w.LeftOn.PartFrom(w.LeftPart)*w.Held.BoneL:w.Drop*(b==CapturedRig.LHand?w.RelL:w.RelR);
            w.Visible??=b=>b==CapturedRig.LHand&&w.Held.Left?w.LeftOn!=null&&!w.LeftOn.IsCrushed(w.LeftPart)&&CarryShown(w.LeftOn):b!=CapturedRig.RHand||w.Held.Main;
            if(host.Look==2&&ColorUtility.TryParseHtmlString(host.LookHex,out var ice)){sourceModels.QueueRecolor(w.Held.Model,w.Drop*w.RelR,w.Bone,w.Visible,ice.linear,Color.black,alpha);return;}
            sourceModels.Queue(w.Held.Model,w.Drop*w.RelR,w.Bone,w.Visible,0,null,accent:AccentTint(death.actor?.AccentColor),opacity:alpha,glow:HeldGlow);
        }
        // The chip modules the weapon carried (the Standard's designed modules, the Prototype's pieces), as the live weapon wore them at
        // death (their animation stopped there); the burn and poison enchant, the blade's blood and the muzzle went with the pilot.
        private void CarryModules(CorpseWeapon w,NativeDeath death)
        {
            var held=w.Held;var a=held.Actor;var pilot=LoadoutPilot(a.Id);if(pilot?.Stats==null||pilot.Weapon==null||pilot.Seat?.chips==null)return;
            string spec=held.Spec;var chips=pilot.Seat.chips;var stats=pilot.Stats;bool ranged=pilot.Weapon.@class=="ranged";int color=(int)pilot.Weapon.color;
            int hands=held.Left&&w.LeftOn!=null&&CarryShown(w.LeftOn)&&!w.LeftOn.IsCrushed(w.LeftPart)?2:1;
            Matrix4x4 Parent(int hand)=>hand==0?w.Drop*w.RelR:w.LeftOn.PartFrom(w.LeftPart)*held.BoneL;
            if(StandardChips(a))
            {
                var eye=WorldEye;float distance=eye!=null?Vector3.Distance(eye.transform.position,w.Drop.GetColumn(3)):0;int tier=NativeWeaponSkins.Tier(LowGraphics,WeaponEffectsQuality,distance,false);
                StandardModulesAt(spec,pilot,Parent(0),hands>1?Parent(1):(Matrix4x4?)null,held.Main,tier,held.At);
                return;
            }
            if(spec==NativeBombs.Spec||spec==NativeTopDog.Spec||spec==NativeGlaive.Spec||NativeBlades.Is(spec))return;
            int pieces=NativeWeaponDressing.Modules(spec,ranged,color,chips,stats,weaponPieces);
            for(int hand=0;hand<hands;hand++){if(hand==0&&!held.Main)continue;var parent=Parent(hand);for(int i=0;i<pieces;i++){var p=weaponPieces[i];NativeWeaponDressing.Animate(ref p,held.At);QueuePiece(p,SkinPiece(a,p,parent),0,null);}}
        }
        // ClearCorpses: every corpse's carried things dropped, the pack mounts back in the pool (before their bodies are taken apart).
        private void CorpseCarriedClear(){foreach(var k in corpseCarries.Values)if(k.Weapon?.Pack!=null)CarryPool(k.Weapon);corpseCarries.Clear();}
        // After the deaths are presented (VisualCorpsesShown): a gone corpse's carried things dropped, its pack mounts back in the pool.
        private void CorpseCarriedShown()
        {
            if(corpseCarries.Count==0)return;corpseCarriesGone.Clear();
            foreach(var pair in corpseCarries)
            {
                bool kept=galleryCorpse!=null&&(galleryCorpse==pair.Key||galleryCorpse.Twin==pair.Key);foreach(var c in corpses.Values)if(c==pair.Key||c.Twin==pair.Key){kept=true;break;}
                if(!kept||!visualCorpses.ContainsKey(pair.Key))corpseCarriesGone.Add(pair.Key);
            }
            foreach(var gone in corpseCarriesGone){var w=corpseCarries[gone].Weapon;if(w?.Pack!=null)CarryPool(w);corpseCarries.Remove(gone);}
        }
        private static void CarryPool(CorpseWeapon w)
        {
            var pack=w.Pack;w.Pack=null;if(pack?.Mount==null)return;
            if(corpsePackHolder==null){corpsePackHolder=new GameObject("Corpse weapon pool");corpsePackHolder.SetActive(false);DontDestroyOnLoad(corpsePackHolder);}
            pack.Hide();foreach(var t in new[]{pack.Mount,pack.LeftMount})if(t!=null)t.SetParent(corpsePackHolder.transform,false);
            var key=w.Held.Pack.Fit.Spec;if(!corpsePacks.TryGetValue(key,out var pool))corpsePacks[key]=pool=new Stack<VisualWeapon>();if(pool.Count<8)pool.Push(pack);else foreach(var t in new[]{pack.Mount,pack.LeftMount})if(t!=null)Destroy(t.gameObject);
        }
    }
}
