using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Deployment Station's console over the scene (docs/EXPEDITION-DEPLOYMENT.md "Presentation"), drawn with the menus' kit:
    /// the instance machine's destination cards (each revealed as its reel lands: name, tag, entrance side, colour), the mutator
    /// machine's cards (name, line, polarity, PENDING until Deploy and ACTIVE after) or its optional / skipped / unavailable
    /// note, the party's readiness, and the console: the host's buttons (ROLL DESTINATIONS, ROLL MUTATORS, SKIP MUTATORS, DEPLOY,
    /// enabled by NativeDeployStage.ControlsOf) or a guest's status line; the travel fade over everything at Deploy. It only
    /// returns what was pressed (DeployIntent): the caller asks the authority. Text comes from the display tables and constant
    /// labels, so a frame builds no strings.
    /// </summary>
    public static class NativeDeployConsole
    {
        /// <summary>The party as the console shows it: names (null: an empty seat) and readiness, seat 0 the host.</summary>
        public sealed class Party {public readonly string[] Names=new string[NativeDeployPresentation.Pads];public readonly bool[] Ready=new bool[NativeDeployPresentation.Pads];public int Count;}
        private static string Side(string side)=>side=="east"?"EAST ENTRANCE":side=="west"?"WEST ENTRANCE":side=="south"?"SOUTH ENTRANCE":"ENTRANCE";
        private static Color Hex(int rgb,float a=1)=>U.Hex(rgb,a);
        private static readonly string[] ReelLabels={"EAST REEL","WEST REEL","SOUTH REEL"};

        /// <summary>One console frame over a W x H canvas; returns the intent pressed (None for guests and while nothing is).</summary>
        public static DeployIntent Draw(NativeDeployStage stage,ExpeditionDeploymentState s,Party party,bool host,float W,float Hh,float now)
        {
            if(stage==null)return DeployIntent.None;var intent=DeployIntent.None;
            float fade=stage.Deploy.Fade;float ui=1-fade;
            if(ui>0)
            {
                Header(W,s,ui);Destinations(stage,s,ui);Mutators(stage,s,W,ui);
                intent=Console(stage,s,party,host,W,Hh,now,ui);
            }
            if(fade>0)NativeScreenTransitionHud.Faded(W,Hh,fade,Mathf.Clamp01(fade*1.6f-.4f),"EXPEDITION · DEPLOYING",stage.Deploy.Travel?"PREPARING DESTINATION":"DEPLOYING");
            return intent;
        }
        private static void Header(float W,ExpeditionDeploymentState s,float a)
        {
            U.Text(new Rect(32,22,520,16),"EXPEDITION · PRE-RUN",11,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.22f);
            U.Text(new Rect(32,38,620,34),"DEPLOYMENT STATION",28,C.Ink(a),TextAnchor.MiddleLeft,bold:true,tracking:.04f);
            U.Text(new Rect(32,72,640,18),s!=null&&s.deployed?"The run is locked. The party is deploying.":"Roll three destinations, add mutators if you dare, then deploy together.",13,C.Muted(a),TextAnchor.MiddleLeft);
        }
        // The instance machine's three destinations, each revealed when its reel lands.
        private static void Destinations(NativeDeployStage stage,ExpeditionDeploymentState s,float a)
        {
            var look=stage.InstanceLook;var r=new Rect(28,112,352,58+3*70);H.Panel(r,a,Hex(0xffb45a),12,H.Corners.Diagonal,true,.9f);
            U.Text(new Rect(r.x+18,r.y+12,300,16),"INSTANCE MACHINE",11,Hex(0xffb45a,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);
            U.Text(new Rect(r.x+18,r.y+30,r.width-36,18),look.Banner,12,C.Ink(.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.06f);
            for(int i=0;i<3;i++)
            {
                var row=new Rect(r.x+14,r.y+58+i*70,r.width-28,62);var d=s!=null&&s.worldRolled&&i<s.districts.Length?s.districts[i]:null;bool shown=d!=null&&stage.Poses[i].Landed;
                U.Fill(row,C.Void(.55f*a));
                if(!shown)
                {
                    U.Fill(new Rect(row.x,row.y,4,row.height),C.Dim(.5f*a));
                    U.Text(new Rect(row.x+16,row.y+10,row.width-24,20),d!=null?"ROLLING…":"AWAITING ROLL",14,C.Muted(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
                    U.Text(new Rect(row.x+16,row.y+32,row.width-24,18),ReelLabels[i],11,C.Dim(a),TextAnchor.MiddleLeft,mono:true,tracking:.12f);
                    continue;
                }
                var dest=NativeDeployPresentation.DestinationOf(d.instance,d.theme);var tone=Hex(dest.Color,a);
                U.Fill(new Rect(row.x,row.y,4,row.height),tone);U.Glow(new Rect(row.x,row.y,4,row.height),Hex(dest.Color,.35f*a),10);
                U.Text(new Rect(row.x+16,row.y+8,row.width-24,24),dest.Name,18,C.Ink(a),TextAnchor.MiddleLeft,bold:true,tracking:.02f);
                U.Text(new Rect(row.x+16,row.y+34,row.width-130,18),dest.Tag,12,C.Muted(a),TextAnchor.MiddleLeft);
                var tag=new Rect(row.xMax-122,row.y+34,114,18);H.Tag(tag,tone,a);U.Text(tag,Side(d.side),9,tone,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.1f);
            }
        }
        // The mutator machine: its cards once landed, else its note (optional, skipped, unavailable, rolling).
        private static void Mutators(NativeDeployStage stage,ExpeditionDeploymentState s,float W,float a)
        {
            var look=stage.MutatorLook;var ph=look.Phase;int n=s!=null?Mathf.Min(3,s.mutators.Length):0;bool cards=n>0&&(ph==NativeDeployPresentation.Phase.Pending||ph==NativeDeployPresentation.Phase.Active);
            var r=new Rect(W-380,112,352,58+(cards?n:1)*70);H.Panel(r,a,Hex(0x9d8cff),12,H.Corners.Diagonal,true,.9f);
            U.Text(new Rect(r.x+18,r.y+12,300,16),"MUTATOR MACHINE",11,Hex(0xb88cff,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);
            U.Text(new Rect(r.x+18,r.y+30,r.width-36,18),look.Banner,12,C.Ink(.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.06f);
            if(!cards)
            {
                var row=new Rect(r.x+14,r.y+58,r.width-28,62);U.Fill(row,C.Void(.55f*a));
                string title=ph==NativeDeployPresentation.Phase.Spinning?"ROLLING…":ph==NativeDeployPresentation.Phase.Skipped||ph==NativeDeployPresentation.Phase.Locked?"NO MUTATORS":ph==NativeDeployPresentation.Phase.Unavailable?"LOCKED":"OPTIONAL";
                string line=ph==NativeDeployPresentation.Phase.Unavailable?"Roll the destinations first.":ph==NativeDeployPresentation.Phase.Optional?"Roll up to three, or deploy without any.":ph==NativeDeployPresentation.Phase.Spinning?"The dials are turning.":"The run deploys as rolled.";
                var tone=ph==NativeDeployPresentation.Phase.Optional?C.CyanSoft(a):C.Muted(a);U.Fill(new Rect(row.x,row.y,4,row.height),tone);
                U.Text(new Rect(row.x+16,row.y+8,row.width-24,24),title,18,C.Ink(a),TextAnchor.MiddleLeft,bold:true,tracking:.04f);
                U.Text(new Rect(row.x+16,row.y+34,row.width-24,18),line,12,C.Muted(a),TextAnchor.MiddleLeft);
                return;
            }
            bool active=ph==NativeDeployPresentation.Phase.Active;
            for(int i=0;i<n;i++)
            {
                var row=new Rect(r.x+14,r.y+58+i*70,r.width-28,62);var m=NativeDeployPresentation.MutatorOf(s.mutators[i]);var tone=Hex(m.Color,a);
                U.Fill(row,C.Void(.55f*a));U.Fill(new Rect(row.x,row.y,4,row.height),tone);U.Glow(new Rect(row.x,row.y,4,row.height),Hex(m.Color,.35f*a),10);
                U.Text(new Rect(row.x+16,row.y+8,row.width-150,24),m.Name,18,tone,TextAnchor.MiddleLeft,bold:true,tracking:.02f);
                U.Text(new Rect(row.x+16,row.y+34,row.width-130,18),m.Line,12,C.Muted(a),TextAnchor.MiddleLeft);
                var pol=new Rect(row.xMax-122,row.y+10,54,18);H.Tag(pol,tone,a);U.Text(pol,NativeDeployPresentation.PolarityLabel(m.Polarity),9,tone,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.08f);
                var st=new Rect(row.xMax-64,row.y+10,58,18);var sc=active?C.Good(a):C.Warn(a);H.Tag(st,sc,a);U.Text(st,active?"ACTIVE":"PENDING",9,sc,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.08f);
            }
        }
        // The console: the party's readiness and the host's buttons (or the guest's status line).
        private static DeployIntent Console(NativeDeployStage stage,ExpeditionDeploymentState s,Party party,bool host,float W,float Hh,float now,float a)
        {
            var intent=DeployIntent.None;float w=Mathf.Min(980,W-64);var r=new Rect((W-w)/2,Hh-138,w,112);var launch=stage.Launch;
            var accent=launch==NativeDeployPresentation.Launch.Ready||launch==NativeDeployPresentation.Launch.HostOnly?C.Good():launch==NativeDeployPresentation.Launch.WaitingParty?C.Warn():C.Cyan();
            H.Panel(r,a,accent,14,H.Corners.Diagonal,true,.92f);
            // the party: a chip per seat (name and lamp)
            U.Text(new Rect(r.x+20,r.y+12,200,14),"PARTY",10,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            // one row of chips, narrower as the party grows (up to the eight pads) so they never reach the buttons
            int count=party!=null?Mathf.Min(party.Count,NativeDeployPresentation.Pads):0;float px=r.x+20,cw=count>4?Mathf.Max(52,(Mathf.Min(420,r.width*.42f)-6*(count-1))/count):96;
            for(int i=0;i<count;i++)
            {
                bool ready=party.Ready[i];var chip=new Rect(px,r.y+32,cw,26);U.Fill(chip,C.Void(.6f*a));var lamp=ready?C.Good(a):C.Warn(a);
                U.Disc(new Rect(chip.x+8,chip.y+8,10,10),lamp);U.Text(new Rect(chip.x+24,chip.y,chip.width-28,chip.height),party.Names[i]??"",cw<80?9:11,C.Ink(a),TextAnchor.MiddleLeft,bold:i==0);px+=chip.width+6;
            }
            U.Text(new Rect(r.x+20,r.y+66,420,18),NativeDeployPresentation.ConsoleLabel(launch),13,accent*new Color(1,1,1,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            // the controls
            var c=stage.Controls;float bx=r.xMax-20,by=r.y+28;
            if(host&&!(s!=null&&s.deployed))
            {
                if(U.Btn(new Rect(bx-170,by,170,56),"DEPLOY",true,c.Deploy&&a>.9f,a,17))intent=DeployIntent.Deploy;bx-=180;
                if(U.Btn(new Rect(bx-150,by+30,150,26),"SKIP MUTATORS",false,c.Skip&&a>.9f,a,11))intent=DeployIntent.SkipMutators;
                if(U.Btn(new Rect(bx-150,by,150,26),"ROLL MUTATORS",false,c.Mutate&&a>.9f,a,11))intent=DeployIntent.RollMutators;bx-=160;
                if(U.Btn(new Rect(bx-190,by,190,56),"ROLL DESTINATIONS",false,c.Roll&&a>.9f,a,13))intent=DeployIntent.RollDestinations;
            }
            else
            {
                string status=s!=null&&s.deployed?"DEPLOYING":launch==NativeDeployPresentation.Launch.NeedDestination?"WAITING FOR THE HOST TO ROLL":NativeDeployPresentation.ConsoleLabel(launch);
                U.Text(new Rect(r.xMax-480,r.y+24,460,30),status,18,C.Ink(a),TextAnchor.MiddleRight,bold:true,tracking:.06f);
                U.Text(new Rect(r.xMax-480,r.y+56,460,18),"The host rolls and deploys; the run locks for everyone at Deploy.",12,C.Muted(a),TextAnchor.MiddleRight);
            }
            return intent;
        }
    }
}
