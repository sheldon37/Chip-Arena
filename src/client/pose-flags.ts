/** Per-rig presentation flags. Client-only, never serialized or fed into simulation, prediction or input.
 * legsAuthored: an action layer placed the feet this sample (rig-ik foot() marks it; sampleCharacter clears it first),
 * so a later ground pass must not re-solve the legs.
 * legsLayered: a layer posed the legs as offsets from this sample's gait targets that blend back to them (the DEV fighting
 * styles' heel pivots, kicks and lunges); the ground pass re-solves them in place but keeps the gait's own foot state.
 * firstPerson: the camera-attached view-model rig (the renderer marks it once at construction). */
type FlaggedRig=object;
const legs=new WeakSet<FlaggedRig>(),layered=new WeakSet<FlaggedRig>(),firstPerson=new WeakSet<FlaggedRig>();
export function markLegsAuthored(r:FlaggedRig){legs.add(r);}
export function legsAuthored(r:FlaggedRig){return legs.has(r);}
export function clearLegsAuthored(r:FlaggedRig){legs.delete(r);layered.delete(r);}
export function markLegsLayered(r:FlaggedRig){layered.add(r);}
export function legsLayered(r:FlaggedRig){return layered.has(r);}
export function markFirstPersonRig(r:FlaggedRig,on=true){if(on)firstPerson.add(r);else firstPerson.delete(r);}
export function isFirstPersonRig(r:FlaggedRig){return firstPerson.has(r);}
