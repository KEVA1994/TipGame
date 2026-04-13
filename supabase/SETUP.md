# Supabase Edge Function — Automatisk kamp-sync

Edge Function'en `sync-matches` erstatter DataMiner-loopet. Den kører automatisk via `pg_cron` i Supabase — gratis, altid online, ingen lokal maskine nødvendig.

## Opsætning (én gang)

### 1. Installer Supabase CLI

```powershell
npx supabase --version   # test om det virker
```

### 2. Link til dit projekt

```powershell
npx supabase login
npx supabase link --project-ref ejcuoqbfssefkeinlkly
```

### 3. Sæt secrets (football-data.org API-nøgle + datoer)

```powershell
npx supabase secrets set FOOTBALL_API_TOKEN=din_football_api_token
npx supabase secrets set FOOTBALL_API_COMPETITION=PL
npx supabase secrets set FOOTBALL_API_DATE_FROM=2026-03-30
npx supabase secrets set FOOTBALL_API_DATE_TO=2026-04-27
```

> `SUPABASE_URL` og `SUPABASE_SERVICE_ROLE_KEY` er automatisk tilgængelige i Edge Functions.

### 4. Deploy funktionen

```powershell
npx supabase functions deploy sync-matches --no-verify-jwt
```

> `--no-verify-jwt` er nødvendigt fordi `pg_cron` kalder funktionen internt uden JWT.

### 5. Opsæt pg_cron (automatisk kald hvert minut)

Gå til **Supabase Dashboard → SQL Editor** og kør:

```sql
-- Aktiver extensions (hvis ikke allerede aktiveret)
create extension if not exists pg_cron;
create extension if not exists pg_net;

-- Planlæg sync hvert minut
select cron.schedule(
  'sync-matches',
  '* * * * *',
  $$
  select net.http_post(
    url := 'https://ejcuoqbfssefkeinlkly.supabase.co/functions/v1/sync-matches',
    headers := '{"Content-Type": "application/json"}'::jsonb,
    body := '{}'::jsonb
  ) as request_id;
  $$
);
```

### 6. (Valgfrit) Deaktiver GitHub Actions sync

Slet eller omdøb `.github/workflows/sync-matches.yml` — Edge Function'en erstatter den.

---

## Hvordan det virker

1. `pg_cron` kalder Edge Function'en **hvert minut**
2. Funktionen tjekker om der er **uafsluttede kampe** i Supabase
3. Hvis alle kampe er færdige → skipper API-kald (sparer football-data.org kvoter)
4. Hvis der er aktive/planlagte kampe → henter data fra football-data.org og opdaterer Supabase
5. Når en kamp skifter til `FINISHED` → beregner point automatisk

## Nyttigt

```sql
-- Se planlagte jobs
select * from cron.job;

-- Se kørte jobs (seneste)
select * from cron.job_run_details order by start_time desc limit 20;

-- Stop cron-jobbet
select cron.unschedule('sync-matches');
```
