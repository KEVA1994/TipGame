-- get_advisors flagged every RPC as callable by the anon role. Cause: Supabase
-- grants EXECUTE on new public-schema functions to anon/authenticated by
-- default (ALTER DEFAULT PRIVILEGES), independent of any `revoke ... from
-- public` — revoking from the PUBLIC pseudo-role does not touch grants made
-- directly to anon/authenticated. Not exploitable today (every RPC checks
-- auth.uid()/membership internally and rejects anon), but needless exposure.
--
-- get_competition_by_token is the one legitimate anon entry point — the
-- /join/{token} page must show "you're invited to X" before login.
revoke execute on function public.activate_competition(int) from anon;
revoke execute on function public.competition_of_match(int) from anon;
revoke execute on function public.create_competition(text, text, date, date) from anon;
revoke execute on function public.current_user_id() from anon;
revoke execute on function public.ensure_user_row() from anon;
revoke execute on function public.is_admin(int) from anon;
revoke execute on function public.is_member(int) from anon;
revoke execute on function public.join_competition(uuid) from anon;
revoke execute on function public.regenerate_invite_token(int) from anon;
revoke execute on function public.request_email(int, text) from anon;

-- handle_new_user is a trigger function (reads NEW, only valid inside a
-- trigger) — it should never be callable directly by any client role.
-- Triggers fire regardless of the triggering role's EXECUTE grants (Postgres
-- invokes trigger functions internally, not via a normal role-checked call),
-- so revoking from PUBLIC/anon/authenticated does not affect signup.
revoke execute on function public.handle_new_user() from public, anon, authenticated;
