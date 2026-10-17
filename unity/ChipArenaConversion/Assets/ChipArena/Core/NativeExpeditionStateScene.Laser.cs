using System;

namespace ChipArena.UnityConversion
{
    // The instance exit's laser door, drawn by the state pieces (Claude, 2026-10-17; NativeExpeditionLaserDoor has the timing and
    // docs/EXPEDITION-PRESENTATION.md "The laser door" the look). The map's own exit portal stands in each live instance (metal
    // jambs either side of the 3.2 m opening and a lintel over it, the closure volume between them); the door's emitters are
    // bolted to the front of that portal and nothing solid stands inside the closure face:
    //  - two emitter columns on the jambs' front (a lens per beam on their inner faces, a charge rail and a status lamp on their
    //    fronts, vent fins outboard), the emitter header over the lintel with its display (padlock, chevrons and LOCKING / SEALED /
    //    UNLOCKING / OPEN), the floor emitter sill and the dark recess behind;
    //  - the beams across the opening in the zone's colour (a white-hot core in glow layers), humming; the light they throw on
    //    the floor, the jambs, the emitters, the lintel and the recess, the haze they light and the motes that glint crossing them;
    //  - the field sheet: exactly the closure face at the seal depth (the airlock's seal plane) for as long as the closure blocks.
    // Low graphics: five beams, no halo, haze, motes, sparks, fins or wash; reduced effects: no flicker, stutter, flash or sparks.
    public sealed partial class NativeExpeditionStateScene
    {
        /// <summary>The laser door drawn this frame (an instance has one exit): its stage, how many beams are lit, whether its
        /// field sheet stands, when its lock and unlock started (presentation clock; NaN for none; the HUD notice and the sounds
        /// read these), its zone and the middle of its doorway.</summary>
        public bool LaserDrawn,LaserSheet;public ExpeditionLaserStage LaserStage;public int LaserBeamsLit;
        public double LaserLockStart=double.NaN,LaserUnlockStart=double.NaN;public string LaserArea;public float LaserX,LaserY,LaserZ;
        /// <summary>A stand-in closure for the DEV preview (presentation only): drawn in place of the record with its id.</summary>
        public ExpeditionClosureView DoorPreview;
        private readonly float[] laserLevel=new float[16];
        // Frame-relative layout (metres): the emitters stand on the portal's front, from D0 to D1 toward the party; the beams at DB.
        private const float Col=.46f,D0=.2f,D1=.46f,DB=.33f;

        private void LaserClear(){LaserDrawn=LaserSheet=false;LaserBeamsLit=0;LaserLockStart=LaserUnlockStart=double.NaN;LaserArea=null;}
        /// <summary>Records the laser door's stage for the HUD and the sounds (whether or not it is near enough to draw).</summary>
        private void LaserState(ExpeditionDoorRig door,ExpeditionClosureView c,double t,ExpeditionStateCues cues)
        {
            double lockAt=cues?.LockAt(c.area)??double.NaN,arrived=cues?.ArrivedAt(c.area)??double.NaN;
            var stage=NativeExpeditionLaserDoor.Stage(c,t,lockAt,arrived,out double age);
            LaserDrawn=true;LaserStage=stage;LaserArea=door.Building;var f=door.Frame;f.Point(0,f.H/2,DB,out LaserX,out LaserY,out LaserZ);
            switch(stage)
            {
                case ExpeditionLaserStage.Lock:LaserLockStart=t-age;break;
                case ExpeditionLaserStage.Sealed:LaserLockStart=!double.IsNaN(lockAt)&&!(c.began>arrived)?lockAt:c.began-NativeExpeditionLaserDoor.LockSeconds;break;
                case ExpeditionLaserStage.Unlock:LaserUnlockStart=c.began;break;
                case ExpeditionLaserStage.Open:LaserUnlockStart=c.began-NativeExpeditionLaserDoor.LockSeconds;break;
            }
        }

        private void Laser(ExpeditionDoorRig door,int index,ExpeditionClosureView c,double t,in ExpeditionEye eye,ExpeditionStateCues cues,bool reduced,bool low)
        {
            var f=door.Frame;float W=f.W,H=f.H,half=W/2,dBack=-f.D/2;int s=index*64+4099;var pal=NativeExpeditionLaserDoor.Palette(door.Building);
            double lockAt=cues?.LockAt(c.area)??double.NaN,arrived=cues?.ArrivedAt(c.area)??double.NaN;
            var stage=NativeExpeditionLaserDoor.Stage(c,t,lockAt,arrived,out double age);int n=NativeExpeditionLaserDoor.Count(low);
            bool blocking=NativeExpeditionStates.Blocking(c),lit=stage!=ExpeditionLaserStage.Open;
            double pulseAge=double.NaN;if(stage==ExpeditionLaserStage.Sealed&&cues!=null&&cues.DoorPulse(c.area,t,out double pa))pulseAge=pa;
            float charge=NativeExpeditionLaserDoor.Charge(stage,age),ch=H*charge;
            // ---- the beams' levels (the floor, the frame, the haze and the motes read them) ----
            float sum=0,floorBeam=0;
            for(int i=0;i<n;i++)
            {
                float lv=NativeExpeditionLaserDoor.Level(stage,age,i,n,t,s,reduced,out _,out _);
                if(lv>0&&(stage==ExpeditionLaserStage.Sealed||stage==ExpeditionLaserStage.Lock))lv*=NativeExpeditionLaserDoor.Hum(i,n,t,s,reduced||stage==ExpeditionLaserStage.Lock&&age<NativeExpeditionLaserDoor.LockSeconds);
                if(!double.IsNaN(pulseAge))lv+=reduced?0:.8f*NativeExpeditionLaserDoor.Pulse(pulseAge,i,n);
                laserLevel[i]=lv;sum+=Math.Min(1.3f,lv);if(i==0)floorBeam=lv;
            }
            float L=sum/n;
            // The state's colour: red sealed, amber while it moves (blinking), green open.
            bool moving=stage==ExpeditionLaserStage.Lock||stage==ExpeditionLaserStage.Unlock;
            float blink=moving&&!reduced?(Math.Floor(t*8)%2==0?1:.25f):1;
            int lamp=stage==ExpeditionLaserStage.Open?NativeExpeditionLaserDoor.OpenHex:moving?NativeExpeditionLaserDoor.AmberHex:NativeExpeditionLaserDoor.LockRedHex;

            // ---- the emitters (solid kit boxes) ----
            for(int k=-1;k<=1;k+=2)
            {
                int ks=s+(k<0?0:20);
                B(f,k,half+.015f,half+Col,0,H+.02f,D0,D1,Metal,Bank,SteelHex,1,1,ks);
                B(f,k,half+.004f,half+.016f,.05f,H-.05f,.25f,.41f,NativeBlockLooks.Plain,0,VoidHex,1,1,ks+1);
                B(f,k,half,half+Col+.04f,0,.06f,D0-.02f,D1+.04f,Metal,Bank,SteelLightHex,1,1,ks+2);
                B(f,k,half,half+Col+.04f,H-.02f,H+.02f,D0-.02f,D1+.04f,Metal,Bank,SteelLightHex,1,1,ks+3);
                B(f,k,half+.06f,half+Col-.06f,.1f,.34f,D1,D1+.006f,NativeBlockLooks.Demolition,0,0xF4B321,1,1,ks+4);
                // The charge rail: a dark track, filled from the floor in the beam's colour.
                B(f,k,half+.17f,half+.29f,.42f,H-.62f,D1,D1+.008f,NativeBlockLooks.Plain,0,VoidHex,1,1,ks+5);
                float top=.42f+(H-1.04f)*charge;if(charge>.003f)B(f,k,half+.185f,half+.275f,.43f,Math.Max(.44f,top-.01f),D1+.008f,D1+.016f,Tube,Bank,pal.Beam,2.2f,1,ks+6);
                // The status lamp over the rail and its hood.
                B(f,k,half+.14f,half+.32f,H-.5f,H-.34f,D1,D1+.03f,Tube,Bank,lamp,3,blink,ks+7);
                B(f,k,half+.12f,half+.34f,H-.33f,H-.3f,D1,D1+.07f,Metal,Bank,SteelLightHex,1,1,ks+8);
                if(!low)for(int j=0;j<4;j++){float v=.9f+j*.5f;B(f,k,half+Col,half+Col+.05f,v,v+.07f,D0+.04f,D1-.04f,Metal,Bank,SteelLightHex,1,1,ks+9+j);}
                // A lens per beam on the inner face: lit with its beam, white-hot as it fires, an ember as it cools, a pilot spark
                // while it charges (the charge front passing it), dark otherwise.
                for(int i=0;i<n;i++)
                {
                    float v=NativeExpeditionLaserDoor.Height(i,n,H),lv=laserLevel[i],em=NativeExpeditionLaserDoor.Ember(stage,age,i,n);int hex=pal.Beam;float g=2.4f,level=Math.Min(1,lv);
                    if(lv<=0){if(em>0){hex=pal.Ember;g=2;level=em;}else if(stage==ExpeditionLaserStage.Lock&&ch>v-.25f){level=.35f;}else{hex=stage==ExpeditionLaserStage.Open?NativeExpeditionLaserDoor.OpenHex:pal.Beam;level=.06f;}}
                    B(f,k,half,half+.012f,v-.032f,v+.032f,DB-.035f,DB+.035f,Tube,Bank,hex,g,level,ks+13+i);
                }
            }
            // The header over the lintel: its display window, the hazard edge, the sill and the recess.
            B(f,0,-(half+Col),half+Col,H+.02f,H+.4f,D0,D1,Metal,Bank,SteelHex,1,1,s+50);
            B(f,0,-1.3f,1.3f,H+.08f,H+.34f,D1,D1+.006f,NativeBlockLooks.Plain,0,VoidHex,1,1,s+51);
            B(f,0,-(half+Col),half+Col,H+.02f,H+.065f,D1,D1+.006f,NativeBlockLooks.Demolition,0,0xF4B321,1,1,s+52);
            // The sill stays within the 3 cm the doorway allows; its emitter groove lies in it, flush with its top.
            B(f,0,-half,half,0,.03f,D0+.02f,DB-.02f,Metal,Bank,SteelHex,1,1,s+53);B(f,0,-half,half,0,.03f,DB+.02f,D1,Metal,Bank,SteelHex,1,1,s+56);
            B(f,0,-(half-.06f),half-.06f,0,.029f,DB-.02f,DB+.02f,Tube,Bank,lit?pal.Beam:NativeExpeditionLaserDoor.OpenHex,2,lit?Math.Max(.15f,Math.Min(1,floorBeam)):.35f,s+54);
            B(f,0,-half,half,.002f,H-.002f,dBack+.01f,dBack+.025f,NativeBlockLooks.Plain,0,VoidHex,1,1,s+55);

            // ---- the field sheet: exactly the closure face, for as long as the closure blocks ----
            if(blocking)
            {
                float sheet=stage==ExpeditionLaserStage.Sealed?(reduced?1:.85f+.15f*(float)Math.Sin(t*2.2)):reduced?.8f:NativeExpeditionStates.Hash((int)Math.Floor(t*20),s)>.25f?.9f:.6f;
                Rect(f,-half,half,0,H,SealDepth(f),pal.Glow,1,.02f*sheet,false);LaserSheet=true;
            }
            // ---- the beams ----
            for(int i=0;i<n;i++)
            {
                float lv0=NativeExpeditionLaserDoor.Level(stage,age,i,n,t,s,reduced,out float flash,out float reach);float I=laserLevel[i];if(lv0<=0||I<=0)continue;
                LaserBeamsLit++;float v=NativeExpeditionLaserDoor.Height(i,n,H);bool fromLeft=(i&1)==0;float ua=fromLeft?-half:half,ub=fromLeft?-half+W*reach:half-W*reach;
                Ribbon(f,ua,ub,v,DB,.011f*(1+.8f*flash),pal.Core,2.4f*Math.Min(1.4f,I)*(1+2*flash),1,eye);
                Ribbon(f,ua,ub,v,DB,.04f,pal.Beam,1.8f,.6f*Math.Min(1.3f,I),eye);
                Ribbon(f,ua,ub,v,DB,.12f,pal.Glow,1.2f,.17f*Math.Min(1.3f,I),eye);
                if(!low)SoftBeam(f,ua,ub,v,DB,.16f+.14f*flash,pal.Glow,1,.11f*Math.Min(1.3f,I)+.35f*flash,eye);
                // The lenses' flares, a burst of sparks as a beam snaps on.
                for(int k=-1;k<=1;k+=2)
                {
                    if(k==(fromLeft?1:-1)&&reach<1)continue;f.Point(k*half,v,DB,out float x,out float y,out float z);
                    Sprite(eye,x,y,z,.045f+.06f*flash,pal.Core,2.6f,.9f*Math.Min(1,I));Sprite(eye,x,y,z,.13f+.2f*flash,pal.Beam,1.4f,.35f*Math.Min(1,I)+.4f*flash);
                    if(flash>0&&!reduced&&!low)for(int j=0;j<5;j++)
                    {
                        float a=1-flash,dx=(NativeExpeditionStates.Hash(s+i*8+j,k+3)-.5f)*.9f,dy=NativeExpeditionStates.Hash(s+i*8+j,k+5)*.7f,dd=NativeExpeditionStates.Hash(s+i*8+j,k+7)*.6f;
                        f.Point(k*half-k*Math.Abs(dx)*a,v+(dy-2.2f*a)*a,DB+dd*a,out float sx,out float sy,out float sz);Sprite(eye,sx,sy,sz,.018f,pal.Core,2.5f,flash);
                    }
                }
                // The haze the beam lights: soft glows drifting along it.
                if(!low)for(int j=0;j<3;j++)
                {
                    float u0=NativeExpeditionStates.Hash(s+i,j+11),drift=reduced?0:(float)((t*.045*(j%2==0?1:-1)+10)%1),u=-half+W*((u0+drift)%1);
                    if(fromLeft?u>ub:u<ub)continue;f.Point(u,v,DB+(NativeExpeditionStates.Hash(s+i,j+17)-.5f)*.2f,out float x,out float y,out float z);
                    Sprite(eye,x,y,z,.28f+.14f*NativeExpeditionStates.Hash(s+i,j+19),pal.Glow,1,.05f*Math.Min(1,I));
                }
            }
            // ---- the light the beams throw: the floor in front and in the recess, the floor beam's line, the jambs, the emitters'
            // inner faces, the lintel's underside and the recess wall ----
            if(L>0)
            {
                Floor(f,-half-.3f,half+.3f,DB-.45f,DB+1.05f,.012f,pal.Glow,1,.17f*L,1);Floor(f,-half-.7f,half+.7f,DB-.5f,DB+2.9f,.01f,pal.Glow,1,.075f*L,1);
                if(floorBeam>0)Floor(f,-half+.05f,half-.05f,DB-.08f,DB+.08f,.016f,pal.Beam,1.4f,.35f*Math.Min(1,floorBeam),1);
                if(!low)
                {
                    Floor(f,-half,half,dBack+.03f,DB-.02f,.014f,pal.Glow,1,.12f*L,1);
                    for(int k=-1;k<=1;k+=2){Side(f,k*(half-.003f),dBack-.02f,D0+.02f,-.1f,H+.1f,pal.Glow,1,.16f*L);Side(f,k*(half+.013f),D0-.02f,D1+.02f,-.1f,H+.1f,pal.Glow,1.2f,.2f*L);}
                    Floor(f,-half,half,dBack,D1,H-.004f,pal.Glow,1,.1f*L,1);Rect(f,-half*.95f,half*.95f,0,H,dBack+.03f,pal.Glow,1,.07f*L,true,1);
                }
            }
            else if(stage==ExpeditionLaserStage.Open&&!low)Floor(f,-half*.8f,half*.8f,DB-.3f,DB+1.3f,.012f,NativeExpeditionLaserDoor.OpenHex,1,.05f,1);
            // ---- the motes that glint as they cross a beam ----
            if(!low&&!reduced&&L>0)for(int m=0;m<18;m++)
            {
                float hu=NativeExpeditionStates.Hash(s+m,41),hv=NativeExpeditionStates.Hash(s+m,42),hd=NativeExpeditionStates.Hash(s+m,43);float cyc=(float)((t*.06+NativeExpeditionStates.Hash(s+m,44))%1);
                float u=-half+W*((hu+.04f*(float)Math.Sin(t*.4+m))%1+1)%1,v=H*((hv+cyc*.5f)%1),d=DB+(hd-.5f)*.5f,g=.05f;
                for(int i=0;i<n;i++){float dv=(v-NativeExpeditionLaserDoor.Height(i,n,H))/.035f;g+=laserLevel[i]*1.4f*(float)Math.Exp(-dv*dv);}
                f.Point(u,v,d,out float x,out float y,out float z);Sprite(eye,x,y,z,.011f+.008f*NativeExpeditionStates.Hash(s+m,45),pal.Core,2,Math.Min(1,g)*.8f);
            }
            // ---- the charge front's sparks (the emitters charging) ----
            if(stage==ExpeditionLaserStage.Lock&&age>=0&&charge<1&&!reduced&&!low)for(int k=-1;k<=1;k+=2)for(int j=0;j<4;j++)
            {
                int slot=(int)Math.Floor(t*30);float a=(float)(t*30-slot),dx=NativeExpeditionStates.Hash(slot*8+j,k+11),dy=NativeExpeditionStates.Hash(slot*8+j,k+13)-.3f;
                f.Point(k*(half+.02f)-k*dx*.25f*a,ch+dy*.3f*a-.6f*a*a,DB+(NativeExpeditionStates.Hash(slot*8+j,k+17)-.5f)*.2f,out float x,out float y,out float z);Sprite(eye,x,y,z,.02f,pal.Core,2.6f,1-a);
                if(j==0){f.Point(k*(half+.03f),ch,DB,out x,out y,out z);Sprite(eye,x,y,z,.16f,pal.Beam,1.6f,.6f);}
            }
            // ---- the cooling emitters: a glow on each column as the beams let go ----
            if(stage==ExpeditionLaserStage.Unlock||stage==ExpeditionLaserStage.Open)for(int i=0;i<n;i++)
            {
                float em=NativeExpeditionLaserDoor.Ember(stage,age,i,n);if(em<=0)continue;float v=NativeExpeditionLaserDoor.Height(i,n,H);
                for(int k=-1;k<=1;k+=2){f.Point(k*(half+.01f),v,DB,out float x,out float y,out float z);Sprite(eye,x,y,z,.07f+.06f*em,pal.Ember,2,.75f*em);if(!low&&!reduced)Sprite(eye,x,y+.15f*(1-em),z,.12f,0x3A3640,1,.18f*em,false);}
            }
            // ---- the flash as SEALED slams on, and a teammate's re-seal ----
            if(stage==ExpeditionLaserStage.Lock&&!reduced){double a=age-NativeExpeditionLaserDoor.SealAt*NativeExpeditionLaserDoor.LockSeconds;if(a>=0&&a<.22){float k=1-(float)a/.22f;Rect(f,-half,half,0,H,SealDepth(f)+.02f,pal.Core,1.4f,.3f*k,true);}}
            if(!double.IsNaN(pulseAge)&&!reduced&&pulseAge<.25)Rect(f,-half,half,0,H,SealDepth(f)+.02f,pal.Glow,1.2f,.18f*(1-(float)pulseAge/.25f),true);
            LaserHeader(f,s,stage,age,t,pal,lamp,blink,reduced,low);
        }

        // The display over the doorway: the padlock, the word and the chevrons (they converge as the door locks, chase while it is
        // sealed and slide apart as it unlocks), on the window's face.
        private void LaserHeader(in ExpeditionFrame f,int s,ExpeditionLaserStage stage,double age,double t,in ExpeditionLaserPalette pal,int lamp,float blink,bool reduced,bool low)
        {
            float H=f.H,d=D1+.009f,cs=.042f;int w=NativeExpeditionLaserDoor.Word(stage,age);
            int ink=stage==ExpeditionLaserStage.Open?NativeExpeditionLaserDoor.OpenHex:w==NativeExpeditionLaserDoor.WordSealed?pal.Ink:NativeExpeditionLaserDoor.AmberHex;
            float g=w==NativeExpeditionLaserDoor.WordSealed?2.2f:2,alpha=w==NativeExpeditionLaserDoor.WordLocking||w==NativeExpeditionLaserDoor.WordUnlocking?blink:1;
            double slam=stage==ExpeditionLaserStage.Lock?age-NativeExpeditionLaserDoor.SealAt*NativeExpeditionLaserDoor.LockSeconds:-1;float scale=1;
            if(slam>=0&&slam<.18&&!reduced){float k=(float)slam/.18f;scale=1+.35f*(1-k)*(1-k);g*=1+1.5f*(1-k);}
            if(stage==ExpeditionLaserStage.Unlock&&!reduced&&NativeExpeditionStates.Hash((int)Math.Floor(t*24),s+3)<.2f)alpha*=.3f;
            float cw=NativeExpeditionLaserDoor.WordWidth(w)*cs*scale,cy=H+.21f,top=cy+2.5f*cs*scale,left=-cw/2+.08f;var cells=NativeExpeditionLaserDoor.WordCells[w];
            for(int i=0;i+3<cells.Length;i+=4)
            {
                float u0=left+cells[i]*cs*scale,u1=u0+cells[i+2]*cs*scale,v1=top-cells[i+1]*cs*scale,v0=v1-cells[i+3]*cs*scale;Rect(f,u0,u1,v0,v1,d,ink,g,alpha,true);
            }
            if(!low)Rect(f,left-.08f,left+cw+.08f,cy-.17f,cy+.17f,d-.001f,ink,1,.22f*alpha,true,1);
            // The padlock, left of the word: its shackle shut while the door holds, lifted as it unlocks and open.
            float pu=left-.2f,lift=stage==ExpeditionLaserStage.Open?.05f:stage==ExpeditionLaserStage.Unlock?.05f*(float)Math.Min(1,age/(NativeExpeditionLaserDoor.LockSeconds*.4)):0;int pl=stage==ExpeditionLaserStage.Open?NativeExpeditionLaserDoor.OpenHex:lamp;
            Rect(f,pu-.055f,pu+.055f,cy-.1f,cy+.0f,d,pl,2,1,true);
            Rect(f,pu-.04f,pu-.022f,cy+lift,cy+.07f+lift,d,pl,2,1,true);Rect(f,pu+.022f,pu+.04f,cy+(stage==ExpeditionLaserStage.Open?.02f:0)+lift,cy+.07f+lift,d,pl,2,1,true);Rect(f,pu-.04f,pu+.04f,cy+.07f+lift,cy+.088f+lift,d,pl,2,1,true);
            if(low||stage==ExpeditionLaserStage.Open)return;
            // The chevrons: three either side pointing in at the word.
            float spread=0,fade=1;
            if(stage==ExpeditionLaserStage.Lock){double k=(age-(NativeExpeditionLaserDoor.SealAt-.12)*NativeExpeditionLaserDoor.LockSeconds)/(.12*NativeExpeditionLaserDoor.LockSeconds);spread=k>=1?0:k<=0?.55f:.55f*(1-(float)(k*k));fade=k<=0?0:1;}
            else if(stage==ExpeditionLaserStage.Unlock){spread=.5f*NativeExpeditionStates.Ease((float)(age/NativeExpeditionLaserDoor.LockSeconds));fade=1-(float)(age/NativeExpeditionLaserDoor.LockSeconds);}
            if(fade<=0)return;
            for(int side=-1;side<=1;side+=2)for(int j=0;j<3;j++)
            {
                float chase=reduced||stage!=ExpeditionLaserStage.Sealed?1:.45f+.55f*(float)Math.Max(0,Math.Sin(t*5-j*1.1));float u=side*(cw/2+.24f+j*.13f+spread)+.08f,h=.06f,wd=.035f,sx=-side*.05f;
                Shape(f,u-sx-wd,cy+h,u-sx,cy+h,u+sx,cy,u+sx-wd,cy,d,pal.Beam,2,fade*chase,true);Shape(f,u+sx-wd,cy,u+sx,cy,u-sx,cy-h,u-sx-wd,cy-h,d,pal.Beam,2,fade*chase,true);
            }
        }
        /// <summary>A kit box mirrored to side k (-1 left, +1 right; 0 as given) of the frame's centre line.</summary>
        private void B(in ExpeditionFrame f,int k,float a,float b,float v0,float v1,float d0,float d1,int look,int flags,int hex,float gain,float level,int seed)
        {
            if(k==0){Box(f,a,b,v0,v1,d0,d1,look,flags,hex,gain,level,seed);return;}float x=k*a,y=k*b;Box(f,Math.Min(x,y),Math.Max(x,y),v0,v1,d0,d1,look,flags,hex,gain,level,seed);
        }
        /// <summary>A camera-facing ribbon along the doorway's width from u0 to u1 at height v and depth d.</summary>
        private void Ribbon(in ExpeditionFrame f,float u0,float u1,float v,float d,float width,int hex,float gain,float alpha,in ExpeditionEye e)
        {
            if(!(alpha>0)||Math.Abs(u1-u0)<1e-3f)return;f.Point(u0,v,d,out float ax,out float ay,out float az);f.Point(u1,v,d,out float bx,out float by,out float bz);
            float dx=bx-ax,dy=by-ay,dz=bz-az,mx=(ax+bx)/2-e.X,my=(ay+by)/2-e.Y,mz=(az+bz)/2-e.Z,sx=dy*mz-dz*my,sy=dz*mx-dx*mz,sz=dx*my-dy*mx,l=(float)Math.Sqrt(sx*sx+sy*sy+sz*sz);
            if(l<1e-6f){sx=0;sy=1;sz=0;l=1;}float k=width/2/l;sx*=k;sy*=k;sz*=k;
            Quads.Add(ax-sx,ay-sy,az-sz,bx-sx,by-sy,bz-sz,bx+sx,by+sy,bz+sz,ax+sx,ay+sy,az+sz,Lin(hex,16)*gain,Lin(hex,8)*gain,Lin(hex,0)*gain,alpha,0,true,1);
        }
        /// <summary>A soft glow round a beam from u0 to u1: overlapping camera-facing soft sprites stretched along it (radius r
        /// across, a third of their length apart), so the halo has no hard edge.</summary>
        private void SoftBeam(in ExpeditionFrame f,float u0,float u1,float v,float d,float r,int hex,float gain,float alpha,in ExpeditionEye e)
        {
            float len=Math.Abs(u1-u0);if(!(alpha>0)||len<.05f)return;int n=Math.Max(2,(int)Math.Ceiling(len/.24f));float step=(u1-u0)/n,half=Math.Abs(step)*1.5f;
            f.Point(1,0,0,out float x1,out float y1,out float z1);f.Point(0,0,0,out float x0,out float y0,out float z0);float ax=x1-x0,az=z1-z0;
            for(int i=0;i<=n;i++)
            {
                float u=u0+step*i;f.Point(u,v,d,out float cx,out float cy,out float cz);
                float tx=cx-e.X,ty=cy-e.Y,tz=cz-e.Z,sx=-az*ty,sy=az*tx-ax*tz,sz=ax*ty,l=(float)Math.Sqrt(sx*sx+sy*sy+sz*sz);if(l<1e-6f){sx=0;sy=1;sz=0;l=1;}
                float k=r/l;sx*=k;sy*=k;sz*=k;float hx=ax*half,hz=az*half,w=i==0||i==n?.5f:1;
                Quads.Add(cx-hx-sx,cy-sy,cz-hz-sz,cx+hx-sx,cy-sy,cz+hz-sz,cx+hx+sx,cy+sy,cz+hz+sz,cx-hx+sx,cy+sy,cz-hz+sz,Lin(hex,16)*gain,Lin(hex,8)*gain,Lin(hex,0)*gain,alpha*w,1,true,1);
            }
        }
        /// <summary>A soft additive glow in the plane u (a jamb's or an emitter's inner face), d0..d1 by v0..v1.</summary>
        private void Side(in ExpeditionFrame f,float u,float d0,float d1,float v0,float v1,int hex,float gain,float alpha)
        {
            if(!(alpha>0))return;f.Point(u,v0,d0,out float ax,out float ay,out float az);f.Point(u,v0,d1,out float bx,out float by,out float bz);f.Point(u,v1,d1,out float cx,out float cy,out float cz);f.Point(u,v1,d0,out float dx,out float dy,out float dz);
            Quads.Add(ax,ay,az,bx,by,bz,cx,cy,cz,dx,dy,dz,Lin(hex,16)*gain,Lin(hex,8)*gain,Lin(hex,0)*gain,alpha,1,true,1);
        }
    }
}
