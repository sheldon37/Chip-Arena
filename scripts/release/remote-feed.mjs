// Test tooling only. Maps the production updater's feed URLs onto a test channel.
// Production builds never include this file; the real updater and its checksum checks are unchanged.
export function remoteFeed(channelBase, updateBase, fetchImpl = fetch) {
  if (!/^https:\/\/[^\s]+\/$/.test(channelBase)) throw Error('Test channel must be an https URL ending in /');
  return (url, options = {}) => {
    const name = String(url).slice(updateBase.length);
    if (!String(url).startsWith(updateBase) || !(name === 'latest.json' || /^arena-[a-f0-9]{16}\.json\.gz$/.test(name))) throw Error('Test channel refused unexpected URL');
    return fetchImpl(channelBase + name + (name === 'latest.json' ? '?t=' + Date.now() : ''), { ...options, cache: 'no-store' });
  };
}
