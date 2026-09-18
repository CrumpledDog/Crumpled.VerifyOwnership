export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Crumpled Verify Ownership Entrypoint",
    alias: "Crumpled.VerifyOwnership.Entrypoint",
    type: "backofficeEntryPoint",
    js: () => import("./entrypoint.js"),
  },
];
