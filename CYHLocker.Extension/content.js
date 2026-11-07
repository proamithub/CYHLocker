/**
 * CYHLocker — Content Script (Auto-Fill & In-Field Form Assistant)
 */

(function () {
  const currentDomain = window.location.hostname;
  let cachedMatches = [];
  let activeDropdown = null;
  let activeTargetInput = null;

  const SVG_LOCK_ICON = `
    <svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M12 2C9.24 2 7 4.24 7 7V10H6C4.9 10 4 10.9 4 12V20C4 21.1 4.9 22 6 22H18C19.1 22 20 21.1 20 20V12C20 10.9 19.1 10 18 10H17V7C17 4.24 14.76 2 12 2ZM9 7C9 5.34 10.34 4 12 4C13.66 4 15 5.34 15 7V10H9V7ZM12 17C10.9 17 10 16.1 10 15C10 13.9 10.9 13 12 13C13.1 13 14 13.9 14 15C14 16.1 13.1 17 12 17Z" fill="#06b6d4"/>
    </svg>
  `;

  // ── 1. QUERY AUTOFILL MATCHES FOR DOMAIN ──────────────────────────
  function fetchMatches() {
    chrome.runtime.sendMessage(
      { type: "GET_AUTOFILL_MATCHES", domain: currentDomain },
      res => {
        if (res && res.success && Array.isArray(res.matches)) {
          cachedMatches = res.matches;
          if (cachedMatches.length > 0) {
            setupFormInteractions();
          }
        }
      }
    );
  }

  // ── 2. DETECT CREDENTIAL INPUTS ──────────────────────────────────
  function getCredentialInputs() {
    const passwordInputs = Array.from(document.querySelectorAll('input[type="password"]')).filter(isVisible);
    const textInputs = Array.from(document.querySelectorAll('input[type="text"], input[type="email"], input[type="tel"]')).filter(isVisible);

    const usernameInputs = textInputs.filter(input => {
      const name = (input.name || input.id || input.placeholder || input.getAttribute("autocomplete") || "").toLowerCase();
      return /user|email|login|account|phone|ident/i.test(name);
    });

    return { passwordInputs, usernameInputs };
  }

  function isVisible(el) {
    if (!el) return false;
    const style = window.getComputedStyle(el);
    return style.display !== 'none' && style.visibility !== 'hidden' && style.opacity !== '0' && el.offsetParent !== null;
  }

  // ── 3. ATTACH BADGE TO INPUTS ────────────────────────────────────
  function setupFormInteractions() {
    const { passwordInputs, usernameInputs } = getCredentialInputs();
    const allInputs = [...new Set([...passwordInputs, ...usernameInputs])];

    allInputs.forEach(input => {
      if (input.dataset.cyhBound) return;
      input.dataset.cyhBound = "true";

      // Badge Icon
      const badge = document.createElement("div");
      badge.className = "cyh-input-badge";
      badge.innerHTML = SVG_LOCK_ICON;
      badge.title = "CYHLocker — Fill credentials";

      // Position badge inside parent or relative container
      positionBadge(badge, input);

      badge.addEventListener("mousedown", e => {
        e.preventDefault();
        e.stopPropagation();
        toggleDropdown(input);
      });

      input.addEventListener("focus", () => {
        if (cachedMatches.length > 0 && !activeDropdown) {
          showDropdown(input);
        }
      });
    });
  }

  function positionBadge(badge, input) {
    const rect = input.getBoundingClientRect();
    const parent = input.parentElement;

    if (parent && window.getComputedStyle(parent).position === 'static') {
      parent.style.position = 'relative';
    }

    if (parent) {
      parent.appendChild(badge);
    } else {
      document.body.appendChild(badge);
    }
  }

  // ── 4. RENDER AUTO-FILL DROPDOWN ─────────────────────────────────
  function showDropdown(targetInput) {
    closeDropdown();
    activeTargetInput = targetInput;

    const rect = targetInput.getBoundingClientRect();
    const dropdown = document.createElement("div");
    dropdown.className = "cyh-autofill-dropdown";
    dropdown.style.top = `${rect.bottom + window.scrollY + 6}px`;
    dropdown.style.left = `${Math.max(10, Math.min(rect.left + window.scrollX, window.innerWidth - 330))}px`;

    let html = `
      <div class="cyh-dropdown-header">
        <div class="cyh-dropdown-header-brand">
          <span>🔒</span> CYHLocker Vault
        </div>
        <span class="cyh-dropdown-close" id="cyhCloseDropdown">&times;</span>
      </div>
      <div class="cyh-items-list">
    `;

    if (cachedMatches.length === 0) {
      html += `<div class="cyh-empty-msg">No stored logins for ${currentDomain}</div>`;
    } else {
      cachedMatches.forEach((m, idx) => {
        const iconSrc = m.itemIcon || `https://www.google.com/s2/favicons?domain=${encodeURIComponent(m.urlDomain || currentDomain)}&sz=64`;
        html += `
          <div class="cyh-item-row" data-index="${idx}">
            <div class="cyh-item-icon">
              <img src="${iconSrc}" onerror="this.src='https://www.google.com/s2/favicons?domain=example.com&sz=64'"/>
            </div>
            <div class="cyh-item-info">
              <div class="cyh-item-title">${escapeHtml(m.title || currentDomain)}</div>
              <div class="cyh-item-username">${escapeHtml(m.username || "••••••••")}</div>
            </div>
            <div class="cyh-item-action">Fill</div>
          </div>
        `;
      });
    }

    html += `
      </div>
      <div class="cyh-dropdown-footer">
        <button class="cyh-gen-btn" id="cyhGenPassBtn">⚡ Generate Strong Password</button>
      </div>
    `;

    dropdown.innerHTML = html;
    document.body.appendChild(dropdown);
    activeDropdown = dropdown;

    // Attach row events
    dropdown.querySelectorAll(".cyh-item-row").forEach(row => {
      row.addEventListener("mousedown", e => {
        e.preventDefault();
        const idx = parseInt(row.getAttribute("data-index"), 10);
        const match = cachedMatches[idx];
        if (match) {
          fillCredentials(match, targetInput);
        }
        closeDropdown();
      });
    });

    dropdown.querySelector("#cyhCloseDropdown")?.addEventListener("click", closeDropdown);

    dropdown.querySelector("#cyhGenPassBtn")?.addEventListener("mousedown", e => {
      e.preventDefault();
      chrome.runtime.sendMessage({ type: "GENERATE_PASSWORD", options: { length: 18 } }, res => {
        if (res && res.password) {
          fillGeneratedPassword(res.password, targetInput);
          closeDropdown();
        }
      });
    });
  }

  function toggleDropdown(targetInput) {
    if (activeDropdown) {
      closeDropdown();
    } else {
      showDropdown(targetInput);
    }
  }

  function closeDropdown() {
    if (activeDropdown && activeDropdown.parentNode) {
      activeDropdown.parentNode.removeChild(activeDropdown);
    }
    activeDropdown = null;
    activeTargetInput = null;
  }

  // ── 5. FILL CREDENTIALS INTO FORM ────────────────────────────────
  function fillCredentials(match, targetInput) {
    const form = targetInput.form || targetInput.closest("form") || document;
    const passwordField = form.querySelector('input[type="password"]');
    const usernameField = form.querySelector('input[type="text"], input[type="email"], input[type="tel"]') || targetInput;

    if (usernameField && match.username) {
      setInputValue(usernameField, match.username);
    }

    if (passwordField && match.password) {
      setInputValue(passwordField, match.password);
    }
  }

  function fillGeneratedPassword(pwd, targetInput) {
    if (targetInput && targetInput.type === "password") {
      setInputValue(targetInput, pwd);
    } else {
      const pwdInput = document.querySelector('input[type="password"]');
      if (pwdInput) setInputValue(pwdInput, pwd);
    }
    navigator.clipboard.writeText(pwd).catch(() => {});
  }

  function setInputValue(input, val) {
    input.value = val;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.dispatchEvent(new Event('change', { bubbles: true }));
    input.dispatchEvent(new Event('blur', { bubbles: true }));
  }

  function escapeHtml(str) {
    return (str || '').replace(/[&<>'"]/g, tag => ({
      '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;'
    }[tag] || tag));
  }

  // ── 6. CLOSE ON OUTSIDE CLICK ────────────────────────────────────
  document.addEventListener("mousedown", e => {
    if (activeDropdown && !activeDropdown.contains(e.target) && !e.target.closest(".cyh-input-badge")) {
      closeDropdown();
    }
  });

  // ── 7. DETECT FORM SUBMIT (SMART SAVE PROMPT) ────────────────────
  document.addEventListener("submit", e => {
    const form = e.target;
    if (!form) return;

    const pwdField = form.querySelector('input[type="password"]');
    const userField = form.querySelector('input[type="text"], input[type="email"], input[type="tel"]');

    if (pwdField && pwdField.value && pwdField.value.length >= 4) {
      const enteredUser = userField ? userField.value : "";
      const enteredPass = pwdField.value;

      // Check if already in cachedMatches
      const isKnown = cachedMatches.some(m => m.username === enteredUser && m.password === enteredPass);
      if (!isKnown) {
        showSavePrompt(enteredUser, enteredPass);
      }
    }
  });

  function showSavePrompt(username, password) {
    if (document.getElementById("cyhSaveBanner")) return;

    const banner = document.createElement("div");
    banner.id = "cyhSaveBanner";
    banner.className = "cyh-save-banner";
    banner.innerHTML = `
      <div class="cyh-banner-header">
        <span>🔒</span> Save password to CYHLocker?
      </div>
      <div class="cyh-banner-body">
        Would you like to securely save your password for <strong>${escapeHtml(currentDomain)}</strong> (${escapeHtml(username || "User")})?
      </div>
      <div class="cyh-banner-actions">
        <button class="cyh-btn-dismiss" id="cyhDismissBanner">Not Now</button>
        <button class="cyh-btn-save" id="cyhSaveCredentialsBtn">Save Password</button>
      </div>
    `;

    document.body.appendChild(banner);

    document.getElementById("cyhDismissBanner")?.addEventListener("click", () => {
      banner.remove();
    });

    document.getElementById("cyhSaveCredentialsBtn")?.addEventListener("click", () => {
      chrome.runtime.sendMessage({
        type: "SAVE_NEW_LOGIN",
        title: currentDomain,
        domain: currentDomain,
        username: username,
        password: password,
        url: window.location.href
      }, res => {
        banner.remove();
        if (res && res.success) {
          fetchMatches();
        }
      });
    });

    // Auto-dismiss after 15s
    setTimeout(() => {
      if (banner.parentNode) banner.remove();
    }, 15000);
  }

  // ── INIT ─────────────────────────────────────────────────────────
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", fetchMatches);
  } else {
    fetchMatches();
  }
})();
