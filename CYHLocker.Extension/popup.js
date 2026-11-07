/**
 * CYHLocker — Extension Popup Controller
 */

document.addEventListener("DOMContentLoaded", async () => {
  // Elements
  const viewLogin          = document.getElementById("viewLogin");
  const viewVault          = document.getElementById("viewVault");
  const vaultStatusBadge   = document.getElementById("vaultStatusBadge");

  // Login form elements
  const loginEmail         = document.getElementById("loginEmail");
  const loginPassword      = document.getElementById("loginPassword");
  const btnUnlockVault     = document.getElementById("btnUnlockVault");
  const loginErrorMsg      = document.getElementById("loginErrorMsg");
  const apiToggleBtn       = document.getElementById("apiToggleBtn");
  const apiBox             = document.getElementById("apiBox");
  const apiUrlInput        = document.getElementById("apiUrlInput");

  // Tabs
  const tabButtons         = document.querySelectorAll(".cyh-tab-btn");
  const tabPanes           = document.querySelectorAll(".cyh-tab-pane");

  // Tab 1: Current site
  const currentSiteDomain  = document.getElementById("currentSiteDomain");
  const currentMatchesList = document.getElementById("currentMatchesList");

  // Tab 2: Search
  const vaultSearchInput   = document.getElementById("vaultSearchInput");
  const allVaultList       = document.getElementById("allVaultList");

  // Tab 3: Generator
  const genPasswordDisplay = document.getElementById("genPasswordDisplay");
  const btnCopyGenPass     = document.getElementById("btnCopyGenPass");
  const btnRegenPass       = document.getElementById("btnRegenPass");
  const lengthSlider       = document.getElementById("lengthSlider");
  const lengthVal          = document.getElementById("lengthVal");
  const chkUpper           = document.getElementById("chkUpper");
  const chkLower           = document.getElementById("chkLower");
  const chkNumbers         = document.getElementById("chkNumbers");
  const chkSymbols         = document.getElementById("chkSymbols");
  const btnFillGenPass     = document.getElementById("btnFillGenPass");

  // Tab 4: Add
  const addTitle           = document.getElementById("addTitle");
  const addUsername        = document.getElementById("addUsername");
  const addPassword        = document.getElementById("addPassword");
  const addUrl             = document.getElementById("addUrl");
  const btnGenForAdd       = document.getElementById("btnGenForAdd");
  const btnSaveNewItem     = document.getElementById("btnSaveNewItem");

  // Footer
  const btnSyncNow         = document.getElementById("btnSyncNow");
  const btnLogout          = document.getElementById("btnLogout");

  let currentTabInfo = { domain: "", url: "" };
  let allCachedVaultItems = [];

  // ── 1. GET ACTIVE TAB DOMAIN ───────────────────────────────────────
  try {
    const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
    if (tabs && tabs[0] && tabs[0].url) {
      const parsed = new URL(tabs[0].url);
      currentTabInfo.domain = parsed.hostname;
      currentTabInfo.url = tabs[0].url;
      currentSiteDomain.textContent = parsed.hostname;
      addUrl.value = tabs[0].url;
      addTitle.value = parsed.hostname.replace(/^www\./, "");
    }
  } catch {
    currentSiteDomain.textContent = "Current Site";
  }

  // ── 2. CHECK AUTH STATE ────────────────────────────────────────────
  chrome.runtime.sendMessage({ type: "GET_AUTH_STATE" }, res => {
    if (res && res.isAuthenticated) {
      showVaultView(res.user);
    } else {
      showLoginView();
    }
  });

  function showLoginView() {
    viewLogin.classList.remove("hidden");
    viewVault.classList.add("hidden");
    vaultStatusBadge.textContent = "Locked";
    vaultStatusBadge.className = "cyh-status-badge locked";
  }

  function showVaultView(user) {
    viewLogin.classList.add("hidden");
    viewVault.classList.remove("hidden");
    vaultStatusBadge.textContent = "Unlocked";
    vaultStatusBadge.className = "cyh-status-badge unlocked";

    loadCurrentSiteMatches();
    loadAllVaultItems();
    generatePassword();
  }

  // ── 3. API TOGGLE ──────────────────────────────────────────────────
  apiToggleBtn.addEventListener("click", () => {
    apiBox.classList.toggle("hidden");
  });

  // ── 4. LOGIN HANDLER ───────────────────────────────────────────────
  btnUnlockVault.addEventListener("click", () => {
    const email = loginEmail.value.trim();
    const password = loginPassword.value;
    const apiUrl = apiUrlInput.value.trim();

    if (!email || !password) {
      showError("Please enter email and master password.");
      return;
    }

    btnUnlockVault.textContent = "Unlocking...";
    btnUnlockVault.disabled = true;
    loginErrorMsg.classList.add("hidden");

    chrome.runtime.sendMessage({ type: "LOGIN", email, password, apiUrl }, res => {
      btnUnlockVault.disabled = false;
      btnUnlockVault.innerHTML = "<span>🔓</span> Unlock My Vault";

      if (res && res.success) {
        showVaultView(res.user);
      } else {
        showError(res?.message || "Invalid email or password.");
      }
    });
  });

  function showError(msg) {
    loginErrorMsg.textContent = msg;
    loginErrorMsg.classList.remove("hidden");
  }

  // ── 5. TABS NAVIGATION ─────────────────────────────────────────────
  tabButtons.forEach(btn => {
    btn.addEventListener("click", () => {
      tabButtons.forEach(b => b.classList.remove("active"));
      tabPanes.forEach(p => p.classList.remove("active"));

      btn.classList.add("active");
      const targetId = btn.getAttribute("data-tab");
      document.getElementById(targetId)?.classList.add("active");
    });
  });

  // ── 6. LOAD MATCHES FOR CURRENT SITE ──────────────────────────────
  function loadCurrentSiteMatches() {
    currentMatchesList.innerHTML = `<div style="padding:16px;text-align:center;color:#94a3b8;">Searching vault...</div>`;

    chrome.runtime.sendMessage({
      type: "GET_AUTOFILL_MATCHES",
      domain: currentTabInfo.domain
    }, res => {
      if (res && res.success && res.matches && res.matches.length > 0) {
        renderMatches(currentMatchesList, res.matches, true);
      } else {
        currentMatchesList.innerHTML = `
          <div style="padding:24px 16px;text-align:center;color:#94a3b8;">
            <div style="font-size:20px;margin-bottom:6px;">🔍</div>
            No saved logins found for <strong>${escapeHtml(currentTabInfo.domain)}</strong>.
            <div style="margin-top:10px;">
              <button class="cyh-sm-btn" id="btnAddForCurrent">➕ Add to Vault</button>
            </div>
          </div>
        `;

        document.getElementById("btnAddForCurrent")?.addEventListener("click", () => {
          document.querySelector('[data-tab="tabAdd"]').click();
        });
      }
    });
  }

  // ── 7. LOAD ALL VAULT ITEMS FOR SEARCH ─────────────────────────────
  function loadAllVaultItems() {
    chrome.storage.local.get(["cyh_vault_items"], data => {
      allCachedVaultItems = data.cyh_vault_items || [];
      renderMatches(allVaultList, allCachedVaultItems, false);
    });
  }

  vaultSearchInput.addEventListener("input", () => {
    const query = vaultSearchInput.value.toLowerCase().trim();
    if (!query) {
      renderMatches(allVaultList, allCachedVaultItems, false);
      return;
    }

    const filtered = allCachedVaultItems.filter(i =>
      (i.title && i.title.toLowerCase().includes(query)) ||
      (i.urlDomain && i.urlDomain.toLowerCase().includes(query)) ||
      (i.itemType && i.itemType.toLowerCase().includes(query))
    );
    renderMatches(allVaultList, filtered, false);
  });

  function renderMatches(container, items, showFillBtn) {
    if (!items || items.length === 0) {
      container.innerHTML = `<div style="padding:16px;text-align:center;color:#94a3b8;">No items found.</div>`;
      return;
    }

    container.innerHTML = "";
    items.forEach(item => {
      const card = document.createElement("div");
      card.className = "cyh-match-card";

      const iconSrc = item.itemIcon || `https://www.google.com/s2/favicons?domain=${encodeURIComponent(item.urlDomain || "example.com")}&sz=64`;

      card.innerHTML = `
        <div class="cyh-match-icon">
          <img src="${iconSrc}" onerror="this.src='https://www.google.com/s2/favicons?domain=example.com&sz=64'"/>
        </div>
        <div class="cyh-match-details">
          <div class="cyh-match-title">${escapeHtml(item.title || "Untitled")}</div>
          <div class="cyh-match-user">${escapeHtml(item.username || item.secondaryInfo || "••••••••")}</div>
        </div>
        <div class="cyh-match-btns">
          ${showFillBtn ? `<button class="cyh-sm-btn btn-fill">Fill</button>` : ""}
          <button class="cyh-sm-btn btn-copy-user" title="Copy Username">👤</button>
          <button class="cyh-sm-btn btn-copy-pass" title="Copy Password">🔑</button>
        </div>
      `;

      // Fill into active tab
      card.querySelector(".btn-fill")?.addEventListener("click", () => {
        fillIntoActiveTab(item.username, item.password);
      });

      // Copy username
      card.querySelector(".btn-copy-user")?.addEventListener("click", e => {
        copyToClipboard(item.username || "", e.target);
      });

      // Copy password
      card.querySelector(".btn-copy-pass")?.addEventListener("click", e => {
        copyToClipboard(item.password || "", e.target);
      });

      container.appendChild(card);
    });
  }

  async function fillIntoActiveTab(username, password) {
    const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tabs || !tabs[0] || !tabs[0].id) return;

    chrome.scripting.executeScript({
      target: { tabId: tabs[0].id },
      func: (u, p) => {
        const pwd = document.querySelector('input[type="password"]');
        const user = document.querySelector('input[type="text"], input[type="email"], input[type="tel"]');

        if (user && u) {
          user.value = u;
          user.dispatchEvent(new Event('input', { bubbles: true }));
          user.dispatchEvent(new Event('change', { bubbles: true }));
        }
        if (pwd && p) {
          pwd.value = p;
          pwd.dispatchEvent(new Event('input', { bubbles: true }));
          pwd.dispatchEvent(new Event('change', { bubbles: true }));
        }
      },
      args: [username, password]
    });
  }

  function copyToClipboard(text, btnElement) {
    if (!text) return;
    navigator.clipboard.writeText(text).then(() => {
      const orig = btnElement.textContent;
      btnElement.textContent = "✓";
      setTimeout(() => { btnElement.textContent = orig; }, 1500);
    });
  }

  // ── 8. PASSWORD GENERATOR ──────────────────────────────────────────
  function generatePassword() {
    chrome.runtime.sendMessage({
      type: "GENERATE_PASSWORD",
      options: {
        length: parseInt(lengthSlider.value, 10),
        uppercase: chkUpper.checked,
        lowercase: chkLower.checked,
        numbers: chkNumbers.checked,
        symbols: chkSymbols.checked
      }
    }, res => {
      if (res && res.password) {
        genPasswordDisplay.value = res.password;
      }
    });
  }

  lengthSlider.addEventListener("input", () => {
    lengthVal.textContent = lengthSlider.value;
    generatePassword();
  });

  [chkUpper, chkLower, chkNumbers, chkSymbols].forEach(chk => {
    chk.addEventListener("change", generatePassword);
  });

  btnRegenPass.addEventListener("click", generatePassword);

  btnCopyGenPass.addEventListener("click", () => {
    copyToClipboard(genPasswordDisplay.value, btnCopyGenPass);
  });

  btnFillGenPass.addEventListener("click", () => {
    fillIntoActiveTab(null, genPasswordDisplay.value);
  });

  btnGenForAdd.addEventListener("click", () => {
    chrome.runtime.sendMessage({
      type: "GENERATE_PASSWORD",
      options: { length: 18 }
    }, res => {
      if (res && res.password) {
        addPassword.value = res.password;
        addPassword.type = "text";
        setTimeout(() => { addPassword.type = "password"; }, 2500);
      }
    });
  });

  // ── 9. QUICK ADD ITEM ──────────────────────────────────────────────
  btnSaveNewItem.addEventListener("click", () => {
    const title = addTitle.value.trim();
    const username = addUsername.value.trim();
    const password = addPassword.value;
    const url = addUrl.value.trim();

    if (!title || !password) {
      alert("Please enter title and password.");
      return;
    }

    btnSaveNewItem.textContent = "Saving...";
    btnSaveNewItem.disabled = true;

    chrome.runtime.sendMessage({
      type: "SAVE_NEW_LOGIN",
      title,
      username,
      password,
      url,
      domain: currentTabInfo.domain
    }, res => {
      btnSaveNewItem.textContent = "💾 Save to Vault";
      btnSaveNewItem.disabled = false;

      if (res && res.success) {
        addTitle.value = "";
        addUsername.value = "";
        addPassword.value = "";
        // Switch to current tab
        document.querySelector('[data-tab="tabCurrent"]').click();
        loadCurrentSiteMatches();
        loadAllVaultItems();
      } else {
        alert("Save failed: " + (res?.message || "Unknown error"));
      }
    });
  });

  // ── 10. SYNC & LOGOUT ──────────────────────────────────────────────
  btnSyncNow.addEventListener("click", () => {
    btnSyncNow.textContent = "⏳";
    chrome.runtime.sendMessage({ type: "SYNC_VAULT" }, () => {
      btnSyncNow.textContent = "🔄";
      loadCurrentSiteMatches();
      loadAllVaultItems();
    });
  });

  btnLogout.addEventListener("click", () => {
    chrome.runtime.sendMessage({ type: "LOGOUT" }, () => {
      showLoginView();
    });
  });

  function escapeHtml(str) {
    return (str || '').replace(/[&<>'"]/g, tag => ({
      '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;'
    }[tag] || tag));
  }
});
