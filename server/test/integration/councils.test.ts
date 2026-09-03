import { randomUUID } from 'crypto';
import { eq } from 'drizzle-orm';
import { describe, it, expect, afterEach } from 'vitest';
import { buildApp } from '../../src/app';
import { db } from '../../src/db/client';
import { councilMembers, councils } from '../../src/db/schema';
import { createTestUser } from './helpers/testUser';
import { truncateTables } from './helpers/db';

describe('councils routes', () => {
  const app = buildApp();

  afterEach(async () => {
    await truncateTables();
  });

  it('POST /api/v1/councils creates a council with the caller as its sole member', async () => {
    const user = await createTestUser();

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${user.jwt}` },
      payload: { name: 'The Round Table' },
    });

    expect(response.statusCode).toBe(201);
    const body = response.json();
    expect(body.name).toBe('The Round Table');
    expect(body.joinCode).toMatch(/^[A-Z0-9]{6}$/);
    expect(body.memberCount).toBe(1);
    expect(body.totalDecisions).toBe(0);
    expect(body.milestoneThreshold).toBe(10);
    expect(body.milestoneReached).toBe(false);
    expect(body.rewardEligible).toBe(false);
  });

  it('POST /api/v1/councils returns 409 if the caller is already in a council', async () => {
    const user = await createTestUser();
    await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${user.jwt}` },
      payload: { name: 'First Council' },
    });

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${user.jwt}` },
      payload: { name: 'Second Council' },
    });

    expect(response.statusCode).toBe(409);
    expect(response.json().error).toBe('You are already in a council');

    // Proves the transaction actually rolled back the council row inserted
    // before the membership insert lost the race -- not just that the HTTP
    // layer reported failure.
    const orphanedCouncils = await db
      .select()
      .from(councils)
      .where(eq(councils.name, 'Second Council'));
    expect(orphanedCouncils).toHaveLength(0);
  });

  it('POST /api/v1/councils returns 400 for a malformed body', async () => {
    const user = await createTestUser();

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${user.jwt}` },
      payload: {},
    });

    expect(response.statusCode).toBe(400);
  });

  it('POST /api/v1/councils/join adds the caller to an existing council', async () => {
    const creator = await createTestUser();
    const createResponse = await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${creator.jwt}` },
      payload: { name: 'Open Council' },
    });
    const joinCode = createResponse.json().joinCode;

    const joiner = await createTestUser();
    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/councils/join',
      headers: { authorization: `Bearer ${joiner.jwt}` },
      payload: { joinCode },
    });

    expect(response.statusCode).toBe(200);
    expect(response.json().memberCount).toBe(2);
  });

  it('POST /api/v1/councils/join returns 404 for an unknown join code', async () => {
    const user = await createTestUser();

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/councils/join',
      headers: { authorization: `Bearer ${user.jwt}` },
      payload: { joinCode: 'ZZZZZZ' },
    });

    expect(response.statusCode).toBe(404);
    expect(response.json().error).toBe('No council found for that code');
  });

  it('POST /api/v1/councils/join returns 409 if the caller is already in a council', async () => {
    const creatorA = await createTestUser();
    const createResponseA = await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${creatorA.jwt}` },
      payload: { name: 'Council A' },
    });
    const joinCodeA = createResponseA.json().joinCode;

    const creatorB = await createTestUser();
    const createResponseB = await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${creatorB.jwt}` },
      payload: { name: 'Council B' },
    });
    const joinCodeB = createResponseB.json().joinCode;

    const joiner = await createTestUser();
    await app.inject({
      method: 'POST',
      url: '/api/v1/councils/join',
      headers: { authorization: `Bearer ${joiner.jwt}` },
      payload: { joinCode: joinCodeA },
    });

    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/councils/join',
      headers: { authorization: `Bearer ${joiner.jwt}` },
      payload: { joinCode: joinCodeB },
    });

    expect(response.statusCode).toBe(409);
    expect(response.json().error).toBe('You are already in a council');
  });

  it('POST /api/v1/councils/join returns 403 once the council has 20 members', async () => {
    const creator = await createTestUser();
    const createResponse = await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${creator.jwt}` },
      payload: { name: 'Popular Council' },
    });
    const { id: councilId, joinCode } = createResponse.json();

    // Creator is member #1; insert 19 more members directly (fabricated
    // userIds -- council_members.userId has no DB-level FK to a real users
    // table, matching kingdoms.userId's own precedent) to reach the
    // 20-member cap without 19 real Supabase anonymous sign-ins, which would
    // risk the rate limit this project's test suite has hit before.
    const fillerMembers = Array.from({ length: 19 }, () => ({
      userId: randomUUID(),
      councilId,
    }));
    await db.insert(councilMembers).values(fillerMembers);

    const rejectedJoiner = await createTestUser();
    const response = await app.inject({
      method: 'POST',
      url: '/api/v1/councils/join',
      headers: { authorization: `Bearer ${rejectedJoiner.jwt}` },
      payload: { joinCode },
    });

    expect(response.statusCode).toBe(403);
    expect(response.json().error).toBe('That council is full');
  });

  it('GET /api/v1/councils/me returns 404 if the caller is not in a council', async () => {
    const user = await createTestUser();

    const response = await app.inject({
      method: 'GET',
      url: '/api/v1/councils/me',
      headers: { authorization: `Bearer ${user.jwt}` },
    });

    expect(response.statusCode).toBe(404);
    expect(response.json().error).toBe('Not in a council');
  });

  it('GET /api/v1/councils/me reflects real membership and join code', async () => {
    const user = await createTestUser();
    const createResponse = await app.inject({
      method: 'POST',
      url: '/api/v1/councils',
      headers: { authorization: `Bearer ${user.jwt}` },
      payload: { name: 'My Council' },
    });
    const joinCode = createResponse.json().joinCode;

    const response = await app.inject({
      method: 'GET',
      url: '/api/v1/councils/me',
      headers: { authorization: `Bearer ${user.jwt}` },
    });

    expect(response.statusCode).toBe(200);
    const body = response.json();
    expect(body.joinCode).toBe(joinCode);
    expect(body.memberCount).toBe(1);
  });
});
