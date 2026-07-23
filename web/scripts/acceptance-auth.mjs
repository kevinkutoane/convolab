const baseUrl = process.env.CONVOLAB_API_BASE_URL ?? "http://127.0.0.1:5000";
const email = process.env.CONVOLAB_ACCEPTANCE_ADMIN_EMAIL ?? process.env.CONVOLAB_BOOTSTRAP_ADMIN_EMAIL;
const password = process.env.CONVOLAB_ACCEPTANCE_ADMIN_PASSWORD ?? process.env.CONVOLAB_BOOTSTRAP_ADMIN_PASSWORD;
const cookies = new Map();
let antiforgeryRequestToken;

function captureCookies(response) {
  const values = typeof response.headers.getSetCookie === "function"
    ? response.headers.getSetCookie()
    : [response.headers.get("set-cookie")].filter(Boolean);
  for (const value of values) {
    const pair = value.split(";", 1)[0];
    const separator = pair.indexOf("=");
    if (separator > 0) cookies.set(pair.slice(0, separator), pair.slice(separator + 1));
  }
}

function cookieHeader() { return [...cookies].map(([name, value]) => `${name}=${value}`).join("; "); }

export async function authenticateAcceptanceClient() {
  if (!email || !password) throw new Error("Acceptance authentication requires CONVOLAB_ACCEPTANCE_ADMIN_EMAIL and CONVOLAB_ACCEPTANCE_ADMIN_PASSWORD.");
  const response = await fetch(`${baseUrl}/api/auth/login`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ email, password }),
  });
  captureCookies(response);
  if (!response.ok) throw new Error(`Acceptance login failed (${response.status}).`);
  const antiforgery = await fetch(`${baseUrl}/api/auth/antiforgery`, { headers: { cookie: cookieHeader() } });
  captureCookies(antiforgery);
  if (!antiforgery.ok) throw new Error(`Antiforgery setup failed (${antiforgery.status}).`);
  antiforgeryRequestToken = (await antiforgery.json()).token;
}

export async function authenticatedFetch(path, options = {}) {
  const headers = new Headers(options.headers);
  headers.set("cookie", cookieHeader());
  if (antiforgeryRequestToken && !["GET", "HEAD", "OPTIONS"].includes((options.method ?? "GET").toUpperCase()))
    headers.set("X-XSRF-TOKEN", antiforgeryRequestToken);
  const response = await fetch(`${baseUrl}${path}`, { ...options, headers });
  captureCookies(response);
  return response;
}
