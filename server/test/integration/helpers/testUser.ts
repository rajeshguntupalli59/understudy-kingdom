import { createClient } from '@supabase/supabase-js';

export async function createTestUser(): Promise<{ userId: string; jwt: string }> {
  const supabaseUrl = process.env.SUPABASE_URL;
  const supabaseAnonKey = process.env.SUPABASE_ANON_KEY;

  if (!supabaseUrl || !supabaseAnonKey) {
    throw new Error('SUPABASE_URL and SUPABASE_ANON_KEY must be set in server/.env to run integration tests');
  }

  const client = createClient(supabaseUrl, supabaseAnonKey);
  const { data, error } = await client.auth.signInAnonymously();

  if (error || !data.session || !data.user) {
    throw new Error(
      `Failed to create anonymous test user: ${error?.message ?? 'no session returned'}. ` +
        'Confirm "Allow anonymous sign-ins" is enabled in Supabase Authentication settings.',
    );
  }

  return { userId: data.user.id, jwt: data.session.access_token };
}
