-- Supabase's default privileges giver anon/authenticated fuld adgang til nye
-- tabeller i public-skemaet. Uden RLS kunne klienter dermed læse alle
-- invitationstokens (og skrive direkte i medlemslisten). RLS slås til UDEN
-- policies: al klientadgang lukkes, indtil etape 3 tilføjer de rigtige
-- medlemsbaserede policies. SECURITY DEFINER-RPC'erne (ejet af postgres) og
-- service role omgår RLS og virker uændret — og den kørende Blazor-klient
-- bruger slet ikke tabellerne endnu.
alter table "Competitions" enable row level security;
alter table "CompetitionMembers" enable row level security;
