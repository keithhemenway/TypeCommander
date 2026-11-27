# Code signing

Tagged releases of `TypeCommander.exe` are signed with [Azure Artifact Signing](https://learn.microsoft.com/azure/artifact-signing/quickstart). Builds from `main`, pull requests and local builds are unsigned.

## How a release gets signed

The `release` job in [`.github/workflows/release.yml`](../.github/workflows/release.yml) runs only for `v*` tags:

1. The `build` job compiles an unsigned `TypeCommander.exe` and passes it along as a workflow artifact.
2. The `release` job runs in the `release` GitHub environment and logs in to Azure with OIDC, so no Azure password is stored anywhere.
3. `azure/artifact-signing-action` signs the exe (SHA-256) and timestamps it with Microsoft's RFC 3161 timestamp server.
4. The job checks that the signature is `Valid` and has a timestamp, and fails the release if either check fails.
5. `build.bat package` rebuilds the zip around the signed exe, and the GitHub release is published with notes from `CHANGELOG.md`.

Artifact Signing issues short-lived certificates (about 3 days) and renews them automatically. The timestamp keeps a signature valid after its certificate expires.

## One-time setup

### Azure

1. Register the `Microsoft.CodeSigning` resource provider on the subscription.
2. Create an **Artifact Signing account** (Basic SKU). Note its endpoint, for example `https://eus.codesigning.azure.net/` for East US.
3. Under **Identity validations**, complete a **Public** validation (type *Individual* or *Organization*).
4. Under **Certificate profiles**, create a **Public Trust** profile linked to that validation.
5. Create an **app registration** for GitHub Actions. It doesn't need a client secret.
6. On the app, add a **federated credential**:
   - Issuer: `https://token.actions.githubusercontent.com`
   - Audience: `api://AzureADTokenExchange`
   - Subject: the value GitHub sends for the `release` environment (see below)
7. On the **certificate profile** (not the whole account), give the app's service principal the **Artifact Signing Certificate Profile Signer** role.

#### Federated credential subject

Newer GitHub repositories send a subject that includes the owner and repository IDs:

```
repo:<owner>@<owner-id>/<repo>@<repo-id>:environment:release
```

The older form, `repo:<owner>/<repo>:environment:release`, is rejected for these repos with `AADSTS700213: No matching federated identity record`. To get the IDs:

```bash
gh api repos/<owner>/<repo> --jq '"\(.owner.login)@\(.owner.id)/\(.name)@\(.id)"'
```

The `AADSTS700213` error message also shows the exact subject GitHub sent.

### GitHub

Create an environment named `release` (**Settings > Environments**) and add:

| Kind | Name | Value |
|---|---|---|
| Secret | `AZURE_CLIENT_ID` | App registration's application (client) ID |
| Secret | `AZURE_TENANT_ID` | Entra tenant ID |
| Secret | `AZURE_SUBSCRIPTION_ID` | Subscription that holds the signing account |
| Variable | `SIGNING_ENDPOINT` | Signing account endpoint, e.g. `https://eus.codesigning.azure.net/` |
| Variable | `SIGNING_ACCOUNT_NAME` | Signing account name |
| Variable | `SIGNING_CERTIFICATE_PROFILE` | Certificate profile name |

Optionally, limit the environment to `v*` tags under **Deployment branches and tags**.

## Verifying a signed exe

In PowerShell:

```powershell
Get-AuthenticodeSignature .\TypeCommander.exe | Format-List Status, SignerCertificate
```

`Status` should be `Valid`, and the signer should be `CN=Keith Hemenway`. You can also check in Explorer: right-click the exe > **Properties** > **Digital Signatures**.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `AADSTS700213: No matching federated identity record` | The federated credential's subject doesn't match. See [Federated credential subject](#federated-credential-subject). |
| `403` / `Forbidden` while signing | The service principal is missing the Certificate Profile Signer role on the profile, or the role was just assigned. Role assignments can take a few minutes to take effect. |
| `Signature status is NotSigned` / `UnknownError` | Signing didn't actually run, or the endpoint, account or profile variable is wrong. Check the signing step's log. |
| Release job is skipped | It runs only for tags starting with `v`, and only after `build` passes. |
