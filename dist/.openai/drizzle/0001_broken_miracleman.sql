CREATE TABLE `dev_login_rates` (
	`id` text PRIMARY KEY NOT NULL,
	`hits` integer NOT NULL,
	`expires` integer NOT NULL
);
--> statement-breakpoint
CREATE INDEX `dev_login_expiry` ON `dev_login_rates` (`expires`);