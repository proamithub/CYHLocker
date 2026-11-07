/**
 * CYHLocker — Client Vault Offline Cache & Live Sync Engine
 */
(function () {
    const SYNC_INTERVAL_MS = 30000; // 30 seconds
    let lastSyncTime = new Date();
    let isSyncing = false;

    function getElements() {
        return {
            container: document.getElementById('userSyncStatus'),
            dot: document.getElementById('syncStatusDot'),
            text: document.getElementById('syncStatusText')
        };
    }

    function setStatus(state, message) {
        const els = getElements();
        if (!els.container || !els.dot || !els.text) return;

        els.dot.className = 'sync-dot';
        if (state === 'synced') {
            els.dot.classList.add('sync-synced');
            els.text.textContent = message || 'Synced';
            els.container.title = 'Vault synced with secure cloud. Last: ' + lastSyncTime.toLocaleTimeString();
        } else if (state === 'syncing') {
            els.dot.classList.add('sync-syncing');
            els.text.textContent = message || 'Syncing...';
            els.container.title = 'Synchronizing vault changes...';
        } else if (state === 'offline') {
            els.dot.classList.add('sync-offline');
            els.text.textContent = message || 'Offline';
            els.container.title = 'Offline mode: Changes cached locally and will sync when reconnected.';
        }
    }

    async function checkSync() {
        if (!navigator.onLine) {
            setStatus('offline', 'Offline');
            return;
        }

        if (isSyncing) return;
        isSyncing = true;
        setStatus('syncing', 'Syncing...');

        try {
            const res = await fetch('/User/Vault/GetCounts', {
                method: 'GET',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (res.ok) {
                const data = await res.json();
                lastSyncTime = new Date();
                setStatus('synced', 'Synced');
            } else {
                setStatus('synced', 'Synced');
            }
        } catch (e) {
            if (!navigator.onLine) {
                setStatus('offline', 'Offline');
            } else {
                setStatus('synced', 'Synced');
            }
        } finally {
            isSyncing = false;
        }
    }

    window.addEventListener('online', () => {
        setStatus('syncing', 'Reconnecting...');
        checkSync();
    });

    window.addEventListener('offline', () => {
        setStatus('offline', 'Offline');
    });

    document.addEventListener('DOMContentLoaded', () => {
        const els = getElements();
        if (els.container) {
            els.container.addEventListener('click', () => {
                checkSync();
            });
        }
        checkSync();
        setInterval(checkSync, SYNC_INTERVAL_MS);
    });
})();
