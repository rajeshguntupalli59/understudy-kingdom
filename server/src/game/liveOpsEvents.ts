export interface EventDefinition {
  name: string;
  narration: string;
  objectiveDecisionCount: number;
  rewardMood: number;
  rewardLoyalty: number;
}

// Fixed, hardcoded weekly-rotating content -- no DB table, no admin
// tooling, no cron job. This project has exactly one operator and no CMS;
// adding a 5th event later is a one-line change plus a deploy. See
// docs/superpowers/specs/2026-09-03-live-ops-events-design.md.
export const EVENTS: EventDefinition[] = [
  {
    name: 'Harvest Tithe',
    narration:
      "The granaries overflow with the autumn harvest, and the court expects wise stewardship. Submit 3 recommendations this week to see your kingdom through the tithe season.",
    objectiveDecisionCount: 3,
    rewardMood: 15,
    rewardLoyalty: 15,
  },
  {
    name: 'Border Skirmish',
    narration:
      "Rumors of raiders stir unrest along the frontier. Submit 3 recommendations this week to steady your ruler's resolve.",
    objectiveDecisionCount: 3,
    rewardMood: 15,
    rewardLoyalty: 15,
  },
  {
    name: "Pilgrims' Procession",
    narration:
      "A procession of pilgrims passes through your lands, testing your ruler's patience and piety. Submit 3 recommendations this week to guide them well.",
    objectiveDecisionCount: 3,
    rewardMood: 15,
    rewardLoyalty: 15,
  },
  {
    name: 'Market Fair',
    narration:
      'Merchants from distant kingdoms have set up a grand market fair. Submit 3 recommendations this week to make the most of the opportunity.',
    objectiveDecisionCount: 3,
    rewardMood: 15,
    rewardLoyalty: 15,
  },
];

export interface IsoWeekInfo {
  isoWeekYear: number;
  isoWeek: number;
  weekStart: Date;
  weekEnd: Date;
}

/**
 * Real ISO 8601 week-numbering semantics (Monday-start weeks; week 1 is the
 * week containing the year's first Thursday) -- NOT calendar-year-based.
 * isoWeekYear and the calendar year of `now` diverge at year boundaries
 * (e.g. Jan 1, 2027 is a Friday, and falls in ISO week 53 of 2026, not week
 * 1 of 2027). weekStart/weekEnd form a half-open interval: weekStart is
 * that ISO week's Monday 00:00:00.000 UTC, weekEnd is the FOLLOWING week's
 * Monday 00:00:00.000 UTC (exclusive) -- callers must filter with
 * `createdAt >= weekStart AND createdAt < weekEnd`, never `<=`. See
 * docs/superpowers/specs/2026-09-03-live-ops-events-design.md.
 */
export function getIsoWeekInfo(now: Date): IsoWeekInfo {
  const dayNr = (now.getUTCDay() + 6) % 7; // Monday=0 .. Sunday=6

  const weekStart = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() - dayNr));
  const weekEnd = new Date(weekStart.getTime() + 7 * 24 * 60 * 60 * 1000);

  // The Thursday of `now`'s own week determines both the ISO
  // week-numbering year and the week number (standard ISO 8601 algorithm).
  const thursday = new Date(weekStart.getTime() + 3 * 24 * 60 * 60 * 1000);
  const isoWeekYear = thursday.getUTCFullYear();

  const firstThursday = getFirstThursdayOfIsoYear(isoWeekYear);
  const isoWeek = 1 + Math.round((thursday.getTime() - firstThursday.getTime()) / (7 * 24 * 60 * 60 * 1000));

  return { isoWeekYear, isoWeek, weekStart, weekEnd };
}

function getFirstThursdayOfIsoYear(isoWeekYear: number): Date {
  const jan4 = new Date(Date.UTC(isoWeekYear, 0, 4));
  const jan4DayNr = (jan4.getUTCDay() + 6) % 7;
  return new Date(jan4.getTime() - jan4DayNr * 24 * 60 * 60 * 1000 + 3 * 24 * 60 * 60 * 1000);
}

export interface ActiveEventWindow {
  eventId: string;
  definition: EventDefinition;
  weekStart: Date;
  weekEnd: Date;
}

/**
 * The event id is keyed to the real ISO week (`W<isoWeekYear>-<isoWeek>`),
 * NOT to the content array index -- `definition` is selected by
 * `isoWeek % EVENTS.length` and will repeat every EVENTS.length weeks, but
 * `eventId` never repeats, so a player can re-earn the reward every real
 * week even when that week's narration happens to match a previous one.
 */
export function getActiveEventWindow(now: Date): ActiveEventWindow {
  const { isoWeekYear, isoWeek, weekStart, weekEnd } = getIsoWeekInfo(now);
  const definition = EVENTS[isoWeek % EVENTS.length];
  const eventId = `W${isoWeekYear}-${isoWeek}`;
  return { eventId, definition, weekStart, weekEnd };
}
