-- Marks a match whose score was set manually and must never be overwritten by
-- the football-data.org sync (e.g. when the upstream API has a wrong result).
alter table "Matches"
  add column if not exists "ScoreLocked" boolean not null default false;
