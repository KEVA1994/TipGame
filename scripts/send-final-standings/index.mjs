// One-shot end-of-tournament email: congratulates the winners and shows the
// final standings to every player. Triggered manually from GitHub Actions
// (workflow_dispatch) — it has no schedule and no dedup table, so a re-run
// mails everyone again.
//
// Standings are computed live from the database (sum of Predictions.Points per
// user, ties share a rank), so the mail always matches the app's leaderboard.
//
// Email is sent over iCloud SMTP using an app-specific password, same setup as
// scripts/notify-missing-predictions.
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
// Preview guard: when set, the full real flow runs but only this address is
// mailed. Use it to send yourself the final mail before the real run.
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

// Refuse to declare winners while matches are still unplayed.
const { data: unfinished, error: matchError } = await supabase
  .from("Matches")
  .select("Id")
  .neq("Status", "FINISHED")
  .limit(1);
if (matchError) {
  console.error("Failed to check matches:", matchError.message);
  process.exit(1);
}
if (unfinished && unfinished.length > 0) {
  console.error("There are unfinished matches — refusing to send final standings.");
  process.exit(1);
}

const { data: users, error: userError } = await supabase
  .from("Users")
  .select("Id, Name, AuthId");
if (userError) {
  console.error("Failed to load users:", userError.message);
  process.exit(1);
}

// PostgREST caps responses at 1000 rows — with ~28 players × ~104 matches the
// table is ~3x that, so page through everything or the standings come out
// wrong (computed from only the earliest matches).
const preds = [];
const pageSize = 1000;
for (let from = 0; ; from += pageSize) {
  const { data: page, error: predError } = await supabase
    .from("Predictions")
    .select("UserId, Points")
    .order("Id", { ascending: true })
    .range(from, from + pageSize - 1);
  if (predError) {
    console.error("Failed to load predictions:", predError.message);
    process.exit(1);
  }
  preds.push(...(page ?? []));
  if (!page || page.length < pageSize) break;
}
console.log(`Loaded ${preds.length} predictions.`);

const pointsByUser = new Map();
for (const p of preds ?? []) {
  pointsByUser.set(p.UserId, (pointsByUser.get(p.UserId) ?? 0) + (p.Points ?? 0));
}

// Competition ranking: sorted by points, ties share a rank (1, 2, 2, 4, ...).
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

const [gold, silver, bronze] = standings;

function standingsRowsHtml(meId) {
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

function finalHtml({ userName, me }) {
  const personal =
    me && me.rank <= 3
      ? `Og hold nu fast: <strong>du sluttede som nr. ${me.rank} med ${me.points} point!</strong> Kassen er din. 🎉`
      : me
        ? `Du sluttede som <strong>nr. ${me.rank} med ${me.points} point</strong>. ${me.rank <= 10 ? "Solidt tippet! 💪" : "Der er et VM igen i 2030 — vi tror på comebacket. 💪"}`
        : "";
  return `<!DOCTYPE html>
<html lang="da">
<head>
	<meta charset="utf-8" />
	<meta name="viewport" content="width=device-width, initial-scale=1.0" />
	<title>VM Tips Kuponen 2026 — vinderne er fundet!</title>
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
								VM er slut — vinderne er fundet!
							</h1>
							<p style="margin:8px 0 0 0;font-size:14px;opacity:0.92;">
								104 kampe. Tusindvis af tips. Én mester.
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
								Så blev der fløjtet af for sidste gang, og <strong>VM Tips Kuponen 2026</strong>
								er officielt slut. Tak fordi du tippede med — uden jer var det bare
								et regneark med følelser.
							</p>

							<!-- Podium box -->
							<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#FFF8E1;border:1px solid #FFE082;border-radius:10px;margin:0 0 24px 0;">
								<tr>
									<td style="padding:20px 16px;text-align:center;">
										<div style="font-size:13px;font-weight:700;color:#5d4500;text-transform:uppercase;letter-spacing:0.08em;margin-bottom:12px;">Sejrsskamlen</div>
										<div style="font-size:18px;font-weight:700;color:#1B5E20;margin-bottom:6px;">🥇 ${gold.Name} — ${gold.points} point</div>
										<div style="font-size:16px;font-weight:600;color:#455A64;margin-bottom:6px;">🥈 ${silver.Name} — ${silver.points} point</div>
										<div style="font-size:16px;font-weight:600;color:#795548;">🥉 ${bronze.Name} — ${bronze.points} point</div>
									</td>
								</tr>
							</table>

							<p style="margin:0 0 24px 0;font-size:16px;line-height:1.5;">
								Kæmpe tillykke til de tre på skamlen! 👏 ${personal}
							</p>

							<!-- Full standings -->
							<div style="font-size:13px;font-weight:700;color:#1B5E20;text-transform:uppercase;letter-spacing:0.08em;margin-bottom:8px;">Slutstillingen</div>
							<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="border:1px solid #E0E0E0;border-radius:10px;border-collapse:separate;overflow:hidden;margin:0 0 24px 0;">
								<tr style="background:#F5F5F5;">
									<th style="padding:8px 12px;font-size:12px;text-align:center;color:#6b7280;">#</th>
									<th style="padding:8px 12px;font-size:12px;text-align:left;color:#6b7280;">Spiller</th>
									<th style="padding:8px 12px;font-size:12px;text-align:right;color:#6b7280;">Point</th>
								</tr>
								__STANDINGS_ROWS__
							</table>

							<p style="margin:0 0 8px 0;font-size:13px;color:#6b7280;line-height:1.5;text-align:center;">
								Hele stillingen, statistik og alle jeres (mere eller mindre heldige) tips kan stadig ses her:
							</p>
							<p style="margin:0 0 8px 0;font-size:12px;word-break:break-all;text-align:center;">
								<a href="${siteUrl}" style="color:#1B5E20;text-decoration:underline;">${siteUrl}</a>
							</p>
						</td>
					</tr>

					<!-- Footer -->
					<tr>
						<td style="background:#F5F5F5;padding:20px 32px;text-align:center;border-top:1px solid #E0E0E0;">
							<p style="margin:0;font-size:13px;color:#1B5E20;font-weight:700;">
								⚽ VM Tips Kuponen 2026 — slut og tak for denne gang!
							</p>
							<p style="margin:6px 0 0 0;font-size:11px;color:#9E9E9E;">
								Denne e-mail er sendt automatisk af en bot uden følelser — men selv den synes, det var et godt VM.
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

  const html = finalHtml({ userName: row.Name, me: row }).replace(
    "__STANDINGS_ROWS__",
    standingsRowsHtml(row.Id),
  );

  try {
    await transporter.sendMail({
      from: fromAddress,
      to: email,
      subject: `🏆 VM Tips Kuponen 2026: Vinderne er fundet — tillykke ${gold.Name}!`,
      html,
    });
    sent++;
    console.log(`Sent to ${row.Name}`);
  } catch (e) {
    errors.push(`smtp ${row.Id}: ${e instanceof Error ? e.message : String(e)}`);
  }
}

console.log(
  `Done. players=${standings.length} sent=${sent} noEmail=${skippedNoEmail} errors=${errors.length}` +
    (onlyEmail ? ` (ONLY_EMAIL=${onlyEmail})` : ""),
);
if (errors.length > 0) {
  console.error("Errors:", errors);
  process.exit(1);
}
