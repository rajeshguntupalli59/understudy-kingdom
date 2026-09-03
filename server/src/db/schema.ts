import { pgTable, uuid, integer, text, boolean, jsonb, timestamp, unique } from 'drizzle-orm/pg-core';

/**
 * kingdoms.userId references Supabase's own auth.users(id) -- a table this
 * project doesn't own or migrate, so there is deliberately no Drizzle-level
 * foreign key here (application-enforced only). See
 * docs/superpowers/specs/2026-09-02-backend-service-design.md.
 */
export const kingdoms = pgTable('kingdoms', {
  id: uuid('id').primaryKey().defaultRandom(),
  userId: uuid('user_id').notNull().unique(),
  foundedAt: timestamp('founded_at', { withTimezone: true }).notNull().defaultNow(),
});

export const rulerNpcs = pgTable('ruler_npcs', {
  id: uuid('id').primaryKey().defaultRandom(),
  kingdomId: uuid('kingdom_id')
    .notNull()
    .references(() => kingdoms.id)
    .unique(),
  mood: integer('mood').notNull().default(50),
  loyalty: integer('loyalty').notNull().default(50),
  agenda: text('agenda').notNull().default('Expansionist'),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
});

export const decisions = pgTable(
  'decisions',
  {
    id: uuid('id').primaryKey().defaultRandom(),
    kingdomId: uuid('kingdom_id')
      .notNull()
      .references(() => kingdoms.id),
    cycleNumber: integer('cycle_number').notNull(),
    playerRecommendation: jsonb('player_recommendation').notNull(),
    rulerOutcome: jsonb('ruler_outcome').notNull(),
    overridden: boolean('overridden').notNull(),
    createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
  },
  (table) => [unique().on(table.kingdomId, table.cycleNumber)],
);

// No scenario_id (this milestone's confirmed mechanic uses the challenger's
// own submitted allocation, not a fixed scenario) and no separate
// winner_kingdom_id column (derivable from `overridden`; nothing reads duel
// history back yet to need it precomputed). defenderRulerSnapshot captures
// the defender's mood/loyalty/agenda AT DUEL TIME -- their kingdom keeps
// changing afterward, and the duel record should stay a fair, reproducible
// historical fact rather than silently drifting. See
// docs/superpowers/specs/2026-09-02-async-pvp-design.md.
export const pvpDuels = pgTable('pvp_duels', {
  id: uuid('id').primaryKey().defaultRandom(),
  challengerKingdomId: uuid('challenger_kingdom_id')
    .notNull()
    .references(() => kingdoms.id),
  defenderKingdomId: uuid('defender_kingdom_id')
    .notNull()
    .references(() => kingdoms.id),
  challengerRecommendation: jsonb('challenger_recommendation').notNull(),
  defenderRulerSnapshot: jsonb('defender_ruler_snapshot').notNull(),
  overridden: boolean('overridden').notNull(),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
});

// council_members.userId has no DB-level FK to Supabase's own auth.users --
// same reasoning as kingdoms.userId (see the comment above that table): this
// project doesn't own or migrate Supabase's auth schema.
export const councils = pgTable('councils', {
  id: uuid('id').primaryKey().defaultRandom(),
  name: text('name').notNull(),
  joinCode: text('join_code').notNull().unique(),
  milestoneThreshold: integer('milestone_threshold').notNull().default(10),
  milestoneReached: boolean('milestone_reached').notNull().default(false),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
});

// userId is this table's own primary key -- not a separate uuid id column --
// which is what makes "one council per user" a DB-enforced invariant rather
// than an application-level check: a second INSERT for the same userId can
// never succeed. rewardEligible is set true for every CURRENT member the
// moment the council's milestone_reached flips to true (see decisions.ts);
// anyone who joins afterward keeps it false forever -- see
// docs/superpowers/specs/2026-09-03-council-social-design.md.
export const councilMembers = pgTable('council_members', {
  userId: uuid('user_id').primaryKey(),
  councilId: uuid('council_id')
    .notNull()
    .references(() => councils.id),
  joinedAt: timestamp('joined_at', { withTimezone: true }).notNull().defaultNow(),
  rewardEligible: boolean('reward_eligible').notNull().default(false),
});
