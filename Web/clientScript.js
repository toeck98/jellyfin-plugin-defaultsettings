// DefaultSettings Client-Skript zur Einbettung des Einstellungs-Buttons auf der Serien-Detailseite
(function () {
    'use strict';

    console.log('[DefaultSettings] Client-Skript erfolgreich initialisiert.');

    function getActiveItemId() {
        // 1. Aus dem Hash (?id=... oder &id=...)
        const hash = window.location.hash || '';
        if (hash.includes('?')) {
            const params = new URLSearchParams(hash.substring(hash.indexOf('?') + 1));
            const id = params.get('id') || params.get('itemId');
            if (id) return id;
        }

        // 2. Aus der Standard-URL Query
        const searchParams = new URLSearchParams(window.location.search);
        const searchId = searchParams.get('id') || searchParams.get('itemId');
        if (searchId) return searchId;

        // 3. Aus dem data-id Attribut der aktiven Seite im DOM
        const activeContainer = document.querySelector('div#itemDetailPage:not(.hide), .detailPageWrapperContainer:not(.hide), .page:not(.hide)[data-role="page"]');
        if (activeContainer) {
            const dataId = activeContainer.getAttribute('data-id') || activeContainer.getAttribute('data-itemid');
            if (dataId) return dataId;
        }

        return null;
    }

    function isDetailPage() {
        const hash = (window.location.hash || '').toLowerCase();
        const href = (window.location.href || '').toLowerCase();
        return hash.includes('details') || hash.includes('item') || href.includes('id=');
    }

    function checkAndInjectButton() {
        if (!isDetailPage()) {
            return;
        }

        const itemId = getActiveItemId();
        if (!itemId) {
            return;
        }

        if (typeof ApiClient === 'undefined' || !ApiClient.getCurrentUserId) {
            return;
        }

        const currentUserId = ApiClient.getCurrentUserId();
        if (!currentUserId) {
            return;
        }

        // Suche den aktiven (sichtbaren) Container der Detailseite
        const activePage = document.querySelector('div#itemDetailPage:not(.hide), .detailPageWrapperContainer:not(.hide), .pageContainer:not(.hide), .page:not(.hide)') || document;

        // Falls auf der aktiven Seite bereits ein Button für diese Item-ID existiert
        if (activePage.querySelector('#btnDefaultSettings[data-itemid="' + itemId + '"]')) {
            return;
        }

        // Container für Buttons auf der aktiven Seite suchen
        const btnContainer = activePage.querySelector('.mainDetailButtons') 
            || activePage.querySelector('.detailButtons')
            || activePage.querySelector('.detailRibbon')
            || activePage.querySelector('.detailPagePrimaryContainer')
            || document.querySelector('.mainDetailButtons:not(.hide)');

        if (!btnContainer) {
            return;
        }

        // Prüfen, ob es sich wirklich um eine Serie handelt
        ApiClient.getItem(currentUserId, itemId).then(function (item) {
            if (!item || item.Type !== 'Series') {
                return;
            }

            // Altes Element auf dieser Seite entfernen
            const oldBtn = activePage.querySelector('#btnDefaultSettings');
            if (oldBtn) oldBtn.remove();

            const btn = document.createElement('button');
            btn.id = 'btnDefaultSettings';
            btn.setAttribute('data-itemid', itemId);
            btn.setAttribute('is', 'emby-button');
            btn.type = 'button';
            btn.className = 'emby-button detailButton raised button-flat';
            btn.style.margin = '0.5em 0.5em 0.5em 0';
            btn.style.display = 'inline-flex';
            btn.style.alignItems = 'center';
            btn.style.cursor = 'pointer';
            btn.style.verticalAlign = 'middle';
            btn.innerHTML = '<i class="md-icon" style="margin-right: 0.4em;">tune</i><span>Standard-Spuren / Version</span>';

            btn.onclick = function (e) {
                e.preventDefault();
                e.stopPropagation();
                openSettingsModal(item);
            };

            btnContainer.appendChild(btn);
            console.log('[DefaultSettings] Einstellungs-Button für Serie "' + item.Name + '" eingefügt.');
        }).catch(function (err) {
            console.debug('[DefaultSettings] Item-Prüfung:', err);
        });
    }

    function openSettingsModal(series) {
        const currentUserId = ApiClient.getCurrentUserId();
        const modalId = 'defaultSettingsSeriesModal';

        let existingModal = document.getElementById(modalId);
        if (existingModal) existingModal.remove();

        const overlay = document.createElement('div');
        overlay.id = modalId;
        overlay.style.position = 'fixed';
        overlay.style.top = '0';
        overlay.style.left = '0';
        overlay.style.width = '100vw';
        overlay.style.height = '100vh';
        overlay.style.background = 'rgba(0, 0, 0, 0.75)';
        overlay.style.zIndex = '99999';
        overlay.style.display = 'flex';
        overlay.style.justifyContent = 'center';
        overlay.style.alignItems = 'center';
        overlay.style.backdropFilter = 'blur(4px)';

        const modalBox = document.createElement('div');
        modalBox.style.background = '#1c1c1c';
        modalBox.style.color = '#fff';
        modalBox.style.borderRadius = '8px';
        modalBox.style.padding = '1.5em';
        modalBox.style.width = '90%';
        modalBox.style.maxWidth = '500px';
        modalBox.style.boxShadow = '0 8px 32px rgba(0,0,0,0.5)';
        modalBox.style.maxHeight = '90vh';
        modalBox.style.overflowY = 'auto';

        modalBox.innerHTML = `
            <h2 style="margin-top: 0; font-size: 1.4em; border-bottom: 1px solid rgba(255,255,255,0.1); padding-bottom: 0.5em;">
                ⚙️ Standardeinstellungen für:<br>
                <span style="color: #00a4dc; font-size: 0.9em;">${escapeHtml(series.Name)}</span>
            </h2>
            <div id="dsModalLoading" style="padding: 2em; text-align: center; color: #aaa;">Lade Optionen...</div>
            <form id="dsModalForm" style="display: none;">
                <div style="margin-bottom: 1em;">
                    <label style="display: block; margin-bottom: 0.3em; font-weight: bold;">Bevorzugte Version / Fassung:</label>
                    <select id="dsModalVersionSelect" class="emby-select" style="width: 100%; padding: 0.5em; border-radius: 4px; background: #2b2b2b; color: #fff; border: 1px solid #444;">
                        <option value="">-- Jellyfin Standard beibehalten --</option>
                    </select>
                    <input type="text" id="dsModalVersionCustom" placeholder="Oder Freitext (z. B. Bunt, Schwarzweiss)..." style="width: 100%; margin-top: 0.4em; padding: 0.5em; border-radius: 4px; background: #2b2b2b; color: #fff; border: 1px solid #444; box-sizing: border-box;" />
                    <div style="font-size: 0.8em; color: #aaa; margin-top: 0.2em;">Fehlt die Version bei einer Folge, greift automatisch Jellyfins Standard.</div>
                </div>

                <div style="margin-bottom: 1em;">
                    <label style="display: block; margin-bottom: 0.3em; font-weight: bold;">Audiosprache für diese Serie:</label>
                    <select id="dsModalAudioSelect" class="emby-select" style="width: 100%; padding: 0.5em; border-radius: 4px; background: #2b2b2b; color: #fff; border: 1px solid #444;">
                        <option value="">-- Jellyfin Kontoeinstellung verwenden --</option>
                    </select>
                </div>

                <div style="margin-bottom: 1em;">
                    <label style="display: block; margin-bottom: 0.3em; font-weight: bold;">Untertitelsprache für diese Serie:</label>
                    <select id="dsModalSubSelect" class="emby-select" style="width: 100%; padding: 0.5em; border-radius: 4px; background: #2b2b2b; color: #fff; border: 1px solid #444;">
                        <option value="">-- Jellyfin Kontoeinstellung verwenden --</option>
                    </select>
                </div>

                <div style="margin-bottom: 1.5em;">
                    <label style="display: block; margin-bottom: 0.3em; font-weight: bold;">Untertitel-Modus:</label>
                    <select id="dsModalSubModeSelect" class="emby-select" style="width: 100%; padding: 0.5em; border-radius: 4px; background: #2b2b2b; color: #fff; border: 1px solid #444;">
                        <option value="">-- Jellyfin Kontoeinstellung verwenden --</option>
                        <option value="0">Nur Forced (erzwungene fremde Dialoge)</option>
                        <option value="1">Immer anzeigen</option>
                        <option value="2">Smart (nur wenn Ton fremdsprachig)</option>
                        <option value="3">Deaktiviert (keine Untertitel)</option>
                    </select>
                </div>

                <div style="display: flex; justify-content: space-between; gap: 0.5em; flex-wrap: wrap;">
                    <button type="submit" class="raised button-submit emby-button" style="background: #00a4dc; color: #fff; padding: 0.6em 1.2em; border: none; border-radius: 4px; cursor: pointer; font-weight: bold;">
                        <span>Speichern</span>
                    </button>
                    <button type="button" id="dsModalReset" class="raised button-flat emby-button" style="background: #555; color: #ff6666; padding: 0.6em 1.2em; border: none; border-radius: 4px; cursor: pointer; display: none;">
                        <span>Zurücksetzen</span>
                    </button>
                    <button type="button" id="dsModalCancel" class="raised button-flat emby-button" style="background: #333; color: #fff; padding: 0.6em 1.2em; border: none; border-radius: 4px; cursor: pointer;">
                        <span>Abbrechen</span>
                    </button>
                </div>
            </form>
        `;

        overlay.appendChild(modalBox);
        document.body.appendChild(overlay);

        document.getElementById('dsModalCancel').onclick = function () {
            overlay.remove();
        };

        // Daten laden (Metadaten & bestehender Override)
        Promise.all([
            ApiClient.getJSON(ApiClient.getUrl(`Plugins/DefaultSettings/Series/${series.Id}/Metadata?userId=${currentUserId}`)),
            ApiClient.getJSON(ApiClient.getUrl(`Plugins/DefaultSettings/Series/${series.Id}?userId=${currentUserId}`))
        ]).then(function (results) {
            const metadata = results[0] || {};
            const currentOverride = results[1] || null;

            document.getElementById('dsModalLoading').style.display = 'none';
            document.getElementById('dsModalForm').style.display = 'block';

            // Versions-Optionen befüllen
            const verSelect = document.getElementById('dsModalVersionSelect');
            (metadata.AvailableVersions || []).forEach(function (v) {
                const opt = document.createElement('option');
                opt.value = v;
                opt.textContent = v;
                verSelect.appendChild(opt);
            });

            // Audio-Sprachen befüllen
            const audioSelect = document.getElementById('dsModalAudioSelect');
            const standardAudios = ['deu', 'eng', 'jpn', 'fra', 'spa', 'ita'];
            const allAudios = Array.from(new Set([...(metadata.AvailableAudioLanguages || []), ...standardAudios]));
            allAudios.forEach(function (lang) {
                const opt = document.createElement('option');
                opt.value = lang;
                opt.textContent = getLangName(lang);
                audioSelect.appendChild(opt);
            });

            // Untertitel-Sprachen befüllen
            const subSelect = document.getElementById('dsModalSubSelect');
            const standardSubs = ['deu', 'eng', 'jpn'];
            const allSubs = Array.from(new Set([...(metadata.AvailableSubtitleLanguages || []), ...standardSubs]));
            allSubs.forEach(function (lang) {
                const opt = document.createElement('option');
                opt.value = lang;
                opt.textContent = getLangName(lang);
                subSelect.appendChild(opt);
            });

            // Vorhandene Werte setzen
            if (currentOverride) {
                if (currentOverride.PreferredVersion) {
                    if (Array.from(verSelect.options).some(o => o.value.toLowerCase() === currentOverride.PreferredVersion.toLowerCase())) {
                        verSelect.value = currentOverride.PreferredVersion;
                    } else {
                        document.getElementById('dsModalVersionCustom').value = currentOverride.PreferredVersion;
                    }
                }
                if (currentOverride.AudioLanguage) {
                    audioSelect.value = currentOverride.AudioLanguage;
                }
                if (currentOverride.SubtitleLanguage) {
                    subSelect.value = currentOverride.SubtitleLanguage;
                }
                if (currentOverride.SubtitleMode !== null && currentOverride.SubtitleMode !== undefined) {
                    document.getElementById('dsModalSubModeSelect').value = currentOverride.SubtitleMode;
                }

                const btnReset = document.getElementById('dsModalReset');
                btnReset.style.display = 'inline-block';
                btnReset.onclick = function () {
                    ApiClient.ajax({
                        type: 'DELETE',
                        url: ApiClient.getUrl(`Plugins/DefaultSettings/Series/${series.Id}?userId=${currentUserId}`)
                    }).then(function () {
                        if (typeof Dashboard !== 'undefined' && Dashboard.alert) {
                            Dashboard.alert('Ausnahme für diese Serie wurde zurückgesetzt.');
                        }
                        overlay.remove();
                    });
                };
            }

            // Speichern
            document.getElementById('dsModalForm').onsubmit = function (e) {
                e.preventDefault();
                let version = verSelect.value || document.getElementById('dsModalVersionCustom').value.trim();
                let audio = audioSelect.value;
                let sub = subSelect.value;
                let modeVal = document.getElementById('dsModalSubModeSelect').value;
                let subMode = modeVal === '' ? null : parseInt(modeVal, 10);

                const payload = {
                    AudioLanguage: audio || null,
                    SubtitleLanguage: sub || null,
                    SubtitleMode: subMode,
                    PreferredVersion: version || null
                };

                ApiClient.ajax({
                    type: 'POST',
                    url: ApiClient.getUrl(`Plugins/DefaultSettings/Series/${series.Id}?userId=${currentUserId}`),
                    data: JSON.stringify(payload),
                    contentType: 'application/json'
                }).then(function () {
                    if (typeof Dashboard !== 'undefined' && Dashboard.alert) {
                        Dashboard.alert('Standardeinstellungen für "' + series.Name + '" gespeichert!');
                    }
                    overlay.remove();
                }).catch(function (err) {
                    alert('Fehler beim Speichern: ' + err);
                });
            };

        }).catch(function (err) {
            document.getElementById('dsModalLoading').textContent = 'Fehler beim Laden: ' + err;
        });
    }

    function getLangName(code) {
        const map = {
            'deu': 'Deutsch (deu)',
            'ger': 'Deutsch (ger)',
            'eng': 'Englisch (eng)',
            'jpn': 'Japanisch (jpn)',
            'fra': 'Französisch (fra)',
            'spa': 'Spanisch (spa)',
            'ita': 'Italienisch (ita)',
            'kor': 'Koreanisch (kor)',
            'zho': 'Chinesisch (zho)',
            'rus': 'Russisch (rus)'
        };
        return map[code.toLowerCase()] || code;
    }

    function escapeHtml(text) {
        if (!text) return '';
        return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    // Beobachter & Event-Listener für Navigation
    window.addEventListener('hashchange', checkAndInjectButton);
    document.addEventListener('viewshow', checkAndInjectButton);
    document.addEventListener('pageshow', checkAndInjectButton);
    setInterval(checkAndInjectButton, 1000);
    checkAndInjectButton();
})();
