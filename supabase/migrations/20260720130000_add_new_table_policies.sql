-- Etape 5 (klient-forudsætning): medlemsbaserede policies på de NYE tabeller,
-- så Blazor-appen kan liste "mine konkurrencer", vise medlemslisten og lade
-- admin redigere indstillinger. Rører ikke de gamle tabellers åbne policies —
-- dem strammer etape 3 til sidst, når ingen gamle klienter er i omløb.
--
-- Bevidst valg: medlemmer kan se rækken inkl. InviteToken — ethvert medlem må
-- gerne invitere flere (admin kan regenerere tokenet, hvis det slipper løs).

-- Hjælpere (security definer => ingen rekursiv RLS-evaluering).
create or replace function public.is_member(p_competition_id int)
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select exists (
    select 1 from "CompetitionMembers"
    where "CompetitionId" = p_competition_id
      and "UserId" = (select "Id" from "Users" where "AuthId" = auth.uid())
  );
$$;

create or replace function public.is_admin(p_competition_id int)
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select exists (
    select 1 from "CompetitionMembers"
    where "CompetitionId" = p_competition_id
      and "UserId" = (select "Id" from "Users" where "AuthId" = auth.uid())
      and "Role" = 'admin'
  );
$$;

revoke execute on function public.is_member(int) from public;
revoke execute on function public.is_admin(int) from public;
grant execute on function public.is_member(int) to authenticated;
grant execute on function public.is_admin(int) to authenticated;

-- Competitions: medlemmer læser; kun admin opdaterer (navn, tekster,
-- påmindelser, status). INSERT/DELETE sker kun via RPC'er/service role.
create policy "Members can read their competitions"
  on "Competitions" for select
  to authenticated
  using (is_member("Id"));

create policy "Admins can update their competitions"
  on "Competitions" for update
  to authenticated
  using (is_admin("Id"))
  with check (is_admin("Id"));

-- CompetitionMembers: medlemmer ser medlemslisten for egne konkurrencer;
-- admin kan fjerne medlemmer (dog ikke sig selv — konkurrencen må ikke stå
-- uden admin). INSERT kun via join_competition-RPC'en.
create policy "Members can read the member list"
  on "CompetitionMembers" for select
  to authenticated
  using (is_member("CompetitionId"));

create policy "Admins can remove members"
  on "CompetitionMembers" for delete
  to authenticated
  using (
    is_admin("CompetitionId")
    and "UserId" <> (select "Id" from "Users" where "AuthId" = auth.uid())
  );
