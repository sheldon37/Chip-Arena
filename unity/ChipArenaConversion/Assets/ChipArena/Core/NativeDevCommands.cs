using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeDevCommand
    {public string transaction,action;public int target;public string[] args=Array.Empty<string>();}
    [Serializable] public sealed class NativeDevReply
    {public string transaction,state,reason;}
    // These DTOs contain no claimed identity or permission. Only the host's verified seat grants authority.
    public static class NativeDevRules
    {
        public static bool DevAllowed(NativeSeat seat,double utc)=>seat?.access?.Developer==true&&seat.access.Expires>utc;
        public static readonly string[] Actions={"restore","down","god","infinite-ammo","no-cooldowns","chip-add","chip-remove","weapon","teleport","spawn-dummy","clear-dummies","launch","freeze","bring","send","swap-weapon","shrink","grow","debris-rain","hazard-at","spawn-dummies-around","time-scale","arena-reset","round-end","hazard-trigger","debris","reveal"};
        public static bool Number(string text,float min,float max,out float value)=>float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&!float.IsNaN(value)&&!float.IsInfinity(value)&&value>=min&&value<=max;
        public static bool Integer(string text,int min,int max)=>int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var n)&&n>=min&&n<=max;
        public static string Validate(NativeDevCommand c)
        {
            if(c==null)return "Missing command.";
            if(string.IsNullOrEmpty(c.transaction)||c.transaction.Length>64||c.transaction.Any(ch=>!char.IsLetterOrDigit(ch)&&ch!='-'&&ch!='_'))return "Invalid transaction.";
            if(c.target<0)return "Invalid target.";
            if(c.action==null||!Actions.Contains(c.action))return "Unknown DEV action.";
            var a=c.args;if(a==null||a.Length>4||a.Any(v=>string.IsNullOrEmpty(v)||v.Length>64||v.Any(char.IsControl)))return "Invalid arguments.";
            bool N(int i,float min,float max)=>i<a.Length&&Number(a[i],min,max,out _);
            bool I(int i,int min,int max)=>i<a.Length&&Integer(a[i],min,max);
            bool ok;
            switch(c.action)
            {
                case "god":case "infinite-ammo":case "no-cooldowns":ok=a.Length==1&&(a[0]=="on"||a[0]=="off");break;
                case "chip-add":case "chip-remove":ok=a.Length==2&&I(1,1,100);break;
                case "weapon":case "swap-weapon":case "debris-rain":case "hazard-trigger":ok=a.Length==1;break;
                case "teleport":case "send":ok=a.Length==3&&N(0,-1000,1000)&&N(1,-100,500)&&N(2,-1000,1000);break;
                case "launch":ok=a.Length==1&&N(0,1,100);break;
                case "freeze":ok=a.Length==1&&N(0,0,30);break;
                case "time-scale":ok=a.Length==1&&N(0,.1f,2);break;
                case "spawn-dummies-around":ok=a.Length==1&&I(0,1,7);break;
                case "spawn-dummy":ok=a.Length==3&&new[]{"ai","idle","primary","q","e","block","dash","jump","1","2","3","4","5"}.Contains(a[1])&&I(2,0,7);break;
                case "debris":ok=a.Length==4&&N(0,1,500)&&N(1,1,100)&&new[]{"face","edge","corner"}.Contains(a[3]);break;
                default:ok=a.Length==0;break;
            }
            return ok?null:"Arguments do not match this action.";
        }
    }
    // One instance per match. Entries never evict, so a delayed duplicate cannot execute again.
    public sealed class NativeDevCommands
    {
        private sealed class Record {public string Fingerprint;public NativeDevReply Reply;}
        private sealed class Budget {public double At;public double Tokens=8;}
        private readonly Dictionary<string,Record> records=new Dictionary<string,Record>();
        private readonly Dictionary<string,Budget> budgets=new Dictionary<string,Budget>();
        public NativeDevReply Run(NativeSeat sender,NativeDevCommand command,int epoch,int generation,int expectedEpoch,int expectedGeneration,double utc,double clock,Func<string> apply,Action accepted=null)
        {
            NativeDevReply Reply(string state,string reason)=>new NativeDevReply{transaction=command?.transaction?.Length<=64?command.transaction:"",state=state,reason=reason??""};
            if(!NativeDevRules.DevAllowed(sender,utc))return Reply("refused","A verified, unexpired developer grant is required.");
            if(!sender.connected||sender.bot)return Reply("refused","The developer seat is not connected.");
            string invalid=NativeDevRules.Validate(command);if(invalid!=null)return Reply("refused",invalid);
            string owner=sender.access.AccountId??("seat:"+sender.id),key=owner+":"+command.transaction;
            string fingerprint=epoch+"/"+generation+"/"+command.action+"/"+command.target+"/"+string.Join("|",command.args.Select(a=>a.Length+":"+a));
            if(records.TryGetValue(key,out var old))return old.Fingerprint==fingerprint?Reply(old.Reply.state,old.Reply.reason):Reply("refused","Transaction was already used for a different command.");
            if(!budgets.TryGetValue(owner,out var budget))budgets[owner]=budget=new Budget{At=clock};
            budget.Tokens=Math.Min(8,budget.Tokens+Math.Max(0,clock-budget.At)*4);budget.At=clock;
            if(records.Count>=8192)return Reply("refused","DEV command budget exhausted for this match.");
            if(budget.Tokens<1){var limited=Reply("refused","DEV command rate limit exceeded.");records[key]=new Record{Fingerprint=fingerprint,Reply=limited};return limited;}budget.Tokens--;
            NativeDevReply reply;
            if(epoch!=expectedEpoch||generation!=expectedGeneration)reply=Reply("refused","The match or arena revision changed.");
            else
            {
                // Reserve before invoking gameplay; exceptions cannot cause a retry to repeat partial effects.
                reply=Reply("refused","DEV action failed; inspect the host log.");records[key]=new Record{Fingerprint=fingerprint,Reply=reply};
                try {string reason=apply();reply=Reply(reason==null?"done":"refused",reason);if(reason==null)accepted?.Invoke();}
                catch(Exception){reply=Reply("refused","DEV action failed; inspect the host log.");}
            }
            records[key]=new Record{Fingerprint=fingerprint,Reply=reply};return reply;
        }
    }
}
