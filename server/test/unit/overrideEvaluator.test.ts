import { describe, it, expect } from 'vitest';
import { overrideProbability, evaluate } from '../../src/game/overrideEvaluator';

// Parity tests against Assets/Tests/EditMode/OverrideEvaluatorTests.cs's known
// cases -- this TypeScript port must produce byte-identical results to the C#
// original for the same inputs, since the two implementations have no shared
// source of truth and could otherwise silently drift.
describe('overrideEvaluator (TypeScript port parity with Assets/Scripts/NPC/OverrideEvaluator.cs)', () => {
  it('neutral ruler, aligned allocation sits at baseline (matches NeutralRuler_Aligned_SitsAtBaseline)', () => {
    const probability = overrideProbability(
      { mood: 50, loyalty: 50, agenda: 'Expansionist' },
      { army: 50, trade: 30, religion: 20 },
    );
    expect(probability).toBeCloseTo(0.1, 4);
  });

  it('worst case (0 mood, 0 loyalty, misaligned) clamps to max probability (matches ExtremeWorstCase_ClampsToMaxProbability)', () => {
    const probability = overrideProbability(
      { mood: 0, loyalty: 0, agenda: 'Pious' },
      { army: 50, trade: 30, religion: 20 }, // religion 20 < 40 -> misaligned
    );
    expect(probability).toBeCloseTo(0.95, 4);
  });

  it('best case (100 mood, 100 loyalty, aligned) clamps to min probability (matches ExtremeBestCase_ClampsToMinProbability)', () => {
    const probability = overrideProbability(
      { mood: 100, loyalty: 100, agenda: 'Mercantile' },
      { army: 20, trade: 60, religion: 20 },
    );
    expect(probability).toBeCloseTo(0.02, 4);
  });

  it('not overridden applies positive deltas (matches NotOverridden_AppliesPositiveDeltas)', () => {
    const result = evaluate(
      { mood: 50, loyalty: 80, agenda: 'Mercantile' },
      { army: 20, trade: 60, religion: 20 },
      0.99,
    );
    expect(result.overridden).toBe(false);
    expect(result.moodDelta).toBe(5);
    expect(result.loyaltyDelta).toBe(3);
  });

  it('overridden applies negative deltas (matches Overridden_AppliesNegativeDeltas)', () => {
    const result = evaluate(
      { mood: 50, loyalty: 10, agenda: 'Expansionist' },
      { army: 50, trade: 30, religion: 20 },
      0.3,
    );
    expect(result.overridden).toBe(true);
    expect(result.moodDelta).toBe(-10);
    expect(result.loyaltyDelta).toBe(-5);
  });
});
