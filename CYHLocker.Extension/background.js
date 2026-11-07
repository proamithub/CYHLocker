/**
 * CYHLocker — Background Service Worker (Manifest V3)
 * Handles Vault Authentication, Auto-fill Dispatch, and Cloud Sync
 */

const DEFAULT_API_URL = "http://localhost:5201/api";
const DEFAULT_WEB_URL = "http://localhost:5248";

// ── GET PERSISTENT DEVICE ID ───────────────────────────────────────
async function getOrCreateDeviceId() {
  const data = await chrome.storage.local.get(["cyh_device_id"]);
  if (data.cyh_device_id) return data.cyh_device_id;
  const newId = "ext-" + ([1e7]+-1e3+-4e3+-8e3+-1e11).replace(/[018]/g, c =>
    (c ^ crypto.getRandomValues(new Uint8Array(1))[0] & 15 >> c / 4).toString(16)
  );
  await chrome.storage.local.set({ cyh_device_id: newId });
  return newId;
}

// ── GET AUTH HEADERS ───────────────────────────────────────────────
async function getAuthHeaders() {
  const data = await chrome.storage.local.get(["cyh_token"]);
  const headers = { "Content-Type": "application/json" };
  if (data.cyh_token) {
    headers["Authorization"] = `Bearer ${data.cyh_token}`;
  }
  return headers;
}

// ── GET API BASE URL ───────────────────────────────────────────────
async function getApiBaseUrl() {
  const data = await chrome.storage.local.get(["cyh_api_url"]);
  return (data.cyh_api_url || DEFAULT_API_URL).replace(/\/+$/, "");
}

// ── REGISTER DEVICE WITH API ───────────────────────────────────────
async function registerDevice(token) {
  try {
    const apiUrl = await getApiBaseUrl();
    const deviceId = await getOrCreateDeviceId();
    await fetch(`${apiUrl}/sync/register-device`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${token}`
      },
      body: JSON.stringify({
        deviceId: deviceId,
        deviceName: "Chrome Auto-Fill Extension",
        deviceType: "BrowserExtension",
        clientVersion: "1.0.0"
      })
    });
  } catch (err) {
    console.warn("[CYHLocker Background] Device registration warning:", err);
  }
}

// ── PULL VAULT ITEMS FOR LOCAL CACHE ──────────────────────────────
async function syncVaultCache() {
  try {
    const data = await chrome.storage.local.get(["cyh_token", "cyh_last_sync"]);
    if (!data.cyh_token) return;

    const apiUrl = await getApiBaseUrl();
    const deviceId = await getOrCreateDeviceId();

    const res = await fetch(`${apiUrl}/sync/pull`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${data.cyh_token}`
      },
      body: JSON.stringify({
        deviceId: deviceId,
        sinceUtc: data.cyh_last_sync || null
      })
    });

    if (res.ok) {
      const json = await res.json();
      if (json && json.data) {
        const existing = (await chrome.storage.local.get(["cyh_vault_items"])).cyh_vault_items || [];
        const itemMap = new Map();
        existing.forEach(i => itemMap.set(i.id, i));

        (json.data.items || []).forEach(item => {
          if (item.isDeleted) {
            itemMap.delete(item.id);
          } else {
            itemMap.set(item.id, item);
          }
        });

        await chrome.storage.local.set({
          cyh_vault_items: Array.from(itemMap.values()),
          cyh_last_sync: json.data.serverTimestampUtc
        });
      }
    }
  } catch (err) {
    console.warn("[CYHLocker Background] Vault sync warning:", err);
  }
}

// ── EXTENSION ALARMS (PERIODIC SYNC) ───────────────────────────────
chrome.alarms.create("CYH_PERIODIC_SYNC", { periodInMinutes: 5 });
chrome.alarms.onAlarm.addListener(alarm => {
  if (alarm.name === "CYH_PERIODIC_SYNC") {
    syncVaultCache();
  }
});

// ── STRONG PASSWORD GENERATOR (CRYPTO RANDOM) ──────────────────────
function generatePassword(options = {}) {
  const length = Math.max(8, Math.min(64, options.length || 18));
  const useUpper = options.uppercase !== false;
  const useLower = options.lowercase !== false;
  const useNums  = options.numbers !== false;
  const useSyms  = options.symbols !== false;

  let pool = "";
  if (useUpper) pool += "ABCDEFGHJKLMNPQRSTUVWXYZ"; // exclude easily confused chars
  if (useLower) pool += "abcdefghjkmnpqrstuvwxyz";
  if (useNums)  pool += "23456789";
  if (useSyms)  pool += "!@#$%^&*()-_=+[]{}";

  if (!pool) pool = "abcdefghjkmnpqrstuvwxyz23456789";

  const arr = new Uint32Array(length);
  crypto.getRandomValues(arr);
  let result = "";
  for (let i = 0; i < length; i++) {
    result += pool[arr[i] % pool.length];
  }
  return result;
}

// ── MESSAGE ROUTER ─────────────────────────────────────────────────
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  const type = message?.type;

  // 1. Check Auth State
  if (type === "GET_AUTH_STATE") {
    chrome.storage.local.get(["cyh_token", "cyh_user", "cyh_api_url", "cyh_last_sync"], data => {
      sendResponse({
        isAuthenticated: !!data.cyh_token,
        user: data.cyh_user || null,
        apiUrl: data.cyh_api_url || DEFAULT_API_URL,
        lastSync: data.cyh_last_sync || null
      });
    });
    return true;
  }

  // 2. Login to Vault
  if (type === "LOGIN") {
    (async () => {
      try {
        const apiUrl = (message.apiUrl || DEFAULT_API_URL).replace(/\/+$/, "");
        const res = await fetch(`${apiUrl}/auth/login`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            email: message.email,
            password: message.password
          })
        });

        const data = await res.json();
        if (res.ok && data?.data?.token) {
          await chrome.storage.local.set({
            cyh_token: data.data.token,
            cyh_user: data.data.user,
            cyh_api_url: apiUrl
          });

          await registerDevice(data.data.token);
          await syncVaultCache();

          sendResponse({ success: true, user: data.data.user });
        } else {
          sendResponse({ success: false, message: data?.message || "Invalid credentials." });
        }
      } catch (err) {
        sendResponse({ success: false, message: "Network connection failed: " + err.message });
      }
    })();
    return true;
  }

  // 3. Logout
  if (type === "LOGOUT") {
    chrome.storage.local.remove(["cyh_token", "cyh_user", "cyh_vault_items", "cyh_last_sync"], () => {
      sendResponse({ success: true });
    });
    return true;
  }

  // 4. Autofill Matches for Domain
  if (type === "GET_AUTOFILL_MATCHES") {
    (async () => {
      const domain = message.domain;
      if (!domain) {
        sendResponse({ success: false, matches: [] });
        return;
      }

      const data = await chrome.storage.local.get(["cyh_token"]);
      const apiUrl = await getApiBaseUrl();

      // Try calling live API autofill first
      if (data.cyh_token) {
        try {
          const res = await fetch(`${apiUrl}/vault/autofill?domain=${encodeURIComponent(domain)}`, {
            headers: { "Authorization": `Bearer ${data.cyh_token}` }
          });
          if (res.ok) {
            const json = await res.json();
            if (json?.data) {
              sendResponse({ success: true, matches: json.data });
              return;
            }
          }
        } catch {
          // Fall through to offline cache
        }
      }

      // Offline Cache Fallback
      const cacheData = await chrome.storage.local.get(["cyh_vault_items"]);
      const items = cacheData.cyh_vault_items || [];
      const cleanDomain = domain.toLowerCase().replace(/^www\./, "");
      const matches = items.filter(i => {
        const itemDomain = (i.urlDomain || "").toLowerCase().replace(/^www\./, "");
        return itemDomain && (itemDomain === cleanDomain || cleanDomain.includes(itemDomain) || itemDomain.includes(cleanDomain));
      }).map(i => ({
        id: i.id,
        title: i.title,
        username: i.encryptedUsername, // cached or decrypted
        password: i.encryptedPassword,
        urlDomain: i.urlDomain,
        itemIcon: i.itemIcon
      }));

      sendResponse({ success: true, matches });
    })();
    return true;
  }

  // 5. Save New Login
  if (type === "SAVE_NEW_LOGIN") {
    (async () => {
      try {
        const data = await chrome.storage.local.get(["cyh_token"]);
        if (!data.cyh_token) {
          sendResponse({ success: false, message: "User not logged in." });
          return;
        }

        const apiUrl = await getApiBaseUrl();
        const res = await fetch(`${apiUrl}/vault/items`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "Authorization": `Bearer ${data.cyh_token}`
          },
          body: JSON.stringify({
            itemType: "Login",
            title: message.title || message.domain,
            username: message.username,
            password: message.password,
            url: message.url,
            itemIcon: message.icon || null
          })
        });

        if (res.ok) {
          await syncVaultCache();
          sendResponse({ success: true });
        } else {
          const err = await res.json();
          sendResponse({ success: false, message: err?.message || "Failed to save." });
        }
      } catch (err) {
        sendResponse({ success: false, message: err.message });
      }
    })();
    return true;
  }

  // 6. Generate Password
  if (type === "GENERATE_PASSWORD") {
    const pwd = generatePassword(message.options || {});
    sendResponse({ password: pwd });
    return true;
  }

  // 7. Manual Sync Request
  if (type === "SYNC_VAULT") {
    syncVaultCache().then(() => sendResponse({ success: true }));
    return true;
  }
});
