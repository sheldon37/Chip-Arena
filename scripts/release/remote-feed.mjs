// Test tooling only. Maps the production updater's feed URLs onto a test channel.
// Production builds never include this file; the real updater and its checksum checks are unchanged.
// Updater v2 adds latest-v2.json and its arena-<16 hex>.pack payload; older launchers never ask for them.
export const FEED_NAME=/^(latest\.json|latest-v2\.json|arena-[a-f0-9]{16}\.json\.gz|arena-[a-f0-9]{16}\.pack)$/;
export function remoteFeed(channelBase, updateBase, fetchImpl = fetch) {
  if (!/^https:\/\/[^\s]+\/$/.test(channelBase)) throw Error('Test channel must be an https URL ending in /');
  return (url, options = {}) => {
    const name = String(url).slice(updateBase.length);
    if (!String(url).startsWith(updateBase) || !FEED_NAME.test(name)) throw Error('Test channel refused unexpected URL');
    return fetchImpl(channelBase + name + (name.endsWith('.json') ? '?t=' + Date.now() : ''), { ...options, cache: 'no-store' });
  };
}
