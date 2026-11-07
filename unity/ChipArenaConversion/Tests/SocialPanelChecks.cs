using System;
using System.IO;

// The Online Players chip and panel (Runtime/NativeSocialPanelUI.cs; owner 2026-11-03: "can we fix the online players button?
// (Used for inviting) Style it a lot better and apply animations"): kit-drawn on the virtual canvas (no fixed physical-pixel
// button, no default IMGUI skin), placed clear of the home screen's hexagons and the lobby's connection pill, animated, its
// actions the account service's own (NativeAccountSocial.cs keeps the logic only); Escape closes it before the menu's Back,
// and the menu under it is blocked while the pointer is on it.
internal static class SocialPanelChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Social panel: "+what);count++;}
    public static void Run(string root)
    {
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        string ui=R("NativeSocialPanelUI.cs"),logic=R("NativeAccountSocial.cs"),game=R("ConversionGame.cs"),home=R("NativeHomeMenu.cs"),lobby=R("NativeLobbyUI.cs");
        Check(!ui.Contains("GUI.Box(")&&!ui.Contains("GUILayout.")&&!ui.Contains("Screen.width")&&!ui.Contains("GUI.Button("),"kit-drawn on the virtual canvas: no default skin, no physical-pixel placement");
        Check(!logic.Contains("partial class ConversionGame")&&logic.Contains("public void InvitePlayer(")&&logic.Contains("public void AnswerInvite("),"the account service's file keeps the logic only");
        Check(ui.Contains("account.InvitePlayer(p.id,roomCode)")&&ui.Contains("account.AnswerInvite(inv.id,true,code=>{if(!CanAcceptInvite)return;internetRooms=true;relayOverride=\"\";joinCode=code;")&&ui.Contains("JoinRoom();")&&ui.Contains("account.AnswerInvite(inv.id,false,null)"),
            "Invite, Accept (joining by the code through the trusted path) and Decline are the existing actions");
        Check(ui.Contains("relayHost!=null&&!relayHost.Closed&&!UseManagedRelay&&arena==null&&!deploymentLocked")&&ui.Contains("arena==null&&!Hosting&&server==null&&connecting==null&&relayConnecting==null&&!StageInputHeld"),
            "who may invite and accept is unchanged");
        Check(ui.Contains("menuScreen==MenuScreen.Home)return new Vector2(U.W-HomeX+8,108)")&&ui.Contains("ConnectionPillWidth(ConnectionPillText(out _))")&&lobby.Contains("float w=ConnectionPillWidth(text);"),
            "placed under the hexagon row at home and left of the lobby's connection pill");
        Check(ui.Contains("U.PanelIn(now-socialOpenAt)")&&ui.Contains("SocialRowIn(")&&ui.Contains("Mathf.Exp(-beat*7)")&&ui.Contains("180*open")&&!ui.Contains(".92f+.08f*open"),
            "animated: the panel ease, staggered rows, the invitations' beat, the turning chevron; slid, never squashed");
        int esc=game.IndexOf("if(escape&&SocialEscape())return;",StringComparison.Ordinal),back=esc<0?-1:game.IndexOf("if(escape){if(arena==null)MenuBack();",esc,StringComparison.Ordinal);
        Check(esc>=0&&back>esc,"Escape closes the open panel before the menu's Back");
        Check(home.Contains("private bool MenuBlocked()=>menuBlockedNow||SocialCovers();"),"the menu under the chip and panel is blocked while the pointer is on them");
        // The pop-up (owner 2026-11-07: "a notification when players get invited", "Pop-up with Accept").
        Check(ui.Contains("(menu||arena!=null&&(match.Phase==NativePhase.Match&&arena.Pve==null||ExpeditionEndingShowing))"),"the pop-up shows on the menus and at a match's end, never in a fight");
        Check(ui.Contains("private void SocialAccept(NativePlayerInvite inv)=>account.AnswerInvite(")&&ui.Contains("\"ACCEPT\",\"check\",0,canAccept&&!busy&&usable,a,9705)){socialToastJoining=inv.id;SocialAccept(inv);}")&&ui.Contains("canAccept&&!busy&&usable,a,hid*3+1))SocialAccept(inv);"),"the pop-up's Accept and the panel's are one path, under the one rule");
        Check(ui.Contains("if(usable&&!joining&&U.Button(x))socialToastGone.Add(inv.id);")&&!ui.Contains("U.Button(x)){account.AnswerInvite"),"closing the pop-up answers nothing: the invitation stays on the badge");
        Check(ui.Contains("if(socialOpen)socialToastGone.Add(i.id);")&&ui.Contains("U.Hover(socialToastRect)")&&ui.Contains("couldn't be accepted"),"what the panel has shown doesn't pop up again, the menu under the card is blocked, and a failed Accept says so");
        Check(logic.Contains("private void TickSocial(){if(Account!=null&&session.Length>0&&!SocialBusy&&Time.unscaledTime>=socialNext)SocialRequest(\"social\",\"{}\");}"),"the account service's polling is unchanged");
        Console.WriteLine("PASS social panel ("+count+" checks)");
    }
}
