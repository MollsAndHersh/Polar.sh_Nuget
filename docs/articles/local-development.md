# Local Development Setup

This guide walks through everything needed to run PolarTestApp locally with a real Polar sandbox token and live webhook delivery from Polar's servers to your machine.

---

## 1. Get a Polar sandbox access token

1. Sign in at [polar.sh](https://polar.sh) and open your organization.
2. Go to **Settings → Developers → Access Tokens**.
3. Create a new token — sandbox tokens begin with `polar_oat_`.
4. Copy the token value immediately (it is only shown once).

---

## 2. Store secrets locally with user-secrets

Never put real tokens in `appsettings.json` — that file is committed to source control. Use .NET user-secrets instead. They are stored in `~/.microsoft/usersecrets/` on your machine and are never committed.

**Run these commands from inside `testapp/PolarTestApp/`:**

```bash
# Set your Polar sandbox access token
dotnet user-secrets set "PolarSharp:AccessToken" "polar_oat_***"

# Set a placeholder webhook secret (you will replace this in Step 5)
dotnet user-secrets set "PolarSharp:Webhooks:Secret" "whsec_placeholder"
```

> If you are running from the repository root instead, append `--project testapp/PolarTestApp` to each command. Omit it when your working directory is already `testapp/PolarTestApp/`.

Verify the secrets are registered:

```bash
dotnet user-secrets list
```

Expected output:
```
PolarSharp:AccessToken = polar_oat_***
PolarSharp:Webhooks:Secret = whsec_placeholder
```

---

## 3. Verify the app starts

```bash
dotnet run
```

The startup banner should appear confirming Test/Sandbox mode. If `OptionsValidationException` is thrown, the token is missing or empty — re-check Step 2.

---

## 4. Set up ngrok for local webhook delivery

Polar's servers need a public HTTPS URL to POST webhook events to. ngrok creates a secure tunnel from a public URL to your localhost.

### Install ngrok

```bash
brew install ngrok/ngrok/ngrok
```

### Create a free ngrok account and configure your authtoken

ngrok requires a verified account even for the free tier.

1. Sign up at [dashboard.ngrok.com/signup](https://dashboard.ngrok.com/signup)
2. Copy your authtoken from [dashboard.ngrok.com/get-started/your-authtoken](https://dashboard.ngrok.com/get-started/your-authtoken)
3. Configure it once on your machine:

```bash
ngrok config add-authtoken YOUR_AUTHTOKEN_HERE
```

### Start the tunnel

```bash
ngrok http 5115
```

ngrok prints a forwarding URL like:

```
Forwarding  https://a1b2-203-0-113-42.ngrok-free.app -> http://localhost:5115
```

Keep this terminal window open. The URL changes each time you restart ngrok on the free tier — a paid ngrok plan gives a static subdomain if you want to avoid updating Polar's dashboard each session.

---

## 5. Register your webhook endpoint in Polar

1. In the Polar dashboard go to **Settings → Webhooks → Add endpoint**.
2. Set the URL to your ngrok forwarding URL plus the path:
   ```
   https://YOUR-NGROK-URL.ngrok-free.app/hooks/polar
   ```
3. Select the events you want to test (or **Send all events**).
4. Click **Create**. Polar shows a webhook secret beginning with `whsec_` — copy it immediately.

### Update your user-secret with the real webhook secret

```bash
dotnet user-secrets set "PolarSharp:Webhooks:Secret" "whsec_***"
```

Restart the app so it picks up the updated secret:

```bash
dotnet run
```

---

## 6. Send a test event

In the Polar dashboard, open the webhook endpoint you just created and click **Send test event**. You should see:

- The ngrok terminal logging an incoming `POST /hooks/polar`
- The app console logging the verified event at `[INF]` level
- Any handler you have registered for that event type executing

Alternatively, use PolarTestApp's built-in simulator (no Polar dashboard needed):

```
POST /test/webhook/simulate/order.created
```

This constructs a correctly HMAC-signed payload and posts it to the local webhook endpoint, proving the full verification pipeline without needing an external sender.

---

## Quick-reference checklist

| Step | What | Command |
|---|---|---|
| 1 | Get Polar sandbox token | Polar dashboard → Settings → Developers |
| 2 | Store access token | `dotnet user-secrets set "PolarSharp:AccessToken" "polar_oat_***"` |
| 2 | Store webhook secret (placeholder) | `dotnet user-secrets set "PolarSharp:Webhooks:Secret" "whsec_placeholder"` |
| 3 | Verify app starts | `dotnet run` |
| 4 | Install ngrok | `brew install ngrok/ngrok/ngrok` |
| 4 | Configure ngrok authtoken (one-time) | `ngrok config add-authtoken YOUR_AUTHTOKEN` |
| 4 | Start tunnel | `ngrok http 5115` |
| 5 | Register endpoint in Polar | `https://YOUR-NGROK-URL.ngrok-free.app/hooks/polar` |
| 5 | Store real webhook secret | `dotnet user-secrets set "PolarSharp:Webhooks:Secret" "whsec_***"` |
| 6 | Send test event | Polar dashboard → test event, or `POST /test/webhook/simulate/order.created` |
