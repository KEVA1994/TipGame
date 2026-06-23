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

---

## Tip-påmindelser på mail (GitHub Actions)

Et planlagt GitHub Actions-job sender en mail til alle spillere, der **ikke** har
registreret et tip til en kamp, der starter om ca. 24 timer. Det kører hver time
og kigger på et vindue 24–25t før kampstart, så hver kamp rammes præcis én gang.
En `SentReminders`-tabel sikrer mod dubletter ved genkørsler.

> **Hvorfor ikke en Edge Function?** Mailen sendes via iCloud SMTP, og Supabase
> Edge Functions kan ikke gennemføre en SMTP/STARTTLS-handshake pålideligt.
> Derfor lever afsendelsen i `scripts/notify-missing-predictions/` og køres fra
> workflow'en `.github/workflows/notify-missing-predictions.yml`.

### 1. Kør migrationen (opretter `SentReminders`)

Allerede kørt mod produktion. Hvis du genskaber databasen:

```sql
create table if not exists "SentReminders" (
  "Id" bigint generated always as identity primary key,
  "UserId" int not null references "Users"("Id") on delete cascade,
  "MatchId" int not null references "Matches"("Id") on delete cascade,
  "SentAt" timestamptz not null default now(),
  constraint "UQ_SentReminders_UserId_MatchId" unique ("UserId", "MatchId")
);
```

### 2. Lav et app-specifikt kodeord til iCloud

Mails sendes via iCloud SMTP med din egen Apple-konto:

1. Log ind på [appleid.apple.com](https://appleid.apple.com)
2. **Log-in og sikkerhed → App-specifikke adgangskoder → Generér**
3. Kopiér adgangskoden (vises kun én gang)

> Kræver at to-faktor-godkendelse er slået til på dit Apple-ID.

### 3. Sæt GitHub-secrets

**GitHub repo → Settings → Secrets and variables → Actions → New repository secret** —
opret disse:

| Secret | Værdi |
|--------|-------|
| `SUPABASE_URL` | `https://ejcuoqbfssefkeinlkly.supabase.co` |
| `SUPABASE_SERVICE_ROLE_KEY` | Service role-nøglen (Project Settings → API) |
| `SMTP_USERNAME` | `din@icloud.com` |
| `SMTP_PASSWORD` | Det app-specifikke kodeord fra trin 2 |
| `REMINDER_FROM_ADDRESS` | *(valgfri)* din iCloud-adresse eller et alias |
| `SITE_URL` | *(valgfri)* defaulter til `https://keva1994.github.io/TipGame/` |

> Service role-nøglen omgår Row Level Security — derfor må den **kun** ligge som
> en GitHub-secret, aldrig i klient-koden.
> `REMINDER_FROM_ADDRESS` skal være din iCloud-adresse (eller et alias) — iCloud
> afviser mails fra fremmede afsendere. Udelades den, bruges `SMTP_USERNAME`.

### 4. Test den

**GitHub repo → Actions → Notify missing predictions → Run workflow** → sæt
**Send a single test email** til `true` (og evt. en modtager-adresse). Det sender
én eksempelmail uden at røre databasen.

Når den virker, kører timeplanen automatisk.

> **Bemærk:** Spillere uden en `AuthId` (ingen Supabase Auth-konto) kan ikke få
> mail, da emailen hentes fra Supabase Auth.

---

```sql
-- Se planlagte jobs
select * from cron.job;

-- Se kørte jobs (seneste)
select * from cron.job_run_details order by start_time desc limit 20;

-- Stop cron-jobbet
select cron.unschedule('sync-matches');
```
