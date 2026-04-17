import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

Deno.serve(async () => {
  const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
  const supabaseKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
  const footballApiToken = Deno.env.get("FOOTBALL_API_TOKEN")!;
  const competition = Deno.env.get("FOOTBALL_API_COMPETITION") ?? "PL";
  const dateFrom = Deno.env.get("FOOTBALL_API_DATE_FROM") ?? "2026-03-30";
  const dateTo = Deno.env.get("FOOTBALL_API_DATE_TO") ?? "2026-04-27";
  const footballApiUrl = `https://api.football-data.org/v4/competitions/${competition}/matches?dateFrom=${dateFrom}&dateTo=${dateTo}`;

  const supabase = createClient(supabaseUrl, supabaseKey);

  // Check if there are any non-finished matches — skip API call if all are done
  const { count: pendingCount } = await supabase
    .from("Matches")
    .select("Id", { count: "exact", head: true })
    .not("Status", "in", '("FINISHED","POSTPONED","CANCELLED")');

  const { count: totalCount } = await supabase
    .from("Matches")
    .select("Id", { count: "exact", head: true });

  if (totalCount && totalCount > 0 && (!pendingCount || pendingCount === 0)) {
    return Response.json({ skipped: true, reason: "All matches finished" });
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

  for (const apiMatch of apiMatches) {
    const existing = existingMatches?.find(
      (m: DbMatch) => m.ExternalId === apiMatch.id
    );

    if (!existing) {
      await supabase.from("Matches").insert({
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
      newCount++;
    } else {
      const wasFinished = existing.Status === "FINISHED";
      const homeScore: number | null = apiMatch.score?.fullTime?.home ?? null;
      const awayScore: number | null = apiMatch.score?.fullTime?.away ?? null;

      // Calculate minute from kickoff for live matches
      let minute: number | null = null;
      if (apiMatch.status === "IN_PLAY") {
        const elapsed =
          (Date.now() - new Date(apiMatch.utcDate).getTime()) / 60000;
        minute = Math.min(Math.floor(elapsed), 120);
      }

      await supabase
        .from("Matches")
        .update({
          Status: apiMatch.status,
          HomeScore: homeScore,
          AwayScore: awayScore,
          KickoffTime: apiMatch.utcDate,
          Minute: minute,
          HomeCrest: apiMatch.homeTeam.crest,
          AwayCrest: apiMatch.awayTeam.crest,
          Group: apiMatch.group ?? null,
          Stage: apiMatch.stage ?? null,
          Matchday: apiMatch.matchday ?? null,
        })
        .eq("Id", existing.Id);

      updatedCount++;

      // Calculate points when a match finishes
      if (
        apiMatch.status === "FINISHED" &&
        !wasFinished &&
        homeScore !== null &&
        awayScore !== null
      ) {
        const { data: predictions } = await supabase
          .from("Predictions")
          .select("*")
          .eq("MatchId", existing.Id);

        if (predictions) {
          for (const pred of predictions) {
            let points = 0;

            if (
              pred.PredictedHome === homeScore &&
              pred.PredictedAway === awayScore
            ) {
              // Exact score
              points = 3;
            } else if (
              pred.PredictedHome === pred.PredictedAway &&
              homeScore === awayScore
            ) {
              // Both predicted draw
              points = 2;
            } else if (
              (pred.PredictedHome > pred.PredictedAway &&
                homeScore > awayScore) ||
              (pred.PredictedHome < pred.PredictedAway &&
                homeScore < awayScore)
            ) {
              // Correct winner
              points = 1;
            }

            await supabase
              .from("Predictions")
              .update({ Points: points })
              .eq("Id", pred.Id);
          }
        }
      }
    }
  }

  return Response.json({
    total: apiMatches.length,
    new: newCount,
    updated: updatedCount,
  });
});

// Type definitions for football-data.org API response
interface ApiMatch {
  id: number;
  status: string;
  utcDate: string;
  homeTeam: { name: string; crest: string };
  awayTeam: { name: string; crest: string };
  score: { fullTime: { home: number | null; away: number | null } } | null;
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
  Group: string | null;
  Stage: string | null;
  Matchday: number | null;
}
