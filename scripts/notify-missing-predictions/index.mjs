// Reminds players who haven't entered a prediction for a match that kicks off
// in roughly 24 hours. Runs hourly from GitHub Actions: each run looks at a
// one-hour window (24h–25h before kickoff) so every match is caught exactly
// once. A "SentReminders" row guards against duplicates on re-runs.
//
// Email is sent over iCloud SMTP using an app-specific password. (SMTP can't run
// reliably inside Supabase Edge Functions, so the sending lives here instead.)
import { createClient } from "@supabase/supabase-js";
import nodemailer from "nodemailer";

const supabaseUrl = process.env.SUPABASE_URL;
const supabaseKey = process.env.SUPABASE_SERVICE_ROLE_KEY;
const smtpUsername = process.env.SMTP_USERNAME;
const smtpPassword = process.env.SMTP_PASSWORD;
// Sender shown to recipients — must be your iCloud address or an alias on it.
const fromAddress = process.env.REMINDER_FROM_ADDRESS ?? smtpUsername;
// Public site URL used in the email's call-to-action link.
const siteUrl = process.env.SITE_URL ?? "https://keva1994.github.io/TipGame/";

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

const now = Date.now();

// Renders the themed reminder email. Shared by the real run and the test run.
// Matches the look of the password-reset email: green hero, rounded card,
// playful Danish tone, info box and footer.
function reminderHtml({ userName, homeTeam, awayTeam, kickoff }) {
  return `<!DOCTYPE html>
<html lang="da">
<head>
	<meta charset="utf-8" />
	<meta name="viewport" content="width=device-width, initial-scale=1.0" />
	<title>Du mangler at tippe en kamp</title>
</head>
<body style="margin:0;padding:0;background:#f3f4f8;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#1f2937;">

	<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#f3f4f8;padding:24px 12px;">
		<tr>
			<td align="center">

				<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:560px;background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.06);">

					<!-- Hero -->
					<tr>
						<td style="background:linear-gradient(135deg,#1B5E20 0%,#388E3C 100%);padding:32px 32px 24px 32px;text-align:center;color:#ffffff;">
							<div style="font-size:48px;line-height:1;margin-bottom:8px;">⚽⏰😱</div>
							<h1 style="margin:0;font-size:24px;font-weight:700;letter-spacing:-0.01em;">
								Du mangler at tippe!
							</h1>
							<p style="margin:8px 0 0 0;font-size:14px;opacity:0.92;">
								Kampen venter ikke. Det gør dine konkurrenter heller ikke.
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
								Du har <strong>endnu ikke</strong> sat dit tip til en kamp i <strong>VM Tips Kuponen</strong>.
								Måske glemte du det. Måske ventede du på et tegn fra oven.
								Dette er tegnet. ☝️
							</p>

							<!-- Match box -->
							<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#F1F8E9;border:1px solid #C5E1A5;border-radius:10px;margin:0 0 24px 0;">
								<tr>
									<td style="padding:18px 16px;text-align:center;">
										<div style="font-size:20px;font-weight:700;color:#1B5E20;">${homeTeam} – ${awayTeam}</div>
										<div style="font-size:14px;color:#5d6b5d;margin-top:4px;">${kickoff}</div>
									</td>
								</tr>
							</table>

							<p style="margin:0 0 24px 0;font-size:16px;line-height:1.5;">
								Klik på den meget flotte knap herunder og gæt resultatet.
								Selv et vildt gæt er bedre end nul point. Vi tror på dig.
							</p>

							<!-- CTA Button -->
							<table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:0 auto 24px auto;">
								<tr>
									<td align="center" style="background:#1B5E20;border-radius:10px;">
										<a href="${siteUrl}"
										   style="display:inline-block;padding:14px 28px;color:#ffffff;text-decoration:none;font-weight:600;font-size:16px;border-radius:10px;">
											⚽ Registrér mit tip
										</a>
									</td>
								</tr>
							</table>

							<p style="margin:0 0 8px 0;font-size:13px;color:#6b7280;line-height:1.5;">
								Virker knappen ikke? (Den slags sker.)
								Kopiér så dette link ind i din browser:
							</p>
							<p style="margin:0 0 24px 0;font-size:12px;word-break:break-all;">
								<a href="${siteUrl}" style="color:#1B5E20;text-decoration:underline;">${siteUrl}</a>
							</p>

							<!-- Info box -->
							<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#FFF8E1;border:1px solid #FFE082;border-radius:10px;margin:16px 0;">
								<tr>
									<td style="padding:14px 16px;font-size:13px;color:#5d4500;line-height:1.5;">
										⏰ <strong>Kampen starter om ca. 24 timer</strong> — derefter lukker tipningen hurtigere end Danmark i kvalifikationen.
									</td>
								</tr>
							</table>

							<p style="margin:24px 0 0 0;font-size:13px;color:#6b7280;line-height:1.5;">
								<strong>Har du allerede tippet?</strong> Så slap af — så er denne mail bare en venlig bot, der overreagerer.
							</p>
						</td>
					</tr>

					<!-- Footer -->
					<tr>
						<td style="background:#F5F5F5;padding:20px 32px;text-align:center;border-top:1px solid #E0E0E0;">
							<p style="margin:0;font-size:13px;color:#1B5E20;font-weight:700;">
								⚽ VM Tips Kuponen 2026
							</p>
							<p style="margin:6px 0 0 0;font-size:11px;color:#9E9E9E;">
								Denne e-mail er sendt automatisk af en bot uden følelser. Den læser ikke svar.
								Men hvis du svarer alligevel, så er du sgu sej. 💌
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

// Test mode: send a single sample email to a fixed address and exit, without
// touching the database. Triggered manually from GitHub Actions.
if (process.env.SEND_TEST === "true") {
  const to = process.env.TEST_EMAIL_TO || fromAddress;
  const homeTeam = "Danmark";
  const awayTeam = "Brasilien";
  const kickoff = new Date(now + 24 * 60 * 60 * 1000).toLocaleString("da-DK", {
    timeZone: "Europe/Copenhagen",
    weekday: "long",
    day: "numeric",
    month: "long",
    hour: "2-digit",
    minute: "2-digit",
  });
  await transporter.sendMail({
    from: fromAddress,
    to,
    subject: `Husk dit tip til VM Tips Kuponen: ${homeTeam} – ${awayTeam}`,
    html: reminderHtml({ userName: "Kasper", homeTeam, awayTeam, kickoff }),
  });
  console.log(`Test email sent to ${to}.`);
  process.exit(0);
}

const windowStart = new Date(now + 24 * 60 * 60 * 1000).toISOString();
const windowEnd = new Date(now + 25 * 60 * 60 * 1000).toISOString();

// Matches kicking off in the 24h–25h window that aren't already underway/over.
const { data: matches, error: matchError } = await supabase
  .from("Matches")
  .select("Id, HomeTeam, AwayTeam, KickoffTime, Status")
  .gte("KickoffTime", windowStart)
  .lt("KickoffTime", windowEnd)
  .not("Status", "in", '("FINISHED","IN_PLAY","PAUSED","POSTPONED","CANCELLED")');

if (matchError) {
  console.error("Failed to load matches:", matchError.message);
  process.exit(1);
}

if (!matches || matches.length === 0) {
  console.log("No matches in the 24–25h window. Nothing to do.");
  process.exit(0);
}

// All players (we need AuthId to look up their email in Supabase Auth).
const { data: users, error: userError } = await supabase
  .from("Users")
  .select("Id, Name, AuthId");

if (userError) {
  console.error("Failed to load users:", userError.message);
  process.exit(1);
}

let sent = 0;
const errors = [];

for (const match of matches) {
  // Players who already predicted this match.
  const { data: preds, error: predError } = await supabase
    .from("Predictions")
    .select("UserId")
    .eq("MatchId", match.Id);
  if (predError) {
    errors.push(`predictions ${match.Id}: ${predError.message}`);
    continue;
  }
  const predicted = new Set((preds ?? []).map((p) => p.UserId));

  // Players already reminded about this match (idempotency guard).
  const { data: reminded, error: remError } = await supabase
    .from("SentReminders")
    .select("UserId")
    .eq("MatchId", match.Id);
  if (remError) {
    errors.push(`reminders ${match.Id}: ${remError.message}`);
    continue;
  }
  const alreadyReminded = new Set((reminded ?? []).map((r) => r.UserId));

  const kickoff = new Date(match.KickoffTime).toLocaleString("da-DK", {
    timeZone: "Europe/Copenhagen",
    weekday: "long",
    day: "numeric",
    month: "long",
    hour: "2-digit",
    minute: "2-digit",
  });

  for (const user of users ?? []) {
    if (predicted.has(user.Id) || alreadyReminded.has(user.Id)) continue;
    if (!user.AuthId) continue;

    // Resolve email from Supabase Auth.
    const { data: authData, error: authErr } =
      await supabase.auth.admin.getUserById(user.AuthId);
    const email = authData?.user?.email;
    if (authErr || !email) {
      if (authErr) errors.push(`auth ${user.Id}: ${authErr.message}`);
      continue;
    }

    try {
      await transporter.sendMail({
        from: fromAddress,
        to: email,
        subject: `Husk dit tip til VM Tips Kuponen: ${match.HomeTeam} – ${match.AwayTeam}`,
        html: reminderHtml({
          userName: user.Name,
          homeTeam: match.HomeTeam,
          awayTeam: match.AwayTeam,
          kickoff,
        }),
      });
    } catch (e) {
      errors.push(`smtp ${user.Id}/${match.Id}: ${e instanceof Error ? e.message : String(e)}`);
      continue;
    }

    const { error: insErr } = await supabase
      .from("SentReminders")
      .insert({ UserId: user.Id, MatchId: match.Id });
    if (insErr) errors.push(`mark ${user.Id}/${match.Id}: ${insErr.message}`);
    else sent++;
  }
}

console.log(`Done. matches=${matches.length} sent=${sent} errors=${errors.length}`);
if (errors.length > 0) {
  console.error("Errors:", errors);
  process.exit(1);
}
