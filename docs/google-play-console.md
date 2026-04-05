# Google Play — Release Setup

This document covers the one-time setup required before the CI/CD pipeline can automatically publish Khepri to Google Play.

---

## 1. Create the app in Play Console

1. Go to [play.google.com/console](https://play.google.com/console) and sign in as **Ion Core Studios**
2. Click **Create app** and fill in:
   - App name: `Khepri`
   - Default language: English
   - App or game: App
   - Free or paid: your choice
3. Complete the mandatory store listing fields (short description, full description, screenshots) — required before publishing, but not before the CI pipeline can upload
4. Go to **Release → Internal testing** and create a release **manually** by uploading an AAB by hand — Google requires at least one manual upload to activate the app before the API can push to it

---

## 2. Create a service account for CI

1. In Play Console go to **Setup → API access**
2. Click **Link to a Google Cloud project** (create a new one if prompted)
3. Click **Create new service account** — this opens Google Cloud Console
4. In Google Cloud Console: create the service account, then under **Keys → Add Key → JSON**, download the key file
5. Back in Play Console: click **Grant access** on the new service account and assign the **Release manager** role (or at minimum the *Releases* permission)

> Keep that JSON key file — you will paste its contents into a GitHub secret in step 4.

---

## 3. Create the Android signing keystore

Run this **once** on your machine to generate the keystore:

```powershell
keytool -genkeypair -v `
  -keystore khepri.keystore `
  -alias khepri `
  -keyalg RSA -keysize 2048 -validity 10000 `
  -storepass YOUR_STORE_PASS `
  -keypass YOUR_KEY_PASS `
  -dname "CN=Alan Keller, O=Ion Core Studios, C=CH"
```

Then base64-encode it so it can be stored as a GitHub secret:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("khepri.keystore")) | Set-Clipboard
```

> **Important:** store the raw `khepri.keystore` file somewhere safe outside the repository. Losing it means you can never push an update to the Play Store.

---

## 4. Add secrets to GitHub

Go to your repo → **Settings → Secrets and variables → Actions → New repository secret** and add the following:

| Secret | Value |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | The base64 string copied above |
| `ANDROID_KEY_ALIAS` | `khepri` (or the alias you chose) |
| `ANDROID_KEY_PASSWORD` | `YOUR_KEY_PASS` |
| `ANDROID_STORE_PASSWORD` | `YOUR_STORE_PASS` |
| `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` | Full contents of the JSON key file from step 2 |

---

## 5. Push to main

Once all secrets are in place, push to `main`. The workflow will:

1. Build and run all tests
2. Produce a signed AAB
3. Upload it to the **internal** track in Play Console

From there you can promote the release to alpha → beta → production inside Play Console whenever you're ready.
