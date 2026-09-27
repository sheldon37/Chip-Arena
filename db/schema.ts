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
