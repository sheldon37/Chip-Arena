import {sqliteTable,text,integer,index} from 'drizzle-orm/sqlite-core';
export const relayRooms=sqliteTable('relay_rooms',{
 code:text('code').primaryKey(),hostKey:text('host_key').notNull(),guestKey:text('guest_key'),guestProfile:text('guest_profile'),creator:text('creator').notNull(),expires:integer('expires').notNull(),closed:integer('closed').notNull().default(0),guestLeft:integer('guest_left').notNull().default(0),hostSeen:integer('host_seen').notNull(),guestSeen:integer('guest_seen').notNull().default(0),hostSeq:integer('host_seq').notNull().default(0),guestSeq:integer('guest_seq').notNull().default(0),hostAck:integer('host_ack').notNull().default(0),guestAck:integer('guest_ack').notNull().default(0),hostPacket:text('host_packet'),guestPacket:text('guest_packet')
},t=>[index('relay_expiry').on(t.expires),index('relay_creator').on(t.creator)]);
export const devLoginRates=sqliteTable('dev_login_rates',{id:text('id').primaryKey(),hits:integer('hits').notNull(),expires:integer('expires').notNull()},t=>[index('dev_login_expiry').on(t.expires)]);
export const partyRooms=sqliteTable('party_rooms',{
 code:text('code').primaryKey(),hostKey:text('host_key').notNull(),creator:text('creator').notNull(),expires:integer('expires').notNull(),closed:integer('closed').notNull().default(0),hostSeen:integer('host_seen').notNull(),hostSeq:integer('host_seq').notNull().default(0),phase:text('phase').notNull().default('lobby'),snapshot:text('snapshot')
},t=>[index('party_expiry').on(t.expires)]);
export const partySeats=sqliteTable('party_seats',{
 id:text('id').primaryKey(),code:text('code').notNull(),key:text('seat_key').notNull(),profile:text('profile').notNull(),seen:integer('seen').notNull(),left:integer('has_left').notNull().default(0),guestSeq:integer('guest_seq').notNull().default(0),guestPacket:text('guest_packet'),hostPacket:text('host_packet')
},t=>[index('party_seat_room').on(t.code)]);

export const onlineRooms=sqliteTable('online_rooms',{code:text('code').primaryKey(),hostKey:text('host_key').notNull(),creator:text('creator').notNull(),origin:text('origin').notNull(),expires:integer('expires').notNull()},t=>[index('online_expiry').on(t.expires),index('online_creator').on(t.creator)]);

// Account data lives on the central Site, never inside a friend's game host.
export const playerAccounts=sqliteTable('player_accounts',{
 id:text('id').primaryKey(),username:text('username').notNull().unique(),password:text('password').notNull(),recovery:text('recovery').notNull(),developer:integer('developer').notNull().default(0),unlocks:text('unlocks').notNull().default('[]'),flags:text('flags').notNull().default('[]'),profile:text('profile').notNull().default('{}'),revision:integer('revision').notNull().default(0),grantVersion:integer('grant_version').notNull().default(0),created:integer('created').notNull(),updated:integer('updated').notNull()
});
export const playerSessions=sqliteTable('player_sessions',{
 hash:text('hash').primaryKey(),accountId:text('account_id').notNull().references(()=>playerAccounts.id,{onDelete:'cascade'}),expires:integer('expires').notNull()
},t=>[index('player_session_account').on(t.accountId),index('player_session_expiry').on(t.expires)]);
export const accountAudit=sqliteTable('account_audit',{
 id:text('id').primaryKey(),actor:text('actor').notNull(),target:text('target').notNull(),action:text('action').notNull(),detail:text('detail').notNull(),created:integer('created').notNull()
},t=>[index('account_audit_target').on(t.target,t.created)]);
