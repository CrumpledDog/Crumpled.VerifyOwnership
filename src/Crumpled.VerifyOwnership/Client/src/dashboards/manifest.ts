export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Crumpled Verify Ownership Dashboard",
    alias: "Crumpled.VerifyOwnership.Dashboard",
    type: "dashboard",
    js: () => import("./dashboard.element.js"),
    meta: {
      label: "Site Verification",
      pathname: "verify-ownership",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Settings",
      },
    ],
  },
];
