# CYHLocker Browser Auto-Fill Extension (Manifest V3)

Enterprise zero-knowledge password manager extension for Chrome, Microsoft Edge, Brave, and Chromium browsers.

---

## 🌟 Key Features

1. **Smart In-Field Auto-Fill**:
   - Detects login, registration, and credential forms across all websites.
   - Embeds a subtle CYHLocker shield lock badge in username/password inputs.
   - 1-Click interactive floating dropdown with auto-detected brand favicons.
   - Emits synthetic `input` and `change` events compatible with React, Angular, Vue, and traditional HTML forms.

2. **Automatic Brand Logo / Favicon Engine**:
   - Queries Google Favicon high-res proxy and internal brand mappings for 50+ popular services.

3. **Smart Password Save Banner**:
   - Detects when you log into a new site or register an account.
   - Displays a non-intrusive top-right prompt: *"Save password to CYHLocker Vault?"* with 1-click save.

4. **Bi-Directional Cloud & Offline Sync**:
   - Automatically synchronizes with `CYHLocker.API` (`/api/sync/pull` and `/api/sync/push`).
   - Caches encrypted vault items locally in `chrome.storage.local` for instant offline access.

5. **Integrated Cryptographic Password Generator**:
   - Generates strong random passwords using Web Crypto API (`crypto.getRandomValues`).
   - Customizable length (8 to 64 chars), uppercase, lowercase, numbers, and symbols.
   - Real-time strength meter and 1-click "Fill into active form".

6. **Quick Vault Search & Fast Add**:
   - Instantly search logins, payment cards, secure notes, and developer API keys.
   - Add new credentials directly from the browser popup.

---

## 🚀 How to Install in Chrome / Edge (Developer Mode)

1. Open your browser and navigate to:
   - **Google Chrome**: `chrome://extensions/`
   - **Microsoft Edge**: `edge://extensions/`
   - **Brave**: `brave://extensions/`

2. Enable **Developer Mode** toggle in the top-right corner.

3. Click **"Load unpacked"** (or *"Load unpacked extension"*).

4. Select the directory:
   ```
   D:\MyAppAi\CYHLocker\CYHLocker.Extension
   ```

5. The **CYHLocker** extension icon will appear in your browser toolbar! Pin it for quick access.

---

## 🔑 How to Connect & Test

1. Ensure the CYHLocker backend API is running:
   ```bash
   dotnet run --project CYHLocker.API
   ```
   *(API runs at `http://localhost:5201`)*

2. Ensure the CYHLocker Web Portal is running:
   ```bash
   dotnet run --project CYHLocker.Web
   ```
   *(Web Portal runs at `http://localhost:5248`)*

3. Click the CYHLocker extension icon in your browser toolbar.

4. Sign in with your vault credentials:
   - **Email**: `user@cyhlocker.com` (or your registered email)
   - **Master Password**: `User@123`
   - Default API URL: `http://localhost:5201/api`

5. Open any website with a login form (e.g. `github.com/login`, `google.com`, or local test forms).
   - Click the CYHLocker icon inside the password field to view matching credentials and auto-fill!
