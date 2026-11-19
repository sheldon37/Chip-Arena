using System;
namespace ChipArena.UnityConversion
{
    // Each room mode's own weapon pick (owner 2026-11-19: "Its pulling chip selection logic from play vs bots (So if people select
    // Blind pick all or anything else, its changing expedition pick logic.) I want Blind Pick choice with 5 options by default for
    // expedition."). The player's blindPick and blindChoices preferences are the Play vs Bots and versus rooms' Weapon pick; before
    // this they also decided an expedition's and a boss battle's opening, and a co-op room's Blind Pick All box wrote them back.
    //
    // Now: a versus room reads its Weapon pick as before; an expedition always deals Blind Pick with its own choice count (5 by
    // default, the host's to change, carried to guests in the room options' own blindPick / blindChoices fields); a boss battle keeps
    // the lobby loadouts its screen offers. A change made in one mode is stored for that mode only, and switching modes stores
    // nothing. The deal, its privacy, the checks and the timer are Astra's rules (NativeOpeningDraft), unchanged: this only says which
    // pick a mode's opening asks them for, and how players read it. Presentation and wiring; no new field, message or save.
    public static class NativeRoomPick
    {
        /// <summary>An expedition's dealt choices until its host changes them.</summary>
        public const int ExpeditionDefault=5;
        public enum Mode{Versus,Boss,Expedition}
        /// <summary>A room's mode from its options (as CaptureRoomOptions: an expedition is a co-op room).</summary>
        public static Mode ModeOf(bool cooperative,bool expedition)=>!cooperative?Mode.Versus:expedition?Mode.Expedition:Mode.Boss;
        /// <summary>An expedition's count kept in range: at least the two the rules deal, at most what the screen lays out (the deal
        /// itself is capped by each pilot's pool, NativeOpeningDraft.OfferCount).</summary>
        public static int Count(int choices)=>Math.Max(NativeBlindChoices.Min,Math.Min(NativeBlindChoices.Max,choices));

        public readonly struct Pick
        {
            public readonly Mode Mode;public readonly bool Blind;public readonly int Choices;
            public Pick(Mode mode,bool blind,int choices){Mode=mode;Blind=blind;Choices=choices;}
            /// <summary>Dealt choices (Blind Pick) rather than the whole pool (Blind Pick All), as BeginOpening asked before.</summary>
            public bool Dealt=>Blind&&Choices!=0;
        }
        /// <summary>The pick a room's opening deals. `blindPick` / `blindChoices`: the Play vs Bots / versus Weapon pick (the player's
        /// preferences); `expeditionChoices`: the expedition's own count.</summary>
        public static Pick Of(Mode mode,bool blindPick,int blindChoices,int expeditionChoices)=>
            mode==Mode.Expedition?new Pick(mode,true,Count(expeditionChoices)):
            mode==Mode.Boss?new Pick(mode,false,0):
            new Pick(mode,blindPick,blindPick?blindChoices:0);

        /// <summary>The room's options as they are shown and broadcast: an expedition's pick (blindPick on, its own count) or a boss
        /// battle's (off) in the options' own fields; a versus room's left as the preferences hold it.</summary>
        public static NativeRoomOptions Apply(NativeRoomOptions o,int expeditionChoices)
        {
            if(o==null)return null;var mode=ModeOf(o.cooperative,o.expedition);
            if(mode==Mode.Expedition){o.blindPick=true;o.blindChoices=Count(expeditionChoices);}
            else if(mode==Mode.Boss){o.blindPick=false;o.blindChoices=0;}
            return o;
        }
        /// <summary>A rules change (SetRoomOptions) stored for its own mode: a versus room's Weapon pick into the preferences, an
        /// expedition's count into its own; nothing for a boss battle, and nothing when the change switches the mode (each mode keeps
        /// its own pick). `was`: the room's mode before the change.</summary>
        public static void Store(Mode was,NativeRoomOptions o,ref bool blindPick,ref int blindChoices,ref int expeditionChoices)
        {
            if(o==null)return;var now=ModeOf(o.cooperative,o.expedition);if(now!=was)return;
            if(now==Mode.Versus){blindPick=o.blindPick;blindChoices=o.blindChoices;}
            else if(now==Mode.Expedition&&o.blindPick&&o.blindChoices>=NativeBlindChoices.Min)expeditionChoices=Count(o.blindChoices);
        }

        // ---- how players read it --------------------------------------------------------------------------------------------
        /// <summary>The Expeditions screen's row, in place of the loadout: its label and its line.</summary>
        public const string RowLabel="WEAPON PICK  ·  DEALT AT DEPLOY";
        public static string RowLine(int choices)=>"BLIND PICK  ·  "+choices+" CHOICES";
        /// <summary>A squad card's line under the pilot's name.</summary>
        public static string CardLine(int choices)=>"BLIND PICK · "+choices;
        /// <summary>The drawer's and the room options' note.</summary>
        public static string Note(int choices)=>"An expedition is always Blind Pick. When the host presses Deploy at the Deployment Station, each pilot is dealt "+
            choices+" different weapons and classes and picks one for the run; the picks stay hidden until every pilot locks in.";
    }
}
