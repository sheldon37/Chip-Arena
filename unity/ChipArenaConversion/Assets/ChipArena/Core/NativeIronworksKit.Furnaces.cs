using System;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeIronworksKit
    {
        /// <summary>The three open-hearth furnaces' centres (x); their faces at z 28, the charging deck at y 6.</summary>
        public static readonly float[] FurnaceX={-15f,0f,15f};
        public const float FurnaceFace=28f;
        /// <summary>The runners' flow (the taphole at the furnace face to the trough's end), their trough's half width and floor.</summary>
        public const float RunnerStart=27.9f,RunnerEnd=12f,RunnerHalf=.6f,RunnerTop=.2f;

        private void Furnaces()
        {
            var steel=Iron(IronHex);var steelD=Detail(steel);var soot=Iron(SootHex);var band=Iron(IronDarkHex);
            for(int k=0;k<3;k++)
            {
                double c=FurnaceX[k];Under("furnace-"+(k+1));short hz=(short)(NativeIronworksHazards.R1+k);double ash=k==0?-17.5:17.5;if(k==1)ash=2.5;
                // Buckstays: H-columns up the refractory (front flange and web; inside the wall band), clear of the doors.
                foreach(double dx in new[]{-4.8,-3.9,-1.0,1.0,3.9,4.8})
                {
                    double x=c+dx;if(Math.Abs(x-ash)<1.25)continue;
                    Span(x-.15,x+.15,0,13.8,27.76,27.8,steel);Span(x-.014,x+.014,0,13.8,27.8,27.98,steelD);Span(x-.2,x+.2,0,.05,27.7,28,steelD);
                }
                // Tie bands across the face, under the deck and over the doors.
                foreach(double y in new[]{3.0,4.7,11.4,13.2})Span(c-5,c+5,y,y+.2,27.72,27.76,y<6?Detail(band):band);
                for(double x=c-4.6;x<=c+4.6;x+=.92)foreach(double y in new[]{3.2,11.5,13.3})Span(x-.03,x+.03,y-.03,y+.03,27.7,27.72,Detail(Iron(SteelHex)));
                // The furnace's name on its face (white paint letters).
                Text("NO "+(k+1),c+3.0,12.2,FurnaceFace,3,.13,.005,.02,P(NativeBlockLooks.Plain,WhitePaintHex,RoleSign));
                Taphole(c,hz);
                // The ash door under the deck (a spawn's door: they walk out of the dark with a dim red glow behind them).
                AshDoor(ash);
                // The charging doors on the deck: the west one open (the furnace-mouth spawn), the east one shut with glowing seams
                // (No 2's raised half way).
                ChargingDoor(c-2.5,k,true,false);ChargingDoor(c+2.5,k,k==1,k==1);
                // The hood over both doors and the flue up through the roof space, banded.
                Under("furnace-"+(k+1));Span(c-4.3,c+4.3,10.1,10.9,26.4,28,soot);Span(c-4.35,c+4.35,10.05,10.12,26.3,26.45,band);
                Span(c-.8,c+.8,10.9,17.45,26.75,27.6,Iron(0x2E2A26));for(double y=11.6;y<17.4;y+=1.4)Span(c-.84,c+.84,y,y+.12,26.71,27.64,steelD);
                Light(c-2.5,7.4,26.4,FurnaceHex,1.6,12,GlowFurnace,0,-1,1.6,k*.37);Light(c+2.5,7.4,26.4,FurnaceHex,k==1?1.2:.55,k==1?11:7,GlowFurnace,0,-1,k==1?1.2:.6,k*.37+.5);
            }
            // The deck's charging-machine rails (flat), the charge boxes' scrap and the deck spawn's door in the skip housing.
            Under("deck-north");var rail=Iron(SteelLightHex,RoleFloor);foreach(double z in new[]{24.1,27.1})Span(-23.6,35.6,6.0,6.045,z-.05,z+.05,rail);
            foreach(var stem in new[]{"charge-box-0","charge-box-1"}){var b=B(stem);if(b==null)continue;Under(stem);Scrap(X0(b)+.1,X1(b)-.1,Y1(b),Z0(b)+.1,Z1(b)-.1,stem[stem.Length-1]+40);}
            {short s=SpawnAt(30.5,6,25.6);Own(s);Under("skip-housing");Begin();Doorway(30.5,6,FurnaceFace,2.3,Iron(IronDarkHex),.0f);End();Own(-1);}
            Under("skip-housing");Text("SKIP",30.9,11.0,FurnaceFace,3,.16,.005,.02,P(NativeBlockLooks.Plain,0xB8B0A0,RoleSign));
            // Tap carts: oxygen bottles and lances on the carts (within the carts' band).
            foreach(var stem in new[]{"tap-cart-w","tap-cart-e"})
            {
                var b=B(stem);if(b==null)continue;Under(stem);double top=Y1(b);
                for(int i=0;i<4;i++){double x=X0(b)+.35+i*.55,z=Z0(b)+.4;RodY(top,top+.2,x,z,.11,Iron(i%2==0?0x2A4A3A:0x3A3A40));Span(x-.04,x+.04,top+.2,top+.24,z-.04,z+.04,Detail(Iron(SteelLightHex)));}
                for(int i=0;i<3;i++)Member(X0(b)+.2,top+.03+i*.03,Z1(b)-.4-i*.18,X1(b)-.2,top+.03+i*.03,Z1(b)-.5-i*.18,.03,.03,Detail(Iron(RustHex)));
                Span(X0(b)+.1,X1(b)-.1,top,top+.04,Z0(b)+.1,Z1(b)-.1,Detail(Iron(SootHex)));
            }
        }
        // The taphole at the furnace's foot: the cast frame, the clay plug's glow, the spout over the runner and the runner's metal.
        private void Taphole(double c,short hz)
        {
            var steel=Iron(IronDarkHex);
            Span(c-.55,c+.55,.08,1.12,27.86,28,steel);Span(c-.38,c+.38,.22,.98,27.84,27.86,Iron(RefractoryDarkHex));
            Span(c-.2,c+.2,.38,.74,27.82,27.84,Glow(FurnaceHex,3.2f,RoleMolten,DriveTap,(float)(c*.1),hz));
            // The spout: a cast trough lip from the hole over the runner's head (inside the curbs).
            Span(c-.46,c+.46,.2,.28,26.85,27.86,steel);Span(c-.52,c-.44,.2,.5,26.85,27.86,steel);Span(c+.44,c+.52,.2,.5,26.85,27.86,steel);
            MeltSpan(c-.42,c+.42,.28,.34,26.88,27.84,MoltenFlow,2,-1,1.8f,1f,.94f,.12f,MDriveRunner,hz);
            // The runner: the trough's metal from the furnace to the trough's end, hottest at the furnace, crust floating on it.
            MeltSpan(c-RunnerHalf,c+RunnerHalf,.02,RunnerTop,RunnerEnd,RunnerStart-1.05,MoltenFlow,2,-1,1.4f,.95f,.6f,.3f,MDriveRunner,hz);
            Light(c,1.0,26.4,FurnaceHex,1.4,9,GlowTaphole,(sbyte)(1+hz),hz,.8,c);
            for(double z=13;z<27;z+=2.0)Light(c,.4,z,0xFF7A26,.55,5.5,GlowRunner,(sbyte)(1+hz),hz,.6,z*.1);
            Light(c,.8,18.5,0xFF7A26,1.2,8,GlowRunner,-1,hz,1,c*.2);
            // Hazard paint along the trough's outer sides (flat, worn) and the MOLTEN METAL stencils at its head.
            var paint=Detail(Paint(0x9C7A2A));
            for(double z=13;z<27.5;z+=1.2){Span(c-1.15,c-.88,0,.012,z,z+.55,paint);Span(c+.88,c+1.15,0,.012,z,z+.55,paint);}
        }
        private void AshDoor(double x)
        {
            short s=SpawnAt(x,0,25.6);Own(s);Begin();
            Doorway(x,0,FurnaceFace,2.4,Iron(IronDarkHex),.25f);
            // The leaf swung open against the face beside the doorway (inside the band), its hinge straps.
            Span(x+1.0,x+1.85,.05,2.35,27.8,27.9,Iron(0x2C2824));Span(x+1.05,x+1.8,.4,.5,27.76,27.8,Detail(Iron(SootHex)));Span(x+1.05,x+1.8,1.9,2.0,27.76,27.8,Detail(Iron(SootHex)));
            End();Own(-1);
        }
        /// <summary>A dark doorway in a face at z = `face` looking toward -z (`dir` -1) or +z (+1): jambs, lintel, the black
        /// interior and a dim red glow low in it (`glow` its share; 0 none).</summary>
        private void Doorway(double x,double y,double face,double h,Pen frame,float glow,int dir=-1)
        {
            double a=face,b=face+dir*.2,c=face+dir*.22,i=face+dir*.02,g=face+dir*.03;
            Span(x-1.02,x-.82,y,y+h,Math.Min(a,b),Math.Max(a,b),frame);Span(x+.82,x+1.02,y,y+h,Math.Min(a,b),Math.Max(a,b),frame);Span(x-1.02,x+1.02,y+h,y+h+.3,Math.Min(a,c),Math.Max(a,c),frame);
            Span(x-.82,x+.82,y,y+h,Math.Min(a,i),Math.Max(a,i),P(NativeBlockLooks.Plain,0x050404,RoleSpawn));
            if(glow>0)Melt(x,y+.25,g,1.5,.5,.02,MoltenFire,1,1,.5f,.38f*glow*4,.18f,0f,MDriveFurnace,-1,FrameStatic,(float)(x*.31));
        }
        /// <summary>The same in a face at x = `face` looking toward -x (`dir` -1) or +x (+1).</summary>
        private void DoorwayX(double z,double y,double face,double h,Pen frame,float glow,int dir)
        {
            double a=face,b=face+dir*.2,c=face+dir*.22,i=face+dir*.02,g=face+dir*.03;
            Span(Math.Min(a,b),Math.Max(a,b),y,y+h,z-1.02,z-.82,frame);Span(Math.Min(a,b),Math.Max(a,b),y,y+h,z+.82,z+1.02,frame);Span(Math.Min(a,c),Math.Max(a,c),y+h,y+h+.3,z-1.02,z+1.02,frame);
            Span(Math.Min(a,i),Math.Max(a,i),y,y+h,z-.82,z+.82,P(NativeBlockLooks.Plain,0x050404,RoleSpawn));
            if(glow>0)Melt(g,y+.25,z,.02,.5,1.5,MoltenFire,1,1,.5f,.38f*glow*4,.18f,0f,MDriveFurnace,-1,FrameStatic,(float)(z*.31));
        }
        private void ChargingDoor(double d,int k,bool open,bool half)
        {
            short s=open&&!half?SpawnAt(d,6,25.6):(short)-1;Own(s);Under("furnace-"+(k+1));Begin();
            var jacket=Iron(0x34302C);var pipe=Detail(Iron(GreenDarkHex));double y0=6.0,top=8.3;
            // The water-cooled frame: jambs, lintel and sill, the cooling water stubs. Everything on the deck sits within the wall
            // band of the furnace face (0.25 m), so nothing stands out into the walkway.
            Span(d-1.15,d-.85,y0,9.2,27.86,28,jacket);Span(d+.85,d+1.15,y0,9.2,27.86,28,jacket);Span(d-.85,d+.85,top,9.2,27.86,28,jacket);Span(d-.85,d+.85,y0,y0+.05,27.86,28,jacket);
            foreach(double sx in new[]{-1.0,1.0})foreach(double y in new[]{6.6,7.8,8.9})Span(d+sx-.06,d+sx+.06,y,y+.1,27.8,27.86,pipe);
            if(open)
            {
                // Fire in the mouth: the charge burning (NativeMolten fire: rising tongues, the hottest low in the bed).
                Melt(d,(y0+top)/2,27.95,1.7,top-y0,.04,MoltenFire,1,1,.9f,.86f,.5f,0f,MDriveFurnace,-1,FrameStatic,(float)(d*.21+k));
                double leaf=half?7.35:9.12;Span(d-.95,d+.95,leaf,leaf+2.16,27.76,27.86,Iron(0x3A3632));Span(d-.9,d+.9,leaf+.95,leaf+1.05,27.75,27.76,Detail(Iron(SootHex)));
                if(half)Span(d-.86,d+.86,leaf-.04,leaf,27.78,27.86,Glow(SeamHex,1.8f,RoleMolten,DriveSeam,(float)d));
                // The lifting chains to the counterweight beam on the hood (a fully raised leaf hangs from it directly).
                if(leaf+2.16<10.1)foreach(double sx in new[]{-.7,.7})Span(d+sx-.02,d+sx+.02,leaf+2.16,10.1,27.79,27.83,Detail(Iron(ChainHex,RoleChain)));
            }
            else
            {
                // Shut: the leaf over the mouth, its glowing seams, a sight hole and the lifting lugs.
                Span(d-.95,d+.95,6.12,8.28,27.76,27.86,Iron(0x3A3632));var seam=Glow(SeamHex,1.7f,RoleMolten,DriveSeam,(float)d);
                Span(d-.86,d+.86,6.06,6.12,27.78,27.86,seam);Span(d-.86,d+.86,8.28,8.31,27.78,27.86,seam);Span(d-.89,d-.86,6.1,8.3,27.78,27.86,seam);Span(d+.86,d+.89,6.1,8.3,27.78,27.86,seam);
                RodZ(27.75,27.76,d,7.6,.07,Glow(FurnaceHex,2.4f,RoleMolten,DriveFire,(float)d));
                foreach(double sx in new[]{-.6,.6}){Span(d+sx-.06,d+sx+.06,8.0,8.25,27.75,27.76,Detail(Iron(SootHex)));Span(d+sx-.02,d+sx+.02,8.25,10.1,27.77,27.81,Detail(Iron(ChainHex,RoleChain)));}
            }
            End();Own(-1);
        }
        // Scrap heaped in a box: jumbled plates and bars (inside the box's band over its top).
        private void Scrap(double x0,double x1,double y,double z0,double z1,int seed)
        {
            int n=(int)((x1-x0)*(z1-z0)*3.2);
            for(int i=0;i<n;i++)
            {
                double x=x0+.2+Hash(seed,i,1)*(x1-x0-.4),z=z0+.2+Hash(seed,i,2)*(z1-z0-.4),a=Hash(seed,i,3)*180,t=Hash(seed,i,4)*30-15;
                var q=Then(Yaw(a),AxisRot(1,0,0,t));Box(x,y+.05+Hash(seed,i,5)*.1,z,.15+Hash(seed,i,6)*.5,.03+Hash(seed,i,7)*.05,.08+Hash(seed,i,8)*.3,Detail(Iron(i%3==0?RustHex:i%3==1?SteelHex:RustDarkHex,RoleProp)),q);
            }
        }
    }
}
