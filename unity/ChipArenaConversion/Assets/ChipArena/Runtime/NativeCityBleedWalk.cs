#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The bleed on the Developer walk (docs/MERIDIAN-BLEED.md): the walk's open and close each make one call here, and the
    /// walk's door-deal picker lives here too. Presentation only: the picker chooses which three instances the walk's hall deal
    /// (NativeCityLayout.DealHalls, unchanged) puts behind the east, south and west doors, by the run seed that deals them so,
    /// and re-opens the walk on it, so every drawer rebuilds from the new deal.
    ///  - The layout menu: a row per door; each press turns that door to the next instance the other two doors do not hold.
    ///    "As dealt" goes back to the walk's own deal (Ward Zero, Central Station and City Ironworks, in the seed's order).
    ///  - In the walk: N deals the next of five deals that put every instance behind every door; B turns the bleed off and on
    ///    (the greybox against the dressing, for comparing).
    /// </summary>
    public sealed partial class ConversionGame
    {
        private NativeCityBleedDraw bleedDraw;
        private NativeCityBleed walkBleed;
        private NativeCityBleedDress.Plan walkBleedPlan;
        private string[] walkDeal={"Ward Zero","Central Station","City Ironworks"};
        private bool walkDealChosen,walkBleedOff;
        private int walkDealTurn;
        private uint walkBleedSeed;
        // Five deals in turn: every instance behind every door.
        private static readonly string[][] WalkDealTurns=
        {
            new[]{"Level 0","The Poolrooms","Ward Zero"},new[]{"The Poolrooms","Ward Zero","Central Station"},
            new[]{"Ward Zero","Central Station","City Ironworks"},new[]{"Central Station","City Ironworks","Level 0"},
            new[]{"City Ironworks","Level 0","The Poolrooms"},
        };

        /// <summary>The walk's hall deal for a run seed: the walk's own three in the seed's order, or the picker's choice per
        /// door (by the first seed from this one that deals them so).</summary>
        private string[] WalkDeal(uint seed)
        {
            if(walkDealChosen)seed=NativeCityBleedDress.DealSeedFor(seed,walkDeal);
            walkBleedSeed=seed;
            return NativeCityLayout.DealHalls(seed,walkDeal);
        }

        /// <summary>The walk opened: its bleed, its approach dressing's plan and the drawer (none without the bleed's bank).</summary>
        private void OpenCityBleed()
        {
            CloseCityBleed();
            if(cityWalk==null||surveySlots==null)return;
            try
            {
                walkBleed=NativeCityBleed.ForWalk(cityWalk,surveySlots);
                walkBleedPlan=NativeCityBleedDress.ForWalk(cityWalk,walkBleed,walkBleedSeed);
                if(!walkBleedOff)bleedDraw=NativeCityBleedDraw.Open(walkBleed,walkBleedPlan,cityWalk.Map,cameraView,()=>presentation!=null&&presentation.LowGraphics);
                // the street dress's three contracts (D1, planned right after this on the walk's open): its modules take the bleed's
                // themes, and where the bleed draws, its lamps and its skins stand in for the street's own
                if(bleedDraw!=null)
                {
                    var bleed=walkBleed;var plan=walkBleedPlan;
                    NativeMeridianDress.ThemeAt=p=>bleed.ThemeAt(p);
                    NativeMeridianDress.LampsReplaced=p=>bleed.ReplacesLamps(p)&&NativeCityBleedDress.NearLamp(plan,p,14);
                    NativeMeridianDress.FacadeCovered=(p,n)=>NativeCityBleedDress.Covers(plan,p,n);
                }
            }
            catch(Exception e){Debug.LogWarning("City bleed: "+e.Message);}
        }

        /// <summary>The walk closed (or re-dealt): the drawer goes and the atmosphere comes back.</summary>
        private void CloseCityBleed()
        {
            if(bleedDraw!=null)bleedDraw.Close();
            bleedDraw=null;walkBleed=null;walkBleedPlan=null;
            NativeMeridianDress.ThemeAt=null;NativeMeridianDress.LampsReplaced=null;NativeMeridianDress.FacadeCovered=null;
        }

        /// <summary>The walk's keys: N the next deal, B the bleed off or on. True when one was used.</summary>
        private bool WalkDealKeys()
        {
            if(cityWalk==null)return false;
            if(Input.GetKeyDown(KeyCode.N))
            {
                walkDealTurn=(walkDealTurn+1)%WalkDealTurns.Length;walkDeal=(string[])WalkDealTurns[walkDealTurn].Clone();walkDealChosen=true;
                ReopenCityWalk();return true;
            }
            if(Input.GetKeyDown(KeyCode.B))
            {
                // the street dress is planned with or without the bleed, so the walk opens again (every drawer rebuilds)
                walkBleedOff=!walkBleedOff;ReopenCityWalk();
                return true;
            }
            return false;
        }

        // The same layout and run seed, dealt again: every drawer rebuilds on the walk's open.
        private void ReopenCityWalk()
        {
            int layout=lastSurveyLayout;uint seed=surveyRunSeed;if(layout<0)return;
            CloseCityWalk();OpenCityWalk(layout,seed);
        }

        /// <summary>The walk's line: its deal's keys and the bleed at the pilot's place.</summary>
        private void WalkDealLabel()
        {
            string at="";
            if(walkBleed!=null&&arena?.Local!=null)
            {
                var p=arena.Local.Body.position;var s=walkBleed.At(new V3(p.x,p.y,p.z));
                at=s.Door<0?"   Plain Meridian":"   "+walkBleed.Doors[s.Door].Instance+" "+s.Band+" "+s.Share.ToString("0.00");
            }
            GUILayout.Label("N  Next door deal   B  Bleed "+(walkBleedOff?"off":bleedDraw!=null?"on":"(no bank)")+at);
        }

        /// <summary>The layout menu's door-deal rows (above the layouts: the next walk opened takes this deal).</summary>
        private void WalkDealGui()
        {
            string[] doors={"East","South","West"};
            for(int d=0;d<3;d++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(doors[d],GUILayout.Width(60));
                if(GUILayout.Button(walkDealChosen?walkDeal[d]:walkDeal[d]+" (as dealt)",button))
                {
                    // the next instance the other two doors do not hold
                    int at=Array.IndexOf(NativeCityBleedDress.Instances,walkDeal[d]);
                    for(int k=1;k<=NativeCityBleedDress.Instances.Length;k++)
                    {
                        var next=NativeCityBleedDress.Instances[(at+k)%NativeCityBleedDress.Instances.Length];
                        if(Array.IndexOf(walkDeal,next)<0){walkDeal[d]=next;break;}
                    }
                    walkDealChosen=true;
                }
                GUILayout.EndHorizontal();
            }
            if(walkDealChosen&&GUILayout.Button("Doors as dealt")){walkDeal=new[]{"Ward Zero","Central Station","City Ironworks"};walkDealChosen=false;}
        }
    }
}
#endif
