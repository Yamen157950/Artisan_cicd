/**
 * Artisan — shared client-side behavior
 */
(function () {
  "use strict";

  var ROLE_KEY = "artisan_selected_role";
  var TRADE_KEY = "artisan_selected_trade";
  var AVATAR_KEY = "artisan_provider_avatar";
  /** When set, GET profile must not overwrite the in-memory avatar (new upload not yet confirmed on server). */
  var AVATAR_DIRTY_KEY = "artisan_provider_avatar_dirty";
  var PORTFOLIO_KEY = "artisan_provider_work_photos";
  var MAX_WORK_PHOTOS = 12;
  var SEARCH_VISIBLE_KEY = "artisan_provider_visible_in_search";
  var RATINGS_KEY = "artisan_provider_ratings_v1";
  var PRICE_KEY = "artisan_provider_price";
  var PRICE_UNIT_KEY = "artisan_provider_price_unit";
  var EXPERIENCE_KEY = "artisan_provider_experience_years";
  var CITY_KEY = "artisan_provider_city";
  var POST_LOGIN_REDIRECT_KEY = "artisan_after_login_redirect";

  /** Safe relative URL saved before login (e.g. return to provider profile to book). */
  function consumePostLoginRedirect() {
    try {
      var u = sessionStorage.getItem(POST_LOGIN_REDIRECT_KEY);
      if (u) sessionStorage.removeItem(POST_LOGIN_REDIRECT_KEY);
      if (!u || typeof u !== "string") return null;
      u = u.trim();
      if (!u || u.indexOf("://") !== -1 || u.indexOf("//") === 0) return null;
      if (!/^[\w./?&=%\-#]+$/.test(u)) return null;
      var pathOnly = u.split("?")[0].split("#")[0];
      if (!/\.html$/i.test(pathOnly)) return null;
      return u;
    } catch (e) {
      return null;
    }
  }

  function $(sel, root) {
    return (root || document).querySelector(sel);
  }

  function $all(sel, root) {
    return [].slice.call((root || document).querySelectorAll(sel));
  }

  function getPageName() {
    var path = window.location.pathname || "";
    var file = path.split("/").pop() || path.split("\\").pop() || "";
    if (!file || file.indexOf(".html") === -1) file = "index.html";
    return file;
  }

  /** Only clear JWT on the real logout URL (do not rely on getPageName — it can mis-detect some path shapes). */
  function shouldClearAuthFromLogoutQuery() {
    try {
      var path = String(window.location.pathname || "").replace(/\\/g, "/");
      if (!/\/login\.html$/i.test(path) && !/^login\.html$/i.test(path)) return false;
      return new URLSearchParams(window.location.search).get("logout") === "1";
    } catch (e) {
      return false;
    }
  }

  /** admin-dashboard.html — user directory, block/unblock (Admin JWT only). */
  function initAdminDashboardPage() {
    var root = document.getElementById("admin-dashboard-root");
    if (!root) return;
    if (!window.ArtisanAuth || typeof ArtisanAuth.ensureLoggedIn !== "function") return;
    if (!ArtisanAuth.ensureLoggedIn()) return;

    var u = ArtisanAuth.getStoredUser();
    var role = u && u.role ? String(u.role).toLowerCase() : "";
    if (role !== "admin") {
      if (window.ArtisanUI && window.ArtisanUI.showToast) {
        ArtisanUI.showToast("Admin access only.", "error");
      }
      window.location.href = "index.html";
      return;
    }

    var tbody = document.getElementById("admin-users-tbody");
    var searchInput = document.getElementById("admin-user-search");
    var btnSearch = document.getElementById("admin-user-search-btn");
    var btnPrev = document.getElementById("admin-page-prev");
    var btnNext = document.getElementById("admin-page-next");
    var pageLabel = document.getElementById("admin-page-label");
    var state = { page: 1, pageSize: 12, q: "", total: 0 };

    function roleBadges(roles) {
      var arr = Array.isArray(roles) ? roles : [];
      return arr
        .map(function (r) {
          var cls = "admin-badge admin-badge--muted";
          if (String(r).toLowerCase() === "admin") cls = "admin-badge admin-badge--admin";
          else if (String(r).toLowerCase() === "moderator") cls = "admin-badge admin-badge--mod";
          else if (String(r).toLowerCase() === "provider") cls = "admin-badge admin-badge--prov";
          else if (String(r).toLowerCase() === "customer") cls = "admin-badge admin-badge--cust";
          return '<span class="' + cls + '">' + escapeHtml(String(r)) + "</span>";
        })
        .join(" ");
    }

    function escapeHtml(s) {
      return String(s)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;");
    }

    function isAdminRoles(roles) {
      return (Array.isArray(roles) ? roles : []).some(function (r) {
        return String(r).toLowerCase() === "admin";
      });
    }

    function renderRows(items) {
      if (!tbody) return;
      tbody.innerHTML = "";
      if (!items || !items.length) {
        tbody.innerHTML =
          '<tr><td colspan="5" class="admin-table__empty">No users match this search.</td></tr>';
        return;
      }
      items.forEach(function (row) {
        var tr = document.createElement("tr");
        var adminRow = isAdminRoles(row.roles);
        var locked = !!row.isLockedOut;
        var actions = [];
        if (!adminRow) {
          if (locked) {
            actions.push(
              '<button type="button" class="admin-btn admin-btn--secondary" data-act="unblock" data-id="' +
                escapeHtml(row.id) +
                '">Unblock</button>'
            );
          } else {
            actions.push(
              '<button type="button" class="admin-btn admin-btn--danger" data-act="block" data-id="' +
                escapeHtml(row.id) +
                '">Block</button>'
            );
          }
        } else {
          actions.push('<span class="admin-table__dash">—</span>');
        }
        tr.innerHTML =
          "<td>" +
          escapeHtml(row.email || "") +
          "</td><td>" +
          escapeHtml(row.fullName || "—") +
          "</td><td>" +
          roleBadges(row.roles) +
          '</td><td class="admin-table__status">' +
          (locked
            ? '<span class="admin-status admin-status--locked">Blocked</span>'
            : '<span class="admin-status admin-status--ok">Active</span>') +
          '</td><td class="admin-table__actions">' +
          actions.join("") +
          "</td>";
        tbody.appendChild(tr);
      });

      tbody.querySelectorAll("button[data-act]").forEach(function (btn) {
        btn.addEventListener("click", function () {
          var id = btn.getAttribute("data-id");
          var act = btn.getAttribute("data-act");
          if (!id || !act) return;
          var p =
            act === "block"
              ? ArtisanAuth.adminBlockUser(id)
              : act === "unblock"
                ? ArtisanAuth.adminUnblockUser(id)
                : Promise.resolve({ ok: false });
          p.then(function (res) {
            if (!res.ok) {
              var msg =
                (res.data && (res.data.error || (res.data.errors && res.data.errors.join(" ")))) ||
                "Action failed.";
              if (window.ArtisanUI && window.ArtisanUI.showToast) ArtisanUI.showToast(msg, "error");
              return;
            }
            if (window.ArtisanUI && window.ArtisanUI.showToast) ArtisanUI.showToast("Updated.", "success");
            load();
          });
        });
      });
    }

    function load() {
      if (window.ArtisanUI && window.ArtisanUI.showLoading) ArtisanUI.showLoading("Loading users…");
      ArtisanAuth.adminListUsers({ q: state.q, page: state.page, pageSize: state.pageSize }).then(function (res) {
        if (window.ArtisanUI && window.ArtisanUI.hideLoading) ArtisanUI.hideLoading();
        if (res.status === 401) {
          window.location.href = "login.html";
          return;
        }
        if (!res.ok || !res.data) {
          if (window.ArtisanUI && window.ArtisanUI.showToast) ArtisanUI.showToast("Could not load users.", "error");
          return;
        }
        state.total = res.data.total || 0;
        renderRows(res.data.items || []);
        if (pageLabel) {
          var pages = Math.max(1, Math.ceil(state.total / state.pageSize));
          pageLabel.textContent = "Page " + state.page + " of " + pages + " · " + state.total + " users";
        }
        if (btnPrev) btnPrev.disabled = state.page <= 1;
        if (btnNext) btnNext.disabled = state.page * state.pageSize >= state.total;
      });
    }

    if (btnSearch) {
      btnSearch.addEventListener("click", function () {
        state.q = searchInput ? searchInput.value.trim() : "";
        state.page = 1;
        load();
      });
    }
    if (searchInput) {
      searchInput.addEventListener("keydown", function (e) {
        if (e.key === "Enter") {
          e.preventDefault();
          state.q = searchInput.value.trim();
          state.page = 1;
          load();
        }
      });
    }
    if (btnPrev) {
      btnPrev.addEventListener("click", function () {
        if (state.page > 1) {
          state.page--;
          load();
        }
      });
    }
    if (btnNext) {
      btnNext.addEventListener("click", function () {
        if (state.page * state.pageSize < state.total) {
          state.page++;
          load();
        }
      });
    }

    load();
  }

  /** Highlight active nav link */
  function initNav() {
    var page = getPageName();
    $all(".nav__link[data-nav]").forEach(function (link) {
      if (link.getAttribute("href") === page) {
        link.classList.add("nav__link--active");
      }
    });

    var toggle = $(".nav-toggle");
    var nav = $(".nav[data-nav-panel]");
    if (toggle && nav) {
      toggle.addEventListener("click", function () {
        var open = nav.classList.toggle("is-open");
        toggle.setAttribute("aria-expanded", open ? "true" : "false");
      });
      $all(".nav__link", nav).forEach(function (a) {
        a.addEventListener("click", function () {
          nav.classList.remove("is-open");
          toggle.setAttribute("aria-expanded", "false");
        });
      });
    }
  }

  /** Real-time when a provider accepts/rejects a booking (SignalR on /hubs/chat). */
  var _bookingHubState = { conn: null, starting: null };

  function loadSignalRScript() {
    return new Promise(function (resolve, reject) {
      if (typeof signalR !== "undefined") {
        resolve();
        return;
      }
      var s = document.createElement("script");
      s.src = "https://cdn.jsdelivr.net/npm/@microsoft/signalr@8.0.7/dist/browser/signalr.min.js";
      s.async = true;
      s.onload = function () {
        resolve();
      };
      s.onerror = function () {
        reject(new Error("signalr_load_failed"));
      };
      document.head.appendChild(s);
    });
  }

  function bookingHubApiBase() {
    var raw = window.ARTISAN_API_BASE || "http://localhost:5172";
    raw = String(raw || "").trim().replace(/\/$/, "");
    if (!/^https?:\/\//i.test(raw)) raw = "http://localhost:5172";
    return raw;
  }

  function ensureGlobalBookingHub() {
    if (!window.ArtisanAuth || typeof ArtisanAuth.getAccessToken !== "function" || !ArtisanAuth.getAccessToken()) {
      return Promise.resolve(null);
    }
    return loadSignalRScript().then(function () {
      if (_bookingHubState.conn && _bookingHubState.conn.state === signalR.HubConnectionState.Connected) {
        return _bookingHubState.conn;
      }
      if (_bookingHubState.starting) return _bookingHubState.starting;
      var c = new signalR.HubConnectionBuilder()
        .withUrl(bookingHubApiBase() + "/hubs/chat", {
          accessTokenFactory: function () {
            return (ArtisanAuth.getAccessToken && ArtisanAuth.getAccessToken()) || "";
          },
          transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .build();
      c.on("BookingResponse", function (payload) {
        try {
          window.dispatchEvent(new CustomEvent("artisan-booking-response", { detail: payload || null }));
        } catch (eBr) {}
      });
      _bookingHubState.starting = c
        .start()
        .then(function () {
          _bookingHubState.starting = null;
          _bookingHubState.conn = c;
          return c;
        })
        .catch(function () {
          _bookingHubState.starting = null;
          _bookingHubState.conn = null;
          return null;
        });
      return _bookingHubState.starting;
    });
  }

  /**
   * Customer booking responses: bell, badge (unread), dropdown list, polling + SignalR refresh.
   * @param {{ wrapId?: string, btnId?: string, panelId?: string, listId?: string, badgeId?: string, panelTitle?: string }} opts
   */
  function initBookingNotificationCenter(opts) {
    opts = opts || {};
    var wrapId = opts.wrapId || "booking-notify-wrap";
    var wrap = document.getElementById(wrapId);
    if (!wrap || wrap.getAttribute("data-bn-init") === "1") return;
    wrap.setAttribute("data-bn-init", "1");

    var btn = document.getElementById(opts.btnId || "booking-notify-btn");
    var panel = document.getElementById(opts.panelId || "booking-notify-panel");
    var listEl = document.getElementById(opts.listId || "booking-notify-list");
    var badge = document.getElementById(opts.badgeId || "booking-notify-badge");
    if (!btn || !panel || !listEl || !badge) return;

    var panelTitle = opts.panelTitle || "Booking updates";
    var headEl = panel.querySelector(".booking-notify-head");
    if (headEl) headEl.textContent = panelTitle;

    var SEEN_KEY = "artisan_booking_notif_seen_v1";
    var lastItems = [];
    var pollMs = opts.pollMs != null ? opts.pollMs : 120000;

    function pick(o, a, b) {
      if (!o) return undefined;
      if (o[a] !== undefined && o[a] !== null) return o[a];
      if (o[b] !== undefined && o[b] !== null) return o[b];
      return undefined;
    }

    function normalizeNotif(raw) {
      if (!raw || typeof raw !== "object") return null;
      var id = pick(raw, "id", "Id");
      if (id == null || id === "") return null;
      var st = String(pick(raw, "status", "Status") || "").toLowerCase();
      if (st !== "accepted" && st !== "rejected") return null;
      return {
        id: String(id),
        status: st,
        providerDisplayName: String(pick(raw, "providerDisplayName", "ProviderDisplayName") || "Provider"),
        providerProfileId: String(pick(raw, "providerProfileId", "ProviderProfileId") || ""),
        providerUserId: String(pick(raw, "providerUserId", "ProviderUserId") || ""),
        body: String(pick(raw, "body", "Body") || ""),
        createdAt: pick(raw, "createdAt", "CreatedAt"),
        respondedAt: pick(raw, "respondedAt", "RespondedAt"),
      };
    }

    function mergePushPayload(detail) {
      if (!detail || typeof detail !== "object") return;
      var row = normalizeNotif(detail);
      if (!row) return;
      var ix = -1;
      for (var i = 0; i < lastItems.length; i++) {
        if (lastItems[i].id === row.id) {
          ix = i;
          break;
        }
      }
      if (ix >= 0) lastItems[ix] = row;
      else lastItems.unshift(row);
      lastItems.sort(function (a, b) {
        var ra = a.respondedAt ? new Date(a.respondedAt).getTime() : 0;
        var rb = b.respondedAt ? new Date(b.respondedAt).getTime() : 0;
        if (rb !== ra) return rb - ra;
        var ca = a.createdAt ? new Date(a.createdAt).getTime() : 0;
        var cb = b.createdAt ? new Date(b.createdAt).getTime() : 0;
        return cb - ca;
      });
      if (lastItems.length > 50) lastItems = lastItems.slice(0, 50);
    }

    function loadSeen() {
      try {
        var raw = localStorage.getItem(SEEN_KEY);
        var arr = raw ? JSON.parse(raw) : [];
        if (!Array.isArray(arr)) return new Set();
        return new Set(arr.map(String).slice(-400));
      } catch (eLs) {
        return new Set();
      }
    }

    function saveSeen(set) {
      try {
        var arr = Array.from(set);
        if (arr.length > 400) arr = arr.slice(-400);
        localStorage.setItem(SEEN_KEY, JSON.stringify(arr));
      } catch (eSv) {}
    }

    function markAllCurrentSeen() {
      var set = loadSeen();
      lastItems.forEach(function (n) {
        set.add(n.id);
      });
      saveSeen(set);
      updateBadge();
    }

    function updateBadge() {
      if (!panel.hidden) {
        badge.hidden = true;
        return;
      }
      var seen = loadSeen();
      var unread = lastItems.filter(function (n) {
        return !seen.has(n.id);
      }).length;
      if (unread > 0) {
        badge.hidden = false;
        badge.textContent = unread > 99 ? "99+" : String(unread);
      } else {
        badge.hidden = true;
      }
    }

    function fmtWhen(iso) {
      if (!iso) return "—";
      try {
        return new Date(iso).toLocaleString();
      } catch (eDt) {
        return "—";
      }
    }

    function truncate(s, max) {
      var t = String(s || "").trim();
      if (t.length <= max) return t;
      return t.slice(0, max - 1) + "…";
    }

    function escapeHtml(s) {
      return String(s)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;");
    }

    function renderList(errMsg) {
      if (errMsg) {
        listEl.innerHTML = '<div class="booking-notify-err" role="alert">' + escapeHtml(errMsg) + "</div>";
        return;
      }
      if (!lastItems.length) {
        listEl.innerHTML =
          '<div class="booking-notify-empty" role="status">' +
          "<strong>No responses yet</strong><br />" +
          "When a provider accepts or declines a booking you sent from the calendar, it will appear here with the date and time of their response." +
          "</div>";
        return;
      }
      listEl.innerHTML = lastItems
        .map(function (n) {
          var name = escapeHtml(n.providerDisplayName);
          var titleLine;
          var subLine;
          if (n.status === "accepted") {
            titleLine = "Booking confirmed";
            subLine = name + " accepted your request. You can continue the conversation in My chats.";
          } else {
            titleLine = "Booking not available";
            subLine = name + " declined this request. You are welcome to book another provider on Browse.";
          }
          var when = escapeHtml(fmtWhen(n.respondedAt || n.createdAt));
          var bodyLine = n.body ? '<p class="booking-notify-item__body">' + escapeHtml(truncate(n.body, 220)) + "</p>" : "";
          var profileHref =
            n.providerProfileId &&
            "provider-profile.html?id=" + encodeURIComponent(n.providerProfileId) + "&pn=" + encodeURIComponent(n.providerDisplayName);
          var chatHref = n.providerUserId ? "chat-thread.html?partner=" + encodeURIComponent(n.providerUserId) : "";
          var links = "";
          if (profileHref) links += '<a href="' + profileHref + '">View profile</a>';
          if (chatHref) links += (links ? " · " : "") + '<a href="' + chatHref + '">Open chat</a>';
          var linksBlock = links ? '<div class="booking-notify-links">' + links + "</div>" : "";
          return (
            '<article class="booking-notify-item booking-notify-item--' +
            escapeHtml(n.status) +
            '">' +
            '<p class="booking-notify-item__title">' +
            escapeHtml(titleLine) +
            "</p>" +
            '<p class="booking-notify-item__meta">' +
            escapeHtml(subLine) +
            "</p>" +
            '<p class="booking-notify-item__meta">Responded · ' +
            when +
            "</p>" +
            bodyLine +
            linksBlock +
            "</article>"
          );
        })
        .join("");
    }

    function fetchNotifications(showErrorsInPanel) {
      if (!window.ArtisanAuth || !ArtisanAuth.getCustomerBookingNotifications || !ArtisanAuth.getAccessToken()) return;
      if (!ArtisanAuth.getAccessToken()) return;
      ArtisanAuth.getCustomerBookingNotifications().then(function (res) {
        if (!res.ok) {
          lastItems = [];
          updateBadge();
          if (showErrorsInPanel) {
            var msg =
              res.status === 401
                ? "Please sign in again to load booking updates."
                : res.data && res.data.error
                  ? String(res.data.error)
                  : "Could not load booking updates (" + res.status + ").";
            renderList(msg);
          }
          return;
        }
        var raw = Array.isArray(res.data) ? res.data : [];
        lastItems = raw.map(normalizeNotif).filter(Boolean);
        updateBadge();
        if (!panel.hidden) {
          renderList(null);
          markAllCurrentSeen();
        }
      });
    }

    function openPanel() {
      panel.hidden = false;
      btn.setAttribute("aria-expanded", "true");
      renderList(null);
      fetchNotifications(true);
    }

    function closePanel() {
      panel.hidden = true;
      btn.setAttribute("aria-expanded", "false");
      updateBadge();
    }

    btn.addEventListener("click", function (e) {
      e.stopPropagation();
      if (panel.hidden) openPanel();
      else closePanel();
    });

    document.addEventListener("click", function () {
      if (!panel.hidden) closePanel();
    });
    panel.addEventListener("click", function (e) {
      e.stopPropagation();
    });

    document.addEventListener("keydown", function (e) {
      if (e.key === "Escape" && !panel.hidden) closePanel();
    });

    function onBookingResponse(ev) {
      var detail = ev && ev.detail;
      mergePushPayload(detail);
      updateBadge();
      if (!panel.hidden) {
        renderList(null);
        markAllCurrentSeen();
      } else {
        fetchNotifications(false);
      }
    }

    window.addEventListener("artisan-booking-response", onBookingResponse);

    var pollId = setInterval(function () {
      fetchNotifications(false);
    }, pollMs);

    window.addEventListener("beforeunload", function () {
      try {
        clearInterval(pollId);
      } catch (eBu) {}
    });

    ensureGlobalBookingHub().catch(function () {});

    fetchNotifications(false);
  }

  /** Premium header: center Browse (+ Log out except on customer-account) + right icons when signed in. */
  function initIndexAuthNav() {
    var host = $("#main-nav-auth");
    var center = $("#main-nav-center");
    if (!host || !window.ArtisanAuth || typeof ArtisanAuth.getAccessToken !== "function") return;

    var nav = host.closest(".main-nav--premium");
    var signedIn = !!ArtisanAuth.getAccessToken();
    if (nav) nav.classList.toggle("main-nav--premium--signed-in", signedIn);

    var svgHome =
      '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>';
    var svgUser =
      '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M20 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>';
    var svgChat =
      '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2z"/></svg>';

    if (!ArtisanAuth.getAccessToken()) {
      if (center) {
        center.innerHTML = "";
        center.setAttribute("aria-hidden", "true");
        center.classList.remove("main-nav__center--visible");
      }
      host.innerHTML =
        '<a href="customer-services.html" class="main-nav__btn main-nav__btn--ghost">Browse</a>' +
        '<a href="login.html" class="main-nav__btn main-nav__btn--ghost">Log in</a>' +
        '<a href="role-selection.html" class="main-nav__btn main-nav__btn--primary">Sign up</a>';
      return;
    }

    var u = typeof ArtisanAuth.getStoredUser === "function" ? ArtisanAuth.getStoredUser() : null;
    var role = u && u.role ? String(u.role).toLowerCase() : "";
    var isAdmin = role === "admin";
    var browseHref = "customer-services.html";
    var browseLabel = "Browse";
    if (role === "provider") {
      browseHref = "provider-dashboard.html";
      browseLabel = "Dashboard";
    }

    var hideCenterLogout = false;
    try {
      hideCenterLogout = (window.location.pathname || "").indexOf("customer-account.html") !== -1;
    } catch (eNav) {}

    if (center) {
      center.setAttribute("aria-hidden", "false");
      center.classList.add("main-nav__center--visible");
      var centerHtml = "";
      if (!isAdmin) {
        centerHtml +=
          '<a href="' +
          browseHref +
          '" class="main-nav__center-link">' +
          browseLabel +
          "</a>";
      }
      if (!hideCenterLogout) {
        if (centerHtml) {
          centerHtml += '<span class="main-nav__center-sep" aria-hidden="true"></span>';
        }
        centerHtml +=
          '<a href="login.html?logout=1" class="main-nav__center-link main-nav__center-link--logout">Log out</a>';
      }
      center.innerHTML = centerHtml;
    }

    var accountHref =
      role === "admin"
        ? "admin-dashboard.html"
        : role === "provider"
          ? "provider-profile-settings.html"
          : "customer-account.html";
    var accountLabel =
      role === "admin" ? "Admin panel" : role === "provider" ? "Profile settings" : "My account";
    var accountBtn =
      '<a href="' +
      accountHref +
      '" class="main-nav__icon-btn" title="' +
      accountLabel +
      '" aria-label="' +
      accountLabel +
      '">' +
      svgUser +
      "</a>";

    var showBookingBell = role !== "provider" && role !== "admin";
    var svgBell =
      '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.73 21a2 2 0 0 1-3.46 0"/></svg>';

    var bookingBellHtml = "";
    if (showBookingBell) {
      bookingBellHtml =
        '<div class="booking-notify-wrap" id="nav-booking-notify-wrap">' +
        '<button type="button" class="main-nav__icon-btn booking-notify-btn" id="nav-booking-notify-btn" aria-label="Booking notifications" aria-expanded="false" aria-controls="nav-booking-notify-panel">' +
        svgBell +
        '<span class="booking-notify-badge" id="nav-booking-notify-badge" hidden aria-hidden="true">0</span>' +
        "</button>" +
        '<div class="booking-notify-panel" id="nav-booking-notify-panel" role="region" aria-label="Booking responses from providers" hidden>' +
        '<div class="booking-notify-head">Booking updates</div>' +
        '<div id="nav-booking-notify-list"></div>' +
        "</div>" +
        "</div>";
    }

    var homeBtn = isAdmin
      ? ""
      : '<a href="index.html" class="main-nav__icon-btn" title="Home" aria-label="Home">' +
        svgHome +
        "</a>";

    host.innerHTML =
      homeBtn +
      bookingBellHtml +
      accountBtn +
      '<a href="chat-thread.html" class="main-nav__icon-btn" title="My chats" aria-label="My chats">' +
      svgChat +
      "</a>";

    if (showBookingBell) {
      initBookingNotificationCenter({
        wrapId: "nav-booking-notify-wrap",
        btnId: "nav-booking-notify-btn",
        panelId: "nav-booking-notify-panel",
        listId: "nav-booking-notify-list",
        badgeId: "nav-booking-notify-badge",
        panelTitle: "Responses from providers",
      });
    }
  }

  function loadGsiScript() {
    return new Promise(function (resolve, reject) {
      if (window.google && google.accounts && google.accounts.id) {
        resolve();
        return;
      }
      var s = document.createElement("script");
      s.src = "https://accounts.google.com/gsi/client";
      s.async = true;
      s.defer = true;
      s.onload = function () {
        resolve();
      };
      s.onerror = function () {
        reject(new Error("gsi_load_failed"));
      };
      document.head.appendChild(s);
    });
  }

  function getGoogleSignInRoleForRequest() {
    var sr = document.getElementById("signup-role");
    if (sr && (sr.value === "customer" || sr.value === "provider")) return sr.value;
    return "customer";
  }

  function redirectAfterOAuthLogin(roleLower) {
    if (roleLower === "provider") {
      try {
        setSelectedTrade("");
      } catch (e) {}
      window.location.href = "provider-dashboard.html";
    } else {
      var next = consumePostLoginRedirect();
      window.location.href = next || "customer-account.html";
    }
  }

  /** Google Identity Services button on login / signup when API exposes googleClientId. */
  function initGoogleSignInOnAuthPages() {
    var slot = document.getElementById("google-signin-slot");
    if (!slot) return;
    var apiBase = window.ARTISAN_API_BASE || "http://localhost:5172";
    fetch(apiBase + "/api/auth/public-config")
      .then(function (r) {
        return r.ok ? r.json() : {};
      })
      .then(function (cfg) {
        if (!cfg || !cfg.googleClientId) {
          slot.innerHTML =
            '<p class="google-signin-hint" style="font-size:0.85rem;color:var(--color-text-muted);margin:0">Google sign-in: add your Web Client ID to <code>Google:ClientId</code> in Backend <code>appsettings.Development.json</code>, then restart the API.</p>';
          return;
        }
        return loadGsiScript().then(function () {
          if (!window.google || !google.accounts || !google.accounts.id) return;
          google.accounts.id.initialize({
            client_id: cfg.googleClientId,
            callback: function (response) {
              if (!response || !response.credential) return;
              if (!window.ArtisanAuth || typeof ArtisanAuth.signInWithGoogle !== "function") return;
              var role = getGoogleSignInRoleForRequest();
              showLoading("Signing in with Google…");
              ArtisanAuth.signInWithGoogle(response.credential, { role: role }).then(function (res) {
                hideLoading();
                if (!res.ok) {
                  var msg =
                    res.data && res.data.error
                      ? String(res.data.error)
                      : res.data && res.data.errors
                        ? Array.isArray(res.data.errors)
                          ? res.data.errors.join(" ")
                          : String(res.data.errors)
                        : "Google sign-in failed (" + res.status + ").";
                  showToast(msg, "error");
                  return;
                }
                if (typeof ArtisanAuth.persistAuthResponse === "function") ArtisanAuth.persistAuthResponse(res.data);
                try {
                  var nmRaw = res.data && (res.data.fullName != null ? res.data.fullName : res.data.FullName);
                  var nm = nmRaw != null ? String(nmRaw).trim() : "";
                  if (nm) sessionStorage.setItem("artisan_provider_name", nm);
                  if (String(res.data.role || res.data.Role || "").toLowerCase() === "provider") setSelectedTrade("");
                } catch (e) {}
                showToast("Signed in with Google.", "success");
                var rl = String(res.data.role || res.data.Role || "").toLowerCase();
                setTimeout(function () {
                  redirectAfterOAuthLogin(rl);
                }, 400);
              });
            },
            use_fedcm_for_prompt: false,
          });
          var w = slot.offsetWidth;
          if (!w || w < 200) w = 360;
          google.accounts.id.renderButton(slot, {
            theme: "outline",
            size: "large",
            text: "continue_with",
            width: Math.min(400, w),
            locale: "en",
          });
        });
      })
      .catch(function () {
        if (slot)
          slot.innerHTML =
            '<p class="google-signin-hint" style="font-size:0.85rem;color:var(--color-text-muted);margin:0">Could not load Google sign-in.</p>';
      });
  }

  /** customer-account.html — profile photo, details, security & provider hub */
  function initCustomerAccountPage() {
    var form = $("#customer-account-form");
    if (!form) return;
    if (!window.ArtisanAuth || typeof ArtisanAuth.me !== "function") return;

    var defaultAvatar =
      "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=240&q=80";
    var lastProfilePhotoUrl = null;
    var photoState = { dirty: false, value: null };

    function showAccountError(msg) {
      var el = $("#customer-account-error");
      if (el) {
        el.hidden = false;
        el.textContent = msg || "";
      }
    }

    function hideAccountError() {
      var el = $("#customer-account-error");
      if (el) {
        el.hidden = true;
        el.textContent = "";
      }
    }

    if (!ArtisanAuth.getAccessToken || !ArtisanAuth.getAccessToken()) {
      try {
        window.location.href = "login.html";
      } catch (e) {}
      return;
    }

    var emailEl = $("#customer-account-email");
    var nameInput = $("#customer-account-fullname");
    var phoneInput = $("#customer-account-phone");
    var avatarImg = $("#customer-avatar");
    var photoInput = $("#customer-photo-input");
    var photoBtn = $("#customer-photo-btn");
    var removePhotoBtn = $("#customer-remove-photo-btn");
    var sidebarName = $("#customer-sidebar-name");
    var sidebarEmail = $("#customer-sidebar-email");
    var sidebarPhone = $("#customer-sidebar-phone");
    var googleStatusEl = $("#customer-linked-google-status");

    function syncSidebarPhone(phoneValue) {
      if (!sidebarPhone) return;
      var t = (phoneValue != null ? String(phoneValue) : "").trim();
      if (t) {
        sidebarPhone.textContent = t;
        sidebarPhone.hidden = false;
      } else {
        sidebarPhone.textContent = "";
        sidebarPhone.hidden = true;
      }
    }

    function syncRemovePhotoVisibility() {
      if (!removePhotoBtn) return;
      var effective = photoState.dirty ? photoState.value : lastProfilePhotoUrl;
      removePhotoBtn.hidden = !effective;
    }

    function applyProfileToUi(d) {
      if (!d) return;
      if (emailEl) emailEl.textContent = d.email || "—";
      if (nameInput) nameInput.value = (d.fullName || "").trim();
      if (phoneInput) phoneInput.value = (d.phone || "").trim();
      if (sidebarName) sidebarName.textContent = (d.fullName || "Customer").trim() || "Customer";
      if (sidebarEmail) sidebarEmail.textContent = d.email || "";
      syncSidebarPhone(d.phone);
      lastProfilePhotoUrl = d.profilePhotoUrl && String(d.profilePhotoUrl).trim() ? String(d.profilePhotoUrl).trim() : null;
      photoState = { dirty: false, value: null };
      if (avatarImg) {
        avatarImg.src = lastProfilePhotoUrl || defaultAvatar;
        avatarImg.alt = (d.fullName || "Profile") + " — photo";
      }
      if (googleStatusEl) {
        googleStatusEl.textContent = d.linkedGoogle
          ? "Google is linked to this Artisan account."
          : "Not linked — you can use “Continue with Google” on login with the same email.";
      }
      syncRemovePhotoVisibility();
      if (typeof ArtisanAuth.applyAuthChrome === "function") ArtisanAuth.applyAuthChrome();
    }

    ArtisanAuth.me().then(function (res) {
      if (!res.ok || !res.data) {
        showAccountError("Could not load your profile. Try signing in again.");
        return;
      }
      hideAccountError();
      var d = res.data;
      var rl = String(d.role || "").toLowerCase();
      if (rl !== "customer") {
        showToast("Provider accounts use the provider dashboard.", "info");
        window.location.href = "provider-dashboard.html";
        return;
      }
      applyProfileToUi(d);
    });

    if (photoBtn && photoInput) {
      photoBtn.addEventListener("click", function () {
        photoInput.click();
      });
    }

    if (photoInput && avatarImg) {
      photoInput.addEventListener("change", function () {
        var file = photoInput.files && photoInput.files[0];
        if (!file) return;
        if (!/^image\//.test(file.type)) {
          showToast("Please choose an image file.", "error");
          photoInput.value = "";
          return;
        }
        fileToResizedJpegDataUrl(file, 720, 0.82, function (dataUrl) {
          if (!dataUrl) {
            showToast("Could not read that image.", "error");
            photoInput.value = "";
            return;
          }
          photoState = { dirty: true, value: dataUrl };
          avatarImg.src = dataUrl;
          syncRemovePhotoVisibility();
          showToast("Photo will be saved when you click Save changes.", "success");
          photoInput.value = "";
        });
      });
    }

    if (removePhotoBtn && avatarImg) {
      removePhotoBtn.addEventListener("click", function () {
        photoState = { dirty: true, value: null };
        avatarImg.src = defaultAvatar;
        syncRemovePhotoVisibility();
        showToast("Photo removed — save to apply.", "success");
      });
    }

    form.addEventListener("submit", function (e) {
      e.preventDefault();
      var cleanName = nameInput ? nameInput.value.trim() : "";
      if (cleanName.length < 2) {
        showToast("Please enter at least 2 characters for your name.", "error");
        return;
      }
      if (!ArtisanAuth.updateMyAccount) {
        showToast("Update API not available.", "error");
        return;
      }
      var profilePhotoUrl = photoState.dirty ? photoState.value : lastProfilePhotoUrl;
      showLoading("Saving…");
      ArtisanAuth.updateMyAccount({
        fullName: cleanName,
        phone: phoneInput && phoneInput.value.trim() ? phoneInput.value.trim() : null,
        profilePhotoUrl: profilePhotoUrl,
      }).then(function (res) {
        hideLoading();
        if (!res.ok) {
          var msg =
            res.data && res.data.errors
              ? Array.isArray(res.data.errors)
                ? res.data.errors.join(" ")
                : String(res.data.errors)
              : "Could not save (" + res.status + ").";
          showToast(msg, "error");
          return;
        }
        applyProfileToUi(res.data);
        var u = ArtisanAuth.getStoredUser() || {};
        ArtisanAuth.setStoredUser({
          email: res.data.email || u.email,
          fullName: res.data.fullName,
          role: res.data.role || u.role,
          providerProfileId: res.data.providerProfileId != null ? res.data.providerProfileId : u.providerProfileId,
        });
        try {
          sessionStorage.setItem("artisan_provider_name", (res.data.fullName || "").trim());
        } catch (e2) {}
        showToast("Account updated.", "success");
      });
    });
  }

  function setSelectedRole(role) {
    try {
      if (role === "customer" || role === "provider") {
        sessionStorage.setItem(ROLE_KEY, role);
      } else {
        sessionStorage.removeItem(ROLE_KEY);
      }
    } catch (e) {}
  }

  function getSelectedRole() {
    try {
      return sessionStorage.getItem(ROLE_KEY);
    } catch (e) {
      return null;
    }
  }

  function setSelectedTrade(trade) {
    try {
      if (trade) {
        sessionStorage.setItem(TRADE_KEY, trade);
      } else {
        sessionStorage.removeItem(TRADE_KEY);
      }
    } catch (e) {}
  }

  function getSelectedTrade() {
    try {
      return sessionStorage.getItem(TRADE_KEY);
    } catch (e) {
      return null;
    }
  }

  /** Stable id for ratings UI — real profile id when logged in as provider */
  function getEffectiveProviderId() {
    try {
      if (window.ArtisanAuth && typeof window.ArtisanAuth.getProviderProfileId === "function") {
        var id = ArtisanAuth.getProviderProfileId();
        if (id) return id;
      }
    } catch (e) {}
    return "local-session-provider";
  }

  function getAvatarDirty() {
    try {
      return sessionStorage.getItem(AVATAR_DIRTY_KEY) === "1";
    } catch (e) {
      return false;
    }
  }

  function setAvatarDirty(on) {
    try {
      if (on) sessionStorage.setItem(AVATAR_DIRTY_KEY, "1");
      else sessionStorage.removeItem(AVATAR_DIRTY_KEY);
    } catch (e) {}
  }

  /** Keep session + every on-page avatar img in sync (same tab, full page loads use sessionStorage). */
  function applyProviderAvatarEverywhere(url) {
    if (!url || typeof url !== "string") return;
    try {
      sessionStorage.setItem(AVATAR_KEY, url);
    } catch (e) {
      showToast("Photo is too large for browser storage. Try a smaller image.", "error");
      return;
    }
    var prev = $("#provider-avatar-preview");
    if (prev) prev.src = url;
    var pub = $("#public-provider-avatar");
    if (pub) pub.src = url;
    var pPhoto = $("#p-photo");
    if (pPhoto) pPhoto.src = url;
    try {
      window.dispatchEvent(new CustomEvent("artisan-provider-avatar-updated", { detail: { url: url } }));
    } catch (e) {}
  }

  /**
   * Downscale large camera photos before storing as data URL (keeps API body reasonable).
   * Falls back to the original data URL if canvas export fails.
   */
  function fileToResizedJpegDataUrl(file, maxEdge, quality, done) {
    maxEdge = maxEdge || 720;
    quality = typeof quality === "number" ? quality : 0.82;
    var reader = new FileReader();
    reader.onload = function (ev) {
      var dataUrl = ev.target && ev.target.result;
      if (!dataUrl || typeof dataUrl !== "string") {
        done(null);
        return;
      }
      var img = new Image();
      img.onload = function () {
        var w = img.naturalWidth || img.width;
        var h = img.naturalHeight || img.height;
        if (!w || !h) {
          done(dataUrl);
          return;
        }
        var scale = 1;
        if (w > maxEdge || h > maxEdge) scale = maxEdge / Math.max(w, h);
        var cw = Math.max(1, Math.round(w * scale));
        var ch = Math.max(1, Math.round(h * scale));
        var canvas = document.createElement("canvas");
        canvas.width = cw;
        canvas.height = ch;
        var ctx = canvas.getContext("2d");
        if (!ctx) {
          done(dataUrl);
          return;
        }
        ctx.drawImage(img, 0, 0, cw, ch);
        var out;
        try {
          out = canvas.toDataURL("image/jpeg", quality);
        } catch (e2) {
          out = null;
        }
        done(out || dataUrl);
      };
      img.onerror = function () {
        done(null);
      };
      img.src = dataUrl;
    };
    reader.onerror = function () {
      done(null);
    };
    reader.readAsDataURL(file);
  }

  /** Merge current server profile with a new photo URL (immediate sync for listings / other devices). */
  function persistProviderAvatarToServer(photoUrl, onComplete) {
    if (!window.ArtisanAuth || !ArtisanAuth.getMyProviderProfile || !ArtisanAuth.updateMyProviderProfile) {
      if (onComplete) onComplete(false);
      return;
    }
    if (!ArtisanAuth.getAccessToken || !ArtisanAuth.getAccessToken()) {
      if (onComplete) onComplete(false);
      return;
    }
    ArtisanAuth.getMyProviderProfile()
      .then(function (pr) {
        if (!pr || !pr.ok || !pr.data) return null;
        var d = pr.data;
        var wj = d.workPhotosJson;
        if (wj == null || wj === "") wj = "[]";
        if (typeof wj !== "string") {
          try {
            wj = JSON.stringify(wj);
          } catch (eJ) {
            wj = "[]";
          }
        }
        return ArtisanAuth.updateMyProviderProfile({
          displayName: (d.displayName || "").trim(),
          trade: d.trade || "",
          city: d.city || "",
          bio: d.bio || "",
          photoUrl: photoUrl,
          workPhotosJson: wj,
          priceAmount: d.priceAmount != null && d.priceAmount !== "" ? d.priceAmount : null,
          priceUnit: d.priceUnit || "hour",
          experienceYears: d.experienceYears != null ? d.experienceYears : null,
          visibleInSearch: d.searchable !== false,
        });
      })
      .then(function (res) {
        if (!res || !res.ok) {
          if (onComplete) onComplete(false);
          return;
        }
        var url = res.data && res.data.photoUrl ? res.data.photoUrl : photoUrl;
        if (url) applyProviderAvatarEverywhere(url);
        setAvatarDirty(false);
        if (onComplete) onComplete(true);
      })
      .catch(function () {
        if (onComplete) onComplete(false);
      });
  }

  /** Reject Arabic script in passwords (signup, login, reset). */
  var ARABIC_IN_PASSWORD_RE =
    /[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF]/;
  var PASSWORD_NO_ARABIC_MSG =
    "Password cannot contain Arabic characters. Use Latin letters, numbers, and symbols only.";

  function passwordContainsArabic(value) {
    return ARABIC_IN_PASSWORD_RE.test(value || "");
  }

  function stripArabicFromPassword(value) {
    return String(value || "").replace(ARABIC_IN_PASSWORD_RE, "");
  }

  function attachLatinPasswordInput(input) {
    if (!input) return;
    input.setAttribute("lang", "en");
    input.setAttribute("spellcheck", "false");
    input.addEventListener("input", function () {
      var cleaned = stripArabicFromPassword(input.value);
      if (cleaned !== input.value) input.value = cleaned;
    });
  }

  /** Toast notifications */
  function ensureToastContainer() {
    var c = $(".toast-container");
    if (!c) {
      c = document.createElement("div");
      c.className = "toast-container";
      c.setAttribute("aria-live", "polite");
      document.body.appendChild(c);
    }
    return c;
  }

  function showToast(message, type) {
    type = type || "success";
    var container = ensureToastContainer();
    var el = document.createElement("div");
    el.className = "toast toast--" + type;
    el.textContent = message;
    container.appendChild(el);
    container.classList.add("is-visible");
    setTimeout(function () {
      el.style.opacity = "0";
      el.style.transition = "opacity 0.25s ease";
      setTimeout(function () {
        el.remove();
        if (!container.children.length) {
          container.classList.remove("is-visible");
        }
      }, 280);
    }, 3200);
  }

  /** Loading overlay */
  function showLoading(message) {
    var existing = $("#global-loading");
    if (existing) {
      existing.classList.add("is-active");
      var t = existing.querySelector("[data-loading-text]");
      if (t && message) t.textContent = message;
      return;
    }
    var overlay = document.createElement("div");
    overlay.id = "global-loading";
    overlay.className = "loading-overlay is-active";
    overlay.innerHTML =
      '<div class="loading-card" role="status" aria-live="assertive">' +
      '<div class="loading-spinner" aria-hidden="true"></div>' +
      '<span data-loading-text>' +
      (message || "Please wait…") +
      "</span></div>";
    document.body.appendChild(overlay);
  }

  function hideLoading() {
    var o = $("#global-loading");
    if (o) {
      o.classList.remove("is-active");
      setTimeout(function () {
        if (o && o.parentNode) o.parentNode.removeChild(o);
      }, 300);
    }
  }

  /** Signup validation */
  function initSignupForm() {
    var form = $("#signup-form");
    if (!form) return;

    var role = getSelectedRole();
    var roleInput = $("#signup-role");
    var rolePill = $("#signup-role-pill");
    function syncRoleUi() {
      if (!roleInput) return;
      var v = roleInput.value;
      if (v === "customer" || v === "provider") {
        setSelectedRole(v);
        if (rolePill) {
          rolePill.style.display = "inline-flex";
          rolePill.textContent =
            v === "customer" ? "Signing up as a customer" : "Signing up as a service provider";
        }
      } else if (rolePill) {
        rolePill.style.display = "none";
      }
    }

    if (roleInput) {
      if (role === "customer" || role === "provider") {
        roleInput.value = role;
      }
      syncRoleUi();
      roleInput.addEventListener("change", syncRoleUi);
    }

    // Signup must start from role-selection page.
    if (!roleInput || !roleInput.value) {
      showToast("Please choose Customer or Provider first.", "error");
      setTimeout(function () {
        window.location.href = "role-selection.html";
      }, 1000);
      return;
    }

    function setError(el, msg) {
      var g = el && el.closest ? el.closest(".form-group") : el;
      if (!g) return;
      g.classList.add("has-error");
      var err = g.querySelector(".form-error");
      if (err) err.textContent = msg || "";
    }

    function clearError(el) {
      var g = el && el.closest ? el.closest(".form-group") : el;
      if (g) g.classList.remove("has-error");
    }

    function validateEmail(v) {
      return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v);
    }

    var pass = $("#signup-password");
    var pass2 = $("#signup-confirm");
    attachLatinPasswordInput(pass);
    attachLatinPasswordInput(pass2);

    form.addEventListener("submit", function (e) {
      e.preventDefault();
      $all(".form-group.has-error", form).forEach(function (g) {
        g.classList.remove("has-error");
      });

      var fullName = $("#signup-name");
      var email = $("#signup-email");
      var phone = $("#signup-phone");
      var pass = $("#signup-password");
      var pass2 = $("#signup-confirm");
      var agree = $("#signup-terms");

      var ok = true;
      if (!fullName.value.trim()) {
        setError(fullName, "Please enter your full name.");
        ok = false;
      }
      if (!validateEmail(email.value.trim())) {
        setError(email, "Enter a valid email address.");
        ok = false;
      }
      if (phone && phone.value.trim() && !/^[\d\s\-+()]{8,}$/.test(phone.value.trim())) {
        setError(phone, "Enter a valid phone number.");
        ok = false;
      }
      if (passwordContainsArabic(pass.value) || passwordContainsArabic(pass2.value)) {
        setError(pass, PASSWORD_NO_ARABIC_MSG);
        ok = false;
      }
      if (pass.value.length < 8) {
        setError(pass, "Password must be at least 8 characters.");
        ok = false;
      }
      if (pass.value !== pass2.value) {
        setError(pass2, "Passwords do not match.");
        ok = false;
      }
      if (agree && !agree.checked) {
        setError(agree, "You must accept the terms to continue.");
        ok = false;
      }
      if (!roleInput || !roleInput.value) {
        showToast("Please choose your role first.", "error");
        ok = false;
      }

      if (!ok) return;

      if (!window.ArtisanAuth || typeof ArtisanAuth.register !== "function") {
        showToast("API script missing. Include js/artisan-api.js before app.js.", "error");
        return;
      }

      showLoading("Creating your account…");
      var selectedRole = roleInput ? roleInput.value : "";
      ArtisanAuth.register({
        email: email.value.trim(),
        password: pass.value,
        fullName: fullName.value.trim(),
        phone: phone && phone.value.trim() ? phone.value.trim() : undefined,
        role: selectedRole,
      }).then(function (res) {
        hideLoading();
        if (!res.ok) {
          var msg =
            res.data && res.data.errors
              ? (Array.isArray(res.data.errors) ? res.data.errors.join(" ") : String(res.data.errors))
              : res.data && res.data.error
                ? String(res.data.error)
                : "Registration failed (" + res.status + ").";
          showToast(msg, "error");
          return;
        }
        var d = res.data;
        if (typeof ArtisanAuth.persistAuthResponse === "function") ArtisanAuth.persistAuthResponse(d);
        try {
          sessionStorage.setItem("artisan_provider_name", fullName.value.trim());
          if (selectedRole === "provider") setSelectedTrade("");
        } catch (e) {}
        showToast("Account created successfully.", "success");
        form.reset();
        if (roleInput) roleInput.value = role || "";
        setTimeout(function () {
          if (selectedRole === "provider") {
            window.location.href = "provider-dashboard.html";
          } else {
            var next = consumePostLoginRedirect();
            window.location.href = next || "customer-account.html";
          }
        }, 600);
      });
    });

    $all("input", form).forEach(function (inp) {
      inp.addEventListener("input", function () {
        var g = inp.closest(".form-group");
        if (g) g.classList.remove("has-error");
      });
    });
  }

  /** Login validation (demo) */
  function initLoginForm() {
    var form = $("#login-form");
    if (!form) return;

    var passInput = $("#login-password");
    attachLatinPasswordInput(passInput);

    form.addEventListener("submit", function (e) {
      e.preventDefault();
      var email = $("#login-email");
      var pass = $("#login-password");

      function err(el, msg) {
        var g = el.closest(".form-group");
        if (!g) return;
        g.classList.add("has-error");
        var fe = g.querySelector(".form-error");
        if (fe) fe.textContent = msg;
      }

      $all(".form-group.has-error", form).forEach(function (g) {
        g.classList.remove("has-error");
      });

      var ok = true;
      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value.trim())) {
        err(email, "Enter a valid email.");
        ok = false;
      }
      if (!pass.value) {
        err(pass, "Enter your password.");
        ok = false;
      } else if (passwordContainsArabic(pass.value)) {
        err(pass, PASSWORD_NO_ARABIC_MSG);
        ok = false;
      }
      if (!ok) return;

      if (!window.ArtisanAuth || typeof ArtisanAuth.login !== "function") {
        showToast("API script missing. Include js/artisan-api.js before app.js.", "error");
        return;
      }

      showLoading("Signing you in…");
      ArtisanAuth.login({
        email: email.value.trim(),
        password: pass.value,
      }).then(function (res) {
        hideLoading();
        if (!res.ok) {
          var failMsg =
            res.status === 403 && res.data && res.data.error
              ? res.data.error
              : (res.data && (res.data.error || res.data.title)) || "Invalid email or password.";
          showToast(failMsg, "error");
          return;
        }
        var d = res.data;
        if (typeof ArtisanAuth.persistAuthResponse === "function") ArtisanAuth.persistAuthResponse(d);
        showToast("Welcome back to Artisan.", "success");
        var r = String(d.role || d.Role || "").toLowerCase();
        setTimeout(function () {
          if (r === "admin") {
            window.location.href = "admin-dashboard.html";
          } else if (r === "provider") {
            window.location.href = "provider-dashboard.html";
          } else {
            var next = consumePostLoginRedirect();
            window.location.href = next || "customer-account.html";
          }
        }, 400);
      });
    });

    $all("input", form).forEach(function (inp) {
      inp.addEventListener("input", function () {
        var g = inp.closest(".form-group");
        if (g) g.classList.remove("has-error");
      });
    });
  }

  /** Role selection cards */
  function initRoleSelection() {
    var links = $all("[data-role]");
    links.forEach(function (link) {
      link.addEventListener("click", function () {
        var role = link.getAttribute("data-role");
        setSelectedRole(role);
      });
    });
  }

  function loadWorkPhotos() {
    try {
      var raw = sessionStorage.getItem(PORTFOLIO_KEY);
      if (!raw) return [];
      var arr = JSON.parse(raw);
      return Array.isArray(arr) ? arr : [];
    } catch (e) {
      return [];
    }
  }

  function saveWorkPhotos(arr) {
    try {
      sessionStorage.setItem(PORTFOLIO_KEY, JSON.stringify(arr));
    } catch (e) {
      showToast("Could not save all photos (storage may be full).", "error");
    }
  }

  function getRatingsMap() {
    try {
      var raw = localStorage.getItem(RATINGS_KEY);
      return raw ? JSON.parse(raw) : {};
    } catch (e) {
      return {};
    }
  }

  function setRatingsMap(map) {
    try {
      localStorage.setItem(RATINGS_KEY, JSON.stringify(map));
    } catch (e) {}
  }

  function getRatingStats(providerId) {
    if (!providerId) return { avg: null, count: 0 };
    var map = getRatingsMap();
    var entry = map[providerId];
    if (!entry || !entry.scores || !entry.scores.length) return { avg: null, count: 0 };
    var sum = entry.scores.reduce(function (a, b) {
      return a + b;
    }, 0);
    return { avg: sum / entry.scores.length, count: entry.scores.length };
  }

  function addProviderRating(providerId, score) {
    if (!providerId || score < 1 || score > 5) return false;
    var map = getRatingsMap();
    if (!map[providerId]) map[providerId] = { scores: [] };
    map[providerId].scores.push(score);
    map[providerId].updated = new Date().toISOString();
    setRatingsMap(map);
    if (window.ArtisanAuth && ArtisanAuth.getAccessToken && ArtisanAuth.postRating) {
      ArtisanAuth.postRating(providerId, score).catch(function () {});
    }
    try {
      window.dispatchEvent(new CustomEvent("artisan-ratings-updated", { detail: { providerId: providerId } }));
    } catch (e) {}
    return true;
  }

  function fillStarRow(container, avg, sizeClass) {
    if (!container) return;
    container.innerHTML = "";
    container.className = "star-row " + (sizeClass || "");
    var rounded = avg != null ? Math.round(avg) : 0;
    for (var i = 1; i <= 5; i++) {
      var s = document.createElement("span");
      s.className = "star" + (i <= rounded ? " star--filled" : "");
      s.setAttribute("aria-hidden", "true");
      s.textContent = "★";
      container.appendChild(s);
    }
  }

  /** Session price for demo — replace with API fields later */
  function getSessionProviderPriceInfo() {
    try {
      var raw = sessionStorage.getItem(PRICE_KEY);
      var unit = sessionStorage.getItem(PRICE_UNIT_KEY) || "hour";
      if (raw === null || String(raw).trim() === "") return { amount: null, unit: /^hour|day|week$/.test(unit) ? unit : "hour" };
      var n = parseFloat(String(raw).replace(",", "."));
      if (isNaN(n) || n < 0) return { amount: null, unit: /^hour|day|week$/.test(unit) ? unit : "hour" };
      return { amount: n, unit: /^hour|day|week$/.test(unit) ? unit : "hour" };
    } catch (e) {
      return { amount: null, unit: "hour" };
    }
  }

  function formatProviderPriceDisplay(amount, unit) {
    if (amount == null || isNaN(amount) || amount < 0) return "";
    var u = { hour: "hour", day: "day", week: "week" };
    var label = u[unit] || "hour";
    var num = amount % 1 === 0 ? String(Math.round(amount)) : amount.toFixed(2);
    return num + " JOD / " + label;
  }

  function updateProviderPricePreviewEl() {
    var pricePreview = $("#provider-price-preview");
    if (!pricePreview) return;
    var pi = getSessionProviderPriceInfo();
    var txt = formatProviderPriceDisplay(pi.amount, pi.unit);
    if (txt) {
      pricePreview.textContent = txt;
      pricePreview.classList.remove("is-empty");
    } else {
      pricePreview.textContent = "Rate not set — add your price in Profile Settings.";
      pricePreview.classList.add("is-empty");
    }
  }

  function getSessionProviderExperienceYears() {
    try {
      var raw = sessionStorage.getItem(EXPERIENCE_KEY);
      if (raw === null || String(raw).trim() === "") return null;
      var n = parseInt(String(raw).trim(), 10);
      if (isNaN(n) || n < 0) return null;
      return n;
    } catch (e) {
      return null;
    }
  }

  /** e.g. "Experience: +8 years" — visible on browse / public profile */
  function formatExperienceLine(years) {
    if (years == null || isNaN(years) || years < 0) return "";
    return "Experience: +" + years + " " + (years === 1 ? "year" : "years");
  }

  function updateProviderExperiencePreviewEl() {
    var el = $("#provider-experience-preview");
    if (!el) return;
    var y = getSessionProviderExperienceYears();
    var txt = formatExperienceLine(y);
    if (txt) {
      el.textContent = txt;
      el.classList.remove("is-empty");
    } else {
      el.textContent = "Experience not set — add it in Profile Settings.";
      el.classList.add("is-empty");
    }
  }

  function refreshDashboardYourRate() {
    var elStars = $("#provider-rate-stars");
    var elSum = $("#provider-rate-summary");
    if (!elStars) return;
    var stats = getRatingStats(getEffectiveProviderId());
    fillStarRow(elStars, stats.avg, "star-row--readonly");
    if (elSum) {
      elSum.textContent = stats.count
        ? stats.avg.toFixed(1) + " · " + stats.count + " review" + (stats.count === 1 ? "" : "s")
        : "No ratings yet";
    }
  }

  /** Dashboard overview (provider-dashboard.html): header preview + your rate — no profile form on this page */
  function initProviderDashboardOverview() {
    var nameDisplay = $("#provider-display-name");
    if (!nameDisplay) return;

    var badge = $("#provider-trade-badge");
    var bioPreview = $("#provider-bio-preview");
    var avatarPreview = $("#provider-avatar-preview");

    var storedName = "";
    var storedBio = "";
    try {
      storedName = sessionStorage.getItem("artisan_provider_name") || "";
      storedBio = sessionStorage.getItem("artisan_provider_bio") || "";
    } catch (e) {}

    var currentTrade = getSelectedTrade();
    if (storedName) nameDisplay.textContent = storedName;

    if (badge) {
      badge.textContent = currentTrade || "Trade not selected";
      badge.classList.toggle("is-empty", !currentTrade);
    }

    if (bioPreview) {
      if (storedBio) {
        bioPreview.textContent = storedBio;
        bioPreview.classList.remove("is-empty");
      } else {
        bioPreview.textContent = "Add a short professional bio so customers can quickly trust your expertise.";
        bioPreview.classList.add("is-empty");
      }
    }

    try {
      var storedAvatar = sessionStorage.getItem(AVATAR_KEY) || "";
      if (storedAvatar && avatarPreview) avatarPreview.src = storedAvatar;
    } catch (e) {}

    updateProviderPricePreviewEl();
    updateProviderExperiencePreviewEl();

    refreshDashboardYourRate();
    window.addEventListener("artisan-ratings-updated", refreshDashboardYourRate);
    window.addEventListener("storage", function (e) {
      if (e.key === RATINGS_KEY) refreshDashboardYourRate();
    });
    window.addEventListener("artisan-provider-avatar-updated", function (ev) {
      var u = ev && ev.detail && ev.detail.url;
      if (u && avatarPreview) avatarPreview.src = u;
    });

    (function syncOverviewFromApi() {
      if (!window.ArtisanAuth || !ArtisanAuth.getAccessToken || !ArtisanAuth.getMyProviderProfile) return;
      if (!ArtisanAuth.getAccessToken()) return;
      ArtisanAuth.getMyProviderProfile().then(function (res) {
        if (!res.ok || !res.data) return;
        var d = res.data;
        var skipPhoto = getAvatarDirty();
        try {
          sessionStorage.setItem("artisan_provider_name", d.displayName || "");
          sessionStorage.setItem(TRADE_KEY, d.trade || "");
          sessionStorage.setItem("artisan_provider_bio", d.bio || "");
          sessionStorage.setItem(SEARCH_VISIBLE_KEY, d.searchable !== false ? "true" : "false");
          if (d.id) ArtisanAuth.setProviderProfileId(d.id);
          if (d.photoUrl && !skipPhoto) sessionStorage.setItem(AVATAR_KEY, d.photoUrl);
          if (d.workPhotosJson) sessionStorage.setItem(PORTFOLIO_KEY, d.workPhotosJson);
          if (d.priceAmount != null && d.priceAmount !== "") {
            sessionStorage.setItem(PRICE_KEY, String(d.priceAmount));
            sessionStorage.setItem(PRICE_UNIT_KEY, d.priceUnit || "hour");
          } else {
            sessionStorage.removeItem(PRICE_KEY);
          }
          if (d.experienceYears != null) sessionStorage.setItem(EXPERIENCE_KEY, String(d.experienceYears));
          else sessionStorage.removeItem(EXPERIENCE_KEY);
        } catch (e) {}
        if (nameDisplay && d.displayName) nameDisplay.textContent = d.displayName;
        if (badge) {
          badge.textContent = d.trade || "Trade not selected";
          badge.classList.toggle("is-empty", !d.trade);
        }
        if (bioPreview && d.bio) {
          bioPreview.textContent = d.bio;
          bioPreview.classList.remove("is-empty");
        }
        if (avatarPreview && d.photoUrl && !skipPhoto) avatarPreview.src = d.photoUrl;
        updateProviderPricePreviewEl();
        updateProviderExperiencePreviewEl();
        refreshDashboardYourRate();
      });
    })();
  }

  /** Provider dashboard profile logic */
  function initProviderDashboard() {
    var form = $("#provider-profile-form");
    var nameInput = $("#provider-name");
    var select = $("#provider-trade-select");
    var bioInput = $("#provider-bio");
    var citySelect = $("#provider-governorate");
    var avatarInput = $("#provider-avatar-input");
    var avatarPreview = $("#provider-avatar-preview");
    var nameDisplay = $("#provider-display-name");
    var badge = $("#provider-trade-badge");
    var bioPreview = $("#provider-bio-preview");
    var workInput = $("#provider-work-input");
    var workGallery = $("#provider-work-gallery");
    var priceInput = $("#provider-price");
    var unitSelect = $("#provider-price-unit");
    var experienceInput = $("#provider-experience");
    if (!form || !nameInput || !select || !bioInput) return;

    var workPhotos = loadWorkPhotos();

    var storedName = "";
    var storedBio = "";
    try {
      storedName = sessionStorage.getItem("artisan_provider_name") || "";
      storedBio = sessionStorage.getItem("artisan_provider_bio") || "";
    } catch (e) {}

    var currentTrade = getSelectedTrade();
    if (currentTrade) select.value = currentTrade;
    try {
      var storedAvatar = sessionStorage.getItem(AVATAR_KEY) || "";
      if (storedAvatar && avatarPreview) avatarPreview.src = storedAvatar;
    } catch (e) {}
    if (storedName) {
      nameInput.value = storedName;
      if (nameDisplay) nameDisplay.textContent = storedName;
    }
    if (storedBio) {
      bioInput.value = storedBio;
      if (bioPreview) {
        bioPreview.textContent = storedBio;
        bioPreview.classList.remove("is-empty");
      }
    }
    if (citySelect) {
      var storedCity = "";
      try {
        storedCity = sessionStorage.getItem(CITY_KEY) || "";
      } catch (e) {}
      if (storedCity && citySelect.querySelector('option[value="' + storedCity + '"]')) {
        citySelect.value = storedCity;
      }
    }

    function renderTrade(saveToStorage) {
      var value = select.value || "";
      if (saveToStorage !== false) setSelectedTrade(value);
      if (badge) {
        badge.textContent = value || "Trade not selected";
        badge.classList.toggle("is-empty", !value);
      }
    }

    function renderBio() {
      if (!bioPreview) return;
      var text = bioInput.value.trim();
      if (!text) {
        bioPreview.textContent = "Add a short professional bio so customers can quickly trust your expertise.";
        bioPreview.classList.add("is-empty");
      } else {
        bioPreview.textContent = text;
        bioPreview.classList.remove("is-empty");
      }
    }

    renderTrade(false);
    renderBio();

    if (priceInput && unitSelect) {
      var pinfo = getSessionProviderPriceInfo();
      priceInput.value = pinfo.amount != null ? String(pinfo.amount) : "";
      unitSelect.value = pinfo.unit || "hour";
    }

    if (experienceInput) {
      var expY = getSessionProviderExperienceYears();
      experienceInput.value = expY != null ? String(expY) : "";
    }

    function renderWorkGallery() {
      if (!workGallery) return;
      workGallery.innerHTML = "";
      workPhotos.forEach(function (dataUrl, index) {
        var item = document.createElement("div");
        item.className = "provider-work-gallery__item";
        var img = document.createElement("img");
        img.src = dataUrl;
        img.alt = "Work sample " + (index + 1);
        img.loading = "lazy";
        var rm = document.createElement("button");
        rm.type = "button";
        rm.className = "provider-work-gallery__remove";
        rm.setAttribute("aria-label", "Remove photo");
        rm.textContent = "×";
        rm.addEventListener("click", function () {
          workPhotos.splice(index, 1);
          saveWorkPhotos(workPhotos);
          renderWorkGallery();
          showToast("Photo removed.", "success");
        });
        item.appendChild(img);
        item.appendChild(rm);
        workGallery.appendChild(item);
      });
    }

    function applyDetailFromApi(d) {
      if (!d) return;
      var skipPhoto = getAvatarDirty();
      try {
        sessionStorage.setItem("artisan_provider_name", d.displayName || "");
        sessionStorage.setItem(TRADE_KEY, d.trade || "");
        sessionStorage.setItem("artisan_provider_bio", d.bio || "");
        sessionStorage.setItem(CITY_KEY, d.city || "");
        sessionStorage.setItem(SEARCH_VISIBLE_KEY, d.searchable !== false ? "true" : "false");
        if (d.id && window.ArtisanAuth) ArtisanAuth.setProviderProfileId(d.id);
        if (d.photoUrl && !skipPhoto) sessionStorage.setItem(AVATAR_KEY, d.photoUrl);
        if (d.workPhotosJson) sessionStorage.setItem(PORTFOLIO_KEY, d.workPhotosJson);
        if (d.priceAmount != null && d.priceAmount !== "") {
          sessionStorage.setItem(PRICE_KEY, String(d.priceAmount));
          sessionStorage.setItem(PRICE_UNIT_KEY, d.priceUnit || "hour");
        } else {
          sessionStorage.removeItem(PRICE_KEY);
        }
        if (d.experienceYears != null) sessionStorage.setItem(EXPERIENCE_KEY, String(d.experienceYears));
        else sessionStorage.removeItem(EXPERIENCE_KEY);
      } catch (e) {}
      nameInput.value = (d.displayName || "").trim();
      bioInput.value = (d.bio || "").trim();
      if (d.trade) select.value = d.trade;
      if (citySelect && d.city && citySelect.querySelector('option[value="' + d.city + '"]')) {
        citySelect.value = d.city;
      }
      renderTrade(false);
      renderBio();
      if (avatarPreview && d.photoUrl && !skipPhoto) avatarPreview.src = d.photoUrl;
      workPhotos = loadWorkPhotos();
      renderWorkGallery();
      if (priceInput && unitSelect) {
        priceInput.value = d.priceAmount != null ? String(d.priceAmount) : "";
        unitSelect.value = d.priceUnit || "hour";
      }
      if (experienceInput) {
        experienceInput.value = d.experienceYears != null ? String(d.experienceYears) : "";
      }
      if (nameDisplay && d.displayName) nameDisplay.textContent = d.displayName;
      updateProviderPricePreviewEl();
      updateProviderExperiencePreviewEl();
    }

    if (window.ArtisanAuth && ArtisanAuth.getAccessToken && ArtisanAuth.getMyProviderProfile && ArtisanAuth.getAccessToken()) {
      ArtisanAuth.getMyProviderProfile().then(function (res) {
        if (res.ok && res.data) applyDetailFromApi(res.data);
      });
    }

    renderWorkGallery();

    var profileSaveInFlight = false;

    if (workInput && workGallery) {
      workInput.addEventListener("change", function () {
        var files = workInput.files;
        if (!files || !files.length) return;

        var remaining = MAX_WORK_PHOTOS - workPhotos.length;
        if (remaining <= 0) {
          showToast("Maximum " + MAX_WORK_PHOTOS + " work photos.", "error");
          workInput.value = "";
          return;
        }

        var toAdd = [].slice.call(files, 0, remaining);
        var total = toAdd.length;
        var done = 0;
        var invalidAny = false;
        if (!total) {
          workInput.value = "";
          return;
        }

        function tryFinish() {
          done++;
          if (done < total) return;
          saveWorkPhotos(workPhotos);
          renderWorkGallery();
          if (invalidAny) showToast("Some files were skipped (images only).", "error");
          else showToast("Work photos updated.", "success");
          workInput.value = "";
        }

        toAdd.forEach(function (file) {
          if (!/^image\//.test(file.type)) {
            invalidAny = true;
            tryFinish();
            return;
          }
          fileToResizedJpegDataUrl(file, 1280, 0.78, function (result) {
            if (result) workPhotos.push(result);
            else invalidAny = true;
            tryFinish();
          });
        });
      });
    }

    if (avatarInput) {
      avatarInput.addEventListener("change", function () {
        var file = avatarInput.files && avatarInput.files[0];
        if (!file) return;

        if (!/^image\//.test(file.type)) {
          showToast("Please choose a valid image file.", "error");
          avatarInput.value = "";
          return;
        }

        fileToResizedJpegDataUrl(file, 720, 0.82, function (result) {
          if (!result) {
            showToast("Could not read that image. Try another file.", "error");
            avatarInput.value = "";
            return;
          }
          setAvatarDirty(true);
          applyProviderAvatarEverywhere(result);
          if (avatarPreview) avatarPreview.src = result;
          showToast("Profile photo updated.", "success");
          persistProviderAvatarToServer(result, function (ok) {
            if (!ok) {
              showToast("Saved here; use Save profile if the server did not sync.", "error");
            }
          });
          avatarInput.value = "";
        });
      });
    }

    form.addEventListener("submit", function (e) {
      e.preventDefault();
      if (profileSaveInFlight) return;

      var cleanName = nameInput.value.trim();
      if (!cleanName) {
        showToast("Please enter your full name before saving.", "error");
        return;
      }

      if (nameDisplay) nameDisplay.textContent = cleanName;
      renderTrade(true);
      renderBio();

      try {
        sessionStorage.setItem("artisan_provider_name", cleanName);
        sessionStorage.setItem("artisan_provider_bio", bioInput.value.trim());
        if (citySelect) sessionStorage.setItem(CITY_KEY, citySelect.value || "");
        saveWorkPhotos(workPhotos);
        if (priceInput && unitSelect) {
          var pv = priceInput.value.trim();
          var un = unitSelect.value || "hour";
          sessionStorage.setItem(PRICE_UNIT_KEY, un);
          if (pv === "") {
            sessionStorage.removeItem(PRICE_KEY);
          } else {
            var pn = parseFloat(pv.replace(",", "."));
            if (isNaN(pn) || pn < 0) {
              showToast("Enter a valid price (0 or greater), or leave the field empty.", "error");
              return;
            }
            sessionStorage.setItem(PRICE_KEY, String(pn));
          }
        }
        if (experienceInput) {
          var ex = experienceInput.value.trim();
          if (ex === "") {
            sessionStorage.removeItem(EXPERIENCE_KEY);
          } else {
            var ey = parseInt(ex, 10);
            if (isNaN(ey) || ey < 0 || ey > 80) {
              showToast("Enter experience in years (0–80), or leave the field empty.", "error");
              return;
            }
            sessionStorage.setItem(EXPERIENCE_KEY, String(ey));
          }
        }
      } catch (err) {}

      updateProviderPricePreviewEl();
      updateProviderExperiencePreviewEl();

      var vis = sessionStorage.getItem(SEARCH_VISIBLE_KEY) !== "false";
      var payload = {
        displayName: cleanName,
        trade: select.value || "",
        city: citySelect ? citySelect.value || "" : "",
        bio: bioInput.value.trim(),
        photoUrl: null,
        workPhotosJson: JSON.stringify(workPhotos),
        priceUnit: unitSelect ? unitSelect.value || "hour" : "hour",
        experienceYears: null,
        visibleInSearch: vis,
      };
      try {
        payload.photoUrl = sessionStorage.getItem(AVATAR_KEY);
      } catch (e) {}
      if (priceInput && priceInput.value.trim() !== "") {
        var pnx = parseFloat(priceInput.value.replace(",", "."));
        payload.priceAmount = !isNaN(pnx) && pnx >= 0 ? pnx : null;
      } else {
        payload.priceAmount = null;
      }
      if (experienceInput && experienceInput.value.trim() !== "") {
        var exy = parseInt(experienceInput.value.trim(), 10);
        payload.experienceYears = !isNaN(exy) ? exy : null;
      }

      if (window.ArtisanAuth && ArtisanAuth.updateMyProviderProfile && ArtisanAuth.getAccessToken()) {
        profileSaveInFlight = true;
        showLoading("Saving profile…");
        ArtisanAuth.updateMyProviderProfile(payload).then(function (res) {
          hideLoading();
          if (!res.ok) {
            var detail = "";
            try {
              if (res.data && (res.data.error || res.data.title)) detail = " " + String(res.data.error || res.data.title);
            } catch (eD) {}
            showToast("Could not save profile to server (" + res.status + ")." + detail, "error");
            return;
          }
          if (res.data && res.data.id) ArtisanAuth.setProviderProfileId(res.data.id);
          setAvatarDirty(false);
          if (res.data && res.data.photoUrl) {
            applyProviderAvatarEverywhere(res.data.photoUrl);
            if (avatarPreview) avatarPreview.src = res.data.photoUrl;
          }
          showToast(
            nameDisplay || badge || bioPreview ? "Profile saved. Header updated instantly." : "Profile saved.",
            "success"
          );
        }).finally(function () {
          profileSaveInFlight = false;
        });
      } else {
        showToast(
          nameDisplay || badge || bioPreview ? "Profile saved. Header updated instantly." : "Profile saved.",
          "success"
        );
      }
    });
  }

  /** Provider dashboard: show profile in customer search (on/off) */
  function initProviderSearchVisibility() {
    var btn = $("#provider-availability-toggle");
    if (!btn) return;
    var busy = false;

    function readVisible() {
      try {
        if (sessionStorage.getItem(SEARCH_VISIBLE_KEY) === "false") return false;
        return true;
      } catch (e) {
        return true;
      }
    }

    function writeVisible(on) {
      try {
        sessionStorage.setItem(SEARCH_VISIBLE_KEY, on ? "true" : "false");
      } catch (e) {}
    }

    function syncUi(on) {
      btn.setAttribute("aria-checked", on ? "true" : "false");
      btn.classList.toggle("is-off", !on);
      var t = btn.querySelector(".availability-switch__text");
      if (t) t.textContent = on ? "On" : "Off";
    }

    function setBtnEnabled(on) {
      btn.disabled = !on;
      btn.setAttribute("aria-disabled", on ? "false" : "true");
    }

    function hasAuthToken() {
      try {
        return !!(window.ArtisanAuth && ArtisanAuth.getAccessToken && ArtisanAuth.getAccessToken());
      } catch (e) {
        return false;
      }
    }

    syncUi(readVisible());
    setBtnEnabled(false);

    if (hasAuthToken() && window.ArtisanAuth && ArtisanAuth.getMyProviderProfile) {
      ArtisanAuth.getMyProviderProfile().then(function (pr) {
        if (!pr || !pr.ok || !pr.data) {
          setBtnEnabled(false);
          return;
        }
        var d = pr.data;
        var on = d.visibleInSearch !== false;
        writeVisible(on);
        syncUi(on);
        setBtnEnabled(true);
      });
    } else {
      setBtnEnabled(false);
    }

    btn.addEventListener("click", function () {
      if (busy || btn.disabled) return;
      var next = !readVisible();
      var prev = !next;
      writeVisible(next);
      syncUi(next);
      if (!(window.ArtisanAuth && ArtisanAuth.getAccessToken && ArtisanAuth.getMyProviderProfile && ArtisanAuth.updateMyProviderProfile && ArtisanAuth.getAccessToken())) {
        writeVisible(prev);
        syncUi(prev);
        showToast("You need to be logged in as a provider.", "error");
        return;
      }

      busy = true;
      setBtnEnabled(false);
      ArtisanAuth.getMyProviderProfile()
        .then(function (pr) {
          if (!pr || !pr.ok || !pr.data) throw new Error("profile_load_failed");
          var d = pr.data;
          return ArtisanAuth.updateMyProviderProfile({
            displayName: d.displayName || "",
            trade: d.trade || "",
            city: d.city || "",
            bio: d.bio || "",
            photoUrl: d.photoUrl || null,
            workPhotosJson: d.workPhotosJson || d.WorkPhotosJson || "[]",
            priceAmount: d.priceAmount,
            priceUnit: d.priceUnit || "hour",
            experienceYears: d.experienceYears,
            visibleInSearch: next,
          });
        })
        .then(function (res) {
          if (!res || !res.ok) throw new Error("profile_save_failed");
          showToast(
            next ? "You are available — customers can find your profile." : "You are unavailable — your profile is hidden from search.",
            "success"
          );
        })
        .catch(function () {
          writeVisible(prev);
          syncUi(prev);
          showToast("Could not update availability now. Please try again.", "error");
        })
        .finally(function () {
          busy = false;
          setBtnEnabled(hasAuthToken());
        });
    });
  }

  /** Public provider profile page — show stored name, trade, bio, avatar, portfolio */
  function initProviderPublicProfile() {
    var root = $("#provider-public-root");
    if (!root) return;

    try {
      var sp = new URLSearchParams(window.location.search);
      var urlPn = sp.get("pn");
      if (urlPn && urlPn.trim()) {
        sessionStorage.setItem("artisan_chat_peer_provider_name", urlPn.trim());
      }
    } catch (ePeer) {}

    var nameEl = $("#public-provider-name");
    var tradeEl = $("#public-provider-trade");
    var bioEl = $("#public-provider-bio");
    var avatarEl = $("#public-provider-avatar");
    var portfolioEl = $("#public-provider-portfolio");

    try {
      var name = sessionStorage.getItem("artisan_provider_name") || "Service provider";
      var bio = sessionStorage.getItem("artisan_provider_bio") || "";
      var trade = sessionStorage.getItem(TRADE_KEY) || "";
      var avatar = sessionStorage.getItem(AVATAR_KEY) || "";
      var photos = loadWorkPhotos();

      if (nameEl) nameEl.textContent = name;
      if (tradeEl) tradeEl.textContent = trade || "Trade not set";
      if (bioEl) {
        bioEl.textContent = bio || "No bio yet.";
        bioEl.classList.toggle("is-empty", !bio);
      }
      if (avatarEl) {
        if (avatar) avatarEl.src = avatar;
        avatarEl.alt = name + " — profile photo";
      }

      if (portfolioEl) {
        portfolioEl.innerHTML = "";
        if (!photos.length) {
          portfolioEl.innerHTML =
            '<p class="public-portfolio-empty">No work photos yet. The provider can add samples from their dashboard.</p>';
        } else {
          var grid = document.createElement("div");
          grid.className = "public-portfolio-grid";
          photos.forEach(function (url, i) {
            var a = document.createElement("a");
            a.href = url;
            a.target = "_blank";
            a.rel = "noopener noreferrer";
            a.className = "public-portfolio-item";
            var img = document.createElement("img");
            img.src = url;
            img.alt = "Work sample " + (i + 1);
            img.loading = "lazy";
            a.appendChild(img);
            grid.appendChild(a);
          });
          portfolioEl.appendChild(grid);
        }
      }

      var providerId =
        new URLSearchParams(window.location.search).get("id") || getEffectiveProviderId();
      var stats = getRatingStats(providerId);
      var pubStars = $("#public-provider-stars");
      var pubText = $("#public-provider-rating-text");
      if (pubStars) {
        fillStarRow(pubStars, stats.avg, "star-row--readonly star-row--public");
      }
      if (pubText) {
        pubText.textContent = stats.count
          ? stats.avg.toFixed(1) + " (" + stats.count + ")"
          : "No ratings yet";
      }

      var priceEl = $("#public-provider-price");
      if (priceEl) {
        var pInfo = getSessionProviderPriceInfo();
        var pTxt = formatProviderPriceDisplay(pInfo.amount, pInfo.unit);
        priceEl.textContent = pTxt || "Not set";
      }

      var expEl = $("#public-provider-experience");
      if (expEl) {
        var expY = getSessionProviderExperienceYears();
        expEl.textContent = formatExperienceLine(expY) || "Not set";
      }
    } catch (e) {}
  }

  /** Home hero AI placeholder animation */
  function initHeroSearchPlaceholder() {
    var input = $("#ai-search-input");
    if (!input) return;

    var prompts = [
      "Ask Artisan AI... e.g., 'Find a custom furniture maker'",
      "Ask Artisan AI... e.g., 'Need a carpenter for built-in shelves'",
      "Ask Artisan AI... e.g., 'Book an electrician near me today'",
    ];
    var i = 0;
    input.placeholder = prompts[i];
    setInterval(function () {
      i = (i + 1) % prompts.length;
      input.placeholder = prompts[i];
    }, 2800);
  }

  document.documentElement.classList.add("js");

  try {
    if (shouldClearAuthFromLogoutQuery() && window.ArtisanAuth) {
      ArtisanAuth.clearAuth();
    }
  } catch (eLogout) {}

  initNav();
  initGoogleSignInOnAuthPages();
  initCustomerAccountPage();
  initIndexAuthNav();
  initAdminDashboardPage();
  initSignupForm();
  initLoginForm();
  initRoleSelection();
  initProviderDashboardOverview();
  initProviderDashboard();
  initProviderSearchVisibility();
  initProviderPublicProfile();
  initHeroSearchPlaceholder();

  window.ArtisanUI = {
    showToast: showToast,
    showLoading: showLoading,
    hideLoading: hideLoading,
    passwordContainsArabic: passwordContainsArabic,
    attachLatinPasswordInput: attachLatinPasswordInput,
    PASSWORD_NO_ARABIC_MSG: PASSWORD_NO_ARABIC_MSG,
    getSelectedRole: getSelectedRole,
    setSelectedRole: setSelectedRole,
    getSelectedTrade: getSelectedTrade,
    setSelectedTrade: setSelectedTrade,
    getProviderRating: getRatingStats,
    addProviderRating: addProviderRating,
    getSessionProviderId: getEffectiveProviderId,
    getSessionProviderPrice: getSessionProviderPriceInfo,
    formatProviderPriceDisplay: formatProviderPriceDisplay,
    getSessionProviderExperienceYears: getSessionProviderExperienceYears,
    formatExperienceLine: formatExperienceLine,
    initBookingNotificationCenter: initBookingNotificationCenter,
  };

  window.addEventListener("artisan-booking-response", function (ev) {
    var detail = ev && ev.detail;
    if (!detail || typeof detail !== "object") return;
    var name = String(detail.providerDisplayName || "").trim() || "The provider";
    var st = String(detail.status || "").toLowerCase();
    if (st === "accepted") {
      showToast(name + " accepted your booking request.", "success");
    } else if (st === "rejected") {
      showToast(name + " declined this booking. You can choose another provider anytime.", "info");
    }
  });
})();
