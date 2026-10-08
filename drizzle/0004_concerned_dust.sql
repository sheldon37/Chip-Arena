CREATE TABLE `account_audit` (
	`id` text PRIMARY KEY NOT NULL,
	`actor` text NOT NULL,
	`target` text NOT NULL,
	`action` text NOT NULL,
	`detail` text NOT NULL,
	`created` integer NOT NULL
);
--> statement-breakpoint
CREATE INDEX `account_audit_target` ON `account_audit` (`target`,`created`);--> statement-breakpoint
CREATE TABLE `player_accounts` (
	`id` text PRIMARY KEY NOT NULL,
	`username` text NOT NULL,
	`password` text NOT NULL,
	`recovery` text NOT NULL,
	`developer` integer DEFAULT 0 NOT NULL,
	`unlocks` text DEFAULT '[]' NOT NULL,
	`flags` text DEFAULT '[]' NOT NULL,
	`profile` text DEFAULT '{}' NOT NULL,
	`revision` integer DEFAULT 0 NOT NULL,
	`grant_version` integer DEFAULT 0 NOT NULL,
	`created` integer NOT NULL,
	`updated` integer NOT NULL
);
--> statement-breakpoint
CREATE UNIQUE INDEX `player_accounts_username_unique` ON `player_accounts` (`username`);--> statement-breakpoint
CREATE TABLE `player_sessions` (
	`hash` text PRIMARY KEY NOT NULL,
	`account_id` text NOT NULL,
	`expires` integer NOT NULL,
	FOREIGN KEY (`account_id`) REFERENCES `player_accounts`(`id`) ON UPDATE no action ON DELETE cascade
);
--> statement-breakpoint
CREATE INDEX `player_session_account` ON `player_sessions` (`account_id`);--> statement-breakpoint
CREATE INDEX `player_session_expiry` ON `player_sessions` (`expires`);