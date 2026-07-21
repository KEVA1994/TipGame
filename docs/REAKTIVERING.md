# Drift — multi-turnerings-platform

Siden 21. juli 2026 er TipGame en multi-turnerings-platform (se
[PLAN-multi-turnering.md](PLAN-multi-turnering.md) for baggrund og design).
Denne fil erstatter den gamle genaktiverings-guide, som er overflødig nu —
alt herunder foregår i appens UI, ikke via SQL Editor eller secrets.

## Ny konkurrence (fx EM, Premier League, et nyt VM)

Kræver ingen kode- eller infrastrukturændringer:

1. Log ind i appen → **Opret en konkurrence**.
2. Vælg navn og turnering fra kataloget (de turneringer football-data.org's
   gratis-tier dækker — se listen på `/opret`-siden).
3. Del invitationslinket, der vises efter oprettelse.

Kamp-synkroniseringen kører permanent i baggrunden (pg_cron kalder
`sync-matches` hvert minut) og henter automatisk data for alle **aktive**
konkurrencer — ingen manuel opsætning, ingen secrets at ændre.

## Turnering uden for gratis-tier'et (fx Superligaen)

Ikke understøttet i dag. Opstår behovet, opgraderes football-data.org-planen,
og turneringskoden tilføjes kataloget i
[CreateCompetition.razor](../TipGame.Blazor/Pages/CreateCompetition.razor).

## Når en konkurrence er slut

Fra konkurrencens **Admin**-side:

1. **Send afslutningsmail** — lægger en anmodning i kø; sendes automatisk til
   alle medlemmer inden for ca. 15 minutter (kan kun ske én gang pr.
   konkurrence).
2. **Afslut konkurrencen** — stopper synkronisering og lukker for tilmelding.
   Stillingen kan stadig ses bagefter.

(Konkurrencer afsluttes også automatisk, når slutrunden er spillet færdig
eller det angivne datovindue er passeret.)

## Hvis noget virker forkert

- **Ingen kampe dukker op**: tjek at konkurrencen er `active` (ikke `draft`)
  og at `sync-matches`-cron-jobbet kører: `select * from cron.job;` i
  Supabase SQL Editor.
- **Mail-påmindelser mangler**: admin kan slå dem til/fra pr. konkurrence
  under Admin → Indstillinger.
- Se `supabase/SETUP.md` for den tekniske baggrund om Edge Function'en og
  hvordan man deployer en ny version af den.
