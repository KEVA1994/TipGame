-- Etape 4: sync-funktionen looper over aktive konkurrencer. football-data.org's
-- gratis-tier tillader 10 kald/min, så ved mange aktive konkurrencer syncses
-- round-robin — LastSyncedAt afgør hvem der er ældst og står for tur.
alter table "Competitions" add column "LastSyncedAt" timestamptz;
