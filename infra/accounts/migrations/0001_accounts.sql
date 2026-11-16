-- Account and social DDL from the v70 deployed Drizzle migrations.
CREATE TABLE `dev_login_rates` (
	`id` text PRIMARY KEY NOT NULL,
	`hits` integer NOT NULL,
	`expires` integer NOT NULL
);

CREATE INDEX `dev_login_expiry` ON `dev_login_rates` (`expires`);
CREATE TABLE `account_audit` (
	`id` text PRIMARY KEY NOT NULL,
	`actor` text NOT NULL,
	`target` text NOT NULL,
	`action` text NOT NULL,
	`detail` text NOT NULL,
	`created` integer NOT NULL
);

CREATE INDEX `account_audit_target` ON `account_audit` (`target`,`created`);
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

CREATE UNIQUE INDEX `player_accounts_username_unique` ON `player_accounts` (`username`);
CREATE TABLE `player_sessions` (
	`hash` text PRIMARY KEY NOT NULL,
	`account_id` text NOT NULL,
	`expires` integer NOT NULL,
	FOREIGN KEY (`account_id`) REFERENCES `player_accounts`(`id`) ON UPDATE no action ON DELETE cascade
);

CREATE INDEX `player_session_account` ON `player_sessions` (`account_id`);
CREATE INDEX `player_session_expiry` ON `player_sessions` (`expires`);
CREATE TABLE `account_xp` (
	`account` text PRIMARY KEY NOT NULL,
	`total` integer DEFAULT 0 NOT NULL,
	`revision` integer DEFAULT 0 NOT NULL,
	FOREIGN KEY (`account`) REFERENCES `player_accounts`(`id`) ON UPDATE no action ON DELETE cascade,
	CONSTRAINT "account_xp_total_range" CHECK("account_xp"."total" BETWEEN 0 AND 1237500)
);

CREATE TABLE `account_xp_awards` (
	`account` text NOT NULL,
	`completion` text NOT NULL,
	`payload` text NOT NULL,
	`previous_total` integer NOT NULL,
	`new_total` integer NOT NULL,
	`marker` text NOT NULL,
	`created` integer NOT NULL,
	PRIMARY KEY(`account`, `completion`),
	FOREIGN KEY (`account`) REFERENCES `player_accounts`(`id`) ON UPDATE no action ON DELETE cascade
);

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

CREATE INDEX `player_invites_inbox` ON `player_invites` (`recipient`,`state`,`expires`);
CREATE INDEX `player_invites_sender` ON `player_invites` (`sender`,`created`);
CREATE TABLE `player_presence` (
	`account_id` text PRIMARY KEY NOT NULL,
	`seen` integer NOT NULL,
	FOREIGN KEY (`account_id`) REFERENCES `player_accounts`(`id`) ON UPDATE no action ON DELETE cascade
);

CREATE INDEX `player_presence_seen` ON `player_presence` (`seen`);
