-- Idempotent point settlement: recalculates Points for all predictions on
-- finished matches with known scores. Safe to call repeatedly.
create or replace function public.settle_points()
returns integer
language sql
security definer
set search_path = public
as $$
  with calc as (
    select p."Id" as pred_id,
           case
             when p."PredictedHome" = m."HomeScore" and p."PredictedAway" = m."AwayScore" then 3
             when p."PredictedHome" = p."PredictedAway" and m."HomeScore" = m."AwayScore" then 2
             when (p."PredictedHome" > p."PredictedAway" and m."HomeScore" > m."AwayScore")
               or (p."PredictedHome" < p."PredictedAway" and m."HomeScore" < m."AwayScore") then 1
             else 0
           end as points
    from "Predictions" p
    join "Matches" m on m."Id" = p."MatchId"
    where m."Status" = 'FINISHED'
      and m."HomeScore" is not null
      and m."AwayScore" is not null
  ),
  updated as (
    update "Predictions" p
    set "Points" = calc.points
    from calc
    where p."Id" = calc.pred_id
      and p."Points" is distinct from calc.points
    returning p."Id"
  )
  select count(*)::integer from updated;
$$;

-- Service role / backend only; keep it away from anon and authenticated.
revoke execute on function public.settle_points() from public, anon, authenticated;
