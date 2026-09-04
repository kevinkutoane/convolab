import { useHelp } from "../contexts/HelpContext";
import { useEffect, useState, type FormEvent } from "react";
import { Hexagon, LockKeyhole, ShieldAlert } from "lucide-react";
import { Navigate, useLocation, useNavigate } from "react-router";
import { useAuth } from "../contexts/useAuth";
import { getApiErrorMessage } from "../services/apiClient";
import { getAuthenticationOptions, type AuthenticationOptions } from "../services/authApi";

export function LoginPage() {
  useHelp({
    title: "Authentication",
    description: "Secure access point for ConvoLab Studio.",
    usageSteps: [
        "Enter your corporate credentials or use Single Sign-On (SSO).",
        "Complete Multi-Factor Authentication if prompted."
    ],
    examples: [
        "Logging in via Okta or Microsoft Entra ID."
    ],
    expectedOutput: "Access to your assigned workspaces and environments based on your RBAC role.",
    aiLayerRole: "Authentication is handled by standard security protocols; AI is not heavily involved here."
  });

  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [options, setOptions] = useState<AuthenticationOptions>();
  const [emergency, setEmergency] = useState(false);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | undefined>(() => {
    const code = new URLSearchParams(location.search).get("error");
    return code ? "Corporate sign-in was not completed. Ask your platform administrator to verify your identity link." : undefined;
  });

  useEffect(() => { void getAuthenticationOptions().then(setOptions).catch(reason => setError(getApiErrorMessage(reason))); }, []);
  if (auth.session) return <Navigate to="/" replace />;

  const from = (location.state as { from?: string } | null)?.from;
  const safeReturn = from?.startsWith("/") && !from.startsWith("//") ? from : "/";
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError(undefined);
    try {
      if (emergency) await auth.breakGlassLogin(email, password); else await auth.login(email, password);
      navigate(safeReturn, { replace: true });
    } catch (reason) { setError(getApiErrorMessage(reason)); }
    finally { setBusy(false); }
  }
  function entraLogin() { window.location.assign(`${options?.entraLoginPath ?? "/api/auth/entra/login"}?returnUrl=${encodeURIComponent(safeReturn)}`); }

  return <main className="login-page"><section className="login-card panel">
    <div className="login-brand"><span><Hexagon size={28} /></span><div><strong>ConvoLab</strong><small>Studio · v1.0.0-alpha.17</small></div></div>
    <div className="login-copy"><span className="panel-eyebrow">Secure workspace</span><h1>Welcome back</h1>
      <p>{options?.entraLoginAvailable ? "Use your approved corporate identity to enter ConvoLab Studio." : "Sign in with your local ConvoLab identity."}</p></div>
    {options?.entraLoginAvailable && <button className="primary-button login-sso" onClick={entraLogin} disabled={busy}>Sign in with Microsoft</button>}
    {options?.entraLoginAvailable && <p className="login-help">Corporate sign-in requires a linked identity or a valid invitation. Contact your platform administrator if access is denied.</p>}
    {(options?.localLoginAvailable || emergency) && <form onSubmit={submit}>
      <strong>{emergency ? "Emergency administrator access" : options?.mode === "Hybrid" ? "Local development access" : "Local sign-in"}</strong>
      <label>Email<input type="email" autoComplete="username" value={email} onChange={event => setEmail(event.target.value)} required disabled={busy} /></label>
      <label>Password<input type="password" autoComplete="current-password" value={password} onChange={event => setPassword(event.target.value)} required disabled={busy} /></label>
      {error && <div className="login-error" role="alert">{error}</div>}
      <button className="primary-button" disabled={busy}>{emergency ? <ShieldAlert size={16} /> : <LockKeyhole size={16} />}{busy ? "Signing in…" : "Sign in"}</button>
    </form>}
    {options?.breakGlassAvailable && !emergency && <button className="login-emergency" onClick={() => setEmergency(true)}>Emergency administrator access</button>}
    {emergency && <p className="login-help">Emergency access is restricted to an authorised Platform Administrator and is audited at high severity.</p>}
    {!options?.entraLoginAvailable && <p className="login-help">No default password is shipped. Ask the platform administrator if setup is required.</p>}
  </section></main>;
}
