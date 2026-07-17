import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

Deno.serve(async () => {
  const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
  const supabaseKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
  const footballApiToken = Deno.env.get("FOOTBALL_API_TOKEN")!;
  const footballApiUrl = "https://api.football-data.org/v4/competitions/WC/matches";

  const supabase = createClient(supabaseUrl, supabaseKey);

  // Skip API call only when the tournament is actually over: every stored match
  // is finished AND the final itself has been played. "All matches finished"
  // alone is not enough — knockout fixtures are inserted as the API publishes
  // them, so right after the semifinals every stored match is finished while
  // the final and third-place match don't exist in the database yet.
  const { count: pendingCount } = await supabase
    .from("Matches")
    .select("Id", { count: "exact", head: true })
    .not("Status", "in", '("FINISHED","POSTPONED","CANCELLED")');

  const { count: finishedFinals } = await supabase
    .from("Matches")
    .select("Id", { count: "exact", head: true })
    .eq("Stage", "FINAL")
    .eq("Status", "FINISHED");

  if (finishedFinals && finishedFinals > 0 && (!pendingCount || pendingCount === 0)) {
    return Response.json({ skipped: true, reason: "Tournament finished" });
  }

  // Fetch from football-data.org
  const apiResponse = await fetch(footballApiUrl, {
    headers: { "X-Auth-Token": footballApiToken },
  });

  if (!apiResponse.ok) {
    return Response.json(
      { error: `football-data.org returned ${apiResponse.status}` },
      { status: 502 }
    );
  }

  const data = await apiResponse.json();
  const apiMatches: ApiMatch[] = data.matches ?? [];

  // Get existing matches from Supabase
  const { data: existingMatches } = await supabase
    .from("Matches")
    .select("*");

  let newCount = 0;
  let updatedCount = 0;
  const errors: string[] = [];

  for (const apiMatch of apiMatches) {
    const existing = existingMatches?.find(
      (m: DbMatch) => m.ExternalId === apiMatch.id
    );

    if (!existing) {
      const { error } = await supabase.from("Matches").insert({
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
      if (error) errors.push(`insert ${apiMatch.id}: ${error.message}`);
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

      if (error) errors.push(`update ${existing.Id}: ${error.message}`);
      else updatedCount++;
    }
  }

  // Settle points for all finished matches. Idempotent — recalculates every run,
  // so a missed/failed run self-heals instead of leaving predictions at 0 forever.
  let settled = 0;
  const { data: settledCount, error: settleError } = await supabase.rpc(
    "settle_points"
  );
  if (settleError) errors.push(`settle_points: ${settleError.message}`);
  else settled = settledCount ?? 0;

  if (errors.length > 0) console.error("sync-matches errors:", errors);

  return Response.json({
    total: apiMatches.length,
    new: newCount,
    updated: updatedCount,
    settled,
    errors,
  });
});

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
