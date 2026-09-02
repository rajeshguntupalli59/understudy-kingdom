CREATE TABLE "pvp_duels" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"challenger_kingdom_id" uuid NOT NULL,
	"defender_kingdom_id" uuid NOT NULL,
	"challenger_recommendation" jsonb NOT NULL,
	"defender_ruler_snapshot" jsonb NOT NULL,
	"overridden" boolean NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "pvp_duels" ADD CONSTRAINT "pvp_duels_challenger_kingdom_id_kingdoms_id_fk" FOREIGN KEY ("challenger_kingdom_id") REFERENCES "public"."kingdoms"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "pvp_duels" ADD CONSTRAINT "pvp_duels_defender_kingdom_id_kingdoms_id_fk" FOREIGN KEY ("defender_kingdom_id") REFERENCES "public"."kingdoms"("id") ON DELETE no action ON UPDATE no action;