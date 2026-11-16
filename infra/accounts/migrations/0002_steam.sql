-- Store unsigned SteamID64 as decimal text. SQLite signed integers and JS Numbers
-- cannot represent the entire identity space exactly.
CREATE TABLE steam_links (
  account_id TEXT PRIMARY KEY NOT NULL REFERENCES player_accounts(id) ON DELETE CASCADE,
  steam_id TEXT NOT NULL UNIQUE CHECK (
    length(steam_id) BETWEEN 1 AND 20 AND steam_id NOT GLOB '*[^0-9]*'
    AND substr(steam_id,1,1) <> '0'
    AND (length(steam_id)<20 OR steam_id<='18446744073709551615')
  ),
  linked_at INTEGER NOT NULL
);
CREATE TABLE steam_ticket_uses (
  digest TEXT PRIMARY KEY NOT NULL,
  used_at INTEGER NOT NULL
);
