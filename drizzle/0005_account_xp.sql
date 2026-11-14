CREATE TABLE `account_xp` (
	`account` text PRIMARY KEY NOT NULL,
	`total` integer DEFAULT 0 NOT NULL,
	`revision` integer DEFAULT 0 NOT NULL,
	FOREIGN KEY (`account`) REFERENCES `player_accounts`(`id`) ON UPDATE no action ON DELETE cascade,
	CONSTRAINT "account_xp_total_range" CHECK("account_xp"."total" BETWEEN 0 AND 1237500)
);
--> statement-breakpoint
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
