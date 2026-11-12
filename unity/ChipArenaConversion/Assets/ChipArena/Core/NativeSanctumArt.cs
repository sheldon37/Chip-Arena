using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Hollow Sanctum's art poses (docs/arenas/STAGE-ART.md): where each modelled rig part of the stage bank
    /// (tools/stages/sanctum_props.py; its frames are listed there) stands this frame, read only from Core NativeSanctumRig's state
    /// after its Build (the very functions the simulation decides hits with), so the models move exactly as the box rig did:
    /// the chandeliers on their chains (swaying, slipping, falling, lying as wrecks with their candles strewn, hoisted again), the
    /// censer on its rope and its pull rope, the bell and its clapper, the crypt gates' grilles and lanterns, the holy fire's
    /// baskets by their heat and the banners stirring. Pure and allocation free per frame.
    /// </summary>
    public static class NativeSanctumArt
    {
        /// <summary>The rig props by slot (the bank's "rig/&lt;name&gt;/&lt;tier&gt;").</summary>
        public static readonly string[] Slots={"chandelier","chandelier-wreck","chain","censer","pull-rope","bell","clapper",
            "chandelier-candles-0","chandelier-candles-1","chandelier-candles-2","chandelier-candles-3",
            "wreck-candles-0","wreck-candles-1","wreck-candles-2","wreck-candles-3","floor-candles-0","floor-candles-1","floor-candles-2","floor-candles-3",
            "gate-sanctum-gate-w","gate-sanctum-gate-e","lanterns-sanctum-gate-w","lanterns-sanctum-gate-e",
            "fire-sanctum-fire-n-0","fire-sanctum-fire-n-1","fire-sanctum-fire-s-0","fire-sanctum-fire-s-1","banner-red","banner-blue",
            "censer-body","censer-lid","candle-loose","debris-iron","debris-link","debris-plaster"};
        public const int Chandelier=0,Wreck=1,Chain=2,Censer=3,PullRope=4,Bell=5,Clapper=6,Candles=7,WreckCandles=11,FloorCandles=15,Gate=19,Lanterns=21,Fire=23,BannerRed=27,BannerBlue=28,
            CenserBody=29,CenserLid=30,LooseCandle=31,DebrisIron=32,DebrisLink=33,DebrisPlaster=34;
        /// <summary>The first of the slots a bank from before the hazards' polish (2026-11-11) lacks. Without them (`legacy`) the
        /// censer draws as the one rigid model on its rope and the thrown candles as the wreck's strewn set.</summary>
        public const int FirstPolishSlot=CenserBody;
        /// <summary>The chain's ring over the hub (local y in the chandelier's frame), where the chain starts.</summary>
        public const double ChainFoot=NativeSanctumRig.ChainFoot;
        /// <summary>The tether's cleat on the south bridge (the rig's).</summary>
        public const double CleatY=NativeSanctumRig.CleatY,CleatZ=NativeSanctumRig.CleatZ;

        public static void Pose(NativeSanctumRig rig,MapHazard[] hazards,double render,NativeStageArtPose into,bool legacy=false)
        {
            into.Clear();if(rig==null)return;
            // the chandeliers: each in its frame (the rig's Body and BodyQ: swaying and slipping on its chain, tipping as it falls,
            // the wreck slamming, hopping and rocking to rest, righted by the winch), its chain link by link along the rig's line
            // (held, lashing after the snap, paid out again), the stub on its crown, the candles and debris the crash throws
            for(int k=0;k<NativeSanctumRig.Chandeliers;k++)
            {
                int o=k*5;double x=rig.Ring[o],y=rig.Ring[o+1]-.12,z=rig.Ring[o+2];int phase=(int)rig.Ring[o+4];
                if(x==0&&z==0&&y<=0)continue;
                double bx=rig.Body[k*3],by=rig.Body[k*3+1],bz=rig.Body[k*3+2];var q=rig.BodyQ[k];
                if(phase!=3){into.Add(Chandelier,bx,by,bz,q);into.Add(Candles+k,bx,by,bz,q);}
                else{into.Add(Wreck,bx,by,bz,q);into.Add(WreckCandles+k,bx,by,bz,q);if(legacy)into.Add(FloorCandles+k,x,y,z,FactoryQ.Identity);}
                int c=k*NativeSanctumRig.ChainPoints*3;for(int i=0;i+1<rig.ChainCount[k];i++)Line(into,Chain,rig.ChainPts[c+i*3+3],rig.ChainPts[c+i*3+4],rig.ChainPts[c+i*3+5],rig.ChainPts[c+i*3],rig.ChainPts[c+i*3+1],rig.ChainPts[c+i*3+2],1,1.15);
                int sc=k*NativeSanctumRig.StubPoints*3;for(int i=0;i+1<rig.StubCount[k];i++)Line(into,Chain,rig.StubPts[sc+i*3],rig.StubPts[sc+i*3+1],rig.StubPts[sc+i*3+2],rig.StubPts[sc+i*3+3],rig.StubPts[sc+i*3+4],rig.StubPts[sc+i*3+5],1,1.15);
                if(legacy)continue;
                for(int i=0;i<NativeSanctumRig.ChandelierCandles;i++)
                {
                    int l=(k*NativeSanctumRig.ChandelierCandles+i)*8;double s=rig.Loose[l+7];if(s<=.02)continue;NativeSanctumRig.Candle(k,i,out _,out double tall,out _,out _,out _,out _);
                    into.AddScaled(LooseCandle,rig.Loose[l],rig.Loose[l+1],rig.Loose[l+2],new FactoryQ{X=rig.Loose[l+3],Y=rig.Loose[l+4],Z=rig.Loose[l+5],W=rig.Loose[l+6]},s,tall*s,s);
                }
                for(int i=0;i<NativeSanctumRig.DebrisCount;i++)
                {
                    int d=(k*NativeSanctumRig.DebrisCount+i)*8;double s=rig.Scrap[d+7];if(s<=.02)continue;int slot=i<NativeSanctumRig.Shards?DebrisIron:i<NativeSanctumRig.Shards+NativeSanctumRig.Links?DebrisLink:DebrisPlaster;
                    into.AddScaled(slot,rig.Scrap[d],rig.Scrap[d+1],rig.Scrap[d+2],new FactoryQ{X=rig.Scrap[d+3],Y=rig.Scrap[d+4],Z=rig.Scrap[d+5],W=rig.Scrap[d+6]},s,s,s);
                }
            }
            // the censer: its body on its swivel at the rig's turn (cocked back by the tether, whipping through at the release,
            // trailing and leading its rope), the lid lifted on its chains, the six chains bowed, the rope to the yoke and the
            // tether to the cleat along the rig's lines
            foreach(var h in hazards??Array.Empty<MapHazard>())
            {
                if(h==null||h.kind!=NativeFactoryHazards.SawRail)continue;
                double cx=rig.CenserX,cy=rig.CenserY,cz=rig.CenserZ,glow=rig.CenserGlow;var q=rig.CenserQ;
                if(legacy)
                {
                    // the one rigid model, up its rope from the simulation's point
                    double a=rig.CenserAngle,L=rig.CenserRope,py=NativeSanctumLooks.CenserPivotY;into.Add(Censer,cx,py-L*Math.Cos(a),Math.Sin(a)*L,FactoryQ.Axis(1,0,0,-a),1,glow,glow,glow);
                }
                else
                {
                    into.Add(CenserBody,cx,cy,cz,q,1,glow,glow,glow);q.Rotate(0,rig.LidHop,0,out double lx,out double ly,out double lz);into.Add(CenserLid,cx+lx,cy+ly,cz+lz,rig.LidQ,1,glow,glow,glow);
                    q.Rotate(0,NativeSanctumRig.CenserRing,0,out double rx,out double ry,out double rz);rx+=cx;ry+=cy;rz+=cz;
                    for(int i=0;i<6;i++)
                    {
                        double a=Math.PI/3*i;q.Rotate(Math.Cos(a)*.6,.16,Math.Sin(a)*.6,out double ax,out double ay,out double az);ax+=cx;ay+=cy;az+=cz;
                        double mx=(ax+rx)/2+rig.ChainBowX,my=(ay+ry)/2,mz=(az+rz)/2+rig.ChainBowZ;Line(into,Chain,ax,ay,az,mx,my,mz,.45,3);Line(into,Chain,mx,my,mz,rx,ry,rz,.45,3);
                    }
                    for(int i=0;i+1<NativeSanctumRig.RopePoints;i++){int p=i*3;Line(into,PullRope,rig.RopePts[p],rig.RopePts[p+1],rig.RopePts[p+2],rig.RopePts[p+3],rig.RopePts[p+4],rig.RopePts[p+5],1.6,2.2);}
                }
                for(int i=0;i+1<NativeSanctumRig.TetherPoints;i++){int p=i*3;Line(into,PullRope,rig.TetherPts[p],rig.TetherPts[p+1],rig.TetherPts[p+2],rig.TetherPts[p+3],rig.TetherPts[p+4],rig.TetherPts[p+5],1,2.2);}
                break;
            }
            // the bell and its clapper (it lags the swing)
            into.Add(Bell,NativeSanctumLooks.BellX,NativeSanctumLooks.BellY,NativeSanctumLooks.BellZ,FactoryQ.Axis(1,0,0,rig.Bell));
            into.Add(Clapper,NativeSanctumLooks.BellX,NativeSanctumLooks.BellY,NativeSanctumLooks.BellZ,FactoryQ.Axis(1,0,0,rig.Bell*.55));
            // the crypt gates: each grille at its closure, the lanterns in their colour; the holy fire by its heat
            int gate=0,fire=0;
            foreach(var h in hazards??Array.Empty<MapHazard>())
            {
                if(h==null)continue;
                if(h.kind==NativeWardZeroHazards.Shutter&&gate<2&&gate<rig.GateCount)
                {
                    double bottom=h.center.y-h.size.y/2,tall=h.size.y,y0=bottom+(1-rig.GateClosure[gate])*tall;into.Add(Gate+gate,h.center.x,y0,h.center.z,FactoryQ.Identity);
                    int lamp=rig.GateLamp[gate];Lin(lamp,out double r,out double gg,out double b);into.Add(Lanterns+gate,0,0,0,FactoryQ.Identity,1,r,gg,b);gate++;
                }
                else if(h.kind==NativeWardZeroHazards.Burner&&fire<rig.FireCount)
                {
                    double heat=rig.FireHeat[fire];double k=.15+1.6*heat*heat;int zones=Math.Min(2,h.zones?.Length??0);
                    for(int zi=0;zi<zones&&fire*2+zi<4;zi++)into.Add(Fire+fire*2+zi,0,0,0,FactoryQ.Identity,1,k,k*(.55+.45*heat),k*(.25+.75*heat*heat));
                    fire++;
                }
            }
            // the banners on the arcade piers' nave faces: crimson west, blue east
            foreach(int s in Sides)foreach(double px in NativeSanctumRig.BannerPiers)foreach(int sz in Sides)
            {
                double x=s*px,zc=sz*NativeSanctumRig.BannerZ-sz*NativeSanctumRig.BannerReach;
                // the model hangs across z on the +z side's pier; the -z side's turns half round to face the nave the same way
                var q=FactoryQ.Axis(0,0,1,NativeSanctumRig.BannerSway(render,px,sz));
                into.Add(s<0?BannerRed:BannerBlue,x,NativeSanctumRig.BannerTop,zc,q);
            }
        }
        private static readonly int[] Sides={-1,1};
        // A unit-length prop (a metre of chain or rope along its y) laid from one point to another, `thick` times its own girth,
        // in pieces no longer than `most` (so a long run's links are not stretched).
        private static void Line(NativeStageArtPose into,int slot,double x0,double y0,double z0,double x1,double y1,double z1,double thick,double most)
        {
            double dx=x1-x0,dy=y1-y0,dz=z1-z0,len=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(len<.02)return;int n=Math.Max(1,(int)Math.Ceiling(len/most-1e-6));double piece=len/n;
            var q=NativeStageArtPose.FromTo(0,1,0,dx/len,dy/len,dz/len);for(int i=0;i<n;i++){double t=(double)i/n;into.AddScaled(slot,x0+dx*t,y0+dy*t,z0+dz*t,q,thick,piece,thick);}
        }
        // An sRGB hex to linear 0..1 (the lanterns' tint multiplies a white glow).
        private static void Lin(int hex,out double r,out double g,out double b){r=L((hex>>16)&255);g=L((hex>>8)&255);b=L(hex&255);}
        private static double L(int c){double v=c/255.0;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}
    }
}
