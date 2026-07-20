-- Etape 2 af multi-turnerings-planen (docs/PLAN-multi-turnering.md):
-- RPC'er til oprettelse, aktivering og join via invitationstoken.
-- Alle er SECURITY DEFINER med eksplicitte auth-tjek, så de virker uafhængigt
-- af RLS (som først strammes i etape 3). Tokens kan ikke enumereres, fordi
-- klienten aldrig SELECT'er på Competitions.InviteToken — kun eksakt opslag
-- via get_competition_by_token/join_competition.

-- Join-flowet finder/opretter Users-rækken ud fra auth.uid(); uden unikhed på
-- AuthId kan to samtidige kald oprette dubletter.
create unique index "UQ_Users_AuthId" on "Users" ("AuthId")
  where "AuthId" is not null;

-- Users.Id for den aktuelle auth-bruger. Genbruges af RLS-policies i etape 3.
create or replace function public.current_user_id()
returns int
language sql
stable
security definer
set search_path = public
as $$
  select "Id" from "Users" where "AuthId" = auth.uid();
$$;

-- Som current_user_id, men opretter Users-rækken hvis den mangler (navn fra
-- auth-metadata). Kun til internt brug fra de øvrige definer-funktioner —
-- ingen EXECUTE-grant til klientroller.
create or replace function public.ensure_user_row()
returns int
language plpgsql
security definer
set search_path = public
as $$
declare
  uid uuid := auth.uid();
  user_id int;
  display_name text;
begin
  if uid is null then
    raise exception 'Not authenticated';
  end if;

  select "Id" into user_id from "Users" where "AuthId" = uid;
  if user_id is not null then
    return user_id;
  end if;

  select coalesce(nullif(raw_user_meta_data ->> 'display_name', ''), email)
    into display_name
  from auth.users
  where id = uid;

  insert into "Users" ("Name", "AuthId")
  values (coalesce(display_name, 'Ukendt spiller'), uid)
  on conflict ("AuthId") where "AuthId" is not null do nothing;

  select "Id" into user_id from "Users" where "AuthId" = uid;
  return user_id;
end;
$$;

-- Opretter konkurrencen som 'draft' og gør kalderen til admin (atomisk).
create or replace function public.create_competition(
  p_name text,
  p_code text,
  p_date_from date default null,
  p_date_to date default null
)
returns int
language plpgsql
security definer
set search_path = public
as $$
declare
  user_id int;
  comp_id int;
  code text := upper(trim(coalesce(p_code, '')));
begin
  user_id := ensure_user_row();

  if coalesce(trim(p_name), '') = '' then
    raise exception 'Name is required';
  end if;
  if code !~ '^[A-Z0-9]{2,10}$' then
    raise exception 'Invalid competition code';
  end if;
  if p_date_from is not null and p_date_to is not null and p_date_to < p_date_from then
    raise exception 'DateTo must not be before DateFrom';
  end if;

  insert into "Competitions" ("Name", "CompetitionCode", "DateFrom", "DateTo")
  values (trim(p_name), code, p_date_from, p_date_to)
  returning "Id" into comp_id;

  insert into "CompetitionMembers" ("CompetitionId", "UserId", "Role")
  values (comp_id, user_id, 'admin');

  return comp_id;
end;
$$;

-- Skifter 'draft' -> 'active'. Idempotent hvis allerede aktiv.
-- BETALINGS-HOOK: i v1 kalder UI'et denne direkte (gratis oprettelse). Når
-- betaling kobles på, fjernes EXECUTE-grantet til authenticated, og funktionen
-- kaldes i stedet af stripe-webhook-edge-funktionen (service role) efter
-- verificeret betaling. Se "Forberedt til betaling" i planen.
create or replace function public.activate_competition(p_competition_id int)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  user_id int;
  current_status text;
begin
  user_id := current_user_id();
  if user_id is null then
    raise exception 'Not authenticated';
  end if;

  if not exists (
    select 1 from "CompetitionMembers"
    where "CompetitionId" = p_competition_id
      and "UserId" = user_id
      and "Role" = 'admin'
  ) then
    raise exception 'Only the competition admin can activate it';
  end if;

  select "Status" into current_status
  from "Competitions"
  where "Id" = p_competition_id
  for update;

  if current_status is null then
    raise exception 'Competition not found';
  elsif current_status = 'active' then
    return;
  elsif current_status <> 'draft' then
    raise exception 'Cannot activate a competition with status %', current_status;
  end if;

  update "Competitions"
  set "Status" = 'active'
  where "Id" = p_competition_id;
end;
$$;

-- Eksakt token-opslag til join-siden ("Du er inviteret til X") — også før login.
create or replace function public.get_competition_by_token(p_token uuid)
returns table ("Id" int, "Name" text, "Status" text)
language sql
stable
security definer
set search_path = public
as $$
  select "Id", "Name", "Status"
  from "Competitions"
  where "InviteToken" = p_token;
$$;

-- Melder kalderen ind i konkurrencen bag tokenet. Idempotent (allerede medlem
-- = OK). Kun aktive konkurrencer kan joines — 'draft' er ikke aktiveret (og
-- senere: ikke betalt), 'finished'/'archived' er lukket.
create or replace function public.join_competition(p_token uuid)
returns int
language plpgsql
security definer
set search_path = public
as $$
declare
  user_id int;
  comp record;
begin
  user_id := ensure_user_row();

  select "Id", "Status" into comp
  from "Competitions"
  where "InviteToken" = p_token;

  if comp."Id" is null then
    raise exception 'Invalid invite link';
  end if;
  if comp."Status" <> 'active' then
    raise exception 'Competition is not open for joining';
  end if;

  insert into "CompetitionMembers" ("CompetitionId", "UserId")
  values (comp."Id", user_id)
  on conflict ("CompetitionId", "UserId") do nothing;

  return comp."Id";
end;
$$;

-- Nyt invitationslink (hvis det gamle er sluppet løs). Kun admin.
create or replace function public.regenerate_invite_token(p_competition_id int)
returns uuid
language plpgsql
security definer
set search_path = public
as $$
declare
  user_id int;
  new_token uuid;
begin
  user_id := current_user_id();
  if user_id is null then
    raise exception 'Not authenticated';
  end if;

  if not exists (
    select 1 from "CompetitionMembers"
    where "CompetitionId" = p_competition_id
      and "UserId" = user_id
      and "Role" = 'admin'
  ) then
    raise exception 'Only the competition admin can regenerate the invite token';
  end if;

  update "Competitions"
  set "InviteToken" = gen_random_uuid()
  where "Id" = p_competition_id
  returning "InviteToken" into new_token;

  if new_token is null then
    raise exception 'Competition not found';
  end if;

  return new_token;
end;
$$;

-- Stram EXECUTE-rettighederne: Postgres giver som udgangspunkt PUBLIC adgang
-- til nye funktioner — fjern det og tildel eksplicit.
revoke execute on function public.current_user_id() from public;
revoke execute on function public.ensure_user_row() from public;
revoke execute on function public.create_competition(text, text, date, date) from public;
revoke execute on function public.activate_competition(int) from public;
revoke execute on function public.get_competition_by_token(uuid) from public;
revoke execute on function public.join_competition(uuid) from public;
revoke execute on function public.regenerate_invite_token(int) from public;

grant execute on function public.current_user_id() to authenticated;
grant execute on function public.create_competition(text, text, date, date) to authenticated;
grant execute on function public.activate_competition(int) to authenticated;
grant execute on function public.get_competition_by_token(uuid) to anon, authenticated;
grant execute on function public.join_competition(uuid) to authenticated;
grant execute on function public.regenerate_invite_token(int) to authenticated;
-- ensure_user_row: ingen klient-grants — kaldes kun internt fra definer-funktionerne.
