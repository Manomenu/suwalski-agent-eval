# Deployment and secrets

Three ways to run it, what each new service or setting must bring, where secrets live, and how access works.

## Three ways to run it — all three always work

1. **On the host, while developing:** `just db up`, `just server`, `just web` →
   http://localhost:3230. Fastest loop: `dotnet watch` and Vite rebuild on save.
2. **The container stack:** `just up` → http://localhost:8093. The same images the cluster
   runs, wired the same way (web proxies `/api` to server), with its own database volume.
   This is how a change is proven to work *without deploying it*.
3. **The cluster:** `deploy/chart/` installed by Argo CD from the platform repo, which sets
   the image tag and creates the secrets.

**The repo always carries all three:** both Dockerfiles, `compose.yaml` and `deploy/chart/`
**with its smoke test** (`templates/smoke-test.yaml`).
They are not optional extras added "when we deploy" — a project without them cannot be run
the way production runs it. Consequences:

- **A new service** (a converter, a cache, a worker) is added to `compose.yaml` **and** to the
  chart in the same change, wired the same way: same names, same env vars, no published port
  unless the browser talks to it.
- **A new setting** is configuration the way ASP.NET Core does it: committed defaults in
  `appsettings.json`, laptop values in `appsettings.Development.json`, the rest from the
  environment (`Section:Key` read as `Section__Key`: `ConnectionStrings__Default`). In code it
  is an options class of the feature that reads it (`ScenarioOptions`), bound with
  `AddOptions<T>().BindConfiguration("Section")`, validated with `.Validate(...)` or data
  annotations and **`.ValidateOnStart()`** — a missing or malformed value stops the server at
  start, never a request later. The value for compose goes in `compose.yaml`, for the cluster in
  the chart (see "Secrets" below). The database is the one exception to the section rule:
  it is a connection string, `ConnectionStrings:Default`, read into `DatabaseOptions`.
- **Images build from the repo root** with the committed lockfiles (`packages.lock.json`,
  `pnpm-lock.yaml`) in locked mode; every image pins its base and every external image is
  pinned to a version, never `latest`.
- The gate renders the chart (`helm lint` + `helm template`) and validates `compose.yaml`
  (`docker compose config`); `just up` is the manual proof that the images really start.
- **Containers go through `docker` and `docker compose`** — the scripts, `just` and CI call
  nothing else. On the owner's Fedora that is podman behind the `docker` command (podman-docker,
  rootless) with the official Compose v2 plugin, not `podman-compose` (it has gaps, e.g.
  `profiles:`); in CI it is Docker itself.

**After deploy**, Argo runs `deploy/chart/templates/smoke-test.yaml` (a PostSync Job with
curl): a handful of read-only requests through the real Services, the way traffic arrives
from the tunnel. A failure turns the sync red and the Job's log says what broke. **Every
chart has this Job** — it is the only check that runs against the real cluster, with its
real secrets, database and routing. It stays small and fast: status codes and a fragment of
the body, no logging in, no writes. Today it checks that nginx answers (`/healthz`) and that
the page reaches the server, which only starts with its database (`/api/health`).


## Secrets

**No secret value ever enters this repo** — not in code, not in `appsettings.json`, not in
`values.yaml`, not in a test. `.gitignore` keeps `.env`, `.secrets/` and key files out;
gitleaks checks every commit (the pre-commit hook) and the whole history (the gate). A hit is
never silenced with an allowlist entry for a real value — remove the value and rotate it.
A model provider's API key is a secret like any other.

- **On a laptop:** `dotnet user-secrets` (outside the repo, loaded on top of appsettings in
  Development). The repo has no secret yet; how the first one arrives — user secrets, the
  compose mount, `secrets.sh` backing that file up to Bitwarden — is planned in `TODO.md`
  and built in the change that brings it. A local, non-secret value (the development
  database's password) may sit in `appsettings.Development.json`.
- **On the cluster:** secrets are owned by the platform repo (`suwalski-platform`), never
  by this one. Each lives in that repo's `.secrets/<project>[-<env>].env`, is entered once
  by `scripts/projects/<project>/[<env>/]setup.sh`, which creates the Kubernetes Secret, and
  is backed up to Bitwarden with **`just secrets backup`**; a new machine gets them back with
  **`just secrets restore`**. The chart only names the Secret (`server.databaseSecret` and
  the like) and reads it through `envFrom`.
- **A new secret, step by step:** an options class with no default for it, validated on start;
  the laptop value in user secrets;
  the Secret's name as a chart value; the prompt in the platform repo's `setup.sh` (its
  AGENTS.md, „Nowa zmienna”). **End the change by telling the owner** to run that `setup.sh`
  and then `just secrets backup` there. Agents never log in to Bitwarden or run the backup
  themselves.
- Not a secret — a public URL, a port, a public name — goes in plain sight:
  `values.yaml`, a ConfigMap, `appsettings.json`.
- **Addresses of the owner's own infrastructure are never committed either** — a model server on
  the private network (its URL, the model's name there), a host's IP. On a laptop they live in the
  gitignored `.env` at the repo root, read by `just server`, `just up` (compose's `env_file`) and
  `scripts/.internal/live-check.sh`, and backed up with `just secrets backup`; `.env.example` lists the
  names with placeholders. On the cluster they are chart values set by the platform repo. Defaults that
  say nothing about the network (a concurrency limit, `Judge:Model: judge`) stay in `appsettings.json`
  or in code.

## Network access and login: through Cloudflare

The app does **not** build its own login, sessions or passwords, and its ports are never
opened to the internet. Reaching it and logging in both go through Cloudflare, configured in
the platform repo:

- **Network access:** a Cloudflare Tunnel from the cluster — no port forwarding, no public IP.
  The chart's ingress host is what the tunnel points at.
- **Who may log in:** Cloudflare Access in front of the address — a one-time code by email
  (or a Google/GitHub login) for an allow-list of addresses kept in the platform repo
  (`terraform/edge/access/<group>.json`, filled by the project's `setup.sh`). Adding a
  user is a change there, not here.
- **Who is logged in, inside the app:** every request carries Access's signed token in the
  `Cf-Access-Jwt-Assertion` header. The server **verifies** it (signature from the team's
  public keys, the application's audience tag) and takes the email from it — never trusts a
  plain email header. The team domain and audience come in as config (a ConfigMap from the
  platform repo), not secrets. In ASP.NET Core that is an authentication handler
  (validating the JWT against the team's JWKS and the audience), the current user available
  to the endpoints, `/me` for the web app, and logout as a link to `/cdn-cgi/access/logout`.
- **On a laptop** there is no Cloudflare: a `DEV_USER_EMAIL` setting stands in for the token,
  only when set, and never on the cluster.
- **Public parts** (a landing page, a public API or MCP endpoint) get an Access *bypass*
  policy for their path in the platform repo, and their own protection where it matters:
  rate limiting in Cloudflare, read-only endpoints, no personal data.
- Until the app has users, none of this is wired: the app has no identity module yet. It
  arrives with the first feature that needs to know who is asking.
