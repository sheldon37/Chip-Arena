// node_modules/@noble/hashes/utils.js
function isBytes(a) {
  return a instanceof Uint8Array || ArrayBuffer.isView(a) && a.constructor.name === "Uint8Array";
}
function anumber(n, title = "") {
  if (!Number.isSafeInteger(n) || n < 0) {
    const prefix = title && `"${title}" `;
    throw new Error(`${prefix}expected integer >= 0, got ${n}`);
  }
}
function abytes(value, length, title = "") {
  const bytes2 = isBytes(value);
  const len = value?.length;
  const needsLen = length !== void 0;
  if (!bytes2 || needsLen && len !== length) {
    const prefix = title && `"${title}" `;
    const ofLen = needsLen ? ` of length ${length}` : "";
    const got = bytes2 ? `length=${len}` : `type=${typeof value}`;
    throw new Error(prefix + "expected Uint8Array" + ofLen + ", got " + got);
  }
  return value;
}
function ahash(h) {
  if (typeof h !== "function" || typeof h.create !== "function")
    throw new Error("Hash must wrapped by utils.createHasher");
  anumber(h.outputLen);
  anumber(h.blockLen);
}
function aexists(instance, checkFinished = true) {
  if (instance.destroyed)
    throw new Error("Hash instance has been destroyed");
  if (checkFinished && instance.finished)
    throw new Error("Hash#digest() has already been called");
}
function aoutput(out, instance) {
  abytes(out, void 0, "digestInto() output");
  const min = instance.outputLen;
  if (out.length < min) {
    throw new Error('"digestInto() output" expected to be of length >=' + min);
  }
}
function u32(arr) {
  return new Uint32Array(arr.buffer, arr.byteOffset, Math.floor(arr.byteLength / 4));
}
function clean(...arrays) {
  for (let i = 0; i < arrays.length; i++) {
    arrays[i].fill(0);
  }
}
function createView(arr) {
  return new DataView(arr.buffer, arr.byteOffset, arr.byteLength);
}
function rotr(word, shift) {
  return word << 32 - shift | word >>> shift;
}
function rotl(word, shift) {
  return word << shift | word >>> 32 - shift >>> 0;
}
var isLE = /* @__PURE__ */ (() => new Uint8Array(new Uint32Array([287454020]).buffer)[0] === 68)();
function byteSwap(word) {
  return word << 24 & 4278190080 | word << 8 & 16711680 | word >>> 8 & 65280 | word >>> 24 & 255;
}
function byteSwap32(arr) {
  for (let i = 0; i < arr.length; i++) {
    arr[i] = byteSwap(arr[i]);
  }
  return arr;
}
var swap32IfBE = isLE ? (u) => u : byteSwap32;
var nextTick = async () => {
};
async function asyncLoop(iters, tick, cb) {
  let ts = Date.now();
  for (let i = 0; i < iters; i++) {
    cb(i);
    const diff = Date.now() - ts;
    if (diff >= 0 && diff < tick)
      continue;
    await nextTick();
    ts += diff;
  }
}
function utf8ToBytes(str) {
  if (typeof str !== "string")
    throw new Error("string expected");
  return new Uint8Array(new TextEncoder().encode(str));
}
function kdfInputToBytes(data, errorTitle = "") {
  if (typeof data === "string")
    return utf8ToBytes(data);
  return abytes(data, void 0, errorTitle);
}
function checkOpts(defaults, opts) {
  if (opts !== void 0 && {}.toString.call(opts) !== "[object Object]")
    throw new Error("options must be object or undefined");
  const merged = Object.assign(defaults, opts);
  return merged;
}
function createHasher(hashCons, info = {}) {
  const hashC = (msg, opts) => hashCons(opts).update(msg).digest();
  const tmp = hashCons(void 0);
  hashC.outputLen = tmp.outputLen;
  hashC.blockLen = tmp.blockLen;
  hashC.create = (opts) => hashCons(opts);
  Object.assign(hashC, info);
  return Object.freeze(hashC);
}
var oidNist = (suffix) => ({
  oid: Uint8Array.from([6, 9, 96, 134, 72, 1, 101, 3, 4, 2, suffix])
});

// node_modules/@noble/hashes/hmac.js
var _HMAC = class {
  oHash;
  iHash;
  blockLen;
  outputLen;
  finished = false;
  destroyed = false;
  constructor(hash4, key) {
    ahash(hash4);
    abytes(key, void 0, "key");
    this.iHash = hash4.create();
    if (typeof this.iHash.update !== "function")
      throw new Error("Expected instance of class which extends utils.Hash");
    this.blockLen = this.iHash.blockLen;
    this.outputLen = this.iHash.outputLen;
    const blockLen = this.blockLen;
    const pad = new Uint8Array(blockLen);
    pad.set(key.length > blockLen ? hash4.create().update(key).digest() : key);
    for (let i = 0; i < pad.length; i++)
      pad[i] ^= 54;
    this.iHash.update(pad);
    this.oHash = hash4.create();
    for (let i = 0; i < pad.length; i++)
      pad[i] ^= 54 ^ 92;
    this.oHash.update(pad);
    clean(pad);
  }
  update(buf) {
    aexists(this);
    this.iHash.update(buf);
    return this;
  }
  digestInto(out) {
    aexists(this);
    abytes(out, this.outputLen, "output");
    this.finished = true;
    this.iHash.digestInto(out);
    this.oHash.update(out);
    this.oHash.digestInto(out);
    this.destroy();
  }
  digest() {
    const out = new Uint8Array(this.oHash.outputLen);
    this.digestInto(out);
    return out;
  }
  _cloneInto(to) {
    to ||= Object.create(Object.getPrototypeOf(this), {});
    const { oHash, iHash, finished, destroyed, blockLen, outputLen } = this;
    to = to;
    to.finished = finished;
    to.destroyed = destroyed;
    to.blockLen = blockLen;
    to.outputLen = outputLen;
    to.oHash = oHash._cloneInto(to.oHash);
    to.iHash = iHash._cloneInto(to.iHash);
    return to;
  }
  clone() {
    return this._cloneInto();
  }
  destroy() {
    this.destroyed = true;
    this.oHash.destroy();
    this.iHash.destroy();
  }
};
var hmac = (hash4, key, message) => new _HMAC(hash4, key).update(message).digest();
hmac.create = (hash4, key) => new _HMAC(hash4, key);

// node_modules/@noble/hashes/pbkdf2.js
function pbkdf2Init(hash4, _password, _salt, _opts) {
  ahash(hash4);
  const opts = checkOpts({ dkLen: 32, asyncTick: 10 }, _opts);
  const { c, dkLen, asyncTick } = opts;
  anumber(c, "c");
  anumber(dkLen, "dkLen");
  anumber(asyncTick, "asyncTick");
  if (c < 1)
    throw new Error("iterations (c) must be >= 1");
  const password = kdfInputToBytes(_password, "password");
  const salt = kdfInputToBytes(_salt, "salt");
  const DK = new Uint8Array(dkLen);
  const PRF = hmac.create(hash4, password);
  const PRFSalt = PRF._cloneInto().update(salt);
  return { c, dkLen, asyncTick, DK, PRF, PRFSalt };
}
function pbkdf2Output(PRF, PRFSalt, DK, prfW, u) {
  PRF.destroy();
  PRFSalt.destroy();
  if (prfW)
    prfW.destroy();
  clean(u);
  return DK;
}
function pbkdf2(hash4, password, salt, opts) {
  const { c, dkLen, DK, PRF, PRFSalt } = pbkdf2Init(hash4, password, salt, opts);
  let prfW;
  const arr = new Uint8Array(4);
  const view2 = createView(arr);
  const u = new Uint8Array(PRF.outputLen);
  for (let ti = 1, pos = 0; pos < dkLen; ti++, pos += PRF.outputLen) {
    const Ti = DK.subarray(pos, pos + PRF.outputLen);
    view2.setInt32(0, ti, false);
    (prfW = PRFSalt._cloneInto(prfW)).update(arr).digestInto(u);
    Ti.set(u.subarray(0, Ti.length));
    for (let ui = 1; ui < c; ui++) {
      PRF._cloneInto(prfW).update(u).digestInto(u);
      for (let i = 0; i < Ti.length; i++)
        Ti[i] ^= u[i];
    }
  }
  return pbkdf2Output(PRF, PRFSalt, DK, prfW, u);
}

// node_modules/@noble/hashes/_md.js
function Chi(a, b, c) {
  return a & b ^ ~a & c;
}
function Maj(a, b, c) {
  return a & b ^ a & c ^ b & c;
}
var HashMD = class {
  blockLen;
  outputLen;
  padOffset;
  isLE;
  // For partial updates less than block size
  buffer;
  view;
  finished = false;
  length = 0;
  pos = 0;
  destroyed = false;
  constructor(blockLen, outputLen, padOffset, isLE2) {
    this.blockLen = blockLen;
    this.outputLen = outputLen;
    this.padOffset = padOffset;
    this.isLE = isLE2;
    this.buffer = new Uint8Array(blockLen);
    this.view = createView(this.buffer);
  }
  update(data) {
    aexists(this);
    abytes(data);
    const { view: view2, buffer, blockLen } = this;
    const len = data.length;
    for (let pos = 0; pos < len; ) {
      const take = Math.min(blockLen - this.pos, len - pos);
      if (take === blockLen) {
        const dataView = createView(data);
        for (; blockLen <= len - pos; pos += blockLen)
          this.process(dataView, pos);
        continue;
      }
      buffer.set(data.subarray(pos, pos + take), this.pos);
      this.pos += take;
      pos += take;
      if (this.pos === blockLen) {
        this.process(view2, 0);
        this.pos = 0;
      }
    }
    this.length += data.length;
    this.roundClean();
    return this;
  }
  digestInto(out) {
    aexists(this);
    aoutput(out, this);
    this.finished = true;
    const { buffer, view: view2, blockLen, isLE: isLE2 } = this;
    let { pos } = this;
    buffer[pos++] = 128;
    clean(this.buffer.subarray(pos));
    if (this.padOffset > blockLen - pos) {
      this.process(view2, 0);
      pos = 0;
    }
    for (let i = pos; i < blockLen; i++)
      buffer[i] = 0;
    view2.setBigUint64(blockLen - 8, BigInt(this.length * 8), isLE2);
    this.process(view2, 0);
    const oview = createView(out);
    const len = this.outputLen;
    if (len % 4)
      throw new Error("_sha2: outputLen must be aligned to 32bit");
    const outLen = len / 4;
    const state = this.get();
    if (outLen > state.length)
      throw new Error("_sha2: outputLen bigger than state");
    for (let i = 0; i < outLen; i++)
      oview.setUint32(4 * i, state[i], isLE2);
  }
  digest() {
    const { buffer, outputLen } = this;
    this.digestInto(buffer);
    const res = buffer.slice(0, outputLen);
    this.destroy();
    return res;
  }
  _cloneInto(to) {
    to ||= new this.constructor();
    to.set(...this.get());
    const { blockLen, buffer, length, finished, destroyed, pos } = this;
    to.destroyed = destroyed;
    to.finished = finished;
    to.length = length;
    to.pos = pos;
    if (length % blockLen)
      to.buffer.set(buffer);
    return to;
  }
  clone() {
    return this._cloneInto();
  }
};
var SHA256_IV = /* @__PURE__ */ Uint32Array.from([
  1779033703,
  3144134277,
  1013904242,
  2773480762,
  1359893119,
  2600822924,
  528734635,
  1541459225
]);

// node_modules/@noble/hashes/sha2.js
var SHA256_K = /* @__PURE__ */ Uint32Array.from([
  1116352408,
  1899447441,
  3049323471,
  3921009573,
  961987163,
  1508970993,
  2453635748,
  2870763221,
  3624381080,
  310598401,
  607225278,
  1426881987,
  1925078388,
  2162078206,
  2614888103,
  3248222580,
  3835390401,
  4022224774,
  264347078,
  604807628,
  770255983,
  1249150122,
  1555081692,
  1996064986,
  2554220882,
  2821834349,
  2952996808,
  3210313671,
  3336571891,
  3584528711,
  113926993,
  338241895,
  666307205,
  773529912,
  1294757372,
  1396182291,
  1695183700,
  1986661051,
  2177026350,
  2456956037,
  2730485921,
  2820302411,
  3259730800,
  3345764771,
  3516065817,
  3600352804,
  4094571909,
  275423344,
  430227734,
  506948616,
  659060556,
  883997877,
  958139571,
  1322822218,
  1537002063,
  1747873779,
  1955562222,
  2024104815,
  2227730452,
  2361852424,
  2428436474,
  2756734187,
  3204031479,
  3329325298
]);
var SHA256_W = /* @__PURE__ */ new Uint32Array(64);
var SHA2_32B = class extends HashMD {
  constructor(outputLen) {
    super(64, outputLen, 8, false);
  }
  get() {
    const { A, B, C, D, E, F, G, H } = this;
    return [A, B, C, D, E, F, G, H];
  }
  // prettier-ignore
  set(A, B, C, D, E, F, G, H) {
    this.A = A | 0;
    this.B = B | 0;
    this.C = C | 0;
    this.D = D | 0;
    this.E = E | 0;
    this.F = F | 0;
    this.G = G | 0;
    this.H = H | 0;
  }
  process(view2, offset) {
    for (let i = 0; i < 16; i++, offset += 4)
      SHA256_W[i] = view2.getUint32(offset, false);
    for (let i = 16; i < 64; i++) {
      const W15 = SHA256_W[i - 15];
      const W2 = SHA256_W[i - 2];
      const s0 = rotr(W15, 7) ^ rotr(W15, 18) ^ W15 >>> 3;
      const s1 = rotr(W2, 17) ^ rotr(W2, 19) ^ W2 >>> 10;
      SHA256_W[i] = s1 + SHA256_W[i - 7] + s0 + SHA256_W[i - 16] | 0;
    }
    let { A, B, C, D, E, F, G, H } = this;
    for (let i = 0; i < 64; i++) {
      const sigma1 = rotr(E, 6) ^ rotr(E, 11) ^ rotr(E, 25);
      const T1 = H + sigma1 + Chi(E, F, G) + SHA256_K[i] + SHA256_W[i] | 0;
      const sigma0 = rotr(A, 2) ^ rotr(A, 13) ^ rotr(A, 22);
      const T2 = sigma0 + Maj(A, B, C) | 0;
      H = G;
      G = F;
      F = E;
      E = D + T1 | 0;
      D = C;
      C = B;
      B = A;
      A = T1 + T2 | 0;
    }
    A = A + this.A | 0;
    B = B + this.B | 0;
    C = C + this.C | 0;
    D = D + this.D | 0;
    E = E + this.E | 0;
    F = F + this.F | 0;
    G = G + this.G | 0;
    H = H + this.H | 0;
    this.set(A, B, C, D, E, F, G, H);
  }
  roundClean() {
    clean(SHA256_W);
  }
  destroy() {
    this.set(0, 0, 0, 0, 0, 0, 0, 0);
    clean(this.buffer);
  }
};
var _SHA256 = class extends SHA2_32B {
  // We cannot use array here since array allows indexing by variable
  // which means optimizer/compiler cannot use registers.
  A = SHA256_IV[0] | 0;
  B = SHA256_IV[1] | 0;
  C = SHA256_IV[2] | 0;
  D = SHA256_IV[3] | 0;
  E = SHA256_IV[4] | 0;
  F = SHA256_IV[5] | 0;
  G = SHA256_IV[6] | 0;
  H = SHA256_IV[7] | 0;
  constructor() {
    super(32);
  }
};
var sha256 = /* @__PURE__ */ createHasher(
  () => new _SHA256(),
  /* @__PURE__ */ oidNist(1)
);

// node_modules/@noble/hashes/scrypt.js
function XorAndSalsa(prev, pi, input, ii, out, oi) {
  let y00 = prev[pi++] ^ input[ii++], y01 = prev[pi++] ^ input[ii++];
  let y02 = prev[pi++] ^ input[ii++], y03 = prev[pi++] ^ input[ii++];
  let y04 = prev[pi++] ^ input[ii++], y05 = prev[pi++] ^ input[ii++];
  let y06 = prev[pi++] ^ input[ii++], y07 = prev[pi++] ^ input[ii++];
  let y08 = prev[pi++] ^ input[ii++], y09 = prev[pi++] ^ input[ii++];
  let y10 = prev[pi++] ^ input[ii++], y11 = prev[pi++] ^ input[ii++];
  let y12 = prev[pi++] ^ input[ii++], y13 = prev[pi++] ^ input[ii++];
  let y14 = prev[pi++] ^ input[ii++], y15 = prev[pi++] ^ input[ii++];
  let x00 = y00, x01 = y01, x02 = y02, x03 = y03, x04 = y04, x05 = y05, x06 = y06, x07 = y07, x08 = y08, x09 = y09, x10 = y10, x11 = y11, x12 = y12, x13 = y13, x14 = y14, x15 = y15;
  for (let i = 0; i < 8; i += 2) {
    x04 ^= rotl(x00 + x12 | 0, 7);
    x08 ^= rotl(x04 + x00 | 0, 9);
    x12 ^= rotl(x08 + x04 | 0, 13);
    x00 ^= rotl(x12 + x08 | 0, 18);
    x09 ^= rotl(x05 + x01 | 0, 7);
    x13 ^= rotl(x09 + x05 | 0, 9);
    x01 ^= rotl(x13 + x09 | 0, 13);
    x05 ^= rotl(x01 + x13 | 0, 18);
    x14 ^= rotl(x10 + x06 | 0, 7);
    x02 ^= rotl(x14 + x10 | 0, 9);
    x06 ^= rotl(x02 + x14 | 0, 13);
    x10 ^= rotl(x06 + x02 | 0, 18);
    x03 ^= rotl(x15 + x11 | 0, 7);
    x07 ^= rotl(x03 + x15 | 0, 9);
    x11 ^= rotl(x07 + x03 | 0, 13);
    x15 ^= rotl(x11 + x07 | 0, 18);
    x01 ^= rotl(x00 + x03 | 0, 7);
    x02 ^= rotl(x01 + x00 | 0, 9);
    x03 ^= rotl(x02 + x01 | 0, 13);
    x00 ^= rotl(x03 + x02 | 0, 18);
    x06 ^= rotl(x05 + x04 | 0, 7);
    x07 ^= rotl(x06 + x05 | 0, 9);
    x04 ^= rotl(x07 + x06 | 0, 13);
    x05 ^= rotl(x04 + x07 | 0, 18);
    x11 ^= rotl(x10 + x09 | 0, 7);
    x08 ^= rotl(x11 + x10 | 0, 9);
    x09 ^= rotl(x08 + x11 | 0, 13);
    x10 ^= rotl(x09 + x08 | 0, 18);
    x12 ^= rotl(x15 + x14 | 0, 7);
    x13 ^= rotl(x12 + x15 | 0, 9);
    x14 ^= rotl(x13 + x12 | 0, 13);
    x15 ^= rotl(x14 + x13 | 0, 18);
  }
  out[oi++] = y00 + x00 | 0;
  out[oi++] = y01 + x01 | 0;
  out[oi++] = y02 + x02 | 0;
  out[oi++] = y03 + x03 | 0;
  out[oi++] = y04 + x04 | 0;
  out[oi++] = y05 + x05 | 0;
  out[oi++] = y06 + x06 | 0;
  out[oi++] = y07 + x07 | 0;
  out[oi++] = y08 + x08 | 0;
  out[oi++] = y09 + x09 | 0;
  out[oi++] = y10 + x10 | 0;
  out[oi++] = y11 + x11 | 0;
  out[oi++] = y12 + x12 | 0;
  out[oi++] = y13 + x13 | 0;
  out[oi++] = y14 + x14 | 0;
  out[oi++] = y15 + x15 | 0;
}
function BlockMix(input, ii, out, oi, r) {
  let head = oi + 0;
  let tail = oi + 16 * r;
  for (let i = 0; i < 16; i++)
    out[tail + i] = input[ii + (2 * r - 1) * 16 + i];
  for (let i = 0; i < r; i++, head += 16, ii += 16) {
    XorAndSalsa(out, tail, input, ii, out, head);
    if (i > 0)
      tail += 16;
    XorAndSalsa(out, head, input, ii += 16, out, tail);
  }
}
function scryptInit(password, salt, _opts) {
  const opts = checkOpts({
    dkLen: 32,
    asyncTick: 10,
    maxmem: 1024 ** 3 + 1024
  }, _opts);
  const { N, r, p, dkLen, asyncTick, maxmem, onProgress } = opts;
  anumber(N, "N");
  anumber(r, "r");
  anumber(p, "p");
  anumber(dkLen, "dkLen");
  anumber(asyncTick, "asyncTick");
  anumber(maxmem, "maxmem");
  if (onProgress !== void 0 && typeof onProgress !== "function")
    throw new Error("progressCb must be a function");
  const blockSize = 128 * r;
  const blockSize32 = blockSize / 4;
  const pow32 = Math.pow(2, 32);
  if (N <= 1 || (N & N - 1) !== 0 || N > pow32)
    throw new Error('"N" expected a power of 2, and 2^1 <= N <= 2^32');
  if (p < 1 || p > (pow32 - 1) * 32 / blockSize)
    throw new Error('"p" expected integer 1..((2^32 - 1) * 32) / (128 * r)');
  if (dkLen < 1 || dkLen > (pow32 - 1) * 32)
    throw new Error('"dkLen" expected integer 1..(2^32 - 1) * 32');
  const memUsed = blockSize * (N + p);
  if (memUsed > maxmem)
    throw new Error('"maxmem" limit was hit, expected 128*r*(N+p) <= "maxmem"=' + maxmem);
  const B = pbkdf2(sha256, password, salt, { c: 1, dkLen: blockSize * p });
  const B32 = u32(B);
  const V = u32(new Uint8Array(blockSize * N));
  const tmp = u32(new Uint8Array(blockSize));
  let blockMixCb = () => {
  };
  if (onProgress) {
    const totalBlockMix = 2 * N * p;
    const callbackPer = Math.max(Math.floor(totalBlockMix / 1e4), 1);
    let blockMixCnt = 0;
    blockMixCb = () => {
      blockMixCnt++;
      if (onProgress && (!(blockMixCnt % callbackPer) || blockMixCnt === totalBlockMix))
        onProgress(blockMixCnt / totalBlockMix);
    };
  }
  return { N, r, p, dkLen, blockSize32, V, B32, B, tmp, blockMixCb, asyncTick };
}
function scryptOutput(password, dkLen, B, V, tmp) {
  const res = pbkdf2(sha256, password, B, { c: 1, dkLen });
  clean(B, V, tmp);
  return res;
}
async function scryptAsync(password, salt, opts) {
  const { N, r, p, dkLen, blockSize32, V, B32, B, tmp, blockMixCb, asyncTick } = scryptInit(password, salt, opts);
  swap32IfBE(B32);
  for (let pi = 0; pi < p; pi++) {
    const Pi = blockSize32 * pi;
    for (let i = 0; i < blockSize32; i++)
      V[i] = B32[Pi + i];
    let pos = 0;
    await asyncLoop(N - 1, asyncTick, () => {
      BlockMix(V, pos, V, pos += blockSize32, r);
      blockMixCb();
    });
    BlockMix(V, (N - 1) * blockSize32, B32, Pi, r);
    blockMixCb();
    await asyncLoop(N, asyncTick, () => {
      const j = (B32[Pi + blockSize32 - 16] & N - 1) >>> 0;
      for (let k = 0; k < blockSize32; k++)
        tmp[k] = B32[Pi + k] ^ V[j * blockSize32 + k];
      BlockMix(tmp, 0, B32, Pi, r);
      blockMixCb();
    });
  }
  swap32IfBE(B32);
  return scryptOutput(password, dkLen, B, V, tmp);
}

// src/shared/dev-auth.ts
var base64url = (b) => btoa(String.fromCharCode(...new Uint8Array(b))).replace(/=/g, "").replace(/\+/g, "-").replace(/\//g, "_");

// src/shared/access.ts
var GRANTABLE_CONTENT = {
  "weapon:emperor": "Emperor Style + Mishima Chips",
  "intro:dev-emperor": "Emperor storm intro",
  "weapon:leet": "L33T H4x staff + L44T Chips",
  "weapon:ansatsuken": "Ansatsuken + arcade Chips",
  "intro:dev-matrix": "Matrix throne intro",
  "intro:dev-ansatsuken": "Rain warrior intro",
  "hat:auditor-halo": "Auditor Halo",
  "effect:dev-raging-demon": "Raging Demon",
  "effect:dev-clown": "Clown getaway",
  "effect:dev-clown-love": "Clown Love",
  "effect:dev-bass-van": "Bass Van"
};
var ACCOUNT_FLAGS = { playtester: "Playtester", vip: "VIP" };
var canUse = (a, key) => typeof a === "boolean" ? a : !!a?.developer || !!a?.unlocks?.includes(key);
var cleanGrants = (x) => Array.isArray(x) ? [...new Set(x.filter((k) => typeof k === "string" && Object.hasOwn(GRANTABLE_CONTENT, k)))] : [];
var cleanFlags = (x) => Array.isArray(x) ? [...new Set(x.filter((k) => typeof k === "string" && Object.hasOwn(ACCOUNT_FLAGS, k)))] : [];

// src/shared/account-auth.ts
var GAME_CLAIM_MS = 15 * 60 * 1e3;

// src/shared/intros.ts
var INTRO_STYLES = {
  "dev-emperor": "DEV \xB7 Emperor\u2019s Oath",
  "dev-ansatsuken": "DEV \xB7 Rain Warrior",
  "dev-matrix": "DEV \xB7 L33T Throne",
  "neon-drop": "Neon Drop",
  "vector-slide": "Vector Slide",
  "signal-spin": "Signal Spin",
  "arc-salute": "Arc Salute",
  "pulse-wave": "Pulse Wave"
};

// src/shared/emperor-content.ts
var chip = (id, name, description, icon) => ({ id: "mishima-" + id, name, description, icon, rarity: "mishima", tags: ["emperor"], max: 1, effects: [], mechanic: "mishima-" + id });
var MISHIMA_CHIPS = [
  chip("godfist", "Perfect Electric", "Electric hooks recover much faster. Keep the lightning chain alive.", "\u03DF"),
  chip("twin", "Twin Thunder", "Each electric hook leaves a second lightning strike at the impact point.", "\u2161"),
  chip("screw", "Screwdriver", "Every third airborne hit spins the enemy sideways and deals extra damage.", "\u27F3"),
  chip("bound", "Bound Breaker", "The first landing in each juggle bounces the enemy back up and deals damage.", "\u219F"),
  chip("magnet", "Magnetic Fists", "Electric hooks steer airborne enemies into your next punch while still carrying them back.", "\u2301"),
  chip("wall", "Wall Tyrant", "Electric hooks carry enemies farther. Their first wall collision adds a damaging wall bounce.", "\u25A5"),
  chip("armor", "Iron Wavedash", "Take half damage while wavedashing. You can still be interrupted and knocked back.", "\u25C7"),
  chip("trail", "Storm Footwork", "Wavedashes leave short-lived lightning that shocks enemies crossing your path.", "\u224B"),
  chip("pressure", "Pressure String", "Your two jabs become a three-punch string with a heavier final hit.", "\u2162"),
  chip("counter", "Counter Voltage", "Electric hooks deal greatly increased damage when they catch an enemy attacking.", "!"),
  chip("rage", "Emperor\u2019s Rage", "Below one-third health, your punches and electric hooks deal greatly increased damage.", "\u265C"),
  chip("bloodline", "Bloodline Surge", "Confirmed electric hits restore a little health and stamina.", "\u2665")
];

// src/shared/arcade-content.ts
var chip2 = (id, name, description, icon) => ({ id: "arcade-" + id, name, rarity: "ansatsuken", tags: ["ansatsuken"], max: 1, icon, mechanic: "arcade-" + id, effects: [], description });
var ARCADE_CHIPS = [
  chip2("shinku", "Shinku Hadoken", "Hold 1 to charge. Release a full charge to fire a huge, piercing five-hit Hadoken.", "\u25CE"),
  chip2("rush", "Ten-Hit Rush", "Your punch\u2013punch\u2013high-kick chain becomes a rapid ten-hit combo with a high-kick finisher.", "10"),
  chip2("red", "Shakunetsu", "Hadokens turn crimson and inflict a much stronger burn.", "\u2668"),
  chip2("cyclone", "Hurricane Tatsu", "Tatsumaki spins longer, pulls nearby enemies inward and lands more kicks.", "\u27F3"),
  chip2("rising", "Shin Shoryuken", "Shoryuken becomes three rising hits. The final hit launches enemies and shatters nearby cover.", "\u21C8"),
  chip2("cancel", "Special Cancel", "After a combo hit, interrupt your punches with Tatsumaki or Shoryuken. Special moves recover faster.", "\xBB"),
  chip2("focus", "Focus Armor", "Take greatly reduced damage while charging Hadoken. You can still be knocked back.", "\u25C7"),
  chip2("perfect", "Perfect Rhythm", "Landing the final combo hit restores stamina and a little health.", "\u266A"),
  chip2("juggle", "Air Juggle", "Hits deal greatly increased damage to airborne enemies. Keep them off the ground.", "\u2191"),
  chip2("afterimage", "Afterimage Assault", "Each punch leaves a delayed echo that strikes the same location. Enemies can move away.", "\u25A5"),
  chip2("counter", "Counter Hit", "Striking an enemy during their attack deals extra damage and knockback.", "!"),
  chip2("secondwind", "Round Two", "Once per round, survive a lethal hit with a sliver of health and recover all stamina.", "\u2161")
];

// src/shared/leet-content.ts
var chip3 = (id, name, description, icon) => ({ id: "l44t-" + id, name: "L44T " + name, rarity: "l44t", tags: ["leet"], max: 1, icon, mechanic: "l44t-" + id, effects: [], description });
var LEET_CHIPS = [
  chip3("brain", "Brain Steal", "Replaces Block with a possession orb. Hit an enemy to control their movement and attacks for 5 seconds. Your own body stays vulnerable.", "\u25CE"),
  chip3("scare", "Scare Tactics", "E summons a haunted tree. Slenderman emerges and stalks an enemy. If he reaches them, a fullscreen jumpscare ends in an instant knockout.", "\u265C"),
  chip3("nuke", "Nuke", "Projectile impacts mark the area with green sparks, then unleash a massive explosion that hurls nearby cover away.", "\u2622"),
  chip3("yeet", "Y33t", "Hits launch enemies and cover with a thousand times the knockback. Goodbye, arena.", "\u21C8"),
  chip3("time", "Admin Pause", "E freezes every enemy and their projectiles for 4 seconds. You keep moving.", "\u2161"),
  chip3("undo", "CTRL + Z", "Once per round, a lethal hit rewinds you to an earlier safe position and completely restores your health.", "\u21B6"),
  chip3("copy", "Copy / Paste", "Hits print three delayed attack orbs above the impact. They fire at the original target position like a matrix firing squad.", "\u25A5"),
  chip3("uprising", "Object Uprising", "Projectile impacts commandeer nearby loose cover and launch it at a visible enemy. The map works for you now.", "\u2317")
];

// src/shared/chaos-content.ts
var chip4 = (id, name, rarity, tags, description, icon = "\u2726") => ({ id, name, rarity, tags: [tags], description, max: 1, icon, mechanic: id, effects: [] });
var CHAOS_CHIPS = [
  chip4("black-hole", "Event Horizon", "mythic", "projectile", "Impacts open a 5 m black hole for 3.5 s: pulls enemies, loose cover and enemy shots inward; 10 DPS. One field per volley, at most 3 of each field per owner.", "\u25C9"),
  chip4("necronomicon", "Necronomicon", "mythic", "magic", "E becomes Raise the Dead: 3 zombies + 1 per extra projectile (max 14), each with 2\xD7 your maximum HP and 0.5\xD7 your movement speed. 12-damage bites scale with damage. 35 mana, 10 s cooldown, 14 s lifetime; a new summon replaces your old army.", "\u2620"),
  chip4("acid-rain", "Chemical Spill", "epic", "projectile", "Impacts leave a luminous 3 m acid pool for 4 s. Enemies inside take 12 DPS; cover corrodes. One pool per volley.", "\u2623"),
  chip4("wildfire", "Bonfire Permit", "rare", "projectile", "Impacts plant a 2.5 m bonfire for 3 s, dealing 15 DPS to enemies and cover. One fire per volley.", "\u2668"),
  chip4("blizzard", "Deep Freeze", "epic", "projectile", "Impacts create a 3.5 m frost field for 3 s. Enemies move at 45% speed inside and take 4 DPS. One field per volley.", "\u2744"),
  chip4("time-warp", "Pocket Slow-Mo", "legendary", "projectile", "Impacts create a 4 m time bubble for 4 s. Enemy projectiles travel at 20% speed while inside; enemies move at 60%. One bubble per volley.", "\u25F7"),
  chip4("repulsion-pad", "Personal Space", "epic", "projectile", "Impacts create a 3.5 m repulsion field for 3 s, pushing enemies and loose cover outward continuously. One field per volley.", "\u2194"),
  chip4("med-garden", "Friendly Fungus", "rare", "projectile", "Impacts grow a 2.5 m healing garden for 4 s. Restore 6 HP/s while you stand inside. No enemy healing; one garden per volley.", "\u271A"),
  chip4("lightning-rod", "Storm In A Jar", "legendary", "projectile", "Impacts plant a 4 m electrical field for 3 s. Enemies inside take 18 DPS through visible arcs. One storm per volley.", "\u03DF"),
  chip4("bubble-mine", "Pop Quiz", "rare", "projectile", "Impacts leave a 3 m bubble that detonates after 1 s for 25 damage and strong outward knockback. One bubble per volley.", "\u25CB"),
  chip4("sticky-bomb", "Sticky Situation", "epic", "projectile", "First impact attaches a visible bomb to its target. After 1.2 s it explodes in 3 m for 28 damage. Follows moving targets; one per volley.", "\u25CF"),
  chip4("cluster-mine", "Mine Sweeper", "legendary", "projectile", "Each volley plants 3 proximity mines around its first impact. Arm after 0.6 s, last 6 s; each explodes for 16 damage in 2.3 m.", "\u22EE"),
  chip4("proximity-mail", "Special Delivery", "rare", "projectile", "Impacts leave an armed parcel. After 0.6 s, an enemy within 2.5 m detonates it for 32 damage; expires after 6 s. One per volley.", "\u25A3"),
  chip4("delayed-gratification", "Wait For It", "epic", "all", "A direct impact marks a 4 m blast for 2.2 s later: 48 damage, huge knockback. Escape the warning circle. One per attack.", "\u2026"),
  chip4("implosion-bomb", "Reverse Grenade", "legendary", "projectile", "Impacts mark a 4 m implosion for 0.9 s later: 30 damage, pulling enemies into the blast center. One per volley.", "\u2299"),
  chip4("wormhole", "Pocket Portal", "mythic", "projectile", "First surface impact per volley blinks you toward the impact, stopping at cover and a maximum of 12 m. 2 s shared portal cooldown. Never places you inside a wall.", "\u25CE"),
  chip4("swap-shop", "Trading Places", "mythic", "all", "Direct opponent hits swap your positions, once per 5 s. Both endpoints must have room for a pilot. Momentum is cleared.", "\u21C4"),
  chip4("grappling-shot", "Yoink!", "rare", "projectile", "Direct hits pull the opponent toward you with 1800 impulse instead of ordinary knockback.", "\u21A4"),
  chip4("uppercut", "Air Mail", "rare", "all", "Direct hits add an upward 8 m/s kick, scaled by the target\u2019s knockback resistance.", "\u2191"),
  chip4("bowling-ball", "Bowling Club", "epic", "projectile", "Shots grow into heavy balls at 45% launch speed and twice the radius. +4 bounces; direct hits launch enemies and loose cover with 3\xD7 impulse.", "\u25CF"),
  chip4("orbitals", "Satellite Office", "legendary", "projectile", "Shots orbit your muzzle for 0.65 s, then launch toward your original aim. Cannot combine with Pong flight.", "\u25CC"),
  chip4("corkscrew", "Drill Sergeant", "epic", "projectile", "Shots corkscrew around their flight axis. Actual curved paths use collision sweeps; cover still blocks them.", "\u27F3"),
  chip4("elevator-shot", "Going Up", "rare", "projectile", "Shots accelerate upward instead of falling. +50% radius; aim low to curve over cover. Pong retains its fixed flight.", "\u2197"),
  chip4("stop-go", "Red Light, Green Light", "legendary", "projectile", "Shots pause every other 0.35 s, then resume. Their lifetime extends to 6 s. Pong keeps moving.", "\u2161"),
  chip4("accelerating-shot", "Late Bloomer", "rare", "projectile", "Shots start at 25% speed, accelerating to 200% over 1 s. Their lifetime becomes 5 s. Pong keeps its fixed speed.", "\xBB"),
  chip4("backfire", "Rear Guard", "uncommon", "projectile", "Every volley also fires a 40%-damage shot directly backward. The extra shot cannot trigger Chips. Disabled by Pong.", "\u2194"),
  chip4("crossfire", "Four-Way Stop", "epic", "projectile", "Every volley adds 3 shots at 90\xB0, 180\xB0 and 270\xB0, each at 35% damage. Extra shots cannot trigger Chips. Disabled by Pong.", "\u271B"),
  chip4("helix", "Double Helix", "epic", "projectile", "Each volley adds two shots offset to either side, twisting in opposite directions at 45% damage. No recursive effects. Disabled by Pong.", "\u221E"),
  chip4("coin-toss", "Heads Or Tails", "uncommon", "all", "Each direct hit rolls either 2\xD7 knockback or a 6 HP heal. The authority flips the coin.", "\u25D0"),
  chip4("artillery", "Meteor Shower", "mythic", "projectile", "The first impact per volley calls down 3 warned explosions, 0.35 s apart, for 22 damage each in 2.8 m. No recursive effects.", "\u2604"),
  chip4("sword-beam", "Blade Runner", "legendary", "melee", "Each primary swing launches a visible energy blade: 18 base damage, 24 m/s, 30 m range. Scales with damage; can be Blocked.", "\u2571"),
  chip4("aftershock", "Ground Pound", "epic", "melee", "Direct hits schedule a 3.5 m shockwave after 0.45 s: 20 damage and heavy knockback. One per sweep.", "\u224B"),
  chip4("blink-cut", "Now You See Me", "legendary", "melee", "Your primary swing steps up to 2.5 m toward your aim before winding up. Cover stops the blink.", "\u2301"),
  chip4("vampire-dash", "Blood Trail", "rare", "all", "Dashing drains 12 health from a visible enemy within 3 m and restores actual damage dealt. Once per dash.", "\u2665"),
  chip4("bread-crumbs", "Exit Gift", "rare", "all", "Dashing drops a proximity mine: 24 damage in 2.8 m, arms in 0.6 s, lasts 5 s.", "\u25C6"),
  chip4("hot-foot", "Hot Foot", "epic", "all", "Each dash leaves a 3 m fire field for 3 s, dealing 15 DPS to enemies and cover.", "\u2668"),
  chip4("thunderstep", "Thunder Step", "legendary", "all", "Each dash schedules a 4 m lightning strike at your starting point after 0.5 s, dealing 35 damage.", "\u03DF"),
  chip4("trampoline", "Bounce House", "rare", "all", "Jumping kicks off a 3 m shockwave: 10 damage and upward knockback to visible enemies. Triggers only on a grounded jump.", "\u219F"),
  chip4("second-chance", "Emergency Exit", "legendary", "all", "Once per round, surviving damage below 25% HP restores 20 HP and blinks you backward up to 6 m. Cover stops the blink.", "\u21B6"),
  chip4("revenge-mail", "Return Address", "epic", "all", "Taking damage marks the attacker\u2019s position for a 25-damage, 3 m explosion after 1 s. 4 s cooldown. No recursive damage triggers.", "\u2709"),
  chip4("mirror-mine", "Block Party", "epic", "all", "First successful Block per activation drops 3 proximity mines around you. Each deals 16 damage in 2.3 m; 6 s lifetime.", "\u25C7"),
  chip4("freezer-block", "Time Out", "legendary", "all", "First successful Block creates a 5 m time bubble for 3 s: enemy shots move at 20%, enemy pilots at 60%.", "\u25F7"),
  chip4("pickpocket", "Five Finger Discount", "uncommon", "ranged", "Direct opponent hits steal 1 of their available rounds into your magazine. Against spells or melee, steal up to 8 of their active resource instead.", "\u25A5"),
  chip4("mana-leech", "Mana Muncher", "uncommon", "magic", "Direct opponent hits drain up to 10 mana or stamina and restore the amount as your mana. Guns lose up to 1 round instead, restoring 8 mana.", "\u25C8"),
  chip4("demolition-charge", "Load-Bearing Surprise", "rare", "all", "Direct cover impacts plant a 1 s demolition charge: 3 m blast, 18 player damage and strong structural damage. One per attack.", "\u2317"),
  chip4("puppet-master", "Toy Soldier", "legendary", "projectile", "First impact per volley raises a tiny 25 HP, 8-damage toy soldier for 6 s. Up to 3 toys per owner.", "\u265F"),
  chip4("graveyard-shift", "Graveyard Shift", "epic", "magic", "First successful Block raises one zombie for 10 s: 2\xD7 your max HP, 0.5\xD7 your speed, 12-damage bites. Shared limit 14 zombies.", "\u2620"),
  chip4("corpse-party", "Corpse Party", "legendary", "magic", "Your summoned zombies explode when destroyed or expired: 30 damage in 3.5 m. Also raise one zombie on each successful Block activation.", "\u2733"),
  chip4("poltergeist", "Poltergeist", "epic", "all", "First successful Block launches nearby loose cover away from you with 900 impulse, within 6 m and line of sight.", "\u219D"),
  chip4("panic-button", "Do Not Press", "mythic", "all", "Once per round, surviving damage below 30% HP creates a 6 m repulsion field for 4 s and restores your Block and dash cooldowns.", "!")
];

// src/shared/config.ts
var CONFIG = {
  maxPlayers: 8,
  simulationHz: 60,
  snapshotHz: 20,
  wins: 5,
  roundsPerSet: 2,
  countdown: 3,
  resultSeconds: 5,
  selectionSeconds: 18,
  disconnectSeconds: 12,
  maxRoundSeconds: 100,
  speed: 6.8,
  acceleration: 35,
  jumpSpeed: 8.5,
  gravity: -23,
  dashSpeed: 15,
  dashDuration: 0.18,
  dashCooldown: 2.8,
  externalDrag: 3.1,
  maxExternalSpeed: 70,
  playerMass: 65,
  playerRadius: 0.42,
  playerHalfHeight: 0.43,
  minimumImpactSpeed: 3.6,
  safeEnergy: 45,
  damageScale: 0.035,
  maximumDamagePerImpact: 65,
  impactPairCooldown: 0.4,
  fragmentWindow: 0.35,
  fragmentDamageCap: 75,
  attributionSeconds: 7,
  manaRegen: 12,
  staminaRegen: 24,
  staminaRegenDelay: 0.45,
  shieldDrain: 33,
  shieldImpactCost: 0.9,
  blockDuration: 0.3,
  blockCooldown: 4,
  blockMaximumDuration: 0.65,
  blockMinimumCooldown: 1.5,
  blockCooldownChip: 0.85,
  blockWindowChip: 0.08,
  blockReflectDamageChip: 1.15,
  blockReflectSpeedChip: 1.1,
  blockPulseRadius: 3,
  blockPulseDamageChip: 6,
  blockPulseImpulseChip: 360,
  maxReflections: 3,
  maxReflectedDamage: 100,
  maxReflectedSpeed: 120,
  volleyDamage: 0.75,
  volleyFanDegrees: 7,
  followupDamage: 0.45,
  followupCost: 5,
  followupGap: 0.08,
  projectileLifetime: 3,
  projectileMaxTravel: 160,
  splitLifetime: 1.2,
  splitMaxTravel: 18,
  homingRange: 16,
  homingConeDegrees: 50,
  maxBodies: 150,
  maxProjectiles: 100,
  maxEvents: 80,
  maxInputRate: 100,
  staleInputSeconds: 0.3,
  maxRooms: 24
};

// src/shared/elements.ts
var ELEMENTS = {
  fire: { name: "Burning", stat: "burn", duration: 2, tick: 0.25, color: 16745007, dpsPerChip: 3, max: 6 },
  poison: { name: "Poisoned", stat: "poison", duration: 4, tick: 0.25, color: 10347341, dpsPerChip: 2.5, max: 6 }
};

// src/shared/content.ts
var WEAPONS = {
  emperor: { id: "emperor", name: "Emperor Style", devOnly: true, class: "melee", description: "DEV \xB7 LMB: left jab, right jab. Q: electric wind hook fist launches and juggles. E: repeatable wavedash with no cooldown. Mishima Chips evolve your style.", damage: 20, interval: 0.39, speed: 0, gravity: 0, range: 2.4, impulse: 120, ammo: 0, reload: 0, cost: 6, spread: 0, windup: 0.075, arc: 1.8, secondary: "wavedash", secondaryCooldown: 0, color: 16724047 },
  ansatsuken: { id: "ansatsuken", name: "Ansatsuken", devOnly: true, class: "melee", description: "DEV \xB7 LMB: right straight, left straight, high kick. RMB: no-cooldown parry. 1: burning Hadoken. E: Tatsumaki Senpukyaku. 2: Shoryuken. Shift: Demon Step, a longer afterimage dash. Exclusive arcade Chips evolve your moves.", damage: 39, interval: 0.9, speed: 23, gravity: 0, range: 2, impulse: 650, ammo: 0, reload: 0, cost: 12, spread: 0, windup: 0.12, arc: 1.6, secondary: "tatsu", secondaryCooldown: 5, color: 8445951 },
  leet: { id: "leet", name: "L33T H4x", devOnly: true, class: "magic", description: "DEV \xB7 Binary bolts pause when they detect a visible enemy, then launch at that captured position. E: Root Command. Unlocks L44T Chips and greatly improves legendary / mythic odds.", damage: 32, interval: 0.6, speed: 19, gravity: 0, range: 60, impulse: 260, ammo: 0, reload: 0, cost: 7, spread: 0, windup: 0, arc: 0, secondary: "root", secondaryCooldown: 12, color: 5439363 },
  pistol: { id: "pistol", name: "Pistol", class: "ranged", description: "Precise shots. 7-round magazine. E secondary: braced shot (2\xD7 damage, 2 ammo).", damage: 23, interval: 0.42, speed: 52, gravity: 32, range: 55, impulse: 175, ammo: 7, reload: 1.1, cost: 0, spread: 8e-3, windup: 0, arc: 0, secondary: "power", secondaryCooldown: 2.2, color: 16767117 },
  rifle: { id: "rifle", name: "Rifle", class: "ranged", description: "Sniper: 58 damage, 3 rounds, 1.15 s between shots, 2.6 s reload. Fast 150 m/s shots retain gravity. Hold Q: 3\xD7 scope. E: 78-damage braced shot (2 ammo).", damage: 58, interval: 1.15, speed: 150, gravity: 12, range: 140, impulse: 350, ammo: 3, reload: 2.6, cost: 0, spread: 15e-4, windup: 0, arc: 0, secondary: "snipe", secondaryCooldown: 3.8, color: 16770986 },
  machinegun: { id: "machinegun", name: "Machine Gun", class: "ranged", description: "Rapid automatic fire, 48-round magazine, 2.4 s reload. E secondary: six-round suppression burst (6 ammo).", damage: 8, interval: 0.085, speed: 49, gravity: 36, range: 48, impulse: 65, ammo: 48, reload: 2.4, cost: 0, spread: 0.025, windup: 0, arc: 0, secondary: "burst", secondaryCooldown: 3, color: 16761449, burstCount: 6 },
  fire: { id: "fire", name: "Fire", class: "magic", description: "22-damage firebolts, 1.5 m blast and 4 burn DPS for 2 s. E secondary: explosive fireball (25 mana).", damage: 22, interval: 0.56, speed: 24, gravity: 12, range: 42, impulse: 210, ammo: 0, reload: 0, cost: 8, spread: 6e-3, windup: 0, arc: 0, secondary: "fireball", secondaryCooldown: 3.4, color: 16743742, burn: 4, blast: 1.5 },
  water: { id: "water", name: "Water", class: "magic", description: "18-damage fast water globes. E secondary: wide pressure wave pushes players and props (22 mana).", damage: 18, interval: 0.4, speed: 31, gravity: 16, range: 40, impulse: 340, ammo: 0, reload: 0, cost: 6, spread: 0.01, windup: 0, arc: 0, secondary: "push", secondaryCooldown: 3, color: 6674413 },
  lightning: { id: "lightning", name: "Lightning", class: "magic", description: "12-damage rapid bolts. Chains 6 damage to one nearby target. E secondary: close electrical nova (24 mana).", damage: 12, interval: 0.27, speed: 85, gravity: 40, range: 45, impulse: 75, ammo: 0, reload: 0, cost: 5, spread: 6e-3, windup: 0, arc: 0, secondary: "nova", secondaryCooldown: 3.5, color: 12755967, chain: 6 },
  darkness: { id: "darkness", name: "Darkness", class: "magic", description: "27-damage slow orbs restore 15% of direct damage. E secondary: short-range pull (20 mana).", damage: 27, interval: 0.7, speed: 15, gravity: 5, range: 36, impulse: 140, ammo: 0, reload: 0, cost: 10, spread: 0, windup: 0, arc: 0, secondary: "pull", secondaryCooldown: 3.8, color: 11368658, drain: 0.15 },
  sword: { id: "sword", name: "One-Handed Sword", class: "melee", description: "30-damage 2.5 m swing; 14 stamina, 0.14 s windup. E secondary: 0.4 s frontal parry and forward step (18 stamina).", damage: 30, interval: 0.62, speed: 0, gravity: 0, range: 2.5, impulse: 300, ammo: 0, reload: 0, cost: 14, spread: 0, windup: 0.14, arc: 1.6, secondary: "parry", secondaryCooldown: 1.5, secondaryCost: 18, color: 11984855 },
  daggers: { id: "daggers", name: "Daggers", class: "melee", description: "15-damage 1.75 m fast strikes; 7 stamina. E secondary: 3 m evasive sidestep (16 stamina); no invulnerability.", damage: 15, interval: 0.28, speed: 0, gravity: 0, range: 1.75, impulse: 95, ammo: 0, reload: 0, cost: 7, spread: 0, windup: 0.06, arc: 1.3, secondary: "sidestep", secondaryCooldown: 1.3, secondaryCost: 16, color: 9891013 },
  shield: { id: "shield", name: "Shield", class: "melee", description: "23-damage bash, 18 stamina, heavy impulse. Hold E: 75% frontal reduction, drains 33 stamina/s plus impact cost; rear exposed.", damage: 23, interval: 0.75, speed: 0, gravity: 0, range: 2.1, impulse: 610, ammo: 0, reload: 0, cost: 18, spread: 0, windup: 0.2, arc: 1.2, secondary: "guard", secondaryCooldown: 0, secondaryCost: 0, color: 9223910 },
  greatsword: { id: "greatsword", name: "Greatsword", class: "melee", description: "52-damage 3.4 m arc; 26 stamina, 0.42 s windup. E secondary: shoulder charge with frontal guard (24 stamina).", damage: 52, interval: 1.18, speed: 0, gravity: 0, range: 3.4, impulse: 560, ammo: 0, reload: 0, cost: 26, spread: 0, windup: 0.42, arc: 2.4, secondary: "charge", secondaryCooldown: 3, secondaryCost: 24, color: 15386784 }
};
var e = (stat, op, value) => ({ stat, op, value });
var CHIPS = [
  { id: "vital", rarity: "common", name: "Vital Core", description: "+25 maximum health per installation. Additive.", tags: ["all"], max: 12, icon: "+", effects: [e("hp", "add", 25)] },
  { id: "overclock", rarity: "common", name: "Overclock", description: "\xD71.18 attack rate; \xD70.92 damage per hit per stack. Multiplicative.", tags: ["all"], max: 8, icon: "\xBB", effects: [e("attackRate", "mul", 1.18), e("damage", "mul", 0.92)] },
  { id: "dash", rarity: "common", name: "Dash Capacitor", description: "\xD70.82 dash cooldown per stack. Minimum 0.5 s.", tags: ["all"], max: 8, icon: "\u2197", effects: [e("dashCooldown", "mul", 0.82)] },
  { id: "frame", rarity: "common", name: "Reinforced Frame", description: "\xD70.88 incoming damage; \xD70.94 movement speed per stack.", tags: ["all"], max: 6, icon: "\u25A3", effects: [e("damageTaken", "mul", 0.88), e("speed", "mul", 0.94)] },
  { id: "stabilizer", rarity: "common", name: "Stabilizer", description: "\xD70.72 received attack knockback per stack. Physics impulses are also damped.", tags: ["all"], max: 5, icon: "\xB1", effects: [e("knockbackTaken", "mul", 0.72)] },
  { id: "ricochet", rarity: "uncommon", name: "Ricochet", description: "+1 direct projectile bounce per stack (max 3). Retains 70% damage on each bounce.", tags: ["projectile"], max: 3, icon: "\u2301", effects: [e("bounces", "add", 1)] },
  { id: "splitter", rarity: "epic", name: "Splitter", description: "Primary impact produces 2 + stacks secondary shots, each 22% direct damage. Max 5; no recursive effects.", tags: ["projectile"], max: 3, icon: "\u22D4", effects: [e("split", "add", 1)] },
  { id: "piercing", rarity: "rare", name: "Piercing", description: "Pass through 1 opponent and continue into props behind them. Cannot pierce cover; each target hit once. Single installation in 1v1.", tags: ["projectile"], max: 1, icon: "\u2192", effects: [e("pierces", "add", 1)] },
  { id: "ember", rarity: "uncommon", name: "Ember Circuit", description: "FIRE ENCHANT: +3 burn DPS per stack for 2 s (6 damage). Flaming shots / blade. Direct hits refresh, never delay ticks. Strongest per source; stacks with poison.", tags: ["all"], max: ELEMENTS.fire.max, icon: "\u2668", effects: [e("burn", "add", ELEMENTS.fire.dpsPerChip)] },
  { id: "poison", rarity: "uncommon", name: "Venom Circuit", description: "POISON ENCHANT: +2.5 poison DPS per stack for 4 s (10 damage). Toxic shots / blade. Direct hits refresh, never delay ticks. Strongest per source; stacks with fire. No recursive procs.", tags: ["all"], max: ELEMENTS.poison.max, icon: "\u2623", effects: [e("poison", "add", ELEMENTS.poison.dpsPerChip)] },
  { id: "static", rarity: "rare", name: "Static Discharge", description: "Every 4 direct opponent hits: up to 7 electrical damage per stack within 3 m, with radial falloff and line of sight. No proc recursion.", tags: ["all"], max: 6, icon: "\u03DF", effects: [e("staticEvery", "set", 4), e("staticDamage", "add", 7)] },
  { id: "reach", rarity: "common", name: "Extended Reach", description: "+20% base melee reach per stack. Arc and visible sweep extend with the hit volume.", tags: ["melee"], max: 5, icon: "\u2922", effects: [e("reach", "add", 0.2)] },
  { id: "kinetic", rarity: "common", name: "Kinetic Driver", description: "\xD72.4 outgoing attack impulse; \xD70.93 attack rate per stack.", tags: ["all"], max: 6, icon: "\u226B", effects: [e("impulse", "mul", 2.4), e("attackRate", "mul", 0.93)] },
  { id: "demolition", rarity: "common", name: "Demolition Module", description: "+60% base structural damage per stack. Mass and launch impulse stay independent.", tags: ["all"], max: 8, icon: "\u2317", effects: [e("demolition", "add", 0.6)] },
  { id: "shrapnel", rarity: "rare", name: "Shrapnel Protocol", description: "Direct impact: 1.8 m blast, up to 6 damage per stack and 220 impulse, with radial falloff. Once per attack; no recursion.", tags: ["all"], max: 5, icon: "\u2733", effects: [e("shrapnel", "add", 6)] },
  { id: "salvage", rarity: "common", name: "Salvage Plating", description: "\xD70.65 environmental debris damage per stack. Applies after fragment-event cap.", tags: ["all"], max: 5, icon: "\u2B21", effects: [e("debrisTaken", "mul", 0.65)] },
  { id: "multishot", rarity: "rare", name: "Parallel Shot", description: "+1 primary projectile per stack (max +3), in a 7\xB0 fan. Each projectile deals 75% damage while installed. One ammo or normal mana cost per volley.", tags: ["projectile"], max: 3, icon: "\u22EE", effects: [e("extraProjectiles", "add", 1)] },
  { id: "echo-strike", rarity: "epic", name: "Echo Strike", description: "+1 follow-up melee sweep per stack (max 2), each at 45% damage and impulse. +5 stamina per sweep; one hit per target per sweep. Extends recovery.", tags: ["melee"], max: 2, icon: "\u3003", effects: [e("extraStrikes", "add", 1)] },
  { id: "magazine", rarity: "common", name: "Magazine Bank", description: "+25% base magazine capacity per stack (rounded up). Applies to every gun, including Machine Gun. Reload fills the expanded magazine.", tags: ["ranged"], max: 8, icon: "\u25A5", effects: [e("ammoCapacity", "add", 0.25)] },
  { id: "mana", rarity: "common", name: "Mana Reservoir", description: "+25 maximum mana per stack. Spells use mana; regeneration stays 12 mana/s.", tags: ["magic"], max: 8, icon: "\u25C8", effects: [e("maxMana", "add", 25)] },
  { id: "stamina", rarity: "common", name: "Stamina Cell", description: "+25 maximum stamina per stack. Melee attacks and defenses use stamina; regenerates 24/s after 0.45 s without spending it.", tags: ["melee"], max: 8, icon: "\u223F", effects: [e("maxStamina", "add", 25)] },
  { id: "velocity", rarity: "common", name: "Velocity Coil", description: "\xD71.25 projectile launch speed per stack (max 4). Flatter flight and farther travel before landing; gravity is unchanged. Split fragments inherit speed and fall, but cannot home.", tags: ["projectile"], max: 4, icon: "\u21E2", effects: [e("projectileSpeed", "mul", 1.25)] },
  { id: "homing", rarity: "epic", name: "Tracking Circuit", description: "+35\xB0/s homing turn rate per stack (max 105\xB0/s). Direct projectiles acquire a visible opponent within 16 m and a 50\xB0 cone. Cover still blocks shots.", tags: ["projectile"], max: 3, icon: "\u25CE", effects: [e("homing", "add", 35)] },
  { id: "fire-rate", rarity: "common", name: "Rapid Cycle", description: "\xD71.15 gun fire rate per stack, including burst cadence. Reload speed is unchanged. Multiplicative; combines with Overclock.", tags: ["ranged"], max: 6, icon: "\xBB", effects: [e("attackRate", "mul", 1.15)] },
  { id: "cast-rate", rarity: "common", name: "Spell Clock", description: "\xD71.15 spell casting rate per stack. Mana cost per cast is unchanged. Multiplicative; combines with Overclock.", tags: ["magic"], max: 6, icon: "\u03DF", effects: [e("attackRate", "mul", 1.15)] },
  { id: "swing-rate", rarity: "common", name: "Flurry Clock", description: "\xD71.15 melee swing rate per stack, including windup and follow-up sweeps. Stamina cost is unchanged. Multiplicative; combines with Overclock.", tags: ["melee"], max: 6, icon: "\u2571", effects: [e("attackRate", "mul", 1.15)] },
  { id: "block-recharge", rarity: "common", name: "Rebound Capacitor", description: `Block cooldown \xD7${CONFIG.blockCooldownChip} per copy (max 5). Base ${CONFIG.blockCooldown}s; two copies = ${(CONFIG.blockCooldown * CONFIG.blockCooldownChip ** 2).toFixed(2)}s. Minimum ${CONFIG.blockMinimumCooldown}s. Hold does not auto-repeat.`, tags: ["all"], max: 5, icon: "\u21BB", effects: [e("blockCooldown", "mul", CONFIG.blockCooldownChip)] },
  { id: "block-window", rarity: "common", name: "Phase Buffer", description: `+${CONFIG.blockWindowChip.toFixed(2)}s Block window per copy (max 4). Base ${CONFIG.blockDuration.toFixed(2)}s; maximum with this Chip ${(CONFIG.blockDuration + CONFIG.blockWindowChip * 4).toFixed(2)}s. No attacks during Block.`, tags: ["all"], max: 4, icon: "\u25C8", effects: [e("blockWindow", "add", CONFIG.blockWindowChip)] },
  { id: "block-return", rarity: "uncommon", name: "Return Amplifier", description: `Reflected projectile damage \xD7${CONFIG.blockReflectDamageChip} and speed \xD7${CONFIG.blockReflectSpeedChip.toFixed(2)} per copy (max 4). Per-projectile limits: ${CONFIG.maxReflectedDamage} damage, ${CONFIG.maxReflectedSpeed} m/s, ${CONFIG.maxReflections} reflections; the next Block absorbs it.`, tags: ["all"], max: 4, icon: "\u21B6", effects: [e("reflectDamage", "mul", CONFIG.blockReflectDamageChip), e("reflectSpeed", "mul", CONFIG.blockReflectSpeedChip)] },
  { id: "block-pulse", rarity: "rare", name: "Counter Pulse", description: `First successful Block per activation emits a ${CONFIG.blockPulseRadius}m pulse: +${CONFIG.blockPulseDamageChip} damage and +${CONFIG.blockPulseImpulseChip} impulse per copy (max 4), reduced by distance. Pushes loose props; cover blocks it. No elemental or recursive procs.`, tags: ["all"], max: 4, icon: "\u25C9", effects: [e("blockPulseDamage", "add", CONFIG.blockPulseDamageChip), e("blockPulseImpulse", "add", CONFIG.blockPulseImpulseChip)] },
  { id: "fleet", rarity: "common", name: "Fleet Feet", description: "\xD71.12 movement speed; \xD70.94 maximum health per copy.", tags: ["all"], max: 5, icon: "\u2197", effects: [e("speed", "mul", 1.12), e("hp", "mul", 0.94)] },
  { id: "hard-hitter", rarity: "common", name: "Heavy Hitter", description: "\xD71.18 damage; \xD70.93 attack rate per copy.", tags: ["all"], max: 5, icon: "\u25C6", effects: [e("damage", "mul", 1.18), e("attackRate", "mul", 0.93)] },
  { id: "quick-loader", rarity: "common", name: "Quick Loader", description: "\xD70.84 reload duration per copy. Minimum 0.2 s.", tags: ["ranged"], max: 5, icon: "\u21BB", effects: [e("reloadSpeed", "mul", 0.84)] },
  { id: "efficient", rarity: "common", name: "Efficient Channels", description: "\xD70.86 mana cost per cast, including secondary spells.", tags: ["magic"], max: 5, icon: "\u25C7", effects: [e("resourceCost", "mul", 0.86)] },
  { id: "footwork", rarity: "common", name: "Efficient Footwork", description: "\xD70.86 stamina cost for attacks and secondary skills. Held shield drain is unchanged.", tags: ["melee"], max: 5, icon: "\u2301", effects: [e("resourceCost", "mul", 0.86)] },
  { id: "spring", rarity: "common", name: "Spring Soles", description: "\xD71.18 jump launch speed per copy.", tags: ["all"], max: 3, icon: "\u2191", effects: [e("jumpPower", "mul", 1.18)] },
  { id: "focus", rarity: "common", name: "Deep Focus", description: "\xD71.3 mana regeneration; \xD70.96 movement speed per copy.", tags: ["magic"], max: 4, icon: "\u2727", effects: [e("manaRegen", "mul", 1.3), e("speed", "mul", 0.96)] },
  { id: "second-wind", rarity: "common", name: "Second Wind", description: "\xD71.3 stamina regeneration after the usual recovery delay.", tags: ["melee"], max: 4, icon: "\u2248", effects: [e("staminaRegen", "mul", 1.3)] },
  { id: "broadside", rarity: "uncommon", name: "Broadside", description: "+2 projectiles, +18\xB0 volley fan; \xD70.75 damage. Volleys also use the normal 75% per-projectile damage rule.", tags: ["projectile"], max: 2, icon: "\u22EE", effects: [e("extraProjectiles", "add", 2), e("fanDegrees", "add", 18), e("damage", "mul", 0.75)] },
  { id: "dense-core", rarity: "uncommon", name: "Dense Core", description: "\xD71.7 projectile radius; \xD70.85 launch speed; \xD71.7 impulse.", tags: ["projectile"], max: 3, icon: "\u25CF", effects: [e("projectileSize", "mul", 1.7), e("projectileSpeed", "mul", 0.85), e("impulse", "mul", 1.7)] },
  { id: "feather", rarity: "uncommon", name: "Feather Rounds", description: "\xD70.45 projectile gravity; \xD70.9 damage per copy.", tags: ["projectile"], max: 3, icon: "\u2303", effects: [e("projectileGravity", "mul", 0.45), e("damage", "mul", 0.9)] },
  { id: "lobber", rarity: "rare", name: "Lobber", description: "\xD72.2 projectile gravity; \xD71.45 damage; \xD71.5 projectile radius.", tags: ["projectile"], max: 2, icon: "\u2312", effects: [e("projectileGravity", "mul", 2.2), e("damage", "mul", 1.45), e("projectileSize", "mul", 1.5)] },
  { id: "banker", rarity: "rare", name: "Bank Shot", description: "+2 bounces. +0.2 bounce damage retention (base 0.7; two copies retain 1.1\xD7).", tags: ["projectile"], max: 2, icon: "\u2194", effects: [e("bounces", "add", 2), e("bounceRetention", "add", 0.2)] },
  { id: "buckshot", rarity: "epic", name: "Pocket Shotgun", description: "+5 pellets in a 38\xB0 fan; \xD70.65 damage, \xD70.6 fire rate. Every pellet also uses the normal 75% volley multiplier.", tags: ["ranged"], max: 1, icon: "\u2059", effects: [e("extraProjectiles", "add", 5), e("fanDegrees", "set", 38), e("damage", "mul", 0.65), e("attackRate", "mul", 0.6)] },
  { id: "leech", rarity: "rare", name: "Leech Circuit", description: "Heal for 12% of direct health damage dealt per copy. Cannot heal past maximum HP; no splash or DoT drain.", tags: ["all"], max: 3, icon: "\u2665", effects: [e("lifeSteal", "add", 0.12)] },
  { id: "executioner", rarity: "epic", name: "Executioner", description: "+50% direct damage per copy to opponents already below 30% health.", tags: ["all"], max: 2, icon: "\xD7", effects: [e("execute", "add", 0.5)] },
  { id: "brawler", rarity: "uncommon", name: "Brawler", description: "+25% direct damage per copy while the opponent is within 5 m of you.", tags: ["all"], max: 3, icon: "\u270A", effects: [e("closeDamage", "add", 0.25)] },
  { id: "marksman", rarity: "rare", name: "Horizon Hunter", description: "+35% direct damage per copy when the opponent is at least 20 m away from you.", tags: ["projectile"], max: 3, icon: "\u2316", effects: [e("farDamage", "add", 0.35)] },
  { id: "backstab", rarity: "rare", name: "Blind Spot", description: "+60% direct damage per copy when striking from behind the opponent.", tags: ["melee"], max: 2, icon: "\u25C0", effects: [e("backstab", "add", 0.6)] },
  { id: "thorns", rarity: "rare", name: "Spite Plating", description: "Return 15% of actual direct health damage to the attacker per copy. Cannot recursively trigger thorns.", tags: ["all"], max: 3, icon: "\u2733", effects: [e("thorns", "add", 0.15)] },
  { id: "repair", rarity: "uncommon", name: "Repair Nanites", description: "Regenerate 2 HP/s per copy after 3 seconds without taking damage.", tags: ["all"], max: 4, icon: "+", effects: [e("regeneration", "add", 2)] },
  { id: "med-loader", rarity: "rare", name: "Medic Magazine", description: "Heal 7 HP per completed reload per copy. \xD71.12 reload duration; firing at least one round is required.", tags: ["ranged"], max: 3, icon: "\u271A", effects: [e("reloadHeal", "add", 7), e("reloadSpeed", "mul", 1.12)] },
  { id: "last-word", rarity: "epic", name: "Last Word", description: "+65% projectile damage per copy when the shot empties your magazine. Applies to a volley; bursts check each emitted shot.", tags: ["ranged"], max: 2, icon: "!", effects: [e("lastRound", "add", 0.65)] },
  { id: "block-medic", rarity: "uncommon", name: "Field Dressing", description: "First successful Block per activation restores 9 HP per copy.", tags: ["all"], max: 3, icon: "\u271A", effects: [e("blockHeal", "add", 9)] },
  { id: "block-loader", rarity: "rare", name: "Catch and Load", description: "First successful Block per activation restores 1 round per copy, up to magazine capacity.", tags: ["ranged"], max: 3, icon: "\u21B6", effects: [e("blockAmmo", "add", 1)] },
  { id: "rebound", rarity: "uncommon", name: "Rebound Engine", description: "\xD71.75 melee Block retaliation impulse per copy. Harder-hitting melee attacks rebound farther.", tags: ["all"], max: 4, icon: "\u21E0", effects: [e("blockForce", "mul", 1.75)] },
  { id: "bulldozer", rarity: "rare", name: "Bulldozer", description: "\xD73.2 attack impulse; \xD70.85 damage per copy. Throw opponents and loose cover around.", tags: ["all"], max: 3, icon: "\u226B", effects: [e("impulse", "mul", 3.2), e("damage", "mul", 0.85)] },
  { id: "anchor", rarity: "uncommon", name: "Anchor Boots", description: "\xD70.5 received knockback; \xD70.88 movement speed; \xD70.9 jump speed.", tags: ["all"], max: 3, icon: "\u22A5", effects: [e("knockbackTaken", "mul", 0.5), e("speed", "mul", 0.88), e("jumpPower", "mul", 0.9)] },
  { id: "blast-dash", rarity: "epic", name: "Blast Dash", description: "Starting a dash creates a 3 m pulse: 14 damage and 380 impulse per copy, with distance falloff and line of sight.", tags: ["all"], max: 2, icon: "\u2739", effects: [e("dashBlast", "add", 14)] },
  { id: "sweeper", rarity: "uncommon", name: "Sweeper", description: "\xD71.3 melee sweep arc per copy (up to 360\xB0). \xD70.95 attack rate.", tags: ["melee"], max: 3, icon: "\u2312", effects: [e("meleeArc", "mul", 1.3), e("attackRate", "mul", 0.95)] },
  { id: "moonboots", rarity: "epic", name: "Moon Boots", description: "Personal gravity \xD70.3; jump speed \xD70.8. Float through long jumps; props keep normal gravity.", tags: ["all"], max: 1, icon: "\u263E", effects: [e("playerGravity", "mul", 0.3), e("jumpPower", "mul", 0.8)] },
  { id: "kickback", rarity: "rare", name: "Kickback Thruster", description: "Firing pushes you opposite your aim by 5 m/s per copy. Aim down to launch yourself; once per volley.", tags: ["projectile"], max: 2, icon: "\u21E4", effects: [e("recoil", "add", 5)] },
  { id: "lucky", rarity: "rare", name: "Lucky Circuit", description: "+15 percentage points critical chance per copy (maximum 75%). Direct hits can deal double damage.", tags: ["all"], max: 3, icon: "\u2726", effects: [e("critical", "add", 0.15)] },
  { id: "vacuum", rarity: "legendary", name: "Vacuum Impact", description: "Each direct attack impact pulls nearby opponents and loose props toward it within 5 m. 1400 impulse, distance falloff and line of sight; once per attack.", tags: ["all"], max: 1, icon: "\u25CE", effects: [e("vacuum", "add", 1400)] },
  { id: "pong", rarity: "mythic", name: "Pong Shot", description: "One persistent shot at fixed 40% base speed, no gravity, unlimited full-damage bounces including arena edges. Next shot replaces it, even if reflected. Overrides volleys, splits, homing and returning flight; Block can still absorb it after 3 returns.", tags: ["ranged"], max: 1, icon: "\u2194", effects: [e("pong", "set", 1)] },
  { id: "boomerang", rarity: "legendary", name: "Return to Sender", description: "Shots fly without gravity and turn back after 24 m or 0.65 s, whichever comes first, at 240\xB0/s. Catch an original shot to recover 1 ammo or half its base mana cost. Once per volley; return speed caps at 60 m/s, flight at 4 s / 320 m. Cover can destroy it.", tags: ["projectile"], max: 1, icon: "\u21A9", effects: [e("boomerang", "set", 1)] },
  { id: "zigzag", rarity: "epic", name: "Sidewinder", description: "Shots weave left and right in a 6 Hz pattern, with swept collisions along their curved path.", tags: ["projectile"], max: 1, icon: "\u223F", effects: [e("zigzag", "set", 1)] },
  { id: "chaos", rarity: "mythic", name: "Dice Reactor", description: "Each direct hit rolls \xD70.5 to \xD72.5 damage on the authority. +20% critical chance. Every hit is a gamble.", tags: ["all"], max: 1, icon: "\u2684", effects: [e("chaos", "set", 1), e("critical", "add", 0.2)] },
  { id: "glass", rarity: "legendary", name: "Glass Reactor", description: "\xD72.4 damage, \xD70.45 maximum health. A dangerous all-or-nothing build.", tags: ["all"], max: 1, icon: "\u25C7", effects: [e("damage", "mul", 2.4), e("hp", "mul", 0.45)] },
  { id: "fortress", rarity: "legendary", name: "Walking Fortress", description: "\xD71.8 maximum health, \xD70.7 incoming damage and \xD70.35 knockback received; \xD70.65 movement speed.", tags: ["all"], max: 1, icon: "\u25A3", effects: [e("hp", "mul", 1.8), e("damageTaken", "mul", 0.7), e("knockbackTaken", "mul", 0.35), e("speed", "mul", 0.65)] },
  { id: "meteor", rarity: "mythic", name: "Meteor Foundry", description: "\xD72.5 damage, \xD73 projectile gravity, \xD73 projectile radius, \xD70.45 cast rate, \xD71.8 mana cost; +24 impact blast damage.", tags: ["magic"], max: 1, icon: "\u2604", effects: [e("damage", "mul", 2.5), e("projectileGravity", "mul", 3), e("projectileSize", "mul", 3), e("attackRate", "mul", 0.45), e("resourceCost", "mul", 1.8), e("shrapnel", "add", 24)] },
  { id: "hurricane", rarity: "mythic", name: "Hurricane Edge", description: "A full 360\xB0 sweep, +2 echo sweeps, +35% base reach, \xD72.8 impulse and \xD70.6 damage. Echoes retain their extra stamina cost and 45% damage.", tags: ["melee"], max: 1, icon: "\u25CE", effects: [e("meleeArc", "set", 8), e("extraStrikes", "add", 2), e("reach", "add", 0.35), e("impulse", "mul", 2.8), e("damage", "mul", 0.6)] },
  { id: "supernova", rarity: "mythic", name: "Supernova Guard", description: "First successful Block per activation, including bullets and spells, explodes around YOU in a 3 m pulse: +45 base damage and +1800 impulse, reduced by distance; cover blocks it. +0.1 s Block window, but \xD71.5 Block cooldown.", tags: ["all"], max: 1, icon: "\u273A", effects: [e("blockPulseDamage", "add", 45), e("blockPulseImpulse", "add", 1800), e("blockWindow", "add", 0.1), e("blockCooldown", "mul", 1.5)] },
  { id: "fireworks", rarity: "legendary", name: "Firework Factory", description: "+3 Splitter levels (five fragments at base), +12 impact blast damage, \xD71.5 radius. Fragments deal 22% damage and never recursively split.", tags: ["projectile"], max: 1, icon: "\u2733", effects: [e("split", "add", 3), e("shrapnel", "add", 12), e("projectileSize", "mul", 1.5)] },
  { id: "vampire", rarity: "mythic", name: "Vampire King", description: "Drain 50% of direct health damage, +50 max HP; \xD70.7 damage and \xD70.8 resource regeneration.", tags: ["all"], max: 1, icon: "\u2665", effects: [e("lifeSteal", "add", 0.5), e("hp", "add", 50), e("damage", "mul", 0.7), e("manaRegen", "mul", 0.8), e("staminaRegen", "mul", 0.8)] },
  { id: "pinball", rarity: "legendary", name: "Pinball Wizard", description: "+8 bounces; each bounce retains full damage before other retention Chips. \xD70.65 projectile gravity, \xD72.4 impulse.", tags: ["projectile"], max: 1, icon: "\u2301", effects: [e("bounces", "add", 8), e("bounceRetention", "add", 0.3), e("projectileGravity", "mul", 0.65), e("impulse", "mul", 2.4)] },
  { id: "rail", rarity: "epic", name: "Rail Driver", description: "\xD71.8 launch speed; \xD71.6 damage; \xD70.65 fire rate; \xD70.5 magazine capacity (rounded up).", tags: ["ranged"], max: 1, icon: "\u2192", effects: [e("projectileSpeed", "mul", 1.8), e("damage", "mul", 1.6), e("attackRate", "mul", 0.65), e("ammoCapacity", "mul", 0.5)] },
  { id: "torrent", rarity: "epic", name: "Torrent Engine", description: "+3 parallel projectiles, \xD70.6 spell damage, \xD71.25 mana cost. The normal 75% volley multiplier also applies.", tags: ["magic"], max: 1, icon: "\u224B", effects: [e("extraProjectiles", "add", 3), e("damage", "mul", 0.6), e("resourceCost", "mul", 1.25)] },
  { id: "duelist", rarity: "rare", name: "Duelist", description: "\xD71.2 damage, \xD71.15 attack rate, \xD70.9 received damage; \u221210 maximum HP per copy.", tags: ["melee"], max: 2, icon: "\u2573", effects: [e("damage", "mul", 1.2), e("attackRate", "mul", 1.15), e("damageTaken", "mul", 0.9), e("hp", "add", -10)] },
  { id: "juggernaut", rarity: "epic", name: "Juggernaut", description: "+40 max HP, \xD72.5 impulse, \xD70.8 received knockback; \xD70.85 attack rate.", tags: ["melee"], max: 2, icon: "\u25A3", effects: [e("hp", "add", 40), e("impulse", "mul", 2.5), e("knockbackTaken", "mul", 0.8), e("attackRate", "mul", 0.85)] },
  { id: "needle", rarity: "rare", name: "Needle Storm", description: "+2 parallel shots, \xD71.2 projectile speed, \xD70.7 projectile radius and \xD70.65 damage. Normal volley penalty also applies.", tags: ["projectile"], max: 2, icon: "\u22EE", effects: [e("extraProjectiles", "add", 2), e("projectileSpeed", "mul", 1.2), e("projectileSize", "mul", 0.7), e("damage", "mul", 0.65)] },
  { id: "plague", rarity: "epic", name: "Plague Engine", description: "+8 poison DPS for 4 s, refreshed on direct hits; \xD70.8 direct damage.", tags: ["all"], max: 2, icon: "\u2623", effects: [e("poison", "add", 8), e("damage", "mul", 0.8)] },
  { id: "inferno", rarity: "epic", name: "Inferno Coil", description: "+10 burn DPS for 2 s, refreshed on direct hits; \xD71.5 attack impulse.", tags: ["all"], max: 2, icon: "\u2668", effects: [e("burn", "add", 10), e("impulse", "mul", 1.5)] },
  { id: "tesla", rarity: "epic", name: "Tesla Rhythm", description: "Discharge every 3 direct hits for +18 base electrical damage within 3 m per copy. Replaces the four-hit cadence.", tags: ["all"], max: 2, icon: "\u03DF", effects: [e("staticEvery", "set", 3), e("staticDamage", "add", 18)] },
  { id: "wrecking", rarity: "rare", name: "Wrecking Crew", description: "\xD72.5 structural damage, \xD72.4 outgoing impulse; \xD70.85 attack rate.", tags: ["all"], max: 2, icon: "\u2317", effects: [e("demolition", "mul", 2.5), e("impulse", "mul", 2.4), e("attackRate", "mul", 0.85)] },
  { id: "recycler", rarity: "uncommon", name: "Scrap Knight", description: "\xD70.6 debris damage received, +10 max HP; \xD70.97 movement speed.", tags: ["all"], max: 3, icon: "\u2B21", effects: [e("debrisTaken", "mul", 0.6), e("hp", "add", 10), e("speed", "mul", 0.97)] },
  { id: "quickguard", rarity: "rare", name: "Quick Guard", description: "\xD70.7 Block cooldown; \u22120.04 s Block window. Timing matters (window minimum 0.1 s).", tags: ["all"], max: 2, icon: "\u25C8", effects: [e("blockCooldown", "mul", 0.7), e("blockWindow", "add", -0.04)] },
  { id: "bulwark", rarity: "epic", name: "Bulwark Timing", description: "+0.18 s Block window, \xD71.8 melee retaliation impulse, \xD71.2 Block cooldown.", tags: ["all"], max: 2, icon: "\u25A3", effects: [e("blockWindow", "add", 0.18), e("blockForce", "mul", 1.8), e("blockCooldown", "mul", 1.2)] },
  { id: "counterweight", rarity: "rare", name: "Counterweight", description: "\xD71.4 reflected damage; \xD71.4 melee retaliation impulse; \xD70.9 incoming attack impulse. Reflection safety limits still apply.", tags: ["all"], max: 3, icon: "\u21CB", effects: [e("reflectDamage", "mul", 1.4), e("blockForce", "mul", 1.4), e("knockbackTaken", "mul", 0.9)] },
  { id: "skirmisher", rarity: "uncommon", name: "Skirmisher", description: "\xD70.8 dash cooldown and \xD71.08 movement speed; \xD70.92 maximum HP.", tags: ["all"], max: 3, icon: "\u2197", effects: [e("dashCooldown", "mul", 0.8), e("speed", "mul", 1.08), e("hp", "mul", 0.92)] },
  { id: "reservoir", rarity: "rare", name: "Arcane Battery", description: "+60 maximum mana and \xD71.15 mana regeneration; \xD70.93 movement speed.", tags: ["magic"], max: 2, icon: "\u25C8", effects: [e("maxMana", "add", 60), e("manaRegen", "mul", 1.15), e("speed", "mul", 0.93)] },
  { id: "endurance", rarity: "rare", name: "Marathon Cell", description: "+60 maximum stamina and \xD71.15 stamina regeneration; \xD70.93 movement speed.", tags: ["melee"], max: 2, icon: "\u223F", effects: [e("maxStamina", "add", 60), e("staminaRegen", "mul", 1.15), e("speed", "mul", 0.93)] },
  { id: "drum", rarity: "uncommon", name: "Drum Magazine", description: "+60% base magazine capacity; \xD71.2 reload duration.", tags: ["ranged"], max: 3, icon: "\u25A5", effects: [e("ammoCapacity", "add", 0.6), e("reloadSpeed", "mul", 1.2)] },
  { id: "chamber", rarity: "legendary", name: "Single Chamber", description: "\xD70.15 magazine capacity (minimum 1, rounded up), \xD72.0 damage, \xD70.6 reload duration. Braced shot still needs 2 rounds.", tags: ["ranged"], max: 1, icon: "\u2160", effects: [e("ammoCapacity", "mul", 0.15), e("damage", "mul", 2), e("reloadSpeed", "mul", 0.6)] },
  { id: "sniper-focus", rarity: "rare", name: "Dead Calm", description: "\xD71.3 damage and \xD71.25 launch speed; \xD70.82 fire rate.", tags: ["ranged"], max: 2, icon: "\u2316", effects: [e("damage", "mul", 1.3), e("projectileSpeed", "mul", 1.25), e("attackRate", "mul", 0.82)] },
  { id: "siphon", rarity: "uncommon", name: "Mana Siphon", description: "Drain 8% of direct health damage; \xD70.9 mana cost per copy.", tags: ["magic"], max: 3, icon: "\u2661", effects: [e("lifeSteal", "add", 0.08), e("resourceCost", "mul", 0.9)] },
  { id: "bloodblade", rarity: "epic", name: "Bloodblade", description: "Drain 25% of direct health damage; +20% base reach; \xD71.15 stamina cost.", tags: ["melee"], max: 2, icon: "\u2665", effects: [e("lifeSteal", "add", 0.25), e("reach", "add", 0.2), e("resourceCost", "mul", 1.15)] },
  { id: "rubber", rarity: "epic", name: "Rubber Soul", description: "\xD72 jump speed and \xD72.8 attack impulse; \xD71.5 received knockback. Everyone goes flying.", tags: ["all"], max: 1, icon: "\u2195", effects: [e("jumpPower", "mul", 2), e("impulse", "mul", 2.8), e("knockbackTaken", "mul", 1.5)] },
  { id: "comet", rarity: "legendary", name: "Comet Dash", description: "Dash pulse: +30 damage and corresponding impulse, \xD70.6 dash cooldown, \xD70.6 personal gravity. Mind the arena edge.", tags: ["all"], max: 1, icon: "\u2604", effects: [e("dashBlast", "add", 30), e("dashCooldown", "mul", 0.6), e("playerGravity", "mul", 0.6)] },
  { id: "repulsor", rarity: "mythic", name: "Repulsor Crown", description: "\xD76 attack impulse and \xD73 melee Block retaliation; \xD70.65 damage. Become the ring-out specialist.", tags: ["all"], max: 1, icon: "\u21C8", effects: [e("impulse", "mul", 6), e("blockForce", "mul", 3), e("damage", "mul", 0.65)] },
  ...CHAOS_CHIPS,
  ...MISHIMA_CHIPS,
  ...ARCADE_CHIPS,
  ...LEET_CHIPS
];

// src/shared/lobby-presentation.ts
var LOBBY_POSES = { weapon: "Standing with weapon", chair: "Sitting on chair", sleep: "Sleeping", crossed: "Arms crossed" };
function lobbyPose(value) {
  return typeof value === "string" && Object.hasOwn(LOBBY_POSES, value) ? value : "weapon";
}
var PREFERRED_WEAPONS = Object.values(WEAPONS).filter((w) => !w.devOnly);

// src/shared/cosmetics.ts
var HATS = { "auditor-halo": "Auditor Halo \xB7 DEV ONLY", none: "No hat", cap: "Ball cap", beanie: "Beanie", top: "Top hat", wizard: "Wizard hat", crown: "Crown", cone: "Traffic cone", antenna: "Antenna", halo: "Halo", cowboy: "Cowboy hat" };
var KILL_STYLES = { ragdoll: "Ragdoll", "dev-raging-demon": "Raging Demon \xB7 DEV ONLY", "voxel-burst": "Voxel breakup", confetti: "Confetti cannon", ascension: "Beam me up", shrink: "Pocket-sized", launch: "Team Rocket", ghost: "Ghost escape", "dev-clown": "Clown getaway \xB7 DEV ONLY", "dev-clown-love": "Clown Love \xB7 DEV ONLY", "dev-bass-van": "Bass Van \xB7 DEV ONLY" };
var developerKillEffect = (id) => id.startsWith("dev-");
var DEFAULT_COSMETICS = { lobbyPose: "weapon", intro: "neon-drop", introText: "Ready to make an impact", bodyColor: "#e5e7eb", hat: "none", killEffect: "voxel-burst" };
function cosmetics(raw, dev = false) {
  return { lobbyPose: lobbyPose(raw?.lobbyPose), preferredSpec: typeof raw?.preferredSpec === "string" && Object.hasOwn(WEAPONS, raw.preferredSpec) && !WEAPONS[raw.preferredSpec].devOnly ? raw.preferredSpec : void 0, introText: introText(raw?.introText), intro: raw && Object.hasOwn(INTRO_STYLES, raw.intro) && (!raw.intro.startsWith("dev-") || canUse(dev, "intro:" + raw.intro)) ? raw.intro : "neon-drop", bodyColor: typeof raw?.bodyColor === "string" && /^#[0-9a-f]{6}$/i.test(raw.bodyColor) ? raw.bodyColor : DEFAULT_COSMETICS.bodyColor, hat: raw && Object.hasOwn(HATS, raw.hat) && (raw.hat !== "auditor-halo" || canUse(dev, "hat:auditor-halo")) ? raw.hat : "none", killEffect: raw && Object.hasOwn(KILL_STYLES, raw.killEffect) && (!developerKillEffect(raw.killEffect) || canUse(dev, "effect:" + raw.killEffect)) ? raw.killEffect : "voxel-burst" };
}
function introText(value) {
  return typeof value === "string" ? Array.from(value.replace(/[\u0000-\u001f\u007f-\u009f\u202a-\u202e\u2066-\u2069]/g, " ").replace(/\s+/g, " ").trim()).slice(0, 64).join("") || DEFAULT_COSMETICS.introText : DEFAULT_COSMETICS.introText;
}

// src/relay/accounts.ts
var encoder = new TextEncoder();
var SESSION_MS = 12 * 36e5;
var REMEMBER_MS = 30 * 24 * 36e5;
var bytes = (s) => Uint8Array.from(atob(s.replace(/-/g, "+").replace(/_/g, "/")), (c) => c.charCodeAt(0));
var random = () => base64url(crypto.getRandomValues(new Uint8Array(32)));
var digest = async (s) => base64url(await crypto.subtle.digest("SHA-256", encoder.encode(s)));
var equal = (a, b) => {
  let d = a.length ^ b.length;
  for (let i = 0; i < a.length; i++) d |= a[i] ^ (b[i] || 0);
  return d === 0;
};
var fail = (message, status = 400) => Object.assign(Error(message), { status });
var hashes = 0;
async function passwordHash(password, salt = base64url(crypto.getRandomValues(new Uint8Array(16)))) {
  if (hashes >= 2) throw fail("Login is busy. Try again in a moment.", 503);
  hashes++;
  try {
    return `scrypt$32768$8$3$${salt}$${base64url(await scryptAsync(password, bytes(salt), { N: 32768, r: 8, p: 3, dkLen: 32, maxmem: 40 * 1024 * 1024, asyncTick: 10 }))}`;
  } finally {
    hashes--;
  }
}
async function passwordMatches(password, stored) {
  const p = stored.split("$");
  if (p.length !== 6 || p.slice(0, 4).join("$") !== "scrypt$32768$8$3") return false;
  const computed = await passwordHash(password, p[4]);
  return equal(bytes(computed.split("$")[5]), bytes(p[5]));
}
async function legacyMatches(username, password, env) {
  if (!env.DEV_ACCOUNT_VERIFIER) return false;
  const a = JSON.parse(env.DEV_ACCOUNT_VERIFIER), key = await crypto.subtle.importKey("raw", encoder.encode(password), "PBKDF2", false, ["deriveBits"]);
  const actual = new Uint8Array(await crypto.subtle.deriveBits({ name: "PBKDF2", hash: "SHA-256", salt: bytes(a.salt), iterations: a.iterations }, key, 256));
  return username === a.username.toLowerCase() && equal(actual, bytes(a.hash));
}
function access(row) {
  return { accountId: row.id, developer: row.developer === 1, unlocks: cleanGrants(JSON.parse(row.unlocks)), flags: cleanFlags(JSON.parse(row.flags)) };
}
function savedProfile(raw, a) {
  return { name: typeof raw?.name === "string" ? raw.name.replace(/[<>\u0000-\u001f]/g, "").slice(0, 20) || "Pilot" : "Pilot", class: ["ranged", "melee", "magic"].includes(raw?.class) ? raw.class : "ranged", color: /^#[0-9a-f]{6}$/i.test(raw?.color || "") ? raw.color : "#ff6b35", cosmetics: cosmetics(raw?.cosmetics, a) };
}
function view(row) {
  return { id: row.id, username: row.username, ...access(row), profile: savedProfile(JSON.parse(row.profile), access(row)), hasProfile: row.profile !== "{}", revision: row.revision, grantVersion: row.grant_version };
}
async function sign(row, env, now) {
  const payload = base64url(encoder.encode(JSON.stringify({ kind: "chip-arena-player", user: row.username, ...access(row), iat: now, exp: now + GAME_CLAIM_MS }))), key = await crypto.subtle.importKey("jwk", JSON.parse(env.DEV_SIGNING_KEY), { name: "ECDSA", namedCurve: "P-256" }, false, ["sign"]);
  return payload + "." + base64url(await crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, key, encoder.encode(payload)));
}
async function rate(request, env, now, username, action) {
  const ip = await digest(request.headers.get("CF-Connecting-IP") || "local");
  const sensitive = ["login", "register", "recover"].includes(action);
  const identity = sensitive ? username : await digest(request.headers.get("authorization") || ip);
  for (const [label, limit, window] of [[`account-ip:${sensitive ? "auth" : "api"}:${ip}`, sensitive ? 12 : 180, 6e4], [`account-user:${identity}:${action}`, sensitive ? 15 : 100, 3e5]]) {
    const key = label + ":" + Math.floor(now / window);
    const r = await env.DB.batch([env.DB.prepare("INSERT INTO dev_login_rates(id,hits,expires) VALUES(?,1,?) ON CONFLICT(id) DO UPDATE SET hits=hits+1").bind(key, now + window * 2), env.DB.prepare("SELECT hits FROM dev_login_rates WHERE id=?").bind(key)]);
    if (r[1].results[0].hits > limit) throw fail("Too many requests. Try again in a few minutes.", 429);
  }
}
async function readBody(request) {
  if (request.method === "GET") return {};
  if (!request.headers.get("content-type")?.includes("application/json")) throw fail("Expected JSON.", 415);
  const reader = request.body?.getReader();
  if (!reader) throw fail("Missing request.");
  let size = 0, text = "";
  const decoder = new TextDecoder();
  while (true) {
    const r = await reader.read();
    if (r.done) break;
    size += r.value.length;
    if (size > 8192) {
      await reader.cancel();
      throw fail("Request too large.", 413);
    }
    text += decoder.decode(r.value, { stream: true });
  }
  try {
    const value = JSON.parse(text + decoder.decode());
    if (!value || typeof value !== "object" || Array.isArray(value)) throw Error();
    return value;
  } catch {
    throw fail("Invalid request.");
  }
}
async function accounts(request, env, now = Date.now()) {
  const headers = { "Access-Control-Allow-Origin": "*", "Access-Control-Allow-Headers": "Content-Type, Authorization", "Access-Control-Allow-Methods": "GET, POST, OPTIONS", "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff" };
  const response = (b, status = 200) => Response.json(b, { status, headers });
  if (request.method === "OPTIONS") return new Response(null, { status: 204, headers });
  try {
    if (!["GET", "POST"].includes(request.method)) throw fail("Method not allowed.", 405);
    if (!env.DB || !env.DEV_SIGNING_KEY) throw fail("Account service is temporarily unavailable.", 503);
    const action = new URL(request.url).pathname.replace("/api/accounts/", ""), input = await readBody(request);
    const username = typeof input.username === "string" ? input.username.trim().toLowerCase() : "";
    if (username.length > 32) throw fail("Username is too long.");
    await rate(request, env, now, username || "signed-in", action);
    if (["login", "register", "recover"].includes(action)) {
      if (request.method !== "POST") throw fail("Use the account form.", 405);
      if (!/^[a-z0-9_]{3,20}$/.test(username) || typeof input.password !== "string" || input.password.length > 128 || input.password.length < 1) throw fail("Use a username of 3\u201320 letters, numbers or underscores, and a password.");
      let row2 = await env.DB.prepare("SELECT * FROM player_accounts WHERE username=?").bind(username).first();
      const reserved = env.DEV_ACCOUNT_VERIFIER ? JSON.parse(env.DEV_ACCOUNT_VERIFIER).username.toLowerCase() : "";
      let recoveryCode;
      if (action === "register") {
        if (row2 || username === reserved) throw fail("That username is unavailable.", 409);
        const hash6 = await passwordHash(input.password);
        recoveryCode = random();
        const id = crypto.randomUUID();
        try {
          await env.DB.prepare("INSERT INTO player_accounts(id,username,password,recovery,created,updated) VALUES(?,?,?,?,?,?)").bind(id, username, hash6, await digest(recoveryCode), now, now).run();
        } catch {
          throw fail("That username is unavailable.", 409);
        }
        row2 = await env.DB.prepare("SELECT * FROM player_accounts WHERE id=?").bind(id).first();
      } else if (action === "recover") {
        if (typeof input.recoveryCode !== "string" || input.recoveryCode.length > 80) throw fail("Enter your recovery code and a new password.");
        const recovery = await digest(input.recoveryCode.trim());
        if (!row2 || !equal(encoder.encode(recovery), encoder.encode(row2.recovery))) throw fail("Username or recovery code is incorrect.", 401);
        const hash6 = await passwordHash(input.password);
        recoveryCode = random();
        const result2 = await env.DB.batch([env.DB.prepare("UPDATE player_accounts SET password=?,recovery=?,updated=? WHERE id=? AND recovery=?").bind(hash6, await digest(recoveryCode), now, row2.id, recovery), env.DB.prepare("DELETE FROM player_sessions WHERE account_id=? AND EXISTS(SELECT 1 FROM player_accounts WHERE id=? AND password=?)").bind(row2.id, row2.id, hash6)]);
        if (!result2[0].meta.changes) throw fail("This recovery code was already used.", 401);
        row2 = await env.DB.prepare("SELECT * FROM player_accounts WHERE id=?").bind(row2.id).first();
      } else {
        if (!row2 && username === reserved && await legacyMatches(username, input.password, env)) {
          const hash6 = await passwordHash(input.password);
          recoveryCode = random();
          const id = crypto.randomUUID();
          await env.DB.prepare("INSERT INTO player_accounts(id,username,password,recovery,developer,created,updated) VALUES(?,?,?,?,1,?,?) ON CONFLICT(username) DO NOTHING").bind(id, username, hash6, await digest(recoveryCode), now, now).run();
          row2 = await env.DB.prepare("SELECT * FROM player_accounts WHERE username=?").bind(username).first();
          if (row2.id !== id) recoveryCode = void 0;
        } else if (!row2) {
          await passwordHash(input.password, "AAAAAAAAAAAAAAAAAAAAAA");
          throw fail("Username or password is incorrect.", 401);
        }
        if (!row2 || !await passwordMatches(input.password, row2.password)) throw fail("Username or password is incorrect.", 401);
      }
      const session2 = random(), hash5 = await digest(session2), expires = now + (input.remember === true ? REMEMBER_MS : SESSION_MS);
      await env.DB.batch([env.DB.prepare("DELETE FROM player_sessions WHERE hash IN (SELECT hash FROM player_sessions WHERE expires<? LIMIT 100)").bind(now), env.DB.prepare("DELETE FROM dev_login_rates WHERE id IN (SELECT id FROM dev_login_rates WHERE expires<? LIMIT 100)").bind(now), env.DB.prepare("INSERT INTO player_sessions(hash,account_id,expires) VALUES(?,?,?)").bind(hash5, row2.id, expires)]);
      return response({ session: session2, expires, account: view(row2), token: await sign(row2, env, now), recoveryCode });
    }
    const session = request.headers.get("authorization")?.replace(/^Bearer /, "");
    if (!session || !/^[-\w]{43}$/.test(session)) throw fail("Please log in.", 401);
    const hash4 = await digest(session);
    let row = await env.DB.prepare("SELECT a.* FROM player_accounts a JOIN player_sessions s ON s.account_id=a.id WHERE s.hash=? AND s.expires>?").bind(hash4, now).first();
    if (!row) throw fail("Session expired. Please log in again.", 401);
    if (action === "me" && request.method === "GET") return response({ account: view(row), token: await sign(row, env, now) });
    if (request.method !== "POST") throw fail("Method not allowed.", 405);
    if (action === "logout") {
      await env.DB.prepare("DELETE FROM player_sessions WHERE hash=?").bind(hash4).run();
      return response({ ok: true });
    }
    if (action === "profile") {
      if (!Number.isSafeInteger(input.revision)) throw fail("Reload your saved pilot before saving.", 409);
      const profile = savedProfile(input.profile, access(row));
      const r = await env.DB.prepare("UPDATE player_accounts SET profile=?,revision=revision+1,updated=? WHERE id=? AND revision=?").bind(JSON.stringify(profile), now, row.id, input.revision).run();
      if (!r.meta.changes) throw fail("Your pilot changed on another device. Reload your saved pilot before saving again.", 409);
      row = await env.DB.prepare("SELECT * FROM player_accounts WHERE id=?").bind(row.id).first();
      return response({ account: view(row), token: await sign(row, env, now) });
    }
    if (!row.developer) throw fail("Only DEVs can manage registered players.", 403);
    if (action === "players") {
      const query = typeof input.query === "string" ? input.query.trim().toLowerCase() : "";
      if (!/^[a-z0-9_]{2,20}$/.test(query)) throw fail("Enter at least 2 username characters.");
      const r = await env.DB.batch([env.DB.prepare("SELECT * FROM player_accounts WHERE username>=? AND username<? ORDER BY username LIMIT 20").bind(query, query + "\uFFFF")]);
      return response({ players: r[0].results.map(view) });
    }
    if (action === "grant") {
      if (typeof input.accountId !== "string" || !Number.isSafeInteger(input.grantVersion) || !Array.isArray(input.unlocks) || !Array.isArray(input.flags)) throw fail("Invalid player grant.");
      const unlocks = cleanGrants(input.unlocks), flags = cleanFlags(input.flags);
      if (unlocks.length !== input.unlocks.length || flags.length !== input.flags.length || "developer" in input) throw fail("Choose only the available items and flags. DEV roles cannot be granted here.");
      const target = await env.DB.prepare("SELECT * FROM player_accounts WHERE id=?").bind(input.accountId).first();
      if (!target) throw fail("Registered player not found.", 404);
      const auditId = crypto.randomUUID(), detail = JSON.stringify({ before: { unlocks: JSON.parse(target.unlocks), flags: JSON.parse(target.flags) }, after: { unlocks, flags } });
      const r = await env.DB.batch([env.DB.prepare("INSERT INTO account_audit(id,actor,target,action,detail,created) SELECT ?,?,?,?, ?,? WHERE EXISTS(SELECT 1 FROM player_accounts WHERE id=? AND grant_version=?)").bind(auditId, row.id, target.id, "grant", detail, now, target.id, input.grantVersion), env.DB.prepare("UPDATE player_accounts SET unlocks=?,flags=?,grant_version=grant_version+1,updated=? WHERE id=? AND grant_version=?").bind(JSON.stringify(unlocks), JSON.stringify(flags), now, target.id, input.grantVersion)]);
      if (!r[1].meta.changes) throw fail("This player changed. Search again before saving.", 409);
      return response({ account: view(await env.DB.prepare("SELECT * FROM player_accounts WHERE id=?").bind(target.id).first()) });
    }
    throw fail("Unknown account action.", 404);
  } catch (e2) {
    return response({ error: e2.status ? e2.message : "Account service is temporarily unavailable." }, e2.status || 503);
  }
}

// src/shared/network-config.ts
var NETWORK = { maxPendingInputs: 180, maxInputBatch: 60, inputQueueLimit: 90, correctionDecay: 18, hardSnapDistance: 2.5, interpolationMinMs: 75, interpolationMaxMs: 350, extrapolationMaxMs: 80 };

// src/shared/relay-protocol.ts
var RELAY = { protocol: "chip-arena-https-v1", intervalMs: 50, inputHoldSeconds: 0.75, timeoutMs: 12e3, requestTimeoutMs: 4e3, maxEvents: 48, maxHostBytes: 384e3, maxGuestBytes: 24e3, ttlMs: 72e5 };
function normalizeRoomCode(value) {
  const code = value.trim().toUpperCase().replace(/[\s-]/g, "");
  return /^[A-HJ-NP-Z2-9]{8}$/.test(code) ? code : null;
}
function newRoomToken() {
  return [...crypto.getRandomValues(new Uint8Array(32))].map((n) => n.toString(16).padStart(2, "0")).join("");
}
function cleanProfile(p) {
  return { cosmetics: cosmetics(p?.cosmetics, true), authToken: typeof p?.authToken === "string" && p.authToken.length <= 3e3 ? p.authToken : void 0, name: typeof p?.name === "string" ? p.name.replace(/[<>]/g, "").slice(0, 20) || "Pilot" : "Pilot", class: ["ranged", "magic", "melee"].includes(p?.class) ? p.class : "ranged", color: /^#[0-9a-f]{6}$/i.test(p?.color || "") ? p.color : "#97e4d0" };
}
var guestTypes = /* @__PURE__ */ new Set(["ready", "class", "spec", "chip", "ping", "rematch", "lobby"]);
var hostTypes = /* @__PURE__ */ new Set(["welcome", "error", "notice", "pong", "rematchVotes"]);
function validPacket(p, host) {
  if (!p || !Number.isSafeInteger(p.seq) || p.seq < 1 || !Number.isSafeInteger(p.ack) || p.ack < 0 || !Array.isArray(p.events) || p.events.length > RELAY.maxEvents) return false;
  let id = 0;
  for (const e2 of p.events) {
    if (!Number.isSafeInteger(e2?.id) || e2.id <= id || !e2.message || !(host ? hostTypes : guestTypes).has(e2.message.type)) return false;
    id = e2.id;
    if (JSON.stringify(e2.message).length > 4096) return false;
  }
  if (p.latest !== void 0) {
    if (host) {
      const s = p.latest;
      if (s?.type !== "snapshot" || !s.state || !Number.isFinite(s.state.tick) || !Array.isArray(s.state.players) || s.state.players.length > 8 || !Array.isArray(s.state.props) || s.state.props.length > 300) return false;
    } else {
      const latest = p.latest;
      const inputs = latest?.type === "inputs" ? latest.inputs : latest?.type === "input" ? [latest.input] : null;
      if (!Array.isArray(inputs) || !inputs.length || inputs.length > NETWORK.maxInputBatch) return false;
      if (latest.type === "inputs" && (typeof latest.epoch !== "string" || !/^\d+:\d+:\d+$/.test(latest.epoch))) return false;
      if (inputs.some((i, n) => !i || !["seq", "x", "z", "yaw", "pitch"].every((k) => typeof i[k] === "number" && Number.isFinite(i[k])) || !Number.isSafeInteger(i.seq) || i.seq < 0 || JSON.stringify(i).length > 2e3 || n > 0 && i.seq <= inputs[n - 1].seq)) return false;
    }
  }
  return true;
}

// src/shared/invite-url.ts
function publicInviteOrigin(value) {
  if (typeof value !== "string") return null;
  try {
    const url = new URL(value);
    return url.protocol === "https:" && !url.username && !url.password && !url.port && /^[-a-z0-9]+\.trycloudflare\.com$/.test(url.hostname) && url.pathname === "/" && !url.search && !url.hash ? url.origin : null;
  } catch {
    return null;
  }
}

// src/shared/room-directory.ts
var DIRECTORY_TTL_MS = 18e4;

// src/relay/directory.ts
var json = (body2, status = 200) => Response.json(body2, { status, headers: { "Cache-Control": "no-store", "Access-Control-Allow-Origin": "*", "X-Content-Type-Options": "nosniff" } });
var hash = async (s) => [...new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(s)))].map((n) => n.toString(16).padStart(2, "0")).join("");
async function directory(request, db, now = Date.now()) {
  if (!db) return json({ error: "Room lookup is temporarily unavailable. Use an invite link." }, 503);
  const url = new URL(request.url), raw = url.pathname.slice("/api/directory/".length), code = normalizeRoomCode(raw);
  if (!code) return json({ error: "Enter the complete room code from your host." }, 400);
  if (request.method === "GET") {
    const row2 = await db.prepare("SELECT origin,expires FROM online_rooms WHERE code=? AND expires>?").bind(code, now).first();
    if (!row2) return json({ error: "Room code expired or was not found. Ask the host to keep their room open and copy its code." }, 404);
    return json({ code, origin: row2.origin, expires: row2.expires });
  }
  if (request.method !== "POST") return json({ error: "Method not allowed." }, 405);
  if (request.headers.get("origin") && request.headers.get("origin") !== url.origin) return json({ error: "Room registration must come from the host server." }, 403);
  const token = request.headers.get("authorization")?.replace(/^Bearer /, "");
  if (!/^[a-f0-9]{64}$/.test(token || "")) return json({ error: "Invalid host session." }, 401);
  if (!request.headers.get("content-type")?.includes("application/json")) return json({ error: "Expected JSON." }, 415);
  const reader = request.body?.getReader();
  if (!reader) return json({ error: "Missing registration." }, 400);
  const decoder = new TextDecoder();
  let body2 = "", bytes2 = 0;
  while (true) {
    const r = await reader.read();
    if (r.done) break;
    bytes2 += r.value.length;
    if (bytes2 > 2048) {
      await reader.cancel();
      return json({ error: "Registration too large." }, 413);
    }
    body2 += decoder.decode(r.value, { stream: true });
  }
  body2 += decoder.decode();
  let input;
  try {
    input = JSON.parse(body2);
  } catch {
    return json({ error: "Invalid JSON." }, 400);
  }
  const origin = publicInviteOrigin(input?.origin);
  if (!origin) return json({ error: "Invalid public game address." }, 400);
  const key = await hash(token), creator = await hash(request.headers.get("CF-Connecting-IP") || "local-preview");
  const row = await db.prepare("SELECT host_key,expires FROM online_rooms WHERE code=?").bind(code).first();
  if (row && row.expires > now && row.host_key !== key) return json({ error: "Room code is already in use. Create another room." }, 409);
  if (!row || row.expires <= now) {
    await db.prepare("DELETE FROM online_rooms WHERE code IN (SELECT code FROM online_rooms WHERE expires<=? LIMIT 100)").bind(now).run();
    const count = await db.prepare("SELECT COUNT(*) AS n, SUM(CASE WHEN creator=? THEN 1 ELSE 0 END) AS mine FROM online_rooms WHERE expires>?").bind(creator, now).first();
    if (count.n >= 2048 || count.mine >= 12) return json({ error: "Too many active rooms. Use an invite link or close an old room." }, 429);
  }
  const expires = now + DIRECTORY_TTL_MS;
  const result2 = await db.prepare("INSERT INTO online_rooms(code,host_key,creator,origin,expires) VALUES(?,?,?,?,?) ON CONFLICT(code) DO UPDATE SET origin=excluded.origin,expires=excluded.expires,host_key=excluded.host_key,creator=excluded.creator WHERE online_rooms.host_key=excluded.host_key OR online_rooms.expires<=?").bind(code, key, creator, origin, expires, now).run();
  if (!result2.meta.changes) return json({ error: "Room code is already in use. Create another room." }, 409);
  return json({ code, expires });
}

// src/relay/party.ts
var protocol = "chip-arena-party-v2";
var fail2 = (error, status = 400) => Object.assign(Error(error), { status });
var json2 = (body2, status = 200) => Response.json({ ...body2, protocol }, { status, headers: { "Cache-Control": "no-store" } });
var hash2 = async (s) => [...new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(s)))].map((n) => n.toString(16).padStart(2, "0")).join("");
async function party(request, db, now = Date.now()) {
  if (request.method !== "POST") throw fail2("Method not allowed.", 405);
  const url = new URL(request.url);
  if (request.headers.get("origin") && request.headers.get("origin") !== url.origin) throw fail2("Use this server to connect.", 403);
  const token = request.headers.get("authorization")?.replace(/^Bearer /, "");
  if (!/^[a-f0-9]{64}$/.test(token || "")) throw fail2("Invalid session.", 401);
  const reader = request.body?.getReader();
  if (!reader) throw fail2("Missing request.");
  let bytes2 = 0, text = "";
  const decoder = new TextDecoder();
  while (true) {
    const r = await reader.read();
    if (r.done) break;
    bytes2 += r.value.length;
    if (bytes2 > RELAY.maxHostBytes) {
      await reader.cancel();
      throw fail2("Message too large.", 413);
    }
    text += decoder.decode(r.value, { stream: true });
  }
  text += decoder.decode();
  let input;
  try {
    input = JSON.parse(text);
  } catch {
    throw fail2("Invalid JSON.");
  }
  const code = typeof input.code === "string" ? normalizeRoomCode(input.code) : null;
  if (!code || input.protocol !== protocol) throw fail2("Reload the game and use a current room code.");
  const key = await hash2(token), action = url.pathname.split("/").at(-1);
  let room = await db.prepare("SELECT * FROM party_rooms WHERE code=?").bind(code).first();
  if (action === "create") {
    if (room) {
      if (room.host_key === key && !room.closed && room.expires > now) return json2({ code });
      throw fail2("Code already in use.", 409);
    }
    const creator = await hash2(request.headers.get("CF-Connecting-IP") || "local");
    const count = await db.prepare("SELECT COUNT(*) AS n, SUM(CASE WHEN creator=? THEN 1 ELSE 0 END) AS mine FROM party_rooms WHERE expires>? AND closed=0 AND host_seen>?").bind(creator, now, now - RELAY.timeoutMs).first();
    if (count.n >= 512 || count.mine >= 12) throw fail2("Too many active rooms.", 429);
    await db.batch([db.prepare("DELETE FROM party_seats WHERE code IN (SELECT code FROM party_rooms WHERE expires<? OR closed=1)").bind(now), db.prepare("DELETE FROM party_rooms WHERE expires<? OR closed=1").bind(now), db.prepare("INSERT INTO party_rooms(code,host_key,creator,expires,host_seen) VALUES(?,?,?,?,?)").bind(code, key, creator, now + RELAY.ttlMs, now)]);
    return json2({ code, id: "host" }, 201);
  }
  if (!room || room.closed || room.expires <= now) throw fail2("Room closed or not found.", 404);
  const host = room.host_key === key;
  if (!host && now - room.host_seen > RELAY.timeoutMs) throw fail2("The host is offline.", 410);
  let seat = host ? null : await db.prepare("SELECT * FROM party_seats WHERE code=? AND seat_key=?").bind(code, key).first();
  if (action === "join") {
    if (host) throw fail2("The host already has a seat.", 403);
    if (seat && !seat.has_left) return json2({ code, id: seat.id });
    if (seat?.has_left) throw fail2("This session ended. Join again from the menu.", 410);
    const id = "p" + newRoomToken().slice(0, 16);
    const r = await db.prepare(`INSERT INTO party_seats(id,code,seat_key,profile,seen) SELECT ?,?,?,?,? WHERE EXISTS(SELECT 1 FROM party_rooms WHERE code=? AND phase='lobby' AND closed=0) AND (SELECT COUNT(*) FROM party_seats WHERE code=? AND has_left=0)<?`).bind(id, code, key, JSON.stringify(cleanProfile(input.profile)), now, code, code, CONFIG.maxPlayers - 1).run();
    if (!r.meta.changes) throw fail2("Room is full or the match has started.", 409);
    return json2({ code, id });
  }
  if (!host && (!seat || seat.has_left)) throw fail2("Seat is not authorized.", 403);
  if (action === "leave") {
    await db.prepare(host ? "UPDATE party_rooms SET closed=1 WHERE code=?" : "UPDATE party_seats SET has_left=1 WHERE id=?").bind(host ? code : seat.id).run();
    return json2({ ok: true });
  }
  if (action !== "exchange") throw fail2("Unknown operation.", 404);
  if (host) {
    if (!Number.isSafeInteger(input.seq) || input.seq < 1 || !Array.isArray(input.peers) || input.peers.length > 7) throw fail2("Invalid host packet.");
    if (input.snapshot && !validPacket({ seq: 1, ack: 0, events: [], latest: input.snapshot }, true)) throw fail2("Invalid snapshot.");
    for (const peer of input.peers) if (typeof peer.id !== "string" || !validPacket(peer.packet, true) || peer.packet.latest) throw fail2("Invalid peer packet.");
    const statements = [db.prepare("UPDATE party_rooms SET host_seen=?,host_seq=?,snapshot=COALESCE(?,snapshot),phase=COALESCE(?,phase) WHERE code=? AND host_key=? AND host_seq<=?").bind(now, input.seq, input.snapshot ? JSON.stringify(input.snapshot) : null, input.snapshot?.state?.phase || null, code, key, input.seq)];
    for (const peer of input.peers) statements.push(db.prepare("UPDATE party_seats SET host_packet=? WHERE id=? AND code=? AND EXISTS(SELECT 1 FROM party_rooms WHERE code=? AND host_seq=?)").bind(JSON.stringify(peer.packet), peer.id, code, code, input.seq));
    statements.push(db.prepare("UPDATE party_seats SET has_left=1 WHERE code=? AND seen<?").bind(code, now - RELAY.timeoutMs));
    statements.push(db.prepare("SELECT * FROM party_seats WHERE code=?").bind(code));
    const rows = (await db.batch(statements)).at(-1).results;
    return json2({ serverNow: now, peers: rows.map((p) => ({ id: p.id, profile: JSON.parse(p.profile), left: !!p.has_left, seen: p.seen, packet: p.guest_packet ? JSON.parse(p.guest_packet) : null })) });
  }
  if (bytes2 > RELAY.maxGuestBytes || !validPacket(input.packet, false)) throw fail2("Invalid guest packet.");
  const result2 = await db.batch([db.prepare("UPDATE party_seats SET guest_packet=?,guest_seq=?,seen=? WHERE id=? AND guest_seq<=?").bind(JSON.stringify(input.packet), input.packet.seq, now, seat.id, input.packet.seq), db.prepare("SELECT host_packet FROM party_seats WHERE id=?").bind(seat.id), db.prepare("SELECT snapshot,host_seen,closed FROM party_rooms WHERE code=?").bind(code)]);
  room = result2[2].results[0];
  if (room.closed) throw fail2("Host closed the room.", 410);
  return json2({ serverNow: now, packet: result2[1].results[0].host_packet ? JSON.parse(result2[1].results[0].host_packet) : null, snapshot: room.snapshot ? JSON.parse(room.snapshot) : null });
}

// src/relay/auth.ts
var result = (body2, status = 200) => Response.json(body2, { status, headers: { "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff" } });
var from64 = (s) => Uint8Array.from(atob(s), (c) => c.charCodeAt(0));
async function login(request, env, now = Date.now()) {
  if (request.method !== "POST") return result({ error: "Use the login form." }, 405);
  const origin = request.headers.get("origin");
  if (origin && origin !== new URL(request.url).origin) return result({ error: "Use this game\u2019s login form." }, 403);
  if (!env.DEV_ACCOUNT_VERIFIER || !env.DEV_SIGNING_KEY) return result({ error: "Developer login is not configured." }, 503);
  const text = await request.text();
  if (text.length > 2048) return result({ error: "Login request too large." }, 413);
  let input;
  try {
    input = JSON.parse(text);
  } catch {
    return result({ error: "Invalid login request." }, 400);
  }
  if (typeof input.username !== "string" || typeof input.password !== "string" || input.username.length > 64 || input.password.length > 128) return result({ error: "Enter your username and password." }, 400);
  const ip = request.headers.get("CF-Connecting-IP") || "local";
  const digest2 = base64url(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(ip)));
  for (const [id, limit, window] of [[digest2, 6, 6e4], ["account", 45, 3e5]]) {
    const key2 = id + ":" + Math.floor(now / window);
    await env.DB.prepare("INSERT INTO dev_login_rates(id,hits,expires) VALUES(?,1,?) ON CONFLICT(id) DO UPDATE SET hits=hits+1").bind(key2, now + window * 2).run();
    const count = await env.DB.prepare("SELECT hits FROM dev_login_rates WHERE id=?").bind(key2).first();
    if (count.hits > limit) return result({ error: "Too many attempts. Try again in a few minutes." }, 429);
  }
  await env.DB.prepare("DELETE FROM dev_login_rates WHERE id IN (SELECT id FROM dev_login_rates WHERE expires<? LIMIT 50)").bind(now).run();
  const account = JSON.parse(env.DEV_ACCOUNT_VERIFIER);
  if (await env.DB.prepare("SELECT id FROM player_accounts WHERE username=?").bind(account.username.toLowerCase()).first()) return result({ error: "This DEV account now uses saved accounts. Update the game, then use Log in / Register." }, 409);
  const material = await crypto.subtle.importKey("raw", new TextEncoder().encode(input.password), "PBKDF2", false, ["deriveBits"]);
  const actual = new Uint8Array(await crypto.subtle.deriveBits({ name: "PBKDF2", hash: "SHA-256", salt: from64(account.salt), iterations: account.iterations }, material, 256)), expected = from64(account.hash);
  let difference = actual.length ^ expected.length;
  for (let n = 0; n < actual.length; n++) difference |= actual[n] ^ (expected[n] || 0);
  if (difference || input.username !== account.username) return result({ error: "Username or password is incorrect." }, 401);
  const payload = base64url(new TextEncoder().encode(JSON.stringify({ user: "Sheldon", role: "dev", iat: now, exp: now + 8 * 36e5, nonce: crypto.randomUUID() }))), key = await crypto.subtle.importKey("jwk", JSON.parse(env.DEV_SIGNING_KEY), { name: "ECDSA", namedCurve: "P-256" }, false, ["sign"]);
  const signature = await crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, key, new TextEncoder().encode(payload));
  return result({ token: payload + "." + base64url(signature), user: "Sheldon", expires: now + 8 * 36e5 });
}

// src/shared/connection.ts
function socketEndpoint(value, origin) {
  if (typeof value !== "string" || !value) return null;
  try {
    const base = new URL(origin), url = new URL(value, origin);
    if (url.username || url.password) return null;
    if (value.startsWith("/")) url.protocol = base.protocol === "https:" ? "wss:" : "ws:";
    if (!["ws:", "wss:"].includes(url.protocol) || base.protocol === "https:" && url.protocol !== "wss:") return null;
    return url.toString();
  } catch {
    return null;
  }
}

// src/relay/worker.ts
var json3 = (body2, status = 200) => Response.json(body2, { status, headers: { "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff" } });
var fail3 = (message, status = 400) => Object.assign(new Error(message), { status });
async function hash3(s) {
  return [...new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(s)))].map((n) => n.toString(16).padStart(2, "0")).join("");
}
async function body(request) {
  if (!request.headers.get("content-type")?.includes("application/json")) throw fail3("Expected JSON.", 415);
  const reader = request.body?.getReader();
  if (!reader) throw fail3("Missing request.");
  let size = 0;
  const chunks = [];
  while (true) {
    const r = await reader.read();
    if (r.done) break;
    size += r.value.length;
    if (size > RELAY.maxHostBytes) {
      await reader.cancel();
      throw fail3("Room message is too large.", 413);
    }
    chunks.push(r.value);
  }
  const bytes2 = new Uint8Array(size);
  let at = 0;
  for (const c of chunks) {
    bytes2.set(c, at);
    at += c.length;
  }
  try {
    return JSON.parse(new TextDecoder().decode(bytes2));
  } catch {
    throw fail3("Invalid JSON.");
  }
}
async function exchange(db, input, code, key, host, now) {
  const packet = input.packet;
  if (!validPacket(packet, host)) throw fail3("Invalid room packet.");
  if (!host && JSON.stringify(input).length > RELAY.maxGuestBytes) throw fail3("Guest message is too large.", 413);
  const side = host ? "host" : "guest", other = host ? "guest" : "host";
  const result2 = await db.batch([
    db.prepare(`UPDATE relay_rooms SET ${side}_packet=?,${side}_seq=?,${side}_ack=?,${side}_seen=? WHERE code=? AND ${side}_key=? AND closed=0 AND expires>? AND ${side}_seq<=? AND (${side}_seen<=? OR ${side}_seq=?) ${host ? "" : "AND guest_left=0 AND host_seen>?"} `).bind(JSON.stringify(packet), packet.seq, packet.ack, now, code, key, now, packet.seq, now - 10, packet.seq, ...host ? [] : [now - RELAY.timeoutMs]),
    db.prepare(`SELECT * FROM relay_rooms WHERE code=? AND ${side}_key=?`).bind(code, key)
  ]);
  const row = result2[1].results[0];
  if (!row) throw fail3("Room closed or this seat is not authorized.", 403);
  if (row.closed || row.expires <= now) throw fail3("The host closed this room.", 410);
  if (!host && (row.guest_left || now - row.host_seen > RELAY.timeoutMs)) throw fail3("The host disconnected or this guest session has ended.", 410);
  if (packet.seq > row[side + "_seq"]) throw fail3("Room messages are arriving too quickly.", 429);
  return json3({ protocol: RELAY.protocol, packet: row[other + "_packet"] ? JSON.parse(row[other + "_packet"]) : null, ack: row[other + "_ack"], peerSeen: row[other + "_seen"], serverNow: now, guest: host && row.guest_profile ? JSON.parse(row.guest_profile) : null, guestLeft: row.guest_left === 1 });
}
async function relay(request, env, now = Date.now()) {
  const url = new URL(request.url);
  if (request.method !== "POST") throw fail3("Method not allowed.", 405);
  if (request.headers.get("origin") && request.headers.get("origin") !== url.origin) throw fail3("Use the Chip Arena website to connect.", 403);
  if (!env.DB) throw fail3("Room service is unavailable. Local practice still works.", 503);
  const token = request.headers.get("authorization")?.replace(/^Bearer /, "");
  if (!/^[a-f0-9]{64}$/.test(token || "")) throw fail3("Invalid room session.", 401);
  const input = await body(request), code = typeof input?.code === "string" ? normalizeRoomCode(input.code) : null;
  if (!code || input.protocol !== RELAY.protocol) throw fail3("Reload the game and use a current 8-character room code.", 400);
  const db = env.DB, key = await hash3(token), action = url.pathname.split("/").at(-1);
  if (action === "exchange" && (input.role === "host" || input.role === "guest")) return exchange(db, input, code, key, input.role === "host", now);
  let row = await db.prepare("SELECT * FROM relay_rooms WHERE code=?").bind(code).first();
  if (action === "create") {
    if (row) {
      if (row.host_key === key && !row.closed && row.expires > now) return json3({ code, protocol: RELAY.protocol });
      throw fail3("Room code is taken. Create another room.", 409);
    }
    await db.prepare("DELETE FROM relay_rooms WHERE code IN (SELECT code FROM relay_rooms WHERE expires<? OR closed=1 LIMIT 100)").bind(now).run();
    const creator = await hash3(request.headers.get("CF-Connecting-IP") || "local-preview");
    const count = await db.prepare("SELECT COUNT(*) AS n, SUM(CASE WHEN creator=? THEN 1 ELSE 0 END) AS mine FROM relay_rooms WHERE expires>? AND closed=0 AND host_seen>?").bind(creator, now, now - RELAY.timeoutMs).first();
    if (count.n >= 512 || count.mine >= 12) throw fail3("Too many active rooms. Close an old room and retry.", 429);
    await db.prepare("INSERT INTO relay_rooms(code,host_key,creator,expires,host_seen) VALUES(?,?,?,?,?)").bind(code, key, creator, now + RELAY.ttlMs, now).run();
    return json3({ code, protocol: RELAY.protocol }, 201);
  }
  if (!row || row.closed || row.expires <= now) throw fail3("Room closed or not found. Ask the host for a fresh invite.", 404);
  if (action === "join") {
    if (now - row.host_seen > RELAY.timeoutMs) throw fail3("The host is offline. Ask them to create a new room.", 410);
    if (row.host_key === key) throw fail3("The host cannot join its own guest seat.", 403);
    const result2 = await db.prepare("UPDATE relay_rooms SET guest_key=?,guest_profile=?,guest_seen=? WHERE code=? AND closed=0 AND guest_left=0 AND (guest_key IS NULL OR guest_key=?)").bind(key, JSON.stringify(cleanProfile(input.profile)), now, code, key).run();
    if (!result2.meta.changes) throw fail3("Room is full. Ask the host for a new room.", 409);
    return json3({ code, protocol: RELAY.protocol });
  }
  const host = row.host_key === key;
  if (!host && row.guest_key !== key) throw fail3("This room session is not authorized.", 403);
  if (action === "leave") {
    await db.prepare(host ? "UPDATE relay_rooms SET closed=1 WHERE code=?" : "UPDATE relay_rooms SET guest_left=1 WHERE code=?").bind(code).run();
    return json3({ ok: true });
  }
  if (action !== "exchange") throw fail3("Unknown room operation.", 404);
  return exchange(db, input, code, key, host, now);
}
var worker_default = { async fetch(request, env) {
  if (new URL(request.url).pathname.startsWith("/api/accounts/")) return accounts(request, env);
  if (new URL(request.url).pathname.startsWith("/api/directory/")) {
    try {
      return await directory(request, env.DB);
    } catch {
      return Response.json({ error: "Room lookup is temporarily unavailable. Use an invite link." }, { status: 503, headers: { "Cache-Control": "no-store", "Access-Control-Allow-Origin": "*" } });
    }
  }
  if (new URL(request.url).pathname === "/api/auth/login") {
    try {
      return await login(request, env);
    } catch {
      return json3({ error: "Login temporarily unavailable." }, 503);
    }
  }
  if (new URL(request.url).pathname === "/api/network") {
    const endpoint = socketEndpoint(env.MULTIPLAYER_URL, new URL(request.url).origin);
    return json3({ transport: endpoint ? "websocket" : "https-relay", endpoint });
  }
  if (new URL(request.url).pathname.startsWith("/api/rooms/")) {
    try {
      return new URL(request.url).pathname.startsWith("/api/rooms/v2/") ? await party(request, env.DB) : await relay(request, env);
    } catch (e2) {
      if (!e2.status) console.error("Chip Arena relay failed", e2.message);
      return json3({ error: e2.status ? e2.message : "Room service interrupted. Retrying\u2026" }, e2.status || 503);
    }
  }
  return env.ASSETS ? env.ASSETS.fetch(request) : new Response("Not found", { status: 404 });
} };
export {
  worker_default as default,
  relay
};
/*! Bundled license information:

@noble/hashes/utils.js:
  (*! noble-hashes - MIT License (c) 2022 Paul Miller (paulmillr.com) *)
*/
