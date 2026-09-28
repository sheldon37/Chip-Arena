import {AUTH_ORIGIN} from '../shared/dev-auth.js';
import {verifyPlayerToken} from '../shared/account-auth.js';
import type {Access} from '../shared/access.js';
import type {Cosmetics} from '../shared/cosmetics.js';
import type {ClassId} from '../shared/config.js';
export interface Account extends Access {id:string;username:string;profile:{name:string;class:ClassId;color:string;cosmetics:Cosmetics};hasProfile:boolean;revision:number;grantVersion:number;}
const SESSION_KEY='chip-arena-account-session';
const desktop=typeof window!=='undefined'?(window as any).arenaAccount as {request:(action:string,body?:unknown)=>Promise<{ok:boolean;status:number;data:any}>}|undefined:undefined;
export class PlayerAccount {
 session=desktop?'desktop':localStorage.getItem(SESSION_KEY)||sessionStorage.getItem(SESSION_KEY)||'';remember=localStorage.getItem('chip-arena-remember')!=='false';account:Account|null=null;token='';recoveryCode='';
 async request(action:string,body?:unknown){if(desktop){const r=await desktop.request(action,body);if(!r.ok)throw Object.assign(Error(r.data.error||'Account request failed.'),{status:r.status});return r.data;}const r=await fetch(AUTH_ORIGIN+'/api/accounts/'+action,{method:body===undefined?'GET':'POST',credentials:'omit',redirect:'error',cache:'no-store',signal:AbortSignal.timeout(action==='me'?4500:20000),headers:{'Content-Type':'application/json',...(this.session?{Authorization:'Bearer '+this.session}:{})},...(body===undefined?{}:{body:JSON.stringify(body)})});let data:any;try{data=await r.json();}catch{throw Error('Account service is unavailable. Try again shortly.');}if(!r.ok)throw Object.assign(Error(data.error||'Account request failed.'),{status:r.status});return data;}
 async accept(data:any){const claim=await verifyPlayerToken(data.token);if(!claim.accountId||claim.accountId!==data.account?.id)throw Error('Account response could not be verified.');this.account=data.account;this.token=data.token;if(data.desktopSession)this.session='desktop';if(data.session){this.session=data.session;localStorage.removeItem(SESSION_KEY);sessionStorage.removeItem(SESSION_KEY);(this.remember?localStorage:sessionStorage).setItem(SESSION_KEY,data.session);}this.recoveryCode=data.recoveryCode||'';return this.account!;}
 async login(action:'login'|'register'|'recover',username:string,password:string,recoveryCode?:string,remember=true){this.remember=remember;localStorage.setItem('chip-arena-remember',String(remember));return this.accept(await this.request(action,{username,password,recoveryCode,remember}));}
 async refresh(){if(!this.session&&!desktop)return null;try{return await this.accept(await this.request('me'));}catch(e:any){if(e.status===401)this.clear();throw e;}}
 async save(profile:Account['profile']){if(!this.account)return null;return this.accept(await this.request('profile',{revision:this.account.revision,profile}));}
 async logout(){try{if(this.session||desktop)await this.request('logout',{});}finally{this.clear();}}
 clear(){this.session='';this.account=null;this.token='';this.recoveryCode='';localStorage.removeItem(SESSION_KEY);sessionStorage.removeItem(SESSION_KEY);}
}
export const accounts=new PlayerAccount();
