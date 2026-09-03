import { describe, it, expect } from 'vitest';
import { getIsoWeekInfo, getActiveEventWindow, EVENTS } from '../../src/game/liveOpsEvents';

describe('liveOpsEvents ISO week rotation', () => {
  it('computes isoWeekYear/isoWeek/weekStart/weekEnd for a date in ISO week 1 of 2026', () => {
    const info = getIsoWeekInfo(new Date('2026-01-01T00:00:00.000Z'));
    expect(info.isoWeekYear).toBe(2026);
    expect(info.isoWeek).toBe(1);
    expect(info.weekStart.toISOString()).toBe('2025-12-29T00:00:00.000Z');
    expect(info.weekEnd.toISOString()).toBe('2026-01-05T00:00:00.000Z');
  });

  it('Jan 1 2027 (a Friday) falls in ISO week 53 of 2026, not week 1 of 2027 -- the week-numbering-year boundary case', () => {
    const info = getIsoWeekInfo(new Date('2027-01-01T00:00:00.000Z'));
    expect(info.isoWeekYear).toBe(2026);
    expect(info.isoWeek).toBe(53);
    expect(info.weekStart.toISOString()).toBe('2026-12-28T00:00:00.000Z');
    expect(info.weekEnd.toISOString()).toBe('2027-01-04T00:00:00.000Z');
  });

  it('Jan 4 2027 (a Monday) is the first moment of ISO week 1 of 2027', () => {
    const info = getIsoWeekInfo(new Date('2027-01-04T00:00:00.000Z'));
    expect(info.isoWeekYear).toBe(2027);
    expect(info.isoWeek).toBe(1);
    expect(info.weekStart.toISOString()).toBe('2027-01-04T00:00:00.000Z');
    expect(info.weekEnd.toISOString()).toBe('2027-01-11T00:00:00.000Z');
  });

  it('the last moment of ISO week 53/2026 (one second before rollover) still resolves to week 53', () => {
    const info = getIsoWeekInfo(new Date('2027-01-03T23:59:59.000Z'));
    expect(info.isoWeekYear).toBe(2026);
    expect(info.isoWeek).toBe(53);
  });

  it('getActiveEventWindow produces a "W<isoWeekYear>-<isoWeek>" eventId', () => {
    const window = getActiveEventWindow(new Date('2026-01-01T00:00:00.000Z'));
    expect(window.eventId).toBe('W2026-1');
    expect(window.definition).toBe(EVENTS[1]);
  });

  it('week 1 and week 53 of the same isoWeekYear rotate to the same content but produce different eventIds -- so the reward can be re-earned each real week even when narration repeats', () => {
    const week1 = getActiveEventWindow(new Date('2026-01-01T00:00:00.000Z'));
    const week53 = getActiveEventWindow(new Date('2027-01-01T00:00:00.000Z'));

    expect(week1.definition).toBe(week53.definition);
    expect(week1.eventId).not.toBe(week53.eventId);
    expect(week1.eventId).toBe('W2026-1');
    expect(week53.eventId).toBe('W2026-53');
  });

  it('every hardcoded event defines a positive objective count and positive rewards', () => {
    expect(EVENTS.length).toBeGreaterThan(0);
    for (const definition of EVENTS) {
      expect(definition.objectiveDecisionCount).toBeGreaterThan(0);
      expect(definition.rewardMood).toBeGreaterThan(0);
      expect(definition.rewardLoyalty).toBeGreaterThan(0);
      expect(definition.name.length).toBeGreaterThan(0);
      expect(definition.narration.length).toBeGreaterThan(0);
    }
  });
});
