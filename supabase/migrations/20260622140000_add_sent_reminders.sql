-- Tracks reminder emails already sent, so a re-run of the cron job (or an
-- overlapping window) never spams the same player twice for the same match.
create table if not exists "SentReminders" (
  "Id" bigint generated always as identity primary key,
  "UserId" int not null references "Users"("Id") on delete cascade,
  "MatchId" int not null references "Matches"("Id") on delete cascade,
  "SentAt" timestamptz not null default now(),
  constraint "UQ_SentReminders_UserId_MatchId" unique ("UserId", "MatchId")
);
