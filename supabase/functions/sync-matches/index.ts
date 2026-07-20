// Syncs matches for every active competition (multi-tenant, etape 4 of
// docs/PLAN-multi-turnering.md). Called by pg_cron every minute — the cron job
// is scheduled once and never touched again: with no active competitions the
// function no-ops without spending football-data.org quota.
//
// Per competition: fetch matches for its CompetitionCode (optionally windowed
// by DateFrom/DateTo), upsert rows scoped to that competition, and auto-mark
// the competition 'finished' when everything is played. settle_points runs
// once at the end and is idempotent across competitions.
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

// football-data.org free tier allows 10 calls/min; pg_cron fires every minute.
// With more active competitions than the budget, sync round-robin — oldest
// LastSyncedAt first — so everyone still gets fresh data within a few minutes.
const SYNC_BUDGET_PER_RUN = 8;

Deno.serve(async () => {
  const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
  const supabaseKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
  const footballApiToken = Deno.env.get("FOOTBALL_API_TOKEN")!;

  const supabase = createClient(supabaseUrl, supabaseKey);

  const { data: competitions, error: compError } = await supabase
    .from("Competitions")
    .select("Id, Name, CompetitionCode, DateFrom, DateTo, LastSyncedAt")
    .eq("Status", "active")
    .order("LastSyncedAt", { ascending: true, nullsFirst: true })
    .limit(SYNC_BUDGET_PER_RUN);

  if (compError) {
    return Response.json({ error: compError.message }, { status: 500 });
  }
  if (!competitions || competitions.length === 0) {
    return Response.json({ skipped: true, reason: "No active competitions" });
  }

  const results: Record<string, unknown>[] = [];
  const errors: string[] = [];

  for (const comp of competitions) {
    const result = await syncCompetition(supabase, comp, footballApiToken, errors);
    results.push({ competition: comp.Name, ...result });

    await supabase
      .from("Competitions")
      .update({ LastSyncedAt: new Date().toISOString() })
      .eq("Id", comp.Id);
  }

  // Settle points for all finished matches. Idempotent — recalculates every
  // run, so a missed/failed run self-heals instead of leaving predictions at 0.
  let settled = 0;
  const { data: settledCount, error: settleError } = await supabase.rpc(
    "settle_points"
  );
  if (settleError) errors.push(`settle_points: ${settleError.message}`);
  else settled = settledCount ?? 0;

  if (errors.length > 0) console.error("sync-matches errors:", errors);

  return Response.json({ competitions: results, settled, errors });
});

async function syncCompetition(
  // deno-lint-ignore no-explicit-any
  supabase: any,
  comp: DbCompetition,
  footballApiToken: string,
  errors: string[]
): Promise<Record<string, unknown>> {
  let apiUrl = `https://api.football-data.org/v4/competitions/${comp.CompetitionCode}/matches`;
  const params = new URLSearchParams();
  if (comp.DateFrom) params.set("dateFrom", comp.DateFrom);
  if (comp.DateTo) params.set("dateTo", comp.DateTo);
  if ([...params].length > 0) apiUrl += `?${params}`;

  const apiResponse = await fetch(apiUrl, {
    headers: { "X-Auth-Token": footballApiToken },
  });

  if (!apiResponse.ok) {
    // 429 = rate limited; skip this run, the round-robin catches up next minute.
    errors.push(`${comp.CompetitionCode}: football-data.org returned ${apiResponse.status}`);
    return { error: apiResponse.status };
  }

  const data = await apiResponse.json();
  const apiMatches: ApiMatch[] = data.matches ?? [];

  const { data: existingMatches } = await supabase
    .from("Matches")
    .select("*")
    .eq("CompetitionId", comp.Id);

  let newCount = 0;
  let updatedCount = 0;

  for (const apiMatch of apiMatches) {
    const existing = existingMatches?.find(
      (m: DbMatch) => m.ExternalId === apiMatch.id
    );

    if (!existing) {
      const { error } = await supabase.from("Matches").insert({
        CompetitionId: comp.Id,
        ExternalId: apiMatch.id,
        HomeTeam: apiMatch.homeTeam.name,
        AwayTeam: apiMatch.awayTeam.name,
        HomeCrest: apiMatch.homeTeam.crest,
        AwayCrest: apiMatch.awayTeam.crest,
        KickoffTime: apiMatch.utcDate,
        Status: apiMatch.status,
        Group: apiMatch.group ?? null,
        Stage: apiMatch.stage ?? null,
        Matchday: apiMatch.matchday ?? null,
      });
      if (error) errors.push(`insert ${comp.CompetitionCode}/${apiMatch.id}: ${error.message}`);
      else newCount++;
    } else {
      // Manually corrected match — the score was set by hand because the API had
      // it wrong. Never let the sync overwrite it (status, score or anything else).
      if (existing.ScoreLocked) {
        continue;
      }

      // The API occasionally flip-flops and reports an already-finished match
      // as TIMED/SCHEDULED again. A finished match never un-finishes — skip it.
      if (existing.Status === "FINISHED" && apiMatch.status !== "FINISHED") {
        continue;
      }

      // The tip game scores against the 90-minute result. For knockout matches
      // decided in extra time or on penalties, football-data.org's `fullTime`
      // is the aggregate (regularTime + extraTime + penalties), e.g. a 1-1 that
      // goes to a 3-4 shootout is reported as fullTime 4-5. `regularTime` holds
      // the score after 90 minutes — prefer it, and fall back to `fullTime` for
      // ordinary matches where `regularTime` is not populated.
      const homeScore: number | null =
        apiMatch.score?.regularTime?.home ??
        apiMatch.score?.fullTime?.home ??
        null;
      const awayScore: number | null =
        apiMatch.score?.regularTime?.away ??
        apiMatch.score?.fullTime?.away ??
        null;

      // Calculate minute from kickoff for live matches
      let minute: number | null = null;
      if (apiMatch.status === "IN_PLAY") {
        const elapsed =
          (Date.now() - new Date(apiMatch.utcDate).getTime()) / 60000;
        minute = Math.min(Math.floor(elapsed), 120);
      }

      const update: Record<string, unknown> = {
        Status: apiMatch.status,
        KickoffTime: apiMatch.utcDate,
        Minute: minute,
        HomeCrest: apiMatch.homeTeam.crest,
        AwayCrest: apiMatch.awayTeam.crest,
        Group: apiMatch.group ?? null,
        Stage: apiMatch.stage ?? null,
        Matchday: apiMatch.matchday ?? null,
      };

      // The API occasionally reports a match without scores (even FINISHED ones).
      // Never overwrite a known score with null — only write scores we actually got.
      if (homeScore !== null) update.HomeScore = homeScore;
      if (awayScore !== null) update.AwayScore = awayScore;

      const { error } = await supabase
        .from("Matches")
        .update(update)
        .eq("Id", existing.Id);

      if (error) errors.push(`update ${comp.CompetitionCode}/${existing.Id}: ${error.message}`);
      else updatedCount++;
    }
  }

  // Auto-finish detection. Two shapes of "done":
  // - Cup: the FINAL has been played and nothing is pending. ("All matches
  //   finished" alone is not enough — knockout fixtures appear as the API
  //   publishes them, so right after the semis everything stored is finished
  //   while the final doesn't exist in the database yet.)
  // - League/date-window (no FINAL stage ever appears): the window has passed
  //   and nothing is pending.
  const { count: pendingCount } = await supabase
    .from("Matches")
    .select("Id", { count: "exact", head: true })
    .eq("CompetitionId", comp.Id)
    .not("Status", "in", '("FINISHED","POSTPONED","CANCELLED")');

  const { count: finishedFinals } = await supabase
    .from("Matches")
    .select("Id", { count: "exact", head: true })
    .eq("CompetitionId", comp.Id)
    .eq("Stage", "FINAL")
    .eq("Status", "FINISHED");

  const windowPassed =
    comp.DateTo != null && new Date(comp.DateTo) < new Date(Date.now() - 24 * 60 * 60 * 1000);
  const nothingPending = (pendingCount ?? 0) === 0;
  const hasAnyMatches = (existingMatches?.length ?? 0) + newCount > 0;

  if (
    hasAnyMatches &&
    nothingPending &&
    ((finishedFinals ?? 0) > 0 || windowPassed)
  ) {
    const { error } = await supabase
      .from("Competitions")
      .update({ Status: "finished" })
      .eq("Id", comp.Id)
      .eq("Status", "active");
    if (error) errors.push(`finish ${comp.CompetitionCode}: ${error.message}`);
    else return { total: apiMatches.length, new: newCount, updated: updatedCount, finished: true };
  }

  return { total: apiMatches.length, new: newCount, updated: updatedCount };
}

// Type definitions for football-data.org API response
interface ApiMatch {
  id: number;
  status: string;
  utcDate: string;
  homeTeam: { name: string; crest: string };
  awayTeam: { name: string; crest: string };
  score: {
    regularTime?: { home: number | null; away: number | null } | null;
    fullTime: { home: number | null; away: number | null };
  } | null;
  group: string | null;
  stage: string | null;
  matchday: number | null;
}

interface DbCompetition {
  Id: number;
  Name: string;
  CompetitionCode: string;
  DateFrom: string | null;
  DateTo: string | null;
  LastSyncedAt: string | null;
}

// Type definition for Supabase Matches row
interface DbMatch {
  Id: number;
  ExternalId: number;
  Status: string;
  HomeScore: number | null;
  AwayScore: number | null;
  ScoreLocked: boolean;
  Group: string | null;
  Stage: string | null;
  Matchday: number | null;
}
