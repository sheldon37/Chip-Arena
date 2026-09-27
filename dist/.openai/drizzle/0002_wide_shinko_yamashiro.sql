CREATE TABLE `party_rooms` (
	`code` text PRIMARY KEY NOT NULL,
	`host_key` text NOT NULL,
	`creator` text NOT NULL,
	`expires` integer NOT NULL,
	`closed` integer DEFAULT 0 NOT NULL,
	`host_seen` integer NOT NULL,
	`host_seq` integer DEFAULT 0 NOT NULL,
	`phase` text DEFAULT 'lobby' NOT NULL,
	`snapshot` text
);
--> statement-breakpoint
CREATE INDEX `party_expiry` ON `party_rooms` (`expires`);--> statement-breakpoint
CREATE TABLE `party_seats` (
	`id` text PRIMARY KEY NOT NULL,
	`code` text NOT NULL,
	`seat_key` text NOT NULL,
	`profile` text NOT NULL,
	`seen` integer NOT NULL,
	`has_left` integer DEFAULT 0 NOT NULL,
	`guest_seq` integer DEFAULT 0 NOT NULL,
	`guest_packet` text,
	`host_packet` text
);
--> statement-breakpoint
CREATE INDEX `party_seat_room` ON `party_seats` (`code`);