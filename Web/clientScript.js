// DefaultSettings Client-Skript zur Einbettung der Einstellungsoption in Jellyfins Kontextmenü ("..." Menü)
(function () {
    'use strict';

    if (window.__defaultSettingsInitialized) {
        return;
    }
    window.__defaultSettingsInitialized = true;

    const pluginId = "2ff22cec-2c92-4428-a1b3-d1ff33748bf2";
    console.log('[DefaultSettings] Client-Skript für Kontextmenü erfolgreich initialisiert.');

    const itemCache = new Map();
    let lastClickedItemId = null;

    function normalizeId(id) {
        if (!id) return '';
        return String(id).replace(/-/g, '').toLowerCase();
    }

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

    // Erfasse das zuletzt per Klick oder Kontextmenü ausgewählte Element
    document.addEventListener('click', function (e) {
        const trigger = e.target.closest('[data-id], [data-itemid], .card, .btnMoreCommands, .cardOverlayButton, button');
        if (trigger) {
            const id = trigger.getAttribute('data-id')
                    || trigger.getAttribute('data-itemid')
                    || trigger.closest('[data-id]')?.getAttribute('data-id')
                    || trigger.closest('[data-itemid]')?.getAttribute('data-itemid');

            if (id) {
                lastClickedItemId = id;
            }
        }
    }, true);

    document.addEventListener('contextmenu', function (e) {
        const trigger = e.target.closest('[data-id], [data-itemid], .card, .listItem');
        if (trigger) {
            const id = trigger.getAttribute('data-id')
                    || trigger.getAttribute('data-itemid')
                    || trigger.closest('[data-id]')?.getAttribute('data-id');

            if (id) {
                lastClickedItemId = id;
            }
        }
    }, true);

    // MutationObserver zur Erkennung von Jellyfins Popup-Aktionsmenü (.actionSheetScroller / .actionSheet)
    const observer = new MutationObserver(function (mutations) {
        for (const m of mutations) {
            for (const node of m.addedNodes) {
                if (node.nodeType === Node.ELEMENT_NODE) {
                    if (node.classList && (node.classList.contains('actionSheet') || node.classList.contains('actionSheetScroller'))) {
                        injectContextMenuItem(node);
                    } else if (node.querySelector) {
                        const scroller = node.querySelector('.actionSheetScroller');
                        if (scroller) {
                            injectContextMenuItem(scroller);
                        }
                    }
                }
            }
        }
    });

    observer.observe(document.body, { childList: true, subtree: true });

    function injectContextMenuItem(container) {
        const scroller = container.classList.contains('actionSheetScroller')
            ? container
            : container.querySelector('.actionSheetScroller');

        if (!scroller || scroller.querySelector('#dsContextMenuItem')) {
            return;
        }

        if (typeof ApiClient === 'undefined' || !ApiClient.getCurrentUserId) {
            return;
        }

        const currentUserId = ApiClient.getCurrentUserId();
        if (!currentUserId) {
            return;
        }

        // Ermittle die betroffene Item-ID: Entweder zuletzt angeklickt oder die aktive Detailseite
        const targetId = lastClickedItemId || getActiveItemId();
        if (!targetId) {
            return;
        }

        resolveSeriesItem(currentUserId, targetId).then(function (series) {
            if (!series) {
                return;
            }

            if (scroller.querySelector('#dsContextMenuItem')) {
                return;
            }

            const menuItem = document.createElement('button');
            menuItem.id = 'dsContextMenuItem';
            menuItem.type = 'button';
            menuItem.className = 'actionSheetMenuItem emby-button';
            menuItem.style.display = 'flex';
            menuItem.style.alignItems = 'center';
            menuItem.style.width = '100%';
            menuItem.style.textAlign = 'left';
            menuItem.style.cursor = 'pointer';

            menuItem.innerHTML = `
                <span class="material-icons tune actionSheetMenuItemIcon" aria-hidden="true" style="margin-right: 0.8em; font-size: 1.3em;"></span>
                <div class="actionSheetItemText">
                    <div class="actionSheetItemText-primary">Standard-Spuren / Version</div>
                    <div class="actionSheetItemText-secondary" style="font-size: 0.85em; opacity: 0.7;">${escapeHtml(series.Name)}</div>
                </div>
            `;

            menuItem.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();

                // Schließe das Kontextmenü
                const actionSheet = scroller.closest('.actionSheet') || document.querySelector('.actionSheet');
                if (actionSheet && actionSheet.parentNode) {
                    actionSheet.parentNode.removeChild(actionSheet);
                }

                openSettingsModal(series);
            });

            // Vor den letzten Befehlen (z. B. "Löschen") oder am Ende der Liste einfügen
            scroller.appendChild(menuItem);
        }).catch(function (err) {
            console.debug('[DefaultSettings] resolveSeriesItem:', err);
        });
    }

    function resolveSeriesItem(userId, itemId) {
        const norm = normalizeId(itemId);
        if (itemCache.has(norm)) {
            return Promise.resolve(itemCache.get(norm));
        }

        return ApiClient.getItem(userId, itemId).then(function (item) {
            if (!item) return null;

            if (item.Type === 'Series') {
                itemCache.set(norm, item);
                return item;
            }

            // Falls es eine Episode oder Season ist: Ermittle die zugehörige Serie
            const seriesId = item.SeriesId;
            if (seriesId) {
                const normSeries = normalizeId(seriesId);
                if (itemCache.has(normSeries)) {
                    return itemCache.get(normSeries);
                }

                return ApiClient.getItem(userId, seriesId).then(function (series) {
                    if (series && series.Type === 'Series') {
                        itemCache.set(normSeries, series);
                        itemCache.set(norm, series);
                        return series;
                    }
                    return null;
                });
            }

            return null;
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
            <div style="display: flex; align-items: center; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.1); padding-bottom: 0.6em; margin-bottom: 1em;">
                <div style="display: flex; align-items: center;">
                    <span class="material-icons tune" aria-hidden="true" style="margin-right: 0.5em; font-size: 1.5em; color: #00a4dc;"></span>
                    <h2 style="margin: 0; font-size: 1.25em;">Standardeinstellungen</h2>
                </div>
                <button type="button" id="dsModalCloseIcon" style="background: transparent; border: none; color: #aaa; cursor: pointer; padding: 0.2em;">
                    <span class="material-icons close" aria-hidden="true" style="font-size: 1.3em;"></span>
                </button>
            </div>

            <div style="font-size: 1.05em; font-weight: bold; color: #00a4dc; margin-bottom: 1em;">
                ${escapeHtml(series.Name)}
            </div>

            <div id="dsModalLoading" style="padding: 2em; text-align: center; color: #aaa;">Lade Optionen...</div>
            <form id="dsModalForm" style="display: none;">
                <div style="margin-bottom: 1.2em;">
                    <label for="dsModalVersionSelect" style="display: block; margin-bottom: 0.35em; font-weight: bold;">Bevorzugte Version / Fassung:</label>
                    <select id="dsModalVersionSelect" style="width: 100%; padding: 0.6em; border-radius: 4px; background: #2b2b2b; color: #fff; border: 1px solid #444; font-size: 0.95em;">
                        <option value="">-- Jellyfin Standard beibehalten --</option>
                    </select>
                    <input type="text" id="dsModalVersionCustom" placeholder="Oder Freitext (z. B. Bunt, Schwarzweiss)..." style="width: 100%; margin-top: 0.4em; padding: 0.6em; border-radius: 4px; background: #2b2b2b; color: #fff; border: 1px solid #444; box-sizing: border-box; font-size: 0.95em;" />
                    <div style="font-size: 0.8em; color: #aaa; margin-top: 0.25em;">Fehlt die Version bei einer Folge, greift automatisch Jellyfins Standard.</div>
                </div>

                <div style="margin-bottom: 1.2em;">
                    <label for="dsModalAudioSelect" style="display: block; margin-bottom: 0.35em; font-weight: bold;">Audiosprache für diese Serie:</label>
                    <select id="dsModalAudioSelect" style="width: 100%; padding: 0.6em; border-radius: 4px; background: #2b2b2b; color: #fff; border: 1px solid #444; font-size: 0.95em;">
                        <option value="">-- Jellyfin Kontoeinstellung verwenden --</option>
                    </select>
                </div>

                <div style="margin-bottom: 1.2em;">
                    <label for="dsModalSubSelect" style="display: block; margin-bottom: 0.35em; font-weight: bold;">Untertitelsprache für diese Serie:</label>
                    <select id="dsModalSubSelect" style="width: 100%; padding: 0.6em; border-radius: 4px; background: #2b2b2b; color: #fff; border: 1px solid #444; font-size: 0.95em;">
                        <option value="">-- Jellyfin Kontoeinstellung verwenden --</option>
                    </select>
                </div>

                <div style="margin-bottom: 1.6em;">
                    <label for="dsModalSubModeSelect" style="display: block; margin-bottom: 0.35em; font-weight: bold;">Untertitel-Modus:</label>
                    <select id="dsModalSubModeSelect" style="width: 100%; padding: 0.6em; border-radius: 4px; background: #2b2b2b; color: #fff; border: 1px solid #444; font-size: 0.95em;">
                        <option value="">-- Jellyfin Kontoeinstellung verwenden --</option>
                        <option value="0">Nur Forced (erzwungene fremde Dialoge)</option>
                        <option value="1">Immer anzeigen</option>
                        <option value="2">Smart (nur wenn Ton fremdsprachig)</option>
                        <option value="3">Deaktiviert (keine Untertitel)</option>
                    </select>
                </div>

                <div style="display: flex; justify-content: space-between; gap: 0.5em; flex-wrap: wrap;">
                    <button type="submit" class="raised button-submit emby-button" style="background: #00a4dc; color: #fff; padding: 0.6em 1.4em; border: none; border-radius: 4px; cursor: pointer; font-weight: bold;">
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

        const closeModal = function () { overlay.remove(); };
        document.getElementById('dsModalCancel').onclick = closeModal;
        document.getElementById('dsModalCloseIcon').onclick = closeModal;
        overlay.addEventListener('click', function (e) {
            if (e.target === overlay) closeModal();
        });

        // 1. Metadaten abrufen (mit clientseitigem Fallback)
        const metaPromise = ApiClient.getJSON(ApiClient.getUrl(`Plugins/DefaultSettings/Series/${series.Id}/Metadata?userId=${currentUserId}&targetUserId=${currentUserId}`))
            .catch(function () {
                if (!ApiClient.getItems) return { AvailableVersions: [], AvailableAudioLanguages: [], AvailableSubtitleLanguages: [] };
                return ApiClient.getItems(currentUserId, {
                    ParentId: series.Id,
                    IncludeItemTypes: 'Episode',
                    Recursive: true,
                    Fields: 'MediaSources,MediaStreams',
                    Limit: 25
                }).then(function (res) {
                    const eps = (res && res.Items) ? res.Items : [];
                    const vSet = new Set();
                    const aSet = new Set();
                    const sSet = new Set();

                    eps.forEach(ep => {
                        (ep.MediaSources || []).forEach(src => {
                            if (src.Name && src.Name !== ep.Name) vSet.add(src.Name);
                            (src.MediaStreams || []).forEach(st => {
                                if (st.Type === 'Audio' && st.Language) aSet.add(st.Language.toLowerCase());
                                if (st.Type === 'Subtitle' && st.Language) sSet.add(st.Language.toLowerCase());
                            });
                        });
                    });

                    return {
                        AvailableVersions: Array.from(vSet),
                        AvailableAudioLanguages: Array.from(aSet),
                        AvailableSubtitleLanguages: Array.from(sSet)
                    };
                }).catch(e => ({ AvailableVersions: [], AvailableAudioLanguages: [], AvailableSubtitleLanguages: [] }));
            });

        // 2. Bestehenden Override abrufen (mit Fallback auf getPluginConfiguration)
        const overridePromise = ApiClient.getJSON(ApiClient.getUrl(`Plugins/DefaultSettings/Series/${series.Id}?userId=${currentUserId}&targetUserId=${currentUserId}`))
            .catch(function () {
                return ApiClient.getPluginConfiguration(pluginId).then(function (cfg) {
                    const uPrefs = (cfg && cfg.UserPreferences) ? cfg.UserPreferences : [];
                    const u = uPrefs.find(p => normalizeId(p.UserId) === normalizeId(currentUserId));
                    if (u && u.SeriesOverrides) {
                        return u.SeriesOverrides.find(o => normalizeId(o.SeriesId) === normalizeId(series.Id)) || null;
                    }
                    return null;
                }).catch(e => null);
            });

        Promise.all([metaPromise, overridePromise]).then(function (results) {
            const metadata = results[0] || {};
            const rawOverride = results[1] || null;
            const currentOverride = (rawOverride && (rawOverride.PreferredVersion || rawOverride.AudioLanguage || rawOverride.SubtitleLanguage || (rawOverride.SubtitleMode !== null && rawOverride.SubtitleMode !== undefined)))
                ? rawOverride
                : null;

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
                opt.value = lang.toLowerCase();
                opt.textContent = getLangName(lang);
                audioSelect.appendChild(opt);
            });

            // Untertitel-Sprachen befüllen
            const subSelect = document.getElementById('dsModalSubSelect');
            const standardSubs = ['deu', 'eng', 'jpn'];
            const allSubs = Array.from(new Set([...(metadata.AvailableSubtitleLanguages || []), ...standardSubs]));
            allSubs.forEach(function (lang) {
                const opt = document.createElement('option');
                opt.value = lang.toLowerCase();
                opt.textContent = getLangName(lang);
                subSelect.appendChild(opt);
            });

            // Vorhandene Werte vorselektieren
            if (currentOverride) {
                if (currentOverride.PreferredVersion) {
                    const matchOpt = Array.from(verSelect.options).find(o => o.value.toLowerCase() === currentOverride.PreferredVersion.toLowerCase());
                    if (matchOpt) {
                        verSelect.value = matchOpt.value;
                    } else {
                        document.getElementById('dsModalVersionCustom').value = currentOverride.PreferredVersion;
                    }
                }
                if (currentOverride.AudioLanguage) {
                    audioSelect.value = currentOverride.AudioLanguage.toLowerCase();
                }
                if (currentOverride.SubtitleLanguage) {
                    subSelect.value = currentOverride.SubtitleLanguage.toLowerCase();
                }
                if (currentOverride.SubtitleMode !== null && currentOverride.SubtitleMode !== undefined) {
                    document.getElementById('dsModalSubModeSelect').value = currentOverride.SubtitleMode;
                }

                const btnReset = document.getElementById('dsModalReset');
                btnReset.style.display = 'inline-block';
                btnReset.onclick = function () {
                    ApiClient.ajax({
                        type: 'DELETE',
                        url: ApiClient.getUrl(`Plugins/DefaultSettings/Series/${series.Id}?userId=${currentUserId}&targetUserId=${currentUserId}`)
                    }).catch(function () {
                        return ApiClient.getPluginConfiguration(pluginId).then(function (cfg) {
                            cfg.UserPreferences = cfg.UserPreferences || [];
                            const u = cfg.UserPreferences.find(p => normalizeId(p.UserId) === normalizeId(currentUserId));
                            if (u && u.SeriesOverrides) {
                                u.SeriesOverrides = u.SeriesOverrides.filter(o => normalizeId(o.SeriesId) !== normalizeId(series.Id));
                                return ApiClient.updatePluginConfiguration(pluginId, cfg);
                            }
                        });
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
                const customVer = document.getElementById('dsModalVersionCustom').value.trim();
                let version = customVer || verSelect.value;
                let audio = audioSelect.value;
                let sub = subSelect.value;
                let modeVal = document.getElementById('dsModalSubModeSelect').value;
                let subMode = modeVal === '' ? null : parseInt(modeVal, 10);

                const payload = {
                    SeriesId: series.Id,
                    SeriesName: series.Name,
                    AudioLanguage: audio || null,
                    SubtitleLanguage: sub || null,
                    SubtitleMode: subMode,
                    PreferredVersion: version || null
                };

                ApiClient.ajax({
                    type: 'POST',
                    url: ApiClient.getUrl(`Plugins/DefaultSettings/Series/${series.Id}?userId=${currentUserId}&targetUserId=${currentUserId}`),
                    data: JSON.stringify(payload),
                    contentType: 'application/json'
                }).catch(function (err) {
                    console.warn('[DefaultSettings] POST fehlgeschlagen, nutze updatePluginConfiguration Fallback:', err);
                    return ApiClient.getPluginConfiguration(pluginId).then(function (cfg) {
                        cfg.UserPreferences = cfg.UserPreferences || [];
                        let u = cfg.UserPreferences.find(p => normalizeId(p.UserId) === normalizeId(currentUserId));
                        if (!u) {
                            u = { UserId: currentUserId, SeriesOverrides: [] };
                            cfg.UserPreferences.push(u);
                        }
                        u.SeriesOverrides = u.SeriesOverrides || [];
                        const exIdx = u.SeriesOverrides.findIndex(o => normalizeId(o.SeriesId) === normalizeId(series.Id));
                        if (exIdx >= 0) {
                            u.SeriesOverrides[exIdx] = payload;
                        } else {
                            u.SeriesOverrides.push(payload);
                        }
                        return ApiClient.updatePluginConfiguration(pluginId, cfg);
                    });
                }).then(function () {
                    if (typeof Dashboard !== 'undefined' && Dashboard.alert) {
                        Dashboard.alert('Standardeinstellungen für "' + series.Name + '" gespeichert!');
                    }
                    overlay.remove();
                }).catch(function (err) {
                    alert('Fehler beim Speichern: ' + (err.message || err));
                });
            };

        }).catch(function (err) {
            document.getElementById('dsModalLoading').textContent = 'Fehler beim Laden: ' + (err.message || err);
        });
    }

    function getLangName(code) {
        if (!code) return '';
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
})();
