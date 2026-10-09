using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private int draftHover=-1,draftViewSeat,draftCardCount;private bool draftViewing;
        // Per offer index, from the table this frame (virtual canvas): the hit area (the chip and half the gap around it), the
        // chip's own footprint, and its top-left corner for the PvE key badge; draftOrder is the table's reading order.
        private readonly Rect[] draftCards=new Rect[NativeDraftTable.MaxChips],draftCores=new Rect[NativeDraftTable.MaxChips];private readonly Vector2[] draftBadges=new Vector2[NativeDraftTable.MaxChips];private readonly int[] draftOrder=new int[NativeDraftTable.MaxChips];
        private readonly List<ContentChip> draftChips=new List<ContentChip>();private readonly string[] draftIds=new string[NativeDraftTable.MaxChips];private int draftIdCount=-1;private bool draftIdsPve;
        private readonly Dictionary<Renderer,bool> pickVisibility=new Dictionary<Renderer,bool>();
        // The Siren personal pick replaces the arena view for the local player only (pve/chip-screen.ts): the fight keeps
        // simulating, so the live renderers are hidden instead of deactivating the arena.
        private void HideLive(){foreach(var r in arena.Root.GetComponentsInChildren<Renderer>()){if(!pickVisibility.ContainsKey(r))pickVisibility[r]=r.enabled;r.enabled=false;}}
        private void RestoreLive(){foreach(var pair in pickVisibility)if(pair.Key!=null)pair.Key.enabled=pair.Value;pickVisibility.Clear();}
        private bool PvePickScreen=>!menu&&arena!=null&&(PveFollowing||PvePickOpen)&&arena.Pve?.Holding!=true&&arena.Local.Hp>0&&!ReplayActive&&match.Phase==NativePhase.Fight;
        // The offers as chips (the PvP draft's or the open personal pick's), rebuilt only when the offered ids change, since
        // replicas receive a fresh list with every frame. Unknown PvE ids are skipped, as the pick screen always did.
        private List<ContentChip> DraftChips(bool pve,NativeSeat seat)
        {
            IList<string> ids=null;if(!pve)ids=seat.offers;else if(arena?.Pve!=null&&arena.Pve.Rows.TryGetValue(seat.id,out var row))ids=row.Open?.Offers;
            int n=ids==null?0:Math.Min(ids.Count,NativeDraftTable.MaxChips);bool same=n==draftIdCount&&pve==draftIdsPve;for(int i=0;same&&i<n;i++)same=string.Equals(ids[i],draftIds[i]);
            if(!same){draftChips.Clear();for(int i=0;i<n;i++){draftIds[i]=ids[i];var c=pve?arena.PveChip(ids[i]):content.Chip(ids[i]);if(c!=null)draftChips.Add(c);}draftIdCount=n;draftIdsPve=pve;}
            return draftChips;
        }
        private void PveOpenClock(NativePvePick pick){if(pick!=null&&pveOpenId!=pick.Id){pveOpenId=pick.Id;pveOpenAt=Time.unscaledTime;draftFocus=-1;}}
        // The table stage (NativeDraftTable): the drafter seated at the table with the offers laid out, rendered from the high
        // tilted camera before the overlay draws. Both the PvP draft (every viewer, from the replicated draft state) and the
        // Siren personal pick use it; the overlay (NativeDraftScreen) reads back the chips' screen areas for hover and clicks.
        private bool DraftFrame()
        {
            bool pve=PvePickScreen,follow=pve&&PveFollowing,active=!menu&&arena!=null&&match.Phase==NativePhase.Draft;
            // The Siren pick's follow-through played on its own snapshot; the next queued pick starts from a fresh table.
            if(draftFollowed&&!follow)presentation.ResetDraftTable();draftFollowed=follow;
            if(!active&&!pve){if(draftViewing){draftViewing=false;RestoreLive();presentation.ResetPresentation();}draftCardCount=0;return false;}
            var seat=pve?arena.Local.Seat:match.Seat(match.DraftSeat);if(seat==null)return false;
            if(!draftViewing||draftViewSeat!=seat.id){RestoreLive();presentation.ResetPresentation();presentation.ResetDraftTable();draftHover=-1;draftFocus=-1;draftViewSeat=seat.id;draftViewing=true;}
            NativePvePick open=null;if(pve&&!follow&&arena.Pve.Rows.TryGetValue(seat.id,out var row))open=row.Open;if(pve&&!follow)PveOpenClock(open);
            var chips=follow?pveFollowChips:DraftChips(pve,seat);int count=Math.Min(chips.Count,NativeDraftTable.MaxChips),chosen=-1;float age=-1;
            if(!pve&&match.Chosen!=null){for(int i=0;i<count;i++)if(chips[i].id==match.Chosen){chosen=i;break;}if(chosen>=0)age=(float)(match.Time-match.ChosenAt);}
            if(follow&&pveFollowIndex<count){chosen=pveFollowIndex;age=PveFollowAge;}
            float elapsed=pve?Time.unscaledTime-(follow?pveFollowOpened:pveOpenAt):(float)(match.Time-DraftStarted(seat));bool own=pve||!seat.bot&&seat.id==arena.Local.Seat.id;
            // Idle gaze: the boot scan in reading order, then a 1.8 s cycle (watchers see the drafter look over the table).
            int scan=count==0?0:elapsed<count*.22f?Mathf.FloorToInt(Math.Max(0,elapsed)/.22f):(int)(Math.Max(0,elapsed)/1.8f)%count;
            if(pve)HideLive();else arena.Root.SetActive(false);viewModelCamera.enabled=false;
            // Stage lights take the picker's colour, blending to the chip's rarity over .5 s after the pick (the Siren pick: its tier).
            string tier=follow?pveFollowTier:open?.Tier;Color tint=pve?(tier=="rainbow"?Color.white:RarityColor(tier??"common")):SeatColor(seat);if(age>=0)tint=Color.Lerp(tint,RarityColor(chips[chosen].rarity),Mathf.Clamp01(age/.5f));
            presentation.DraftBandTeam=seat.spec=="siren"?-1:NativeStatusVisuals.PilotBand(match.Teams,match.Cooperative,seat.AuthorityBoss,seat.team,seat.id==arena.LocalId);
            // The table frames itself left of the side card column, under the heading and above the nameplate.
            var ui=NativeUiMotion.Measure(Screen.width,Screen.height,prefs.uiScale);float right=Mathf.Clamp((DraftCardLeft(ui.Width,ui.Height)-24)/ui.Width,.45f,.9f);
            draftCardCount=presentation.RenderDraftTable(seat,chips,count,pve?arena.Time:(float)match.Time,elapsed,draftHover,own&&age<0?draftHover:-1,scan,chosen,age,tint,cameraView,prefs.reducedEffects,new Rect(.03f,.1f,right-.03f,.73f),ui.Scale,draftCards,draftCores,draftBadges,draftOrder);
            return true;
        }
    }
    public sealed partial class ArenaPresentation
    {
        private CapturedAnimation.Player draftRig;private Func<int,Matrix4x4> tableBone;private readonly DraftTableMotion tableMotion=new DraftTableMotion();
        private readonly MeleeRig tableRestRig=new MeleeRig();private readonly CapturedRig.Pose tableRestPose=new CapturedRig.Pose();
        private readonly NVector[] tableSlots=new NVector[NativeDraftTable.MaxChips],tablePoints=new NVector[NativeDraftTable.MaxChips*4+8];private readonly int[] tableOrder=new int[NativeDraftTable.MaxChips];private readonly string[] tableTiers=new string[NativeDraftTable.MaxChips];
        private TableView tableView;private int tableViewKey;private bool tableViewSet;private int tableSeat=-1;private string tableHat,tableLoose,tableHatSource,tableSpecSource,tableBodySource,tableAccentSource;private Color tableBody,tableAccent;private Matrix4x4 tableWeapon;private bool tableWeaponShown;
        public void ResetDraftTable(){tableMotion.Reset();tableViewSet=false;tableSeat=-1;}
        private static Color Lin(Color c)=>QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;
        private static Color Lin(int hex)=>Lin(NativeArena.ColorOf(hex));
        private void TableBox(Matrix4x4 parent,float x,float y,float z,float sx,float sy,float sz,Color tint,bool glow){sourceModels.QueuePiece(WeaponShape.Cube,glow,parent*Matrix4x4.TRS(new Vector3(x,y,z),Quaternion.identity,new Vector3(sx,sy,sz)),tint);}
        // Draws the table stage and returns the number of chips laid out; fills the overlay's hit areas (virtual canvas).
        // `window` is the viewport area (0..1, y up) the frame must fit; `hover` the lifted chip, `reach` the chip the seated
        // drafter reaches toward (the local drafter only), `chosen`/`age` the authoritative pick.
        public int RenderDraftTable(NativeSeat seat,List<ContentChip> chips,int count,float time,float elapsed,int hover,int reach,int scan,int chosen,float age,Color tint,Camera camera,bool reduced,Rect window,float uiScale,Rect[] hits,Rect[] cores,Vector2[] badges,int[] order)
        {
            bool siren=seat.spec=="siren";
            if(draftRig==null){draftRig=captured.Create(transform,-101);tableBone=b=>draftRig.Bones[b].localToWorldMatrix;}
            sourceModels.Begin();foreach(var b in batches)b.Clear();FirstPerson=false;combatGlow?.Clear();particles.Clear();
            count=Mathf.Min(count,NativeDraftTable.MaxChips);for(int i=0;i<count;i++)tableTiers[i]=chips[i].rarity??"common";
            int rows=NativeDraftTable.Arrange(tableTiers,count,tableOrder,tableSlots);Array.Copy(tableOrder,order,count);
            if(tableSeat!=seat.id||!string.Equals(tableHatSource,seat.hat)||!string.Equals(tableSpecSource,seat.spec)||!string.Equals(tableBodySource,seat.bodyColor)||!string.Equals(tableAccentSource,seat.accentColor))
            {
                // Per drafter (not per frame): model ids, tints and where the put-aside weapon lies on the floor.
                tableSeat=seat.id;tableHatSource=seat.hat;tableSpecSource=seat.spec;tableBodySource=seat.bodyColor;tableAccentSource=seat.accentColor;tableHat="hat-"+seat.hat;tableLoose="loose-"+seat.spec;tableBody=BodyTint(seat.bodyColor);tableAccent=AccentTint(seat.accentColor);
                var bounds=default(Bounds);tableWeaponShown=!siren&&sourceModels.ModelBounds(tableLoose,out bounds);
                if(tableWeaponShown)
                {
                    var rotation=Quaternion.Euler(0,14,0)*Quaternion.Euler(0,0,90);float low=float.MaxValue;var c=bounds.center;var e=bounds.extents;
                    for(int k=0;k<8;k++){var corner=c+new Vector3((k&1)==0?-e.x:e.x,(k&2)==0?-e.y:e.y,(k&4)==0?-e.z:e.z);low=Mathf.Min(low,(rotation*corner).y);}
                    var centre=rotation*c;tableWeapon=Matrix4x4.TRS(new Vector3(1.02f-centre.x,.001f-low,.3f-centre.z),rotation,Vector3.one);
                }
            }
            // The pose: seated (or the Siren standing in her idle), the look, lean, reach and pick, then onto the captured rig.
            float idle=reduced?0:time;if(siren){tableMotion.Pose.Reset();NativeSourceMotion.Sample("siren-idle-melee",idle,0,tableMotion.Pose);}
            tableMotion.Step(tableSlots,tableOrder,count,idle,elapsed,hover,reach,scan,chosen,age,Time.unscaledDeltaTime,siren);
            var pose=tableMotion.Pose;draftRig.Root.transform.SetPositionAndRotation(new Vector3(0,NativeDraftTable.RootY,0),Quaternion.identity);
            for(int i=0;i<CapturedRig.Bones.Length;i++){draftRig.Bones[i].localPosition=CapturedAnimation.Vec(CapturedRig.Mirror(pose.Position[i]));draftRig.Bones[i].localRotation=CapturedAnimation.Quat(CapturedRig.Mirror(pose.Rotation[i]));}
            // The camera: 65 degrees down, framed once per layout and screen so every chip and the pilot's upper body fit.
            int key=count*7919^rows*131^Screen.width*31^Screen.height*17^(siren?977:0)^(seat.hat??"").GetHashCode()^window.GetHashCode();for(int i=0;i<count;i++)key=key*31+(int)(tableSlots[i].X*1000)+(int)(tableSlots[i].Z*1000)*7;
            if(!tableViewSet||key!=tableViewKey)
            {
                tableViewSet=true;tableViewKey=key;var head=siren?pose.WorldPosition[CapturedRig.Head]:RestHead();
                var top=NativeDraftTable.ToWorld(head)+new NVector(0,(siren?.5f:.36f)+(seat.hat!="none"&&!siren?.16f:0),0);
                int n=NativeDraftTable.FramePoints(tableSlots,count,top,.52f,tablePoints);tableView=NativeDraftTable.Frame(tablePoints,n,camera.aspect,window.xMin,window.xMax,window.yMin,window.yMax);
            }
            camera.fieldOfView=tableView.Fov;camera.transform.SetPositionAndRotation(CapturedAnimation.Vec(tableView.Position),Quaternion.LookRotation(CapturedAnimation.Vec(tableView.Forward),Vector3.up));
            float pulse=tableMotion.PulseK,flash=pulse>=0?(1-pulse)*(1-pulse):0;NativeLighting.Draft(camera,tint,flash);
            // The table, floor and chair; an empty socket on the mat under every offer.
            foreach(var p in NativeDraftTable.Stage)TableBox(Matrix4x4.identity,p.Position.X,p.Position.Y,p.Position.Z,p.Size.X,p.Size.Y,p.Size.Z,Lin(p.Color),false);
            if(!siren)foreach(var p in NativeDraftTable.ChairPieces)TableBox(Matrix4x4.identity,p.Position.X,p.Position.Y,p.Position.Z,p.Size.X,p.Size.Y,p.Size.Z,Lin(p.Color),false);
            var socket=Lin(NativeDraftTable.SocketColor);for(int i=0;i<count;i++)TableBox(Matrix4x4.identity,tableSlots[i].X,NativeDraftTable.TableTop+.0045f,tableSlots[i].Z,.1f,.003f,.1f,socket,false);
            for(int i=0;i<count;i++)if(tableMotion.Visible[i])Chip(i,time);
            if(pulse>=0)Pulse(pulse,chosen>=0&&chosen<count?tableTiers[chosen]:"common");
            // The drafter: their model, colours and hat; the weapon put aside on the floor by the chair.
            var root=draftRig.Root.transform.localToWorldMatrix;sourceModels.Queue(siren?"siren":"pilot",root,tableBone,body:tableBody,accent:tableAccent);
            if(!siren&&seat.hat!="none")sourceModels.Queue(tableHat,root,tableBone);if(tableWeaponShown)sourceModels.Queue(tableLoose,tableWeapon);
            TeamBands(draftRig,DraftBandTeam);DrawBatches(null,0,false);sourceModels.Flush(UsesInstancing);
            // Screen areas for the overlay: each chip's footprint at rest, and that footprint grown by half the gap.
            float sw=Screen.width/uiScale,sh=Screen.height/uiScale,h=NativeDraftTable.Chip*.5f+NativeDraftTable.Pin;
            for(int i=0;i<count;i++)
            {
                var s=tableSlots[i];s.Y=NativeDraftTable.TableTop+NativeDraftTable.ChipThick;
                hits[i]=Area(s,NativeDraftTable.PitchX*.5f,NativeDraftTable.PitchZ*.5f,sw,sh);cores[i]=Area(s,h,h,sw,sh);
                var corner=NativeDraftTable.Project(tableView,s+new NVector(h,0,-h),out _);badges[i]=new Vector2(corner.X*sw,(1-corner.Y)*sh);
            }
            return count;
        }
        private Rect Area(NVector centre,float hx,float hz,float sw,float sh)
        {
            float x0=float.MaxValue,y0=float.MaxValue,x1=float.MinValue,y1=float.MinValue;
            for(int k=0;k<4;k++){var v=NativeDraftTable.Project(tableView,centre+new NVector((k&1)==0?-hx:hx,0,(k&2)==0?-hz:hz),out _);float x=v.X*sw,y=(1-v.Y)*sh;x0=Mathf.Min(x0,x);x1=Mathf.Max(x1,x);y0=Mathf.Min(y0,y);y1=Mathf.Max(y1,y);}
            return Rect.MinMaxRect(x0,y0,x1,y1);
        }
        // The seated head position at rest (framing should not follow the look).
        private NVector RestHead(){NativeDraftTable.Seat(tableRestRig,tableRestPose,0,out _,out _);return tableRestPose.WorldPosition[CapturedRig.Head];}
        // One chip as low-poly voxels (client/chip-card.ts's processor package at real scale): substrate with the rarity edge,
        // heat spreader (gold for Legendary, foil for Mythic), the lit die, grade pips (a ring for the pilot exclusives) and
        // two rows of legs. Lifted chips light their edge and die.
        private void Chip(int i,float time)
        {
            var m=tableMotion;string tier=tableTiers[i];Color rare=NativeChipCard.Rarity(tier),rareLin=Lin(rare);float lift=NativeDraftTable.Ease(m.Lift[i]),t=NativeDraftTable.ChipThick;
            var root=Matrix4x4.TRS(CapturedAnimation.Vec(m.Position[i]),CapturedAnimation.Quat(m.Rotation[i]),Vector3.one*m.Scale[i]);bool legendary=tier=="legendary",mythic=tier=="mythic";
            float c=NativeDraftTable.Chip,p=NativeDraftTable.Pin;
            TableBox(root,0,0,0,c,t,c,Lin(0x0d0e12),false);
            // The rim: lit in the card's edge colour at rest; while lifted it becomes emissive and brightens to the rarity colour
            // (the same box, so the two never z-fight).
            var edge=Lin(NativeUi.Mix(NativeUi.Hex(0x34363f),rare,.52f));
            if(lift>.02f)TableBox(root,0,-.001f,0,c+.006f,t*.6f,c+.006f,Color.Lerp(edge*.55f,rareLin*1.3f,lift),true);else TableBox(root,0,-.001f,0,c+.006f,t*.6f,c+.006f,edge,false);
            var plate=mythic?Lin(Color.HSVToRGB(Mathf.Repeat(time/7,1),.55f,.42f)):Lin(legendary?0x4d3b17:0x2b2d35);TableBox(root,0,t*.5f+.0025f,0,.06f,.005f,.06f,plate,false);
            TableBox(root,0,t*.5f+.0055f,0,.026f,.003f,.026f,rareLin*Mathf.Min(1.7f,m.Bright[i]),true);
            int rank=NativeDraftTable.Rank(tier);
            if(rank>=7)sourceModels.QueuePiece(WeaponShape.Ring,true,root*Matrix4x4.TRS(new Vector3(0,t*.5f+.006f,0),Quaternion.Euler(90,0,0),Vector3.one*.021f),rareLin*1.2f);
            else{var off=Lin(0x3a3d46);for(int k=0;k<6;k++)TableBox(root,(k-2.5f)*.009f,t*.5f+.006f,.024f,.005f,.002f,.005f,k<rank?rareLin:off,k<rank);}
            var pin=Lin(legendary?0xffe08a:0xc9ccd4);
            for(int k=0;k<5;k++){float x=(k-2)*.015f;TableBox(root,x,-t*.25f,c*.5f+p*.5f,.006f,.0035f,p,pin,false);TableBox(root,x,-t*.25f,-c*.5f-p*.5f,.006f,.0035f,p,pin,false);}
        }
        // The plant pulse: two rarity rings expanding around the receiving forearm, and the chip's last spark at the wrist.
        private void Pulse(float k,string tier)
        {
            var m=tableMotion;var rare=Lin(NativeChipCard.Rarity(tier));var at=CapturedAnimation.Vec(m.PulseAt);var axis=Quaternion.FromToRotation(Vector3.forward,CapturedAnimation.Vec(m.PulseAxis));
            for(int ring=0;ring<2;ring++){float u=Mathf.Clamp01(k*1.35f-ring*.3f),fade=(1-u)*(1-u);if(u<=0||fade<.03f)continue;float r=.06f+.16f*NativeDraftTable.Ease(u);sourceModels.QueuePiece(WeaponShape.Ring,true,Matrix4x4.TRS(at,axis,new Vector3(r,r,r*.6f)),rare*(1.6f*fade));}
            float s=.03f*(1-k);if(s>.002f)sourceModels.QueuePiece(WeaponShape.Cube,true,Matrix4x4.TRS(at,Quaternion.identity,Vector3.one*s),rare*2);
        }
    }
    public sealed partial class NativeSourceModels
    {
        // Mesh-space bounds of a model (all parts), to lay a prop on a surface. False when the model is unknown.
        public bool ModelBounds(string id,out Bounds bounds)
        {
            bounds=default;if(id==null||!models.TryGetValue(id,out var parts))return false;bool any=false;
            foreach(var p in parts){if(p.Mesh==null)continue;if(!any){bounds=p.Mesh.bounds;any=true;}else bounds.Encapsulate(p.Mesh.bounds);}
            return any;
        }
    }
}
