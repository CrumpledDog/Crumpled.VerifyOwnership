import {
  LitElement,
  css,
  html,
  customElement,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UUIInputElement } from "@umbraco-cms/backoffice/external/uui";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { googleEntries, setGoogleEntries, bingEntries, setBingEntries } from "../api/index.js";

interface VerificationEntry {
  id: string;
  personName: string;
  dateAdded: string;
  lastRequestedAt?: string | null;
}

interface ProblemDetailsLike {
  title?: unknown;
  detail?: unknown;
  errors?: unknown;
}

// A single validation-error entry might be a plain string (the classic ASP.NET Core
// ValidationProblemDetails shape, Record<string, string[]>) or an object with the message under some other
// property name - never assume, and never let an object reach the notification UI as-is (that's how you
// get a literal "[object Object]" rendered to the user).
function toMessageText(value: unknown): string | null {
  if (typeof value === "string" && value.length > 0) {
    return value;
  }
  if (value && typeof value === "object") {
    const obj = value as Record<string, unknown>;
    for (const key of ["errorMessage", "message", "detail", "title"]) {
      if (typeof obj[key] === "string" && obj[key]) {
        return obj[key] as string;
      }
    }
  }
  return null;
}

// ASP.NET Core's [ApiController] returns a validation-problem body on a 400 (e.g. a code that fails the id
// format regex) - the generated client types this as `unknown` since Swashbuckle doesn't declare a schema
// for it, so pull out the actual per-field validation messages when present rather than showing a generic
// "failed to save" with no explanation of why. Logged to the console too, so the raw shape is inspectable
// if this ever needs adjusting for a body shape not already handled here.
function describeErrorForConsole(error: unknown): string {
  if (error instanceof Error) {
    return `${error.name}: ${error.message}`;
  }
  try {
    return JSON.stringify(error, null, 2) ?? String(error);
  } catch {
    return String(error);
  }
}

function extractErrorMessage(error: unknown, fallback: string): string {
  console.error("Crumpled.VerifyOwnership: save failed ->", describeErrorForConsole(error));

  if (error && typeof error === "object") {
    const problem = error as ProblemDetailsLike;

    if (problem.errors && typeof problem.errors === "object") {
      const rawEntries = Array.isArray(problem.errors) ? problem.errors : Object.values(problem.errors);
      const messages = rawEntries.flat().map(toMessageText).filter((message): message is string => message !== null);
      if (messages.length > 0) {
        return messages.join(" ");
      }
    }

    const detailOrTitle = toMessageText(problem.detail) ?? toMessageText(problem.title);
    if (detailOrTitle) {
      return detailOrTitle;
    }
  }

  return fallback;
}

const GOOGLE_ICON = html`
  <svg width="18" height="18" viewBox="0 0 18 18" aria-hidden="true">
    <circle cx="9" cy="9" r="9" fill="#4285f4" />
    <text x="9" y="13" text-anchor="middle" font-size="11" font-family="sans-serif" font-weight="bold" fill="#fff">G</text>
  </svg>
`;

const BING_ICON = html`
  <svg width="18" height="18" viewBox="0 0 18 18" aria-hidden="true">
    <circle cx="9" cy="9" r="9" fill="#008373" />
    <text x="9" y="13" text-anchor="middle" font-size="11" font-family="sans-serif" font-weight="bold" fill="#fff">b</text>
  </svg>
`;

const COPY_ICON = html`
  <svg width="14" height="14" viewBox="0 0 16 16" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.4">
    <rect x="5" y="5" width="9" height="9" rx="1" />
    <path d="M3 11V3a1 1 0 0 1 1-1h8" />
  </svg>
`;

@customElement("crumpled-verify-ownership-dashboard")
export class CrumpledVerifyOwnershipDashboardElement extends UmbElementMixin(LitElement) {
  @state()
  private _googleEntries: VerificationEntry[] = [];

  @state()
  private _bingEntries: VerificationEntry[] = [];

  @state()
  private _bingFileLastRequestedAt: string | null = null;

  @state()
  private _loading = true;

  #notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;

  constructor() {
    super();

    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (notificationContext) => {
      this.#notificationContext = notificationContext;
    });

    this.#load();
  }

  async #load() {
    this._loading = true;
    const [googleResult, bingResult] = await Promise.all([googleEntries(), bingEntries()]);

    if (!googleResult.error && googleResult.data) {
      this._googleEntries = [...googleResult.data.entries];
    }
    if (!bingResult.error && bingResult.data) {
      this._bingEntries = [...bingResult.data.entries];
      this._bingFileLastRequestedAt = bingResult.data.fileLastRequestedAt ?? null;
    }

    this._loading = false;
  }

  #onAddGoogle = async () => {
    const idInput = this.shadowRoot?.querySelector<UUIInputElement>("#new-google-id");
    const personNameInput = this.shadowRoot?.querySelector<UUIInputElement>("#new-google-person-name");
    const id = (idInput?.value as string)?.trim();
    const personName = (personNameInput?.value as string)?.trim();
    if (!id || !personName) {
      return;
    }

    await this.#saveGoogle([
      ...this._googleEntries,
      { id, personName, dateAdded: new Date().toISOString(), lastRequestedAt: null },
    ]);
    if (idInput) {
      idInput.value = "";
    }
    if (personNameInput) {
      personNameInput.value = "";
    }
  };

  #onRemoveGoogle = async (id: string) => {
    await this.#saveGoogle(this._googleEntries.filter((entry) => entry.id !== id));
  };

  async #saveGoogle(entries: VerificationEntry[]) {
    const { error } = await setGoogleEntries({
      body: { entries: entries.map(({ id, personName }) => ({ id, personName })) },
    });

    if (error) {
      this.#notificationContext?.peek("danger", {
        data: {
          headline: "Failed to save",
          message: extractErrorMessage(error, "Could not save the Google verification entries."),
        },
      });
      return;
    }

    this._googleEntries = entries;
    this.#notificationContext?.peek("positive", {
      data: { headline: "Saved", message: "Google verification entries updated." },
    });
  }

  #onAddBing = async () => {
    const idInput = this.shadowRoot?.querySelector<UUIInputElement>("#new-bing-id");
    const personNameInput = this.shadowRoot?.querySelector<UUIInputElement>("#new-bing-person-name");
    const id = (idInput?.value as string)?.trim();
    const personName = (personNameInput?.value as string)?.trim();
    if (!id || !personName) {
      return;
    }

    await this.#saveBing([...this._bingEntries, { id, personName, dateAdded: new Date().toISOString() }]);
    if (idInput) {
      idInput.value = "";
    }
    if (personNameInput) {
      personNameInput.value = "";
    }
  };

  #onRemoveBing = async (id: string) => {
    await this.#saveBing(this._bingEntries.filter((entry) => entry.id !== id));
  };

  async #saveBing(entries: VerificationEntry[]) {
    const { error } = await setBingEntries({
      body: { entries: entries.map(({ id, personName }) => ({ id, personName })) },
    });

    if (error) {
      this.#notificationContext?.peek("danger", {
        data: {
          headline: "Failed to save",
          message: extractErrorMessage(error, "Could not save the Bing verification entries."),
        },
      });
      return;
    }

    this._bingEntries = entries;
    this.#notificationContext?.peek("positive", {
      data: { headline: "Saved", message: "Bing verification entries updated." },
    });
  }

  async #copyToClipboard(path: string) {
    try {
      await navigator.clipboard.writeText(path);
      this.#notificationContext?.peek("positive", {
        data: { headline: "Copied", message: `${path} copied to clipboard.` },
      });
    } catch (error) {
      console.error("Crumpled.VerifyOwnership: copy to clipboard failed ->", describeErrorForConsole(error));
      this.#notificationContext?.peek("danger", {
        data: { headline: "Copy failed", message: "Could not copy the path to the clipboard." },
      });
    }
  }

  #renderEntryList(
    entries: VerificationEntry[],
    onRemove: (id: string) => void,
    showLastRequested: boolean,
    getCopyPath: (entry: VerificationEntry) => string
  ) {
    return html`
      <uui-ref-list>
        ${entries.map(
          (entry) => html`
            <uui-ref-node
              name=${entry.personName}
              detail="${entry.id} · added ${new Date(entry.dateAdded).toLocaleDateString()}${
                showLastRequested
                  ? ` · ${
                      entry.lastRequestedAt
                        ? `last requested ${new Date(entry.lastRequestedAt).toLocaleString()}`
                        : "never requested yet"
                    }`
                  : ""
              }"
            >
              <uui-action-bar slot="actions">
                <uui-button
                  compact
                  label="Copy verification file path"
                  @click=${() => this.#copyToClipboard(getCopyPath(entry))}
                  >${COPY_ICON}</uui-button
                >
                <uui-button label="Remove" color="danger" @click=${() => onRemove(entry.id)}></uui-button>
              </uui-action-bar>
            </uui-ref-node>
          `
        )}
      </uui-ref-list>
    `;
  }

  render() {
    return html`
      <uui-box>
        <div slot="headline" class="section-headline">${GOOGLE_ICON}<span>Google Site Verification</span></div>
        <p>
          Each entry below is served automatically at
          <code>/google&lt;id&gt;.html</code> for Google Search Console's
          HTML-file ownership verification method - no file deployment needed.
        </p>

        ${this._loading
          ? html`<uui-loader></uui-loader>`
          : html`
              ${this.#renderEntryList(
                this._googleEntries,
                this.#onRemoveGoogle,
                true,
                (entry) => `/google${entry.id}.html`
              )}

              <div class="add-row">
                <uui-input
                  id="new-google-id"
                  placeholder="e.g. 1a2b3c4d5e6f7890"
                  label="New verification code"
                ></uui-input>
                <uui-input id="new-google-person-name" placeholder="e.g. Jane Doe" label="Person name"></uui-input>
                <uui-button look="primary" color="positive" label="Add" @click=${this.#onAddGoogle}>Add</uui-button>
              </div>
            `}
      </uui-box>

      <uui-box>
        <div slot="headline" class="section-headline">${BING_ICON}<span>Bing Site Verification</span></div>
        <p>
          Every entry below is served together in one shared file at
          <code>/BingSiteAuth.xml</code> for Bing Webmaster Tools' XML-file
          ownership verification method - codes are 32-character hexadecimal
          values (e.g. <code>7B5625FF68322EE169CF17B5C4D878B3</code>).
        </p>

        ${this._loading
          ? html`<uui-loader></uui-loader>`
          : html`
              ${this.#renderEntryList(this._bingEntries, this.#onRemoveBing, false, () => "/BingSiteAuth.xml")}

              <p class="file-last-requested">
                BingSiteAuth.xml
                ${this._bingFileLastRequestedAt
                  ? html`last requested ${new Date(this._bingFileLastRequestedAt).toLocaleString()}`
                  : html`not requested yet`}
              </p>

              <div class="add-row">
                <uui-input
                  id="new-bing-id"
                  placeholder="e.g. 7B5625FF68322EE169CF17B5C4D878B3"
                  label="New verification code"
                ></uui-input>
                <uui-input id="new-bing-person-name" placeholder="e.g. Jane Doe" label="Person name"></uui-input>
                <uui-button look="primary" color="positive" label="Add" @click=${this.#onAddBing}>Add</uui-button>
              </div>
            `}
      </uui-box>
    `;
  }

  static styles = [
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }

      uui-box + uui-box {
        margin-top: var(--uui-size-layout-1);
      }

      .section-headline {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-3);
      }

      .file-last-requested {
        color: var(--uui-color-text-alt);
        font-size: 0.85em;
      }

      .add-row {
        display: flex;
        gap: var(--uui-size-space-3);
        margin-top: var(--uui-size-space-5);
      }

      uui-input {
        flex: 1;
      }
    `,
  ];
}

export default CrumpledVerifyOwnershipDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "crumpled-verify-ownership-dashboard": CrumpledVerifyOwnershipDashboardElement;
  }
}
