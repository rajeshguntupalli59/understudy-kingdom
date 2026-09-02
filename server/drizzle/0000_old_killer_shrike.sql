CREATE TABLE "decisions" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"kingdom_id" uuid NOT NULL,
	"cycle_number" integer NOT NULL,
	"player_recommendation" jsonb NOT NULL,
	"ruler_outcome" jsonb NOT NULL,
	"overridden" boolean NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	CONSTRAINT "decisions_kingdom_id_cycle_number_unique" UNIQUE("kingdom_id","cycle_number")
);
--> statement-breakpoint
CREATE TABLE "kingdoms" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"user_id" uuid NOT NULL,
	"founded_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "ruler_npcs" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"kingdom_id" uuid NOT NULL,
	"mood" integer DEFAULT 50 NOT NULL,
	"loyalty" integer DEFAULT 50 NOT NULL,
	"agenda" text DEFAULT 'Expansionist' NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	CONSTRAINT "ruler_npcs_kingdom_id_unique" UNIQUE("kingdom_id")
);
--> statement-breakpoint
ALTER TABLE "decisions" ADD CONSTRAINT "decisions_kingdom_id_kingdoms_id_fk" FOREIGN KEY ("kingdom_id") REFERENCES "public"."kingdoms"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "ruler_npcs" ADD CONSTRAINT "ruler_npcs_kingdom_id_kingdoms_id_fk" FOREIGN KEY ("kingdom_id") REFERENCES "public"."kingdoms"("id") ON DELETE no action ON UPDATE no action;