CREATE TABLE `player_invites` (
	`id` text PRIMARY KEY NOT NULL,
	`sender` text NOT NULL,
	`recipient` text NOT NULL,
	`code` text NOT NULL,
	`created` integer NOT NULL,
	`expires` integer NOT NULL,
	`state` text DEFAULT 'pending' NOT NULL,
	FOREIGN KEY (`sender`) REFERENCES `player_accounts`(`id`) ON UPDATE no action ON DELETE cascade,
	FOREIGN KEY (`recipient`) REFERENCES `player_accounts`(`id`) ON UPDATE no action ON DELETE cascade
);
--> statement-breakpoint
CREATE INDEX `player_invites_inbox` ON `player_invites` (`recipient`,`state`,`expires`);--> statement-breakpoint
CREATE INDEX `player_invites_sender` ON `player_invites` (`sender`,`created`);--> statement-breakpoint
CREATE TABLE `player_presence` (
	`account_id` text PRIMARY KEY NOT NULL,
	`seen` integer NOT NULL,
	FOREIGN KEY (`account_id`) REFERENCES `player_accounts`(`id`) ON UPDATE no action ON DELETE cascade
);
--> statement-breakpoint
CREATE INDEX `player_presence_seen` ON `player_presence` (`seen`);