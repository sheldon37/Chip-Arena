/** The default is the published service until the owner authorizes cutover. */
export const LEGACY_ACCOUNT_ORIGIN='https://chip-arena-prototype.sheldon37.chatgpt.site';

export function accountOrigin(value?:string){
 if(!value)return LEGACY_ACCOUNT_ORIGIN;
 const u=new URL(value);
 const local=['localhost','127.0.0.1','[::1]'].includes(u.hostname);
 if(u.username||u.password||u.pathname!=='/'||u.search||u.hash||
    (u.protocol!=='https:'&&!(local&&u.protocol==='http:')))throw Error('Invalid account service origin.');
 return u.origin;
}
