#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The loading screen's party panel (Claude, 2026-10-18; Core NativePartyLoadModel, NativePartyLayout; Runtime
    // NativePartyLoadingHud, NativePartyLoadingUI): each member's state from the public view only (a first load, a loop's, a
    // transfer's cohort with the authority's count, a member already in the destination, one loading elsewhere, dropped,
    // extracted, my own load and readiness), YOU first then this load's members by seat, the footer's count, the replicated
    // adapters unknown today (and bound once the member view carries the contract's fields), the layout at 720p @1.5 through
    // 3440x1440 and 4K for one to eight members, the reduced tier, no per-frame allocation, and the wiring: an Expedition load
    // only, presentation only, no authority or network file touched.
    internal static class PartyLoadingChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Party loading: "+what);count++;}
        private const string City="exp-city",Station="exp-transit",Ward="exp-ward-zero",Foundry="exp-foundry";
        private static readonly string[] Accounts={"nova","astra","kestrel","sheldon","vex","juno","rook","echo"};
        private static readonly string[] Weapons={"pistol","greatsword","bow","daggers","rifle","sword","shield","fire"};
        /// <summary>A view as the authority projects it for `viewer`: n members in the city (seat i+1, nova the host), the barrier
        /// active toward `target` (the city by default) unless `held` is false; `edit` changes members, `entered` marks buildings.</summary>
        private static ExpeditionView View(string viewer,int n=4,bool held=true,string target=City,int ready=0,int required=1,Action<ExpeditionViewMember> edit=null,bool entered=false,int loop=0)
        {
            var v=new ExpeditionView{run="r",world=City,area=target,phase=ExpeditionPhase.Town,viewer=viewer,host="nova",loop=loop,loading=held};
            v.instances=new[]{Ward,Station,Foundry}.Select(i=>new ExpeditionViewInstance{id=i,target=36,entered=entered&&i==Ward}).ToArray();
            v.members=Enumerable.Range(0,n).Select(i=>{var m=new ExpeditionViewMember{account=Accounts[i],name=char.ToUpperInvariant(Accounts[i][0])+Accounts[i].Substring(1),weapon=Weapons[i],seat=i+1,host=i==0,connected=true,alive=true,area=City};edit?.Invoke(m);return m;}).ToArray();
            v.areaBarrier=new ExpeditionAreaBarrierView{target=target,active=held,ready=ready,required=required,layout=new string('a',64)};
            v.mine=new ExpeditionViewPersonal{account=viewer,connected=true,alive=true};return v;
        }
        private static PartyLoadRow Row(NativePartyLoadModel m,string account){for(int i=0;i<m.Count;i++)if(m.Rows[i].Account==account)return m.Rows[i];throw new Exception("no row "+account);}

        public static void Run(string root)
        {
            count=0;var R=new Func<string,string>(f=>File.ReadAllText(Path.Combine(root,f)));
            States();Transfers();Order();Footer();Adapters();Local();
            Layouts();Motion();Allocation();Wiring(R,root);
            Console.WriteLine("PASS party loading: "+count+" checks (states per source: first load, loop, transfer cohort and count, resident, elsewhere, dropped, extracted, mine; YOU first; footer counts; replicated adapters unknown today; layouts 720p @1.5 to 4K and 3440x1440 for 1-8; reduced tier; allocation-free; wiring, no authority or network file touched)");
        }

        // ---- the state of each member, by source -------------------------------------------------------------------------------
        private static void States()
        {
            var labels=new ExpeditionLabels();var m=new NativePartyLoadModel();
            // The run's first load, on the host: everyone in the city, my barrier up. I load at 60%; the view names no one else's.
            m.Build(View("nova"),.6f,false,1,10,labels);
            Check(m.Count==4&&m.Rows[0].You&&m.Rows[0].Account=="nova","first load: four rows, mine first");
            var me=Row(m,"nova");Check(me.State==PartyLoadState.Loading&&Math.Abs(me.Progress-.6f)<1e-6f&&me.Counted&&me.Chip=="LOADING","first load: mine LOADING at my real 60%");
            foreach(var a in new[]{"astra","kestrel","sheldon"}){var r=Row(m,a);Check(r.State==PartyLoadState.Loading&&r.Progress==NativePartyLoadModel.Unknown&&r.Counted,"first load: "+a+" LOADING, no value (indeterminate), counted");}
            Check(m.Counted==4&&m.Pending==4&&m.ReadyCount==0&&m.Footer=="WAITING FOR 4 PLAYERS…"&&m.Aggregate=="READY 0 / 4"&&!m.EveryoneIn,"first load: waiting for 4, ready 0 / 4");
            // A guest's view of the same load: the host is LOADING too (the view has no per-member readiness), its HOST tag kept.
            m.Build(View("kestrel"),.3f,false,1,10,labels);var host=Row(m,"nova");
            Check(m.Rows[0].Account=="kestrel"&&host.Host&&host.State==PartyLoadState.Loading&&host.Progress<0,"a guest sees the host LOADING (no per-member value), HOST marked");
            // Mine ready while the game still holds me: READY, full; once the barrier is down: IN WORLD.
            m.Build(View("nova"),1,true,1,11,labels);me=Row(m,"nova");Check(me.State==PartyLoadState.Ready&&me.Progress==1&&me.Chip=="READY","mine READY while held");
            Check(m.Pending==3&&m.Footer=="WAITING FOR 3 PLAYERS…"&&m.Aggregate=="READY 1 / 4","my readiness leaves three to wait for");
            m.Build(View("nova",held:false),1,true,1,12,labels);me=Row(m,"nova");Check(me.State==PartyLoadState.InWorld&&me.Chip=="IN WORLD","mine IN WORLD once the barrier is down");
            foreach(var a in new[]{"astra","kestrel","sheldon"})Check(Row(m,a).State==PartyLoadState.InWorld&&Row(m,a).Counted,"no barrier: "+a+" stands in the area with me (in)");
            Check(m.EveryoneIn&&m.Footer==NativePartyLoadModel.Everyone&&m.Footer=="EVERYONE'S IN","no barrier: everyone's in");
            // A new loop reloads the whole party into the city (no building entered): the same as a first load.
            m.Build(View("astra",loop:1),.2f,false,2,13,labels);Check(Row(m,"sheldon").State==PartyLoadState.Loading&&Row(m,"sheldon").Counted&&m.Pending==4,"a new loop: the party loads together");
            // A lone reload after buildings were entered (a resume): the others stand where they are.
            m.Build(View("astra",entered:true,edit:x=>{if(x.account=="sheldon")x.area=Ward;}),.2f,false,3,14,labels);
            Check(Row(m,"nova").State==PartyLoadState.InWorld&&Row(m,"nova").Counted&&Row(m,"sheldon").State==PartyLoadState.InWorld&&!Row(m,"sheldon").Counted&&Row(m,"sheldon").Place=="WARD ZERO","a resume: city members in (counted), a zone member in world elsewhere (not counted, its place)");
            Check(m.Pending==1&&m.Footer=="WAITING FOR 1 PLAYER…","a resume: only my own load to wait for (singular)");
            // Dropped and extracted: shown, never waited for.
            m.Build(View("nova",edit:x=>{if(x.account=="astra")x.connected=false;if(x.account=="kestrel")x.extracted=true;}),.5f,false,4,15,labels);
            Check(Row(m,"astra").State==PartyLoadState.Dropped&&!Row(m,"astra").Counted&&Row(m,"astra").Chip=="DROPPED","a lost link: DROPPED, not waited for");
            Check(Row(m,"kestrel").State==PartyLoadState.Extracted&&!Row(m,"kestrel").Counted&&Row(m,"kestrel").Chip=="EXTRACTED","extracted: shown, not waited for");
            Check(m.Counted==2&&m.Pending==2&&m.Aggregate=="READY 0 / 2","the count leaves out the dropped and the extracted");
            // A solo run: one row, mine.
            m.Build(View("nova",n:1),.45f,false,5,16,labels);Check(m.Count==1&&m.Rows[0].You&&m.Header=="PARTY · 1"&&m.Footer=="WAITING FOR 1 PLAYER…","solo: one row");
            m.Build(View("nova",n:1),1,true,5,17,labels);Check(m.EveryoneIn&&m.Aggregate=="READY 1 / 1","solo: everyone's in once ready");
            // Names: the authority's display name, upper case; the account when none.
            m.Build(View("nova",edit:x=>{if(x.account=="astra")x.name=null;}),.5f,false,6,18,labels);Check(Row(m,"nova").Name=="NOVA"&&Row(m,"astra").Name=="ASTRA"&&Row(m,"astra").Weapon=="greatsword","names upper case (account fallback), weapon id kept for the icon");
            // No view, no panel.
            m.Build(null,.5f,false,7,19,labels);Check(m.Count==0&&m.Footer==null,"no view: no panel");
        }

        // ---- an area transfer: the cohort and the authority's count ----------------------------------------------------------------
        private static void Transfers()
        {
            var labels=new ExpeditionLabels();var m=new NativePartyLoadModel();
            // Nova and Astra deploy to Central Station; Kestrel stays in the hub; Sheldon loads into Ward Zero.
            Action<ExpeditionViewMember> split=x=>{if(x.account=="nova"||x.account=="astra")x.pendingArea=Station;if(x.account=="sheldon")x.pendingArea=Ward;};
            m.Build(View("nova",target:Station,ready:0,required:2,edit:split),.4f,false,1,10,labels);
            Check(Row(m,"astra").State==PartyLoadState.Loading&&Row(m,"astra").Counted&&Row(m,"astra").Progress<0,"transfer: the cohort member LOADING, counted");
            Check(Row(m,"kestrel").State==PartyLoadState.InWorld&&!Row(m,"kestrel").Counted&&Row(m,"kestrel").Place=="HUB","transfer: the one staying is IN WORLD at the HUB, not counted");
            Check(Row(m,"sheldon").State==PartyLoadState.Loading&&!Row(m,"sheldon").Counted&&Row(m,"sheldon").Place=="WARD ZERO","transfer: one loading elsewhere is LOADING into WARD ZERO, not counted");
            Check(m.Counted==2&&m.Pending==2&&m.Footer=="WAITING FOR 2 PLAYERS…","transfer: waiting for the cohort (2)");
            // The authority counts one of two ready while I am still loading: that is Astra.
            m.Build(View("nova",target:Station,ready:1,required:2,edit:split),.5f,false,1,10.5f,labels);
            Check(Row(m,"astra").State==PartyLoadState.Ready&&m.Pending==1&&m.Aggregate=="READY 1 / 2","transfer: the count names the only other member READY");
            Check(Row(m,"astra").StateAge<1e-4f,"her arrival starts its fill now");
            // I am locally ready and the count says one: my own report may be the one, so she stays LOADING.
            var m2=new NativePartyLoadModel();m2.Build(View("nova",target:Station,ready:1,required:2,edit:split),1,true,1,11,labels);
            Check(Row(m2,"astra").State==PartyLoadState.Loading&&Row(m2,"nova").State==PartyLoadState.Ready&&m2.Pending==1,"transfer: an uncertain count never shows a member in early");
            m2.Build(View("nova",target:Station,ready:2,required:2,edit:split),1,true,1,11.2f,labels);
            Check(Row(m2,"astra").State==PartyLoadState.Ready&&m2.EveryoneIn&&m2.Footer=="EVERYONE'S IN","transfer: both counted, everyone's in");
            // A cohort of four: me and one of the three others counted ready, nobody named: the rows stay LOADING, the footer counts.
            Action<ExpeditionViewMember> all=x=>x.pendingArea=Station;
            m.Build(View("nova",target:Station,ready:2,required:4,edit:all),1,true,2,12,labels);
            Check(new[]{"astra","kestrel","sheldon"}.All(a=>Row(m,a).State==PartyLoadState.Loading),"partial count: nobody is named in");
            Check(m.Pending==2&&m.Aggregate=="READY 2 / 4"&&m.Footer=="WAITING FOR 2 PLAYERS…","partial count: the footer and the ready count follow the authority");
            // Committed: everyone in Central Station, the barrier down.
            m.Build(View("nova",target:Station,held:false,edit:x=>x.area=Station),1,true,2,13,labels);
            Check(m.Rows.Take(m.Count).All(r=>r.State==PartyLoadState.InWorld&&r.Counted)&&m.EveryoneIn,"committed: all IN WORLD in the destination");
            // A member already in the destination while my cohort loads (they went first): in, counted.
            m.Build(View("astra",target:Station,ready:0,required:1,edit:x=>{if(x.account=="astra")x.pendingArea=Station;if(x.account=="nova")x.area=Station;}),.3f,false,3,14,labels);
            Check(Row(m,"nova").State==PartyLoadState.InWorld&&Row(m,"nova").Counted&&Row(m,"nova").Place=="CENTRAL STATION","a member already in the destination is in (counted)");
            Check(m.Counted==2&&m.Pending==1,"only my own load to wait for");
            // Returning to the city: the barrier's target is the world, my cohort comes back.
            m.Build(View("kestrel",target:City,ready:0,required:2,entered:true,edit:x=>{x.area=Ward;if(x.account=="kestrel"||x.account=="sheldon")x.pendingArea=City;}),.7f,false,4,15,labels);
            Check(Row(m,"sheldon").State==PartyLoadState.Loading&&Row(m,"sheldon").Counted&&Row(m,"nova").State==PartyLoadState.InWorld&&!Row(m,"nova").Counted&&Row(m,"nova").Place=="WARD ZERO","returning: the cohort loads the city, the rest stay in Ward Zero");
        }

        // ---- order: YOU first, then this load's members by seat, then the rest ------------------------------------------------------
        private static void Order()
        {
            var labels=new ExpeditionLabels();var m=new NativePartyLoadModel();
            m.Build(View("sheldon",n:8),.1f,false,1,1,labels);
            Check(m.Rows[0].Account=="sheldon"&&m.Rows[0].You&&m.Rows.Take(8).Count(r=>r.You)==1,"YOU first, once");
            Check(string.Join(",",m.Rows.Skip(1).Take(7).Select(r=>r.Seat))=="1,2,3,5,6,7,8","then by seat");
            for(int i=0;i<m.Count;i++)Check(m.Rows[i].Order==i,"order indexes the stagger");
            // Seats in any order and an unbound seat (0): sorted, the unbound last; uncounted after counted.
            m.Build(View("astra",n:6,edit:x=>{x.seat=x.account=="nova"?6:x.account=="kestrel"?0:x.seat;if(x.account=="juno")x.area=Ward;}),.1f,false,2,2,labels);
            var seq=string.Join(",",m.Rows.Take(m.Count).Select(r=>r.Account));
            Check(seq=="astra,sheldon,vex,nova,kestrel,juno","counted by seat (unbound last), then the uncounted ("+seq+")");
            // The host's and a guest's panel list the same rows, only YOU moves to the top.
            var a=new NativePartyLoadModel();var b=new NativePartyLoadModel();a.Build(View("nova"),.5f,false,1,1,labels);b.Build(View("kestrel"),.5f,false,1,1,labels);
            var ra=a.Rows.Take(4).Select(r=>r.Account+":"+r.State).OrderBy(x=>x).ToArray();var rb=b.Rows.Take(4).Select(r=>r.Account+":"+r.State).OrderBy(x=>x).ToArray();
            Check(ra.SequenceEqual(rb),"host and guest see the same members in the same states");
            // At most eight rows.
            var big=View("nova",n:8);big.members=big.members.Concat(new[]{new ExpeditionViewMember{account="extra",name="Extra",connected=true,seat=9,area=City}}).ToArray();
            m.Build(big,.1f,false,3,3,labels);Check(m.Count==NativePartyLoadModel.Max,"at most eight rows");
        }

        // ---- the footer and header words ------------------------------------------------------------------------------------------------
        private static void Footer()
        {
            Check(NativePartyLoadModel.WaitingFor(1)=="WAITING FOR 1 PLAYER…"&&NativePartyLoadModel.WaitingFor(2)=="WAITING FOR 2 PLAYERS…"&&NativePartyLoadModel.WaitingFor(8)=="WAITING FOR 8 PLAYERS…","waiting for n player(s)…");
            Check(NativePartyLoadModel.WaitingFor(-3)==NativePartyLoadModel.WaitingFor(0)&&NativePartyLoadModel.WaitingFor(40)==NativePartyLoadModel.WaitingFor(8),"clamped");
            Check(ReferenceEquals(NativePartyLoadModel.WaitingFor(3),NativePartyLoadModel.WaitingFor(3)),"cached");
            Check(NativePartyLoadModel.More(2)=="+2 MORE"&&NativePartyLoadModel.More(0)==null,"the clipped note");
            var labels=new ExpeditionLabels();var m=new NativePartyLoadModel();
            for(int n=1;n<=8;n++)
            {
                m.Build(View("nova",n:n),.5f,false,1,1,labels);Check(m.Header==(n==0?"PARTY":"PARTY · "+n)&&m.Pending==n&&m.Footer==NativePartyLoadModel.WaitingFor(n),"n="+n+": header and footer");
                m.Build(View("nova",n:n,held:false),1,true,1,2,labels);Check(m.EveryoneIn&&m.Pending==0&&m.Aggregate=="READY "+n+" / "+n,"n="+n+": everyone's in");
            }
            Check(NativePartyLoadModel.Chips.Length==Enum.GetValues(typeof(PartyLoadState)).Length,"a chip per state");
        }

        // ---- the replicated adapters: unknown today, bound once the view carries the contract ---------------------------------------
        private static void Adapters()
        {
            var m=new ExpeditionViewMember{account="astra",connected=true};
            var progressField=typeof(ExpeditionViewMember).GetField("loadProgress");var stateField=typeof(ExpeditionViewMember).GetField("loadState");
            if(progressField==null&&stateField==null)
            {
                Check(NativePartyLoadModel.ReplicatedProgress(m)==-1&&NativePartyLoadModel.ReplicatedProgress(null)==-1,"no members[].loadProgress yet: the progress adapter returns -1");
                Check(NativePartyLoadModel.ReplicatedState(m)==null&&NativePartyLoadModel.ReplicatedState(null)==null,"no members[].loadState yet: the state adapter returns unknown");
            }
            else
            {
                // The contract has landed: the adapters must read it (the one-line bindings in their summaries).
                if(stateField!=null)stateField.SetValue(m,"loading");if(progressField!=null)progressField.SetValue(m,.5f);
                Check(Math.Abs(NativePartyLoadModel.ReplicatedProgress(m)-.5f)<1e-6f,"members[].loadProgress is bound to the adapter");
                if(stateField!=null)Check(NativePartyLoadModel.ReplicatedState(m)==PartyLoadState.Loading,"members[].loadState is bound to the adapter");
            }
            Check(NativePartyLoadModel.CleanProgress(float.NaN)==-1&&NativePartyLoadModel.CleanProgress(-.01f)==-1&&NativePartyLoadModel.CleanProgress(1.01f)==-1&&NativePartyLoadModel.CleanProgress(float.PositiveInfinity)==-1,"a bad value is unknown");
            Check(NativePartyLoadModel.CleanProgress(0)==0&&NativePartyLoadModel.CleanProgress(.37f)==.37f&&NativePartyLoadModel.CleanProgress(1)==1,"a good value passes");
            Check(NativePartyLoadModel.ParseState("waiting")==PartyLoadState.Waiting&&NativePartyLoadModel.ParseState("loading")==PartyLoadState.Loading&&NativePartyLoadModel.ParseState("ready")==PartyLoadState.Ready&&NativePartyLoadModel.ParseState("world")==PartyLoadState.InWorld&&NativePartyLoadModel.ParseState("dropped")==PartyLoadState.Dropped,"the contract's state words");
            Check(NativePartyLoadModel.ParseState(null)==null&&NativePartyLoadModel.ParseState("")==null&&NativePartyLoadModel.ParseState("LOADING")==null&&NativePartyLoadModel.ParseState("extracted")==null,"anything else unknown (an older host sends nothing)");
            // With a value (stand-ins for the bound adapters): a determinate bar, READY at 1, the state's word wins.
            var labels=new ExpeditionLabels();var p=new NativePartyLoadModel();
            p.ProgressOverride=x=>x.account=="astra"?.42f:x.account=="kestrel"?1f:-1;p.StateOverride=x=>x.account=="sheldon"?PartyLoadState.Waiting:(PartyLoadState?)null;
            p.Build(View("nova"),.6f,false,1,1,labels);
            Check(Row(p,"astra").State==PartyLoadState.Loading&&Math.Abs(Row(p,"astra").Progress-.42f)<1e-6f,"a replicated value: LOADING with its bar");
            Check(Row(p,"kestrel").State==PartyLoadState.Ready&&Row(p,"kestrel").Arrived,"a replicated 1: READY");
            Check(Row(p,"sheldon").State==PartyLoadState.Waiting&&Row(p,"sheldon").Chip=="WAITING"&&Row(p,"sheldon").Counted,"a replicated waiting: WAITING (for the host), counted");
            Check(p.Pending==3&&p.ReadyCount==1,"replicated values feed the footer");
            p.StateOverride=x=>x.account=="astra"?PartyLoadState.InWorld:(PartyLoadState?)null;p.ProgressOverride=null;
            p.Build(View("nova"),.6f,false,1,2,labels);Check(Row(p,"astra").State==PartyLoadState.InWorld&&p.Pending==3,"a replicated world: IN WORLD");
            Check(Row(p,"nova").Progress==.6f,"mine keeps my local value, whatever is replicated");
        }

        // ---- my own bar ---------------------------------------------------------------------------------------------------------------
        private static void Local()
        {
            var labels=new ExpeditionLabels();var m=new NativePartyLoadModel();
            foreach(var (raw,want) in new[]{(0f,0f),(.25f,.25f),(.6f,.6f),(.999f,.999f),(-.5f,0f),(1.5f,1f),(float.NaN,0f)})
            {m.Build(View("nova"),raw,false,1,1,labels);Check(Row(m,"nova").Progress==want&&Row(m,"nova").State==PartyLoadState.Loading,"my bar shows my local progress ("+raw+")");}
            // Every other member's bar knows nothing unless replicated.
            m.Build(View("nova"),.6f,false,1,1,labels);Check(m.Rows.Skip(1).Take(3).All(r=>r.Progress==-1),"the others' bars are indeterminate");
        }

        // ---- layout at every resolution, 1 to 8 members --------------------------------------------------------------------------
        private static void Layouts()
        {
            var required=new (int w,int h,float s)[]{(1280,720,1.5f),(1920,1080,1),(3440,1440,1),(3840,2160,1)};
            var others=new (int w,int h,float s)[]{(2560,1440,1),(2560,1080,1),(1280,720,1),(1280,720,1.6f),(1280,720,2f),(1600,1200,1),(1024,768,1),(1920,1080,2.4f)};
            foreach(var c in required.Concat(others))
            {
                var ui=NativeUiMotion.Measure(c.w,c.h,c.s);var none=NativeLoadingLayout.Measure(ui.Width,ui.Height,ui.SafeX,ui.SafeWidth);var zero=NativeLoadingLayout.Measure(ui.Width,ui.Height,ui.SafeX,ui.SafeWidth,true,0);
                string at0=c.w+"x"+c.h+" @"+c.s;bool must=required.Contains(c);
                Check(none.Eyebrow==zero.Eyebrow&&none.Party==zero.Party&&none.TitleSize==zero.TitleSize&&!zero.TagHidden&&zero.Roster.Count==0,at0+": no party, the layout is unchanged");
                for(int n=1;n<=8;n++)
                {
                    var l=NativeLoadingLayout.Measure(ui.Width,ui.Height,ui.SafeX,ui.SafeWidth,true,n);var p=l.Roster;string at=at0+" n="+n;
                    Check(p.Count==n&&p.Box.Inside(ui.Width,ui.Height)&&p.Box.X>=ui.SafeX-.01f&&p.Box.Right<=ui.SafeX+ui.SafeWidth+.01f,at+": the panel inside the canvas and the safe band");
                    Check(p.Box.X>=l.Text.X-.01f&&p.Box.Right<=l.Text.Right+.01f&&p.Box.Y>=l.Text.Y-.01f&&(!must||p.Box.Bottom<=l.Text.Bottom+.01f),at+": the panel inside the title column");
                    if(l.Scan)Check(!p.Box.Overlaps(l.Panel),at+": clear of the stage scan");
                    Check(p.Box.Bottom<=l.Label.Y-8&&p.Box.Bottom<=l.Spinner.Y,at+": above the progress row");
                    Check(p.Box.Y>=l.Mode+26+8-.01f&&(l.TagHidden||p.Box.Y>=l.Tag+40-.01f),at+": under the mode tag"+(l.TagHidden?"":" and the tag line"));
                    Check(l.Eyebrow>=l.Text.Y-.01f&&l.TitleSize>=NativePartyLayout.MinTitle&&l.TitleSize<=none.TitleSize+.01f,at+": the title block fits above it");
                    Check(p.Shown>=1&&p.Shown<=n&&p.PerColumn>=1&&p.Columns>=1&&p.Columns<=2&&p.Column>=(p.Columns==2?NativePartyLayout.MinColumn:100),at+": rows to draw ("+p.Shown+", "+p.Columns+" x "+p.Column+")");
                    for(int i=0;i<p.Shown;i++)
                    {
                        var b=p.RowBox(i);Check(b.X>=p.Box.X-.01f&&b.Right<=p.Box.Right+.01f&&b.Y>=p.Header.Bottom-.01f&&b.Bottom<=p.Footer.Y+.01f,at+": row "+i+" inside the panel, between header and footer");
                        for(int j=0;j<i;j++)Check(!b.Overlaps(p.RowBox(j)),at+": rows "+j+" and "+i+" apart");
                    }
                    Check(p.Header.Bottom<=p.Footer.Y&&p.Footer.Bottom<=p.Box.Bottom+.01f,at+": header over footer");
                    if(must)
                    {
                        Check(!p.Clipped&&p.Shown==n,at+": every member shown");
                        Check(p.Columns==(n>4?2:1),at+": one column to four, two from five");
                        Check(Math.Abs(l.TitleSize-none.TitleSize)<.01f,at+": the title keeps its size");
                        Check(p.Row>=NativePartyLayout.RowCompact&&p.Column>=200,at+": readable rows ("+p.Row+" x "+p.Column+")");
                    }
                    else if(n<=4&&!(c.w==1920&&c.s>=2.4f))Check(!p.Clipped,at+": every member shown");
                }
            }
            // Wide: normal rows at 1080p and 4K (compact only where the canvas is short).
            var hd=NativeUiMotion.Measure(1920,1080,1);var lh=NativeLoadingLayout.Measure(hd.Width,hd.Height,hd.SafeX,hd.SafeWidth,true,4);Check(!lh.Roster.Compact&&!lh.TagHidden,"1080p: normal rows, the tag line kept");
            var sm=NativeUiMotion.Measure(1280,720,1.5f);var ls=NativeLoadingLayout.Measure(sm.Width,sm.Height,sm.SafeX,sm.SafeWidth,true,4);Check(ls.Roster.Compact&&!ls.TagHidden,"720p @1.5: compact rows, the tag line kept");
        }

        // ---- motion and the reduced tier ------------------------------------------------------------------------------------------
        private static void Motion()
        {
            var r=new PartyLoadRow{Order=2,Age=0,StateAge=0};
            Check(NativePartyLoadModel.Entrance(r,true)==1&&NativePartyLoadModel.ArrivalFill(r,true)==1&&NativePartyLoadModel.TickScale(r,true)==1,"reduced: rows whole, fill full and the tick at once");
            for(float t=0;t<3;t+=.05f){Check(NativePartyLoadModel.Sweep(t,1,true)==.5f,"reduced: the sweep holds still");}
            float last=-1;for(float age=0;age<1;age+=.01f){r.Age=age;float e=NativePartyLoadModel.Entrance(r,false);Check(e>=last-1e-5f&&e>=0&&e<=1,"the entrance only rises");last=e;}
            r.Age=2*NativePartyLoadModel.Stagger-.001f;Check(NativePartyLoadModel.Entrance(r,false)==0,"the third row waits its stagger");
            r.Age=2*NativePartyLoadModel.Stagger+NativePartyLoadModel.RowIn;Check(NativePartyLoadModel.Entrance(r,false)==1,"and is in after RowIn");
            r.StateAge=0;Check(NativePartyLoadModel.ArrivalFill(r,false)==0&&NativePartyLoadModel.TickScale(r,false)==0,"an arrival starts empty, no tick yet");
            r.StateAge=NativePartyLoadModel.Fill;Check(NativePartyLoadModel.ArrivalFill(r,false)==1,"filled after Fill");
            r.StateAge=NativePartyLoadModel.TickAt;Check(Math.Abs(NativePartyLoadModel.TickScale(r,false)-1.45f)<1e-4f,"the tick pops in");
            r.StateAge=NativePartyLoadModel.TickAt+NativePartyLoadModel.TickPop;Check(Math.Abs(NativePartyLoadModel.TickScale(r,false)-1)<1e-4f,"and settles");
            r.StateAge=NativePartyLoadModel.Settled;Check(NativePartyLoadModel.ArrivalFill(r,false)==1&&NativePartyLoadModel.TickScale(r,false)==1,"a member already in shows settled");
            float a=NativePartyLoadModel.Sweep(1.3f,0,false),b=NativePartyLoadModel.Sweep(1.3f,1,false);Check(a>=0&&a<1&&b>=0&&b<1&&Math.Abs(a-b)>.05f,"each row sweeps on its own phase");
            // A member first seen in: settled; one who arrives later: its fill starts at the change.
            var labels=new ExpeditionLabels();var m=new NativePartyLoadModel();m.Build(View("nova",held:false),1,true,9,50,labels);
            Check(m.Rows.Take(4).All(x=>x.StateAge>=NativePartyLoadModel.Settled-1e-3f&&x.Age==0),"first sight: settled states, entrances from now");
            m.Build(View("nova",held:false),1,true,9,50.5f,labels);Check(Math.Abs(m.Rows[3].Age-.5f)<1e-4f,"entrance age runs");
            m.Build(View("nova",held:false),1,true,10,51,labels);Check(m.Rows[0].Age==0,"a new load restarts the entrances");
        }

        // ---- per frame, nothing allocates ---------------------------------------------------------------------------------------------
        private static void Allocation()
        {
            var labels=new ExpeditionLabels();var m=new NativePartyLoadModel();
            Action<ExpeditionViewMember> split=x=>{if(x.account!="kestrel")x.pendingArea=Station;if(x.account=="echo")x.connected=false;};
            var views=new[]{View("nova",n:8),View("kestrel",n:4,held:false),View("nova",n:8,target:Station,ready:3,required:7,edit:split),View("astra",n:1),View("vex",n:6,entered:true)};
            float sink=0;var ui=NativeUiMotion.Measure(3440,1440,1);
            for(int warm=0;warm<2;warm++)
            {
                long before=GC.GetAllocatedBytesForCurrentThread();
                for(int f=0;f<600;f++)
                {
                    var v=views[f%views.Length];m.Build(v,f/600f,f%7==0,f/120,f/60f,labels);
                    for(int i=0;i<m.Count;i++){var r=m.Rows[i];sink+=NativePartyLoadModel.Entrance(r,false)+NativePartyLoadModel.ArrivalFill(r,false)+NativePartyLoadModel.TickScale(r,f%2==0)+NativePartyLoadModel.Sweep(f/60f,i,false)+r.Chip.Length+r.Name.Length+(r.Place?.Length??0);}
                    sink+=m.Footer.Length+(m.Aggregate?.Length??0)+m.Header.Length;
                    var l=NativeLoadingLayout.Measure(ui.Width,ui.Height,ui.SafeX,ui.SafeWidth,true,1+f%8);sink+=l.Roster.Box.H+l.Roster.RowBox(f%8).Y;
                    sink+=NativeStageLabels.Percent(f/600f).Length+(NativePartyLoadModel.More(f%8)?.Length??0);
                }
                long used=GC.GetAllocatedBytesForCurrentThread()-before;if(warm==1)Check(used==0,"the panel's model, layout and words allocate nothing per frame ("+used+" bytes)");
            }
            Check(!float.IsNaN(sink),"sink");
        }

        // ---- wiring: an Expedition load only; presentation only ---------------------------------------------------------------------
        private static string Body(string text,string signature)
        {
            int i=text.IndexOf(signature,StringComparison.Ordinal);if(i<0)return null;int b=text.IndexOf('{',i);if(b<0)return null;int depth=0;
            for(int j=b;j<text.Length;j++){if(text[j]=='{')depth++;else if(text[j]=='}'&&--depth==0)return text.Substring(i,j-i+1);}return null;
        }
        private static string Code(string text)=>string.Join("\n",text.Split('\n').Where(l=>!l.TrimStart().StartsWith("//")));
        private static void Wiring(Func<string,string> R,string root)
        {
            var ui=R("Assets/ChipArena/Runtime/NativeStageLoadingUI.cs");var gui=Body(ui,"private void StageLoadingGui()");
            Check(gui!=null&&gui.Contains(",waiting,PartyLoadingRoster());")&&!Regex.IsMatch(gui,"\\+\\s*\"|\"\\s*\\+|\\$\"|ToString\\("),"the loading screen's GUI pass hands the panel to the screen, building no strings");
            var hud=R("Assets/ChipArena/Runtime/NativeStageLoadingHud.cs");var draw=Body(hud,"public static void Draw(NativeStageLoading load");
            Check(draw.Contains("NativePartyLoadModel roster=null")&&draw.Contains("NativeLoadingLayout.Measure(W,Hh,U.SafeX,U.SafeW,v.Kind!=StageLoadKind.Return,roster!=null?roster.Count:0)"),"the screen lays the panel out with the title block");
            Check(draw.Contains("if(roster!=null)NativePartyLoadingHud.Draw(l0.Roster,roster,a,now,reduced);else if(waiting&&party!=null)Party("),"the panel replaces the pips for an Expedition load; PvP/PvE keep the pilots' count");
            var title=Body(hud,"private static void TitleBlock(");Check(title.Contains("!l.TagHidden")&&title.Contains("Mathf.Min(v.TitleSize,l.TitleSize)"),"the title block honours the panel's room");
            var wire=R("Assets/ChipArena/Runtime/NativePartyLoadingUI.cs");var roster=Body(wire,"private NativePartyLoadModel PartyLoadingRoster()");
            Check(roster.Contains("var v=LiveExpedition;")&&roster.Contains("k!=StageLoadKind.Expedition&&k!=StageLoadKind.Area")&&roster.Contains("partyLoad.Build(v,stageLoad.Shown,stageReadiness.Ready,stageLoad.Serial,Time.unscaledTime,partyLoadLabels)"),"an Expedition start or area load only, from the live view, my shown progress and readiness");
            Check(wire.Contains("internal float StageLoadProgress=>stageReadiness.Ready?1:stageLoad.Visible?stageLoad.Shown:-1;"),"my own progress exposed for the relay contract");
            // Presentation only: the panel's files send, decide and write nothing.
            var core=Code(R("Assets/ChipArena/Core/NativePartyLoading.cs"));var draws=Code(R("Assets/ChipArena/Runtime/NativePartyLoadingHud.cs"));
            foreach(var (name,text) in new[]{("NativePartyLoading.cs",core),("NativePartyLoadingHud.cs",draws),("NativePartyLoadingUI.cs",Code(wire))})
            {
                foreach(var api in new[]{"Send(","SendServer(","SendEncoded(","Acknowledge(","ReportExpeditionAreaLoaded(","CompleteAreaLoad(","NativeNetCodec","splitCohorts","expeditionLoading","StageGate.","Execute(","HoldMembers(","MoveMembers("})
                    Check(!text.Contains(api),name+" calls no authority or network API ("+api+")");
                Check(!Regex.IsMatch(text,@"\b(m|v|member|self)\.(account|name|weapon|area|pendingArea|connected|extracted|seat|host|areaBarrier|members|loading)\s*=[^=]"),name+" writes nothing into the view");
            }
            Check(!Regex.IsMatch(core,"\\bthrow\\b"),"the model never throws on a view");
            // The authority and network files do not know the panel exists.
            foreach(var f in new[]{"Runtime/NativeNetwork.cs","Runtime/NativeExpeditionNetwork.cs","Runtime/NativeExpeditionSplitRuntime.cs","Runtime/NativeStageGateNetwork.cs","Runtime/NativeExpeditionM1.cs","Runtime/NativeConnectionReport.cs",
                "Core/ExpeditionCohorts.cs","Core/ExpeditionAreaProjection.cs","Core/NativeExpeditionView.cs","Core/NativeExpeditionWire.cs","Core/NativeExpeditionParty.cs","Core/NativeExpedition.cs","Core/NativeExpeditionDirectorState.cs"})
            {
                var text=R("Assets/ChipArena/"+f);Check(!text.Contains("PartyLoad")&&!text.Contains("NativePartyLayout")&&!text.Contains("StageLoadProgress"),f+" is untouched by the party panel");
            }
            Check(!Directory.GetFiles(Path.Combine(root,"Assets/ChipArena"),"*.cs",SearchOption.AllDirectories).Where(f=>!f.Contains("NativePartyLoading")&&!f.EndsWith("NativeStageLoading.cs")&&!f.EndsWith("NativeStageLoadingHud.cs")&&!f.EndsWith("NativeStageLoadingUI.cs")).Any(f=>File.ReadAllText(f).Contains("NativePartyLoad")),"only the loading screen's files use the panel");
            // Draw paths: constants only (no concatenation, formatting, arrays or lists per frame).
            foreach(var f in new[]{"NativePartyLoadingHud.cs","NativePartyLoadingUI.cs"})
            {
                var code=Code(R("Assets/ChipArena/Runtime/"+f));
                Check(!Regex.IsMatch(code,"\\+\\s*\"|\"\\s*\\+|\\$\"|ToString\\(|string\\.Format|new List|Substring\\(|new\\[\\]|\\.Select\\(|\\.Where\\("),f+" builds no strings, arrays or queries per frame");
            }
            // Reduced effects: every clock-driven motion in the panel's draw goes through a reduced-aware helper.
            foreach(var line in draws.Split('\n').Where(l=>Regex.IsMatch(l,@"\bnow\s*\*")))Check(line.Contains("reduced?"),"a time-driven motion respects reduced effects: "+line.Trim());
            Check(draws.Contains("P.Entrance(p,reduced)")&&draws.Contains("P.ArrivalFill(p,reduced)")&&draws.Contains("P.TickScale(p,reduced)")&&draws.Contains("P.Sweep(now,p.Order,reduced)"),"the draw samples the model's motion with reduced effects");
            Check(draws.Contains("NativeIcons.Spec(p.Weapon)")&&draws.Contains("NativeStageLabels.Percent(p.Progress)"),"the weapon icon from the icon set, the cached percentages");
            // Metas: every new file has one, with a guid of its own.
            var guids=new HashSet<string>();
            foreach(var m in Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories)){var g=Regex.Match(File.ReadAllText(m),"guid: ([0-9a-f]{32})");if(g.Success)Check(guids.Add(g.Groups[1].Value),"unique guid "+m);}
            foreach(var f in new[]{"Core/NativePartyLoading.cs","Runtime/NativePartyLoadingHud.cs","Runtime/NativePartyLoadingUI.cs"})Check(File.Exists(Path.Combine(root,"Assets/ChipArena/"+f+".meta")),f+" has its meta");
        }
    }
}
#endif
