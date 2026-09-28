CREATE TABLE `online_rooms` (
	`code` text PRIMARY KEY NOT NULL,
	`host_key` text NOT NULL,
	`creator` text NOT NULL,
	`origin` text NOT NULL,
	`expires` integer NOT NULL
);
--> statement-breakpoint
CREATE INDEX `online_expiry` ON `online_rooms` (`expires`);--> statement-breakpoint
CREATE INDEX `online_creator` ON `online_rooms` (`creator`);