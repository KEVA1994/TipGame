-- Two concurrent sync-matches runs (pg_cron + a manual trigger) can both see a
-- match as missing and insert it twice. Enforce uniqueness on the external id
-- so the second insert fails instead of creating a duplicate match.
alter table "Matches" add constraint "UQ_Matches_ExternalId" unique ("ExternalId");
