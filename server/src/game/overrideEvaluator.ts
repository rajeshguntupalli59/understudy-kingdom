// Faithful TypeScript port of Assets/Scripts/NPC/OverrideEvaluator.cs. Any
// change to the constants or formula there must be mirrored here -- see the
// parity tests in server/test/unit/overrideEvaluator.test.ts, which assert
// against the C# test suite's own known values.

export interface RulerState {
  mood: number;
  loyalty: number;
  agenda: string;
}

export interface ResourceAllocation {
  army: number;
  trade: number;
  religion: number;
}

export interface OverrideResult {
  overridden: boolean;
  moodDelta: number;
  loyaltyDelta: number;
}

const NEUTRAL = 50;
const BASELINE = 0.1;
const LOYALTY_WEIGHT = 0.012;
const MOOD_WEIGHT = 0.005;
const AGENDA_MISALIGNED_BUMP = 0.25;
const MIN_PROBABILITY = 0.02;
const MAX_PROBABILITY = 0.95;

const ACCEPTED_MOOD_DELTA = 5;
const ACCEPTED_LOYALTY_DELTA = 3;
const OVERRIDDEN_MOOD_DELTA = -10;
const OVERRIDDEN_LOYALTY_DELTA = -5;

export function isAligned(agenda: string, allocation: ResourceAllocation): boolean {
  switch (agenda) {
    case 'Expansionist':
      return allocation.army >= 40;
    case 'Isolationist':
      return allocation.army <= 20;
    case 'Mercantile':
      return allocation.trade >= 40;
    case 'Pious':
      return allocation.religion >= 40;
    default:
      return true;
  }
}

function clamp(value: number, min: number, max: number): number {
  if (value < min) return min;
  if (value > max) return max;
  return value;
}

export function overrideProbability(state: RulerState, allocation: ResourceAllocation): number {
  const probability =
    BASELINE +
    (NEUTRAL - state.loyalty) * LOYALTY_WEIGHT +
    (NEUTRAL - state.mood) * MOOD_WEIGHT +
    (isAligned(state.agenda, allocation) ? 0 : AGENDA_MISALIGNED_BUMP);

  return clamp(probability, MIN_PROBABILITY, MAX_PROBABILITY);
}

export function evaluate(state: RulerState, allocation: ResourceAllocation, roll: number): OverrideResult {
  const overridden = roll < overrideProbability(state, allocation);

  return overridden
    ? { overridden: true, moodDelta: OVERRIDDEN_MOOD_DELTA, loyaltyDelta: OVERRIDDEN_LOYALTY_DELTA }
    : { overridden: false, moodDelta: ACCEPTED_MOOD_DELTA, loyaltyDelta: ACCEPTED_LOYALTY_DELTA };
}
