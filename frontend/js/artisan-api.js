/**
 * Artisan backend API — JWT auth + JSON helpers (Backend/ArtisanApi).
 *
 * New pages: include api-config.js + this file before calling the API.
 * Use apiJson("/api/...", { method, body }) or the helpers below.
 */
(function () {
  "use strict";

  var TOKEN_KEY = "artisan_access_token";
  var USER_KEY = "artisan_auth_user";
  var PROFILE_ID_KEY = "artisan_provider_profile_id";

  function baseUrl() {
    return window.ARTISAN_API_BASE || "http://localhost:5172";
  }

  function getAccessToken() {
    try {
      return localStorage.getItem(TOKEN_KEY);
    } catch (e) {
      return null;
    }
  }

  function setAccessToken(token) {
    try {
      if (token) localStorage.setItem(TOKEN_KEY, token);
      else localStorage.removeItem(TOKEN_KEY);
    } catch (e) {}
  }

  function getStoredUser() {
    try {
      var raw = localStorage.getItem(USER_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch (e) {
      return null;
    }
  }

  function setStoredUser(obj) {
    try {
      if (obj) localStorage.setItem(USER_KEY, JSON.stringify(obj));
      else localStorage.removeItem(USER_KEY);
    } catch (e) {}
  }

  function setProviderProfileId(id) {
    try {
      if (id) sessionStorage.setItem(PROFILE_ID_KEY, id);
      else sessionStorage.removeItem(PROFILE_ID_KEY);
    } catch (e) {}
  }

  function getProviderProfileId() {
    try {
      return sessionStorage.getItem(PROFILE_ID_KEY);
    } catch (e) {
      return null;
    }
  }

  function clearAuth() {
    setAccessToken(null);
    setStoredUser(null);
    setProviderProfileId(null);
  }

  function apiJson(path, options) {
    options = options || {};
    var headers = options.headers || {};
    if (!headers["Content-Type"] && options.body && typeof options.body === "string") {
      headers["Content-Type"] = "application/json";
    }
    var token = getAccessToken();
    if (token && options.auth !== false) {
      headers["Authorization"] = "Bearer " + token;
    }
    return fetch(baseUrl() + path, {
      method: options.method || "GET",
      headers: headers,
      body: options.body,
      credentials: "omit",
    }).then(function (r) {
      var p = r.json().catch(function () {
        return null;
      });
      return p.then(function (data) {
        return { ok: r.ok, status: r.status, data: data };
      });
    });
  }

  function register(payload) {
    return apiJson("/api/auth/register", {
      method: "POST",
      auth: false,
      body: JSON.stringify(payload),
    });
  }

  function login(payload) {
    return apiJson("/api/auth/login", {
      method: "POST",
      auth: false,
      body: JSON.stringify(payload),
    });
  }

  function forgotPasswordRequest(payload) {
    return apiJson("/api/auth/forgot-password/request", {
      method: "POST",
      auth: false,
      body: JSON.stringify(payload),
    });
  }

  function forgotPasswordVerifyOtp(payload) {
    return apiJson("/api/auth/forgot-password/verify-otp", {
      method: "POST",
      auth: false,
      body: JSON.stringify(payload),
    });
  }

  function forgotPasswordReset(payload) {
    return apiJson("/api/auth/forgot-password/reset", {
      method: "POST",
      auth: false,
      body: JSON.stringify(payload),
    });
  }

  /** Persist JWT + user snapshot after login, register, or Google sign-in. */
  function persistAuthResponse(data) {
    if (!data) return;
    var token = data.accessToken != null ? data.accessToken : data.AccessToken;
    var email = data.email != null ? data.email : data.Email;
    var fullName = data.fullName != null ? data.fullName : data.FullName;
    var role = data.role != null ? data.role : data.Role;
    var profileId = data.providerProfileId != null ? data.providerProfileId : data.ProviderProfileId;
    setAccessToken(token || null);
    setStoredUser({
      email: email,
      fullName: fullName,
      role: role,
      providerProfileId: profileId,
    });
    if (profileId) setProviderProfileId(profileId);
    applyAuthChrome();
  }

  /** POST Google GIS credential JWT; optional role only for new accounts (customer | provider). */
  function signInWithGoogle(idToken, options) {
    options = options || {};
    var body = { idToken: idToken };
    if (options.role === "customer" || options.role === "provider") body.role = options.role;
    return apiJson("/api/auth/google", {
      method: "POST",
      auth: false,
      body: JSON.stringify(body),
    });
  }

  function me() {
    return apiJson("/api/auth/me", { method: "GET" });
  }

  function updateMyAccount(payload) {
    return apiJson("/api/me/account", {
      method: "PUT",
      body: JSON.stringify(payload),
    });
  }

  function getMyProviderProfile() {
    return apiJson("/api/me/provider-profile", { method: "GET" });
  }

  function updateMyProviderProfile(payload) {
    return apiJson("/api/me/provider-profile", {
      method: "PUT",
      body: JSON.stringify(payload),
    });
  }

  function postRating(providerId, score) {
    return apiJson("/api/providers/" + encodeURIComponent(providerId) + "/ratings", {
      method: "POST",
      body: JSON.stringify({ score: score }),
    });
  }

  function sendMessage(payload) {
    return apiJson("/api/me/messages", {
      method: "POST",
      body: JSON.stringify(payload),
    });
  }

  /** Multipart upload for chat thread; optional caption uses current message text on client. */
  function uploadChatAttachment(partnerUserId, file, caption) {
    return new Promise(function (resolve) {
      var token = getAccessToken();
      if (!token) {
        resolve({ ok: false, status: 401, data: { error: "Not signed in." } });
        return;
      }
      var fd = new FormData();
      fd.append("file", file);
      if (caption) fd.append("caption", caption);
      fetch(
        baseUrl() + "/api/me/chats/" + encodeURIComponent(String(partnerUserId)) + "/attachments",
        {
          method: "POST",
          headers: { Authorization: "Bearer " + token },
          body: fd,
        }
      )
        .then(function (r) {
          return r
            .json()
            .catch(function () {
              return null;
            })
            .then(function (data) {
              resolve({ ok: r.ok, status: r.status, data: data });
            });
        })
        .catch(function (e) {
          resolve({
            ok: false,
            status: 0,
            data: { error: e && e.message ? String(e.message) : "Network error." },
          });
        });
    });
  }

  /** Customer: create a booking request (saves row + sends same text as first chat message). */
  function createServiceRequest(payload) {
    return apiJson("/api/me/service-requests", {
      method: "POST",
      body: JSON.stringify({
        providerProfileId: payload.providerProfileId,
        body: payload.body,
      }),
    });
  }

  /** Customer: accepted/rejected booking updates for the notification center. */
  function getCustomerBookingNotifications() {
    return apiJson("/api/me/customer/booking-notifications", { method: "GET" });
  }

  /** Provider: list incoming booking requests. */
  function getProviderServiceRequests() {
    return apiJson("/api/me/provider/service-requests", { method: "GET" });
  }

  /** Provider: accept or reject a pending request (id = GUID string). */
  function updateServiceRequestStatus(requestId, status) {
    return apiJson("/api/me/provider/service-requests/" + encodeURIComponent(requestId), {
      method: "PATCH",
      body: JSON.stringify({ status: status }),
    });
  }

  /** If no JWT, redirect to login. Returns true if logged in. */
  function ensureLoggedIn() {
    if (getAccessToken()) return true;
    try {
      window.location.href = "login.html";
    } catch (e) {}
    return false;
  }

  function adminListUsers(params) {
    params = params || {};
    var q = new URLSearchParams();
    if (params.q) q.set("q", params.q);
    if (params.page) q.set("page", String(params.page));
    if (params.pageSize) q.set("pageSize", String(params.pageSize));
    var qs = q.toString();
    return apiJson("/api/admin/users" + (qs ? "?" + qs : ""), { method: "GET" });
  }

  function adminBlockUser(userId) {
    return apiJson("/api/admin/users/" + encodeURIComponent(userId) + "/block", { method: "POST", body: "{}" });
  }

  function adminUnblockUser(userId) {
    return apiJson("/api/admin/users/" + encodeURIComponent(userId) + "/unblock", { method: "POST", body: "{}" });
  }

  function adminAddModerator(userId) {
    return apiJson("/api/admin/users/" + encodeURIComponent(userId) + "/roles/moderator", { method: "POST", body: "{}" });
  }

  function adminRemoveModerator(userId) {
    return apiJson("/api/admin/users/" + encodeURIComponent(userId) + "/roles/moderator", { method: "DELETE" });
  }

  /**
   * Show elements marked data-auth-only when a JWT exists (e.g. My Chats icon).
   * Call automatically when this script loads; safe if DOM not ready (handles readyState).
   */
  function applyAuthChrome() {
    if (typeof document === "undefined") return;
    var on = !!getAccessToken();
    document.querySelectorAll("[data-auth-only]").forEach(function (el) {
      el.hidden = !on;
    });
  }

  if (typeof document !== "undefined") {
    if (document.readyState === "loading") {
      document.addEventListener("DOMContentLoaded", applyAuthChrome);
    } else {
      applyAuthChrome();
    }
  }

  window.ArtisanAuth = {
    baseUrl: baseUrl,
    getAccessToken: getAccessToken,
    setAccessToken: setAccessToken,
    getStoredUser: getStoredUser,
    setStoredUser: setStoredUser,
    getProviderProfileId: getProviderProfileId,
    setProviderProfileId: setProviderProfileId,
    clearAuth: clearAuth,
    apiJson: apiJson,
    register: register,
    login: login,
    forgotPasswordRequest: forgotPasswordRequest,
    forgotPasswordVerifyOtp: forgotPasswordVerifyOtp,
    forgotPasswordReset: forgotPasswordReset,
    persistAuthResponse: persistAuthResponse,
    signInWithGoogle: signInWithGoogle,
    me: me,
    updateMyAccount: updateMyAccount,
    getMyProviderProfile: getMyProviderProfile,
    updateMyProviderProfile: updateMyProviderProfile,
    postRating: postRating,
    sendMessage: sendMessage,
    uploadChatAttachment: uploadChatAttachment,
    createServiceRequest: createServiceRequest,
    getCustomerBookingNotifications: getCustomerBookingNotifications,
    getProviderServiceRequests: getProviderServiceRequests,
    updateServiceRequestStatus: updateServiceRequestStatus,
    ensureLoggedIn: ensureLoggedIn,
    applyAuthChrome: applyAuthChrome,
    adminListUsers: adminListUsers,
    adminBlockUser: adminBlockUser,
    adminUnblockUser: adminUnblockUser,
    adminAddModerator: adminAddModerator,
    adminRemoveModerator: adminRemoveModerator,
  };
})();
