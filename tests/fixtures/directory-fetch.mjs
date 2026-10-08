// Test-only mapping: exercise the production Node registration against a local D1 equivalent.
const original=globalThis.fetch;
globalThis.fetch=(input,init)=>original(typeof input==='string'&&input.startsWith('https://chip-arena-prototype.sheldon37.chatgpt.site/api/directory/')?input.replace('https://chip-arena-prototype.sheldon37.chatgpt.site',process.env.CHIP_TEST_DIRECTORY_ORIGIN):input,init);
