CREATE TABLE "council_members" (
	"user_id" uuid PRIMARY KEY NOT NULL,
	"council_id" uuid NOT NULL,
	"joined_at" timestamp with time zone DEFAULT now() NOT NULL,
	"reward_eligible" boolean DEFAULT false NOT NULL
);
--> statement-breakpoint
CREATE TABLE "councils" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"name" text NOT NULL,
	"join_code" text NOT NULL,
	"milestone_threshold" integer DEFAULT 10 NOT NULL,
	"milestone_reached" boolean DEFAULT false NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	CONSTRAINT "councils_join_code_unique" UNIQUE("join_code")
);
--> statement-breakpoint
ALTER TABLE "council_members" ADD CONSTRAINT "council_members_council_id_councils_id_fk" FOREIGN KEY ("council_id") REFERENCES "public"."councils"("id") ON DELETE no action ON UPDATE no action;