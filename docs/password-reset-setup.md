# Supabase opsætning — Glemt kodeord

For at "Glemt kodeord"-flowet virker i produktion skal du lave to ting i Supabase Dashboard. Du skal kun gøre det én gang.

## 1. Tilføj redirect URLs

**Authentication → URL Configuration → Redirect URLs** — tilføj begge:

- `https://keva1994.github.io/TipGame/nulstil-kodeord`
- `http://localhost:5000/nulstil-kodeord` (eller hvilken port du kører lokalt på — kig i `Properties/launchSettings.json`)

> **Vigtigt:** Uden disse i listen vil Supabase nægte at sende brugeren tilbage til appen efter klik i e-mailen.

Det kan også være en god idé at sætte **Site URL** til `https://keva1994.github.io/TipGame/` så Supabase bruger det som default.

## 2. Skift e-mail-skabelonen til dansk

**Authentication → Email Templates → Reset Password** — udskift indholdet med:

**Subject:**
```
Nulstil dit kodeord til VM Tips Kuponen
```

**Body (HTML):**
```html
<h2>Nulstil dit kodeord</h2>

<p>Hej!</p>

<p>Vi har modtaget en anmodning om at nulstille kodeordet til din konto på <strong>VM Tips Kuponen</strong>.</p>

<p>Klik på knappen herunder for at vælge et nyt kodeord:</p>

<p>
	<a href="{{ .ConfirmationURL }}"
	   style="display:inline-block;padding:12px 24px;background:#594AE2;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:600;">
		Nulstil mit kodeord
	</a>
</p>

<p>Eller kopiér dette link ind i din browser:</p>
<p><a href="{{ .ConfirmationURL }}">{{ .ConfirmationURL }}</a></p>

<p>Linket er gyldigt i 1 time. Har du ikke selv anmodet om at nulstille dit kodeord, kan du roligt ignorere denne e-mail — der sker ingenting.</p>

<p>God fornøjelse med VM 2026! ⚽</p>

<p style="color:#888;font-size:12px;margin-top:32px;">
	Denne e-mail er sendt automatisk. Du behøver ikke svare på den.
</p>
```

> Variablen `{{ .ConfirmationURL }}` fylder Supabase selv ud med det rigtige link inkl. `access_token` og `type=recovery`.

## 3. (Valgfrit) Skift også de andre skabeloner

Hvis du vil have hele oplevelsen på dansk, kan du tilsvarende oversætte:

- **Confirm signup** (bekræft e-mail efter oprettelse — bruges kun hvis du aktiverer e-mail-bekræftelse i Auth-indstillingerne)
- **Magic Link**
- **Change Email Address**

## Sådan tester du det

1. Åbn appen (lokalt eller på GitHub Pages).
2. Klik på profilen → "Glemt kodeord?".
3. Indtast en e-mail tilknyttet en eksisterende konto.
4. Tjek e-mail-indbakken (og spam-mappen).
5. Klik på linket → du lander på `/nulstil-kodeord`.
6. Indtast et nyt kodeord → du bliver logget ind og sendt til forsiden.

## Fejlsøgning

- **"Linket er ikke gyldigt"** → tjek at redirect URL'en er tilføjet præcis som ovenfor (inkl. `/TipGame/` på GitHub Pages).
- **Ingen e-mail modtaget** → Supabase's gratis tier har en SMTP-rate-limit på 3 e-mails i timen. Til produktion bør du konfigurere din egen SMTP under **Project Settings → Auth → SMTP Settings** (fx SendGrid, Mailgun, eller endda Gmail).
- **"Email rate limit exceeded"** i Supabase loggen → samme grund som ovenfor.
