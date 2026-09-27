/** All clients sample the same death age; audio accents share these pose peaks. */
export const CLOWN_LOVE={danceStart:1.05,beats:[1.3,1.7,2.1,2.5,2.9,3.3],carryStart:3.65,end:5} as const;
export function loveDip(age:number){return CLOWN_LOVE.beats.reduce((best,at)=>{const distance=Math.abs(age-at);return Math.max(best,distance<.18?(1+Math.cos(Math.PI*distance/.18))/2:0);},0);}
