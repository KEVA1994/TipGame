-- Etape 6 af multi-turnerings-planen (docs/PLAN-multi-turnering.md):
-- Admin-styrede mails via en kø i databasen. Admin trykker en knap i UI'et,
-- som lægger en række her; et scheduled GitHub Actions-job samler op og
-- stempler ProcessedAt. Giver samtidig den dubletbeskyttelse, den gamle
-- engangs-afslutningsmail manglede.
create table "EmailRequests" (
  "Id" bigint generated always as identity primary key,
  "CompetitionId" int not null references "Competitions"("Id") on delete cascade,
  "Kind" text not null,
  "RequestedBy" int not null references "Users"("Id"),
  "RequestedAt" timestamptz not null default now(),
  "ProcessedAt" timestamptz,
  constraint "CK_EmailRequests_Kind" check ("Kind" in ('final_standings'))
);

create index "IX_EmailRequests_CompetitionId" on "EmailRequests" ("CompetitionId");

alter table "EmailRequests" enable row level security;

-- Only the competition's admin may see its email request history/status.
-- No client policies for insert/update/delete: creation goes through
-- request_email() below, and marking ProcessedAt is done by the mail scripts
-- using the service role key (which bypasses RLS entirely).
create policy "Admins can view their competition's email requests"
  on "EmailRequests" for select
  to authenticated
  using (is_admin("CompetitionId"));

-- Queues a mail for a competition. Admin-only; rejects if a request of the
-- same kind already exists for this competition (processed or not) — a
-- final-standings mail should only ever be sent once per competition.
create or replace function public.request_email(p_competition_id int, p_kind text)
returns bigint
language plpgsql
security definer
set search_path = public
as $$
declare
  user_id int;
  new_id bigint;
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
    raise exception 'Only the competition admin can request emails';
  end if;

  if p_kind <> 'final_standings' then
    raise exception 'Unknown email kind: %', p_kind;
  end if;

  if exists (
    select 1 from "EmailRequests"
    where "CompetitionId" = p_competition_id and "Kind" = p_kind
  ) then
    raise exception 'A % email has already been requested for this competition', p_kind;
  end if;

  insert into "EmailRequests" ("CompetitionId", "Kind", "RequestedBy")
  values (p_competition_id, p_kind, user_id)
  returning "Id" into new_id;

  return new_id;
end;
$$;

revoke execute on function public.request_email(int, text) from public;
grant execute on function public.request_email(int, text) to authenticated;
