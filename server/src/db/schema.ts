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
