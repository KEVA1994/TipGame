// Processes the EmailRequests queue (kind='final_standings'): for each
// competition an admin has requested it for, computes the final standings and
// mails every member exactly once, then stamps ProcessedAt as a dedup guard.
// Triggered on a schedule from GitHub Actions (etape 6 of
// docs/PLAN-multi-turnering.md) — admins queue the mail from the app's Admin
// page (request_email RPC); this script is the worker that drains the queue.
//
// Standings are computed live from the database (sum of Predictions.Points
// per competition member, ties share a rank), so the mail always matches the
// app's leaderboard for that competition.
//
// Email is sent over iCloud SMTP using an app-specific password, same setup
// as scripts/notify-missing-predictions.
import { createClient } from "@supabase/supabase-js";
import nodemailer from "nodemailer";

const supabaseUrl = process.env.SUPABASE_URL;
const supabaseKey = process.env.SUPABASE_SERVICE_ROLE_KEY;
const smtpUsername = process.env.SMTP_USERNAME;
const smtpPassword = process.env.SMTP_PASSWORD;
// Sender shown to recipients — must be your iCloud address or an alias on it.
// Use || (not ??) so an empty secret from GitHub Actions falls back too.
const fromAddress = process.env.REMINDER_FROM_ADDRESS || smtpUsername;
const siteUrl = process.env.SITE_URL || "https://keva1994.github.io/TipGame/";
// Preview guard: when set, every matching request is rendered and sent ONLY
// to this address, and ProcessedAt is NOT stamped — the request stays queued
// for the real run. Lets an admin preview the exact mail before it goes out.
const onlyEmail = (process.env.ONLY_EMAIL || "").trim().toLowerCase() || null;

for (const [name, value] of Object.entries({
  SUPABASE_URL: supabaseUrl,
  SUPABASE_SERVICE_ROLE_KEY: supabaseKey,
  SMTP_USERNAME: smtpUsername,
  SMTP_PASSWORD: smtpPassword,
})) {
  if (!value) {
    console.error(`Missing required environment variable: ${name}`);
    process.exit(1);
  }
}

const supabase = createClient(supabaseUrl, supabaseKey);

const transporter = nodemailer.createTransport({
  host: "smtp.mail.me.com",
  port: 587,
  secure: false, // STARTTLS is negotiated on port 587
  requireTLS: true,
  auth: { user: smtpUsername, pass: smtpPassword },
});

// PostgREST caps responses at 1000 rows — page through everything or large
// competitions' standings come out wrong (computed from only some matches).
async function fetchAll(table, select, filters) {
  const rows = [];
  const pageSize = 1000;
  for (let from = 0; ; from += pageSize) {
    let query = supabase.from(table).select(select).order("Id", { ascending: true });
    for (const [col, val] of Object.entries(filters ?? {})) query = query.eq(col, val);
    const { data: page, error } = await query.range(from, from + pageSize - 1);
    if (error) throw new Error(`${table}: ${error.message}`);
    rows.push(...(page ?? []));
    if (!page || page.length < pageSize) break;
  }
  return rows;
}

function standingsRowsHtml(standings, meId) {
  const medals = { 1: "🥇", 2: "🥈", 3: "🥉" };
  return standings
    .map((row) => {
      const isMe = row.Id === meId;
      const bg = isMe ? "background:#F1F8E9;" : "";
      const weight = isMe || row.rank <= 3 ? "font-weight:700;" : "";
      return `<tr style="${bg}">
        <td style="padding:8px 12px;border-bottom:1px solid #eee;font-size:14px;${weight}text-align:center;white-space:nowrap;">${medals[row.rank] ?? row.rank + "."}</td>
        <td style="padding:8px 12px;border-bottom:1px solid #eee;font-size:14px;${weight}">${row.Name}${isMe ? " (dig)" : ""}</td>
        <td style="padding:8px 12px;border-bottom:1px solid #eee;font-size:14px;${weight}text-align:right;">${row.points}</td>
      </tr>`;
    })
    .join("\n");
}

function finalHtml({ competitionName, matchCount, userName, me, standings }) {
  const [gold, silver, bronze] = standings;
  const personal =
    me && me.rank <= 3
      ? `Og hold nu fast: <strong>du sluttede som nr. ${me.rank} med ${me.points} point!</strong> Kassen er din. 🎉`
      : me
        ? `Du sluttede som <strong>nr. ${me.rank} med ${me.points} point</strong>. ${me.rank <= 10 ? "Solidt tippet! 💪" : "Der bliver nye chancer — vi tror på comebacket. 💪"}`
        : "";
  return `<!DOCTYPE html>
<html lang="da">
<head>
	<meta charset="utf-8" />
	<meta name="viewport" content="width=device-width, initial-scale=1.0" />
	<title>${competitionName} — vinderne er fundet!</title>
</head>
<body style="margin:0;padding:0;background:#f3f4f8;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#1f2937;">

	<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#f3f4f8;padding:24px 12px;">
		<tr>
			<td align="center">

				<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:560px;background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.06);">

					<!-- Hero -->
					<tr>
						<td style="background:linear-gradient(135deg,#1B5E20 0%,#388E3C 100%);padding:32px 32px 24px 32px;text-align:center;color:#ffffff;">
							<div style="font-size:48px;line-height:1;margin-bottom:8px;">🏆⚽🎉</div>
							<h1 style="margin:0;font-size:24px;font-weight:700;letter-spacing:-0.01em;">
								Slut — vinderne er fundet!
							</h1>
							<p style="margin:8px 0 0 0;font-size:14px;opacity:0.92;">
								${matchCount} kampe. Mange tips. Én mester.
							</p>
						</td>
					</tr>

					<!-- Body -->
					<tr>
						<td style="padding:32px;">
							<p style="margin:0 0 16px 0;font-size:16px;line-height:1.5;">
								Hej ${userName} 👋
							</p>

							<p style="margin:0 0 16px 0;font-size:16px;line-height:1.5;">
								Så blev der fløjtet af for sidste gang, og <strong>${competitionName}</strong>
								er officielt slut. Tak fordi du tippede med!
							</p>

							<!-- Podium box -->
							<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#FFF8E1;border:1px solid #FFE082;border-radius:10px;margin:0 0 24px 0;">
								<tr>
									<td style="padding:20px 16px;text-align:center;">
										<div style="font-size:13px;font-weight:700;color:#5d4500;text-transform:uppercase;letter-spacing:0.08em;margin-bottom:12px;">Sejrsskamlen</div>
										<div style="font-size:18px;font-weight:700;color:#1B5E20;margin-bottom:6px;">🥇 ${gold.Name} — ${gold.points} point</div>
										${silver ? `<div style="font-size:16px;font-weight:600;color:#455A64;margin-bottom:6px;">🥈 ${silver.Name} — ${silver.points} point</div>` : ""}
										${bronze ? `<div style="font-size:16px;font-weight:600;color:#795548;">🥉 ${bronze.Name} — ${bronze.points} point</div>` : ""}
									</td>
								</tr>
							</table>

							<p style="margin:0 0 24px 0;font-size:16px;line-height:1.5;">
								Kæmpe tillykke til dem på podiet! 👏 ${personal}
							</p>

							<!-- Full standings -->
							<div style="font-size:13px;font-weight:700;color:#1B5E20;text-transform:uppercase;letter-spacing:0.08em;margin-bottom:8px;">Slutstillingen</div>
							<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="border:1px solid #E0E0E0;border-radius:10px;border-collapse:separate;overflow:hidden;margin:0 0 24px 0;">
								<tr style="background:#F5F5F5;">
									<th style="padding:8px 12px;font-size:12px;text-align:center;color:#6b7280;">#</th>
									<th style="padding:8px 12px;font-size:12px;text-align:left;color:#6b7280;">Spiller</th>
									<th style="padding:8px 12px;font-size:12px;text-align:right;color:#6b7280;">Point</th>
								</tr>
								${standingsRowsHtml(standings, me?.Id)}
							</table>

							<p style="margin:0 0 8px 0;font-size:13px;color:#6b7280;line-height:1.5;text-align:center;">
								Hele stillingen, statistik og alle jeres (mere eller mindre heldige) tips kan stadig ses her:
							</p>
							<p style="margin:0 0 8px 0;font-size:12px;word-break:break-all;text-align:center;">
								<a href="${siteUrl}" style="color:#1B5E20;text-decoration:underline;">${siteUrl}</a>
							</p>

							<!-- ROFUS -->
							<p style="margin:24px 0 0 0;font-size:13px;color:#6b7280;line-height:1.5;text-align:center;">
								🎰 Har tipningen taget overhånd? Så kan du helt gratis melde dig ind i
								<a href="https://spillemyndigheden.dk/rofus" style="color:#1B5E20;text-decoration:underline;">ROFUS</a>
								— Spillemyndighedens register over frivilligt udelukkede spillere.
								(Vi regner nu med, at det kun var æren, der stod på spil. 😄)
							</p>
						</td>
					</tr>

					<!-- Footer -->
					<tr>
						<td style="background:#F5F5F5;padding:20px 32px;text-align:center;border-top:1px solid #E0E0E0;">
							<p style="margin:0;font-size:13px;color:#1B5E20;font-weight:700;">
								⚽ ${competitionName} — slut og tak for denne gang!
							</p>
							<p style="margin:6px 0 0 0;font-size:11px;color:#9E9E9E;">
								Denne e-mail er sendt automatisk af en bot uden følelser — men selv den synes, det gik godt.
								Den læser ikke svar, men svar gerne alligevel, så er du sgu sej. 💌
							</p>
						</td>
					</tr>

				</table>

			</td>
		</tr>
	</table>

</body>
</html>`;
}

async function processRequest(request) {
  const { data: competition, error: compError } = await supabase
    .from("Competitions")
    .select("Id, Name")
    .eq("Id", request.CompetitionId)
    .single();
  if (compError || !competition) {
    console.error(`[request ${request.Id}] Competition not found: ${compError?.message}`);
    return { sent: 0, skipped: true };
  }

  // Refuse to declare winners while matches are still unplayed. Leaves the
  // request pending — retried on the next scheduled run.
  const { data: unfinished, error: matchCheckError } = await supabase
    .from("Matches")
    .select("Id")
    .eq("CompetitionId", competition.Id)
    .neq("Status", "FINISHED")
    .limit(1);
  if (matchCheckError) {
    console.error(`[${competition.Name}] Failed to check matches:`, matchCheckError.message);
    return { sent: 0, skipped: true };
  }
  if (unfinished && unfinished.length > 0) {
    console.log(`[${competition.Name}] Matches still unfinished — will retry later.`);
    return { sent: 0, skipped: true };
  }

  const members = await fetchAll("CompetitionMembers", "UserId", { CompetitionId: competition.Id });
  const memberIds = [...new Set(members.map((m) => m.UserId))];
  if (memberIds.length === 0) {
    console.log(`[${competition.Name}] No members — nothing to send.`);
    return { sent: 0, skipped: false };
  }

  const { data: users, error: userError } = await supabase
    .from("Users")
    .select("Id, Name, AuthId")
    .in("Id", memberIds);
  if (userError) throw new Error(`users: ${userError.message}`);

  const matches = await fetchAll("Matches", "Id", { CompetitionId: competition.Id });
  const matchIds = new Set(matches.map((m) => m.Id));

  const allPreds = await fetchAll("Predictions", "UserId, MatchId, Points");
  const preds = allPreds.filter((p) => matchIds.has(p.MatchId) && memberIds.includes(p.UserId));

  const pointsByUser = new Map();
  for (const p of preds) {
    pointsByUser.set(p.UserId, (pointsByUser.get(p.UserId) ?? 0) + (p.Points ?? 0));
  }

  const standings = (users ?? [])
    .map((u) => ({ ...u, points: pointsByUser.get(u.Id) ?? 0 }))
    .sort((a, b) => b.points - a.points || a.Name.localeCompare(b.Name, "da"));
  let lastPoints = null;
  let lastRank = 0;
  standings.forEach((row, i) => {
    row.rank = row.points === lastPoints ? lastRank : i + 1;
    lastPoints = row.points;
    lastRank = row.rank;
  });

  let sent = 0;
  let skippedNoEmail = 0;
  const errors = [];

  for (const row of standings) {
    if (!row.AuthId) {
      skippedNoEmail++;
      continue;
    }

    const { data: authData, error: authErr } =
      await supabase.auth.admin.getUserById(row.AuthId);
    const email = authData?.user?.email;
    if (authErr || !email) {
      if (authErr) errors.push(`auth ${row.Id}: ${authErr.message}`);
      else skippedNoEmail++;
      continue;
    }

    if (onlyEmail && email.toLowerCase() !== onlyEmail) continue;

    const html = finalHtml({
      competitionName: competition.Name,
      matchCount: matches.length,
      userName: row.Name,
      me: row,
      standings,
    });

    try {
      await transporter.sendMail({
        from: fromAddress,
        to: email,
        subject: `🏆 ${competition.Name}: Vinderne er fundet — tillykke ${standings[0].Name}!`,
        html,
      });
      sent++;
      console.log(`[${competition.Name}] Sent to ${row.Name}`);
    } catch (e) {
      errors.push(`smtp ${row.Id}: ${e instanceof Error ? e.message : String(e)}`);
    }
  }

  console.log(
    `[${competition.Name}] players=${standings.length} sent=${sent} noEmail=${skippedNoEmail} errors=${errors.length}` +
      (onlyEmail ? ` (ONLY_EMAIL preview — not marking processed)` : ""),
  );
  if (errors.length > 0) console.error(`[${competition.Name}] Errors:`, errors);

  if (!onlyEmail) {
    const { error: markError } = await supabase
      .from("EmailRequests")
      .update({ ProcessedAt: new Date().toISOString() })
      .eq("Id", request.Id);
    if (markError) {
      console.error(`[${competition.Name}] Failed to mark request processed:`, markError.message);
    }
  }

  return { sent, skipped: false };
}

const { data: pending, error: queueError } = await supabase
  .from("EmailRequests")
  .select("Id, CompetitionId")
  .eq("Kind", "final_standings")
  .is("ProcessedAt", null);

if (queueError) {
  console.error("Failed to load email queue:", queueError.message);
  process.exit(1);
}

if (!pending || pending.length === 0) {
  console.log("No pending final-standings requests. Nothing to do.");
  process.exit(0);
}

let totalSent = 0;
let hadError = false;

for (const request of pending) {
  try {
    const { sent } = await processRequest(request);
    totalSent += sent;
  } catch (e) {
    hadError = true;
    console.error(`[request ${request.Id}] Failed:`, e instanceof Error ? e.message : String(e));
  }
}

console.log(`Done. requests=${pending.length} sent=${totalSent}`);
if (hadError) process.exit(1);
