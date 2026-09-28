import {AUTH_ORIGIN} from '../shared/dev-auth.js';
import {verifyPlayerToken} from '../shared/account-auth.js';
import type {Access} from '../shared/access.js';
import type {Cosmetics} from '../shared/cosmetics.js';
import type {ClassId} from '../shared/config.js';
export interface Account extends Access {id:string;username:string;profile:{name:string;class:ClassId;color:string;cosmetics:Cosmetics};hasProfile:boolean;revision:number;grantVersion:number;}
export class PlayerAccount {
 session=sessionStorage.getItem('chip-arena-account-session')||'';account:Account|null=null;token='';recoveryCode='';
 async request(action:string,body?:unknown){const r=await fetch(AUTH_ORIGIN+'/api/accounts/'+action,{method:body===undefined?'GET':'POST',credentials:'omit',redirect:'error',cache:'no-store',signal:AbortSignal.timeout(action==='me'?4500:20000),headers:{'Content-Type':'application/json',...(this.session?{Authorization:'Bearer '+this.session}:{})},...(body===undefined?{}:{body:JSON.stringify(body)})});let data:any;try{data=await r.json();}catch{throw Error('Account service is unavailable. Try again shortly.');}if(!r.ok)throw Object.assign(Error(data.error||'Account request failed.'),{status:r.status});return data;}
 async accept(data:any){const claim=await verifyPlayerToken(data.token);if(!claim.accountId||claim.accountId!==data.account?.id)throw Error('Account response could not be verified.');this.account=data.account;this.token=data.token;if(data.session){this.session=data.session;sessionStorage.setItem('chip-arena-account-session',data.session);}this.recoveryCode=data.recoveryCode||'';return this.account!;}
 async login(action:'login'|'register'|'recover',username:string,password:string,recoveryCode?:string){return this.accept(await this.request(action,{username,password,recoveryCode}));}
 async refresh(){if(!this.session)return null;try{return await this.accept(await this.request('me'));}catch(e:any){if(e.status===401)this.clear();throw e;}}
 async save(profile:Account['profile']){if(!this.account)return null;return this.accept(await this.request('profile',{revision:this.account.revision,profile}));}
 async logout(){if(this.session)await this.request('logout',{});this.clear();}
 clear(){this.session='';this.account=null;this.token='';this.recoveryCode='';sessionStorage.removeItem('chip-arena-account-session');}
}
export const accounts=new PlayerAccount();
