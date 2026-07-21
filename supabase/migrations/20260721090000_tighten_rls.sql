-- Etape 3 af multi-turnerings-planen (docs/PLAN-multi-turnering.md) —
-- deployes SIDST, efter etape 5/6 er i produktion, så ingen gamle klienter
-- er i omløb. Erstatter de åbne "public"-policies fra enkelt-turnerings-æraen
-- (alle kunne læse/skrive alt i Users og Predictions!) med medlemsbaserede
-- policies. Kickoff-reglen på Predictions var hidtil kun håndhævet i
-- klienten — bliver nu en databasegaranti.

-- Slår en kamps CompetitionId op. Security definer => bypasser Matches'
-- egen RLS (owner-bypass), så Predictions-policyen ikke afhænger af, om
-- brugeren i forvejen kan se selve kamp-rækken.
create or replace function public.competition_of_match(p_match_id int)
returns int
language sql
stable
security definer
set search_path = public
as $$
  select "CompetitionId" from "Matches" where "Id" = p_match_id;
$$;

revoke execute on function public.competition_of_match(int) from public;
grant execute on function public.competition_of_match(int) to authenticated;

-- SentReminders manglede helt RLS (kun brugt af service-role-scripts).
alter table "SentReminders" enable row level security;
-- Ingen policies for anon/authenticated -> al klientadgang blokeres.
-- Service role (edge function/scripts) bypasser RLS uanset og er upåvirket.

-- Matches: kun medlemmer af kampens konkurrence må se den. Ingen
-- insert/update/delete-policies for klienter — kun service role (sync).
drop policy "Matches are public" on "Matches";

create policy "Members can view matches in their competitions"
  on "Matches" for select
  to authenticated
  using (is_member("CompetitionId"));

-- Predictions: medlemmer af kampens konkurrence kan se alle predictions;
-- man må kun skrive sin egen række, og kun mens kampen ikke er startet.
drop policy "Predictions are public" on "Predictions";

create policy "Members can view predictions in their competitions"
  on "Predictions" for select
  to authenticated
  using (is_member(competition_of_match("MatchId")));

create policy "Users can insert their own prediction before kickoff"
  on "Predictions" for insert
  to authenticated
  with check (
    "UserId" = current_user_id()
    and exists (
      select 1 from "Matches" m
      where m."Id" = "MatchId"
        and m."KickoffTime" > now()
        and m."Status" in ('SCHEDULED', 'TIMED')
    )
  );

create policy "Users can update their own prediction before kickoff"
  on "Predictions" for update
  to authenticated
  using (
    "UserId" = current_user_id()
    and exists (
      select 1 from "Matches" m
      where m."Id" = "MatchId"
        and m."KickoffTime" > now()
        and m."Status" in ('SCHEDULED', 'TIMED')
    )
  )
  with check (
    "UserId" = current_user_id()
    and exists (
      select 1 from "Matches" m
      where m."Id" = "MatchId"
        and m."KickoffTime" > now()
        and m."Status" in ('SCHEDULED', 'TIMED')
    )
  );

create policy "Users can delete their own prediction before kickoff"
  on "Predictions" for delete
  to authenticated
  using (
    "UserId" = current_user_id()
    and exists (
      select 1 from "Matches" m
      where m."Id" = "MatchId"
        and m."KickoffTime" > now()
        and m."Status" in ('SCHEDULED', 'TIMED')
    )
  );

-- Users: man kan altid se sig selv, plus alle man deler mindst én
-- konkurrence med (nødvendigt for leaderboard/stats/predictions-visning).
-- Kun egen række må opdateres (fx display-navn).
drop policy "Users are public" on "Users";

create policy "Users can view themselves and their competition-mates"
  on "Users" for select
  to authenticated
  using (
    "AuthId" = auth.uid()
    or exists (
      select 1
      from "CompetitionMembers" cm1
      join "CompetitionMembers" cm2 on cm1."CompetitionId" = cm2."CompetitionId"
      where cm1."UserId" = "Users"."Id"
        and cm2."UserId" = current_user_id()
    )
  );

create policy "Users can update their own row"
  on "Users" for update
  to authenticated
  using ("AuthId" = auth.uid())
  with check ("AuthId" = auth.uid());
