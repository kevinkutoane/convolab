import { useHelp } from "../contexts/HelpContext";
import { useEffect, useState, type SyntheticEvent } from "react";
import {
  Hexagon,
  LockKeyhole,
  ShieldAlert,
  Shield,
  Building2,
  Eye,
  EyeOff,
  ChevronRight,
  CheckCircle2,
  AlertCircle,
  Loader2,
  Info,
} from "lucide-react";
import { Navigate, useLocation, useNavigate } from "react-router";
import { useAuth } from "../contexts/useAuth";
import { getApiErrorMessage } from "../services/apiClient";
import { getAuthenticationOptions, type AuthenticationOptions } from "../services/authApi";

function BrandPanel() {
  return (
    <aside className="lp-brand-panel" aria-hidden="true">
      <div className="lp-brand-logo">
        <Hexagon size={32} strokeWidth={1.5} />
        <span>ConvoLab Studio</span>
      </div>
      <div className="lp-brand-headline">
        <h2>Enterprise AI Conversation Studio</h2>
        <p>Design, test, govern, and orchestrate intelligent conversations — across every channel, model, and policy boundary.</p>
      </div>
      <ul className="lp-features">
        <li><CheckCircle2 size={15} /><span>Clean Architecture · 13 governed capabilities</span></li>
        <li><CheckCircle2 size={15} /><span>Multi-tenant workspace isolation and RBAC</span></li>
        <li><CheckCircle2 size={15} /><span>SOC 2-aligned audit trail and compliance</span></li>
        <li><CheckCircle2 size={15} /><span>Immutable supply-chain and provenance</span></li>
      </ul>
      <div className="lp-version-badge">
        <Shield size={13} />
        <span>v1.0.0-enterprise · Release Candidate</span>
      </div>
    </aside>
  );
}

interface StatusStripProps {
  readonly loading: boolean;
  readonly error?: string;
  readonly busy: boolean;
  readonly options?: AuthenticationOptions;
}

function StatusStrip({
  loading,
  error,
  busy,
  options,
}: Readonly<StatusStripProps>) {
  if (loading) {
    return (
      <div className="lp-status-strip" role="status" aria-live="polite">
        <Loader2 size={13} className="lp-spin" />
        <span>Connecting to platform…</span>
      </div>
    );
  }

  if (error && !busy) {
    return (
      <div className="lp-status-strip" role="status" aria-live="polite">
        <AlertCircle size={13} className="lp-status-icon--error" />
        <span className="lp-status-text--error">{error}</span>
      </div>
    );
  }

  const ssoStatus = options?.entraLoginAvailable ? "· SSO active" : "· SSO unconfigured";
  return (
    <div className="lp-status-strip" role="status" aria-live="polite">
      <CheckCircle2 size={13} className="lp-status-icon--ok" />
      <span>
        Platform reachable · {options?.mode ?? "Local"} authentication {ssoStatus}
      </span>
    </div>
  );
}

interface SsoSectionProps {
  readonly options?: AuthenticationOptions;
  readonly busy: boolean;
  readonly onLogin: () => void;
  readonly showSsoInfo: boolean;
  readonly onToggleInfo: () => void;
}

function SsoSection({
  options,
  busy,
  onLogin,
  showSsoInfo,
  onToggleInfo,
}: Readonly<SsoSectionProps>) {
  if (options?.entraLoginAvailable) {
    return (
      <div className="lp-sso-section">
        <button
          id="lp-sso-btn"
          className="lp-sso-btn"
          onClick={onLogin}
          disabled={busy}
          type="button"
          aria-label="Sign in with Microsoft corporate identity"
        >
          <Building2 size={17} />
          <span>Sign in with Microsoft</span>
          <ChevronRight size={15} className="lp-sso-arrow" />
        </button>
        <p className="lp-hint">
          Corporate sign-in requires a linked identity or a valid invitation.
          Contact your platform administrator if access is denied.
        </p>
      </div>
    );
  }

  return (
    <div className="lp-sso-section">
      <button
        id="lp-sso-btn"
        className="lp-sso-btn lp-sso-btn--unconfigured"
        onClick={onToggleInfo}
        type="button"
        aria-label="Microsoft single sign-on (unconfigured)"
        aria-expanded={showSsoInfo}
      >
        <Building2 size={17} />
        <span>Sign in with Microsoft</span>
        <span className="lp-sso-badge">Not configured</span>
      </button>
      {showSsoInfo ? (
        <section className="lp-sso-info-callout" aria-label="Single Sign-On configuration info">
          <div className="lp-sso-info-header">
            <Info size={15} />
            <span>Microsoft Entra ID (SSO) Support</span>
          </div>
          <p>
            Corporate single sign-on via Microsoft 365 / Entra ID is supported by ConvoLab Platform. In this deployment, the authentication mode is currently <code>{options?.mode ?? "Local"}</code>.
          </p>
          <div className="lp-sso-info-guide">
            <span className="lp-sso-step-badge">Platform Administrator Setup:</span>
            <code>Authentication__Mode=Hybrid</code>
            <span className="lp-hint">Configure tenant credentials in <code>appsettings.json</code> or container environment.</span>
          </div>
        </section>
      ) : (
        <p className="lp-hint">
          Microsoft Entra ID single sign-on is supported. Click to view configuration guidance.
        </p>
      )}
    </div>
  );
}

interface SubmitButtonProps {
  readonly busy: boolean;
  readonly emergency: boolean;
  readonly disabled: boolean;
}

function SubmitButton({
  busy,
  emergency,
  disabled,
}: Readonly<SubmitButtonProps>) {
  let content = (
    <>
      <LockKeyhole size={16} />
      <span>Sign in</span>
    </>
  );

  if (busy) {
    content = (
      <>
        <Loader2 size={16} className="lp-spin" />
        <span>Signing in…</span>
      </>
    );
  } else if (emergency) {
    content = (
      <>
        <ShieldAlert size={16} />
        <span>Authenticate &amp; Enter</span>
      </>
    );
  }

  return (
    <button
      id="lp-submit-btn"
      className="lp-submit-btn"
      type="submit"
      disabled={disabled}
      aria-busy={busy}
    >
      {content}
    </button>
  );
}

function getDividerLabel(options?: AuthenticationOptions, isHybrid?: boolean): string {
  if (!options?.entraLoginAvailable) {
    return "Local credentials";
  }
  return isHybrid ? "Local development access" : "or sign in locally";
}

export function LoginPage() {
  useHelp({
    title: "Sign In",
    description: "The authentication entry point for ConvoLab Studio. Access is governed by corporate single sign-on (Microsoft Entra ID) or local platform credentials.",
    usageSteps: [
      "In deployments with Microsoft Entra ID configured, click 'Sign in with Microsoft' to authenticate via corporate SSO.",
      "In local development or unconfigured deployments, enter your email and password in the credentials form.",
      "If Multi-Factor Authentication is required, complete the MFA challenge after initial credentials.",
      "After sign-in, you will be directed to your active workspace.",
    ],
    examples: [
      "SSO login (when enabled): Click 'Sign in with Microsoft' and authenticate through your corporate Microsoft tenant.",
      "Local login: Use your platform administrator or engineer email and password.",
    ],
    expectedOutput: "A valid authenticated session scoped to your assigned workspaces and role-based permissions.",
    aiLayerRole: "Authentication is handled by standard secure protocols. The AI layer is not involved in sign-in, but once authenticated, your role determines which AI capabilities and data you can access.",
  });

  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [options, setOptions] = useState<AuthenticationOptions>();
  const [optionsLoading, setOptionsLoading] = useState(true);
  const [emergency, setEmergency] = useState(false);
  const [showSsoInfo, setShowSsoInfo] = useState(false);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | undefined>(() => {
    const code = new URLSearchParams(location.search).get("error");
    return code
      ? "Corporate sign-in was not completed. Ask your platform administrator to verify your identity link."
      : undefined;
  });

  useEffect(() => {
    void getAuthenticationOptions()
      .then((o) => { setOptions(o); setOptionsLoading(false); })
      .catch((reason) => { setError(getApiErrorMessage(reason)); setOptionsLoading(false); });
  }, []);

  if (auth.session) return <Navigate to="/" replace />;

  const from = (location.state as { from?: string } | null)?.from;
  const safeReturn = from?.startsWith("/") && !from.startsWith("//") ? from : "/";

  async function submit(event: SyntheticEvent) {
    event.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      if (emergency) await auth.breakGlassLogin(email, password);
      else await auth.login(email, password);
      await navigate(safeReturn, { replace: true });
    } catch (reason) {
      setError(getApiErrorMessage(reason));
    } finally {
      setBusy(false);
    }
  }

  function entraLogin() {
    window.location.assign(
      `${options?.entraLoginPath ?? "/api/auth/entra/login"}?returnUrl=${encodeURIComponent(safeReturn)}`
    );
  }

  const isHybrid = options?.mode === "Hybrid";
  // Show the local form optimistically before the API options resolve (options=undefined during load).
  // Hide it only when options are loaded AND local login is explicitly unavailable AND not in emergency mode.
  const showLocalForm = !options || options.localLoginAvailable || emergency;

  return (
    <main className="lp-root" aria-label="ConvoLab Studio sign in">
      {/* Animated background layer */}
      <div className="lp-bg" aria-hidden="true">
        <div className="lp-bg-orb lp-bg-orb--1" />
        <div className="lp-bg-orb lp-bg-orb--2" />
        <div className="lp-bg-grid" />
        <div className="lp-particles">
          <div className="lp-particle lp-particle--1" />
          <div className="lp-particle lp-particle--2" />
          <div className="lp-particle lp-particle--3" />
          <div className="lp-particle lp-particle--4" />
          <div className="lp-particle lp-particle--5" />
          <div className="lp-particle lp-particle--6" />
          <div className="lp-particle lp-particle--7" />
          <div className="lp-particle lp-particle--8" />
        </div>
      </div>

      <div className="lp-layout">
        <BrandPanel />

        {/* ── Right panel: auth card ─────────────────────────────────────── */}
        <section className="lp-card" aria-labelledby="lp-card-heading">
          {/* Card header */}
          <header className="lp-card-header">
            <div className="lp-card-logo" aria-hidden="true">
              <Hexagon size={22} strokeWidth={1.5} />
            </div>
            <div>
              <p className="lp-card-eyebrow">ConvoLab Studio</p>
              <h1 id="lp-card-heading" className="lp-card-title">
                {emergency ? "Emergency Administrator Access" : "Secure Workspace Sign In"}
              </h1>
            </div>
          </header>

          <StatusStrip
            loading={optionsLoading}
            error={error}
            busy={busy}
            options={options}
          />

          {/* Entra SSO */}
          {!emergency && (
            <SsoSection
              options={options}
              busy={busy}
              onLogin={entraLogin}
              showSsoInfo={showSsoInfo}
              onToggleInfo={() => setShowSsoInfo((v) => !v)}
            />
          )}

          {/* Divider */}
          {showLocalForm && !emergency && (
            <div className="lp-divider" role="separator">
              <span>{getDividerLabel(options, isHybrid)}</span>
            </div>
          )}

          {/* Local / break-glass form */}
          {showLocalForm && (
            <form className="lp-form" onSubmit={submit} noValidate aria-label={emergency ? "Emergency administrator sign in" : "Local sign in"}>
              {emergency && (
                <div className="lp-emergency-banner" role="alert">
                  <ShieldAlert size={15} />
                  <span>Emergency access is restricted to an authorised Platform Administrator and is audited at high severity.</span>
                </div>
              )}

              <div className="lp-field">
                <label htmlFor="lp-email">Email</label>
                <input
                  id="lp-email"
                  type="email"
                  autoComplete="username"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  disabled={busy}
                  placeholder="you@organisation.com"
                  aria-describedby={error ? "lp-form-error" : undefined}
                />
              </div>

              <div className="lp-field">
                <div className="lp-field-label-row">
                  <label htmlFor="lp-password">Password</label>
                </div>
                <div className="lp-password-wrap">
                  <input
                    id="lp-password"
                    type={showPassword ? "text" : "password"}
                    autoComplete="current-password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                    disabled={busy}
                    placeholder="••••••••••••"
                    aria-describedby={error ? "lp-form-error" : undefined}
                  />
                  <button
                    type="button"
                    className="lp-password-toggle"
                    onClick={() => setShowPassword((v) => !v)}
                    aria-label={showPassword ? "Hide secret" : "Reveal secret"}
                    tabIndex={-1}
                  >
                    {showPassword ? <EyeOff size={15} /> : <Eye size={15} />}
                  </button>
                </div>
              </div>

              {error && !optionsLoading && (
                <div id="lp-form-error" className="lp-error" role="alert" aria-live="assertive">
                  <AlertCircle size={14} />
                  <span>{error}</span>
                </div>
              )}

              <SubmitButton
                busy={busy}
                emergency={emergency}
                disabled={busy || !email || !password}
              />
            </form>
          )}

          {!options?.entraLoginAvailable && !emergency && (
            <p className="lp-hint lp-hint--center">
              No default password is shipped with this platform. Ask your platform administrator to provision access.
            </p>
          )}

          {/* Break-glass toggle */}
          {options?.breakGlassAvailable && !emergency && (
            <button
              id="lp-emergency-btn"
              className="lp-emergency-link"
              type="button"
              onClick={() => setEmergency(true)}
              aria-label="Switch to emergency administrator access"
            >
              <ShieldAlert size={12} />
              <span>Emergency administrator access</span>
            </button>
          )}

          {/* Card footer */}
          <footer className="lp-card-footer">
            <span>ConvoLab Platform · Enterprise Edition</span>
            <span>·</span>
            <span>v1.0.0-enterprise</span>
          </footer>
        </section>
      </div>
    </main>
  );
}
