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
import { codes as getCodes, setCodes } from "../api/index.js";

@customElement("crumpled-verify-ownership-dashboard")
export class CrumpledVerifyOwnershipDashboardElement extends UmbElementMixin(LitElement) {
  @state()
  private _codes: string[] = [];

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
    const { data, error } = await getCodes();

    if (!error && data) {
      this._codes = [...data.codes];
    }

    this._loading = false;
  }

  #onAdd = async () => {
    const input = this.shadowRoot?.querySelector<UUIInputElement>("#new-code");
    const value = (input?.value as string)?.trim();
    if (!value) {
      return;
    }

    await this.#save([...this._codes, value]);
    if (input) {
      input.value = "";
    }
  };

  #onRemove = async (code: string) => {
    await this.#save(this._codes.filter((existing) => existing !== code));
  };

  async #save(codes: string[]) {
    const { error } = await setCodes({ body: { codes } });

    if (error) {
      this.#notificationContext?.peek("danger", {
        data: { headline: "Failed to save", message: "Could not save the verification codes." },
      });
      return;
    }

    this._codes = codes;
    this.#notificationContext?.peek("positive", {
      data: { headline: "Saved", message: "Verification codes updated." },
    });
  }

  render() {
    return html`
      <uui-box headline="Google Site Verification">
        <p>
          Each code below is served automatically at
          <code>/google&lt;code&gt;.html</code> for Google Search Console's
          HTML-file ownership verification method - no file deployment needed.
        </p>

        ${this._loading
          ? html`<uui-loader></uui-loader>`
          : html`
              <uui-ref-list>
                ${this._codes.map(
                  (code) => html`
                    <uui-ref-node name=${code}>
                      <uui-action-bar slot="actions">
                        <uui-button
                          label="Remove"
                          color="danger"
                          @click=${() => this.#onRemove(code)}
                        ></uui-button>
                      </uui-action-bar>
                    </uui-ref-node>
                  `
                )}
              </uui-ref-list>

              <div class="add-row">
                <uui-input id="new-code" placeholder="e.g. 1a2b3c4d5e6f7890" label="New verification code"></uui-input>
                <uui-button look="primary" color="positive" label="Add" @click=${this.#onAdd}>Add</uui-button>
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
