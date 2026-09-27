CREATE TABLE `relay_rooms` (
	`code` text PRIMARY KEY NOT NULL,
	`host_key` text NOT NULL,
	`guest_key` text,
	`guest_profile` text,
	`creator` text NOT NULL,
	`expires` integer NOT NULL,
	`closed` integer DEFAULT 0 NOT NULL,
	`guest_left` integer DEFAULT 0 NOT NULL,
	`host_seen` integer NOT NULL,
	`guest_seen` integer DEFAULT 0 NOT NULL,
	`host_seq` integer DEFAULT 0 NOT NULL,
	`guest_seq` integer DEFAULT 0 NOT NULL,
	`host_ack` integer DEFAULT 0 NOT NULL,
	`guest_ack` integer DEFAULT 0 NOT NULL,
	`host_packet` text,
	`guest_packet` text
);
--> statement-breakpoint
CREATE INDEX `relay_expiry` ON `relay_rooms` (`expires`);--> statement-breakpoint
CREATE INDEX `relay_creator` ON `relay_rooms` (`creator`);