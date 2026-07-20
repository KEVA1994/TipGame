# Genaktivering — sådan bruges appen til en ny turnering

Efter VM 2026 blev al automatik lukket ned (20. juli 2026). Denne guide beskriver
præcis, hvad der blev slukket, og hvordan det hele tændes igen — fx til EM,
Premier League eller et nyt VM.

## Hvad er slukket lige nu

| Ting | Hvor | Status |
|------|------|--------|
| `sync-matches` pg_cron-job (poll hvert minut) | Supabase | Afmeldt via `cron.unschedule('sync-matches')` |
| Tip-påmindelser hver time | `.github/workflows/notify-missing-predictions.yml` | `schedule:`-blokken er kommenteret ud |
| Edge Function `sync-matches` | Supabase | Stadig deployet, men kaldes ikke af nogen (koster intet) |
| Deploy af Blazor-appen | `.github/workflows/deploy-blazor.yml` | Kører stadig ved push — appen er fortsat online |

Selve appen (GitHub Pages) og databasen kører stadig, så alle kan se
slutstillingen.

## Tænd det hele igen (ny turnering)

### 1. Nulstil data i Supabase

Kør i **SQL Editor** (sletter alt fra den gamle turnering — spillere og logins
bevares):

```sql
delete from "SentReminders";
delete from "Predictions";
delete from "Matches";
```

> Vil du starte helt forfra med nye deltagere, skal `"Users"` og de tilhørende
> Supabase Auth-konti også ryddes — men typisk vil man beholde spillerne.

### 2. Opdater turnerings-secrets i Supabase

Edge Function'en styres af disse secrets (se `supabase/SETUP.md`):

```powershell
npx supabase secrets set FOOTBALL_API_COMPETITION=EC        # fx EC (EM), PL (Premier League), WC (VM)
npx supabase secrets set FOOTBALL_API_DATE_FROM=2028-06-09
npx supabase secrets set FOOTBALL_API_DATE_TO=2028-07-09
```

`FOOTBALL_API_TOKEN` er der allerede. Kompetitionskoder: se
[football-data.org](https://www.football-data.org/coverage).

### 3. Genstart pg_cron-jobbet

Kør i **SQL Editor**:

```sql
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

Tjek at kampene begynder at rulle ind: `select * from "Matches" limit 5;`

### 4. Genaktivér mail-påmindelser

Fjern udkommenteringen af `schedule:`-blokken i
`.github/workflows/notify-missing-predictions.yml` og push til `main`.

GitHub-secrets (`SMTP_*`, `SUPABASE_*` m.fl.) ligger der allerede — tjek dog at
det app-specifikke iCloud-kodeord stadig virker (kør workflow'en manuelt med
**Send a single test email** = `true`).

> **OBS:** GitHub deaktiverer automatisk scheduled workflows efter 60 dages
> inaktivitet i repoet. Kommer der en "workflow disabled"-mail, genaktivér den
> under **Actions → Notify missing predictions → Enable workflow**.

### 5. Tilpas tekster

Turneringsnavnet er hardcodet et par steder — søg efter `VM Tips Kuponen` og
`2026` i:

- `scripts/notify-missing-predictions/index.mjs` (påmindelsesmail)
- `scripts/send-final-standings/index.mjs` (afslutningsmail)
- Blazor-appens UI-tekster

## Når turneringen er slut igen

1. Send afslutningsmailen: **Actions → Send final standings email → Run
   workflow**. Kør først med `only_email` = din egen adresse som preview,
   derefter uden input for at maile alle. (Ingen dublet-beskyttelse — kør kun
   den rigtige kørsel én gang.)
2. Stop polling: `select cron.unschedule('sync-matches');` i SQL Editor.
3. Kommentér `schedule:`-blokken ud igen i
   `notify-missing-predictions.yml`.
