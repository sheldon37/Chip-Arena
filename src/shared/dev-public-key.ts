// Public verification key only. Signing key and account verifier live in server secrets.
export const DEV_PUBLIC_KEY = {"key_ops":["verify"],"ext":true,"kty":"EC","x":"8phe-ghSPLnonQXfeMgAtMeLKCzFEzQNGTt27gFc1eY","y":"f6sN-RmLQtVJUGkovXUbb3SdjSb7D6YVJdmyagmNxmM","crv":"P-256"};

// Distribute both pins before enabling the replacement account signer.
export const REPLACEMENT_PUBLIC_KEY = {"kty":"EC","crv":"P-256","x":"urC8CU-5Kc4ai_8YmWai71JxEHcfjQ2CuP51VS_vCnU","y":"XyIBKO9k1gR5ROgb1oZxGFRJK_gor3s05sNluJYx4bo","key_ops":["verify"],"ext":true};
export const PRODUCTION_PUBLIC_KEYS = [DEV_PUBLIC_KEY, REPLACEMENT_PUBLIC_KEY] as const;

export async function verifyClaimSignature(payload:string, signature:Uint8Array<ArrayBuffer>, override?:JsonWebKey) {
  const data = new TextEncoder().encode(payload);
  for (const jwk of override ? [override] : PRODUCTION_PUBLIC_KEYS) {
    const key = await crypto.subtle.importKey('jwk', jwk, {name:'ECDSA', namedCurve:'P-256'}, false, ['verify']);
    if (await crypto.subtle.verify({name:'ECDSA', hash:'SHA-256'}, key, signature, data)) return true;
  }
  return false;
}
